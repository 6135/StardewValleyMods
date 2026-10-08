// DataBuilder.CreateList / CreateDataGrid and Core/AutoForm.cs for the layout builder (build.ts): collections laid out
// over the preview's rows, and a data Form's generated grid of captions and controls.
import { defNum, type BuildHost } from './buildHost';
import {
  clampOffset, LButton, LCheckbox, LDataGrid, LDropdown, LForm, LGrid, LLabel, LListView, LPanel, LSpacer, LStack, LTextBox,
  type ElementInfo, type GridColumn, type LElement
} from './elements';
import { parseTracks } from './engine';
import type { Scope } from './scope';
import type { Src } from './source';
import { parseBool, parseInt32 } from './values';

/** DataBuilder.CreateList + ListView rows (Panel rows, no box, no padding). */
export function createList(b: BuildHost, src: Src, scope: Scope, info: ElementInfo): LElement {
  const v = b.resolve;
  const rowHeight = Math.max(1, v.int(src.fields.RowHeight, scope) ?? defNum('List', 'RowHeight', 1));
  const visibleRows = Math.max(1, v.int(src.fields.VisibleRows, scope) ?? defNum('List', 'VisibleRows', 1));
  const list = new LListView(b.ctx, info, rowHeight, visibleRows);
  const template = b.rowTemplate(src);
  const rows = b.inlineRows(src.fields.Source ?? src.extra.Source, scope);
  list.count = rows?.length ?? Math.max(0, b.opts.repeatCount);
  list.first = clampOffset(b.opts.scrollOffsets?.[info.nodeId] ?? 0, list.count - visibleRows);
  for (let i = 0; i < visibleRows; i++) {
    const index = list.first + i;
    const rowScope: Scope = { ...b.rowScope(scope, rows, index, src.fields.As), ownerId: scope.ownerId ?? (src.raw ? undefined : src.id) };
    const row = new LPanel(b.ctx, b.synthetic(src.id, 'List.row', rowScope), false, 0);
    row.visible = index < list.count;
    if (row.visible) {
      b.buildChildren(row, template, rowScope);
    }
    list.add(row);
  }
  return list;
}

/** DataGridColumn (Width "*", MinWidth 0, Sortable false by default): its header and a cell per shown row (`rowScopes`). */
function gridColumn(b: BuildHost, col: Src, scope: Scope, rowScopes: Scope[]): GridColumn {
  const v = b.resolve;
  const def = col.fields;
  const header = new LLabel(b.ctx, null);
  header.text = v.text(def.Header ?? def.Id ?? '', scope);
  header.info = b.synthetic(col.id, 'DataGrid.header', scope, header.text);
  header.info.detail = {};
  const column: GridColumn = {
    track: parseTracks(def.Width ?? '*')[0] ?? { type: 'star', value: 1 },
    minWidth: Math.max(0, parseInt32(def.MinWidth ?? '0') ?? 0),
    sortable: parseBool(def.Sortable ?? 'false') ?? false,
    header,
    cells: []
  };
  const cellDefs = col.children();
  for (const rowScope of rowScopes) {
    if (cellDefs.length > 0) {
      // a cell's elements belong to the column: clicking one selects it
      const panel = new LPanel(b.ctx, null, false, 0);
      b.buildChildren(panel, cellDefs, { ...rowScope, ownerId: scope.ownerId ?? (col.raw ? undefined : col.id) });
      column.cells.push(panel);
    } else {
      const label = new LLabel(b.ctx, null);
      label.text = v.text(def.Text ?? '', rowScope);
      label.verticalAlign = 'center';
      label.info = b.synthetic(col.id, 'DataGrid.cell', rowScope, label.text);
      label.info.detail = {};
      column.cells.push(label);
    }
  }
  return column;
}

/** DataBuilder.CreateDataGrid / DataGridColumn (Width "*", MinWidth 0, Sortable false by default) over its Column items. */
export function createDataGrid(b: BuildHost, src: Src, scope: Scope, info: ElementInfo): LElement {
  const v = b.resolve;
  const rowHeight = Math.max(1, v.int(src.fields.RowHeight, scope) ?? defNum('DataGrid', 'RowHeight', 1));
  const visibleRows = Math.max(1, v.int(src.fields.VisibleRows, scope) ?? defNum('DataGrid', 'VisibleRows', 1));
  const grid = new LDataGrid(b.ctx, info, rowHeight, visibleRows);
  const source = b.inlineRows(src.fields.Source ?? src.extra.Source, scope);
  grid.count = source?.length ?? Math.max(0, b.opts.repeatCount);
  grid.first = clampOffset(b.opts.scrollOffsets?.[info.nodeId] ?? 0, grid.count - visibleRows);
  const rowScopes = Array.from({ length: Math.min(grid.count - grid.first, visibleRows) },
    (_, i) => b.rowScope(scope, source, grid.first + i, src.fields.As));
  for (const rowScope of rowScopes) {
    grid.rows.push(new LPanel(b.ctx, b.synthetic(src.id, 'DataGrid.row', rowScope), false, 0));
  }
  for (const col of src.children()) {
    grid.columns.push(gridColumn(b, col, scope, rowScopes));
  }
  for (const c of grid.columns) {
    for (const e of [c.header, ...c.cells]) {
      e.parent = grid;
    }
  }
  for (const r of grid.rows) {
    r.parent = grid;
  }
  return grid;
}

/** Core/AutoForm.cs over a data Form's FormField items (AutoFormField: caption, control; DataBuilder.Form kinds). */
export function createForm(b: BuildHost, src: Src, scope: Scope, info: ElementInfo): LElement {
  const v = b.resolve;
  const ctx = b.ctx;
  const form = new LForm(ctx, info);
  const grid = new LGrid(ctx, null, 'auto,*', 'auto');
  grid.columnSpacing = 24;
  grid.rowSpacing = 12;
  grid.horizontalAlign = 'stretch';
  let row = 0;
  src.children().forEach((field, index) => {
    const def = field.fields;
    if (def.Section !== undefined) {
      if (row > 0) {
        const gap = new LSpacer(ctx, null);
        gap.height = 8;
        gap.row = row++;
        gap.columnSpan = 2;
        grid.add(gap);
      }
      const header = new LLabel(ctx, null);
      header.text = v.text(def.Section, scope);
      header.font = 'dialogue';
      header.wrap = true;
      header.row = row++;
      header.columnSpan = 2;
      header.info = b.synthetic(field.id, 'Form.section', scope, header.text);
      header.info.detail = {};
      grid.add(header);
    }
    const caption = new LLabel(ctx, null);
    caption.text = v.text(def.Label ?? def.Id ?? `field ${index + 1}`, scope);
    caption.verticalAlign = 'center';
    caption.row = row;
    caption.info = b.synthetic(field.id, 'Form.label', scope, caption.text);
    caption.info.detail = {};
    const cell = new LStack(ctx, null, false, 4);
    cell.row = row;
    cell.column = 1;
    const sourced = b.sourcedChoices(field, scope);
    const choices = sourced?.labels ?? b.stringList(field, 'Choices');
    const kind = (def.Kind ?? (choices.length > 0 || (field.extra.ChoicesSource ?? def.ChoicesSource) !== undefined ? 'Dropdown' : 'Text')).trim().toLowerCase();
    let control: LElement;
    if (kind === 'checkbox') {
      control = new LCheckbox(ctx, b.synthetic(field.id, 'Checkbox', scope));
      control.info!.detail = { checked: false };
    } else if (kind === 'dropdown') {
      const dropdown = new LDropdown(ctx, null);
      dropdown.labels = choices.map(c => v.text(c, scope));
      dropdown.info = b.synthetic(field.id, 'Dropdown', scope, dropdown.labels[0]);
      dropdown.info.detail = { value: dropdown.labels[0] ?? '' };
      control = dropdown;
    } else {
      control = new LTextBox(ctx, b.synthetic(field.id, kind === 'number' || kind === 'integer' ? 'NumberInput' : 'TextInput', scope));
      control.info!.detail = { value: def.Value ?? '' };
    }
    cell.add(control);
    grid.add(caption);
    grid.add(cell);
    row++;
  });
  form.add(grid);
  const buttons = new LStack(ctx, null, true, 16);
  buttons.horizontalAlign = 'center';
  for (const text of ['Save', 'Cancel', 'Undo', 'Redo']) {
    const button = new LButton(ctx, b.synthetic(src.id, 'Button', scope, text));
    button.text = text;
    button.info!.detail = { drawBox: true };
    buttons.add(button);
  }
  buttons.visible = v.bool(src.fields.ShowButtons, scope) ?? true;
  form.add(buttons);
  return form;
}
