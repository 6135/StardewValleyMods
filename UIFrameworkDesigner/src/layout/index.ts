// Layout module entry point (architecture.md §7.1): a TypeScript port of the framework's measure / arrange pass.
export type { BoxDetail, FontName, LayoutBox, LayoutOptions, LayoutResult, Rect, TextMeasurer, TextMetrics } from './types';
export { CHIP_END, CHIP_EXPR, CHIP_I18N, CHIP_MARKS, CHIP_TOKEN } from './types';
export { layoutDocument } from './menu';
export { approximateTextMeasurer, calibratedCssFont, canvasTextMeasurer, CALIBRATION_SAMPLE, GAME_FONTS } from './textMetrics';
export { hasTemplate } from './values';
export { chipTokens } from './build';
