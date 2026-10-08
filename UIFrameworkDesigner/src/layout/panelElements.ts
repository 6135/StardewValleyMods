// Components/Panel.cs, Canvas.cs, ScrollView.cs (and its scrollbar) and Spacer.cs's measure / arrange pass (elements.ts).
import { type ElementInfo, type LayoutContext, LContainer, LElement, type ScrollState, type Vec, zeroRect } from './elementBase';
import { roundEven } from './engine';
import type { Rect, ScrollbarParts, Scroller } from './types';

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
/** ScrollbarGadget ArrowHeight, TrackWidth, TrackInset, TrackGap, ThumbHeight. */
const SCROLL_ARROW_HEIGHT = 48;
const SCROLL_TRACK_WIDTH = 24;
const SCROLL_TRACK_INSET = 12;
const SCROLL_TRACK_GAP = 4;
const SCROLL_THUMB_HEIGHT = 40;

/** ScrollbarGadget.Layout / SetFraction: the column at (x, y), `height` tall, the thumb at `fraction` of the track. */
export function scrollbarParts(x: number, y: number, height: number, fraction: number): ScrollbarParts {
  const h = Math.max(0, height);
  const bounds: Rect = { x, y, width: SCROLLBAR_WIDTH, height: h };
  let track: Rect = { x: x + SCROLL_TRACK_INSET, y, width: SCROLL_TRACK_WIDTH, height: h };
  let arrows: Pick<ScrollbarParts, 'up' | 'down'> = {};
  if (h >= 2 * SCROLL_ARROW_HEIGHT) {
    const up: Rect = { x, y, width: SCROLLBAR_WIDTH, height: SCROLL_ARROW_HEIGHT };
    const down: Rect = { x, y: y + h - SCROLL_ARROW_HEIGHT, width: SCROLLBAR_WIDTH, height: SCROLL_ARROW_HEIGHT };
    const trackY = up.y + up.height + SCROLL_TRACK_GAP;
    track = { x: x + SCROLL_TRACK_INSET, y: trackY, width: SCROLL_TRACK_WIDTH, height: Math.max(0, down.y - SCROLL_TRACK_GAP - trackY) };
    arrows = { up, down };
  }
  const f = Number.isNaN(fraction) ? 0 : Math.min(1, Math.max(0, fraction));
  const range = Math.max(0, track.height - SCROLL_THUMB_HEIGHT);
  const thumb: Rect = { x: track.x, y: track.y + roundEven(range * f), width: SCROLL_TRACK_WIDTH, height: SCROLL_THUMB_HEIGHT };
  return { bounds, ...arrows, track, thumb };
}

/** The offset a thumb drag / track click at `y` gives (ScrollbarGadget.FractionFromY × max, rounded like the owners). */
export function scrollOffsetAt(scroller: Scroller, y: number): number {
  const track = scroller.bar?.track;
  const range = track ? track.height - SCROLL_THUMB_HEIGHT : 0;
  if (!track || range <= 0) {
    return 0;
  }
  const fraction = Math.min(1, Math.max(0, (y - track.y - (SCROLL_THUMB_HEIGHT / 2)) / range));
  return roundEven(fraction * scroller.max);
}

/** An asked offset clamped to [0, max] like the framework's setters. */
export const clampOffset = (offset: number, max: number): number => Math.min(Math.max(0, Math.trunc(offset) || 0), Math.max(0, max));

/** Components/ScrollView.cs. */
export class LScrollView extends LContainer {
  private viewportHeightValue: number;
  showScrollbar = true;
  fitContent = false;
  /** ScrollOffset as asked (LayoutOptions.scrollOffsets); arrange clamps it to MaxScroll. */
  scrollOffset = 0;
  scrollStep = 64;
  /** Scroller.key: the node id, or MENU_SCROLL_KEY for the menu viewport. */
  scrollKey: string | null;
  private overflowing = false;
  private contentHeight = 0;

  constructor(ctx: LayoutContext, info: ElementInfo | null, viewportHeight: number) {
    super(ctx, info);
    this.viewportHeightValue = Math.max(0, viewportHeight);
    this.scrollKey = info?.nodeId ?? null;
  }

  get viewportHeight(): number { return this.viewportHeightValue; }
  set viewportHeight(v: number) { this.viewportHeightValue = Math.max(0, v); }

  private get reservedWidth(): number {
    return this.showScrollbar && (!this.fitContent || this.overflowing) ? SCROLLBAR_RESERVED : 0;
  }

  /** ScrollView.MaxScroll (after arrange). */
  private get maxScroll(): number {
    return Math.max(0, this.contentHeight - this.bounds.height);
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
    // the final height is known now; keep the offset valid before positioning anything
    this.scrollOffset = clampOffset(this.scrollOffset, this.maxScroll);
    const viewport = this.viewportRect();
    let y = viewport.y - this.scrollOffset;
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

  override scrollState(): ScrollState | null {
    const max = this.maxScroll;
    if (this.scrollKey === null || max <= 0) {
      return null;
    }
    const b = this.bounds;
    const state: ScrollState = { key: this.scrollKey, offset: this.scrollOffset, max, step: this.scrollStep };
    if (this.showScrollbar) {
      state.bar = scrollbarParts(b.x + b.width - SCROLLBAR_WIDTH, b.y, b.height, this.scrollOffset / max);
    }
    return state;
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
