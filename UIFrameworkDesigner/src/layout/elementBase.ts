// Core/UIElement.cs and Core/UIContainer.cs: the element / container base of the layout pass (elements.ts).
import type { NodeId } from '../model/document';
import { type Align, alignOffset, toInt } from './engine';
import type { BoxDetail, Rect, Scroller, TextMeasurer } from './types';

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

export const zeroRect = (x: number, y: number): Rect => ({ x, y, width: 0, height: 0 });

export const sum = (values: readonly number[]): number => values.reduce((a, b) => a + b, 0);

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

  /** After arrange: how the element scrolls, when its content overflows (ScrollView, List, DataGrid); else null. */
  scrollState(): ScrollState | null {
    return null;
  }
}

/** A scroller as the element knows it; the menu adds the area and the ancestors' clip. */
export type ScrollState = Omit<Scroller, 'area' | 'clip'>;

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
