import type { DesignerDocument, DesignerNode, JsonExportOptions, NodeId } from '../model/document';
import { defaultOf, typeInfo } from '../model/metadata';
import { inferType, isValueMember, modelMembers, pointer, valueShorthands, type JsonObject, type Model } from './dataFormat';

// JSON export (architecture.md §9.1): a Menus entry, a Content Patcher EditData patch or a standalone From file.
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
  const writer = new Writer(doc, options, nodeAt);
  return { value: writer.menu(), nodeAt };
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

class Writer {
  constructor(
    private readonly doc: DesignerDocument,
    private readonly options: Pick<JsonExportOptions, 'collapseShorthands' | 'omitDefaults'>,
    private readonly nodeAt: Map<string, NodeId>
  ) {}

  menu(): JsonObject {
    const { doc } = this;
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
        templates[name] = this.template(name);
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

  private template(name: string): JsonObject {
    const t = this.doc.templates[name]!;
    const base = pointer('/Templates', name);
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
    return this.doc.nodes[id]?.children ?? [];
  }

  private childList(parent: NodeId, parentPointer: string): JsonObject[] {
    const list: JsonObject[] = [];
    const base = pointer(parentPointer, 'Children');
    for (const childId of this.children(parent)) {
      const child = this.doc.nodes[childId];
      if (child) {
        list.push(this.element(child, pointer(base, list.length)));
      }
    }

    return list;
  }

  private element(node: DesignerNode, at: string): JsonObject {
    this.nodeAt.set(at, node.id);
    const type = node.type;
    const values = new Map<string, unknown>();
    for (const [key, text] of Object.entries(node.fields)) {
      if (this.options.omitDefaults && key !== 'Id' && sameValue(defaultOf(type, key), text)) {
        continue;
      }

      values.set(key, literal('ElementDefinition', key, text));
    }

    for (const [key, value] of Object.entries(node.extra)) {
      if (!values.has(key) && !(key in node.fields) && !(key === 'Children' && node.children.length > 0)) {
        values.set(key, value);
      }
    }

    const children = this.childList(node.id, at);
    if (children.length > 0) {
      values.set('Children', children);
    }

    // shorthand: move the main value back into the shorthand member, and drop Type when the definition still reads as
    // the same type (DataValidator.NormalizeType's order decides)
    let shorthandKey: string | null = null;
    const has = (m: string) => values.get(m) !== undefined && values.get(m) !== null;
    if (type.length > 0) {
      const valueShorthand = Object.entries(valueShorthands).find(([, v]) => v.type === type)?.[0];
      let moved = false;
      if (valueShorthand && (this.options.collapseShorthands || node.shorthand === valueShorthand)
        && !has(valueShorthand) && typeof node.fields[valueShorthands[valueShorthand]!.main] === 'string'
        && values.has(valueShorthands[valueShorthand]!.main)) {
        const main = valueShorthands[valueShorthand]!.main;
        values.set(valueShorthand, values.get(main));
        values.delete(main);
        moved = true;
      }

      const inferred = inferType(has);
      const collapse = inferred !== null && inferred.type === type
        && (node.shorthand === inferred.shorthand || (this.options.collapseShorthands && inferred.shorthand !== 'Children'));
      if (collapse) {
        shorthandKey = inferred.shorthand;
      } else {
        if (moved) {
          const main = valueShorthands[valueShorthand!]!.main;
          values.set(main, values.get(valueShorthand!));
          values.delete(valueShorthand!);
        }

        values.set('Type', type);
      }
    }

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
