// ListView, DataGrid and AutoForm's measure / arrange pass (elements.ts).
import { type ElementInfo, type LayoutContext, LContainer, LElement, type ScrollState, sum, type Vec, zeroRect } from './elementBase';
import { type GridTrack, resolveTracks, roundEven, unbounded } from './engine';
import { SCROLLBAR_RESERVED, SCROLLBAR_WIDTH, scrollbarParts } from './panelElements';
import { LLabel } from './textElements';
import type { Rect } from './types';

// ---------------------------------------------------------------------------------------------------------------------
//  Collections
// ---------------------------------------------------------------------------------------------------------------------

/** The scroll state of a List / DataGrid: `first` of `count` items shown, VisibleRows at a time, one row per step. */
function rowScrollState(key: string | undefined, first: number, count: number, visibleRows: number, right: number, y: number, height: number): ScrollState | null {
  const max = Math.max(0, count - visibleRows);
  if (key === undefined || max <= 0) {
    return null;
  }
  return { key, offset: first, max, step: 1, bar: scrollbarParts(right - SCROLLBAR_WIDTH, y, height, first / max) };
}

/**
 * Components/ListView.cs: VisibleRows row panels (no box, no padding) of RowHeight, a scrollbar column always reserved,
 * and as wide as offered. Rows past the item count are hidden.
 */
export class LListView extends LContainer {
  rowHeight: number;
  visibleRows: number;
  /** Item count and the item in the first row (FirstVisibleIndex, clamped by the builder). */
  count = 0;
  first = 0;

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

  override scrollState(): ScrollState | null {
    const b = this.bounds;
    return rowScrollState(this.info?.nodeId, this.first, this.count, this.visibleRows, b.x + b.width, b.y, b.height);
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
  /** Item count and the item in the first row (FirstVisibleIndex, clamped by the builder). */
  count = 0;
  first = 0;
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

  override scrollState(): ScrollState | null {
    const b = this.bounds;
    return rowScrollState(this.info?.nodeId, this.first, this.count, this.visibleRows, b.x + b.width, b.y + this.headerHeight,
      Math.max(0, b.height - this.headerHeight));
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
