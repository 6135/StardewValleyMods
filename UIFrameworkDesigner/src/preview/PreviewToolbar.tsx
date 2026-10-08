// The schematic preview's toolbar (PreviewPane.tsx): screen, UI scale, full height, zoom, rows, hidden elements and theme;
// and the notice listing the unknown values the layout depends on.
import type { ReactNode } from 'react';
import { uiScales, windowSizes, type PreviewSettings, type ScreenKey } from './previewSettings';

export type Skin = 'default' | 'dark' | 'art';

/** Whether the browser can pick a folder (Chromium); the game-art option is hidden elsewhere. */
const gameArtSupported = (): boolean => typeof window !== 'undefined' && 'showDirectoryPicker' in window;

export interface PreviewToolbarProps {
  settings: PreviewSettings;
  change(patch: Partial<Omit<PreviewSettings, 'view'>>): void;
  /** The UI viewport the menu is laid out on. */
  screenW: number;
  screenH: number;
  /** A menu or template is shown (not a tooltip): the layout-only options apply. */
  hasDoc: boolean;
  zoom: number;
  onFit(): void;
  onActualSize(): void;
  skin: Skin;
  onSkin(skin: Skin): void;
  /** The game-art skin is on: another Content folder can be picked. */
  artOn: boolean;
  onPickContent(): void;
}

export function PreviewToolbar(props: PreviewToolbarProps): ReactNode {
  const { settings, change, screenW, screenH, hasDoc, zoom, onFit, onActualSize, skin, onSkin, artOn, onPickContent } = props;
  const { screen, custom, uiScale, fullHeight, repeatCount, showHidden } = settings;
  const scaleTitle = "The game's UI scale option: menus are laid out on the window size ÷ the UI scale (" + String(screenW) + '×' + String(screenH) + ' UI pixels)';
  return (
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
      <label title={scaleTitle}>UI{' '}
        <select value={uiScale} onChange={e => change({ uiScale: Number(e.target.value) })}>
          {uiScales.map(p => <option key={p} value={p}>{p}%</option>)}
        </select>
      </label>
      <label title="Lay the window out as if the screen were tall enough for all of it; a line marks where the screen ends and the window would scroll">
        <input type="checkbox" checked={fullHeight} disabled={!hasDoc} onChange={e => change({ fullHeight: e.target.checked })} /> Full height
      </label>
      <span className="pv-zoom" role="group" aria-label="Zoom">
        <button type="button" onClick={onFit} title="Fit the screen and the content (double-click empty canvas)">Fit</button>
        <button type="button" onClick={onActualSize} title="Actual size">100%</button>
        <output title="Wheel or pinch to zoom; drag empty canvas, middle button or space + drag to pan">{Math.round(zoom * 100)}%</output>
      </span>
      <label title="Rows rendered by Repeat, List and DataGrid">Rows{' '}
        <input type="number" min={0} max={50} value={repeatCount} className="pv-small" disabled={!hasDoc}
          onChange={e => change({ repeatCount: Math.min(50, Math.max(0, Number(e.target.value) || 0)) })} />
      </label>
      <label><input type="checkbox" checked={showHidden} onChange={e => change({ showHidden: e.target.checked })} /> Show hidden</label>
      <label>Theme{' '}
        <select value={skin} onChange={e => onSkin(e.target.value as Skin)}>
          <option value="default">Default</option>
          <option value="dark">Dark</option>
          {gameArtSupported() ? <option value="art" title="Pick your unpacked Content folder; read locally, never uploaded">Game art…</option> : null}
        </select>
      </label>
      {artOn ? <button type="button" onClick={onPickContent} title="Pick another Content folder">Content…</button> : null}
    </div>
  );
}

/** The unknown values the layout depends on, with a way to set them in the Preview state panel. */
export function UnknownNotice({ unknown, onShowState }: { unknown: string[]; onShowState: ((names: string[]) => void) | undefined }): ReactNode {
  if (unknown.length === 0) {
    return null;
  }
  return (
    <div className="pv-notice" role="status">
      <span className="pv-notice-text" title={unknown.join(', ')}>
        Layout depends on {unknown.length} unknown value{unknown.length === 1 ? '' : 's'}: <code>{unknown.join(', ')}</code>
      </span>
      {onShowState ? <button type="button" onClick={() => onShowState(unknown)}>Set them in Preview state</button> : null}
    </div>
  );
}
