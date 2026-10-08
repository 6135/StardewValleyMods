// The schematic preview's settings (PreviewPane.tsx): the game window, UI scale, scroll offsets, canvas view and rows.
import type { CanvasView } from './panZoom';

/** Game window sizes; the menu is laid out on the UI viewport, the window ÷ the UI scale. */
export const windowSizes = {
  '1280×720': [1280, 720], '1366×768': [1366, 768], '1600×900': [1600, 900], '1920×1080': [1920, 1080], '2560×1440': [2560, 1440]
} as const;
export type ScreenKey = keyof typeof windowSizes | 'custom';
/** The game's UI scale option (Game1.options.uiScale), in percent. */
export const uiScales = [75, 100, 125, 150] as const;

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
export function normalizeSettings(saved: PreviewSettings): PreviewSettings {
  const s = { ...defaultPreviewSettings, ...saved };
  if (s.screen !== 'custom' && !(s.screen in windowSizes)) s.screen = defaultPreviewSettings.screen;
  if (!(uiScales as readonly number[]).includes(s.uiScale)) s.uiScale = defaultPreviewSettings.uiScale;
  return s;
}
