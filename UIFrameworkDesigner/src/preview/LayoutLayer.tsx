// The laid-out menu in the schematic preview (PreviewPane.tsx): the window, its title, a positioned DOM element per
// layout box (selection / hover / state classes, a link badge for menu actions), the scrollbars and the screen cut.
import type { CSSProperties, ReactNode } from 'react';
import type { LayoutBox, LayoutResult, Scroller } from '../layout';
import type { DesignerDocument, NodeId } from '../model/document';
import { subItemKind } from '../model/subItems';
import { BoxContent } from './BoxContent';
import type { GameArt } from './gameArt';
import { clipInset } from './geometry';
import { Scrollbar } from './Scrollbar';
import { fontStyle, renderText } from './text';

const containerKinds = new Set(['Stack', 'Grid', 'Canvas', 'Switch', 'Repeat', 'Outlet', 'Slot', 'Template', 'ScrollView', 'List', 'DataGrid', 'Form', 'Panel']);

/** What the boxes show: the selection, hover, i18n, art and the menu links. */
export interface BoxViewContext {
  nodes: DesignerDocument['nodes'] | undefined;
  selection: NodeId | null;
  hovered: NodeId | null;
  i18n: Record<string, string> | undefined;
  art: GameArt | undefined;
  /** The canvas zoom (badges keep their size). */
  scale: number;
  resolveMenuLink: ((nodeId: NodeId) => string | undefined) | undefined;
  onOpenMenu: ((key: string) => void) | undefined;
}

/** A box with no size (but a Slot) or scrolled / clipped out of sight is not drawn. */
function isDrawn(box: LayoutBox): boolean {
  const r = box.rect;
  if (r.width <= 0 && r.height <= 0 && box.kind !== 'Slot') {
    return false;
  }
  return !box.clipped || (box.clipped.width > 0 && box.clipped.height > 0);
}

function boxClasses(box: LayoutBox, ctx: BoxViewContext): string {
  const panelBox = box.kind === 'Panel' && box.detail?.drawBox;
  const classes = ['pv-box', 'pv-k-' + box.kind.replace(/\./g, '-').toLowerCase()];
  if (containerKinds.has(box.kind) && !panelBox) classes.push('pv-container');
  if (panelBox) classes.push('pv-panel-box');
  if (box.instance > 0) classes.push('pv-dim');
  if (box.hidden) classes.push('pv-hidden');
  if (box.detail?.enabled === false) classes.push('pv-disabled');
  if (box.unresolved) classes.push('pv-unresolved');
  // a node's own boxes (every instance) show its selection / hover; framework-made parts only when they stand for
  // a sub-item (a form field's caption and control, a column's header and cells), not for their parent
  const own = !box.synthetic || subItemKind(ctx.nodes?.[box.nodeId]?.type ?? '') !== undefined;
  if (own && box.nodeId === ctx.selection) classes.push('pv-selected');
  if (own && box.nodeId === ctx.hovered) classes.push('pv-hover');
  return classes.join(' ');
}

function boxTitle(box: LayoutBox): string {
  return box.kind + (box.instance > 0 ? ' #' + String(box.instance) : '') + (box.unresolved ? ' (position depends on unknown values)' : '');
}

function BoxView({ box, index, ctx }: { box: LayoutBox; index: number; ctx: BoxViewContext }): ReactNode {
  const r = box.rect;
  const link = !box.synthetic && box.instance === 0 && ctx.onOpenMenu ? ctx.resolveMenuLink?.(box.nodeId) : undefined;
  const style: CSSProperties = { left: r.x, top: r.y, width: r.width, height: r.height };
  if (box.clipped) {
    style.clipPath = clipInset(r, box.clipped);
  }
  return (
    <div data-box={index} className={boxClasses(box, ctx)} style={style} title={boxTitle(box)}>
      <BoxContent box={box} i18n={ctx.i18n} art={ctx.art} />
      {box.kind === 'Template' ? <span className="pv-tag">{renderText(box.label ?? '', ctx.i18n)}</span> : null}
      {link !== undefined ? (
        <button type="button" className="pv-link" style={{ transform: 'scale(' + String(1 / ctx.scale) + ')' }} title={'Open ' + link}
          onClick={e => { e.stopPropagation(); ctx.onOpenMenu?.(link); }}>↗</button>
      ) : null}
    </div>
  );
}

/** The drawn boxes, keyed by their index in the layout (data-box). */
export function layoutBoxes(boxes: LayoutBox[], ctx: BoxViewContext): ReactNode[] {
  return boxes.map((box, i) => (isDrawn(box) ? <BoxView key={i} box={box} index={i} ctx={ctx} /> : null));
}

export interface LayoutLayerProps {
  layout: LayoutResult;
  boxes: ReactNode;
  i18n: Record<string, string> | undefined;
  scale: number;
  fullHeight: boolean;
  screenH: number;
  toScreen(e: { clientX: number; clientY: number }): { x: number; y: number };
  onScroll(scroller: Scroller, offset: number): void;
}

export function LayoutLayer({ layout, boxes, i18n, scale, fullHeight, screenH, toScreen, onScroll }: LayoutLayerProps): ReactNode {
  const { window: w, title, titleText, drawBox } = layout;
  return (
    <>
      <div className={drawBox ? 'pv-window' : 'pv-window pv-window-bare'} style={{ left: w.x, top: w.y, width: w.width, height: w.height }} />
      {title && titleText ? (
        <div className={drawBox ? 'pv-title pv-title-scroll' : 'pv-title'}
          style={{ left: title.x, top: title.y, width: title.width, height: title.height, ...fontStyle('dialogue') }}>
          <span className="pv-nowrap">{renderText(titleText, i18n)}</span>
        </div>
      ) : null}
      {boxes}
      {layout.scrollers.map((sc, i) => (sc.bar ? <Scrollbar key={sc.key + ':' + String(i)} scroller={sc} toScreen={toScreen} onScroll={onScroll} /> : null))}
      {fullHeight && w.y + w.height > screenH ? (
        <div className="pv-cut" style={{ left: w.x, top: screenH, width: w.width }}>
          <span style={{ transform: 'scale(' + String(1 / scale) + ')' }}>Screen height {screenH} px: in game the window ends here and scrolls</span>
        </div>
      ) : null}
    </>
  );
}
