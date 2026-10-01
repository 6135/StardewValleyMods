import { menuSchema } from './model/metadata';
import { subItemKinds } from './model/subItems';

// The one hand-written metadata table (architecture.md §6.2): the literal shape of each string field, which the
// schema cannot express because every data field is a string. Shapes follow the framework's ValueParsers use in
// Data/Building/DataBuilder*.cs; anything not listed is edited as text.

export type FieldShape =
  | { kind: 'bool' }
  /** A whole number; `auto` also accepts "auto" (ValueParsers.OptionalInt). */
  | { kind: 'int'; auto?: boolean }
  | { kind: 'float' }
  /** One of `values` (case-insensitive); `aliases` map extra accepted spellings to a value. */
  | { kind: 'enum'; values: string[]; aliases?: Record<string, string> }
  | { kind: 'margin' }
  | { kind: 'color' }
  | { kind: 'tracks' }
  | { kind: 'sprite' }
  | { kind: 'actions' }
  | { kind: 'text' }
  | { kind: 'multiline' };

const bool: FieldShape = { kind: 'bool' };
const int: FieldShape = { kind: 'int' };
const size: FieldShape = { kind: 'int', auto: true };
const float: FieldShape = { kind: 'float' };
const color: FieldShape = { kind: 'color' };
const sprite: FieldShape = { kind: 'sprite' };
const actions: FieldShape = { kind: 'actions' };
const text: FieldShape = { kind: 'text' };
const multiline: FieldShape = { kind: 'multiline' };

/** UIAlign, with the aliases ValueParsers.TryParseAlign accepts. */
const align: FieldShape = {
  kind: 'enum',
  values: ['Start', 'Center', 'End', 'Stretch'],
  aliases: { left: 'Start', top: 'Start', right: 'End', bottom: 'End', middle: 'Center', centre: 'Center' }
};
const textAlign: FieldShape = { ...align, values: ['Start', 'Center', 'End'] };
const font: FieldShape = { kind: 'enum', values: ['small', 'dialogue', 'tiny'] };
const anchor: FieldShape = {
  kind: 'enum',
  values: ['Center', 'TopLeft', 'TopCenter', 'TopRight', 'MiddleLeft', 'MiddleRight', 'BottomLeft', 'BottomCenter', 'BottomRight', 'Explicit']
};

/** Shapes by field name (element and menu fields share names and shapes). */
export const fieldShapes: Record<string, FieldShape> = {
  // identity and behaviour
  Id: text, Condition: text, If: text, Switch: text, Case: text, With: text, Class: text, Tag: text, AccessibleName: text,
  Visible: bool, Enabled: bool, Sealed: bool, Tooltip: multiline, TooltipTitle: text, RichTooltip: multiline,
  DrawExtra: text, DrawOverlay: text, Outlet: text, Template: text, Composite: text, ContentTarget: text,
  // layout
  Margin: { kind: 'margin' }, MarginLeft: int, MarginTop: int, MarginRight: int, MarginBottom: int,
  Width: size, Height: size, MinWidth: size, MaxWidth: size, MaxHeight: size,
  HorizontalAlign: align, VerticalAlign: align, Alignment: align, TextAlign: textAlign,
  X: int, Y: int, Row: int, Column: int, RowSpan: int, ColumnSpan: int, Cell: text, Span: text,
  // containers
  Horizontal: bool, Spacing: int, Wrap: bool, Columns: { kind: 'tracks' }, Rows: { kind: 'tracks' }, ColumnSpacing: int, RowSpacing: int,
  DrawBox: bool, Padding: int, ViewportHeight: int, ScrollStep: int, ShowScrollbar: bool, MaxContributions: int, Line: bool,
  // text
  Text: multiline, Label: text, Button: text, Checkbox: text, Font: font, Color: color, Shadow: bool, Shrink: bool, Scale: float, RichText: bool,
  // images and buttons
  Icon: sprite, IconScale: float, ClickSound: text, HoverSound: text, Sprite: sprite, Image: sprite, Source: text, Tint: color,
  Item: text, Quality: int, Count: int, Stack: { kind: 'enum', values: ['Hide', 'Quality', 'NumberAndQuality'] }, DrawShadow: bool, Alpha: float,
  // inputs
  Value: text, Bind: text, Validate: text, Placeholder: text, MaxLength: int, Texture: sprite,
  Min: float, Max: float, Step: float, Clamp: bool, Decimals: int, Choices: text, Labels: text, MaxVisible: int,
  ChoicesSource: text, ChoiceValue: text, ChoiceLabel: text,
  // collections and forms
  Repeat: text, As: text, RowHeight: int, VisibleRows: int, Selectable: bool, MultiSelect: bool, BindSelected: text, BindSelection: text,
  Sort: text, SortDescending: bool, Filter: text, RowTooltip: multiline, ScrollSound: text, SelectSound: text, SortSound: text, Model: text, ShowButtons: bool,
  // events
  OnClick: actions, OnRightClick: actions, OnHover: actions, OnHoverEnd: actions, OnFocus: actions, OnBlur: actions,
  OnValueChanged: actions, OnSubmit: actions, OnInvalid: actions, OnLink: actions, OnScroll: actions,
  OnRowClick: actions, OnRowActivated: actions, OnColumnResized: actions, OnSaved: actions, OnCancelled: actions, OnChanged: actions,
  // menu
  From: text, Hotkey: text, Title: text, ShowCloseButton: bool, Modal: bool, DimBackground: bool, Anchor: anchor,
  CloseOnEscape: bool, PlayerLayout: bool, Resizable: bool, DefaultButton: text, CancelButton: text,
  StateLifetime: { kind: 'enum', values: ['Session', 'Open'] }, UpdateIntervalMs: int,
  OnOpen: actions, OnClose: actions, OnUpdate: actions
};

/**
 * Shapes that differ by node type: an Image Source is a rectangle; a Form field (FormFieldDefinition, kinds of
 * DataBuilder.Form) and a DataGrid column (ColumnDefinition, DataGridColumn) are sub-item nodes with their own fields.
 */
const typeShapes: Record<string, Record<string, FieldShape>> = {
  Image: { Source: text },
  FormField: {
    Kind: { kind: 'enum', values: ['Text', 'Number', 'Integer', 'Checkbox', 'Dropdown'] },
    Label: text, Tooltip: multiline, Section: text, ReadOnly: bool
  },
  Column: { Header: text, Width: text, MinWidth: int, Align: textAlign, Sortable: bool, Resizable: bool, Text: text, SortKey: text, SortNumber: text }
};

/** The shape of `field` on an element of `type` (or on the menu); text when unknown. */
export function shapeOf(type: string | 'menu', field: string): FieldShape {
  return (type === 'menu' ? undefined : typeShapes[type]?.[field]) ?? fieldShapes[field] ?? text;
}

/** Schema element, menu and sub-item fields that can hold a string but have no shape entry (logged in dev). */
export function missingShapes(): string[] {
  const missing: string[] = [];
  const models: [string, Record<string, FieldShape>][] = [
    ['ElementDefinition', {}],
    ['MenuDefinition', {}],
    ...subItemKinds.map((k): [string, Record<string, FieldShape>] => [k.schema, typeShapes[k.type] ?? {}])
  ];
  for (const [model, own] of models) {
    for (const [name, schema] of Object.entries(schemaProperties(model))) {
      if (name !== 'Type' && name !== '$schema' && !(name in own) && !(name in fieldShapes) && allowsString(schema)) {
        missing.push(`${model}.${name}`);
      }
    }
  }

  return missing;
}

// --- schema helpers (shared with the inspector) ---------------------------------------------------------------------

type SchemaNode = Record<string, unknown>;

function definitions(): Record<string, SchemaNode> {
  return (menuSchema['definitions'] ?? {}) as Record<string, SchemaNode>;
}

/** The `properties` of a schema definition (ElementDefinition, MenuDefinition …); empty when missing. */
export function schemaProperties(model: string): Record<string, SchemaNode> {
  return (definitions()[model]?.['properties'] ?? {}) as Record<string, SchemaNode>;
}

/** A property's description from the schema, if any. */
export function describe(model: string, field: string): string | undefined {
  const d = schemaProperties(model)[field]?.['description'];
  return typeof d === 'string' ? d : undefined;
}

/** True when a property schema accepts a JSON string (following $ref, oneOf and anyOf). */
export function allowsString(schema: SchemaNode | undefined, depth = 0): boolean {
  if (!schema || depth > 8) {
    return false;
  }

  const ref = schema['$ref'];
  if (typeof ref === 'string') {
    return allowsString(definitions()[ref.replace('#/definitions/', '')], depth + 1);
  }

  const type = schema['type'];
  if (type === 'string' || (Array.isArray(type) && type.includes('string'))) {
    return true;
  }

  for (const key of ['oneOf', 'anyOf']) {
    const options = schema[key];
    if (Array.isArray(options) && options.some(o => allowsString(o as SchemaNode, depth + 1))) {
      return true;
    }
  }

  return false;
}
