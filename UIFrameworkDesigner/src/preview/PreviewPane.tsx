// Schematic preview (architecture.md §7): the layout port's boxes drawn as absolutely positioned DOM elements, in the
// schematic skin or, when the user picks their Content folder, the game-art skin (gameArt.ts), on a pan / zoom canvas
// (panZoom.ts) around the game screen. Scrollable boxes (the menu viewport, ScrollView, List, DataGrid) scroll like in
// game: wheel, scrollbar arrows, thumb drag. A tooltip tab shows its tooltip on the same canvas, its blocks selectable.
import { useEffect, useMemo, useRef, useState, type CSSProperties, type MouseEvent, type PointerEvent, type ReactNode } from 'react';
import type { DesignerDocument, NodeId } from '../model/document';
import { canvasTextMeasurer, CHIP_MARKS, layoutDocument, scrollOffsetAt, type LayoutBox, type Rect, type Scroller } from '../layout';
import { subItemKind } from '../model/subItems';
import { createEvaluator, previewValues, unknownNames, type ExternalFunctions } from './evaluate';
import type { GameArt } from './gameArt';
import { objectIndex } from './gameData';
import { fontStyle, renderText } from './text';
import { ItemSprite, TooltipBox } from './Tooltip';
import { previewThemes, themeVariables } from './theme';
import { usePanZoom, type CanvasView } from './panZoom';
import './PreviewPane.css';

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

/** Game window sizes; the menu is laid out on the UI viewport, the window ÷ the UI scale. */
const windowSizes = {
  '1280×720': [1280, 720], '1366×768': [1366, 768], '1600×900': [1600, 900], '1920×1080': [1920, 1080], '2560×1440': [2560, 1440]
} as const;
type ScreenKey = keyof typeof windowSizes | 'custom';
/** The game's UI scale option (Game1.options.uiScale), in percent. */
const uiScales = [75, 100, 125, 150] as const;

export interface PreviewSettings {
  /** The game window size. */
  screen: ScreenKey;
  custom: { width: number; height: number };
  /** UI scale in percent. */
  uiScale: number;
  /** Lay the window out as if the screen were tall enough for it (LayoutOptions.fullHeight). */
  fullHeight: boolean;
  /** Scroll offsets by scroll key (LayoutOptions.scrollOffsets). */
  scroll?: Record<string, number>;
  /** The canvas pan / zoom; absent: fit the screen and the content. */
  view?: CanvasView;
  /** Rows rendered by Repeat, List and DataGrid. */
  repeatCount: number;
  showHidden: boolean;
}

export const defaultPreviewSettings: PreviewSettings = {
  screen: '1920×1080', custom: { width: 1600, height: 900 }, uiScale: 100, fullHeight: false, repeatCount: 3, showHidden: false
};

/** Settings saved before a member existed take its default; a screen or scale no longer offered, the default one. */
function normalizeSettings(saved: PreviewSettings): PreviewSettings {
  const s = { ...defaultPreviewSettings, ...saved };
  if (s.screen !== 'custom' && !(s.screen in windowSizes)) s.screen = defaultPreviewSettings.screen;
  if (!(uiScales as readonly number[]).includes(s.uiScale)) s.uiScale = defaultPreviewSettings.uiScale;
  return s;
}

/** Wheel movement (CSS pixels) that makes one notch: a mouse wheel notch is ~100, a trackpad sends small deltas. */
const WheelNotch = 40;

type Skin = 'default' | 'dark' | 'art';

/** Whether the browser can pick a folder (Chromium); the game-art option is hidden elsewhere. */
const gameArtSupported = (): boolean => typeof window !== 'undefined' && 'showDirectoryPicker' in window;

/** The game-art skin is its own chunk, loaded when the user first picks a Content folder. */
const pickGameArt = (): Promise<GameArt | undefined> => import('./gameArt').then(m => m.pickGameArt());

interface BoxProps { box: LayoutBox; i18n: Record<string, string> | undefined; art: GameArt | undefined }

function TextLines({ box, i18n }: BoxProps): ReactNode {
  const d = box.detail ?? {};
  const lines = d.lines && d.lines.length > 0 ? d.lines : [d.text ?? box.label ?? ''];
  const align = d.textAlign === 'center' ? 'center' : d.textAlign === 'end' ? 'right' : 'left';
  return (
    <div className="pv-text" style={{ ...fontStyle(d.font, d.scale), textAlign: align, ...(d.color ? { color: d.color } : {}) }}>
      {lines.map((line, i) => <div key={i} className="pv-line">{renderText(line, i18n)}</div>)}
    </div>
  );
}

const containerKinds = new Set(['Stack', 'Grid', 'Canvas', 'Switch', 'Repeat', 'Outlet', 'Slot', 'Template', 'ScrollView', 'List', 'DataGrid', 'Form', 'Panel']);

function BoxContent(props: BoxProps): ReactNode {
  const { box, i18n, art } = props;
  const d = box.detail ?? {};
  const label = box.label ?? '';
  switch (box.kind) {
    case 'Label':
    case 'DataGrid.header':
    case 'DataGrid.cell':
    case 'Form.label':
    case 'Form.section':
      return <TextLines {...props} />;
    case 'Button':
      return (
        <div className={`pv-button${d.drawBox === false ? ' pv-flat' : ''}`} style={fontStyle(d.font)}>
          {d.sprite ? <span className="pv-icon" title={d.sprite}>▣</span> : null}
          <span className="pv-nowrap">{renderText(label, i18n)}</span>
        </div>
      );
    case 'Checkbox':
      return (
        <div className="pv-checkbox" style={fontStyle()}>
          <span className={`pv-check${d.checked ? ' pv-checked' : ''}`} />
          <span className="pv-nowrap">{renderText(label, i18n)}</span>
        </div>
      );
    case 'TextInput':
    case 'NumberInput':
      return (
        <div className="pv-textbox" style={fontStyle()}>
          {d.value ? renderText(d.value, i18n) : <span className="pv-placeholder">{renderText(d.placeholder ?? '', i18n)}</span>}
        </div>
      );
    case 'Dropdown':
      return (
        <div className="pv-dropdown" style={fontStyle()}>
          <span className="pv-nowrap">{renderText(d.value ?? label, i18n)}</span>
          <span className="pv-caret">▼</span>
        </div>
      );
    case 'Slider':
      return (
        <div className="pv-slider">
          <span className="pv-knob" style={{ left: `calc((100% - 40px) * ${d.fraction ?? 0})` }} />
        </div>
      );
    case 'ItemImage':
    case 'Image':
      if (box.kind === 'ItemImage' && d.sprite && objectIndex(d.sprite) !== undefined && art?.objects) {
        return <div className="pv-item-image"><ItemSprite id={d.sprite} art={art} size="100%" /></div>;
      }
      // an Item / Sprite expression the preview cannot evaluate: a neutral icon, the expression on hover
      if (d.sprite !== undefined && /[-]/.test(d.sprite)) {
        return <div className="pv-image" title={d.sprite.replace(CHIP_MARKS, '')}><span className="pv-image-icon">▣</span></div>;
      }
      return <div className="pv-image" title={d.sprite}><span>{renderText(d.sprite ?? box.kind, i18n)}</span></div>;
    case 'Spacer':
      return d.line ? <div className="pv-spacer-line" /> : null;
    case 'Custom':
    case 'Composite':
      return <div className="pv-unknown"><span>⟨{renderText(label, i18n)}⟩</span></div>;
    default:
      return null;
  }
}

const within = (r: Rect, x: number, y: number): boolean => x >= r.x && y >= r.y && x < r.x + r.width && y < r.y + r.height;
const inside = (box: LayoutBox, x: number, y: number): boolean => within(box.clipped ?? box.rect, x, y);

/** A CSS clip-path showing the part `c` of the element drawn at `r`. */
const clipInset = (r: Rect, c: Rect): string =>
  `inset(${c.y - r.y}px ${r.x + r.width - (c.x + c.width)}px ${r.y + r.height - (c.y + c.height)}px ${c.x - r.x}px)`;

interface ScrollbarProps {
  scroller: Scroller;
  /** A pointer position in screen (UI) pixels. */
  toScreen(e: { clientX: number; clientY: number }): { x: number; y: number };
  onScroll(scroller: Scroller, offset: number): void;
}

/** Components/ScrollbarGadget.cs drawn and clicked: arrows step, the thumb drags, the track jumps and drags. */
function Scrollbar({ scroller, toScreen, onScroll }: ScrollbarProps): ReactNode {
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

function union(a: Rect, b: Rect): Rect {
  const x = Math.min(a.x, b.x);
  const y = Math.min(a.y, b.y);
  return { x, y, width: Math.max(a.x + a.width, b.x + b.width) - x, height: Math.max(a.y + a.height, b.y + b.height) - y };
}

/** Clickable content: a layout box or a tooltip block. */
const itemSelector = '[data-box],[data-node]';

export function PreviewPane(props: PreviewPaneProps): ReactNode {
  const { doc, tooltip, selection, onSelect, switchCases, i18n, resolveTooltip, resolveMenuLink, onOpenMenu, functions, sampleRows, onShowState } = props;
  const [ownSettings, setOwnSettings] = useState(defaultPreviewSettings);
  // settings saved before a member existed take its default
  const settings = normalizeSettings(props.settings ?? ownSettings);
  const { screen, custom, uiScale, fullHeight, scroll, repeatCount, showHidden } = settings;
  const emit = (next: PreviewSettings): void => {
    if (props.onSettingsChange) {
      props.onSettingsChange(next);
    } else {
      setOwnSettings(next);
    }
  };
  const change = (patch: Partial<Omit<PreviewSettings, 'view'>>): void => emit({ ...settings, ...patch });
  const setView = (view: CanvasView | undefined): void => {
    const { view: _old, ...rest } = settings;
    emit(view ? { ...rest, view } : rest);
  };
  const [skin, setSkin] = useState<Skin>('default');
  const [art, setArt] = useState<GameArt | undefined>(undefined);
  const [hovered, setHovered] = useState<NodeId | null>(null);
  const [cursor, setCursor] = useState<{ x: number; y: number } | null>(null);
  const [tipNode, setTipNode] = useState<NodeId | null>(null);
  const worldRef = useRef<HTMLDivElement>(null);

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

  // what a fit shows: the screen and everything laid out beyond it
  const bounds = useMemo(() => {
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
  }, [layout, screenW, screenH]);
  const scrollTo = (scroller: Scroller, offset: number): void => {
    const next = Math.min(Math.max(0, Math.round(offset)), scroller.max);
    if (next !== scroller.offset) change({ scroll: { ...scroll, [scroller.key]: next } });
  };

  // the wheel over a scrollable box scrolls it (one step per notch); at its end it falls through to the box around it
  // (HandleScroll unhandled), and over none it zooms the canvas
  const wheelRest = useRef(0);
  const onWheel = (x: number, y: number, deltaY: number): boolean => {
    const under = (layout?.scrollers ?? []).filter(sc => within(sc.area, x, y));
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

  const canvas = usePanZoom(bounds, settings.view, setView, itemSelector, onWheel);
  const { x: viewX, y: viewY, zoom: scale } = canvas.view;
  const toScreen = (e: { clientX: number; clientY: number }): { x: number; y: number } => {
    const r = worldRef.current?.getBoundingClientRect();
    return r ? { x: (e.clientX - r.left) / scale, y: (e.clientY - r.top) / scale } : { x: 0, y: 0 };
  };

  const pick = (box: LayoutBox, alt: boolean): NodeId => (alt ? box.nodeId : box.ownerId ?? box.nodeId);
  const artOn = skin === 'art' && art !== undefined;
  const shownArt = artOn ? art : undefined;

  /** The node under an event target: a layout box's (its owner unless alt is held) or a tooltip block's. */
  const nodeAt = (target: EventTarget, alt: boolean): NodeId | null => {
    const el = (target as HTMLElement).closest?.(itemSelector);
    const node = el?.getAttribute('data-node');
    if (node) {
      return node;
    }
    const box = el ? layout?.boxes[Number(el.getAttribute('data-box'))] : undefined;
    return box ? pick(box, alt) : null;
  };

  /** The node whose tooltip shows at a point: the topmost box there whose node has one. */
  const tooltipNodeAt = (x: number, y: number): NodeId | null => {
    const boxes = layout?.boxes ?? [];
    for (let i = boxes.length - 1; i >= 0; i--) {
      const box = boxes[i]!;
      if (!inside(box, x, y)) continue;
      const fields = doc?.nodes[box.nodeId]?.fields;
      if (fields?.Tooltip || fields?.TooltipTitle || (resolveTooltip?.(box.nodeId) ?? undefined) !== undefined) {
        return box.nodeId;
      }
    }
    return null;
  };

  const onClick = (e: MouseEvent): void => {
    onSelect(nodeAt(e.target, e.altKey));
  };

  const onMove = (e: MouseEvent): void => {
    const id = nodeAt(e.target, e.altKey);
    if (id !== hovered) {
      setHovered(id);
    }
    const r = worldRef.current?.getBoundingClientRect();
    if (r && layout) {
      const point = { x: Math.round((e.clientX - r.left) / scale), y: Math.round((e.clientY - r.top) / scale) };
      setCursor(point);
      const tip = tooltipNodeAt(point.x, point.y);
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

  const boxes = useMemo(() => layout?.boxes.map((box, i) => {
    const r = box.rect;
    if (r.width <= 0 && r.height <= 0 && box.kind !== 'Slot') {
      return null;
    }
    if (box.clipped && (box.clipped.width <= 0 || box.clipped.height <= 0)) {
      return null;
    }
    const classes = ['pv-box', `pv-k-${box.kind.replace(/\./g, '-')}`];
    if (containerKinds.has(box.kind) && !(box.kind === 'Panel' && box.detail?.drawBox)) classes.push('pv-container');
    if (box.kind === 'Panel' && box.detail?.drawBox) classes.push('pv-panel-box');
    if (box.instance > 0) classes.push('pv-dim');
    if (box.hidden) classes.push('pv-hidden');
    if (box.detail?.enabled === false) classes.push('pv-disabled');
    if (box.unresolved) classes.push('pv-unresolved');
    // a node's own boxes (every instance) show its selection / hover; framework-made parts only when they stand for
    // a sub-item (a form field's caption and control, a column's header and cells), not for their parent
    const own = !box.synthetic || subItemKind(doc?.nodes[box.nodeId]?.type ?? '') !== undefined;
    if (own && box.nodeId === selection) classes.push('pv-selected');
    if (own && box.nodeId === hovered) classes.push('pv-hover');
    const link = !box.synthetic && box.instance === 0 && onOpenMenu ? resolveMenuLink?.(box.nodeId) : undefined;
    const style: CSSProperties = { left: r.x, top: r.y, width: r.width, height: r.height };
    if (box.clipped) {
      style.clipPath = clipInset(r, box.clipped);
    }
    return (
      <div key={i} data-box={i} className={classes.join(' ')} style={style}
        title={`${box.kind}${box.instance > 0 ? ` #${box.instance}` : ''}${box.unresolved ? ' (position depends on unknown values)' : ''}`}>
        <BoxContent box={box} i18n={i18n} art={shownArt} />
        {box.kind === 'Template' ? <span className="pv-tag">{renderText(box.label ?? '', i18n)}</span> : null}
        {link !== undefined ? (
          <button type="button" className="pv-link" style={{ transform: `scale(${1 / scale})` }} title={`Open ${link}`}
            onClick={e => { e.stopPropagation(); onOpenMenu?.(link); }}>↗</button>
        ) : null}
      </div>
    );
  }), [layout, doc?.nodes, selection, hovered, i18n, shownArt, scale, resolveMenuLink, onOpenMenu]);

  const tipFields = tipNode ? doc?.nodes[tipNode]?.fields : undefined;
  const tipDefinition = tipNode ? resolveTooltip?.(tipNode) : undefined;
  const worldClasses = ['pv-world', ...(shownArt ? shownArt.sheets.map(s => `pv-art-${s}`) : [])];
  const screenSize = { width: screenW, height: screenH };
  return (
    <div className="pv-pane">
      <div className="pv-toolbar">
        <label title="The game window size">Screen{' '}
          <select value={screen} onChange={e => change({ screen: e.target.value as ScreenKey })}>
            {Object.keys(windowSizes).map(k => <option key={k} value={k}>{k}</option>)}
            <option value="custom">Custom</option>
          </select>
        </label>
        {screen === 'custom' ? (
          <span className="pv-custom">
            <input type="number" min={320} max={7680} value={custom.width} aria-label="Window width"
              onChange={e => change({ custom: { ...custom, width: Math.max(320, Number(e.target.value) || 320) } })} />
            ×
            <input type="number" min={240} max={4320} value={custom.height} aria-label="Window height"
              onChange={e => change({ custom: { ...custom, height: Math.max(240, Number(e.target.value) || 240) } })} />
          </span>
        ) : null}
        <label title={`The game's UI scale option: menus are laid out on the window size ÷ the UI scale (${screenW}×${screenH} UI pixels)`}>UI{' '}
          <select value={uiScale} onChange={e => change({ uiScale: Number(e.target.value) })}>
            {uiScales.map(p => <option key={p} value={p}>{p}%</option>)}
          </select>
        </label>
        <label title="Lay the window out as if the screen were tall enough for all of it; a line marks where the screen ends and the window would scroll">
          <input type="checkbox" checked={fullHeight} disabled={!doc} onChange={e => change({ fullHeight: e.target.checked })} /> Full height
        </label>
        <span className="pv-zoom" role="group" aria-label="Zoom">
          <button type="button" onClick={canvas.fit} title="Fit the screen and the content (double-click empty canvas)">Fit</button>
          <button type="button" onClick={canvas.actualSize} title="Actual size">100%</button>
          <output title="Wheel or pinch to zoom; drag empty canvas, middle button or space + drag to pan">{Math.round(scale * 100)}%</output>
        </span>
        <label title="Rows rendered by Repeat, List and DataGrid">Rows{' '}
          <input type="number" min={0} max={50} value={repeatCount} className="pv-small" disabled={!doc}
            onChange={e => change({ repeatCount: Math.min(50, Math.max(0, Number(e.target.value) || 0)) })} />
        </label>
        <label><input type="checkbox" checked={showHidden} onChange={e => change({ showHidden: e.target.checked })} /> Show hidden</label>
        <label>Theme{' '}
          <select value={skin} onChange={e => void chooseSkin(e.target.value as Skin)}>
            <option value="default">Default</option>
            <option value="dark">Dark</option>
            {gameArtSupported() ? <option value="art" title="Pick your unpacked Content folder; read locally, never uploaded">Game art…</option> : null}
          </select>
        </label>
        {artOn ? <button type="button" onClick={() => void pickGameArt().then(a => a && setArt(a))} title="Pick another Content folder">Content…</button> : null}
      </div>
      {unknown.length > 0 ? (
        <div className="pv-notice" role="status">
          <span className="pv-notice-text" title={unknown.join(', ')}>
            Layout depends on {unknown.length} unknown value{unknown.length === 1 ? '' : 's'}: <code>{unknown.join(', ')}</code>
          </span>
          {onShowState ? <button type="button" onClick={() => onShowState(unknown)}>Set them in Preview state</button> : null}
        </div>
      ) : null}
      <div ref={canvas.viewportRef} className={canvas.grabbing ? 'pv-view pv-grabbing' : 'pv-view'} {...canvas.handlers}
        onClick={onClick} onMouseMove={onMove} onMouseLeave={onLeave}>
        <div
          ref={worldRef}
          className={worldClasses.join(' ')}
          style={{ transform: `translate(${viewX}px, ${viewY}px) scale(${scale})`, ...themeVariables(previewThemes[skin === 'dark' ? 'dark' : 'default']), ...shownArt?.vars } as CSSProperties}
        >
          <div className="pv-screen" style={screenSize} />
          {layout ? (
            <>
              <div className={layout.drawBox ? 'pv-window' : 'pv-window pv-window-bare'}
                style={{ left: layout.window.x, top: layout.window.y, width: layout.window.width, height: layout.window.height }} />
              {layout.title && layout.titleText ? (
                <div className={layout.drawBox ? 'pv-title pv-title-scroll' : 'pv-title'}
                  style={{ left: layout.title.x, top: layout.title.y, width: layout.title.width, height: layout.title.height, ...fontStyle('dialogue') }}>
                  <span className="pv-nowrap">{renderText(layout.titleText, i18n)}</span>
                </div>
              ) : null}
              {boxes}
              {layout.scrollers.map((sc, i) => (sc.bar ? <Scrollbar key={`${sc.key}:${i}`} scroller={sc} toScreen={toScreen} onScroll={scrollTo} /> : null))}
              {fullHeight && layout.window.y + layout.window.height > screenH ? (
                <div className="pv-cut" style={{ left: layout.window.x, top: screenH, width: layout.window.width }}>
                  <span style={{ transform: `scale(${1 / scale})` }}>Screen height {screenH} px: in game the window ends here and scrolls</span>
                </div>
              ) : null}
            </>
          ) : null}
          {tooltip ? (
            <TooltipBox definition={tooltip.definition} screen={screenSize} state={tooltip.state} blocks={tooltip.blocks}
              selection={selection} hovered={hovered} showHidden={showHidden}
              {...(functions ? { functions } : {})} {...(i18n ? { i18n } : {})} {...(shownArt ? { art: shownArt } : {})} />
          ) : null}
          <div className="pv-screen-frame" style={screenSize} />
          {cursor && tipNode ? (
            <TooltipBox
              definition={tipDefinition}
              {...(tipFields?.TooltipTitle ? { title: tipFields.TooltipTitle } : {})}
              {...(tipFields?.Tooltip ? { text: tipFields.Tooltip } : {})}
              cursor={cursor}
              screen={screenSize}
              state={values}
              {...(functions ? { functions } : {})}
              {...(i18n ? { i18n } : {})}
              {...(shownArt ? { art: shownArt } : {})}
            />
          ) : null}
        </div>
      </div>
    </div>
  );
}

export default PreviewPane;
