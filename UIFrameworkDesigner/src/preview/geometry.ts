// Rectangle helpers of the schematic preview (PreviewPane.tsx, Scrollbar.tsx).
import type { LayoutBox, Rect } from '../layout';

export const within = (r: Rect, x: number, y: number): boolean => x >= r.x && y >= r.y && x < r.x + r.width && y < r.y + r.height;
export const inside = (box: LayoutBox, x: number, y: number): boolean => within(box.clipped ?? box.rect, x, y);

/** A CSS clip-path showing the part `c` of the element drawn at `r`. */
export const clipInset = (r: Rect, c: Rect): string =>
  `inset(${c.y - r.y}px ${r.x + r.width - (c.x + c.width)}px ${r.y + r.height - (c.y + c.height)}px ${c.x - r.x}px)`;

export function union(a: Rect, b: Rect): Rect {
  const x = Math.min(a.x, b.x);
  const y = Math.min(a.y, b.y);
  return { x, y, width: Math.max(a.x + a.width, b.x + b.width) - x, height: Math.max(a.y + a.height, b.y + b.height) - y };
}
