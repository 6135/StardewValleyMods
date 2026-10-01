// Schematic skin colors. Source: StardewUIFramework/assets/themes.json and the vanilla fallbacks in
// StardewUIFramework/Rendering/Theme.cs (the "default" theme in themes.json is empty, so every color is Theme.cs's:
// TextColor = Game1.textColor (34, 17, 34), HoverColor = Color.Wheat, DisabledTextColor = TextColor × 0.5,
// BoxTint / ScrollbarTint = white). Copied rather than imported because the JSON lives outside the Vite root.
// The box fills stand in for the vanilla art (Game1.menuTexture, the mouseCursors button and LooseSprites/textBox),
// which cannot be bundled.

export interface PreviewTheme {
  textColor: string;
  disabledTextColor: string;
  hoverColor: string;
  /** Multiplied into box fills (themes.json BoxTint). */
  boxTint: string;
  scrollbarTint: string;
  /** Solid box fill (themes.json BoxFill), null = the textured look. */
  boxFill: string | null;
  borderColor: string | null;
}

export const previewThemes: Record<'default' | 'dark', PreviewTheme> = {
  default: {
    textColor: 'rgb(34, 17, 34)',
    disabledTextColor: 'rgba(34, 17, 34, 0.5)',
    hoverColor: '#F5DEB3',
    boxTint: '#FFFFFF',
    scrollbarTint: '#FFFFFF',
    boxFill: null,
    borderColor: null
  },
  dark: {
    textColor: '#F0E6D2',
    disabledTextColor: '#8C8578',
    hoverColor: '#C8B890',
    boxTint: '#5C5C5C',
    scrollbarTint: '#9A9A9A',
    boxFill: null,
    borderColor: null
  }
};

/** Approximate base colors of the vanilla box art (before BoxTint). */
const art = {
  window: [249, 212, 152],
  windowBorder: [176, 95, 26],
  panel: [242, 196, 125],
  button: [233, 176, 103],
  textBox: [255, 243, 214],
  scrollbar: [214, 145, 71]
} as const;

function hexRgb(hex: string): [number, number, number] {
  const n = parseInt(hex.replace('#', '').slice(0, 6), 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}

function tint(base: readonly number[], by: string): string {
  const t = hexRgb(by);
  return `rgb(${Math.round(base[0]! * t[0] / 255)}, ${Math.round(base[1]! * t[1] / 255)}, ${Math.round(base[2]! * t[2] / 255)})`;
}

/** CSS custom properties for the preview stage. */
export function themeVariables(theme: PreviewTheme): Record<string, string> {
  return {
    '--pv-text': theme.textColor,
    '--pv-text-disabled': theme.disabledTextColor,
    '--pv-hover': theme.hoverColor,
    '--pv-window': theme.boxFill ?? tint(art.window, theme.boxTint),
    '--pv-window-border': theme.borderColor ?? tint(art.windowBorder, theme.boxTint),
    '--pv-panel': theme.boxFill ?? tint(art.panel, theme.boxTint),
    '--pv-button': theme.boxFill ?? tint(art.button, theme.boxTint),
    '--pv-textbox': theme.boxFill ?? tint(art.textBox, theme.boxTint),
    '--pv-scrollbar': tint(art.scrollbar, theme.scrollbarTint)
  };
}
