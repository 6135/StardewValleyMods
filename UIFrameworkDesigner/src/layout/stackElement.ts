// Components/Stack.cs's measure / arrange pass (elements.ts).
import { type ElementInfo, type LayoutContext, LContainer, type Vec, zeroRect } from './elementBase';
import { type Align, distributeWidth, roundEven, toInt, unbounded } from './engine';

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
