// Schematic preview (architecture.md §7): the layout port's boxes drawn as absolutely positioned DOM elements, in the
// schematic skin or, when the user picks their Content folder, the game-art skin (gameArt.ts), on a pan / zoom canvas
// (panZoom.ts) around the game screen. Scrollable boxes (the menu viewport, ScrollView, List, DataGrid) scroll like in
// game: wheel, scrollbar arrows, thumb drag. A tooltip tab shows its tooltip on the same canvas, its blocks selectable.
import { useEffect, useMemo, useRef, useState, type CSSProperties, type MouseEvent, type ReactNode } from 'react';
import type { DesignerDocument, NodeId } from '../model/document';
import { canvasTextMeasurer, layoutDocument, type LayoutBox, type LayoutResult, type Rect, type Scroller } from '../layout';
import { createEvaluator, previewValues, unknownNames, type ExternalFunctions } from './evaluate';
import type { GameArt } from './gameArt';
import { inside, union, within } from './geometry';
import { layoutBoxes, LayoutLayer } from './LayoutLayer';
import { defaultPreviewSettings, normalizeSettings, windowSizes, type PreviewSettings } from './previewSettings';
import { PreviewToolbar, UnknownNotice, type Skin } from './PreviewToolbar';
import { TooltipBox } from './Tooltip';
import { previewThemes, themeVariables } from './theme';
import { usePanZoom, type CanvasView } from './panZoom';
import './PreviewPane.css';

export { defaultPreviewSettings, type PreviewSettings } from './previewSettings';

/** A named tooltip shown by itself (a tooltip tab), centred on the screen. */
export interface TooltipContent {
  /** The TooltipDefinition JSON. */
  definition: unknown;
  /** The values its expressions read. */
  state: Record<string, string>;
  /** Each block's node, in block order (clicking a block selects it). */
  blocks: NodeId[];
}

export interface PreviewPaneProps {
  /** The menu or template laid out; absent when `tooltip` is shown instead. */
  doc?: DesignerDocument;
  tooltip?: TooltipContent;
  selection: NodeId | null;
  onSelect(id: NodeId | null): void;
  /** The case each Switch shows (from the inspector's case picker); the first page otherwise. */
  switchCases?: Record<NodeId, string>;
  /** A loaded i18n/default.json (I18nLoader): `{{i18n:key}}` text shows and measures as the translation; chips otherwise. */
  i18n?: Record<string, string>;
  /**
   * The RichTooltip of a node as TooltipDefinition JSON (its `From` already followed), shown on hover; when it gives
   * nothing, the node's Tooltip / TooltipTitle fields are shown.
   */
  resolveTooltip?: (nodeId: NodeId) => unknown;
  /** Screen, view and rows; controlled when given (with `onSettingsChange`), the pane's own state otherwise. */
  settings?: PreviewSettings;
  onSettingsChange?(settings: PreviewSettings): void;
  /** The menu (`owner/menu`) a node's action opens (`6135.UIFramework_OpenMenu …`), when it is one the host can open. */
  resolveMenuLink?: (nodeId: NodeId) => string | undefined;
  /** Open the menu of a link badge. */
  onOpenMenu?(key: string): void;
  /** Stand-ins for the functions C# registers (previewData.ts externalFunctions). */
  functions?: ExternalFunctions;
  /** Sample rows of the sources C# provides, by source key (LayoutOptions.sampleRows). */
  sampleRows?: Record<string, unknown[]>;
  /** Open the Preview state panel with these names (values the layout depends on that are unknown) first. */
  onShowState?(names: string[]): void;
}

/** Wheel movement (CSS pixels) that makes one notch: a mouse wheel notch is ~100, a trackpad sends small deltas. */
const WheelNotch = 40;

/** The game-art skin is its own chunk, loaded when the user first picks a Content folder. */
const pickGameArt = (): Promise<GameArt | undefined> => import('./gameArt').then(m => m.pickGameArt());

/** Clickable content: a layout box or a tooltip block. */
const itemSelector = '[data-box],[data-node]';

/** The settings, controlled (`controlled` with `onChange`) or the pane's own; saved ones normalized. */
function usePreviewSettings(controlled: PreviewSettings | undefined, onChange: ((settings: PreviewSettings) => void) | undefined) {
  const [ownSettings, setOwnSettings] = useState(defaultPreviewSettings);
  // settings saved before a member existed take its default
  const settings = normalizeSettings(controlled ?? ownSettings);
  const emit = (next: PreviewSettings): void => {
    if (onChange) {
      onChange(next);
    } else {
      setOwnSettings(next);
    }
  };
  const change = (patch: Partial<Omit<PreviewSettings, 'view'>>): void => emit({ ...settings, ...patch });
  const setView = (view: CanvasView | undefined): void => {
    const { view: _old, ...rest } = settings;
    emit(view ? { ...rest, view } : rest);
  };
  return { settings, change, setView };
}

/** The skin, and the game art (picked on first use of the art skin, released when replaced); `shownArt` while it is on. */
function useSkin() {
  const [skin, setSkin] = useState<Skin>('default');
  const [art, setArt] = useState<GameArt | undefined>(undefined);

  useEffect(() => () => {
    if (art) {
      void import('./gameArt').then(m => m.releaseGameArt(art));
    }
  }, [art]);

  const chooseSkin = async (value: Skin): Promise<void> => {
    if (value === 'art' && !art) {
      const picked = await pickGameArt();
      if (!picked) {
        return;
      }
      setArt(picked);
    }
    setSkin(value);
  };
  const pickContent = (): void => void pickGameArt().then(a => a && setArt(a));
  const shownArt = skin === 'art' && art !== undefined ? art : undefined;
  return { skin, shownArt, chooseSkin, pickContent };
}

/** What a fit shows: the screen and everything laid out beyond it. */
function contentBounds(layout: LayoutResult | null, screenW: number, screenH: number): Rect {
  let b: Rect = { x: 0, y: 0, width: screenW, height: screenH };
  if (layout) {
    b = union(b, layout.window);
    if (layout.title) b = union(b, layout.title);
    for (const box of layout.boxes) {
      // a box scrolled or clipped out of sight is not drawn and does not count
      const r = box.clipped ?? box.rect;
      if (!box.clipped || (r.width > 0 && r.height > 0)) b = union(b, r);
    }
  }
  return b;
}

/**
 * The wheel over a scrollable box scrolls it (one step per notch); at its end it falls through to the box around it
 * (HandleScroll unhandled), and over none it zooms the canvas (false).
 */
function useWheelScroll(scrollers: Scroller[], scrollTo: (scroller: Scroller, offset: number) => void) {
  const wheelRest = useRef(0);
  return (x: number, y: number, deltaY: number): boolean => {
    const under = scrollers.filter(sc => within(sc.area, x, y));
    if (under.length === 0) {
      wheelRest.current = 0;
      return false;
    }
    wheelRest.current += deltaY;
    if (Math.abs(wheelRest.current) < WheelNotch) {
      return true;
    }
    const down = wheelRest.current > 0;
    wheelRest.current = 0;
    const target = [...under].reverse().find(sc => (down ? sc.offset < sc.max : sc.offset > 0));
    if (target) scrollTo(target, target.offset + (down ? target.step : -target.step));
    return true;
  };
}

/** The node whose tooltip shows at a point: the topmost box there whose node has one. */
function tooltipNodeAt(boxes: LayoutBox[], doc: DesignerDocument | undefined, resolveTooltip: PreviewPaneProps['resolveTooltip'], at: { x: number; y: number }): NodeId | null {
  for (let i = boxes.length - 1; i >= 0; i--) {
    const box = boxes[i]!;
    if (!inside(box, at.x, at.y)) continue;
    const fields = doc?.nodes[box.nodeId]?.fields;
    if (fields?.Tooltip || fields?.TooltipTitle || (resolveTooltip?.(box.nodeId) ?? undefined) !== undefined) {
      return box.nodeId;
    }
  }
  return null;
}

/** The node under an event target: a layout box's (its owner unless alt is held) or a tooltip block's. */
function nodeAt(target: EventTarget, alt: boolean, layout: LayoutResult | null): NodeId | null {
  const el = (target as HTMLElement).closest?.(itemSelector);
  const node = el?.getAttribute('data-node');
  if (node) {
    return node;
  }
  const box = el ? layout?.boxes[Number(el.getAttribute('data-box'))] : undefined;
  if (!box) {
    return null;
  }
  return alt ? box.nodeId : box.ownerId ?? box.nodeId;
}

export function PreviewPane(props: PreviewPaneProps): ReactNode {
  const { doc, tooltip, selection, onSelect, switchCases, i18n, resolveTooltip, resolveMenuLink, onOpenMenu, functions, sampleRows, onShowState } = props;
  const { settings, change, setView } = usePreviewSettings(props.settings, props.onSettingsChange);
  const { screen, custom, uiScale, fullHeight, scroll, repeatCount, showHidden } = settings;
  const { skin, shownArt, chooseSkin, pickContent } = useSkin();
  const [hovered, setHovered] = useState<NodeId | null>(null);
  const [cursor, setCursor] = useState<{ x: number; y: number } | null>(null);
  const [tipNode, setTipNode] = useState<NodeId | null>(null);
  const worldRef = useRef<HTMLDivElement>(null);

  // the UI viewport (Game1.uiViewport, UIServices.ViewportSize): the window size ÷ options.uiScale, rounded up
  const [windowW, windowH] = screen === 'custom' ? [custom.width, custom.height] : windowSizes[screen];
  const screenW = Math.ceil(windowW * 100 / uiScale);
  const screenH = Math.ceil(windowH * 100 / uiScale);
  const measureText = useMemo(() => canvasTextMeasurer(i18n), [i18n]);
  const layout = useMemo(() => (doc ? layoutDocument(doc, {
    screenWidth: screenW,
    screenHeight: screenH,
    repeatCount,
    measureText,
    evaluate: createEvaluator(doc, functions),
    showHidden,
    fullHeight,
    ...(scroll ? { scrollOffsets: scroll } : {}),
    ...(switchCases ? { switchCases } : {}),
    ...(sampleRows ? { sampleRows } : {})
  }) : null), [doc, screenW, screenH, repeatCount, measureText, showHidden, fullHeight, scroll, switchCases, functions, sampleRows]);
  const values = useMemo(() => (doc ? previewValues(doc, functions) : {}), [doc, functions]);
  const unknown = useMemo(() => (doc && layout ? unknownNames(layout.unresolved, doc, functions) : []), [doc, layout, functions]);

  const bounds = useMemo(() => contentBounds(layout, screenW, screenH), [layout, screenW, screenH]);
  const scrollTo = (scroller: Scroller, offset: number): void => {
    const next = Math.min(Math.max(0, Math.round(offset)), scroller.max);
    if (next !== scroller.offset) change({ scroll: { ...scroll, [scroller.key]: next } });
  };
  const onWheel = useWheelScroll(layout?.scrollers ?? [], scrollTo);

  const canvas = usePanZoom(bounds, settings.view, setView, itemSelector, onWheel);
  const { x: viewX, y: viewY, zoom: scale } = canvas.view;
  const toScreen = (e: { clientX: number; clientY: number }): { x: number; y: number } => {
    const r = worldRef.current?.getBoundingClientRect();
    return r ? { x: (e.clientX - r.left) / scale, y: (e.clientY - r.top) / scale } : { x: 0, y: 0 };
  };

  const onClick = (e: MouseEvent): void => {
    onSelect(nodeAt(e.target, e.altKey, layout));
  };

  const onMove = (e: MouseEvent): void => {
    const id = nodeAt(e.target, e.altKey, layout);
    if (id !== hovered) {
      setHovered(id);
    }
    const r = worldRef.current?.getBoundingClientRect();
    if (r && layout) {
      const point = { x: Math.round((e.clientX - r.left) / scale), y: Math.round((e.clientY - r.top) / scale) };
      setCursor(point);
      const tip = tooltipNodeAt(layout.boxes, doc, resolveTooltip, point);
      if (tip !== tipNode) {
        setTipNode(tip);
      }
    }
  };

  const onLeave = (): void => {
    setHovered(null);
    setCursor(null);
    setTipNode(null);
  };

  const boxes = useMemo(() => (layout
    ? layoutBoxes(layout.boxes, { nodes: doc?.nodes, selection, hovered, i18n, art: shownArt, scale, resolveMenuLink, onOpenMenu })
    : undefined), [layout, doc?.nodes, selection, hovered, i18n, shownArt, scale, resolveMenuLink, onOpenMenu]);

  const tipFields = tipNode ? doc?.nodes[tipNode]?.fields : undefined;
  const tipDefinition = tipNode ? resolveTooltip?.(tipNode) : undefined;
  const worldClasses = ['pv-world', ...(shownArt ? shownArt.sheets.map(s => 'pv-art-' + s) : [])];
  const worldStyle = {
    transform: 'translate(' + String(viewX) + 'px, ' + String(viewY) + 'px) scale(' + String(scale) + ')',
    ...themeVariables(previewThemes[skin === 'dark' ? 'dark' : 'default']),
    ...shownArt?.vars
  } as CSSProperties;
  const screenSize = { width: screenW, height: screenH };
  return (
    <div className="pv-pane">
      <PreviewToolbar settings={settings} change={change} screenW={screenW} screenH={screenH} hasDoc={!!doc}
        zoom={scale} onFit={canvas.fit} onActualSize={canvas.actualSize}
        skin={skin} onSkin={value => void chooseSkin(value)} artOn={shownArt !== undefined} onPickContent={pickContent} />
      <UnknownNotice unknown={unknown} onShowState={onShowState} />
      <div ref={canvas.viewportRef} className={canvas.grabbing ? 'pv-view pv-grabbing' : 'pv-view'} onPointerDown={canvas.handlers.onPointerDown} onPointerMove={canvas.handlers.onPointerMove}
        onPointerUp={canvas.handlers.onPointerUp} onPointerCancel={canvas.handlers.onPointerCancel}
        onClickCapture={canvas.handlers.onClickCapture} onDoubleClick={canvas.handlers.onDoubleClick}
        onClick={onClick} onMouseMove={onMove} onMouseLeave={onLeave}>
        <div ref={worldRef} className={worldClasses.join(' ')} style={worldStyle}>
          <div className="pv-screen" style={screenSize} />
          {layout ? (
            <LayoutLayer layout={layout} boxes={boxes} i18n={i18n} scale={scale} fullHeight={fullHeight} screenH={screenH}
              toScreen={toScreen} onScroll={scrollTo} />
          ) : null}
          {tooltip ? (
            <TooltipBox definition={tooltip.definition} screen={screenSize} state={tooltip.state} blocks={tooltip.blocks}
              selection={selection} hovered={hovered} showHidden={showHidden}
              functions={functions} i18n={i18n} art={shownArt} />
          ) : null}
          <div className="pv-screen-frame" style={screenSize} />
          {cursor && tipNode ? (
            <TooltipBox
              definition={tipDefinition}
              title={tipFields?.TooltipTitle || undefined}
              text={tipFields?.Tooltip || undefined}
              cursor={cursor}
              screen={screenSize}
              state={values}
              functions={functions}
              i18n={i18n}
              art={shownArt}
            />
          ) : null}
        </div>
      </div>
    </div>
  );
}

export default PreviewPane;
