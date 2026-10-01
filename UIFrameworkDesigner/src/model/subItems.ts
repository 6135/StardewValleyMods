// Sub-items (architecture.md §6.1): list members of an element whose items are definitions of their own (a Form's
// Fields, a DataGrid's Columns). They are child nodes of synthetic types so the tree, inspector, validation and preview
// handle them like elements; import / export convert between the member array and the nodes. This table is the only
// place that names them.

export interface SubItemKind {
  /** The element type that holds the items. */
  parent: string;
  /** The parent's member the items are written in (an array). */
  member: string;
  /** The synthetic node type of an item. */
  type: string;
  /** The schema definition of an item (its fields, in declaration order, with descriptions). */
  schema: 'FormFieldDefinition' | 'ColumnDefinition';
  /** Display name (palette, messages, suggested ids). */
  title: string;
  /** The item field the tree shows next to its Id. */
  caption: string;
  /** The item member holding elements (one element or an array); they are the item node's children. */
  elements?: string;
  /** The member a bare string item stands for (`"Columns": ["auto", ...]` reads as Width). */
  valueMember?: string;
}

export const subItemKinds: readonly SubItemKind[] = [
  { parent: 'Form', member: 'Fields', type: 'FormField', schema: 'FormFieldDefinition', title: 'Field', caption: 'Label' },
  { parent: 'DataGrid', member: 'Columns', type: 'Column', schema: 'ColumnDefinition', title: 'Column', caption: 'Header', elements: 'Cell', valueMember: 'Width' }
];

/** The sub-items an element of `type` holds, or undefined. */
export function subItemsOf(type: string): SubItemKind | undefined {
  return subItemKinds.find(k => k.parent === type);
}

/** The kind of a sub-item node type (FormField, Column), or undefined for elements. */
export function subItemKind(type: string): SubItemKind | undefined {
  return subItemKinds.find(k => k.type === type);
}
