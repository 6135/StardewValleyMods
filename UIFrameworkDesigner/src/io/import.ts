import { parse, printParseErrorCode, visit, type ParseError } from 'jsonc-parser';
import type { DesignerDocument, DesignerNode, ImportCandidate, ImportResult, NodeId, Problem, TemplateDoc } from '../model/document';
import { createNode, newNodeId } from '../model/factory';
import { canonicalType, elementTypes, tooltipBlockTypes } from '../model/metadata';
import { subItemsOf, type SubItemKind } from '../model/subItems';
import {
  createWorkspace, DefaultOwner, menuTab, type OwnerDoc, type PatchMembers, type TooltipDoc, type Workspace, type WorkspaceTab
} from '../model/workspace';
import {
  builtInTypes,
  canonicalMember,
  entryPath,
  fieldPath,
  getMember,
  indexPath,
  inferType,
  isObject,
  scalarText,
  suggest,
  templateMatcher,
  typeKind,
  valueShorthands,
  type JsonObject
} from './dataFormat';

// JSONC import (architecture.md §9.1, §14 phase 2): a CP content.json (every EditData patch of the Menus asset), an
// ImportData file ({ "Menus": {...}, "Owner": {...} }), a bare Menus object or a standalone MenuDefinition ("From" file),
// converted to DesignerDocuments, and the whole text as a workspace (§18.4: each Owners entry becomes an owner tab plus
// a tab per template and tooltip). Shorthands are expanded like DataValidator.NormalizeType and remembered on the node.

const MenusAsset = 'Mods/6135.UIFramework/Menus';
const OwnersAsset = 'Mods/6135.UIFramework/Owners';

/** The members of an ImportData root (DataImport.Import); a root with none of them is a Menus object. */
const ImportDataMembers = ['menus', 'huds', 'sprites', 'composites', 'contributions', 'owner', 'owners'];

/** The `$designer` member a saved / shared document carries (architecture.md §11); the framework ignores it. */
export const DesignerMember = '$designer';

/**
 * Parse `text` (JSON with comments and trailing commas) and return every menu it holds, and all of it as a workspace.
 * `knownTemplates` (owner, lower case, to template names) adds owner templates defined outside the text, so instances of
 * them are not read as unknown types.
 */
export function importText(text: string, knownTemplates: ReadonlyMap<string, ReadonlySet<string>> = new Map()): ImportResult {
  const problems: Problem[] = [];
  let hadComments = false;
  visit(text, { onComment: () => { hadComments = true; } }, { allowTrailingComma: true, disallowComments: false });

  const errors: ParseError[] = [];
  const root: unknown = parse(text, errors, { allowTrailingComma: true, disallowComments: false, allowEmptyContent: false });
  for (const e of errors) {
    const { line, column } = lineColumn(text, e.offset);
    problems.push({ severity: 'error', path: '', message: `line ${line}, column ${column}: ${describeParseError(printParseErrorCode(e.error))}.` });
  }

  const found: Found = { candidates: [], tabs: [], content: {}, otherChanges: [] };
  if (isObject(root)) {
    collect(root, found, problems, knownTemplates);
  }

  if (found.tabs.length === 0) {
    problems.push({
      severity: 'error',
      path: '',
      message: 'no menu found: expected a Content Patcher content.json with an EditData patch of Mods/6135.UIFramework/Menus, a Menus object ("<owner>/<menu id>": {...}), an ImportData file ({ "Menus": {...} }) or a single menu definition.'
    });
  }

  const first = found.tabs.find(t => t.kind === 'menu') ?? found.tabs[0];
  const workspace: Workspace | null = first
    ? { ...createWorkspace(found.tabs), activeTab: first.id, content: found.content, otherChanges: found.otherChanges }
    : null;
  const designer = isObject(root) ? getMember(root, DesignerMember) : undefined;
  return designer !== undefined
    ? { candidates: found.candidates, workspace, problems, hadComments, designer }
    : { candidates: found.candidates, workspace, problems, hadComments };
}

/** What an import collects: the menus (single-menu picker) and every tab, plus what the workspace keeps verbatim. */
interface Found {
  candidates: ImportCandidate[];
  tabs: WorkspaceTab[];
  content: Record<string, unknown>;
  otherChanges: unknown[];
}

/** EditData members the workspace export writes itself; the others (LogName, When …) are kept with the tab. */
const PatchOwnMembers = ['action', 'target', 'entries'];
/** EditData members that edit entries in other ways: such a patch is kept verbatim, not opened as tabs. */
const PatchOtherEdits = ['targetfield', 'fields', 'moveentries', 'textoperations'];

function collect(root: JsonObject, found: Found, problems: Problem[], known: ReadonlyMap<string, ReadonlySet<string>>): void {
  const changesMember = getMember(root, 'Changes');
  const changes = Array.isArray(changesMember) ? changesMember : isEditData(root, MenusAsset) ? [root] : null;
  if (changes !== null) {
    // a Content Patcher content.json (or one EditData patch): owner templates first (instances of them are not unknown types), then the menus
    if (Array.isArray(changesMember)) {
      for (const [key, value] of Object.entries(root)) {
        if (key !== 'Changes' && key !== DesignerMember) {
          found.content[key] = value;
        }
      }
    }

    const ownerTemplates = copyTemplates(known);
    changes.forEach(change => {
      if (isEditData(change, OwnersAsset)) {
        const entries = getMember(change, 'Entries');
        if (isObject(entries)) {
          for (const [owner, def] of Object.entries(entries)) {
            addOwnerTemplates(ownerTemplates, owner, def);
          }
        }
      }
    });

    changes.forEach((change, i) => {
      const entries = isObject(change) ? getMember(change, 'Entries') : undefined;
      const editable = isObject(change) && isObject(entries) && !Object.keys(change).some(k => PatchOtherEdits.includes(k.toLowerCase()));
      const patch: PatchMembers = editable ? Object.fromEntries(Object.entries(change).filter(([k]) => !PatchOwnMembers.includes(k.toLowerCase()))) : {};
      if (editable && isEditData(change, OwnersAsset)) {
        for (const [owner, def] of Object.entries(entries)) {
          addOwner(found, problems, owner, def, entryPath(owner), ownerTemplates, patch);
        }
      } else if (editable && isEditData(change, MenusAsset)) {
        for (const [key, def] of Object.entries(entries)) {
          if (isObject(def)) {
            addCandidate(found, problems, `Changes[${i}] › Entries › ${key}`, key, def, ownerTemplates, false, patch);
          }
        }
      } else {
        found.otherChanges.push(change);
      }
    });
    return;
  }

  const keys = Object.keys(root).filter(k => !k.startsWith('$'));
  if (keys.some(k => ImportDataMembers.includes(k.toLowerCase()))) {
    // an ImportData file: keys without an owner belong to the importing mod
    const ownerTemplates = copyTemplates(known);
    const ownerDef = getMember(root, 'Owner');
    addOwnerTemplates(ownerTemplates, DefaultOwner, ownerDef);
    const owners = getMember(root, 'Owners');
    if (isObject(owners)) {
      for (const [owner, def] of Object.entries(owners)) {
        addOwnerTemplates(ownerTemplates, owner, def);
      }
    }

    for (const [key, value] of Object.entries(root)) {
      if (!['menus', 'owner', 'owners'].includes(key.toLowerCase()) && key !== DesignerMember) {
        found.content[key] = value;
      }
    }

    if (ownerDef !== undefined) {
      addOwner(found, problems, DefaultOwner, ownerDef, 'Owner', ownerTemplates);
    }

    if (isObject(owners)) {
      for (const [owner, def] of Object.entries(owners)) {
        addOwner(found, problems, owner, def, fieldPath('Owners', owner), ownerTemplates);
      }
    }

    const menus = getMember(root, 'Menus');
    if (isObject(menus)) {
      for (const [key, def] of Object.entries(menus)) {
        if (!key.startsWith('$') && isObject(def)) {
          addCandidate(found, problems, `Menus › ${key}`, key, def, ownerTemplates, true);
        }
      }
    }

    return;
  }

  if (getMember(root, DesignerMember) !== undefined || keys.some(k => canonicalMember('MenuDefinition', k) !== null)) {
    addCandidate(found, problems, 'Menu definition', `${DefaultOwner}/menu`, root, copyTemplates(known), false);
    return;
  }

  if (keys.length > 0 && keys.every(k => isObject(root[k]))) {
    for (const key of keys) {
      addCandidate(found, problems, key, key, root[key] as JsonObject, copyTemplates(known), true);
    }
  }
}

function copyTemplates(known: ReadonlyMap<string, ReadonlySet<string>>): Map<string, Set<string>> {
  return new Map([...known].map(([owner, names]) => [owner, new Set(names)]));
}

function isEditData(change: unknown, asset: string): change is JsonObject {
  if (!isObject(change)) {
    return false;
  }

  const action = getMember(change, 'Action');
  const target = getMember(change, 'Target');
  return typeof action === 'string' && action.trim().toLowerCase() === 'editdata'
    && typeof target === 'string' && target.split(',').some(t => t.trim().toLowerCase() === asset.toLowerCase());
}

function addOwnerTemplates(map: Map<string, Set<string>>, owner: string, def: unknown): void {
  if (!isObject(def)) {
    return;
  }

  const templates = getMember(def, 'Templates');
  if (isObject(templates)) {
    const key = owner.trim().toLowerCase();
    const set = map.get(key) ?? new Set<string>();
    Object.keys(templates).forEach(t => set.add(t));
    map.set(key, set);
  }
}

function addCandidate(
  found: Found,
  problems: Problem[],
  label: string,
  key: string,
  def: JsonObject,
  ownerTemplates: Map<string, Set<string>>,
  ownerless: boolean,
  patch?: PatchMembers
): void {
  const slash = key.indexOf('/');
  let owner = slash > 0 ? key.slice(0, slash).trim() : '';
  let menuId = slash > 0 ? key.slice(slash + 1).trim() : '';
  if (owner.length === 0 || menuId.length === 0) {
    if (!ownerless) {
      problems.push({ severity: 'error', path: entryPath(key), message: "the key must be '<owner mod id>/<menu id>'; the entry is imported with the {{ModId}} owner." });
    }

    owner = DefaultOwner;
    menuId = (slash >= 0 ? key.slice(slash + 1) : key).trim() || 'menu';
  }

  const document = convertMenu(def, owner, menuId, ownerTemplates.get(owner.toLowerCase()) ?? new Set(), problems);
  found.candidates.push({ label, document });
  found.tabs.push(menuTab(document, patch));
}

// ---------------------------------------------------------------------------------------------------------------
//  Owners entry: an owner tab plus a tab per template and tooltip
// ---------------------------------------------------------------------------------------------------------------

function addOwner(found: Found, problems: Problem[], owner: string, def: unknown, path: string, ownerTemplates: Map<string, Set<string>>, patch?: PatchMembers): void {
  if (!isObject(def)) {
    problems.push({ severity: 'warning', path, message: 'the owner entry is not an object; it is not imported.' });
    return;
  }

  const doc: OwnerDoc = { fields: {}, extra: {} };
  const templates: [string, unknown][] = [];
  const tooltips: [string, unknown][] = [];
  for (const [key, value] of Object.entries(def)) {
    const member = canonicalMember('OwnerDefinition', key);
    const text = scalarText(value);
    if (member === 'Templates' && isObject(value)) {
      templates.push(...Object.entries(value));
    } else if (member === 'Tooltips' && isObject(value)) {
      tooltips.push(...Object.entries(value));
    } else if (member !== null && text !== undefined) {
      doc.fields[member] = text;
    } else {
      doc.extra[member ?? key] = value;
    }
  }

  found.tabs.push(patch ? { id: newNodeId(), kind: 'owner', owner, doc, patch } : { id: newNodeId(), kind: 'owner', owner, doc });
  const isTemplate = templateMatcher(ownerTemplates.get(owner.trim().toLowerCase()) ?? []);
  for (const [name, t] of templates) {
    const templatePath = fieldPath(fieldPath(path, 'Templates'), name);
    if (!isObject(t)) {
      problems.push({ severity: 'warning', path: templatePath, message: `template '${name}' is not an object; it is not imported.` });
      continue;
    }

    const ctx: Context = { nodes: {}, problems, isTemplate };
    const template = convertTemplate(t, templatePath, ctx);
    found.tabs.push({ id: newNodeId(), kind: 'template', owner, name, doc: { ...template, nodes: ctx.nodes, previewState: {} } });
  }

  for (const [name, t] of tooltips) {
    found.tabs.push({ id: newNodeId(), kind: 'tooltip', owner, name, doc: convertTooltip(t, fieldPath(fieldPath(path, 'Tooltips'), name), problems) });
  }
}

/** A TooltipDefinition (an object, a bare block array or a bare string, TooltipConverter) as a "Tooltip" root with block nodes. */
function convertTooltip(value: unknown, path: string, problems: Problem[]): TooltipDoc {
  const root = createNode('Tooltip');
  const doc: TooltipDoc = { root: root.id, nodes: { [root.id]: root }, fields: {}, extra: {} };
  let blocks: unknown[] = [];
  const text = scalarText(value);
  if (text !== undefined) {
    doc.shorthand = 'text';
    blocks = [{ Type: 'Line', Text: text }];
  } else if (Array.isArray(value)) {
    doc.shorthand = 'blocks';
    blocks = value;
  } else if (isObject(value)) {
    for (const [key, member] of Object.entries(value)) {
      const name = canonicalMember('TooltipDefinition', key);
      const memberText = scalarText(member);
      if (name === 'Blocks' && Array.isArray(member)) {
        blocks = member;
      } else if (name !== null && memberText !== undefined) {
        doc.fields[name] = memberText;
      } else {
        doc.extra[name ?? key] = member;
      }
    }
  }

  blocks.forEach((block, i) => {
    if (!isObject(block)) {
      problems.push({ severity: 'warning', path: indexPath(fieldPath(path, 'Blocks'), i), message: 'the block is not an object; it is skipped.' });
      return;
    }

    const written = scalarText(getMember(block, 'Type'))?.trim() ?? '';
    const node = createNode(tooltipBlockTypes.find(t => t.toLowerCase() === written.toLowerCase()) ?? written);
    for (const [key, member] of Object.entries(block)) {
      const name = canonicalMember('TooltipBlockDefinition', key);
      const memberText = scalarText(member);
      if (name === 'Type') {
        continue;
      }

      if (name !== null && memberText !== undefined) {
        node.fields[name] = memberText;
      } else {
        node.extra[name ?? key] = member;
      }
    }

    doc.nodes[node.id] = node;
    root.children.push(node.id);
  });
  return doc;
}

// ---------------------------------------------------------------------------------------------------------------
//  Menu
// ---------------------------------------------------------------------------------------------------------------

interface Context {
  nodes: Record<NodeId, DesignerNode>;
  problems: Problem[];
  isTemplate: (name: string) => boolean;
}

/** Convert one MenuDefinition (the value of a Menus entry or a From file) into a document. */
export function convertMenu(def: JsonObject, owner: string, menuId: string, ownerTemplates: Set<string>, problems: Problem[]): DesignerDocument {
  const root = createNode('Menu');
  const doc: DesignerDocument = {
    owner,
    menuId,
    menu: {},
    menuExtra: {},
    root: root.id,
    nodes: { [root.id]: root },
    templates: {},
    previewState: {}
  };

  const designer = getMember(def, DesignerMember);
  if (isObject(designer)) {
    if (typeof designer.owner === 'string' && designer.owner.length > 0) {
      doc.owner = designer.owner;
    }

    if (typeof designer.menuId === 'string' && designer.menuId.length > 0) {
      doc.menuId = designer.menuId;
    }

    if (isObject(designer.previewState)) {
      for (const [k, v] of Object.entries(designer.previewState)) {
        const text = scalarText(v);
        if (text !== undefined) {
          doc.previewState[k] = text;
        }
      }
    }
  }

  const prefix = entryPath(`${doc.owner}/${doc.menuId}`);
  const templatesRaw = getMember(def, 'Templates');
  const localTemplates = isObject(templatesRaw) ? Object.keys(templatesRaw) : [];
  const ctx: Context = { nodes: doc.nodes, problems, isTemplate: templateMatcher([...localTemplates, ...ownerTemplates]) };

  for (const [key, value] of Object.entries(def)) {
    if (key === DesignerMember) {
      continue;
    }

    const member = canonicalMember('MenuDefinition', key);
    if (member === 'Children' && Array.isArray(value)) {
      root.children = convertChildren(value, prefix, ctx);
    } else if (member === 'Templates' && isObject(value)) {
      for (const [name, t] of Object.entries(value)) {
        if (isObject(t)) {
          doc.templates[name] = convertTemplate(t, fieldPath(fieldPath(prefix, 'Templates'), name), ctx);
        } else {
          problems.push({ severity: 'warning', path: fieldPath(fieldPath(prefix, 'Templates'), name), message: `template '${name}' is not an object; it is not imported.` });
        }
      }
    } else {
      const text = scalarText(value);
      if (member !== null && text !== undefined) {
        doc.menu[member] = text;
      } else {
        doc.menuExtra[member ?? key] = value;
      }
    }
  }

  if (doc.menu.From) {
    problems.push({ severity: 'info', path: fieldPath(prefix, 'From'), message: `the entry reads its other members from '${doc.menu.From}' (From); import that file to edit them.` });
  }

  return doc;
}

function convertTemplate(def: JsonObject, path: string, ctx: Context): TemplateDoc {
  const root = createNode('Template');
  ctx.nodes[root.id] = root;
  const template: TemplateDoc = { params: {}, fields: {}, extra: {}, root: root.id };
  for (const [key, value] of Object.entries(def)) {
    const member = canonicalMember('TemplateDefinition', key) ?? key;
    if (member === 'Params' && isObject(value)) {
      template.params = value;
    } else if (member === 'Children' && Array.isArray(value)) {
      root.children = convertChildren(value, path, ctx);
    } else {
      const text = scalarText(value);
      if (text !== undefined) {
        template.fields[member] = text;
      } else {
        template.extra[member] = value;
      }
    }
  }

  return template;
}

// ---------------------------------------------------------------------------------------------------------------
//  Elements
// ---------------------------------------------------------------------------------------------------------------

function convertChildren(list: unknown[], parentPath: string, ctx: Context): NodeId[] {
  return convertElements(list, fieldPath(parentPath, 'Children'), ctx);
}

/** The elements of a list member (`listPath[i]`); a single object (a Cell written as one element) is the list itself. */
function convertElements(list: unknown, listPath: string, ctx: Context): NodeId[] {
  if (isObject(list)) {
    return [convertElement(list, listPath, ctx)];
  }

  const ids: NodeId[] = [];
  (Array.isArray(list) ? list : []).forEach((item, i) => {
    if (!isObject(item)) {
      ctx.problems.push({ severity: 'warning', path: indexPath(listPath, i), message: 'empty element; it is skipped.' });
      return;
    }

    ids.push(convertElement(item, indexPath(listPath, i, scalarText(getMember(item, 'Id'))), ctx));
  });
  return ids;
}

/** A Form's Fields / a DataGrid's Columns as sub-item nodes (model/subItems.ts). */
function convertSubItems(list: unknown[], listPath: string, kind: SubItemKind, ctx: Context): NodeId[] {
  const ids: NodeId[] = [];
  list.forEach((item, i) => {
    const path = indexPath(listPath, i, isObject(item) ? scalarText(getMember(item, 'Id')) : undefined);
    const node = createNode(kind.type);
    const text = scalarText(item);
    if (text !== undefined && kind.valueMember !== undefined) {
      node.fields[kind.valueMember] = text;
      node.shorthand = kind.valueMember;
    } else if (isObject(item)) {
      for (const [key, value] of Object.entries(item)) {
        const member = canonicalMember(kind.schema, key) ?? key;
        const memberText = scalarText(value);
        if (member === kind.elements && (isObject(value) || Array.isArray(value))) {
          node.children = convertElements(value, fieldPath(path, member), ctx);
          node.singleElement = isObject(value);
        } else if (memberText !== undefined) {
          node.fields[member] = memberText;
        } else {
          node.extra[member] = value;
        }
      }
    } else {
      ctx.problems.push({ severity: 'warning', path, message: `empty ${kind.title.toLowerCase()}; it is skipped.` });
      return;
    }

    ctx.nodes[node.id] = node;
    ids.push(node.id);
  });
  return ids;
}

function convertElement(raw: JsonObject, path: string, ctx: Context): NodeId {
  // canonical member spellings (Newtonsoft matches them case-insensitively); unknown members keep theirs
  const members = new Map<string, unknown>();
  const written = new Map<string, string>();
  for (const [key, value] of Object.entries(raw)) {
    const member = canonicalMember('ElementDefinition', key) ?? key;
    members.set(member, value);
    written.set(member, key);
  }

  const has = (m: string) => members.get(m) !== undefined && members.get(m) !== null;
  const node = createNode('');
  ctx.nodes[node.id] = node;
  let builtIn = true;

  const typeValue = members.get('Type');
  members.delete('Type');
  if (has('Template') && (typeValue === undefined || typeValue === null)) {
    node.type = 'Template';
    node.shorthand = 'Template';
  } else if (typeValue !== undefined && typeValue !== null) {
    const written = (scalarText(typeValue) ?? JSON.stringify(typeValue)).trim();
    const kind = typeKind(written, ctx.isTemplate);
    node.type = kind === 'builtin' ? canonicalType(written)! : written;
    builtIn = kind === 'builtin';
    if (kind === 'unknown') {
      const suggestion = suggest(written, builtInTypes());
      ctx.problems.push(suggestion !== null
        ? { severity: 'error', path: fieldPath(path, 'Type'), nodeId: node.id, field: 'Type', message: `unknown element type '${written}' (did you mean '${suggestion}'?); the element is skipped.` }
        : { severity: 'warning', path: fieldPath(path, 'Type'), nodeId: node.id, field: 'Type', message: `'${written}' is not a built-in type, a template of this menu or its owner, or a dotted custom tag; unless a template of that name exists, the element is skipped.` });
    }
  } else {
    const inferred = inferType(has);
    if (inferred === null) {
      ctx.problems.push({ severity: 'error', path, nodeId: node.id, message: 'the element has no Type (or shorthand such as "Label": "text"); it is skipped.' });
    } else {
      node.type = inferred.type;
      node.shorthand = inferred.shorthand;
      const value = valueShorthands[inferred.shorthand];
      if (value && !has(value.main)) {
        // { "Label": "Hi" } → Type Label, Text "Hi" (the value keeps its JSON form when it is not a scalar)
        members.set(value.main, members.get(inferred.shorthand));
        members.delete(inferred.shorthand);
      }
    }
  }

  // the child list: Children, or a Form's Fields / a DataGrid's Columns as sub-item nodes
  const items = subItemsOf(node.type);
  for (const [member, value] of members) {
    if (member === (items?.member ?? 'Children') && Array.isArray(value)) {
      node.children = items !== undefined ? convertSubItems(value, fieldPath(path, member), items, ctx) : convertChildren(value, path, ctx);
      continue;
    }

    const text = scalarText(value);
    // a template instance's / custom tag's extra fields are its arguments: plain values, edited like fields, under the
    // name as written (the arguments are read case-insensitively)
    const argument = !builtIn && !elementTypes.common.includes(member);
    const known = !builtIn || canonicalMember('ElementDefinition', member) !== null;
    const key = argument ? written.get(member)! : member;
    if (text !== undefined && known) {
      node.fields[key] = text;
    } else {
      node.extra[key] = value;
    }
  }

  return node.id;
}

// ---------------------------------------------------------------------------------------------------------------
//  Parse errors
// ---------------------------------------------------------------------------------------------------------------

function lineColumn(text: string, offset: number): { line: number; column: number } {
  let line = 1;
  let lineStart = 0;
  for (let i = 0; i < offset && i < text.length; i++) {
    if (text.charCodeAt(i) === 10) {
      line++;
      lineStart = i + 1;
    }
  }

  return { line, column: offset - lineStart + 1 };
}

function describeParseError(code: string): string {
  // "CommaExpected" → "comma expected"
  return code.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
}
