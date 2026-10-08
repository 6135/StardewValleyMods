// The per-type part of DataBuilder.Create + ApplyTypeMembers (Data/Building/DataBuilder.cs) for the layout builder
// (build.ts): one creator per built-in element type, containers building their children through the builder.
import { createDataGrid, createForm, createList } from './buildData';
import { defBool, defNum, defText, type Creator } from './buildHost';
import {
  LButton, LCanvas, LCheckbox, LDropdown, LGrid, LImage, LItemImage, LLabel, LPanel, LScrollView, LSlider, LSpacer, LStack, LTextBox,
  type ElementInfo
} from './elements';
import { substituteArgs } from './scope';
import { scalarText, stringOf } from './source';
import { cssColor, hasTemplate, parseAlign, parseInt32, parseRect } from './values';

/** Width / height of a sprite reference when the reference spells it (`asset:Path@x,y,w,h`); items are 16 px. */
function spriteSize(ref: string | undefined): { x: number; y: number } | null {
  if (!ref) {
    return null;
  }
  const at = /@\s*(-?\d+\s*,\s*-?\d+\s*,\s*\d+\s*,\s*\d+)\s*$/.exec(ref);
  const rect = at ? parseRect(at[1]!) : undefined;
  if (rect) {
    return { x: rect[2], y: rect[3] };
  }
  return /^\s*item\s*:/i.test(ref) ? { x: 16, y: 16 } : null;
}

// ---------------------------------------------------------------------------------------------------------------------
//  Containers
// ---------------------------------------------------------------------------------------------------------------------

const createStack: Creator = (b, src, type, scope, info) => {
  const stack = new LStack(b.ctx, info(src.fields.Id), defBool(type, 'Horizontal', false), defNum(type, 'Spacing', 0));
  b.applyStack(stack, src, scope);
  if (type === 'Outlet') {
    b.buildOutlet(stack, src, scope);
  } else {
    b.buildChildren(stack, src.children(), scope);
  }
  return stack;
};

/** DataBuilder.BuildElement: a Switch is a vertical stack with no spacing holding the selected page. */
const createSwitch: Creator = (b, src, _type, scope, info) => {
  const f = src.fields;
  const stack = new LStack(b.ctx, info(f.Switch !== undefined ? 'ƒ ' + f.Switch : undefined), false, 0);
  const page = b.selectPage(src, scope);
  if (page) {
    b.buildOne(stack, page, scope);
  }
  return stack;
};

const createRepeat: Creator = (b, src, type, scope, info) => {
  const f = src.fields;
  const stack = new LStack(b.ctx, info(f.Repeat ?? stringOf(src.extra.Repeat ?? '')), defBool(type, 'Horizontal', false), defNum(type, 'Spacing', 0));
  b.applyStack(stack, src, scope);
  const template = src.children();
  const rows = b.inlineRows(f.Repeat ?? src.extra.Repeat, scope);
  for (let i = 0; i < (rows?.length ?? Math.max(0, b.opts.repeatCount)); i++) {
    b.buildChildren(stack, template, b.rowScope(scope, rows, i, f.As));
  }
  return stack;
};

/** Components/Slot.cs: a stack for other mods' contributions (none in the designer). */
const createSlot: Creator = (b, src, _type, scope, info) => {
  const slot = new LStack(b.ctx, info(src.fields.Id), false, 0);
  b.applyStack(slot, src, scope);
  const maxHeight = b.resolve.optInt(src.fields.MaxHeight, scope);
  if (maxHeight !== undefined) slot.maxHeight = maxHeight;
  return slot;
};

const createGrid: Creator = (b, src, type, scope, info) => {
  const v = b.resolve;
  const f = src.fields;
  const grid = new LGrid(b.ctx, info(f.Id), defText(type, 'Columns', '*'), defText(type, 'Rows', 'auto'));
  const columns = b.gridColumns(src);
  if (columns !== undefined) grid.columns = substituteArgs(columns, scope.args, false);
  const rows = v.typed(f.Rows, scope, s => s, false);
  if (rows !== undefined) grid.rows = rows;
  const cs = v.int(f.ColumnSpacing, scope);
  if (cs !== undefined) grid.columnSpacing = cs;
  const rsp = v.int(f.RowSpacing, scope);
  if (rsp !== undefined) grid.rowSpacing = rsp;
  b.buildChildren(grid, src.children(), scope);
  return grid;
};

const createPanel: Creator = (b, src, type, scope, info) => {
  const v = b.resolve;
  const f = src.fields;
  const panel = new LPanel(b.ctx, info(f.Id), defBool(type, 'DrawBox', false), defNum(type, 'Padding', 0));
  const drawBox = v.bool(f.DrawBox, scope);
  if (drawBox !== undefined) panel.drawBox = drawBox;
  const padding = v.int(f.Padding, scope);
  if (padding !== undefined) panel.padding = padding;
  const stylePadding = b.styleField(src, 'Padding');
  if (stylePadding !== undefined) panel.stylePadding = parseInt32(stylePadding) ?? null;
  panel.info!.detail = { drawBox: panel.drawBox };
  b.buildChildren(panel, src.children(), scope);
  return panel;
};

const createCanvas: Creator = (b, src, _type, scope, info) => {
  const canvas = new LCanvas(b.ctx, info(src.fields.Id));
  b.buildChildren(canvas, src.children(), scope);
  return canvas;
};

const createScrollView: Creator = (b, src, type, scope, info) => {
  const v = b.resolve;
  const f = src.fields;
  const scrollInfo = info(f.Id);
  const scroll = new LScrollView(b.ctx, scrollInfo, defNum(type, 'ViewportHeight', 0));
  const vh = v.int(f.ViewportHeight, scope);
  if (vh !== undefined) scroll.viewportHeight = vh;
  const show = v.bool(f.ShowScrollbar, scope);
  if (show !== undefined) scroll.showScrollbar = show;
  const step = v.int(f.ScrollStep, scope);
  if (step !== undefined) scroll.scrollStep = Math.max(1, step);
  scroll.scrollOffset = b.opts.scrollOffsets?.[scrollInfo.nodeId] ?? 0;
  b.buildChildren(scroll, src.children(), scope);
  return scroll;
};

// ---------------------------------------------------------------------------------------------------------------------
//  Controls
// ---------------------------------------------------------------------------------------------------------------------

const createSpacer: Creator = (b, src, type, scope, info) => {
  const spacer = new LSpacer(b.ctx, info());
  // the Spacer constructor's width / height (0 = unset) come from the data defaults
  const dw = defNum(type, 'Width', 0);
  const dh = defNum(type, 'Height', 0);
  spacer.width = dw > 0 ? dw : null;
  spacer.height = dh > 0 ? dh : null;
  spacer.line = b.resolve.bool(src.fields.Line, scope) ?? false;
  spacer.info!.detail = { line: spacer.line };
  return spacer;
};

const createLabel: Creator = (b, src, _type, scope, info) => {
  const v = b.resolve;
  const f = src.fields;
  const label = new LLabel(b.ctx, null);
  const rich = v.bool(f.RichText, scope) ?? false;
  label.text = v.text(f.Text ?? f.Label, scope, rich);
  label.font = b.font(src, scope, true);
  label.wrap = v.bool(f.Wrap, scope) ?? false;
  label.shrink = v.bool(f.Shrink, scope) ?? false;
  label.scale = v.num(f.Scale, scope) ?? 1;
  label.info = info(label.text);
  const detail: ElementInfo['detail'] = {};
  const align = v.typed(f.TextAlign, scope, parseAlign, false);
  if (align) detail.textAlign = align;
  const color = f.Color !== undefined && !hasTemplate(f.Color) ? cssColor(f.Color) : undefined;
  if (color) detail.color = color;
  label.info.detail = detail;
  return label;
};

const createButton: Creator = (b, src, _type, scope, info) => {
  const v = b.resolve;
  const f = src.fields;
  const button = new LButton(b.ctx, null);
  button.text = v.text(f.Text ?? f.Button, scope, v.bool(f.RichText, scope) ?? false);
  button.font = b.font(src, scope, true);
  button.drawBox = v.bool(f.DrawBox, scope) ?? true;
  button.shrink = v.bool(f.Shrink, scope) ?? false;
  if (f.Icon !== undefined) {
    // Button.IconScale defaults to 4; a sprite whose size the reference does not spell counts as 16 px
    const scale = v.num(f.IconScale, scope) ?? 4;
    const size = spriteSize(f.Icon) ?? { x: 16, y: 16 };
    button.iconSize = { x: size.x * scale, y: size.y * scale };
  }
  button.info = info(button.text);
  button.info.detail = { drawBox: button.drawBox, ...(f.Icon !== undefined ? { sprite: v.text(f.Icon, scope) } : {}) };
  return button;
};

const createCheckbox: Creator = (b, src, _type, scope, info) => {
  const v = b.resolve;
  const f = src.fields;
  const box = new LCheckbox(b.ctx, null);
  box.label = v.text(f.Label ?? f.Checkbox ?? f.Text, scope);
  box.font = b.font(src, scope, false);
  box.shrink = v.bool(f.Shrink, scope) ?? false;
  box.info = info(box.label);
  box.info.detail = { checked: v.bool(f.Value, scope) ?? false };
  return box;
};

const createInput: Creator = (b, src, type, scope, info) => {
  const v = b.resolve;
  const f = src.fields;
  const input = new LTextBox(b.ctx, null);
  input.font = b.font(src, scope, false);
  const value = f.Value !== undefined ? v.text(f.Value, scope) : type === 'NumberInput' ? defText(type, 'Min', '0') : '';
  const placeholder = f.Placeholder !== undefined ? v.text(f.Placeholder, scope) : undefined;
  input.info = info(value || placeholder);
  input.info.detail = { value, ...(placeholder !== undefined ? { placeholder } : {}) };
  return input;
};

const createDropdown: Creator = (b, src, _type, scope, info) => {
  const v = b.resolve;
  const f = src.fields;
  const dropdown = new LDropdown(b.ctx, null);
  dropdown.font = b.font(src, scope, false);
  dropdown.shrink = v.bool(f.Shrink, scope) ?? false;
  const sourced = b.sourcedChoices(src, scope);
  const choices = sourced?.values ?? b.stringList(src, 'Choices');
  const labels = sourced?.labels ?? b.stringList(src, 'Labels');
  dropdown.labels = choices.map((c, i) => v.text(labels[i] ?? c, scope));
  const value = f.Value !== undefined ? v.text(f.Value, scope) : choices[0] !== undefined ? v.text(choices[0], scope) : '';
  const index = choices.findIndex(c => c === f.Value);
  const shown = index >= 0 ? dropdown.labels[index] ?? value : value;
  dropdown.info = info(shown);
  dropdown.info.detail = { value: shown };
  return dropdown;
};

const createSlider: Creator = (b, src, type, scope, info) => {
  const v = b.resolve;
  const f = src.fields;
  const slider = new LSlider(b.ctx, null);
  const min = v.num(f.Min, scope) ?? defNum(type, 'Min', 0);
  const max = v.num(f.Max, scope) ?? defNum(type, 'Max', 0);
  const value = v.num(f.Value, scope) ?? min;
  slider.info = info(f.Value !== undefined ? v.text(f.Value, scope) : undefined);
  slider.info.detail = { fraction: max > min ? Math.min(1, Math.max(0, (value - min) / (max - min))) : 0 };
  return slider;
};

const createImage: Creator = (b, src, type, scope, info) => {
  const v = b.resolve;
  const f = src.fields;
  const image = new LImage(b.ctx, null);
  const sprite = f.Sprite ?? f.Image;
  const source = f.Source ?? scalarText((src.extra.Source as Record<string, unknown> | undefined)?.Shorthand);
  const rect = source !== undefined ? v.typed(source, scope, parseRect, false) : undefined;
  const resolved = sprite !== undefined ? v.text(sprite, scope) : undefined;
  image.sourceSize = rect ? { x: rect[2], y: rect[3] } : spriteSize(resolved) ?? { x: 16, y: 16 };
  image.scale = v.num(f.Scale, scope) ?? defNum(type, 'Scale', 1);
  image.info = info(resolved);
  image.info.detail = resolved !== undefined ? { sprite: resolved } : {};
  return image;
};

const createItemImage: Creator = (b, src, type, scope, info) => {
  const v = b.resolve;
  const item = new LItemImage(b.ctx, null);
  item.scale = v.num(src.fields.Scale, scope) ?? defNum(type, 'Scale', 1);
  const ref = src.fields.Item !== undefined ? v.text(src.fields.Item, scope) : undefined;
  item.info = info(ref);
  item.info.detail = ref !== undefined ? { sprite: ref } : {};
  return item;
};

/** The creator of each built-in type the designer lays out; other types are drawn as a placeholder. */
export const creators: Readonly<Record<string, Creator>> = {
  Stack: createStack,
  Outlet: createStack,
  Switch: createSwitch,
  Repeat: createRepeat,
  Slot: createSlot,
  Grid: createGrid,
  Panel: createPanel,
  Canvas: createCanvas,
  ScrollView: createScrollView,
  Spacer: createSpacer,
  Label: createLabel,
  Button: createButton,
  Checkbox: createCheckbox,
  TextInput: createInput,
  NumberInput: createInput,
  Dropdown: createDropdown,
  Slider: createSlider,
  Image: createImage,
  ItemImage: createItemImage,
  List: (b, src, _type, scope, info) => createList(b, src, scope, info(src.fields.Id)),
  DataGrid: (b, src, _type, scope, info) => createDataGrid(b, src, scope, info(src.fields.Id)),
  Form: (b, src, _type, scope, info) => createForm(b, src, scope, info(src.fields.Id))
};
