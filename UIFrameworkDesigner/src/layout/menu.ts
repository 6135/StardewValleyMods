// Port of the window layout in StardewUIFramework/Core/UIMenu.cs (Relayout, ResolvePosition, AnchorX / AnchorY,
// TitleStrip, ContentBounds, the title draw) plus the box list the preview draws.
import type { DesignerDocument } from '../model/document';
import { defaultOf } from '../model/metadata';
import { Builder, fromNode } from './build';
import { LScrollView, LStack, type LElement } from './elements';
import type { LayoutBox, LayoutOptions, LayoutResult, Rect } from './types';
import { CHIP_MARKS } from './types';
import { type Anchor, parseAlign, parseAnchor, parseBool, parseInt32 } from './values';

/** UIMenu.BoxInsetSide / BoxInsetTop / BoxInsetBottom: IClickableMenu.spaceToClearSideBorder + borderWidth. */
const BOX_INSET = 56;
/** UIMenu.TitleReserve: the title scroll is 72 px tall and drawn 64 px above the box. */
const TITLE_RESERVE = 80;
/** UIMenu.TitleScrollCaps: SpriteText's 12 px end caps at 4x, one per side. */
const TITLE_SCROLL_CAPS = 2 * 12 * 4;
/** UIMenu.TitleMargin. */
const TITLE_MARGIN = 8;

function intersect(a: Rect, b: Rect): Rect {
  const x = Math.max(a.x, b.x);
  const y = Math.max(a.y, b.y);
  const right = Math.min(a.x + a.width, b.x + b.width);
  const bottom = Math.min(a.y + a.height, b.y + b.height);
  return { x, y, width: Math.max(0, right - x), height: Math.max(0, bottom - y) };
}

const inside = (r: Rect, clip: Rect): boolean =>
  r.x >= clip.x && r.y >= clip.y && r.x + r.width <= clip.x + clip.width && r.y + r.height <= clip.y + clip.height;

function emit(element: LElement, clip: Rect | null, depth: number, boxes: LayoutBox[]): void {
  if (!element.visible) {
    return;
  }
  element.finishDetail();
  let childDepth = depth;
  const info = element.info;
  if (info) {
    const box: LayoutBox = { nodeId: info.nodeId, rect: { ...element.bounds }, kind: info.kind, instance: info.instance, depth };
    if (info.label !== undefined) box.label = info.label;
    if (clip && !inside(element.bounds, clip)) box.clipped = intersect(element.bounds, clip);
    if (info.hidden) box.hidden = true;
    if (info.ownerId !== undefined) box.ownerId = info.ownerId;
    if (info.synthetic) box.synthetic = true;
    if (info.unresolved) box.unresolved = true;
    if (info.detail) box.detail = info.detail;
    boxes.push(box);
    childDepth = depth + 1;
  }
  const own = element.childClip();
  const childClip = own ? (clip ? intersect(clip, own) : own) : clip;
  for (const child of element.childElements) {
    emit(child, childClip, childDepth, boxes);
  }
}

/** Lays out the document's menu on a screen of opts.screenWidth × opts.screenHeight (UIMenu.Relayout). */
export function layoutDocument(doc: DesignerDocument, opts: LayoutOptions): LayoutResult {
  const ctx = { measure: opts.measureText };
  const builder = new Builder(doc, opts, ctx);
  const v = builder.resolve;
  const scope = builder.rootScope();
  const menu = doc.menu;
  const menuBool = (field: string): boolean => v.bool(menu[field], scope) ?? parseBool(defaultOf('menu', field) ?? '') ?? false;
  const menuInt = (field: string): number => v.int(menu[field], scope) ?? parseInt32(defaultOf('menu', field) ?? '') ?? 0;

  const drawBox = menuBool('DrawBox');
  const { padding, width, height, anchor, x, y } = v.collect(builder.unresolved, () => ({
    padding: Math.max(0, menuInt('Padding')),
    width: v.optInt(menu.Width, scope) ?? null,
    height: v.optInt(menu.Height, scope) ?? null,
    anchor: v.typed(menu.Anchor, scope, parseAnchor, false) ?? parseAnchor(defaultOf('menu', 'Anchor') ?? '') ?? 'center' as Anchor,
    x: menuInt('X'),
    y: menuInt('Y')
  }));
  const hasTitle = menu.Title !== undefined;
  const titleText = hasTitle ? v.text(menu.Title, scope) : undefined;

  // UIMenu constructor: the root stack (stretched) inside a fit-content viewport (stretched)
  const root = new LStack(ctx, null, menuBool('Horizontal'), menuInt('Spacing'));
  root.alignment = v.typed(menu.Alignment, scope, parseAlign, false) ?? parseAlign(defaultOf('menu', 'Alignment') ?? '') ?? 'start';
  root.horizontalAlign = 'stretch';
  root.verticalAlign = 'stretch';
  const viewport = new LScrollView(ctx, null, 0);
  viewport.fitContent = true;
  viewport.horizontalAlign = 'stretch';
  viewport.verticalAlign = 'stretch';
  viewport.add(root);
  const rootSrc = fromNode(doc, doc.root);
  builder.buildChildren(root, rootSrc ? rootSrc.children() : [], scope);

  // Relayout
  const vp = { x: Math.max(1, opts.screenWidth), y: Math.max(1, opts.screenHeight) };
  const inset = (drawBox ? BOX_INSET : 0) + padding;
  const insetW = 2 * inset;
  const insetH = 2 * inset;
  const banner = hasTitle && drawBox ? TITLE_RESERVE : 0;
  const maxH = Math.max(1, vp.y - banner);
  const availW = (width ?? vp.x) - insetW;
  const availH = Math.min(height ?? maxH, maxH) - insetH;
  viewport.measure({ x: Math.max(0, availW), y: Math.max(0, availH) });
  let w = width ?? Math.ceil(viewport.desired.x) + insetW;
  let h = height ?? Math.ceil(viewport.desired.y) + insetH;
  w = Math.min(Math.max(w, Math.min(insetW, vp.x)), Math.max(vp.x, 1));
  h = Math.min(Math.max(h, Math.min(insetH, maxH)), maxH);

  // ResolvePosition (first layout: no settled position)
  const minY = banner > 0 ? Math.min(TITLE_RESERVE, Math.max(0, vp.y - h)) : 0;
  const ax = anchor === 'explicit' ? x
    : anchor === 'topleft' || anchor === 'middleleft' || anchor === 'bottomleft' ? 0
    : anchor === 'topright' || anchor === 'middleright' || anchor === 'bottomright' ? vp.x - w
    : Math.trunc((vp.x - w) / 2);
  const ay = anchor === 'explicit' ? y
    : anchor === 'topleft' || anchor === 'topcenter' || anchor === 'topright' ? minY
    : anchor === 'bottomleft' || anchor === 'bottomcenter' || anchor === 'bottomright' ? vp.y - h
    : Math.trunc((vp.y - h) / 2);
  const px = Math.min(Math.max(ax, 0), Math.max(0, vp.x - w));
  const py = Math.min(Math.max(ay, minY), Math.max(minY, vp.y - h));
  const window: Rect = { x: px, y: py, width: w, height: h };
  const content: Rect = { x: px + inset, y: py + inset, width: Math.max(0, w - insetW), height: Math.max(0, h - insetH) };
  viewport.arrange(content);

  const boxes: LayoutBox[] = [];
  emit(viewport, null, 0, boxes);

  const result: LayoutResult = { window, content, drawBox, boxes, unresolved: builder.unresolved };
  if (titleText !== undefined && titleText.replace(CHIP_MARKS, '').length > 0) {
    result.titleText = titleText;
    // SpriteText's width is approximated with the dialogue font
    const textWidth = opts.measureText(titleText, 'dialogue', 1).width;
    const centerX = px + Math.trunc(w / 2);
    if (drawBox) {
      // drawStringWithScrollCenteredAt(…, Math.Max(12, Bounds.Y - 68)): the scroll spans [y - 12, y + 60]
      const sw = Math.min(textWidth, Math.max(0, w - (2 * TITLE_MARGIN) - TITLE_SCROLL_CAPS)) + TITLE_SCROLL_CAPS;
      const ty = Math.max(12, py - 68) - 12;
      result.title = { x: Math.round(centerX - (sw / 2)), y: ty, width: Math.round(sw), height: 72 };
    } else {
      const tw = Math.min(textWidth, Math.max(0, w - (2 * TITLE_MARGIN)));
      result.title = { x: Math.round(centerX - (tw / 2)), y: py + 8, width: Math.round(tw), height: opts.measureText('', 'dialogue', 1).height };
    }
  }
  return result;
}
