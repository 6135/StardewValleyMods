// The designer's document model (architecture.md §6.1). Shared contract between the store, io, validate,
// layout, preview and codegen modules; keep it free of React and of any module-specific state.

import type { Workspace } from './workspace';

/** Designer-internal node id: stable across edits, never exported (not the element's "Id"). */
export type NodeId = string;

/** A node table with its root: what the tree, the inspector and the tree operations (model/ops.ts) edit. */
export interface NodeTree {
  root: NodeId;
  nodes: Record<NodeId, DesignerNode>;
}

export interface DesignerDocument extends NodeTree {
  /** "{{ModId}}" or a literal mod id; the Menus key is `${owner}/${menuId}`. */
  owner: string;
  menuId: string;
  /** MenuDefinition members other than Children and Templates, as raw strings (every data field is a string). */
  menu: Record<string, string>;
  /** Menu-level members the designer does not model as strings (State, Sources, Computed, Watch, Keys, OnOpen …) and unknown members, verbatim. */
  menuExtra: Record<string, unknown>;
  /** Synthetic root node holding the menu's Children (type "Menu", no fields); `nodes` holds the menu tree and the templates. */
  /** Menu-level Templates, edited like menus. */
  templates: Record<string, TemplateDoc>;
  /** Sample values for expressions in the preview (§7.2); never exported. */
  previewState: Record<string, string>;
}

export interface DesignerNode {
  id: NodeId;
  /**
   * Canonical built-in type, template name or custom tag; "Menu" / "Template" / "Tooltip" for synthetic roots; a tooltip
   * block kind ("Title", "Line" … under a "Tooltip" root, metadata.tooltipBlockTypes); a sub-item type
   * (model/subItems.ts) for an item of an element's list member: "FormField" (one of a Form's Fields, children: none) and
   * "Column" (one of a DataGrid's Columns, children: its Cell elements). A Form / DataGrid holds only those as children.
   */
  type: string;
  /** Every string-valued member the element sets, as written in the data format (raw strings, expressions included). */
  fields: Record<string, string>;
  /** Members that are not plain strings (Args objects, RichTooltip, Choices arrays …) or not known, verbatim. */
  extra: Record<string, unknown>;
  children: NodeId[];
  /**
   * The shorthand member the element was imported with ("Label", "Button", "Checkbox", "Image"), re-used on export;
   * for a sub-item, its valueMember when it was written as a bare string ("Width" for a `"Columns": ["auto"]` item).
   */
  shorthand?: string;
  /** Sub-item: its elements member (a Column's Cell) was written as one element, not an array; kept while it has one. */
  singleElement?: boolean;
}

export interface TemplateDoc {
  /** Params definitions, verbatim. */
  params: Record<string, unknown>;
  /** Template members other than Params and Children (Spacing, Horizontal …). */
  fields: Record<string, string>;
  extra: Record<string, unknown>;
  /** Synthetic root (type "Template") holding the template's Children. */
  root: NodeId;
}

/** A validation or import message; path uses the framework's format (Children[2].Children[0].Spacing). */
export interface Problem {
  severity: 'error' | 'warning' | 'info';
  message: string;
  path: string;
  /** The node the message is about, when known (clicking the message selects it). */
  nodeId?: NodeId;
  field?: string;
}

/** One menu found in an imported text (a Menus entry, a CP EditData patch entry or a From file). */
export interface ImportCandidate {
  /** Where it was found, for the picker: "Changes[3] › Entries › {{ModId}}/demo". */
  label: string;
  document: DesignerDocument;
}

export interface ImportResult {
  candidates: ImportCandidate[];
  /**
   * Everything the text holds as a workspace (§18.4): each Owners entry as an owner tab plus a tab per template and
   * tooltip, each menu as a menu tab, in the text's order; null when it holds nothing the designer edits.
   */
  workspace: Workspace | null;
  /** The root's `$designer` member (a saved workspace's tab order and preview state, io/workspace.ts), verbatim. */
  designer?: unknown;
  problems: Problem[];
  /** True when the text had comments (they are not preserved on export, §6.1). */
  hadComments: boolean;
}

export type JsonExportShape = 'entry' | 'cpPatch' | 'fromFile';

export interface JsonExportOptions {
  shape: JsonExportShape;
  /** Collapse shorthands back (default true). */
  collapseShorthands: boolean;
  /** Omit fields equal to the data-format default (default true). */
  omitDefaults: boolean;
  indent: number;
}

export const defaultJsonExportOptions: JsonExportOptions = { shape: 'entry', collapseShorthands: true, omitDefaults: true, indent: 2 };
