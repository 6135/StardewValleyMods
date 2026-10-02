// Layout module entry point (architecture.md §7.1): a TypeScript port of the framework's measure / arrange pass.
export type { BoxDetail, FontName, LayoutBox, LayoutOptions, LayoutResult, Rect, ScrollbarParts, Scroller, TextMeasurer, TextMetrics } from './types';
export { CHIP_END, CHIP_EXPR, CHIP_FONT_SCALE, CHIP_I18N, CHIP_MARKS, CHIP_PADDING, CHIP_TOKEN, MENU_SCROLL_KEY } from './types';
export { scrollOffsetAt } from './elements';
export { layoutDocument } from './menu';
export { approximateTextMeasurer, calibratedCssFont, canvasTextMeasurer, CALIBRATION_SAMPLE, GAME_FONTS } from './textMetrics';
export { hasTemplate } from './values';
export { chipTokens, flattenLocals, sourceKey } from './build';
