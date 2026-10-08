// The text and control leaves' fixed sizes over the injected text measurer (elements.ts).
import { LElement, type Vec } from './elementBase';
import { toInt } from './engine';
import { CHIP_MARKS, type FontName, type TextMeasurer } from './types';

// ---------------------------------------------------------------------------------------------------------------------
//  Text leaves
// ---------------------------------------------------------------------------------------------------------------------

/** DrawHelper.MinFitScale (Rendering/Draw.cs): a squeezed single line shrinks to 70 % before truncating. */
const MIN_FIT_SCALE = 0.7;

function stripMarks(text: string): string {
  return text.replace(CHIP_MARKS, '');
}

/** DrawHelper.FitTextMinWidth: the narrower of the whole text at 70 % and its first character + "..." at 70 %. */
function fitTextMinWidth(measure: TextMeasurer, text: string, font: FontName, scale: number): number {
  const plain = stripMarks(text);
  if (plain.length === 0) {
    return 0;
  }
  const smallest = MIN_FIT_SCALE * scale;
  const whole = measure(text, font, smallest).width;
  const truncated = measure(plain.substring(0, 1) + '...', font, smallest).width;
  return Math.ceil(Math.min(whole, truncated));
}

/** GameTextMeasurer.LongestWord: the widest piece between spaces / line breaks. */
function longestWord(measure: TextMeasurer, text: string, font: FontName, scale: number): number {
  let widest = 0;
  for (const word of text.split(/[ \n\r]/)) {
    if (stripMarks(word).length > 0) {
      widest = Math.max(widest, measure(word, font, scale).width);
    }
  }
  return widest;
}

/** Components/Label.cs (plain text; rich text is measured as its plain text). */
export class LLabel extends LElement {
  text = '';
  font: FontName = 'small';
  wrap = false;
  shrink = false;
  private scaleValue = 1;
  private wrapWidth = -1;
  private lines: string[] = [];
  private laidOut: Vec = { x: 0, y: 0 };

  get scale(): number { return this.scaleValue; }
  set scale(v: number) { this.scaleValue = Math.max(0.05, v); }

  protected measureCore(available: Vec): Vec {
    this.wrapWidth = this.wrap ? toInt(Math.floor(available.x)) : -1;
    return this.reflow();
  }

  private reflow(): Vec {
    const m = this.wrapWidth > 0
      ? this.ctx.measure(this.text, this.font, this.scale, this.wrapWidth)
      : this.ctx.measure(this.text, this.font, this.scale);
    this.lines = m.lines;
    this.laidOut = { x: m.width, y: m.height };
    return { x: m.width, y: m.height };
  }

  protected minWidthCore(): number {
    if (this.wrap) {
      return longestWord(this.ctx.measure, this.text, this.font, this.scale);
    }
    return this.shrink
      ? fitTextMinWidth(this.ctx.measure, this.text, this.font, this.scale)
      : Math.ceil(this.ctx.measure(this.text, this.font, this.scale).width);
  }

  protected override arrangeCore(): void {
    if (!this.wrap || this.bounds.width <= 0 || this.bounds.width >= Math.ceil(this.laidOut.x)) {
      return;
    }
    this.wrapWidth = this.bounds.width;
    this.reflow();
  }

  override finishDetail(): void {
    if (this.info) {
      this.info.detail = { ...this.info.detail, text: this.text, lines: this.lines, font: this.font, scale: this.scale };
    }
  }
}

/** Components/Button.cs (PadX 24, PadY 12, IconGap 8, MinHeight 64). */
export class LButton extends LElement {
  static readonly padX = 24;
  static readonly padY = 12;
  static readonly iconGap = 8;
  static readonly minHeight = 64;
  text = '';
  font: FontName = 'small';
  drawBox = true;
  shrink = false;
  /** Icon source size × IconScale, or zero without an icon. */
  iconSize: Vec = { x: 0, y: 0 };

  private contentWidth(textWidth: number): number {
    const iconWidth = this.iconSize.x;
    return textWidth + iconWidth + (textWidth > 0 && iconWidth > 0 ? LButton.iconGap : 0) + (this.drawBox ? 2 * LButton.padX : 0);
  }

  protected measureCore(): Vec {
    const empty = stripMarks(this.text).length === 0;
    const m = empty ? { width: 0, height: 0 } : this.ctx.measure(this.text, this.font, 1);
    let h = Math.max(m.height, this.iconSize.y) + (this.drawBox ? 2 * LButton.padY : 0);
    if (this.drawBox) {
      h = Math.max(h, LButton.minHeight);
    }
    return { x: this.contentWidth(m.width), y: h };
  }

  protected minWidthCore(): number {
    const textWidth = stripMarks(this.text).length === 0 ? 0
      : this.shrink ? fitTextMinWidth(this.ctx.measure, this.text, this.font, 1)
      : Math.ceil(this.ctx.measure(this.text, this.font, 1).width);
    return this.contentWidth(textWidth);
  }
}

/** Components/Checkbox.cs: a 36 px box (CheckboxChecked 9 px × PixelScale 4) + 8 px gap + the label. */
export class LCheckbox extends LElement {
  static readonly boxSize = 36;
  static readonly labelGap = 8;
  label = '';
  font: FontName = 'small';
  shrink = false;

  protected measureCore(): Vec {
    if (stripMarks(this.label).length === 0) {
      return { x: LCheckbox.boxSize, y: LCheckbox.boxSize };
    }
    const m = this.ctx.measure(this.label, this.font, 1);
    return { x: LCheckbox.boxSize + LCheckbox.labelGap + m.width, y: Math.max(LCheckbox.boxSize, m.height) };
  }

  protected minWidthCore(): number {
    if (stripMarks(this.label).length === 0) {
      return LCheckbox.boxSize;
    }
    const w = this.shrink ? fitTextMinWidth(this.ctx.measure, this.label, this.font, 1) : Math.ceil(this.ctx.measure(this.label, this.font, 1).width);
    return LCheckbox.boxSize + LCheckbox.labelGap + w;
  }
}

/** TextInput / NumberInput: Rendering/TextBoxDrawing.cs (vanilla text box 192×48; min = max(2×16, 26) + "000"). */
export class LTextBox extends LElement {
  font: FontName = 'small';
  /** A custom Texture's size when known (the designer cannot read textures: null = the vanilla box). */
  textureSize: Vec | null = null;

  protected measureCore(): Vec {
    return this.textureSize ? { ...this.textureSize } : { x: 192, y: 48 };
  }

  protected minWidthCore(): number {
    return Math.max(2 * 16, 26) + this.ctx.measure('000', this.font, 1).width;
  }
}

/** Components/Dropdown.cs: DefaultWidth 300, row height 44, ButtonWidth 48, TextPadX 4, MinTextWidth 40. */
export class LDropdown extends LElement {
  labels: string[] = [];
  font: FontName = 'small';
  shrink = false;

  protected measureCore(available: Vec): Vec {
    return { x: Math.max(0, Math.min(available.x, 300)), y: 44 };
  }

  protected minWidthCore(): number {
    let text = 40;
    for (const label of this.labels) {
      text = Math.max(text, this.shrink ? fitTextMinWidth(this.ctx.measure, label, this.font, 1) : Math.ceil(this.ctx.measure(label, this.font, 1).width));
    }
    const width = 48 + (2 * 4) + text;
    return this.shrink ? width : Math.min(width, 300);
  }
}

/** Components/Slider.cs: DefaultWidth 192, DefaultHeight 24, minimum three knob widths (SliderKnob 10 px × 4). */
export class LSlider extends LElement {
  protected measureCore(available: Vec): Vec {
    return { x: Math.max(0, Math.min(available.x, 192)), y: 24 };
  }

  protected minWidthCore(): number {
    return 3 * 10 * 4;
  }
}

/** Components/Image.cs: source size × scale. */
export class LImage extends LElement {
  sourceSize: Vec = { x: 16, y: 16 };
  scale = 1;

  protected measureCore(): Vec {
    return { x: this.sourceSize.x * this.scale, y: this.sourceSize.y * this.scale };
  }

  protected minWidthCore(): number {
    return this.sourceSize.x * this.scale;
  }
}

/** Components/ItemImage.cs: a 16 px item sprite × scale. */
export class LItemImage extends LElement {
  scale = 1;

  protected measureCore(): Vec {
    return { x: 16 * this.scale, y: 16 * this.scale };
  }

  protected minWidthCore(): number {
    return 16 * this.scale;
  }
}

/** A template / custom tag / composite the designer cannot expand: a fixed box (Width / Height override it). */
export class LPlaceholder extends LElement {
  static readonly size: Vec = { x: 200, y: 48 };

  protected measureCore(): Vec {
    return { ...LPlaceholder.size };
  }

  protected minWidthCore(): number {
    return LPlaceholder.size.x;
  }
}
