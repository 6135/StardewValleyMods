// Hover tooltips in the preview, laid out like StardewUIFramework/Rendering/TooltipRenderer.cs: a panel box with 16 px
// padding, rows 4 px apart (Title in the dialogue font, Line in the small font, Item / Money as a line-high icon, 8 px
// gap and the name / amount, Divider 2 px with 4 px margins), placed 32 px right / below the cursor and kept on screen.
// The definition is the TooltipDefinition JSON (Data/Model/TooltipBlockDefinition.cs): an object { MaxWidth, Blocks },
// the block array alone, or a string (one Line); members are matched without case like Json.NET.
import { useLayoutEffect, useRef, useState, type CSSProperties, type ReactNode } from 'react';
import { GAME_FONTS } from '../layout';
import type { NodeId } from '../model/document';
import { evaluateExpression, evaluateTemplateText, type ExternalFunctions } from './evaluate';
import { objectIndex, itemDisplayName } from './gameData';
import type { GameArt } from './gameArt';
import { chipTokens, fontStyle, renderText } from './text';

const Padding = 16;
const CursorOffset = 32;
const EdgeNudge = 16;
const ViewportSlack = 64;

interface Block { type: string; text?: string; color?: string; sprite?: string; item?: string; amount?: string; when?: string }

function member(o: Record<string, unknown>, name: string): string | undefined {
  const key = Object.keys(o).find(k => k.toLowerCase() === name.toLowerCase());
  const v = key !== undefined ? o[key] : undefined;
  return typeof v === 'string' ? v : typeof v === 'number' || typeof v === 'boolean' ? String(v) : undefined;
}

const isObject = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v);

/** The definition's MaxWidth and blocks (TooltipConverter's three shapes). */
function normalize(definition: unknown): { maxWidth?: string; blocks: Block[] } {
  if (typeof definition === 'string') {
    return { blocks: [{ type: 'line', text: definition }] };
  }
  const list = Array.isArray(definition) ? definition : isObject(definition) ? definition[Object.keys(definition).find(k => k.toLowerCase() === 'blocks') ?? ''] : undefined;
  const blocks = (Array.isArray(list) ? list : []).filter(isObject).map(b => ({
    type: (member(b, 'Type') ?? '').trim().toLowerCase(),
    text: member(b, 'Text'), color: member(b, 'Color'), sprite: member(b, 'Sprite'), item: member(b, 'Item'),
    amount: member(b, 'Amount'), when: member(b, 'When')
  }));
  const maxWidth = isObject(definition) ? member(definition, 'MaxWidth') : undefined;
  return maxWidth !== undefined ? { maxWidth, blocks } : { blocks };
}

/** The value as the framework reads a bool: false for false / 0 / no / empty. */
const truthy = (v: string): boolean => !['', 'false', '0', 'no'].includes(v.trim().toLowerCase());

export function ItemSprite({ id, art, size }: { id: string; art: GameArt | undefined; size: number | string }): ReactNode {
  const index = objectIndex(id);
  const o = art?.objects;
  if (index === undefined || !o || index >= o.columns * o.rows) {
    return null;
  }
  const col = index % o.columns;
  const row = Math.floor(index / o.columns);
  const style: CSSProperties = {
    width: size,
    height: size,
    backgroundImage: `url(${o.url})`,
    backgroundSize: `${o.columns * 100}% ${o.rows * 100}%`,
    backgroundPosition: `${o.columns > 1 ? (col / (o.columns - 1)) * 100 : 0}% ${o.rows > 1 ? (row / (o.rows - 1)) * 100 : 0}%`
  };
  return <span className="pv-item-sprite" style={style} />;
}

export interface TooltipBoxProps {
  /** A TooltipDefinition (resolveTooltip), or undefined to use the inline title / text. */
  definition: unknown;
  title?: string;
  text?: string;
  /** Cursor in screen (UI) pixels; absent: the tooltip is centred on the screen (a tooltip tab's preview). */
  cursor?: { x: number; y: number };
  screen: { width: number; height: number };
  state: Record<string, string>;
  functions?: ExternalFunctions;
  i18n?: Record<string, string>;
  art?: GameArt;
  /** A tooltip tab's preview: each block's node, in block order; its blocks then select (data-node) and show outlines. */
  blocks?: NodeId[];
  selection?: NodeId | null;
  hovered?: NodeId | null;
  /** Show blocks whose When is false (marked hidden). */
  showHidden?: boolean;
}

export function TooltipBox(props: TooltipBoxProps): ReactNode {
  const { definition, title, text, cursor, screen, state, functions, i18n, art, blocks: nodes, selection, hovered, showHidden } = props;
  const ref = useRef<HTMLDivElement>(null);
  const [at, setAt] = useState(cursor ? { x: cursor.x + CursorOffset, y: cursor.y + CursorOffset } : { x: 0, y: 0 });

  const { maxWidth, blocks } = definition !== undefined && definition !== null
    ? normalize(definition)
    : { maxWidth: undefined, blocks: [...(title ? [{ type: 'title', text: title }] : []), ...(text ? [{ type: 'line', text }] : [])] as Block[] };
  const wrapLimit = Number(maxWidth !== undefined ? evaluateExpression(maxWidth, state, functions) : undefined);
  let wrap = Math.max(1, screen.width - (2 * Padding) - ViewportSlack);
  if (wrapLimit > 0) wrap = Math.min(wrap, wrapLimit);

  const textOf = (raw: string | undefined): string => chipTokens(evaluateTemplateText(raw ?? '', state, functions));
  const lineHeight = GAME_FONTS.small.lineSpacing;
  const rows: ReactNode[] = [];
  blocks.forEach((b, i) => {
    let hidden = false;
    if (b.when !== undefined) {
      const shown = evaluateExpression(b.when, state, functions);
      hidden = shown !== undefined && !truthy(shown);
      if (hidden && !showHidden) return;
    }
    const before = rows.length;
    const color = b.color !== undefined ? textOf(b.color).trim() : '';
    const colorStyle: CSSProperties = color && typeof CSS !== 'undefined' && CSS.supports('color', color) ? { color } : {};
    switch (b.type) {
      case 'title':
      case 'line': {
        const content = textOf(b.text);
        if (content) {
          rows.push(<div key={i} className="pv-tip-text" style={{ ...fontStyle(b.type === 'title' ? 'dialogue' : 'small'), maxWidth: wrap, ...colorStyle }}>{renderText(content, i18n)}</div>);
        }
        break;
      }
      case 'item': {
        const id = textOf(b.item).trim();
        rows.push(
          <div key={i} className="pv-tip-icon-row" style={fontStyle()}>
            <ItemSprite id={id} art={art} size={lineHeight} />
            {objectIndex(id) === undefined || !art?.objects ? <span className="pv-tip-icon" style={{ width: lineHeight, height: lineHeight }}>▣</span> : null}
            <span>{renderText(itemDisplayName(id), i18n)}</span>
          </div>
        );
        break;
      }
      case 'money': {
        const amount = Number(b.amount !== undefined ? evaluateExpression(b.amount, state, functions) : undefined);
        rows.push(
          <div key={i} className="pv-tip-icon-row" style={fontStyle()}>
            <span className="pv-tip-coin" style={{ width: lineHeight, height: lineHeight }} />
            <span>{Number.isFinite(amount) ? String(Math.trunc(amount)) : renderText(chipTokens(b.amount ?? ''), i18n)}</span>
          </div>
        );
        break;
      }
      case 'icon': {
        const sprite = textOf(b.sprite).trim();
        const item = /^item:(.+)$/i.exec(sprite);
        rows.push(
          <div key={i} className="pv-tip-icon-row">
            {item && objectIndex(item[1]!) !== undefined && art?.objects
              ? <ItemSprite id={item[1]!} art={art} size={64} />
              : <span className="pv-tip-icon" title={sprite} style={{ width: 64, height: 64 }}>▣</span>}
          </div>
        );
        break;
      }
      case 'divider':
        rows.push(<div key={i} className="pv-tip-divider" />);
        break;
      default:
        break;
    }
    // a tooltip tab's block: its row wrapped to select its node
    const node = nodes?.[i];
    if (node !== undefined && rows.length > before) {
      const classes = ['pv-tip-block', ...(hidden ? ['pv-hidden'] : []), ...(node === selection ? ['pv-selected'] : []), ...(node === hovered ? ['pv-hover'] : [])];
      rows[before] = <div key={i} data-node={node} className={classes.join(' ')}>{rows[before]}</div>;
    }
  });

  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    // TooltipRenderer.Place
    const w = el.offsetWidth;
    const h = el.offsetHeight;
    if (!cursor) {
      const centre = { x: Math.max(0, Math.round((screen.width - w) / 2)), y: Math.max(0, Math.round((screen.height - h) / 2)) };
      setAt(prev => (prev.x === centre.x && prev.y === centre.y ? prev : centre));
      return;
    }
    let x = cursor.x + CursorOffset;
    let y = cursor.y + CursorOffset;
    if (x + w > screen.width) {
      x = screen.width - w;
      y += EdgeNudge;
    }
    if (y + h > screen.height) {
      x += EdgeNudge;
      if (x + w > screen.width) x = screen.width - w;
      y = screen.height - h;
    }
    setAt(prev => (prev.x === Math.max(0, x) && prev.y === Math.max(0, y) ? prev : { x: Math.max(0, x), y: Math.max(0, y) }));
  });

  if (rows.length === 0) {
    return null;
  }
  return (
    <div ref={ref} className={nodes ? 'pv-tooltip pv-tooltip-tab' : 'pv-tooltip'} style={{ left: at.x, top: at.y }}>
      {rows}
    </div>
  );
}
