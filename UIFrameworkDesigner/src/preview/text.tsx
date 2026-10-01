// Text drawing shared by the preview's boxes and tooltips: chip marks as styled spans, the calibrated game fonts.
import type { CSSProperties, ReactNode } from 'react';
import { CHIP_END, CHIP_EXPR, CHIP_FONT_SCALE, CHIP_I18N, CHIP_PADDING, CHIP_TOKEN, calibratedCssFont, chipTokens, GAME_FONTS, type FontName } from '../layout';
import { translateChips } from '../layout/textMetrics';

/** CP tokens as chips, the way the layout marks them (layout/build.ts). */
export { chipTokens };

/** A chip's size, as the layout measures it (textMetrics.ts); a chip wider than its line ellipsizes (PreviewPane.css). */
const chipStyle: CSSProperties = { fontSize: `${CHIP_FONT_SCALE}em`, padding: `0 ${CHIP_PADDING}px` };

/** Text with chip marks as spans: i18n keys (translated when `i18n` has them), CP tokens and unevaluated expressions get their own chip style. */
export function renderText(source: string, i18n?: Record<string, string>): ReactNode[] {
  const text = translateChips(source, i18n);
  const out: ReactNode[] = [];
  let buffer = '';
  let chip: string | null = null;
  let key = 0;
  const flush = (): void => {
    if (buffer) {
      out.push(chip ? <span key={key++} className={`pv-chip pv-chip-${chip}`} style={chipStyle} title={buffer}>{buffer}</span> : buffer);
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

export function fontStyle(font: FontName = 'small', scale = 1): CSSProperties {
  return { font: calibratedCssFont(font, scale), lineHeight: `${GAME_FONTS[font].lineSpacing * scale}px` };
}
