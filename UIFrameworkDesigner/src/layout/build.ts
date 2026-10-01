// Port of the data builder's element construction (Data/Building/DataBuilder.cs Create / ApplyTypeMembers /
// ApplyCommon, DataBuilder.Structure.cs If / Switch, DataBuilder.Repeat.cs, DataBuilder.List.cs, DataBuilder.Grid.cs,
// DataBuilder.Composite.cs CreateTemplate / BuildOutlet) over the designer document, producing the layout tree.
import type { DesignerDocument, NodeId, TemplateDoc } from '../model/document';
import { canonicalType, defaultOf, elementTypes } from '../model/metadata';
import { subItemsOf, type SubItemKind } from '../model/subItems';
import {
  LButton, LCanvas, LCheckbox, LContainer, LDataGrid, LDropdown, LElement, LForm, LGrid, LImage, LItemImage, LLabel, LListView,
  LPanel, LPlaceholder, LScrollView, LSlider, LSpacer, LStack, LTextBox, type ElementInfo, type GridColumn,
  type LayoutContext
} from './elements';
import { parseTracks } from './engine';
import { CHIP_END, CHIP_EXPR, CHIP_I18N, CHIP_TOKEN, type LayoutOptions } from './types';
import {
  cssColor, hasTemplate, parseAlign, parseBool, parseFont, parseInt32, parseMargin, parseNumber, parseOptionalInt,
  parsePair, parseRect
} from './values';

// ---------------------------------------------------------------------------------------------------------------------
//  Source nodes: document nodes and raw definitions (a List's RowTemplate and what it holds) behind one shape
// ---------------------------------------------------------------------------------------------------------------------

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

const scalarText = (v: unknown): string | undefined =>
  typeof v === 'string' ? v : typeof v === 'number' || typeof v === 'boolean' ? String(v) : undefined;

const isRecord = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null && !Array.isArray(v);

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
function rawList(value: unknown, holder: NodeId): Src[] {
  const list = Array.isArray(value) ? value : value === undefined || value === null ? [] : [value];
  return list.map(v => fromRaw(v, holder)).filter((s): s is Src => s !== null);
}

// ---------------------------------------------------------------------------------------------------------------------
//  Scope and value resolution (ExpressionValueResolver.Resolve rules)
// ---------------------------------------------------------------------------------------------------------------------

interface Outlets {
  routes: Map<string, Src[]>;
  taken: Set<string>;
  scope: Scope;
}

interface Scope {
  args: Record<string, string> | null;
  /** Row locals (RowScope.For: row, index, the As alias and <alias>Index) as JSON values, for nested sources. */
  locals: Record<string, unknown>;
  /** `locals` flattened to the dotted scalar names the evaluator reads (row.price, season.name …). */
  vars: Record<string, string>;
  instance: number;
  ownerId?: NodeId;
  hidden: boolean;
  outlets: Outlets | null;
  depth: number;
}

type Segment = { text: string } | { expr: string };

/** Template.Parse segments: literal text, `${expr}` and `$:{expr}` (braces and quoted strings nest), `$${` = literal `${`. */
function scanTemplate(raw: string): Segment[] {
  const out: Segment[] = [];
  let text = '';
  let i = 0;
  while (i < raw.length) {
    if (raw.startsWith('$${', i)) {
      text += '${';
      i += 3;
      continue;
    }
    const open = raw.startsWith('${', i) ? 2 : raw.startsWith('$:{', i) ? 3 : 0;
    if (open === 0) {
      text += raw[i];
      i++;
      continue;
    }
    let j = i + open;
    let depth = 1;
    let quote = '';
    for (; j < raw.length; j++) {
      const c = raw[j]!;
      if (quote) {
        if (c === '\\') {
          j++;
        } else if (c === quote) {
          quote = '';
        }
      } else if (c === '"' || c === '\'') {
        quote = c;
      } else if (c === '{') {
        depth++;
      } else if (c === '}' && --depth === 0) {
        break;
      }
    }
    if (text) {
      out.push({ text });
      text = '';
    }
    out.push({ expr: raw.substring(i + open, j).trim() });
    i = j + 1;
  }
  if (text) {
    out.push({ text });
  }
  return out;
}

const argPattern = /\bargs\.([A-Za-z_][A-Za-z0-9_]*)/g;

function argLiteral(value: string): string {
  if (parseNumber(value) !== undefined || /^(true|false|null)$/i.test(value.trim())) {
    return value.trim();
  }
  return `'${value.replace(/\\/g, '\\\\').replace(/'/g, '\\\'')}'`;
}

/** An argument by name: exact first, then case-insensitively (CompositeArgs.TryGetRaw). */
function argValue(args: Record<string, string>, name: string): string | undefined {
  if (name in args) {
    return args[name];
  }
  const key = Object.keys(args).find(k => k.toLowerCase() === name.toLowerCase());
  return key === undefined ? undefined : args[key];
}

/** Template arguments: `${args.x}` segments become the argument text, `args.x` inside expressions a literal. */
function substituteArgs(raw: string, args: Record<string, string> | null, bare: boolean): string {
  if (!args || !raw.includes('args.')) {
    return raw;
  }
  const inExpr = (expr: string): string => expr.replace(argPattern, (m, name: string) => {
    const value = argValue(args, name);
    return value === undefined ? m : argLiteral(value);
  });
  if (!hasTemplate(raw)) {
    return bare ? inExpr(raw) : raw;
  }
  return scanTemplate(raw).map(s => {
    if ('text' in s) {
      return s.text.split('${').join('$${');
    }
    const whole = /^args\.([A-Za-z_][A-Za-z0-9_]*)$/.exec(s.expr);
    const value = whole ? argValue(args, whole[1]!) : undefined;
    if (value !== undefined) {
      return value.split('${').join('$${');
    }
    return '${' + inExpr(s.expr) + '}';
  }).join('');
}

/** CP tokens in text as chips: `{{i18n:key}}` → an i18n chip with the key, other `{{Token}}` → a token chip. */
export function chipTokens(text: string): string {
  return text.replace(/\{\{\s*([^{}]*?)\s*\}\}/g, (_m, inner: string) => {
    const i18n = /^i18n\s*:\s*(.+)$/i.exec(inner);
    return i18n ? CHIP_I18N + i18n[1]!.trim() + CHIP_END : CHIP_TOKEN + inner + CHIP_END;
  });
}

/**
 * The key of a collection source C# provides (a `hook:name` / `@name` shorthand, or a definition with a Hook), under
 * which the preview keeps its sample rows (LayoutOptions.sampleRows); undefined for any other source.
 */
export function sourceKey(source: unknown): string | undefined {
  if (isRecord(source)) {
    return typeof source.Hook === 'string' && source.Hook.trim().length > 0 ? `hook:${source.Hook.trim()}` : undefined;
  }
  if (typeof source !== 'string') {
    return undefined;
  }
  const text = source.trim();
  const name = (/^\$\{([\s\S]*)\}$/.exec(text)?.[1] ?? text).trim();
  return /^(hook:|@)\S/i.test(name) ? name : undefined;
}

/** Rich-text markup ([b], [color=…], [link=…], [icon=…]) removed; an icon keeps roughly one glyph pair of room. */
function stripMarkup(text: string): string {
  return text.replace(/\[(\/?)(b|color|link|icon)(=[^\]]*)?\]/gi, (_m, close: string, tag: string) =>
    (!close && tag.toLowerCase() === 'icon' ? '  ' : ''));
}

const truthy = (v: string): boolean => parseBool(v) ?? (v.length > 0 && parseNumber(v) !== 0 && v.toLowerCase() !== 'false');

class Values {
  constructor(private readonly opts: LayoutOptions) {}

  evaluate(expr: string, scope: Scope): string | undefined {
    return this.opts.evaluate?.(expr, scope.vars);
  }

  /**
   * A typed field: a literal when it parses, else (bare-expression kinds) an expression evaluated through
   * opts.evaluate; undefined (unset, the default applies) when it cannot be resolved.
   */
  typed<T>(raw: string | undefined, scope: Scope, parse: (s: string) => T | undefined, bare = true): T | undefined {
    if (raw === undefined) {
      return undefined;
    }
    const r = substituteArgs(raw, scope.args, bare);
    if (!hasTemplate(r)) {
      const literal = parse(r.split('$${').join('${'));
      if (literal !== undefined || !bare) {
        return literal;
      }
    }
    const v = this.evaluate(r, scope);
    return v === undefined ? undefined : parse(v);
  }

  bool(raw: string | undefined, scope: Scope): boolean | undefined {
    if (raw === undefined) {
      return undefined;
    }
    const r = substituteArgs(raw, scope.args, true);
    if (!hasTemplate(r)) {
      const literal = parseBool(r);
      if (literal !== undefined) {
        return literal;
      }
    }
    const v = this.evaluate(r, scope);
    return v === undefined ? undefined : truthy(v);
  }

  int(raw: string | undefined, scope: Scope): number | undefined {
    return this.typed(raw, scope, s => parseInt32(s) ?? (parseNumber(s) !== undefined ? Math.round(parseNumber(s)!) : undefined));
  }

  num(raw: string | undefined, scope: Scope): number | undefined {
    return this.typed(raw, scope, parseNumber);
  }

  /** OptionalInt: null for 'auto' / empty, undefined when unresolved. */
  optInt(raw: string | undefined, scope: Scope): number | null | undefined {
    return this.typed(raw, scope, s => {
      const v = parseOptionalInt(s);
      return v === undefined && parseNumber(s) !== undefined ? Math.round(parseNumber(s)!) : v;
    });
  }

  /** Visible / If / Condition: hidden only by a literal false or an expression that evaluates to 'false'. */
  hides(raw: string | undefined, scope: Scope): boolean {
    if (raw === undefined) {
      return false;
    }
    const r = substituteArgs(raw, scope.args, true);
    if (!hasTemplate(r) && parseBool(r) !== undefined) {
      return parseBool(r) === false;
    }
    return this.evaluate(r, scope)?.trim().toLowerCase() === 'false';
  }

  /** A text field as drawn: evaluated when possible, else literal text with token / expression chips. */
  text(raw: string | undefined, scope: Scope, rich = false): string {
    if (raw === undefined) {
      return '';
    }
    const r = substituteArgs(raw, scope.args, false);
    let out: string;
    if (hasTemplate(r)) {
      const v = this.evaluate(r, scope);
      out = v !== undefined
        ? chipTokens(v)
        : scanTemplate(r).map(s => ('text' in s ? chipTokens(s.text) : CHIP_EXPR + 'ƒ ' + s.expr + CHIP_END)).join('');
    } else {
      out = chipTokens(r.split('$${').join('${'));
    }
    return rich ? stripMarkup(out) : out;
  }
}

// ---------------------------------------------------------------------------------------------------------------------
//  Builder
// ---------------------------------------------------------------------------------------------------------------------

/** DataBuilder.MaxBodyDepth: templates nest at most this deep. */
const MAX_BODY_DEPTH = 8;

const common = new Set((elementTypes.common ?? []).map(c => c.toLowerCase()));
const notArgs = new Set(['type', 'id', 'template', 'args', 'children', 'contenttarget', 'on', 'composite']);

function defNum(type: string, field: string, fallback: number): number {
  const v = defaultOf(type, field);
  const n = v === undefined ? undefined : parseNumber(v);
  return n ?? fallback;
}

function defBool(type: string, field: string, fallback: boolean): boolean {
  const v = defaultOf(type, field);
  return (v === undefined ? undefined : parseBool(v)) ?? fallback;
}

function defText(type: string, field: string, fallback: string): string {
  return defaultOf(type, field) ?? fallback;
}

const stringOf = (v: unknown): string => scalarText(v) ?? JSON.stringify(v);

/** Width / height of a sprite reference when the reference spells it (`asset:Path@x,y,w,h`); items are 16 px. */
function spriteSize(ref: string | undefined): { x: number; y: number } | null {
  if (!ref) {
    return null;
  }
  const at = /@\s*(-?\d+\s*,\s*-?\d+\s*,\s*\d+\s*,\s*\d+)\s*$/.exec(ref);
  const rect = at ? parseRect(at[1]!) : undefined;
  if (rect) {
    return { x: rect[2], y: rect[3] };
  }
  return /^\s*item\s*:/i.test(ref) ? { x: 16, y: 16 } : null;
}

export class Builder {
  private readonly values: Values;

  constructor(private readonly doc: DesignerDocument, private readonly opts: LayoutOptions, private readonly ctx: LayoutContext) {
    this.values = new Values(opts);
  }

  get resolve(): Values {
    return this.values;
  }

  rootScope(): Scope {
    return { args: null, locals: {}, vars: {}, instance: 0, hidden: false, outlets: null, depth: 0 };
  }

  private findTemplate(name: string): TemplateDoc | undefined {
    const key = name.trim().toLowerCase();
    const found = Object.keys(this.doc.templates).find(k => k.trim().toLowerCase() === key);
    return found === undefined ? undefined : this.doc.templates[found];
  }

  private info(src: Src, scope: Scope, kind: string, hidden: boolean, label?: string): ElementInfo {
    const info: ElementInfo = { nodeId: src.id, kind, instance: scope.instance };
    if (label !== undefined && label.length > 0) {
      info.label = label;
    }
    if (hidden) {
      info.hidden = true;
    }
    if (scope.ownerId !== undefined && scope.ownerId !== src.id) {
      info.ownerId = scope.ownerId;
    }
    if (src.raw) {
      info.synthetic = true;
    }
    return info;
  }

  private synthetic(nodeId: NodeId, kind: string, scope: Scope, label?: string): ElementInfo {
    const info: ElementInfo = { nodeId, kind, instance: scope.instance, synthetic: true };
    if (label) {
      info.label = label;
    }
    if (scope.hidden) {
      info.hidden = true;
    }
    return info;
  }

  buildChildren(parent: LContainer, children: Src[], scope: Scope): void {
    for (const child of children) {
      this.buildOne(parent, child, scope);
    }
  }

  /** DataBuilder.BuildOne / BuildIf: an `If` that is false builds nothing. */
  private buildOne(parent: LContainer, src: Src, scope: Scope): void {
    let s = scope;
    if (src.fields.If !== undefined && this.values.hides(src.fields.If, scope)) {
      if (!this.opts.showHidden) {
        return;
      }
      s = { ...scope, hidden: true };
    }
    parent.add(this.buildElement(src, s));
  }

  private buildElement(src: Src, scope: Scope): LElement {
    const v = this.values;
    const f = src.fields;
    const ownHidden = v.hides(f.Visible, scope) || v.hides(f.Condition, scope);
    const hidden = scope.hidden || ownHidden;
    const inner: Scope = hidden === scope.hidden ? scope : { ...scope, hidden };
    const canonical = canonicalType(src.type === 'Menu' ? 'Stack' : src.type);
    let element: LElement;
    if (canonical === 'Template' || (canonical === null && this.findTemplate(src.type) !== undefined)) {
      element = this.createTemplate(src, inner, hidden, canonical === 'Template' ? f.Template ?? '' : src.type);
    } else if (canonical === null || canonical === 'Composite') {
      const name = canonical === 'Composite' ? f.Composite ?? 'Composite' : src.type;
      element = new LPlaceholder(this.ctx, this.info(src, scope, canonical ?? 'Custom', hidden, name));
    } else {
      element = this.create(src, canonical, inner, hidden);
    }
    this.applyCommon(element, src, scope);
    if (ownHidden && !this.opts.showHidden) {
      element.visible = false;
    }
    const enabled = v.bool(f.Enabled, scope);
    if (enabled === false && element.info) {
      element.info.detail = { ...element.info.detail, enabled: false };
    }
    return element;
  }

  /** DataBuilder.ApplyCommon (layout members). */
  private applyCommon(e: LElement, src: Src, scope: Scope): void {
    const v = this.values;
    const f = src.fields;
    const margin = v.typed(f.Margin, scope, parseMargin, false);
    if (margin) {
      e.setMargin(margin[0], margin[1], margin[2], margin[3]);
    }
    const ml = v.int(f.MarginLeft, scope);
    if (ml !== undefined) e.marginLeft = ml;
    const mt = v.int(f.MarginTop, scope);
    if (mt !== undefined) e.marginTop = mt;
    const mr = v.int(f.MarginRight, scope);
    if (mr !== undefined) e.marginRight = mr;
    const mb = v.int(f.MarginBottom, scope);
    if (mb !== undefined) e.marginBottom = mb;
    const w = v.optInt(f.Width, scope);
    if (w !== undefined) e.width = w;
    const h = v.optInt(f.Height, scope);
    if (h !== undefined) e.height = h;
    const minW = v.optInt(f.MinWidth, scope);
    if (minW !== undefined) e.minWidth = minW;
    const maxW = v.optInt(f.MaxWidth, scope);
    if (maxW !== undefined) e.maxWidth = maxW;
    const ha = v.typed(f.HorizontalAlign, scope, parseAlign, false);
    if (ha) e.horizontalAlign = ha;
    const va = v.typed(f.VerticalAlign, scope, parseAlign, false);
    if (va) e.verticalAlign = va;
    const x = v.int(f.X, scope);
    if (x !== undefined) e.x = x;
    const y = v.int(f.Y, scope);
    if (y !== undefined) e.y = y;
    const cellPair = v.typed(f.Cell, scope, parsePair, false);
    if (cellPair) {
      e.row = cellPair[0];
      e.column = cellPair[1];
    }
    const span = v.typed(f.Span, scope, parsePair, false);
    if (span) {
      e.rowSpan = span[0];
      e.columnSpan = span[1];
    }
    const row = v.int(f.Row, scope);
    if (row !== undefined) e.row = row;
    const col = v.int(f.Column, scope);
    if (col !== undefined) e.column = col;
    const rs = v.int(f.RowSpan, scope);
    if (rs !== undefined) e.rowSpan = rs;
    const cs = v.int(f.ColumnSpan, scope);
    if (cs !== undefined) e.columnSpan = cs;
  }

  private styleField(src: Src, name: string): string | undefined {
    const style = src.extra.Style;
    if (style && typeof style === 'object' && !Array.isArray(style)) {
      return scalarText((style as Record<string, unknown>)[name]);
    }
    return undefined;
  }

  private font(src: Src, scope: Scope, own: boolean): 'small' | 'dialogue' | 'tiny' {
    return (own ? this.values.typed(src.fields.Font, scope, parseFont, false) : undefined)
      ?? (this.styleField(src, 'Font') !== undefined ? parseFont(this.styleField(src, 'Font')!) : undefined)
      ?? 'small';
  }

  /** IUIStack members (ApplyTypeMembers): Horizontal, Spacing, Alignment, Wrap. */
  private applyStack(stack: LStack, src: Src, scope: Scope): void {
    const v = this.values;
    const horizontal = v.bool(src.fields.Horizontal, scope);
    if (horizontal !== undefined) stack.horizontal = horizontal;
    const spacing = v.int(src.fields.Spacing, scope);
    if (spacing !== undefined) stack.spacing = spacing;
    const alignment = v.typed(src.fields.Alignment, scope, parseAlign, false);
    if (alignment) stack.alignment = alignment;
    const wrap = v.bool(src.fields.Wrap, scope);
    if (wrap !== undefined) stack.wrap = wrap;
  }

  /** DataBuilder.Create + ApplyTypeMembers + the container's children. */
  private create(src: Src, type: string, scope: Scope, hidden: boolean): LElement {
    const v = this.values;
    const f = src.fields;
    const ctx = this.ctx;
    const info = (label?: string): ElementInfo => this.info(src, scope, type, hidden, label);
    switch (type) {
      case 'Stack':
      case 'Outlet': {
        const stack = new LStack(ctx, info(f.Id), defBool(type, 'Horizontal', false), defNum(type, 'Spacing', 0));
        this.applyStack(stack, src, scope);
        if (type === 'Outlet') {
          this.buildOutlet(stack, src, scope);
        } else {
          this.buildChildren(stack, src.children(), scope);
        }
        return stack;
      }
      case 'Switch': {
        // DataBuilder.BuildElement: a Switch is a vertical stack with no spacing holding the selected page
        const stack = new LStack(ctx, info(f.Switch !== undefined ? 'ƒ ' + f.Switch : undefined), false, 0);
        const page = this.selectPage(src, scope);
        if (page) {
          this.buildOne(stack, page, scope);
        }
        return stack;
      }
      case 'Repeat': {
        const stack = new LStack(ctx, info(f.Repeat ?? stringOf(src.extra.Repeat ?? '')), defBool(type, 'Horizontal', false), defNum(type, 'Spacing', 0));
        this.applyStack(stack, src, scope);
        const template = src.children();
        const rows = this.inlineRows(f.Repeat ?? src.extra.Repeat, scope);
        for (let i = 0; i < (rows?.length ?? Math.max(0, this.opts.repeatCount)); i++) {
          this.buildChildren(stack, template, this.rowScope(scope, rows, i, f.As));
        }
        return stack;
      }
      case 'Slot': {
        // Components/Slot.cs: a stack for other mods' contributions (none in the designer)
        const slot = new LStack(ctx, info(f.Id), false, 0);
        this.applyStack(slot, src, scope);
        const maxHeight = v.optInt(f.MaxHeight, scope);
        if (maxHeight !== undefined) slot.maxHeight = maxHeight;
        return slot;
      }
      case 'Grid': {
        const grid = new LGrid(ctx, info(f.Id), defText(type, 'Columns', '*'), defText(type, 'Rows', 'auto'));
        const columns = this.gridColumns(src);
        if (columns !== undefined) grid.columns = substituteArgs(columns, scope.args, false);
        const rows = v.typed(f.Rows, scope, s => s, false);
        if (rows !== undefined) grid.rows = rows;
        const cs = v.int(f.ColumnSpacing, scope);
        if (cs !== undefined) grid.columnSpacing = cs;
        const rsp = v.int(f.RowSpacing, scope);
        if (rsp !== undefined) grid.rowSpacing = rsp;
        this.buildChildren(grid, src.children(), scope);
        return grid;
      }
      case 'Panel': {
        const panel = new LPanel(ctx, info(f.Id), defBool(type, 'DrawBox', false), defNum(type, 'Padding', 0));
        const drawBox = v.bool(f.DrawBox, scope);
        if (drawBox !== undefined) panel.drawBox = drawBox;
        const padding = v.int(f.Padding, scope);
        if (padding !== undefined) panel.padding = padding;
        const stylePadding = this.styleField(src, 'Padding');
        if (stylePadding !== undefined) panel.stylePadding = parseInt32(stylePadding) ?? null;
        panel.info!.detail = { drawBox: panel.drawBox };
        this.buildChildren(panel, src.children(), scope);
        return panel;
      }
      case 'Canvas': {
        const canvas = new LCanvas(ctx, info(f.Id));
        this.buildChildren(canvas, src.children(), scope);
        return canvas;
      }
      case 'ScrollView': {
        const scroll = new LScrollView(ctx, info(f.Id), defNum(type, 'ViewportHeight', 0));
        const vh = v.int(f.ViewportHeight, scope);
        if (vh !== undefined) scroll.viewportHeight = vh;
        const show = v.bool(f.ShowScrollbar, scope);
        if (show !== undefined) scroll.showScrollbar = show;
        this.buildChildren(scroll, src.children(), scope);
        return scroll;
      }
      case 'Spacer': {
        const spacer = new LSpacer(ctx, info());
        // the Spacer constructor's width / height (0 = unset) come from the data defaults
        const dw = defNum(type, 'Width', 0);
        const dh = defNum(type, 'Height', 0);
        spacer.width = dw > 0 ? dw : null;
        spacer.height = dh > 0 ? dh : null;
        spacer.line = v.bool(f.Line, scope) ?? false;
        spacer.info!.detail = { line: spacer.line };
        return spacer;
      }
      case 'Label': {
        const label = new LLabel(ctx, null);
        const rich = v.bool(f.RichText, scope) ?? false;
        label.text = v.text(f.Text ?? f.Label, scope, rich);
        label.font = this.font(src, scope, true);
        label.wrap = v.bool(f.Wrap, scope) ?? false;
        label.shrink = v.bool(f.Shrink, scope) ?? false;
        label.scale = v.num(f.Scale, scope) ?? 1;
        label.info = info(label.text);
        const detail: ElementInfo['detail'] = {};
        const align = v.typed(f.TextAlign, scope, parseAlign, false);
        if (align) detail.textAlign = align;
        const color = f.Color !== undefined && !hasTemplate(f.Color) ? cssColor(f.Color) : undefined;
        if (color) detail.color = color;
        label.info.detail = detail;
        return label;
      }
      case 'Button': {
        const button = new LButton(ctx, null);
        button.text = v.text(f.Text ?? f.Button, scope, v.bool(f.RichText, scope) ?? false);
        button.font = this.font(src, scope, true);
        button.drawBox = v.bool(f.DrawBox, scope) ?? true;
        button.shrink = v.bool(f.Shrink, scope) ?? false;
        if (f.Icon !== undefined) {
          // Button.IconScale defaults to 4; a sprite whose size the reference does not spell counts as 16 px
          const scale = v.num(f.IconScale, scope) ?? 4;
          const size = spriteSize(f.Icon) ?? { x: 16, y: 16 };
          button.iconSize = { x: size.x * scale, y: size.y * scale };
        }
        button.info = info(button.text);
        button.info.detail = { drawBox: button.drawBox, ...(f.Icon !== undefined ? { sprite: v.text(f.Icon, scope) } : {}) };
        return button;
      }
      case 'Checkbox': {
        const box = new LCheckbox(ctx, null);
        box.label = v.text(f.Label ?? f.Checkbox ?? f.Text, scope);
        box.font = this.font(src, scope, false);
        box.shrink = v.bool(f.Shrink, scope) ?? false;
        box.info = info(box.label);
        box.info.detail = { checked: v.bool(f.Value, scope) ?? false };
        return box;
      }
      case 'TextInput':
      case 'NumberInput': {
        const input = new LTextBox(ctx, null);
        input.font = this.font(src, scope, false);
        const value = f.Value !== undefined ? v.text(f.Value, scope) : type === 'NumberInput' ? defText(type, 'Min', '0') : '';
        const placeholder = f.Placeholder !== undefined ? v.text(f.Placeholder, scope) : undefined;
        input.info = info(value || placeholder);
        input.info.detail = { value, ...(placeholder !== undefined ? { placeholder } : {}) };
        return input;
      }
      case 'Dropdown': {
        const dropdown = new LDropdown(ctx, null);
        dropdown.font = this.font(src, scope, false);
        dropdown.shrink = v.bool(f.Shrink, scope) ?? false;
        const sourced = this.sourcedChoices(src, scope);
        const choices = sourced?.values ?? this.stringList(src, 'Choices');
        const labels = sourced?.labels ?? this.stringList(src, 'Labels');
        dropdown.labels = choices.map((c, i) => v.text(labels[i] ?? c, scope));
        const value = f.Value !== undefined ? v.text(f.Value, scope) : choices[0] !== undefined ? v.text(choices[0], scope) : '';
        const index = choices.findIndex(c => c === f.Value);
        const shown = index >= 0 ? dropdown.labels[index] ?? value : value;
        dropdown.info = info(shown);
        dropdown.info.detail = { value: shown };
        return dropdown;
      }
      case 'Slider': {
        const slider = new LSlider(ctx, null);
        const min = v.num(f.Min, scope) ?? defNum(type, 'Min', 0);
        const max = v.num(f.Max, scope) ?? defNum(type, 'Max', 0);
        const value = v.num(f.Value, scope) ?? min;
        slider.info = info(f.Value !== undefined ? v.text(f.Value, scope) : undefined);
        slider.info.detail = { fraction: max > min ? Math.min(1, Math.max(0, (value - min) / (max - min))) : 0 };
        return slider;
      }
      case 'Image': {
        const image = new LImage(ctx, null);
        const sprite = f.Sprite ?? f.Image;
        const source = f.Source ?? scalarText((src.extra.Source as Record<string, unknown> | undefined)?.Shorthand);
        const rect = source !== undefined ? v.typed(source, scope, parseRect, false) : undefined;
        const resolved = sprite !== undefined ? v.text(sprite, scope) : undefined;
        image.sourceSize = rect ? { x: rect[2], y: rect[3] } : spriteSize(resolved) ?? { x: 16, y: 16 };
        image.scale = v.num(f.Scale, scope) ?? defNum(type, 'Scale', 1);
        image.info = info(resolved);
        image.info.detail = resolved !== undefined ? { sprite: resolved } : {};
        return image;
      }
      case 'ItemImage': {
        const item = new LItemImage(ctx, null);
        item.scale = v.num(f.Scale, scope) ?? defNum(type, 'Scale', 1);
        const ref = f.Item !== undefined ? v.text(f.Item, scope) : undefined;
        item.info = info(ref);
        item.info.detail = ref !== undefined ? { sprite: ref } : {};
        return item;
      }
      case 'List':
        return this.createList(src, scope, info(f.Id));
      case 'DataGrid':
        return this.createDataGrid(src, scope, info(f.Id));
      case 'Form':
        return this.createForm(src, scope, info(f.Id));
      default:
        return new LPlaceholder(ctx, info(type));
    }
  }

  /** A list member written as a JSON array (or a comma list in a string field). */
  /**
   * SourcedChoices: a ChoicesSource's rows as choices when the preview has them, each row's ChoiceValue (else the row)
   * and ChoiceLabel (else the value) evaluated in its row scope; null without rows.
   */
  private sourcedChoices(src: Src, scope: Scope): { values: string[]; labels: string[] } | null {
    const source = src.extra.ChoicesSource ?? src.fields.ChoicesSource;
    const rows = source !== undefined ? this.inlineRows(source, scope) : null;
    if (!rows) {
      return null;
    }
    const values: string[] = [];
    const labels: string[] = [];
    rows.forEach((row, i) => {
      const rowScope = this.rowScope(scope, rows, i, src.fields.As);
      const value = src.fields.ChoiceValue !== undefined ? this.values.text(src.fields.ChoiceValue, rowScope) : stringOf(row);
      values.push(value);
      labels.push(src.fields.ChoiceLabel !== undefined ? this.values.text(src.fields.ChoiceLabel, rowScope) : value);
    });
    return { values, labels };
  }

  private stringList(src: Src, name: string): string[] {
    const value = src.extra[name] ?? src.fields[name];
    if (Array.isArray(value)) {
      return value.map(stringOf);
    }
    return typeof value === 'string' ? value.split(',').map(s => s.trim()).filter(s => s.length > 0) : [];
  }

  /** Grid Columns: a track string, or column definitions joined like DataBuilder.GridTracks. */
  private gridColumns(src: Src): string | undefined {
    const value = src.extra.Columns ?? src.fields.Columns;
    if (Array.isArray(value)) {
      return value.length === 0 ? undefined : value.map(c => {
        const width = c && typeof c === 'object' ? scalarText((c as Record<string, unknown>).Width) : scalarText(c);
        return width === undefined || width.trim().length === 0 ? '*' : width.trim();
      }).join(', ');
    }
    return typeof value === 'string' ? value : undefined;
  }

  /** SwitchRefresher.Select: the designer's chosen case, else the Switch expression's value, else the first page. */
  private selectPage(src: Src, scope: Scope): Src | undefined {
    const pages = src.children();
    const key = (!src.raw ? this.opts.switchCases?.[src.id] : undefined)
      ?? (src.fields.Switch !== undefined ? this.values.evaluate(substituteArgs(src.fields.Switch, scope.args, true), scope) : undefined);
    if (key !== undefined) {
      let fallback: Src | undefined;
      for (const page of pages) {
        const raw = page.fields.Case;
        if (raw === undefined || raw.trim() === '*') {
          fallback ??= page;
          continue;
        }
        if (raw.split(',').some(c => c.trim().toLowerCase() === key.trim().toLowerCase())) {
          return page;
        }
      }
      if (fallback) {
        return fallback;
      }
    }
    return pages[0];
  }

  // ------------------------------------------------------------------------------------------------------------------
  //  Templates (DataBuilder.Composite.cs)
  // ------------------------------------------------------------------------------------------------------------------

  private createTemplate(src: Src, scope: Scope, hidden: boolean, name: string): LElement {
    const v = this.values;
    const template = this.findTemplate(name);
    if (!template) {
      return new LPlaceholder(this.ctx, this.info(src, scope, 'Template', hidden, name));
    }
    const host = new LStack(this.ctx, this.info(src, scope, 'Template', hidden, name),
      v.bool(template.fields.Horizontal, scope) ?? false, v.int(template.fields.Spacing, scope) ?? 0);
    const alignment = v.typed(template.fields.Alignment, scope, parseAlign, false);
    if (alignment) {
      host.alignment = alignment;
    }
    if (scope.depth >= MAX_BODY_DEPTH) {
      return host;
    }
    // TemplateArgs.TryGet: the instance's argument (any case), else the parameter's Default; an argument is stored
    // under its parameter's name so it replaces that default
    const args: Record<string, string> = {};
    const params = Object.keys(template.params);
    for (const [param, def] of Object.entries(template.params)) {
      const d = isRecord(def) ? def.Default : undefined;
      if (d !== undefined && d !== null) {
        args[param] = stringOf(d);
      }
    }
    // DataValidator.ExpandTag: every member but the common ones (and Type, Children …) is an argument, then Args
    const given: Record<string, unknown> = {};
    for (const [k, value] of [...Object.entries(src.fields), ...Object.entries(src.extra)]) {
      if (!common.has(k.toLowerCase()) && !notArgs.has(k.toLowerCase())) {
        given[k] = value;
      }
    }
    if (isRecord(src.extra.Args)) {
      Object.assign(given, src.extra.Args);
    }
    for (const [k, value] of Object.entries(given)) {
      const name = params.find(p => p.toLowerCase() === k.trim().toLowerCase()) ?? k.trim();
      // an argument is evaluated in the instance's scope (DataArgument)
      args[name] = substituteArgs(stringOf(value), scope.args, false);
    }
    const routes = new Map<string, Src[]>();
    for (const child of src.children()) {
      const outlet = canonicalType(child.type) === 'Outlet' ? '' : normalizeOutlet(child.fields.Outlet);
      const list = routes.get(outlet) ?? [];
      list.push(child);
      routes.set(outlet, list);
    }
    const outlets: Outlets = { routes, taken: new Set(), scope };
    const body = fromNode(this.doc, template.root);
    const bodyScope: Scope = {
      args,
      locals: scope.locals,
      vars: scope.vars,
      instance: scope.instance,
      ownerId: scope.ownerId ?? src.id,
      hidden: scope.hidden,
      outlets,
      depth: scope.depth + 1
    };
    this.buildChildren(host, body ? body.children() : [], bodyScope);
    // PlaceUnrouted: children for outlets the body does not have go at the end
    for (const [outlet, children] of routes) {
      if (!outlets.taken.has(outlet)) {
        this.buildChildren(host, children, scope);
      }
    }
    return host;
  }

  /** DataBuilder.BuildOutlet: the instance's children routed here, else the outlet's own (fallback) children. */
  private buildOutlet(placeholder: LStack, src: Src, scope: Scope): void {
    const outlets = scope.outlets;
    const name = normalizeOutlet(src.fields.Outlet);
    if (outlets && !outlets.taken.has(name)) {
      const routed = outlets.routes.get(name);
      outlets.taken.add(name);
      if (routed && routed.length > 0) {
        this.buildChildren(placeholder, routed, outlets.scope);
        return;
      }
    }
    this.buildChildren(placeholder, src.children(), scope);
  }

  // ------------------------------------------------------------------------------------------------------------------
  //  Collections and forms
  // ------------------------------------------------------------------------------------------------------------------

  /**
   * The rows of a collection source when the preview has them (SourceBinding): a Sources entry or inline definition
   * with Rows, an inline array, the sample rows of a source C# provides (sourceKey), or a row local holding an array
   * (`${season.crops}`); null otherwise.
   */
  private inlineRows(source: unknown, scope: Scope): unknown[] | null {
    if (Array.isArray(source)) {
      return source;
    }
    if (isRecord(source)) {
      return Array.isArray(source.Rows) ? source.Rows : this.sampleRows(source);
    }
    if (typeof source !== 'string') {
      return null;
    }
    const text = source.trim();
    const name = (/^\$\{([\s\S]*)\}$/.exec(text)?.[1] ?? text).trim();
    const sources = this.doc.menuExtra.Sources;
    const key = isRecord(sources) ? Object.keys(sources).find(k => k.trim().toLowerCase() === name.toLowerCase()) : undefined;
    if (key !== undefined) {
      const def = (sources as Record<string, unknown>)[key];
      return isRecord(def) && Array.isArray(def.Rows) ? def.Rows : this.sampleRows(def);
    }
    const sample = this.sampleRows(name);
    if (sample) {
      return sample;
    }
    let value: unknown = scope.locals;
    for (const part of name.split('.')) {
      value = isRecord(value) ? value[part] : undefined;
    }
    return Array.isArray(value) ? value : null;
  }

  /** The preview's sample rows of a source C# provides (LayoutOptions.sampleRows, keys matched without case); null when it has none. */
  private sampleRows(source: unknown): unknown[] | null {
    const name = sourceKey(source)?.toLowerCase();
    const rows = this.opts.sampleRows;
    const key = name !== undefined && rows ? Object.keys(rows).find(k => k.toLowerCase() === name) : undefined;
    return key !== undefined ? rows![key]! : null;
  }

  /** RowScope.For: row `index` of `rows` as row, index, the As alias and <alias>Index (only the instance without rows). */
  private rowScope(scope: Scope, rows: unknown[] | null, index: number, as: string | undefined): Scope {
    if (rows === null) {
      return { ...scope, instance: index };
    }
    const row = rows[index];
    const locals: Record<string, unknown> = { ...scope.locals, row, index };
    const alias = as?.trim();
    if (alias) {
      locals[alias] = row;
      locals[`${alias}Index`] = index;
    }
    return { ...scope, instance: index, locals, vars: flattenLocals(locals) };
  }

  private rowTemplate(src: Src): Src[] {
    const own = src.children();
    return own.length > 0 ? own : rawList(src.extra.RowTemplate, src.id);
  }

  /** DataBuilder.CreateList + ListView rows (Panel rows, no box, no padding). */
  private createList(src: Src, scope: Scope, info: ElementInfo): LElement {
    const v = this.values;
    const rowHeight = Math.max(1, v.int(src.fields.RowHeight, scope) ?? defNum('List', 'RowHeight', 1));
    const visibleRows = Math.max(1, v.int(src.fields.VisibleRows, scope) ?? defNum('List', 'VisibleRows', 1));
    const list = new LListView(this.ctx, info, rowHeight, visibleRows);
    const template = this.rowTemplate(src);
    const rows = this.inlineRows(src.fields.Source ?? src.extra.Source, scope);
    for (let i = 0; i < visibleRows; i++) {
      const rowScope: Scope = { ...this.rowScope(scope, rows, i, src.fields.As), ownerId: scope.ownerId ?? (src.raw ? undefined : src.id) };
      const row = new LPanel(this.ctx, this.synthetic(src.id, 'List.row', rowScope), false, 0);
      row.visible = i < (rows?.length ?? this.opts.repeatCount);
      if (row.visible) {
        this.buildChildren(row, template, rowScope);
      }
      list.add(row);
    }
    return list;
  }

  /** DataBuilder.CreateDataGrid / DataGridColumn (Width "*", MinWidth 0, Sortable false by default) over its Column items. */
  private createDataGrid(src: Src, scope: Scope, info: ElementInfo): LElement {
    const v = this.values;
    const rowHeight = Math.max(1, v.int(src.fields.RowHeight, scope) ?? defNum('DataGrid', 'RowHeight', 1));
    const visibleRows = Math.max(1, v.int(src.fields.VisibleRows, scope) ?? defNum('DataGrid', 'VisibleRows', 1));
    const grid = new LDataGrid(this.ctx, info, rowHeight, visibleRows);
    const source = this.inlineRows(src.fields.Source ?? src.extra.Source, scope);
    const rowScopes = Array.from({ length: Math.min(source?.length ?? Math.max(0, this.opts.repeatCount), visibleRows) },
      (_, i) => this.rowScope(scope, source, i, src.fields.As));
    for (const rowScope of rowScopes) {
      grid.rows.push(new LPanel(this.ctx, this.synthetic(src.id, 'DataGrid.row', rowScope), false, 0));
    }
    for (const col of src.children()) {
      const def = col.fields;
      const header = new LLabel(this.ctx, null);
      header.text = v.text(def.Header ?? def.Id ?? '', scope);
      header.info = this.synthetic(col.id, 'DataGrid.header', scope, header.text);
      header.info.detail = {};
      const column: GridColumn = {
        track: parseTracks(def.Width ?? '*')[0] ?? { type: 'star', value: 1 },
        minWidth: Math.max(0, parseInt32(def.MinWidth ?? '0') ?? 0),
        sortable: parseBool(def.Sortable ?? 'false') ?? false,
        header,
        cells: []
      };
      const cellDefs = col.children();
      for (const rowScope of rowScopes) {
        if (cellDefs.length > 0) {
          // a cell's elements belong to the column: clicking one selects it
          const panel = new LPanel(this.ctx, null, false, 0);
          this.buildChildren(panel, cellDefs, { ...rowScope, ownerId: scope.ownerId ?? (col.raw ? undefined : col.id) });
          column.cells.push(panel);
        } else {
          const label = new LLabel(this.ctx, null);
          label.text = v.text(def.Text ?? '', rowScope);
          label.verticalAlign = 'center';
          label.info = this.synthetic(col.id, 'DataGrid.cell', rowScope, label.text);
          label.info.detail = {};
          column.cells.push(label);
        }
      }
      grid.columns.push(column);
    }
    for (const c of grid.columns) {
      for (const e of [c.header, ...c.cells]) {
        e.parent = grid;
      }
    }
    for (const r of grid.rows) {
      r.parent = grid;
    }
    return grid;
  }

  /** Core/AutoForm.cs over a data Form's FormField items (AutoFormField: caption, control; DataBuilder.Form kinds). */
  private createForm(src: Src, scope: Scope, info: ElementInfo): LElement {
    const v = this.values;
    const ctx = this.ctx;
    const form = new LForm(ctx, info);
    const grid = new LGrid(ctx, null, 'auto,*', 'auto');
    grid.columnSpacing = 24;
    grid.rowSpacing = 12;
    grid.horizontalAlign = 'stretch';
    let row = 0;
    src.children().forEach((field, index) => {
      const def = field.fields;
      if (def.Section !== undefined) {
        if (row > 0) {
          const gap = new LSpacer(ctx, null);
          gap.height = 8;
          gap.row = row++;
          gap.columnSpan = 2;
          grid.add(gap);
        }
        const header = new LLabel(ctx, null);
        header.text = v.text(def.Section, scope);
        header.font = 'dialogue';
        header.wrap = true;
        header.row = row++;
        header.columnSpan = 2;
        header.info = this.synthetic(field.id, 'Form.section', scope, header.text);
        header.info.detail = {};
        grid.add(header);
      }
      const caption = new LLabel(ctx, null);
      caption.text = v.text(def.Label ?? def.Id ?? `field ${index + 1}`, scope);
      caption.verticalAlign = 'center';
      caption.row = row;
      caption.info = this.synthetic(field.id, 'Form.label', scope, caption.text);
      caption.info.detail = {};
      const cell = new LStack(ctx, null, false, 4);
      cell.row = row;
      cell.column = 1;
      const sourced = this.sourcedChoices(field, scope);
      const choices = sourced?.labels ?? this.stringList(field, 'Choices');
      const kind = (def.Kind ?? (choices.length > 0 || (field.extra.ChoicesSource ?? def.ChoicesSource) !== undefined ? 'Dropdown' : 'Text')).trim().toLowerCase();
      let control: LElement;
      if (kind === 'checkbox') {
        control = new LCheckbox(ctx, this.synthetic(field.id, 'Checkbox', scope));
        control.info!.detail = { checked: false };
      } else if (kind === 'dropdown') {
        const dropdown = new LDropdown(ctx, null);
        dropdown.labels = choices.map(c => v.text(c, scope));
        dropdown.info = this.synthetic(field.id, 'Dropdown', scope, dropdown.labels[0]);
        dropdown.info.detail = { value: dropdown.labels[0] ?? '' };
        control = dropdown;
      } else {
        control = new LTextBox(ctx, this.synthetic(field.id, kind === 'number' || kind === 'integer' ? 'NumberInput' : 'TextInput', scope));
        control.info!.detail = { value: def.Value ?? '' };
      }
      cell.add(control);
      grid.add(caption);
      grid.add(cell);
      row++;
    });
    form.add(grid);
    const buttons = new LStack(ctx, null, true, 16);
    buttons.horizontalAlign = 'center';
    for (const text of ['Save', 'Cancel', 'Undo', 'Redo']) {
      const button = new LButton(ctx, this.synthetic(src.id, 'Button', scope, text));
      button.text = text;
      button.info!.detail = { drawBox: true };
      buttons.add(button);
    }
    buttons.visible = v.bool(src.fields.ShowButtons, scope) ?? true;
    form.add(buttons);
    return form;
  }
}

/** Row locals as the dotted scalar names the evaluator reads: { row: { price: 35 } } → row.price = "35". */
export function flattenLocals(locals: Record<string, unknown>): Record<string, string> {
  const out: Record<string, string> = {};
  const walk = (name: string, value: unknown): void => {
    // null as the text the evaluator reads back as null (row.item != null)
    const text = value === null ? 'null' : scalarText(value);
    if (text !== undefined) {
      out[name] = text;
    } else if (isRecord(value)) {
      for (const [k, v] of Object.entries(value)) {
        walk(`${name}.${k}`, v);
      }
    }
  };
  for (const [k, v] of Object.entries(locals)) {
    walk(k, v);
  }
  return out;
}

/** OutletBinding.Normalize: trimmed, "default" = the unnamed outlet. */
function normalizeOutlet(name: string | undefined): string {
  const text = name?.trim() ?? '';
  return text.toLowerCase() === 'default' ? '' : text.toLowerCase();
}

export { Values };
export type { Scope };
