import type { DesignerDocument, DesignerNode, JsonExportOptions, NodeId, TemplateDoc } from '../model/document';
import type { OwnerDoc, TooltipDoc } from '../model/workspace';
import { defaultOf, typeInfo } from '../model/metadata';
import { subItemsOf, type SubItemKind } from '../model/subItems';
import { inferType, isValueMember, modelMembers, pointer, valueShorthands, type JsonObject, type Model } from './dataFormat';

// JSON export (architecture.md §9.1): a Menus entry, a Content Patcher EditData patch or a standalone From file; and
// the owner-level definitions of a workspace (§18.4): an owner template, a named tooltip, an Owners entry.
// Members follow Core/Export/JsonEmitter's order (type / id, main value, type members, layout, state, tooltips, style,
// events, args, children); unknown members are written back verbatim. Never writes previewState or designer node ids.

const MenusAsset = 'Mods/6135.UIFramework/Menus';

/** The Menus key of a document: `<owner>/<menuId>`. */
export function menuKey(doc: DesignerDocument): string {
  return `${doc.owner}/${doc.menuId}`;
}

/** Serialise `doc` in the chosen shape. */
export function exportJson(doc: DesignerDocument, options: JsonExportOptions): string {
  const { value } = buildMenuObject(doc, options);
  let output: unknown;
  switch (options.shape) {
    case 'fromFile':
      output = value;
      break;
    case 'cpPatch':
      output = { LogName: `${doc.menuId} menu`, Action: 'EditData', Target: MenusAsset, Entries: { [menuKey(doc)]: value } };
      break;
    default:
      output = { [menuKey(doc)]: value };
      break;
  }

  return printJson(output, options.indent);
}

/** The MenuDefinition object of `doc`, plus the node each element object came from, by JSON pointer (`/Children/2`). */
export interface BuiltMenu {
  value: JsonObject;
  /** JSON pointer (relative to the MenuDefinition) → node; the synthetic roots are at `` and `/Templates/<name>`. */
  nodeAt: Map<string, NodeId>;
}

/** Build the MenuDefinition object (the fromFile shape) as plain JSON. */
export function buildMenuObject(doc: DesignerDocument, options: Pick<JsonExportOptions, 'collapseShorthands' | 'omitDefaults'>): BuiltMenu {
  const nodeAt = new Map<string, NodeId>();
  const writer = new Writer(doc.nodes, options, nodeAt);
  return { value: writer.menu(doc), nodeAt };
}

/** A TemplateDefinition object (an owner template: `nodes` holds its body). */
export function buildTemplateObject(template: TemplateDoc, nodes: Record<NodeId, DesignerNode>, options: Pick<JsonExportOptions, 'collapseShorthands' | 'omitDefaults'>): JsonObject {
  return new Writer(nodes, options, new Map()).template(template, '');
}

/** A TooltipDefinition: blocks in member order, written as a bare block array / string again when it was and still fits. */
export function buildTooltipObject(doc: TooltipDoc): unknown {
  const blocks = (doc.nodes[doc.root]?.children ?? []).map(id => doc.nodes[id]).filter((n): n is DesignerNode => n !== undefined).map(node => {
    const members: Member[] = [];
    const order = modelMembers('TooltipBlockDefinition');
    let unknown = 0;
    const add = (key: string, value: unknown) => {
      const index = order.indexOf(key);
      members.push({ key, value, rank: index >= 0 ? 0 : 1, order: index >= 0 ? index : unknown++ });
    };
    add('Type', node.type);
    Object.entries(node.fields).forEach(([key, text]) => add(key, literal('TooltipBlockDefinition', key, text)));
    Object.entries(node.extra).filter(([key]) => !(key in node.fields)).forEach(([key, value]) => add(key, value));
    return assemble(members);
  });

  const plain = Object.keys(doc.fields).length === 0 && Object.keys(doc.extra).length === 0;
  const only = blocks.length === 1 ? blocks[0]! : null;
  if (plain && doc.shorthand === 'text' && only && only['Type'] === 'Line' && typeof only['Text'] === 'string' && Object.keys(only).length === 2) {
    return only['Text'];
  }

  if (plain && doc.shorthand === 'blocks') {
    return blocks;
  }

  const result = modelObject('TooltipDefinition', doc.fields, doc.extra);
  result['Blocks'] = blocks;
  return result;
}

/** An OwnerDefinition without Templates and Tooltips (the caller adds them from their tabs). */
export function buildOwnerObject(doc: OwnerDoc): JsonObject {
  return modelObject('OwnerDefinition', doc.fields, doc.extra);
}

/** String members (as literals) and JSON members of a model, in the model's member order, unknown members after. */
function modelObject(model: Model, fields: Record<string, string>, extra: Record<string, unknown>): JsonObject {
  const order = modelMembers(model);
  const members: Member[] = [];
  let unknown = 0;
  const add = (key: string, value: unknown) => {
    const index = order.indexOf(key);
    members.push({ key, value, rank: index >= 0 ? 0 : 1, order: index >= 0 ? index : unknown++ });
  };
  Object.entries(fields).forEach(([key, text]) => add(key, literal(model, key, text)));
  Object.entries(extra).filter(([key]) => !(key in fields)).forEach(([key, value]) => add(key, value));
  return assemble(members);
}

// ---------------------------------------------------------------------------------------------------------------
//  Member order (JsonEmitter.Rank)
// ---------------------------------------------------------------------------------------------------------------

const MainMembers = new Set(['Text', 'Label', 'Value', 'Sprite', 'Item', 'Quality', 'Count', 'Choices', 'Labels']);
const LayoutMembers = new Set([
  'Margin', 'MarginLeft', 'MarginTop', 'MarginRight', 'MarginBottom', 'Width', 'Height', 'MinWidth', 'MaxWidth',
  'HorizontalAlign', 'VerticalAlign', 'X', 'Y', 'Row', 'Column', 'RowSpan', 'ColumnSpan', 'Cell', 'Span'
]);
const StateMembers = new Set(['Visible', 'Enabled', 'Sealed']);
const TooltipMembers = new Set(['Tooltip', 'TooltipTitle', 'RichTooltip', 'AccessibleName', 'Tag']);

function rank(key: string): number {
  if (key === 'Type' || key === 'Composite' || key === 'Template') {
    return 0;
  }

  if (key === 'Id') {
    return 1;
  }

  if (MainMembers.has(key)) {
    return 10;
  }

  if (LayoutMembers.has(key)) {
    return 50;
  }

  if (StateMembers.has(key)) {
    return 60;
  }

  if (TooltipMembers.has(key)) {
    return 70;
  }

  switch (key) {
    case 'Style':
      return 80;
    case 'Args':
      return 95;
    case 'Children':
      return 100;
    default:
      return /^On[A-Z]/.test(key) ? 90 : 20;
  }
}

// ---------------------------------------------------------------------------------------------------------------
//  Writer
// ---------------------------------------------------------------------------------------------------------------

interface Member {
  key: string;
  value: unknown;
  rank: number;
  order: number;
}

/** The element's members in written order: by rank (the kept shorthand first), then the type's / model's member order. */
function orderedMembers(type: string, values: Map<string, unknown>, shorthandKey: string | null): Member[] {
  const typeMembers = typeInfo(type)?.members ?? [];
  const elementMembers = modelMembers('ElementDefinition');
  const members: Member[] = [];
  let unknown = 0;
  for (const [key, value] of values) {
    const typeIndex = typeMembers.indexOf(key);
    const modelIndex = elementMembers.indexOf(key);
    members.push({
      key,
      value,
      rank: key === shorthandKey ? 2 : rank(key),
      order: typeIndex >= 0 ? typeIndex : modelIndex >= 0 ? 1000 + modelIndex : 10000 + unknown++
    });
  }

  return members;
}

class Writer {
  constructor(
    private readonly nodes: Record<NodeId, DesignerNode>,
    private readonly options: Pick<JsonExportOptions, 'collapseShorthands' | 'omitDefaults'>,
    private readonly nodeAt: Map<string, NodeId>
  ) {}

  menu(doc: DesignerDocument): JsonObject {
    const order = modelMembers('MenuDefinition');
    const members: Member[] = [];
    let unknown = 0;
    const add = (key: string, value: unknown) => {
      const index = order.indexOf(key);
      // "$schema" (editor metadata) first, then the declared members in order, then unknown members as written
      members.push({ key, value, rank: key.startsWith('$') ? -1 : index >= 0 ? 0 : 1, order: index >= 0 ? index : unknown++ });
    };

    for (const [key, text] of Object.entries(doc.menu)) {
      if (this.options.omitDefaults && sameValue(defaultOf('menu', key), text)) {
        continue;
      }

      add(key, literal('MenuDefinition', key, text));
    }

    for (const [key, value] of Object.entries(doc.menuExtra)) {
      if (!(key in doc.menu) && !(key === 'Children' && this.children(doc.root).length > 0)) {
        add(key, value);
      }
    }

    const templateNames = Object.keys(doc.templates);
    if (templateNames.length > 0) {
      const templates: JsonObject = {};
      for (const name of templateNames) {
        templates[name] = this.template(doc.templates[name]!, pointer('/Templates', name));
      }

      add('Templates', templates);
    }

    this.nodeAt.set('', doc.root);
    const children = this.childList(doc.root, '');
    const result = assemble(members);
    if (children.length > 0) {
      result.Children = children;
    }

    return result;
  }

  template(t: TemplateDoc, base: string): JsonObject {
    this.nodeAt.set(base, t.root);
    const result: JsonObject = {};
    if (Object.keys(t.params).length > 0) {
      result.Params = t.params;
    }

    for (const [key, text] of Object.entries(t.fields)) {
      result[key] = literal('TemplateDefinition', key, text);
    }

    for (const [key, value] of Object.entries(t.extra)) {
      if (!(key in result)) {
        result[key] = value;
      }
    }

    const children = this.childList(t.root, base);
    if (children.length > 0) {
      result.Children = children;
    }

    return result;
  }

  private children(id: NodeId): NodeId[] {
    return this.nodes[id]?.children ?? [];
  }

  private childList(parent: NodeId, parentPointer: string): JsonObject[] {
    const list: JsonObject[] = [];
    const base = pointer(parentPointer, 'Children');
    for (const childId of this.children(parent)) {
      const child = this.nodes[childId];
      if (child) {
        list.push(this.element(child, pointer(base, list.length)));
      }
    }

    return list;
  }

  private element(node: DesignerNode, at: string): JsonObject {
    this.nodeAt.set(at, node.id);
    const values = this.elementValues(node, at);
    const shorthandKey = node.type.length > 0 ? this.applyShorthand(node, values) : null;
    return assemble(orderedMembers(node.type, values, shorthandKey));
  }

  /** The node's fields (defaults omitted when asked), extra members and child list, by member name. */
  private elementValues(node: DesignerNode, at: string): Map<string, unknown> {
    const type = node.type;
    const values = new Map<string, unknown>();
    for (const [key, text] of Object.entries(node.fields)) {
      if (this.options.omitDefaults && key !== 'Id' && sameValue(defaultOf(type, key), text)) {
        continue;
      }

      values.set(key, literal('ElementDefinition', key, text));
    }

    const items = subItemsOf(type);
    const listMember = items?.member ?? 'Children';
    for (const [key, value] of Object.entries(node.extra)) {
      if (!values.has(key) && !(key in node.fields) && !(key === listMember && node.children.length > 0)) {
        values.set(key, value);
      }
    }

    const children = items !== undefined ? this.subItemList(node, items, pointer(at, items.member)) : this.childList(node.id, at);
    if (children.length > 0) {
      values.set(listMember, children);
    }

    return values;
  }

  /**
   * Shorthand: moves the main value back into the shorthand member, and drops Type when the definition still reads as
   * the same type (DataValidator.NormalizeType's order decides); returns the shorthand member kept, or null.
   */
  private applyShorthand(node: DesignerNode, values: Map<string, unknown>): string | null {
    const type = node.type;
    const has = (m: string) => values.get(m) !== undefined && values.get(m) !== null;
    const valueShorthand = Object.entries(valueShorthands).find(([, v]) => v.type === type)?.[0];
    const main = valueShorthand !== undefined ? valueShorthands[valueShorthand]!.main : undefined;
    let moved = false;
    if (valueShorthand && main !== undefined && (this.options.collapseShorthands || node.shorthand === valueShorthand)
      && !has(valueShorthand) && typeof node.fields[main] === 'string' && values.has(main)) {
      values.set(valueShorthand, values.get(main));
      values.delete(main);
      moved = true;
    }

    const inferred = inferType(has);
    const collapse = inferred !== null && inferred.type === type
      && (node.shorthand === inferred.shorthand || (this.options.collapseShorthands && inferred.shorthand !== 'Children'));
    if (collapse) {
      return inferred.shorthand;
    }

    if (moved) {
      values.set(main!, values.get(valueShorthand!));
      values.delete(valueShorthand!);
    }

    values.set('Type', type);
    return null;
  }

  /** A Form's Fields / a DataGrid's Columns from the node's sub-item children. */
  private subItemList(parent: DesignerNode, kind: SubItemKind, listPointer: string): unknown[] {
    const list: unknown[] = [];
    for (const childId of parent.children) {
      const child = this.nodes[childId];
      if (child) {
        list.push(this.subItem(child, kind, pointer(listPointer, list.length)));
      }
    }

    return list;
  }

  /** One sub-item: its fields in the schema definition's order, unknown members after, its elements member last. */
  private subItem(node: DesignerNode, kind: SubItemKind, at: string): unknown {
    this.nodeAt.set(at, node.id);
    const names = Object.keys(node.fields);
    if (kind.valueMember !== undefined && node.shorthand === kind.valueMember && names.length === 1 && names[0] === kind.valueMember
      && Object.keys(node.extra).length === 0 && node.children.length === 0) {
      return node.fields[kind.valueMember];
    }

    const order = modelMembers(kind.schema);
    const members: Member[] = [];
    let unknown = 0;
    const add = (key: string, value: unknown) => {
      const index = order.indexOf(key);
      members.push({ key, value, rank: index >= 0 ? 0 : 1, order: index >= 0 ? index : unknown++ });
    };

    for (const [key, text] of Object.entries(node.fields)) {
      add(key, literal(kind.schema, key, text));
    }

    for (const [key, value] of Object.entries(node.extra)) {
      if (!(key in node.fields) && !(key === kind.elements && node.children.length > 0)) {
        add(key, value);
      }
    }

    if (kind.elements !== undefined && node.children.length > 0) {
      const base = pointer(at, kind.elements);
      const elements = node.children.map(id => this.nodes[id]).filter((n): n is DesignerNode => n !== undefined);
      add(kind.elements, node.singleElement && elements.length === 1
        ? this.element(elements[0]!, base)
        : elements.map((e, i) => this.element(e, pointer(base, i))));
    }

    return assemble(members);
  }
}

function assemble(members: Member[]): JsonObject {
  members.sort((a, b) => a.rank - b.rank || a.order - b.order);
  const result: JsonObject = {};
  for (const m of members) {
    result[m.key] = m.value;
  }

  return result;
}

/** True when a field equals its data-format default (values are compared as the parsers read them: trimmed, any case). */
function sameValue(defaultValue: string | undefined, text: string): boolean {
  return defaultValue !== undefined && defaultValue.trim().toLowerCase() === text.trim().toLowerCase();
}

/**
 * A field as JSON: a plain value member whose text is exactly a JSON number or true / false is written as that literal
 * (the way authors write it; Newtonsoft reads it back as the same text), everything else as a string.
 */
function literal(model: Model, key: string, text: string): unknown {
  if (!isValueMember(model, key)) {
    return text;
  }

  if (text === 'true') {
    return true;
  }

  if (text === 'false') {
    return false;
  }

  if (/^-?(0|[1-9]\d*)(\.\d+)?([eE][+-]?\d+)?$/.test(text)) {
    const n = Number(text);
    if (Number.isFinite(n) && String(n) === text) {
      return n;
    }
  }

  return text;
}

// ---------------------------------------------------------------------------------------------------------------
//  Printing
// ---------------------------------------------------------------------------------------------------------------

const LineWidth = 120;

/** JSON text; with an indent, short objects and arrays stay on one line (`{ "Label": "Hi" }`) like hand-written packs. */
export function printJson(value: unknown, indent: number): string {
  if (indent <= 0) {
    return JSON.stringify(value);
  }

  return print(value, 0, indent);
}

function print(value: unknown, level: number, indent: number): string {
  if (value === null || typeof value !== 'object') {
    return JSON.stringify(value) ?? 'null';
  }

  const inline = printInline(value);
  if (inline.length + level * indent <= LineWidth) {
    return inline;
  }

  const pad = ' '.repeat((level + 1) * indent);
  const end = ' '.repeat(level * indent);
  if (Array.isArray(value)) {
    return `[\n${value.map(v => pad + print(v, level + 1, indent)).join(',\n')}\n${end}]`;
  }

  const entries = Object.entries(value as JsonObject).filter(([, v]) => v !== undefined);
  return `{\n${entries.map(([k, v]) => `${pad}${JSON.stringify(k)}: ${print(v, level + 1, indent)}`).join(',\n')}\n${end}}`;
}

function printInline(value: unknown): string {
  if (value === null || typeof value !== 'object') {
    return JSON.stringify(value) ?? 'null';
  }

  if (Array.isArray(value)) {
    return value.length === 0 ? '[]' : `[ ${value.map(printInline).join(', ')} ]`;
  }

  const entries = Object.entries(value as JsonObject).filter(([, v]) => v !== undefined);
  return entries.length === 0 ? '{}' : `{ ${entries.map(([k, v]) => `${JSON.stringify(k)}: ${printInline(v)}`).join(', ')} }`;
}
