// Source nodes for the layout builder (build.ts): document nodes and raw definitions (a List's RowTemplate and what it
// holds) behind one shape.
import type { DesignerDocument, NodeId } from '../model/document';
import { canonicalType } from '../model/metadata';
import { subItemsOf, type SubItemKind } from '../model/subItems';

export interface Src {
  /** The document node, or for a raw definition the document node holding it. */
  id: NodeId;
  raw: boolean;
  type: string;
  fields: Record<string, string>;
  extra: Record<string, unknown>;
  children: () => Src[];
}

export function fromNode(doc: DesignerDocument, id: NodeId): Src | null {
  const node = doc.nodes[id];
  if (!node) {
    return null;
  }
  return {
    id,
    raw: false,
    type: node.type,
    fields: node.fields,
    extra: node.extra,
    children: () => node.children.map(c => fromNode(doc, c)).filter((s): s is Src => s !== null)
  };
}

export const scalarText = (v: unknown): string | undefined =>
  typeof v === 'string' ? v : typeof v === 'number' || typeof v === 'boolean' ? String(v) : undefined;

export const isRecord = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null && !Array.isArray(v);

/** A raw element definition (JSON) as a source node; shorthands expanded like DataValidator.NormalizeType. */
export function fromRaw(raw: unknown, holder: NodeId): Src | null {
  if (typeof raw === 'string') {
    return { id: holder, raw: true, type: 'Label', fields: { Text: raw }, extra: {}, children: () => [] };
  }
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) {
    return null;
  }
  const fields: Record<string, string> = {};
  const extra: Record<string, unknown> = {};
  for (const [k, v] of Object.entries(raw as Record<string, unknown>)) {
    const s = scalarText(v);
    if (s !== undefined) {
      fields[k] = s;
    } else if (k !== 'Children') {
      extra[k] = v;
    }
  }
  const type = fields.Type ?? (fields.Switch !== undefined ? 'Switch'
    : fields.Repeat !== undefined || extra.Repeat !== undefined ? 'Repeat'
    : fields.Button !== undefined ? 'Button'
    : fields.Checkbox !== undefined ? 'Checkbox'
    : fields.Image !== undefined ? 'Image'
    : fields.Label !== undefined ? 'Label'
    : (raw as Record<string, unknown>).Children !== undefined ? 'Stack'
    : fields.Outlet !== undefined ? 'Outlet' : 'Stack');
  // a Form's Fields / a DataGrid's Columns are its children, as on a document node
  const items = subItemsOf(canonicalType(type) ?? type);
  const children = (raw as Record<string, unknown>)[items?.member ?? 'Children'];
  return {
    id: holder,
    raw: true,
    type,
    fields,
    extra,
    children: () => (items !== undefined ? rawSubItems(children, items, holder) : rawList(children, holder))
  };
}

/** Raw sub-items (model/subItems.ts) as source nodes; a bare string item sets the kind's valueMember. */
function rawSubItems(value: unknown, kind: SubItemKind, holder: NodeId): Src[] {
  const out: Src[] = [];
  for (const item of Array.isArray(value) ? value : []) {
    const text = scalarText(item);
    if (text !== undefined && kind.valueMember !== undefined) {
      out.push({ id: holder, raw: true, type: kind.type, fields: { [kind.valueMember]: text }, extra: {}, children: () => [] });
    } else if (item && typeof item === 'object' && !Array.isArray(item)) {
      const fields: Record<string, string> = {};
      const extra: Record<string, unknown> = {};
      for (const [k, v] of Object.entries(item as Record<string, unknown>)) {
        const s = scalarText(v);
        if (s !== undefined) {
          fields[k] = s;
        } else {
          extra[k] = v;
        }
      }
      const elements = kind.elements !== undefined ? (item as Record<string, unknown>)[kind.elements] : undefined;
      out.push({ id: holder, raw: true, type: kind.type, fields, extra, children: () => rawList(elements, holder) });
    }
  }
  return out;
}

/** A raw definition or list of them (RowTemplate / Cell accept both). */
export function rawList(value: unknown, holder: NodeId): Src[] {
  const list = Array.isArray(value) ? value : value === undefined || value === null ? [] : [value];
  return list.map(v => fromRaw(v, holder)).filter((s): s is Src => s !== null);
}

export const stringOf = (v: unknown): string => scalarText(v) ?? JSON.stringify(v);
