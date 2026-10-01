// Schematic preview (architecture.md §7): the layout port's boxes drawn as absolutely positioned DOM elements.
import { useEffect, useMemo, useRef, useState, type CSSProperties, type MouseEvent, type ReactNode } from 'react';
import type { DesignerDocument, NodeId } from '../model/document';
import {
  CHIP_END, CHIP_EXPR, CHIP_I18N, CHIP_TOKEN, calibratedCssFont, canvasTextMeasurer, GAME_FONTS, layoutDocument,
  type FontName, type LayoutBox
} from '../layout';
import { createEvaluator } from './evaluate';
import { previewThemes, themeVariables } from './theme';
import './PreviewPane.css';

export interface PreviewPaneProps {
  doc: DesignerDocument;
  selection: NodeId | null;
  onSelect(id: NodeId | null): void;
  /** The case each Switch shows (from the inspector's case picker); the first page otherwise. */
  switchCases?: Record<NodeId, string>;
}

const screens = { '1280×720': [1280, 720], '1920×1080': [1920, 1080] } as const;
type ScreenKey = keyof typeof screens | 'custom';
const zooms = [0.25, 0.5, 0.75, 1, 1.5, 2];

/** Text with chip marks as spans: i18n keys, CP tokens and unevaluated expressions get their own chip style. */
function renderText(text: string): ReactNode[] {
  const out: ReactNode[] = [];
  let buffer = '';
  let chip: string | null = null;
  let key = 0;
  const flush = (): void => {
    if (buffer) {
      out.push(chip ? <span key={key++} className={`pv-chip pv-chip-${chip}`}>{buffer}</span> : buffer);
    }
    buffer = '';
  };
  for (const c of text) {
    if (c === CHIP_I18N || c === CHIP_TOKEN || c === CHIP_EXPR) {
      flush();
      chip = c === CHIP_I18N ? 'i18n' : c === CHIP_TOKEN ? 'token' : 'expr';
    } else if (c === CHIP_END) {
      flush();
      chip = null;
    } else if (c >= '' && c <= '') {
      // other marks: ignored
    } else {
      buffer += c;
    }
  }
  flush();
  return out;
}

function fontStyle(font: FontName = 'small', scale = 1): CSSProperties {
  return { font: calibratedCssFont(font, scale), lineHeight: `${GAME_FONTS[font].lineSpacing * scale}px` };
}

function TextLines({ box }: { box: LayoutBox }): ReactNode {
  const d = box.detail ?? {};
  const lines = d.lines && d.lines.length > 0 ? d.lines : [d.text ?? box.label ?? ''];
  const align = d.textAlign === 'center' ? 'center' : d.textAlign === 'end' ? 'right' : 'left';
  return (
    <div className="pv-text" style={{ ...fontStyle(d.font, d.scale), textAlign: align, ...(d.color ? { color: d.color } : {}) }}>
      {lines.map((line, i) => <div key={i} className="pv-line">{renderText(line)}</div>)}
    </div>
  );
}

const containerKinds = new Set(['Stack', 'Grid', 'Canvas', 'Switch', 'Repeat', 'Outlet', 'Slot', 'Template', 'ScrollView', 'List', 'DataGrid', 'Form', 'Panel']);

function BoxContent({ box }: { box: LayoutBox }): ReactNode {
  const d = box.detail ?? {};
  const label = box.label ?? '';
  switch (box.kind) {
    case 'Label':
    case 'DataGrid.header':
    case 'DataGrid.cell':
    case 'Form.label':
    case 'Form.section':
      return <TextLines box={box} />;
    case 'Button':
      return (
        <div className={`pv-button${d.drawBox === false ? ' pv-flat' : ''}`} style={fontStyle(d.font)}>
          {d.sprite ? <span className="pv-icon" title={d.sprite}>▣</span> : null}
          <span className="pv-nowrap">{renderText(label)}</span>
        </div>
      );
    case 'Checkbox':
      return (
        <div className="pv-checkbox" style={fontStyle()}>
          <span className={`pv-check${d.checked ? ' pv-checked' : ''}`} />
          <span className="pv-nowrap">{renderText(label)}</span>
        </div>
      );
    case 'TextInput':
    case 'NumberInput':
      return (
        <div className="pv-textbox" style={fontStyle()}>
          {d.value ? renderText(d.value) : <span className="pv-placeholder">{renderText(d.placeholder ?? '')}</span>}
        </div>
      );
    case 'Dropdown':
      return (
        <div className="pv-dropdown" style={fontStyle()}>
          <span className="pv-nowrap">{renderText(d.value ?? label)}</span>
          <span className="pv-caret">▼</span>
        </div>
      );
    case 'Slider':
      return (
        <div className="pv-slider">
          <span className="pv-knob" style={{ left: `calc((100% - 40px) * ${d.fraction ?? 0})` }} />
        </div>
      );
    case 'Image':
    case 'ItemImage':
      return <div className="pv-image" title={d.sprite}><span>{renderText(d.sprite ?? box.kind)}</span></div>;
    case 'Spacer':
      return d.line ? <div className="pv-spacer-line" /> : null;
    case 'Custom':
    case 'Composite':
      return <div className="pv-unknown"><span>⟨{renderText(label)}⟩</span></div>;
    default:
      return null;
  }
}

export function PreviewPane({ doc, selection, onSelect, switchCases }: PreviewPaneProps): ReactNode {
  const [screen, setScreen] = useState<ScreenKey>('1280×720');
  const [custom, setCustom] = useState({ width: 1600, height: 900 });
  const [zoom, setZoom] = useState<number | 'fit'>('fit');
  const [repeatCount, setRepeatCount] = useState(3);
  const [showHidden, setShowHidden] = useState(false);
  const [skin, setSkin] = useState<'default' | 'dark'>('default');
  const [hovered, setHovered] = useState<NodeId | null>(null);
  const [paneSize, setPaneSize] = useState({ width: 800, height: 600 });
  const viewRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const el = viewRef.current;
    if (!el || typeof ResizeObserver === 'undefined') {
      return undefined;
    }
    const observer = new ResizeObserver(entries => {
      const r = entries[0]?.contentRect;
      if (r) {
        setPaneSize({ width: r.width, height: r.height });
      }
    });
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  const [screenW, screenH] = screen === 'custom' ? [custom.width, custom.height] : screens[screen];
  const measureText = useMemo(() => canvasTextMeasurer(), []);
  const layout = useMemo(() => layoutDocument(doc, {
    screenWidth: screenW,
    screenHeight: screenH,
    repeatCount,
    measureText,
    evaluate: createEvaluator(doc),
    showHidden,
    ...(switchCases ? { switchCases } : {})
  }), [doc, screenW, screenH, repeatCount, measureText, showHidden, switchCases]);

  const fit = Math.max(0.05, Math.min((paneSize.width - 16) / screenW, (paneSize.height - 16) / screenH));
  const scale = zoom === 'fit' ? fit : zoom;
  const pick = (box: LayoutBox, alt: boolean): NodeId => (alt ? box.nodeId : box.ownerId ?? box.nodeId);

  const boxAt = (target: EventTarget): LayoutBox | undefined => {
    const el = (target as HTMLElement).closest?.('[data-box]');
    const index = el ? Number(el.getAttribute('data-box')) : -1;
    return index >= 0 ? layout.boxes[index] : undefined;
  };

  const onClick = (e: MouseEvent): void => {
    const box = boxAt(e.target);
    onSelect(box ? pick(box, e.altKey) : null);
  };

  const onMove = (e: MouseEvent): void => {
    const box = boxAt(e.target);
    const id = box ? pick(box, e.altKey) : null;
    if (id !== hovered) {
      setHovered(id);
    }
  };

  const w = layout.window;
  return (
    <div className="pv-pane">
      <div className="pv-toolbar">
        <label>Screen{' '}
          <select value={screen} onChange={e => setScreen(e.target.value as ScreenKey)}>
            {Object.keys(screens).map(k => <option key={k} value={k}>{k}</option>)}
            <option value="custom">Custom</option>
          </select>
        </label>
        {screen === 'custom' ? (
          <span className="pv-custom">
            <input type="number" min={320} max={7680} value={custom.width} aria-label="Screen width"
              onChange={e => setCustom({ ...custom, width: Math.max(320, Number(e.target.value) || 320) })} />
            ×
            <input type="number" min={240} max={4320} value={custom.height} aria-label="Screen height"
              onChange={e => setCustom({ ...custom, height: Math.max(240, Number(e.target.value) || 240) })} />
          </span>
        ) : null}
        <label>Zoom{' '}
          <select value={String(zoom)} onChange={e => setZoom(e.target.value === 'fit' ? 'fit' : Number(e.target.value))}>
            <option value="fit">Fit ({Math.round(fit * 100)}%)</option>
            {zooms.map(z => <option key={z} value={z}>{z * 100}%</option>)}
          </select>
        </label>
        <label title="Rows rendered by Repeat, List and DataGrid">Rows{' '}
          <input type="number" min={0} max={50} value={repeatCount} className="pv-small"
            onChange={e => setRepeatCount(Math.min(50, Math.max(0, Number(e.target.value) || 0)))} />
        </label>
        <label><input type="checkbox" checked={showHidden} onChange={e => setShowHidden(e.target.checked)} /> Show hidden</label>
        <label>Theme{' '}
          <select value={skin} onChange={e => setSkin(e.target.value as 'default' | 'dark')}>
            <option value="default">Default</option>
            <option value="dark">Dark</option>
          </select>
        </label>
      </div>
      <div className="pv-view" ref={viewRef}>
        <div className="pv-stage-size" style={{ width: screenW * scale, height: screenH * scale }}>
          <div
            className="pv-screen"
            style={{ width: screenW, height: screenH, transform: `scale(${scale})`, ...themeVariables(previewThemes[skin]) } as CSSProperties}
            onClick={onClick}
            onMouseMove={onMove}
            onMouseLeave={() => setHovered(null)}
          >
            <div className={layout.drawBox ? 'pv-window' : 'pv-window pv-window-bare'} style={{ left: w.x, top: w.y, width: w.width, height: w.height }} />
            {layout.title && layout.titleText ? (
              <div className={layout.drawBox ? 'pv-title pv-title-scroll' : 'pv-title'}
                style={{ left: layout.title.x, top: layout.title.y, width: layout.title.width, height: layout.title.height, ...fontStyle('dialogue') }}>
                <span className="pv-nowrap">{renderText(layout.titleText)}</span>
              </div>
            ) : null}
            {layout.boxes.map((box, i) => {
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
              // a node's own boxes (every instance) show its selection / hover; framework-made parts do not
              if (!box.synthetic && box.nodeId === selection) classes.push('pv-selected');
              if (!box.synthetic && box.nodeId === hovered) classes.push('pv-hover');
              const style: CSSProperties = { left: r.x, top: r.y, width: r.width, height: r.height };
              if (box.clipped) {
                const c = box.clipped;
                style.clipPath = `inset(${c.y - r.y}px ${r.x + r.width - (c.x + c.width)}px ${r.y + r.height - (c.y + c.height)}px ${c.x - r.x}px)`;
              }
              return (
                <div key={i} data-box={i} className={classes.join(' ')} style={style} title={`${box.kind}${box.instance > 0 ? ` #${box.instance}` : ''}`}>
                  <BoxContent box={box} />
                  {box.detail?.scrollbar ? <div className="pv-scrollbar" style={{ width: box.detail.scrollbar }} /> : null}
                  {box.kind === 'Template' ? <span className="pv-tag">{renderText(box.label ?? '')}</span> : null}
                </div>
              );
            })}
          </div>
        </div>
      </div>
    </div>
  );
}

export default PreviewPane;
