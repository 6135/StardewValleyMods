import { parse, printParseErrorCode, visit, type ParseError } from 'jsonc-parser';
import type { DesignerDocument, ImportCandidate, ImportResult, Problem, TemplateDoc } from '../model/document';
import { createNode, newNodeId } from '../model/factory';
import { tooltipBlockTypes } from '../model/metadata';
import {
  createWorkspace, DefaultOwner, menuTab, type OwnerDoc, type PatchMembers, type TooltipDoc, type Workspace, type WorkspaceTab
} from '../model/workspace';
import {
  canonicalMember,
  entryPath,
  fieldPath,
  getMember,
  indexPath,
  isObject,
  scalarText,
  templateMatcher,
  type JsonObject
} from './dataFormat';
import { convertChildren, type Context } from './importElements';

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

/** A Content Patcher content.json (`isCpContent`) or one EditData patch: owner templates first (instances of them are not unknown types), then the menus. */
function collectPatches(root: JsonObject, isCpContent: boolean, changes: unknown[], found: Found, problems: Problem[], known: ReadonlyMap<string, ReadonlySet<string>>): void {
  if (isCpContent) {
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
}

/** An ImportData file: keys without an owner belong to the importing mod. */
function collectImportData(root: JsonObject, found: Found, problems: Problem[], known: ReadonlyMap<string, ReadonlySet<string>>): void {
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
}

function collect(root: JsonObject, found: Found, problems: Problem[], known: ReadonlyMap<string, ReadonlySet<string>>): void {
  const changesMember = getMember(root, 'Changes');
  const changes = Array.isArray(changesMember) ? changesMember : isEditData(root, MenusAsset) ? [root] : null;
  if (changes !== null) {
    collectPatches(root, Array.isArray(changesMember), changes, found, problems, known);
    return;
  }

  const keys = Object.keys(root).filter(k => !k.startsWith('$'));
  if (keys.some(k => ImportDataMembers.includes(k.toLowerCase()))) {
    collectImportData(root, found, problems, known);
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
