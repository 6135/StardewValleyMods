// Port of the framework's measure / arrange pass: Core/UIElement.cs, Core/UIContainer.cs and the components'
// MeasureCore / MinWidthCore / ArrangeCore (Components/*.cs, Core/AutoForm.cs). Containers are ported line by line;
// leaves use the framework's fixed sizes and the injected text measurer. The theme's SpacingScale / FontScale are the
// vanilla 1 (Theme.Space, Theme.ScaleForText and Theme.ExtraTextHeight are then identities / 0).
import type { NodeId } from '../model/document';
import {
  type Align, type GridTrack, alignOffset, distributeWidth, parseTracks, resolveTracks, roundEven, spanSize, toInt,
  trackOffset, unbounded
} from './engine';
import { CHIP_MARKS, type BoxDetail, type FontName, type Rect, type TextMeasurer } from './types';

export interface Vec { x: number; y: number }

/** What the designer knows about an element: which node it shows and how to draw it. */
export interface ElementInfo {
  nodeId: NodeId;
  kind: string;
  label?: string;
  instance: number;
  hidden?: boolean;
  ownerId?: NodeId;
  synthetic?: boolean;
  /** A layout member (Row, Width, Margin …) is an expression the preview could not evaluate. */
  unresolved?: boolean;
  detail?: BoxDetail;
}

/** Shared by every element of one layout: the text measurer. */
export interface LayoutContext {
  measure: TextMeasurer;
}

const zeroRect = (x: number, y: number): Rect => ({ x, y, width: 0, height: 0 });

// ---------------------------------------------------------------------------------------------------------------------
//  UIElement
// ---------------------------------------------------------------------------------------------------------------------

/** Core/UIElement.cs: margins, Width / Height / MinWidth / MaxWidth, alignment, Measure / MeasureMinWidth / Arrange. */
export abstract class LElement {
  visible = true;
  marginLeft = 0;
  marginTop = 0;
  marginRight = 0;
  marginBottom = 0;
  width: number | null = null;
  height: number | null = null;
  private minW: number | null = null;
  private maxW: number | null = null;
  private hAlign: Align = 'start';
  private vAlign: Align = 'start';
  private hAlignSet = false;
  private vAlignSet = false;
  x = 0;
  y = 0;
  private rowValue = 0;
  private columnValue = 0;
  private rowSpanValue = 1;
  private columnSpanValue = 1;
  parent: LContainer | null = null;
  desired: Vec = { x: 0, y: 0 };
  bounds: Rect = zeroRect(0, 0);
  info: ElementInfo | null;
  protected readonly ctx: LayoutContext;

  constructor(ctx: LayoutContext, info: ElementInfo | null) {
    this.ctx = ctx;
    this.info = info;
  }

  get minWidth(): number | null { return this.minW; }
  set minWidth(v: number | null) { this.minW = v === null ? null : Math.max(0, v); }
  get maxWidth(): number | null { return this.maxW; }
  set maxWidth(v: number | null) { this.maxW = v === null ? null : Math.max(0, v); }
  get row(): number { return this.rowValue; }
  set row(v: number) { this.rowValue = Math.max(0, v); }
  get column(): number { return this.columnValue; }
  set column(v: number) { this.columnValue = Math.max(0, v); }
  get rowSpan(): number { return this.rowSpanValue; }
  set rowSpan(v: number) { this.rowSpanValue = Math.max(1, v); }
  get columnSpan(): number { return this.columnSpanValue; }
  set columnSpan(v: number) { this.columnSpanValue = Math.max(1, v); }

  setMargin(left: number, top: number, right: number, bottom: number): void {
    this.marginLeft = left;
    this.marginTop = top;
    this.marginRight = right;
    this.marginBottom = bottom;
  }

  get horizontalAlign(): Align { return this.hAlign; }
  set horizontalAlign(v: Align) { this.hAlign = v; this.hAlignSet = true; }
  get verticalAlign(): Align { return this.vAlign; }
  set verticalAlign(v: Align) { this.vAlign = v; this.vAlignSet = true; }

  get resolvedHorizontalAlign(): Align {
    return this.hAlignSet ? this.hAlign : this.parent?.defaultChildHorizontalAlign(this) ?? this.hAlign;
  }

  get resolvedVerticalAlign(): Align {
    return this.vAlignSet ? this.vAlign : this.parent?.defaultChildVerticalAlign(this) ?? this.vAlign;
  }

  private clampWidth(value: number): number {
    if (this.maxW !== null) {
      value = Math.min(value, this.maxW);
    }
    if (this.minW !== null) {
      value = Math.max(value, this.minW);
    }
    return value;
  }

  measure(available: Vec): Vec {
    if (!this.visible) {
      this.desired = { x: 0, y: 0 };
      return this.desired;
    }
    const inner: Vec = {
      x: Math.max(0, available.x - this.marginLeft - this.marginRight),
      y: Math.max(0, available.y - this.marginTop - this.marginBottom)
    };
    inner.x = this.width ?? this.clampWidth(inner.x);
    if (this.height !== null) {
      inner.y = this.height;
    }
    const core = this.measureCore(inner);
    core.x = this.width ?? this.clampWidth(core.x);
    if (this.height !== null) {
      core.y = this.height;
    }
    this.desired = {
      x: Math.max(0, core.x) + this.marginLeft + this.marginRight,
      y: Math.max(0, core.y) + this.marginTop + this.marginBottom
    };
    return this.desired;
  }

  protected abstract measureCore(available: Vec): Vec;

  measureMinWidth(): number {
    if (!this.visible) {
      return 0;
    }
    const core = this.width ?? this.clampWidth(this.minWidthCore());
    return Math.max(0, core) + this.marginLeft + this.marginRight;
  }

  protected abstract minWidthCore(): number;

  arrange(slot: Rect): void {
    if (!this.visible) {
      this.bounds = zeroRect(slot.x, slot.y);
      return;
    }
    const availW = Math.max(0, slot.width - this.marginLeft - this.marginRight);
    const availH = Math.max(0, slot.height - this.marginTop - this.marginBottom);
    const desiredW = Math.max(0, Math.ceil(this.desired.x) - this.marginLeft - this.marginRight);
    const desiredH = Math.max(0, Math.ceil(this.desired.y) - this.marginTop - this.marginBottom);
    const ha = this.resolvedHorizontalAlign;
    const va = this.resolvedVerticalAlign;
    const w = this.width ?? toInt(this.clampWidth(ha === 'stretch' ? availW : Math.min(desiredW, availW)));
    const h = this.height ?? (va === 'stretch' ? availH : Math.min(desiredH, availH));
    const px = slot.x + this.marginLeft + alignOffset(ha, availW, w);
    const py = slot.y + this.marginTop + alignOffset(va, availH, h);
    this.bounds = { x: px, y: py, width: w, height: h };
    this.arrangeCore();
  }

  protected arrangeCore(): void {
    // leaves have nothing to place
  }

  /** Children in draw order (containers). */
  get childElements(): readonly LElement[] {
    return [];
  }

  /** The rectangle this element clips its children to (ScrollView viewport, list rows), or null. */
  childClip(): Rect | null {
    return null;
  }

  /** Called after arrange so the element can publish what it draws. */
  finishDetail(): void {
    // leaves with live geometry override this
  }
}

// ---------------------------------------------------------------------------------------------------------------------
//  UIContainer
// ---------------------------------------------------------------------------------------------------------------------

/** Core/UIContainer.cs. */
export abstract class LContainer extends LElement {
  readonly children: LElement[] = [];

  add(child: LElement): void {
    this.children.push(child);
    child.parent = this;
  }

  override get childElements(): readonly LElement[] {
    return this.children;
  }

  protected maxChildMinWidth(): number {
    let min = 0;
    for (const child of this.children) {
      min = Math.max(min, child.measureMinWidth());
    }
    return min;
  }

  defaultChildHorizontalAlign(_child: LElement): Align {
    return 'start';
  }

  defaultChildVerticalAlign(_child: LElement): Align {
    return 'start';
  }
}

// ---------------------------------------------------------------------------------------------------------------------
//  Stack (Components/Stack.cs)
// ---------------------------------------------------------------------------------------------------------------------

export class LStack extends LContainer {
  horizontal: boolean;
  private spacingValue: number;
  alignment: Align = 'start';
  wrap = false;
  /** Slot.MaxHeight (Components/Slot.cs: a Stack whose measure is capped). */
  maxHeight: number | null = null;
  private rowNatural: number[] = [];
  private rowMin: number[] = [];
  private rowWidth: number[] = [];
  private rowMeasuredAt: number[] = [];
  private rowCount = 0;
  private rowMinValid = false;
  private rowShared = false;
  private rowRoom = 0;
  private rowTotal = 0;

  constructor(ctx: LayoutContext, info: ElementInfo | null, horizontal: boolean, spacing: number) {
    super(ctx, info);
    this.horizontal = horizontal;
    this.spacingValue = Math.max(0, spacing);
  }

  get spacing(): number { return this.spacingValue; }
  set spacing(v: number) { this.spacingValue = Math.max(0, v); }

  private get wrapping(): boolean { return this.horizontal && this.wrap; }

  override defaultChildHorizontalAlign(): Align { return this.horizontal ? 'start' : this.alignment; }
  override defaultChildVerticalAlign(): Align { return this.horizontal ? this.alignment : 'start'; }

  protected measureCore(available: Vec): Vec {
    if (this.maxHeight !== null) {
      // Slot.MeasureCore
      const size = this.measureStack({ x: available.x, y: Math.min(available.y, this.maxHeight) });
      return { x: size.x, y: Math.min(size.y, this.maxHeight) };
    }
    return this.measureStack(available);
  }

  private measureStack(available: Vec): Vec {
    if (this.wrapping) {
      return this.measureWrapped(available);
    }
    let main = 0;
    let cross = 0;
    let visible = 0;
    const gap = this.spacing;
    const count = this.children.length;
    this.rowCount = 0;
    if (this.horizontal) {
      this.rowNatural = new Array<number>(count).fill(0);
      this.rowMin = new Array<number>(count).fill(0);
      this.rowWidth = new Array<number>(count).fill(0);
      this.rowMeasuredAt = new Array<number>(count).fill(0);
      this.rowCount = count;
      this.rowMinValid = false;
      this.rowShared = false;
    }
    for (let k = 0; k < count; k++) {
      const child = this.children[k]!;
      if (!child.visible) {
        if (this.horizontal) {
          this.rowNatural[k] = 0;
          this.rowMeasuredAt[k] = 0;
        }
        continue;
      }
      const used = main + (visible > 0 ? gap : 0);
      const remaining: Vec = this.horizontal
        ? { x: Math.max(0, available.x - used), y: available.y }
        : { x: available.x, y: Math.max(0, available.y - used) };
      const size = child.measure(remaining);
      if (this.horizontal) {
        this.rowNatural[k] = size.x;
        this.rowMeasuredAt[k] = remaining.x;
        main += size.x;
        cross = Math.max(cross, size.y);
      } else {
        main += size.y;
        cross = Math.max(cross, size.x);
      }
      visible++;
    }
    const gaps = gap * Math.max(0, visible - 1);
    main += gaps;
    if (this.horizontal && !unbounded(available.x) && main > available.x) {
      this.shareRow(available.x - gaps, available.y);
      main = this.rowTotal + gaps;
      cross = 0;
      for (const child of this.children) {
        if (child.visible) {
          cross = Math.max(cross, child.desired.y);
        }
      }
    }
    return this.horizontal ? { x: main, y: cross } : { x: cross, y: main };
  }

  private shareRow(room: number, height: number): void {
    const count = this.rowCount;
    if (!this.rowMinValid) {
      for (let k = 0; k < count; k++) {
        const c = this.children[k]!;
        this.rowMin[k] = c.visible ? c.measureMinWidth() : 0;
      }
      this.rowMinValid = true;
    }
    this.rowWidth = distributeWidth(this.rowMin.slice(0, count), this.rowNatural.slice(0, count), room);
    let sum = 0;
    let edge = 0;
    for (let k = 0; k < count; k++) {
      sum += this.rowWidth[k]!;
      const next = roundEven(sum);
      this.rowWidth[k] = next - edge;
      edge = next;
    }
    this.rowShared = true;
    this.rowRoom = room;
    this.rowTotal = edge;
    for (let k = 0; k < count; k++) {
      const child = this.children[k]!;
      const width = this.rowWidth[k]!;
      if (!child.visible || width === this.rowMeasuredAt[k] || (width >= this.rowNatural[k]! && this.rowMeasuredAt[k]! >= this.rowNatural[k]!)) {
        continue;
      }
      child.measure({ x: width, y: height });
      this.rowMeasuredAt[k] = width;
    }
  }

  protected minWidthCore(): number {
    if (!this.horizontal || this.wrap) {
      return this.maxChildMinWidth();
    }
    let total = 0;
    let visible = 0;
    for (const child of this.children) {
      if (!child.visible) {
        continue;
      }
      total += child.measureMinWidth();
      visible++;
    }
    return total + (this.spacing * Math.max(0, visible - 1));
  }

  protected override arrangeCore(): void {
    if (this.wrapping) {
      this.arrangeWrapped();
      return;
    }
    const b = this.bounds;
    let cursor = this.horizontal ? b.x : b.y;
    const end = this.horizontal ? b.x + b.width : b.y + b.height;
    const gap = this.spacing;
    const shared = this.horizontal && this.shareRowForArrange(gap);
    for (let k = 0; k < this.children.length; k++) {
      const child = this.children[k]!;
      if (!child.visible) {
        child.arrange(zeroRect(b.x, b.y));
        continue;
      }
      const start = Math.min(cursor, end);
      const wanted = shared ? toInt(this.rowWidth[k]!) : Math.ceil(this.horizontal ? child.desired.x : child.desired.y);
      const extent = Math.min(wanted, end - start);
      child.arrange(this.horizontal
        ? { x: start, y: b.y, width: extent, height: b.height }
        : { x: b.x, y: start, width: b.width, height: extent });
      cursor += extent + gap;
    }
  }

  private shareRowForArrange(gap: number): boolean {
    if (this.rowCount !== this.children.length || this.rowCount === 0) {
      return false;
    }
    let visible = 0;
    let naturalTotal = 0;
    for (let k = 0; k < this.rowCount; k++) {
      if (this.children[k]!.visible) {
        naturalTotal += this.rowNatural[k]!;
        visible++;
      }
    }
    const room = this.bounds.width - (gap * Math.max(0, visible - 1));
    if (this.rowShared && (room === this.rowTotal || room === this.rowRoom)) {
      return true;
    }
    if (!this.rowShared && naturalTotal <= room) {
      return false;
    }
    this.shareRow(room, this.bounds.height);
    return true;
  }

  private measureWrapped(available: Vec): Vec {
    for (const child of this.children) {
      if (child.visible) {
        child.measure(available);
      }
    }
    let width = 0;
    let height = 0;
    let lines = 0;
    for (let first = 0; first < this.children.length;) {
      const line = this.nextLine(first, available.x);
      first = line.end;
      if (!line.any) {
        continue;
      }
      width = Math.max(width, line.width);
      height += line.height;
      lines++;
    }
    if (lines > 1) {
      height += this.spacing * (lines - 1);
    }
    return { x: width, y: height };
  }

  private arrangeWrapped(): void {
    const gap = this.spacing;
    const b = this.bounds;
    const right = b.x + b.width;
    const bottom = b.y + b.height;
    let top = b.y;
    for (let first = 0; first < this.children.length;) {
      const line = this.nextLine(first, b.width);
      const y = Math.min(top, bottom);
      const height = Math.min(Math.ceil(line.height), bottom - y);
      let cursor = b.x;
      for (let i = first; i < line.end; i++) {
        const child = this.children[i]!;
        if (!child.visible) {
          child.arrange(zeroRect(b.x, b.y));
          continue;
        }
        const start = Math.min(cursor, right);
        const extent = Math.min(Math.ceil(child.desired.x), right - start);
        child.arrange({ x: start, y, width: extent, height });
        cursor += extent + gap;
      }
      if (line.any) {
        top += Math.ceil(line.height) + gap;
      }
      first = line.end;
    }
  }

  private nextLine(first: number, width: number): { end: number; width: number; height: number; any: boolean } {
    const gap = this.spacing;
    let lineWidth = 0;
    let lineHeight = 0;
    let any = false;
    let i = first;
    for (; i < this.children.length; i++) {
      const child = this.children[i]!;
      if (!child.visible) {
        continue;
      }
      const childWidth = Math.ceil(child.desired.x);
      if (any && lineWidth + gap + childWidth > width) {
        break;
      }
      lineWidth += (any ? gap : 0) + childWidth;
      lineHeight = Math.max(lineHeight, child.desired.y);
      any = true;
    }
    return { end: i, width: lineWidth, height: lineHeight, any };
  }
}

// ---------------------------------------------------------------------------------------------------------------------
//  Grid (Components/Grid.cs)
// ---------------------------------------------------------------------------------------------------------------------

function extend(defined: GridTrack[], needed: number): GridTrack[] {
  if (needed <= defined.length) {
    return defined;
  }
  const result = defined.slice();
  while (result.length < needed) {
    result.push({ type: 'auto', value: 0 });
  }
  return result;
}

function cell(start: number, span: number, count: number): [number, number] {
  const s = Math.min(Math.max(start, 0), Math.max(0, count - 1));
  const n = Math.min(Math.max(span, 1), Math.max(1, count - s));
  return [s, n];
}

function allPixels(tracks: GridTrack[], start: number, span: number): boolean {
  for (let i = start; i < start + span && i < tracks.length; i++) {
    if (tracks[i]!.type !== 'px') {
      return false;
    }
  }
  return true;
}

function pixelSpan(tracks: GridTrack[], start: number, span: number, spacing: number): number {
  let total = 0;
  const end = Math.min(tracks.length, start + span);
  for (let i = start; i < end; i++) {
    total += tracks[i]!.value;
  }
  return total + (Math.max(0, end - start - 1) * spacing);
}

function spansWeightedStar(tracks: GridTrack[], start: number, span: number): boolean {
  for (let i = start; i < start + span && i < tracks.length; i++) {
    if (tracks[i]!.type === 'star' && tracks[i]!.value > 0) {
      return true;
    }
  }
  return false;
}

function distributeSpan(tracks: GridTrack[], auto: number[], start: number, span: number, spacing: number, desired: number): void {
  const excess = desired - spanSize(auto, start, span, spacing);
  if (excess <= 0) {
    return;
  }
  const end = Math.min(tracks.length, start + span);
  let flexible = 0;
  let starWeight = 0;
  for (let i = start; i < end; i++) {
    if (tracks[i]!.type !== 'px') {
      flexible++;
    }
    if (tracks[i]!.type === 'star' && tracks[i]!.value > 0) {
      starWeight += tracks[i]!.value;
    }
  }
  if (starWeight > 0) {
    for (let i = start; i < end; i++) {
      if (tracks[i]!.type === 'star' && tracks[i]!.value > 0) {
        auto[i] = auto[i]! + (excess * tracks[i]!.value / starWeight);
      }
    }
    return;
  }
  if (flexible === 0) {
    return;
  }
  const share = excess / flexible;
  for (let i = start; i < end; i++) {
    if (tracks[i]!.type !== 'px') {
      auto[i] = auto[i]! + share;
    }
  }
}

function pixelSizes(tracks: GridTrack[]): number[] {
  return tracks.map(t => (t.type === 'px' ? t.value : 0));
}

function minimumTotal(tracks: GridTrack[], min: number[]): number {
  let fixedTotal = 0;
  let starTotal = 0;
  let starMinTotal = 0;
  tracks.forEach((t, i) => {
    if (t.type === 'star') {
      starTotal += t.value;
      starMinTotal += min[i]!;
    } else {
      fixedTotal += min[i]!;
    }
  });
  let starSpace = starTotal > 0 ? 0 : starMinTotal;
  if (starTotal > 0) {
    tracks.forEach((t, i) => {
      if (t.type === 'star' && t.value > 0) {
        starSpace = Math.max(starSpace, min[i]! * starTotal / t.value);
      }
    });
  }
  return fixedTotal + starSpace;
}

const sum = (values: readonly number[]): number => values.reduce((a, b) => a + b, 0);

export class LGrid extends LContainer {
  private columnTracks: GridTrack[];
  private rowTracks: GridTrack[];
  private columnSpacingValue = 0;
  private rowSpacingValue = 0;
  private effectiveColumns: GridTrack[];
  private effectiveRows: GridTrack[];
  private columnAuto: number[] = [];
  private rowAuto: number[] = [];
  private columnMin: number[] = [];
  private measuredWidth: number[] = [];

  constructor(ctx: LayoutContext, info: ElementInfo | null, columns: string, rows: string) {
    super(ctx, info);
    this.columnTracks = parseTracks(columns);
    this.rowTracks = parseTracks(rows);
    this.effectiveColumns = this.columnTracks;
    this.effectiveRows = this.rowTracks;
  }

  set columns(v: string) { this.columnTracks = parseTracks(v); }
  set rows(v: string) { this.rowTracks = parseTracks(v); }
  get columnSpacing(): number { return this.columnSpacingValue; }
  set columnSpacing(v: number) { this.columnSpacingValue = Math.max(0, v); }
  get rowSpacing(): number { return this.rowSpacingValue; }
  set rowSpacing(v: number) { this.rowSpacingValue = Math.max(0, v); }

  protected measureCore(available: Vec): Vec {
    this.resolveEffectiveTracks();
    const colSpace = this.columnSpacing;
    const colSpacingTotal = colSpace * Math.max(0, this.effectiveColumns.length - 1);
    const rowSpacingTotal = this.rowSpacing * Math.max(0, this.effectiveRows.length - 1);
    const bounded = !unbounded(available.x);
    let slack = 0;
    if (bounded) {
      this.columnMin = pixelSizes(this.effectiveColumns);
      this.fillColumnMinimums(this.effectiveColumns, this.columnMin);
      slack = available.x - colSpacingTotal - minimumTotal(this.effectiveColumns, this.columnMin);
    }
    this.measureColumnContent(available, bounded, Math.max(0, slack));
    this.computeColumnSizes();
    if (bounded) {
      this.fitContentColumns(slack, available.y);
    }
    const colSizes = resolveTracks(this.effectiveColumns, this.columnAuto, available.x - colSpacingTotal);
    this.remeasureAtCellWidths(colSizes, available.y);
    this.computeRowSizes();
    const rowSizes = resolveTracks(this.effectiveRows, this.rowAuto, available.y - rowSpacingTotal);
    return { x: sum(colSizes) + colSpacingTotal, y: sum(rowSizes) + rowSpacingTotal };
  }

  private resolveEffectiveTracks(): void {
    let neededColumns = this.columnTracks.length;
    let neededRows = this.rowTracks.length;
    for (const child of this.children) {
      if (child.visible) {
        neededColumns = Math.max(neededColumns, child.column + child.columnSpan);
        neededRows = Math.max(neededRows, child.row + child.rowSpan);
      }
    }
    this.effectiveColumns = extend(this.columnTracks, neededColumns);
    this.effectiveRows = extend(this.rowTracks, neededRows);
    this.columnAuto = pixelSizes(this.effectiveColumns);
    this.rowAuto = pixelSizes(this.effectiveRows);
  }

  private fillColumnMinimums(tracks: GridTrack[], min: number[]): void {
    const count = tracks.length;
    for (const child of this.children) {
      if (!child.visible) {
        continue;
      }
      const [col, span] = cell(child.column, child.columnSpan, count);
      if (span === 1 && tracks[col]!.type !== 'px') {
        min[col] = Math.max(min[col]!, child.measureMinWidth());
      }
    }
    for (const child of this.children) {
      if (!child.visible) {
        continue;
      }
      const [col, span] = cell(child.column, child.columnSpan, count);
      if (span > 1) {
        distributeSpan(tracks, min, col, span, this.columnSpacing, child.measureMinWidth());
      }
    }
  }

  private measureColumnContent(available: Vec, bounded: boolean, slack: number): void {
    const colCount = this.effectiveColumns.length;
    const rowCount = this.effectiveRows.length;
    this.measuredWidth = new Array<number>(this.children.length).fill(Number.NaN);
    this.children.forEach((child, k) => {
      if (!child.visible) {
        return;
      }
      const [col, colSpan] = cell(child.column, child.columnSpan, colCount);
      const [row, rowSpan] = cell(child.row, child.rowSpan, rowCount);
      let cellW: number;
      if (allPixels(this.effectiveColumns, col, colSpan)) {
        cellW = pixelSpan(this.effectiveColumns, col, colSpan, this.columnSpacing);
      } else if (!bounded) {
        cellW = available.x;
      } else if (spansWeightedStar(this.effectiveColumns, col, colSpan)) {
        return;
      } else {
        cellW = spanSize(this.columnMin, col, colSpan, this.columnSpacing) + slack;
      }
      this.measuredWidth[k] = cellW;
      child.measure({ x: cellW, y: this.cellHeight(row, rowSpan, available.y) });
    });
  }

  private static sizesToContent(tracks: GridTrack[], index: number, weightedStars: boolean): boolean {
    const t = tracks[index]!;
    return t.type === 'auto' || (t.type === 'star' && !weightedStars);
  }

  private fitContentColumns(slack: number, availableHeight: number): void {
    const cols = this.effectiveColumns;
    const colCount = cols.length;
    const weightedStars = cols.some(t => t.type === 'star' && t.value > 0);
    const fitMin: number[] = [];
    const fitNatural: number[] = [];
    let minTotal = 0;
    let naturalTotal = 0;
    for (let c = 0; c < colCount; c++) {
      const fits = LGrid.sizesToContent(cols, c, weightedStars);
      fitMin.push(fits ? this.columnMin[c]! : 0);
      fitNatural.push(fits ? this.columnAuto[c]! : 0);
      minTotal += fitMin[c]!;
      naturalTotal += fitNatural[c]!;
    }
    const room = minTotal + slack;
    if (naturalTotal <= room) {
      return;
    }
    const fitWidth = distributeWidth(fitMin, fitNatural, room);
    for (let c = 0; c < colCount; c++) {
      if (LGrid.sizesToContent(cols, c, weightedStars)) {
        this.columnAuto[c] = fitWidth[c]!;
      }
    }
    const rowCount = this.effectiveRows.length;
    this.children.forEach((child, k) => {
      if (!child.visible || Number.isNaN(this.measuredWidth[k]!)) {
        return;
      }
      const [col, colSpan] = cell(child.column, child.columnSpan, colCount);
      if (colSpan !== 1 || !LGrid.sizesToContent(cols, col, weightedStars) || child.desired.x <= fitWidth[col]!) {
        return;
      }
      const [row, rowSpan] = cell(child.row, child.rowSpan, rowCount);
      child.measure({ x: fitWidth[col]!, y: this.cellHeight(row, rowSpan, availableHeight) });
      this.measuredWidth[k] = fitWidth[col]!;
    });
  }

  private cellHeight(row: number, rowSpan: number, availableHeight: number): number {
    return allPixels(this.effectiveRows, row, rowSpan) ? pixelSpan(this.effectiveRows, row, rowSpan, this.rowSpacing) : availableHeight;
  }

  private computeColumnSizes(): void {
    const colCount = this.effectiveColumns.length;
    this.children.forEach((child, k) => {
      if (!child.visible || Number.isNaN(this.measuredWidth[k]!)) {
        return;
      }
      const [col, colSpan] = cell(child.column, child.columnSpan, colCount);
      if (colSpan === 1 && this.effectiveColumns[col]!.type !== 'px') {
        this.columnAuto[col] = Math.max(this.columnAuto[col]!, child.desired.x);
      }
    });
    this.children.forEach((child, k) => {
      if (!child.visible || Number.isNaN(this.measuredWidth[k]!)) {
        return;
      }
      const [col, colSpan] = cell(child.column, child.columnSpan, colCount);
      if (colSpan > 1) {
        distributeSpan(this.effectiveColumns, this.columnAuto, col, colSpan, this.columnSpacing, child.desired.x);
      }
    });
  }

  private remeasureAtCellWidths(colSizes: number[], availableHeight: number): void {
    const colCount = this.effectiveColumns.length;
    const rowCount = this.effectiveRows.length;
    this.children.forEach((child, k) => {
      if (!child.visible) {
        return;
      }
      const [col, colSpan] = cell(child.column, child.columnSpan, colCount);
      if (allPixels(this.effectiveColumns, col, colSpan) || (colSpan === 1 && this.effectiveColumns[col]!.type !== 'star')) {
        return;
      }
      const cellW = spanSize(colSizes, col, colSpan, this.columnSpacing);
      const measured = this.measuredWidth[k]!;
      if (cellW === measured || (measured === Number.POSITIVE_INFINITY && cellW >= child.desired.x)) {
        return;
      }
      const [row, rowSpan] = cell(child.row, child.rowSpan, rowCount);
      child.measure({ x: cellW, y: this.cellHeight(row, rowSpan, availableHeight) });
    });
  }

  private computeRowSizes(): void {
    const rowCount = this.effectiveRows.length;
    for (const child of this.children) {
      if (!child.visible) {
        continue;
      }
      const [row, rowSpan] = cell(child.row, child.rowSpan, rowCount);
      if (rowSpan === 1 && this.effectiveRows[row]!.type !== 'px') {
        this.rowAuto[row] = Math.max(this.rowAuto[row]!, child.desired.y);
      }
    }
    for (const child of this.children) {
      if (!child.visible) {
        continue;
      }
      const [row, rowSpan] = cell(child.row, child.rowSpan, rowCount);
      if (rowSpan > 1) {
        distributeSpan(this.effectiveRows, this.rowAuto, row, rowSpan, this.rowSpacing, child.desired.y);
      }
    }
  }

  protected minWidthCore(): number {
    let needed = this.columnTracks.length;
    for (const child of this.children) {
      if (child.visible) {
        needed = Math.max(needed, child.column + child.columnSpan);
      }
    }
    const tracks = extend(this.columnTracks, needed);
    const min = pixelSizes(tracks);
    this.fillColumnMinimums(tracks, min);
    return minimumTotal(tracks, min) + (this.columnSpacing * Math.max(0, tracks.length - 1));
  }

  protected override arrangeCore(): void {
    const colCount = this.effectiveColumns.length;
    const rowCount = this.effectiveRows.length;
    if (this.columnAuto.length !== colCount || this.rowAuto.length !== rowCount) {
      this.columnAuto = new Array<number>(colCount).fill(0);
      this.rowAuto = new Array<number>(rowCount).fill(0);
    }
    const b = this.bounds;
    const colSizes = resolveTracks(this.effectiveColumns, this.columnAuto, b.width - (this.columnSpacing * Math.max(0, colCount - 1)));
    const rowSizes = resolveTracks(this.effectiveRows, this.rowAuto, b.height - (this.rowSpacing * Math.max(0, rowCount - 1)));
    for (const child of this.children) {
      if (!child.visible) {
        child.arrange(zeroRect(b.x, b.y));
        continue;
      }
      const [col, colSpan] = cell(child.column, child.columnSpan, colCount);
      const [row, rowSpan] = cell(child.row, child.rowSpan, rowCount);
      const x0 = trackOffset(colSizes, col, this.columnSpacing);
      const y0 = trackOffset(rowSizes, row, this.rowSpacing);
      const x1 = x0 + spanSize(colSizes, col, colSpan, this.columnSpacing);
      const y1 = y0 + spanSize(rowSizes, row, rowSpan, this.rowSpacing);
      const left = b.x + roundEven(x0);
      const top = b.y + roundEven(y0);
      const right = b.x + roundEven(x1);
      const bottom = b.y + roundEven(y1);
      child.arrange({ x: left, y: top, width: Math.max(0, right - left), height: Math.max(0, bottom - top) });
    }
  }
}

// ---------------------------------------------------------------------------------------------------------------------
//  Panel, Canvas, ScrollView, Spacer
// ---------------------------------------------------------------------------------------------------------------------

/** Components/Panel.cs. */
export class LPanel extends LContainer {
  drawBox: boolean;
  private paddingValue: number;
  /** Style.Padding, used when the panel's own padding is 0 (EffectivePadding). */
  stylePadding: number | null = null;

  constructor(ctx: LayoutContext, info: ElementInfo | null, drawBox: boolean, padding: number) {
    super(ctx, info);
    this.drawBox = drawBox;
    this.paddingValue = Math.max(0, padding);
  }

  get padding(): number { return this.paddingValue; }
  set padding(v: number) { this.paddingValue = Math.max(0, v); }

  private get effectivePadding(): number {
    return this.paddingValue > 0 ? this.paddingValue : this.stylePadding ?? 0;
  }

  protected measureCore(available: Vec): Vec {
    const p = this.effectivePadding;
    const inner = { x: Math.max(0, available.x - (2 * p)), y: Math.max(0, available.y - (2 * p)) };
    let w = 0;
    let h = 0;
    for (const child of this.children) {
      const size = child.measure(inner);
      w = Math.max(w, size.x);
      h = Math.max(h, size.y);
    }
    return { x: w + (2 * p), y: h + (2 * p) };
  }

  protected minWidthCore(): number {
    return this.maxChildMinWidth() + (2 * this.effectivePadding);
  }

  protected override arrangeCore(): void {
    const p = this.effectivePadding;
    const b = this.bounds;
    const content = { x: b.x + p, y: b.y + p, width: Math.max(0, b.width - (2 * p)), height: Math.max(0, b.height - (2 * p)) };
    for (const child of this.children) {
      child.arrange(content);
    }
  }
}

/** Components/Canvas.cs. */
export class LCanvas extends LContainer {
  protected measureCore(available: Vec): Vec {
    let w = 0;
    let h = 0;
    for (const child of this.children) {
      if (!child.visible) {
        continue;
      }
      const size = child.measure({ x: Math.max(0, available.x - child.x), y: Math.max(0, available.y - child.y) });
      w = Math.max(w, child.x + size.x);
      h = Math.max(h, child.y + size.y);
    }
    return { x: w, y: h };
  }

  protected minWidthCore(): number {
    let w = 0;
    for (const child of this.children) {
      if (child.visible) {
        w = Math.max(w, child.x + child.measureMinWidth());
      }
    }
    return w;
  }

  protected override arrangeCore(): void {
    const b = this.bounds;
    for (const child of this.children) {
      let w = Math.ceil(child.desired.x);
      let h = Math.ceil(child.desired.y);
      if (child.resolvedHorizontalAlign === 'stretch') {
        w = Math.max(w, b.width - child.x);
      }
      if (child.resolvedVerticalAlign === 'stretch') {
        h = Math.max(h, b.height - child.y);
      }
      child.arrange({ x: b.x + child.x, y: b.y + child.y, width: w, height: h });
    }
  }
}

/** ScrollbarGadget.ReservedWidth: Width (44, the arrow sprite at 4x) + Gap (4). Components/ScrollbarGadget.cs. */
export const SCROLLBAR_RESERVED = 48;
/** ScrollbarGadget.Width. */
export const SCROLLBAR_WIDTH = 44;

/** Components/ScrollView.cs (scroll offset 0: the preview shows the top of the content). */
export class LScrollView extends LContainer {
  private viewportHeightValue: number;
  showScrollbar = true;
  fitContent = false;
  private overflowing = false;
  private contentHeight = 0;

  constructor(ctx: LayoutContext, info: ElementInfo | null, viewportHeight: number) {
    super(ctx, info);
    this.viewportHeightValue = Math.max(0, viewportHeight);
  }

  get viewportHeight(): number { return this.viewportHeightValue; }
  set viewportHeight(v: number) { this.viewportHeightValue = Math.max(0, v); }

  private get reservedWidth(): number {
    return this.showScrollbar && (!this.fitContent || this.overflowing) ? SCROLLBAR_RESERVED : 0;
  }

  /** MaxScroll > 0: the content is taller than the viewport, so the scrollbar draws. */
  get scrollbarVisible(): boolean {
    const viewport = this.bounds.height > 0 ? this.bounds.height : this.height ?? this.viewportHeightValue;
    return this.showScrollbar && this.contentHeight - viewport > 0;
  }

  private viewportRect(): Rect {
    const b = this.bounds;
    return { x: b.x, y: b.y, width: Math.max(0, b.width - this.reservedWidth), height: b.height };
  }

  protected measureCore(available: Vec): Vec {
    if (this.fitContent) {
      this.overflowing = false;
      let content = this.measureContent(available);
      if (content.y <= available.y) {
        return content;
      }
      this.overflowing = true;
      const reserved = this.reservedWidth;
      content = this.measureContent({ x: Math.max(0, available.x - reserved), y: Math.max(available.y, content.y) });
      return { x: content.x + reserved, y: available.y };
    }
    const reserved = this.reservedWidth;
    const content = this.measureContent({ x: Math.max(0, available.x - reserved), y: Number.POSITIVE_INFINITY });
    return { x: content.x + reserved, y: this.viewportHeightValue };
  }

  private measureContent(inner: Vec): Vec {
    let w = 0;
    let h = 0;
    for (const child of this.children) {
      if (!child.visible) {
        continue;
      }
      const size = child.measure(inner);
      w = Math.max(w, size.x);
      h += size.y;
    }
    this.contentHeight = Math.ceil(h);
    return { x: w, y: h };
  }

  protected minWidthCore(): number {
    return this.maxChildMinWidth() + (this.showScrollbar ? SCROLLBAR_RESERVED : 0);
  }

  protected override arrangeCore(): void {
    const viewport = this.viewportRect();
    let y = viewport.y;
    for (const child of this.children) {
      if (!child.visible) {
        child.arrange(zeroRect(viewport.x, viewport.y));
        continue;
      }
      const h = Math.ceil(child.desired.y);
      child.arrange({ x: viewport.x, y, width: viewport.width, height: h });
      y += h;
    }
  }

  override childClip(): Rect {
    return this.viewportRect();
  }

  override finishDetail(): void {
    if (this.info) {
      this.info.detail = { ...this.info.detail, scrollbar: this.scrollbarVisible ? SCROLLBAR_WIDTH : 0 };
    }
  }
}

/** Components/Spacer.cs. */
export class LSpacer extends LElement {
  /** Spacer.LineThickness. */
  static readonly lineThickness = 4;
  line = false;

  protected measureCore(): Vec {
    return { x: 0, y: this.line ? LSpacer.lineThickness : 0 };
  }

  protected minWidthCore(): number {
    return 0;
  }
}

// ---------------------------------------------------------------------------------------------------------------------
//  Text leaves
// ---------------------------------------------------------------------------------------------------------------------

/** DrawHelper.MinFitScale (Rendering/Draw.cs): a squeezed single line shrinks to 70 % before truncating. */
const MIN_FIT_SCALE = 0.7;

function stripMarks(text: string): string {
  return text.replace(CHIP_MARKS, '');
}

/** DrawHelper.FitTextMinWidth: the narrower of the whole text at 70 % and its first character + "..." at 70 %. */
function fitTextMinWidth(measure: TextMeasurer, text: string, font: FontName, scale: number): number {
  const plain = stripMarks(text);
  if (plain.length === 0) {
    return 0;
  }
  const smallest = MIN_FIT_SCALE * scale;
  const whole = measure(text, font, smallest).width;
  const truncated = measure(plain.substring(0, 1) + '...', font, smallest).width;
  return Math.ceil(Math.min(whole, truncated));
}

/** GameTextMeasurer.LongestWord: the widest piece between spaces / line breaks. */
function longestWord(measure: TextMeasurer, text: string, font: FontName, scale: number): number {
  let widest = 0;
  for (const word of text.split(/[ \n\r]/)) {
    if (stripMarks(word).length > 0) {
      widest = Math.max(widest, measure(word, font, scale).width);
    }
  }
  return widest;
}

/** Components/Label.cs (plain text; rich text is measured as its plain text). */
export class LLabel extends LElement {
  text = '';
  font: FontName = 'small';
  wrap = false;
  shrink = false;
  private scaleValue = 1;
  private wrapWidth = -1;
  private lines: string[] = [];
  private laidOut: Vec = { x: 0, y: 0 };

  get scale(): number { return this.scaleValue; }
  set scale(v: number) { this.scaleValue = Math.max(0.05, v); }

  protected measureCore(available: Vec): Vec {
    this.wrapWidth = this.wrap ? toInt(Math.floor(available.x)) : -1;
    return this.reflow();
  }

  private reflow(): Vec {
    const m = this.wrapWidth > 0
      ? this.ctx.measure(this.text, this.font, this.scale, this.wrapWidth)
      : this.ctx.measure(this.text, this.font, this.scale);
    this.lines = m.lines;
    this.laidOut = { x: m.width, y: m.height };
    return { x: m.width, y: m.height };
  }

  protected minWidthCore(): number {
    if (this.wrap) {
      return longestWord(this.ctx.measure, this.text, this.font, this.scale);
    }
    return this.shrink
      ? fitTextMinWidth(this.ctx.measure, this.text, this.font, this.scale)
      : Math.ceil(this.ctx.measure(this.text, this.font, this.scale).width);
  }

  protected override arrangeCore(): void {
    if (!this.wrap || this.bounds.width <= 0 || this.bounds.width >= Math.ceil(this.laidOut.x)) {
      return;
    }
    this.wrapWidth = this.bounds.width;
    this.reflow();
  }

  override finishDetail(): void {
    if (this.info) {
      this.info.detail = { ...this.info.detail, text: this.text, lines: this.lines, font: this.font, scale: this.scale };
    }
  }
}

/** Components/Button.cs (PadX 24, PadY 12, IconGap 8, MinHeight 64). */
export class LButton extends LElement {
  static readonly padX = 24;
  static readonly padY = 12;
  static readonly iconGap = 8;
  static readonly minHeight = 64;
  text = '';
  font: FontName = 'small';
  drawBox = true;
  shrink = false;
  /** Icon source size × IconScale, or zero without an icon. */
  iconSize: Vec = { x: 0, y: 0 };

  private contentWidth(textWidth: number): number {
    const iconWidth = this.iconSize.x;
    return textWidth + iconWidth + (textWidth > 0 && iconWidth > 0 ? LButton.iconGap : 0) + (this.drawBox ? 2 * LButton.padX : 0);
  }

  protected measureCore(): Vec {
    const empty = stripMarks(this.text).length === 0;
    const m = empty ? { width: 0, height: 0 } : this.ctx.measure(this.text, this.font, 1);
    let h = Math.max(m.height, this.iconSize.y) + (this.drawBox ? 2 * LButton.padY : 0);
    if (this.drawBox) {
      h = Math.max(h, LButton.minHeight);
    }
    return { x: this.contentWidth(m.width), y: h };
  }

  protected minWidthCore(): number {
    const textWidth = stripMarks(this.text).length === 0 ? 0
      : this.shrink ? fitTextMinWidth(this.ctx.measure, this.text, this.font, 1)
      : Math.ceil(this.ctx.measure(this.text, this.font, 1).width);
    return this.contentWidth(textWidth);
  }
}

/** Components/Checkbox.cs: a 36 px box (CheckboxChecked 9 px × PixelScale 4) + 8 px gap + the label. */
export class LCheckbox extends LElement {
  static readonly boxSize = 36;
  static readonly labelGap = 8;
  label = '';
  font: FontName = 'small';
  shrink = false;

  protected measureCore(): Vec {
    if (stripMarks(this.label).length === 0) {
      return { x: LCheckbox.boxSize, y: LCheckbox.boxSize };
    }
    const m = this.ctx.measure(this.label, this.font, 1);
    return { x: LCheckbox.boxSize + LCheckbox.labelGap + m.width, y: Math.max(LCheckbox.boxSize, m.height) };
  }

  protected minWidthCore(): number {
    if (stripMarks(this.label).length === 0) {
      return LCheckbox.boxSize;
    }
    const w = this.shrink ? fitTextMinWidth(this.ctx.measure, this.label, this.font, 1) : Math.ceil(this.ctx.measure(this.label, this.font, 1).width);
    return LCheckbox.boxSize + LCheckbox.labelGap + w;
  }
}

/** TextInput / NumberInput: Rendering/TextBoxDrawing.cs (vanilla text box 192×48; min = max(2×16, 26) + "000"). */
export class LTextBox extends LElement {
  font: FontName = 'small';
  /** A custom Texture's size when known (the designer cannot read textures: null = the vanilla box). */
  textureSize: Vec | null = null;

  protected measureCore(): Vec {
    return this.textureSize ? { ...this.textureSize } : { x: 192, y: 48 };
  }

  protected minWidthCore(): number {
    return Math.max(2 * 16, 26) + this.ctx.measure('000', this.font, 1).width;
  }
}

/** Components/Dropdown.cs: DefaultWidth 300, row height 44, ButtonWidth 48, TextPadX 4, MinTextWidth 40. */
export class LDropdown extends LElement {
  labels: string[] = [];
  font: FontName = 'small';
  shrink = false;

  protected measureCore(available: Vec): Vec {
    return { x: Math.max(0, Math.min(available.x, 300)), y: 44 };
  }

  protected minWidthCore(): number {
    let text = 40;
    for (const label of this.labels) {
      text = Math.max(text, this.shrink ? fitTextMinWidth(this.ctx.measure, label, this.font, 1) : Math.ceil(this.ctx.measure(label, this.font, 1).width));
    }
    const width = 48 + (2 * 4) + text;
    return this.shrink ? width : Math.min(width, 300);
  }
}

/** Components/Slider.cs: DefaultWidth 192, DefaultHeight 24, minimum three knob widths (SliderKnob 10 px × 4). */
export class LSlider extends LElement {
  protected measureCore(available: Vec): Vec {
    return { x: Math.max(0, Math.min(available.x, 192)), y: 24 };
  }

  protected minWidthCore(): number {
    return 3 * 10 * 4;
  }
}

/** Components/Image.cs: source size × scale. */
export class LImage extends LElement {
  sourceSize: Vec = { x: 16, y: 16 };
  scale = 1;

  protected measureCore(): Vec {
    return { x: this.sourceSize.x * this.scale, y: this.sourceSize.y * this.scale };
  }

  protected minWidthCore(): number {
    return this.sourceSize.x * this.scale;
  }
}

/** Components/ItemImage.cs: a 16 px item sprite × scale. */
export class LItemImage extends LElement {
  scale = 1;

  protected measureCore(): Vec {
    return { x: 16 * this.scale, y: 16 * this.scale };
  }

  protected minWidthCore(): number {
    return 16 * this.scale;
  }
}

/** A template / custom tag / composite the designer cannot expand: a fixed box (Width / Height override it). */
export class LPlaceholder extends LElement {
  static readonly size: Vec = { x: 200, y: 48 };

  protected measureCore(): Vec {
    return { ...LPlaceholder.size };
  }

  protected minWidthCore(): number {
    return LPlaceholder.size.x;
  }
}

// ---------------------------------------------------------------------------------------------------------------------
//  Collections
// ---------------------------------------------------------------------------------------------------------------------

/**
 * Components/ListView.cs: VisibleRows row panels (no box, no padding) of RowHeight, a scrollbar column always reserved,
 * and as wide as offered. Rows past the item count are hidden.
 */
export class LListView extends LContainer {
  rowHeight: number;
  visibleRows: number;

  constructor(ctx: LayoutContext, info: ElementInfo | null, rowHeight: number, visibleRows: number) {
    super(ctx, info);
    this.rowHeight = rowHeight;
    this.visibleRows = visibleRows;
  }

  private contentRect(): Rect {
    const b = this.bounds;
    return { x: b.x, y: b.y, width: Math.max(0, b.width - SCROLLBAR_RESERVED), height: b.height };
  }

  protected measureCore(available: Vec): Vec {
    const rowAvailable = { x: Math.max(0, available.x - SCROLLBAR_RESERVED), y: this.rowHeight };
    let maxRowWidth = 0;
    for (const child of this.children) {
      if (child.visible) {
        maxRowWidth = Math.max(maxRowWidth, child.measure(rowAvailable).x);
      }
    }
    const width = unbounded(available.x) ? maxRowWidth + SCROLLBAR_RESERVED : Math.max(available.x, maxRowWidth + SCROLLBAR_RESERVED);
    return { x: width, y: this.visibleRows * this.rowHeight };
  }

  protected minWidthCore(): number {
    return this.maxChildMinWidth() + SCROLLBAR_RESERVED;
  }

  protected override arrangeCore(): void {
    const content = this.contentRect();
    this.children.forEach((row, i) => {
      row.arrange({ x: content.x, y: content.y + (i * this.rowHeight), width: content.width, height: this.rowHeight });
    });
  }

  override childClip(): Rect {
    return this.contentRect();
  }

  override finishDetail(): void {
    if (this.info) {
      this.info.detail = { ...this.info.detail, scrollbar: SCROLLBAR_WIDTH };
    }
  }
}

export interface GridColumn {
  track: GridTrack;
  minWidth: number;
  sortable: boolean;
  header: LLabel;
  /** One cell element per row. */
  cells: LElement[];
}

/**
 * Components/DataGrid.cs, approximated: header (small font line + 2 × HeaderPadY 8), VisibleRows × RowHeight, a
 * reserved scrollbar column, columns resolved like Grid tracks from their content (header / cells + 2 × CellPadX 8).
 * The auto-column fitting (FitAutoColumns) is not ported: auto columns keep their content width.
 */
export class LDataGrid extends LContainer {
  static readonly cellPadX = 8;
  static readonly headerPadY = 8;
  rowHeight: number;
  visibleRows: number;
  columns: GridColumn[] = [];
  /** Row containers (synthetic), one per rendered row. */
  rows: LElement[] = [];
  private headerHeight = 0;
  private widths: number[] = [];

  constructor(ctx: LayoutContext, info: ElementInfo | null, rowHeight: number, visibleRows: number) {
    super(ctx, info);
    this.rowHeight = rowHeight;
    this.visibleRows = visibleRows;
  }

  private headerWidth(column: GridColumn): number {
    // HeaderWidth: the header text, plus ArrowGap 6 + ScrollUpArrow 11 px × ArrowScale 2 when sortable
    return this.ctx.measure(column.header.text, 'small', 1).width + (column.sortable ? 6 + (11 * 2) : 0);
  }

  private autoWidth(column: GridColumn): number {
    let width = this.headerWidth(column);
    for (const c of column.cells) {
      if (c.visible) {
        width = Math.max(width, c.measure({ x: Number.POSITIVE_INFINITY, y: this.rowHeight }).x);
      }
    }
    return width + (2 * LDataGrid.cellPadX);
  }

  private resolveColumns(contentWidth: number): number {
    const tracks = this.columns.map(c => c.track);
    const auto = this.columns.map(c => (c.track.type === 'px' ? 0 : this.autoWidth(c)));
    const sizes = resolveTracks(tracks, auto, contentWidth);
    this.widths = this.columns.map((c, j) => roundEven(Math.max(sizes[j]!, c.minWidth)));
    return sum(this.widths);
  }

  protected measureCore(available: Vec): Vec {
    this.headerHeight = Math.ceil(this.ctx.measure('', 'small', 1).height) + (2 * LDataGrid.headerPadY);
    const free = unbounded(available.x);
    const total = this.resolveColumns(free ? Number.POSITIVE_INFINITY : Math.max(0, available.x - SCROLLBAR_RESERVED));
    this.columns.forEach((c, j) => {
      const inner = Math.max(0, this.widths[j]! - (2 * LDataGrid.cellPadX));
      c.header.measure({ x: inner, y: this.headerHeight });
      for (const cellElement of c.cells) {
        cellElement.measure({ x: inner, y: this.rowHeight });
      }
    });
    for (const row of this.rows) {
      row.measure({ x: total, y: this.rowHeight });
    }
    const width = free ? total + SCROLLBAR_RESERVED : Math.max(available.x, total + SCROLLBAR_RESERVED);
    return { x: width, y: this.headerHeight + (this.visibleRows * this.rowHeight) };
  }

  protected minWidthCore(): number {
    let total = 0;
    for (const c of this.columns) {
      total += c.track.type === 'px' ? Math.max(c.track.value, c.minWidth) : c.track.type === 'auto' ? Math.max(this.autoWidth(c), c.minWidth) : c.minWidth;
    }
    return total + SCROLLBAR_RESERVED;
  }

  protected override arrangeCore(): void {
    const b = this.bounds;
    const contentWidth = Math.max(0, b.width - SCROLLBAR_RESERVED);
    this.resolveColumns(contentWidth);
    let x = b.x;
    const xs: number[] = [];
    for (const w of this.widths) {
      xs.push(x);
      x += w;
    }
    const total = x - b.x;
    this.rows.forEach((row, i) => {
      row.arrange({ x: b.x, y: b.y + this.headerHeight + (i * this.rowHeight), width: total, height: this.rowHeight });
    });
    this.columns.forEach((c, j) => {
      const cx = xs[j]! + LDataGrid.cellPadX;
      const cw = Math.max(0, this.widths[j]! - (2 * LDataGrid.cellPadX));
      c.header.arrange({ x: cx, y: b.y + LDataGrid.headerPadY, width: cw, height: Math.max(0, this.headerHeight - (2 * LDataGrid.headerPadY)) });
      c.cells.forEach((cellElement, i) => {
        cellElement.arrange({ x: cx, y: b.y + this.headerHeight + (i * this.rowHeight), width: cw, height: this.rowHeight });
      });
    });
  }

  override get childElements(): readonly LElement[] {
    const all: LElement[] = [...this.rows];
    for (const c of this.columns) {
      all.push(c.header, ...c.cells);
    }
    return all;
  }

  override childClip(): Rect {
    const b = this.bounds;
    return { x: b.x, y: b.y, width: Math.max(0, b.width - SCROLLBAR_RESERVED), height: b.height };
  }

  override finishDetail(): void {
    if (this.info) {
      this.info.detail = { ...this.info.detail, scrollbar: SCROLLBAR_WIDTH };
    }
  }
}

/** Core/AutoForm.cs: the field grid and the button row stacked with 12 px between them. */
export class LForm extends LContainer {
  static readonly spacing = 12;

  protected measureCore(available: Vec): Vec {
    let height = 0;
    let width = 0;
    let visible = 0;
    for (const child of this.children) {
      if (!child.visible) {
        continue;
      }
      const size = child.measure(available);
      height += size.y;
      width = Math.max(width, size.x);
      visible++;
    }
    return { x: width, y: height + (LForm.spacing * Math.max(0, visible - 1)) };
  }

  protected minWidthCore(): number {
    return this.maxChildMinWidth();
  }

  protected override arrangeCore(): void {
    const b = this.bounds;
    let cursor = b.y;
    for (const child of this.children) {
      if (!child.visible) {
        child.arrange(zeroRect(b.x, b.y));
        continue;
      }
      const extent = Math.ceil(child.desired.y);
      child.arrange({ x: b.x, y: cursor, width: b.width, height: extent });
      cursor += extent + LForm.spacing;
    }
  }
}
