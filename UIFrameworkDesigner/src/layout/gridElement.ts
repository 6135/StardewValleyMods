// Components/Grid.cs's measure / arrange pass (elements.ts).
import { type ElementInfo, type LayoutContext, LContainer, sum, type Vec, zeroRect } from './elementBase';
import { type GridTrack, distributeWidth, parseTracks, resolveTracks, roundEven, spanSize, trackOffset, unbounded } from './engine';

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
