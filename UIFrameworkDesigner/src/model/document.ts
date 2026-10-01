// The designer's document model (architecture.md §6.1). Shared contract between the store, io, validate,
// layout, preview and codegen modules; keep it free of React and of any module-specific state.

/** Designer-internal node id: stable across edits, never exported (not the element's "Id"). */
export type NodeId = string;

export interface DesignerDocument {
  /** "{{ModId}}" or a literal mod id; the Menus key is `${owner}/${menuId}`. */
  owner: string;
  menuId: string;
  /** MenuDefinition members other than Children and Templates, as raw strings (every data field is a string). */
  menu: Record<string, string>;
  /** Menu-level members the designer does not model as strings (State, Sources, Computed, Watch, Keys, OnOpen …) and unknown members, verbatim. */
  menuExtra: Record<string, unknown>;
  /** Synthetic root node holding the menu's Children; its type is "Menu" and it has no fields. */
  root: NodeId;
  /** Every node of the menu tree and of the templates, flat. */
  nodes: Record<NodeId, DesignerNode>;
  /** Menu-level Templates, edited like menus. */
  templates: Record<string, TemplateDoc>;
  /** Sample values for expressions in the preview (§7.2); never exported. */
  previewState: Record<string, string>;
}

export interface DesignerNode {
  id: NodeId;
  /** Canonical built-in type, template name or custom tag; "Menu" / "Template" for synthetic roots. */
  type: string;
  /** Every string-valued member the element sets, as written in the data format (raw strings, expressions included). */
  fields: Record<string, string>;
  /** Members that are not plain strings (Columns definitions, Args objects, RichTooltip, Fields …) or not known, verbatim. */
  extra: Record<string, unknown>;
  children: NodeId[];
  /** The shorthand member the element was imported with ("Label", "Button", "Checkbox", "Image"), re-used on export. */
  shorthand?: string;
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
