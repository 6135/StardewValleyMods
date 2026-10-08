// Port of the data builder's element construction (Data/Building/DataBuilder.cs Create / ApplyTypeMembers /
// ApplyCommon, DataBuilder.Structure.cs If / Switch, DataBuilder.Repeat.cs, DataBuilder.List.cs, DataBuilder.Grid.cs,
// DataBuilder.Composite.cs CreateTemplate / BuildOutlet) over the designer document, producing the layout tree.
import type { DesignerDocument, NodeId, TemplateDoc } from '../model/document';
import { canonicalType, elementTypes } from '../model/metadata';
import { creators } from './buildCreate';
import type { BuildHost } from './buildHost';
import { LContainer, LElement, LPlaceholder, LStack, type ElementInfo, type LayoutContext } from './elements';
import { flattenLocals, sourceKey, substituteArgs, Values, type Outlets, type Scope } from './scope';
import { fromNode, isRecord, rawList, scalarText, stringOf, type Src } from './source';
import type { LayoutOptions } from './types';
import { parseAlign, parseFont, parseMargin, parsePair } from './values';

// ---------------------------------------------------------------------------------------------------------------------
//  Builder
// ---------------------------------------------------------------------------------------------------------------------

/** DataBuilder.MaxBodyDepth: templates nest at most this deep. */
const MAX_BODY_DEPTH = 8;

const common = new Set((elementTypes.common ?? []).map(c => c.toLowerCase()));
const notArgs = new Set(['type', 'id', 'template', 'args', 'children', 'contenttarget', 'on', 'composite']);

export class Builder implements BuildHost {
  private readonly values: Values;
  /** The layout-affecting expressions (LayoutResult.unresolved) that could not be evaluated, in build order. */
  readonly unresolved: string[] = [];

  constructor(private readonly doc: DesignerDocument, readonly opts: LayoutOptions, readonly ctx: LayoutContext) {
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

  synthetic(nodeId: NodeId, kind: string, scope: Scope, label?: string): ElementInfo {
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
  buildOne(parent: LContainer, src: Src, scope: Scope): void {
    let s = scope;
    if (src.fields.If !== undefined && this.values.collect(this.unresolved, () => this.values.hides(src.fields.If, scope))) {
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
    const ownHidden = v.collect(this.unresolved, () => v.hides(f.Visible, scope) || v.hides(f.Condition, scope));
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

  /** DataBuilder.ApplyCommon (layout members); an element whose members could not all be evaluated is marked `unresolved`. */
  private applyCommon(e: LElement, src: Src, scope: Scope): void {
    const failed: string[] = [];
    this.values.collect(failed, () => this.readCommon(e, src, scope));
    if (failed.length > 0) {
      this.unresolved.push(...failed);
      if (e.info) {
        e.info.unresolved = true;
      }
    }
  }

  private readCommon(e: LElement, src: Src, scope: Scope): void {
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

  styleField(src: Src, name: string): string | undefined {
    const style = src.extra.Style;
    if (style && typeof style === 'object' && !Array.isArray(style)) {
      return scalarText((style as Record<string, unknown>)[name]);
    }
    return undefined;
  }

  font(src: Src, scope: Scope, own: boolean): 'small' | 'dialogue' | 'tiny' {
    return (own ? this.values.typed(src.fields.Font, scope, parseFont, false) : undefined)
      ?? (this.styleField(src, 'Font') !== undefined ? parseFont(this.styleField(src, 'Font')!) : undefined)
      ?? 'small';
  }

  /** IUIStack members (ApplyTypeMembers): Horizontal, Spacing, Alignment, Wrap. */
  applyStack(stack: LStack, src: Src, scope: Scope): void {
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

  /** DataBuilder.Create + ApplyTypeMembers + the container's children (buildCreate.ts). */
  private create(src: Src, type: string, scope: Scope, hidden: boolean): LElement {
    const info = (label?: string): ElementInfo => this.info(src, scope, type, hidden, label);
    const creator = Object.prototype.hasOwnProperty.call(creators, type) ? creators[type] : undefined;
    return creator ? creator(this, src, type, scope, info) : new LPlaceholder(this.ctx, info(type));
  }

  /**
   * SourcedChoices: a ChoicesSource's rows as choices when the preview has them, each row's ChoiceValue (else the row)
   * and ChoiceLabel (else the value) evaluated in its row scope; null without rows.
   */
  sourcedChoices(src: Src, scope: Scope): { values: string[]; labels: string[] } | null {
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

  stringList(src: Src, name: string): string[] {
    const value = src.extra[name] ?? src.fields[name];
    if (Array.isArray(value)) {
      return value.map(stringOf);
    }
    return typeof value === 'string' ? value.split(',').map(s => s.trim()).filter(s => s.length > 0) : [];
  }

  /** Grid Columns: a track string, or column definitions joined like DataBuilder.GridTracks. */
  gridColumns(src: Src): string | undefined {
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
  selectPage(src: Src, scope: Scope): Src | undefined {
    const pages = src.children();
    const key = (!src.raw ? this.opts.switchCases?.[src.id] : undefined)
      ?? (src.fields.Switch !== undefined
        ? this.values.collect(this.unresolved, () => this.values.evaluate(substituteArgs(src.fields.Switch!, scope.args, true), scope))
        : undefined);
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
  buildOutlet(placeholder: LStack, src: Src, scope: Scope): void {
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
  inlineRows(source: unknown, scope: Scope): unknown[] | null {
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
    const name = (text.startsWith('${') && text.endsWith('}') ? text.slice(2, -1) : text).trim();
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
  rowScope(scope: Scope, rows: unknown[] | null, index: number, as: string | undefined): Scope {
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

  rowTemplate(src: Src): Src[] {
    const own = src.children();
    return own.length > 0 ? own : rawList(src.extra.RowTemplate, src.id);
  }
}

/** OutletBinding.Normalize: trimmed, "default" = the unnamed outlet. */
function normalizeOutlet(name: string | undefined): string {
  const text = name?.trim() ?? '';
  return text.toLowerCase() === 'default' ? '' : text.toLowerCase();
}

export { chipTokens, flattenLocals, sourceKey, Values, type Scope } from './scope';
export { fromNode, fromRaw, type Src } from './source';
