// Shapes of the metadata bundle written by tools/DesignerMetadata (architecture.md §4).
// The JSON files next to this one are generated; this file is the contract they follow.

/** element-types.json */
export interface ElementTypesFile {
  /** Members every element reads (ElementTypes.Common). */
  common: string[];
  /** Keys an element's Out map accepts. */
  outKeys: string[];
  /** Every element type, in documentation order. */
  types: ElementTypeInfo[];
}

export interface ElementTypeInfo {
  name: string;
  /** Type-specific members. */
  members: string[];
  /** True when the type holds data-built children (a Repeat's children are its row template instead). */
  container: boolean;
  /** Style fields this leaf draws with; null for types with children (every style field can apply). */
  leafStyle: string[] | null;
}

/** defaults.json: data-format defaults, values as the data format writes them (strings). */
export interface DefaultsFile {
  menu: Record<string, string>;
  elements: Record<string, Record<string, string>>;
}

/** csharp-map.json: how CSharpEmitter writes each element kind. */
export type CSharpMapFile = Record<string, CSharpKind>;

export interface CSharpKind {
  /** The IStardewUIApi method, e.g. AddStack. */
  method: string;
  /** The interface the method returns, e.g. IUIStack. */
  interface: string;
  /** Properties the creation call consumes, in order (CSharpEmitter.Calls); not all are real parameters. */
  ctorArgs: string[];
  parameters: CSharpParameter[];
  overloads?: CSharpParameter[][];
}

export interface CSharpParameter {
  name: string;
  type: string;
  optional: boolean;
  default: string | null;
}
