// The preview's pan / zoom canvas (architecture.md §7.1), like a diagram viewer: the wheel (and a trackpad pinch,
// ctrl + wheel) zooms around the cursor, a drag pans (left button once past a few pixels, so clicks still select;
// middle button or space + drag at once), one finger pans and two pinch on touch, a double click on empty canvas fits.
import { useEffect, useLayoutEffect, useRef, useState, type MouseEvent, type PointerEvent, type RefObject } from 'react';
import type { Rect } from '../layout';

/** Where the screen's top-left corner sits in the viewport (CSS pixels) and the zoom; absent in settings: fit. */
export interface CanvasView { x: number; y: number; zoom: number }

const MinZoom = 0.05;
const MaxZoom = 8;
/** A left-button drag pans once it has moved this far (CSS pixels); shorter, it is a click. */
const DragSlop = 4;
const FitMargin = 24;

const clampZoom = (zoom: number): number => Math.min(MaxZoom, Math.max(MinZoom, zoom));

/** The view that shows `bounds` whole, centred in a viewport of `size`. */
export function fitView(bounds: Rect, size: { width: number; height: number }): CanvasView {
  const zoom = clampZoom(Math.min(
    Math.max(1, size.width - (2 * FitMargin)) / Math.max(1, bounds.width),
    Math.max(1, size.height - (2 * FitMargin)) / Math.max(1, bounds.height)
  ));
  return { zoom, x: ((size.width - (bounds.width * zoom)) / 2) - (bounds.x * zoom), y: ((size.height - (bounds.height * zoom)) / 2) - (bounds.y * zoom) };
}

/** The view zoomed to `zoom` keeping the content under viewport point (px, py) in place. */
function zoomAt(view: CanvasView, zoom: number, px: number, py: number): CanvasView {
  const z = clampZoom(zoom);
  return { zoom: z, x: px - ((px - view.x) * z / view.zoom), y: py - ((py - view.y) * z / view.zoom) };
}

export interface PanZoom {
  viewportRef: RefObject<HTMLDivElement | null>;
  /** The view in effect (the fit when none is set). */
  view: CanvasView;
  /** Whether a pan is in progress or space is held (the grab cursor). */
  grabbing: boolean;
  fit(): void;
  /** 100 % around the viewport centre. */
  actualSize(): void;
  /** Viewport handlers; `onClickCapture` swallows the click that ends a pan. */
  handlers: {
    onPointerDown(e: PointerEvent): void;
    onPointerMove(e: PointerEvent): void;
    onPointerUp(e: PointerEvent): void;
    onPointerCancel(e: PointerEvent): void;
    onClickCapture(e: MouseEvent): void;
    onDoubleClick(e: MouseEvent): void;
  };
}

interface Gesture {
  /** Active pointers, by id: where each is now. */
  points: Map<number, { x: number; y: number }>;
  start: { x: number; y: number };
  startView: CanvasView;
  panning: boolean;
  /** Two-finger pinch: the distance and view when it began. */
  pinch?: { distance: number; view: CanvasView };
}

/**
 * Pan / zoom state for a viewport showing `bounds` (screen coordinates); `view` undefined means fit, which follows the
 * viewport's size. `itemSelector` matches the clickable content (a double click there is not a fit).
 */
export function usePanZoom(bounds: Rect, view: CanvasView | undefined, onView: (view: CanvasView | undefined) => void, itemSelector: string): PanZoom {
  const viewportRef = useRef<HTMLDivElement | null>(null);
  const [size, setSize] = useState({ width: 800, height: 600 });
  const [space, setSpace] = useState(false);
  const [panning, setPanning] = useState(false);
  const gesture = useRef<Gesture | null>(null);
  const swallowClick = useRef(false);

  useLayoutEffect(() => {
    const el = viewportRef.current;
    if (!el || typeof ResizeObserver === 'undefined') {
      return undefined;
    }
    setSize({ width: el.clientWidth, height: el.clientHeight });
    const observer = new ResizeObserver(entries => {
      const r = entries[0]?.contentRect;
      if (r) {
        setSize(prev => (prev.width === r.width && prev.height === r.height ? prev : { width: r.width, height: r.height }));
      }
    });
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  const current = view ?? fitView(bounds, size);
  // the native wheel listener (non-passive, to keep the page from scrolling) reads the latest view through a ref
  const latest = useRef({ current, onView });
  latest.current = { current, onView };

  useEffect(() => {
    const el = viewportRef.current;
    if (!el) {
      return undefined;
    }
    const onWheel = (e: WheelEvent): void => {
      e.preventDefault();
      const r = el.getBoundingClientRect();
      const lines = e.deltaMode === 1 ? 16 : e.deltaMode === 2 ? r.height : 1;
      // a trackpad pinch arrives as ctrl + wheel with small deltas: zoom faster per pixel
      const factor = Math.exp(-e.deltaY * lines * (e.ctrlKey ? 0.01 : 0.0015));
      const { current: v, onView: set } = latest.current;
      set(zoomAt(v, v.zoom * factor, e.clientX - r.left, e.clientY - r.top));
    };
    el.addEventListener('wheel', onWheel, { passive: false });
    return () => el.removeEventListener('wheel', onWheel);
  }, []);

  useEffect(() => {
    const typing = (target: EventTarget | null): boolean =>
      target instanceof HTMLElement && (target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName));
    const down = (e: KeyboardEvent): void => {
      if (e.code === 'Space' && !typing(e.target)) {
        if (viewportRef.current?.matches(':hover')) {
          e.preventDefault();
        }
        setSpace(true);
      }
    };
    const up = (e: KeyboardEvent): void => {
      if (e.code === 'Space') setSpace(false);
    };
    const blur = (): void => setSpace(false);
    window.addEventListener('keydown', down);
    window.addEventListener('keyup', up);
    window.addEventListener('blur', blur);
    return () => {
      window.removeEventListener('keydown', down);
      window.removeEventListener('keyup', up);
      window.removeEventListener('blur', blur);
    };
  }, []);

  const local = (e: { clientX: number; clientY: number }): { x: number; y: number } => {
    const r = viewportRef.current!.getBoundingClientRect();
    return { x: e.clientX - r.left, y: e.clientY - r.top };
  };

  const startPan = (e: PointerEvent, g: Gesture): void => {
    g.panning = true;
    setPanning(true);
    viewportRef.current?.setPointerCapture(e.pointerId);
  };

  const onPointerDown = (e: PointerEvent): void => {
    if (e.button !== 0 && e.button !== 1) {
      return;
    }
    const point = local(e);
    const g = gesture.current;
    if (g && e.pointerType === 'touch' && g.points.size === 1) {
      // a second finger: pinch around the two
      g.points.set(e.pointerId, point);
      const [a, b] = [...g.points.values()];
      g.pinch = { distance: Math.max(1, Math.hypot(a!.x - b!.x, a!.y - b!.y)), view: current };
      viewportRef.current?.setPointerCapture(e.pointerId);
      return;
    }
    const next: Gesture = { points: new Map([[e.pointerId, point]]), start: point, startView: current, panning: false };
    gesture.current = next;
    swallowClick.current = false;
    if (e.button === 1 || space) {
      e.preventDefault();
      startPan(e, next);
    }
  };

  const onPointerMove = (e: PointerEvent): void => {
    const g = gesture.current;
    if (!g || !g.points.has(e.pointerId)) {
      return;
    }
    const point = local(e);
    g.points.set(e.pointerId, point);
    if (g.pinch && g.points.size === 2) {
      const [a, b] = [...g.points.values()];
      const distance = Math.max(1, Math.hypot(a!.x - b!.x, a!.y - b!.y));
      onView(zoomAt(g.pinch.view, g.pinch.view.zoom * distance / g.pinch.distance, (a!.x + b!.x) / 2, (a!.y + b!.y) / 2));
      swallowClick.current = true;
      return;
    }
    if (!g.panning && Math.hypot(point.x - g.start.x, point.y - g.start.y) > DragSlop) {
      startPan(e, g);
    }
    if (g.panning) {
      onView({ ...g.startView, x: g.startView.x + point.x - g.start.x, y: g.startView.y + point.y - g.start.y });
    }
  };

  const end = (e: PointerEvent): void => {
    const g = gesture.current;
    if (!g || !g.points.has(e.pointerId)) {
      return;
    }
    if (g.panning || g.pinch) {
      swallowClick.current = true;
    }
    g.points.delete(e.pointerId);
    if (g.points.size === 0) {
      gesture.current = null;
      setPanning(false);
    } else if (g.pinch) {
      // one finger left after a pinch: it pans on from where it is
      const [rest] = [...g.points.values()];
      gesture.current = { points: g.points, start: rest!, startView: latest.current.current, panning: true };
    }
  };

  const onClickCapture = (e: MouseEvent): void => {
    if (swallowClick.current) {
      swallowClick.current = false;
      e.stopPropagation();
      e.preventDefault();
    }
  };

  const onDoubleClick = (e: MouseEvent): void => {
    if (!(e.target as Element).closest?.(itemSelector)) {
      onView(undefined);
    }
  };

  const fit = (): void => onView(undefined);
  const actualSize = (): void => onView(zoomAt(current, 1, size.width / 2, size.height / 2));

  return {
    viewportRef,
    view: current,
    grabbing: panning || space,
    fit,
    actualSize,
    handlers: { onPointerDown, onPointerMove, onPointerUp: end, onPointerCancel: end, onClickCapture, onDoubleClick }
  };
}
