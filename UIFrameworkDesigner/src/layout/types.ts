// Public shapes of the layout module (architecture.md §7.1). Pure TypeScript: no React, no DOM.
import type { NodeId } from '../model/document';

export interface Rect { x: number; y: number; width: number; height: number }

/** The game fonts the framework draws with (UIFont): Game1.smallFont, Game1.dialogueFont, Game1.tinyFont. */
export type FontName = 'small' | 'dialogue' | 'tiny';

export interface TextMetrics {
  /** Width of the widest line, at `scale`. */
  width: number;
  /** Line count × line height, at `scale` (one line height for empty text, as GameTextMeasurer.Measure). */
  height: number;
  /** The text broken into lines (wrapped when `wrapWidth` is given, else split on line breaks). */
  lines: string[];
}

/**
 * Measures text like the framework's GameTextMeasurer. `wrapWidth` is in final (scaled) pixels; the measurer wraps
 * at `wrapWidth / scale` like Label.Reflow. Chip marks (U+E000–U+E00F, see CHIP_I18N) take no
 * width and stay in the returned lines.
 */
export type TextMeasurer = (text: string, font: FontName, scale: number, wrapWidth?: number) => TextMetrics;

export interface LayoutOptions {
  /** UI viewport size (Game1.uiViewport) in UI pixels. */
  screenWidth: number;
  screenHeight: number;
  /** How many rows Repeat / List / DataGrid render from their row template. */
  repeatCount: number;
  measureText: TextMeasurer;
  /**
   * Evaluates a non-literal field value (a bare expression or a `${…}` template) with the row locals of its place
   * (row.x, the As alias, index); undefined when it cannot.
   */
  evaluate?: (expr: string, locals: Record<string, string>) => string | undefined;
  /** Sample rows of the collection sources C# provides, by source key (sourceKey; matched without case). */
  sampleRows?: Record<string, unknown[]>;
  /** The case each Switch node shows (its children's `Case` value); the first page when absent. */
  switchCases?: Record<NodeId, string>;
  /** Designer addition: lay out elements hidden by Visible / If / Condition as if visible (their boxes get `hidden`). */
  showHidden?: boolean;
}

export interface LayoutResult {
  /** The window (UIMenu.Bounds). */
  window: Rect;
  /** Inside the chrome and padding (UIMenu.ContentBounds). */
  content: Rect;
  /** The title scroll (DrawBox) or title line (no box); absent without a title. */
  title?: Rect;
  /** The title as it is drawn (with chip marks, see CHIP_I18N). */
  titleText?: string;
  /** Whether the window draws its box (menu DrawBox). */
  drawBox: boolean;
  /** Every placed element, parents before children (draw order). */
  boxes: LayoutBox[];
  /**
   * The layout-affecting expressions (menu Width / Height / X / Y / Padding, element Row, Column, spans, Cell, size,
   * margins, Visible / If / Condition, Switch) the preview could not evaluate, in build order (repeats possible).
   */
  unresolved: string[];
}

/** What a box draws, for the schematic renderer. Text carries chip marks (see CHIP_I18N). */
export interface BoxDetail {
  text?: string;
  /** Lines as laid out (wrapped labels). */
  lines?: string[];
  font?: FontName;
  scale?: number;
  textAlign?: 'start' | 'center' | 'end' | 'stretch';
  /** CSS color from the element's Color field (literal only). */
  color?: string;
  /** Panel / Button DrawBox, Spacer Line. */
  drawBox?: boolean;
  line?: boolean;
  /** ScrollView / List / DataGrid reserve a scrollbar column on the right of this width. */
  scrollbar?: number;
  /** Image / ItemImage / Button icon reference. */
  sprite?: string;
  placeholder?: string;
  value?: string;
  checked?: boolean;
  /** Slider position 0..1. */
  fraction?: number;
  enabled?: boolean;
}

export interface LayoutBox {
  /** The document node the box shows (for elements the framework creates itself, the node that created them). */
  nodeId: NodeId;
  rect: Rect;
  /** Element type (Stack, Label …), or a part name for framework-made elements ("DataGrid.header", "Form.label" …). */
  kind: string;
  /** Short text for the box: the element's text, sprite name, template name … (with chip marks). */
  label?: string;
  /** Repeat / List / DataGrid row index, 0 otherwise. */
  instance: number;
  /** The visible part when an ancestor (ScrollView viewport, List / DataGrid rows) clips the box. */
  clipped?: Rect;
  /** Nesting depth (0 = a direct child of the menu). */
  depth: number;
  /** Laid out only because LayoutOptions.showHidden is set: in game Visible / If / Condition hide it. */
  hidden?: boolean;
  /** For boxes from an expanded template body or a raw row template: the menu-tree node they belong to. */
  ownerId?: NodeId;
  /** True for elements the framework creates (rows, headers, form fields) rather than a document element. */
  synthetic?: boolean;
  /** Its position or size depends on an expression the preview could not evaluate (LayoutResult.unresolved). */
  unresolved?: boolean;
  detail?: BoxDetail;
}

/**
 * Chip delimiters in laid-out text: a start mark (its kind), the chip text, then CHIP_END. Measurers give the marks no
 * width (every U+E000–U+E00F character is a mark).
 */
export const CHIP_I18N = '';
export const CHIP_END = '';
export const CHIP_TOKEN = '';
export const CHIP_EXPR = '';
/** A chip is drawn (preview/text.tsx) and measured (textMetrics.ts) at this fraction of the font size … */
export const CHIP_FONT_SCALE = 0.82;
/** … with this much padding on each side, in UI pixels; a chip is never broken across lines. */
export const CHIP_PADDING = 3;
/** Matches every chip mark. */
export const CHIP_MARKS = /[-]/g;
