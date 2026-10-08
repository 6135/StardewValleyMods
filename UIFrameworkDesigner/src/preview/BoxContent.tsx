// Box contents of the schematic preview (PreviewPane.tsx): the text, control or image a layout box draws, by its kind.
import type { ReactNode } from 'react';
import { CHIP_MARKS, type LayoutBox } from '../layout';
import type { GameArt } from './gameArt';
import { objectIndex } from './gameData';
import { fontStyle, renderText } from './text';
import { ItemSprite } from './Tooltip';

export interface BoxProps { box: LayoutBox; i18n: Record<string, string> | undefined; art: GameArt | undefined }

function TextLines({ box, i18n }: BoxProps): ReactNode {
  const d = box.detail ?? {};
  const lines = d.lines && d.lines.length > 0 ? d.lines : [d.text ?? box.label ?? ''];
  const align = d.textAlign === 'center' ? 'center' : d.textAlign === 'end' ? 'right' : 'left';
  return (
    <div className="pv-text" style={{ ...fontStyle(d.font, d.scale), textAlign: align, ...(d.color ? { color: d.color } : {}) }}>
      {lines.map((line, i) => <div key={i} className="pv-line">{renderText(line, i18n)}</div>)}
    </div>
  );
}

function ButtonContent({ box, i18n }: BoxProps): ReactNode {
  const d = box.detail ?? {};
  return (
    <div className={d.drawBox === false ? 'pv-button pv-flat' : 'pv-button'} style={fontStyle(d.font)}>
      {d.sprite ? <span className="pv-icon" title={d.sprite}>▣</span> : null}
      <span className="pv-nowrap">{renderText(box.label ?? '', i18n)}</span>
    </div>
  );
}

function CheckboxContent({ box, i18n }: BoxProps): ReactNode {
  return (
    <div className="pv-checkbox" style={fontStyle()}>
      <span className={box.detail?.checked ? 'pv-check pv-checked' : 'pv-check'} />
      <span className="pv-nowrap">{renderText(box.label ?? '', i18n)}</span>
    </div>
  );
}

function TextBoxContent({ box, i18n }: BoxProps): ReactNode {
  const d = box.detail ?? {};
  return (
    <div className="pv-textbox" style={fontStyle()}>
      {d.value ? renderText(d.value, i18n) : <span className="pv-placeholder">{renderText(d.placeholder ?? '', i18n)}</span>}
    </div>
  );
}

function DropdownContent({ box, i18n }: BoxProps): ReactNode {
  return (
    <div className="pv-dropdown" style={fontStyle()}>
      <span className="pv-nowrap">{renderText(box.detail?.value ?? box.label ?? '', i18n)}</span>
      <span className="pv-caret">▼</span>
    </div>
  );
}

function SliderContent({ box }: BoxProps): ReactNode {
  const left = 'calc((100% - 40px) * ' + String(box.detail?.fraction ?? 0) + ')';
  return (
    <div className="pv-slider">
      <span className="pv-knob" style={{ left }} />
    </div>
  );
}

function ImageContent({ box, i18n, art }: BoxProps): ReactNode {
  const d = box.detail ?? {};
  if (box.kind === 'ItemImage' && d.sprite && objectIndex(d.sprite) !== undefined && art?.objects) {
    return <div className="pv-item-image"><ItemSprite id={d.sprite} art={art} size="100%" /></div>;
  }
  // an Item / Sprite expression the preview cannot evaluate: a neutral icon, the expression on hover
  if (d.sprite !== undefined && /[-]/.test(d.sprite)) {
    return <div className="pv-image" title={d.sprite.replace(CHIP_MARKS, '')}><span className="pv-image-icon">▣</span></div>;
  }
  return <div className="pv-image" title={d.sprite}><span>{renderText(d.sprite ?? box.kind, i18n)}</span></div>;
}

const SpacerContent = ({ box }: BoxProps): ReactNode => (box.detail?.line ? <div className="pv-spacer-line" /> : null);

const UnknownContent = ({ box, i18n }: BoxProps): ReactNode => <div className="pv-unknown"><span>⟨{renderText(box.label ?? '', i18n)}⟩</span></div>;

/** What each kind of box draws inside it; other kinds (containers) draw nothing. */
const contentRenderers: Partial<Record<string, (props: BoxProps) => ReactNode>> = {
  'Label': TextLines,
  'DataGrid.header': TextLines,
  'DataGrid.cell': TextLines,
  'Form.label': TextLines,
  'Form.section': TextLines,
  'Button': ButtonContent,
  'Checkbox': CheckboxContent,
  'TextInput': TextBoxContent,
  'NumberInput': TextBoxContent,
  'Dropdown': DropdownContent,
  'Slider': SliderContent,
  'ItemImage': ImageContent,
  'Image': ImageContent,
  'Spacer': SpacerContent,
  'Custom': UnknownContent,
  'Composite': UnknownContent
};

export function BoxContent(props: BoxProps): ReactNode {
  const render = Object.prototype.hasOwnProperty.call(contentRenderers, props.box.kind) ? contentRenderers[props.box.kind] : undefined;
  return render ? render(props) : null;
}
