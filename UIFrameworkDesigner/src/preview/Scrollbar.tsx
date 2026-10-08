// Components/ScrollbarGadget.cs in the schematic preview (PreviewPane.tsx).
import { useRef, type CSSProperties, type MouseEvent, type PointerEvent, type ReactNode } from 'react';
import { scrollOffsetAt, type Rect, type Scroller } from '../layout';
import { clipInset, within } from './geometry';

interface ScrollbarProps {
  scroller: Scroller;
  /** A pointer position in screen (UI) pixels. */
  toScreen(e: { clientX: number; clientY: number }): { x: number; y: number };
  onScroll(scroller: Scroller, offset: number): void;
}

/** Components/ScrollbarGadget.cs drawn and clicked: arrows step, the thumb drags, the track jumps and drags. */
export function Scrollbar({ scroller, toScreen, onScroll }: ScrollbarProps): ReactNode {
  const bar = scroller.bar!;
  const b = bar.bounds;
  const dragging = useRef(false);
  const at = (r: Rect): CSSProperties => ({ left: r.x - b.x, top: r.y - b.y, width: r.width, height: r.height });
  const style: CSSProperties = { left: b.x, top: b.y, width: b.width, height: b.height };
  if (scroller.clip) style.clipPath = clipInset(b, scroller.clip);

  const down = (e: PointerEvent): void => {
    e.stopPropagation();
    if (e.button !== 0) return;
    const p = toScreen(e);
    if (bar.up && within(bar.up, p.x, p.y)) {
      onScroll(scroller, scroller.offset - scroller.step);
    } else if (bar.down && within(bar.down, p.x, p.y)) {
      onScroll(scroller, scroller.offset + scroller.step);
    } else if (p.y >= bar.track.y && p.y < bar.track.y + bar.track.height) {
      // ScrollbarGadget.HitTest: the strip beside the track is track; a track click jumps, then both drag
      dragging.current = true;
      e.currentTarget.setPointerCapture(e.pointerId);
      if (!within(bar.thumb, p.x, p.y)) onScroll(scroller, scrollOffsetAt(scroller, p.y));
    }
  };
  const move = (e: PointerEvent): void => {
    if (dragging.current) onScroll(scroller, scrollOffsetAt(scroller, toScreen(e).y));
  };
  const up = (): void => {
    dragging.current = false;
  };
  const stop = (e: MouseEvent): void => e.stopPropagation();
  return (
    <div className="pv-sb" style={style} onPointerDown={down} onPointerMove={move} onPointerUp={up} onPointerCancel={up}
      onClick={stop} onDoubleClick={stop}>
      {bar.up ? <div className="pv-sb-arrow" style={at(bar.up)}>▲</div> : null}
      {bar.down ? <div className="pv-sb-arrow" style={at(bar.down)}>▼</div> : null}
      {bar.track.height > 0 ? <div className="pv-sb-track" style={at(bar.track)} /> : null}
      {bar.track.height >= bar.thumb.height ? <div className="pv-sb-thumb" style={at(bar.thumb)} /> : null}
    </div>
  );
}
