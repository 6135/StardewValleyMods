using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    /// <summary>
    /// Virtualized table (architecture §16.2). Built like <see cref="ListView"/>: exactly <see cref="VisibleRows"/>
    /// <see cref="DataGridRow"/> containers exist under a header row, each showing the item at display position
    /// <see cref="FirstVisibleIndex"/> + slot. Display positions map to <b>underlying</b> row indices through an
    /// order array rebuilt by <see cref="Refresh"/> (count → filter → sort); selection, events and every column
    /// delegate use underlying indices, so sorting or filtering never invalidates them.
    /// <para>
    /// This file holds the data / layout / drawing half; <c>DataGrid.Input.cs</c> holds clicks, keys and selection.
    /// </para>
    /// </summary>
    internal sealed partial class DataGrid : UIContainer, IUIDataGrid
    {
        private const int CellPadX = 8;
        private const int HeaderPadY = 8;
        private const int ArrowScale = 2;
        private const int ArrowGap = 6;
        private const string DefaultScrollSound = "shwip";
        private const string DefaultSelectSound = "smallSelect";
        private const string DefaultSortSound = "drumkit6";
        private static readonly Color ZebraColor = Color.White * 0.06f;
        private static readonly Color SelectionColor = Color.Wheat * 0.5f;
        private static readonly Color HoverRowColor = Color.Wheat * 0.25f;
        private static readonly Color HeaderHoverColor = Color.Wheat * 0.4f;
        private static readonly Color DividerColor = Color.Black * 0.15f;
        private static readonly Color DividerActiveColor = Color.Wheat;

        private readonly Func<int>? rowCount;
        private readonly List<DataGridColumn> columns = new();
        private readonly List<DataGridRow> rows = new();
        private readonly ScrollbarGadget scrollbar = new();
        private readonly HashSet<int> selected = new();

        /// <summary>Display position → underlying row.</summary>
        private int[] order = Array.Empty<int>();

        /// <summary>Underlying row → display position, or -1 when filtered out.</summary>
        private int[] position = Array.Empty<int>();

        private int rowHeight;

        /// <summary>The row height actually laid out: <see cref="RowHeight"/> grown with the text scale.</summary>
        private int EffectiveRowHeight => Theme.ScaleForText(rowHeight);
        private int visibleRows;
        private int firstVisible;
        private int lastCount;
        private int headerHeight;
        private bool needsRefresh = true;
        private string sortColumn = string.Empty;
        private bool sortDescending;
        private Func<int, bool>? filter;

        internal DataGrid(string id, int rowHeight, int visibleRows, Func<int>? rowCount) : base(id)
        {
            this.rowHeight = Math.Max(1, rowHeight);
            this.visibleRows = Math.Max(1, visibleRows);
            this.rowCount = rowCount;
            EnsureRowContainers();
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Properties
        // ---------------------------------------------------------------------------------------------------------

        public int RowHeight
        {
            get => rowHeight;
            set
            {
                value = Math.Max(1, value);
                if (rowHeight == value)
                {
                    return;
                }

                rowHeight = value;
                InvalidateLayout();
            }
        }

        /// <summary>Number of row containers; changing it rebuilds the rows.</summary>
        public int VisibleRows
        {
            get => visibleRows;
            set
            {
                value = Math.Max(1, value);
                if (visibleRows == value)
                {
                    return;
                }

                visibleRows = value;
                EnsureRowContainers();
                Refresh();
            }
        }

        /// <summary>Display position of the first visible row, clamped to [0, count - <see cref="VisibleRows"/>].</summary>
        public int FirstVisibleIndex
        {
            get => firstVisible;
            set => SetFirstVisible(value);
        }

        /// <summary>Rows shown after filtering (valid after the last refresh).</summary>
        public int RowCount => order.Length;

        public int ColumnCount => columns.Count;

        public IUIDataGridColumn GetColumn(int index) => columns[index];

        public IUIDataGridColumn FindColumn(string columnId) => FindColumnInternal(columnId)!;

        public string SortColumn => sortColumn;

        public bool SortDescending => sortDescending;

        internal Func<int, bool>? FilterFunc
        {
            get => filter;
            set
            {
                filter = value;
                needsRefresh = true;
            }
        }

        Func<int, bool> IUIDataGrid.Filter { get => filter!; set => FilterFunc = value; }

        internal Action<string, int>? OnColumnResized { get; set; }
        internal Action<IUIValueEvent>? OnValueChanged { get; set; }
        internal Action<IUIRowEvent>? OnRowClick { get; set; }
        internal Action<int>? OnRowActivated { get; set; }
        internal Action<int>? OnScroll { get; set; }
        internal Func<int, IUITooltip>? RowTooltip { get; set; }

        Action<string, int> IUIDataGrid.OnColumnResized { get => OnColumnResized!; set => OnColumnResized = value; }
        Action<IUIValueEvent> IUIDataGrid.OnValueChanged { get => OnValueChanged!; set => OnValueChanged = value; }
        Action<IUIRowEvent> IUIDataGrid.OnRowClick { get => OnRowClick!; set => OnRowClick = value; }
        Action<int> IUIDataGrid.OnRowActivated { get => OnRowActivated!; set => OnRowActivated = value; }
        Func<int, IUITooltip> IUIDataGrid.RowTooltip { get => RowTooltip!; set => RowTooltip = value; }
        Action<int> IUIDataGrid.OnScroll { get => OnScroll!; set => OnScroll = value; }

        /// <summary>null = default cue, empty = silent.</summary>
        internal string? ScrollSound { get; set; }

        internal string? SelectSound { get; set; }

        internal string? SortSound { get; set; }

        string IUIDataGrid.ScrollSound { get => ScrollSound!; set => ScrollSound = value; }
        string IUIDataGrid.SelectSound { get => SelectSound!; set => SelectSound = value; }
        string IUIDataGrid.SortSound { get => SortSound!; set => SortSound = value; }

        internal override bool Focusable => true;

        // rows fill their slot exactly
        internal override UIAlign DefaultChildHorizontalAlign(UIElement child) => UIAlign.Stretch;
        internal override UIAlign DefaultChildVerticalAlign(UIElement child) => UIAlign.Stretch;

        // ---------------------------------------------------------------------------------------------------------
        //  Columns
        // ---------------------------------------------------------------------------------------------------------

        public IUIDataGridColumn AddColumn(string id, Func<string> header, string width)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A column id is required.", nameof(id));
            }

            DataGridColumn? existing = FindColumnInternal(id);
            if (existing != null)
            {
                UIServices.Log($"[{Consumer.ModId}] data grid '{Id}' already has a column '{id}'; it is replaced.", LogLevel.Debug);
                columns.Remove(existing);
            }

            var column = new DataGridColumn(this, id, header, width);
            columns.Add(column);
            needsRefresh = true;
            InvalidateLayout();
            return column;
        }

        public void RemoveColumn(string columnId)
        {
            DataGridColumn? column = FindColumnInternal(columnId);
            if (column == null)
            {
                return;
            }

            columns.Remove(column);
            if (sortColumn == column.Id)
            {
                sortColumn = string.Empty;
                sortDescending = false;
            }

            needsRefresh = true;
            InvalidateLayout();
        }

        private DataGridColumn? FindColumnInternal(string? columnId)
        {
            foreach (DataGridColumn c in columns)
            {
                if (c.Id == columnId)
                {
                    return c;
                }
            }
            return null;
        }

        /// <summary>Rebuild the rows before the next update (a column delegate changed).</summary>
        internal void RequestRefresh() => needsRefresh = true;

        /// <summary>Run a column delegate through the consumer guard, keyed by grid id + column id.</summary>
        internal T GuardColumn<T>(DataGridColumn column, string eventName, Func<T>? func, T fallback)
        {
            return Consumer.Invoke(Id + "." + column.Id, eventName, func, fallback);
        }

        /// <summary>Run a column action through the consumer guard, keyed by grid id + column id.</summary>
        internal void GuardColumn(DataGridColumn column, string eventName, Action? action)
        {
            Consumer.Invoke(Id + "." + column.Id, eventName, action);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Sorting / filtering
        // ---------------------------------------------------------------------------------------------------------

        public void Sort(string columnId, bool descending)
        {
            DataGridColumn? column = FindColumnInternal(columnId);
            sortColumn = column?.Id ?? string.Empty;
            sortDescending = column != null && descending;
            Refresh();
        }

        public void ClearSort()
        {
            sortColumn = string.Empty;
            sortDescending = false;
            Refresh();
        }

        /// <summary>The row count as the data source reports it right now (never negative).</summary>
        private int SourceCount => Math.Max(0, Raise("rowCount", rowCount, 0));

        /// <summary>Re-query the count, re-run filter and sort, prune the selection and rebuild every row.</summary>
        public void Refresh()
        {
            needsRefresh = false;
            lastCount = SourceCount;
            RebuildOrder(lastCount);
            PruneSelection(lastCount);
            firstVisible = Math.Clamp(firstVisible, 0, MaxFirstIndex);
            for (int i = 0; i < rows.Count; i++)
            {
                BuildRow(i, firstVisible + i);
            }

            InvalidateLayout();
        }

        /// <summary>Recompute <see cref="order"/> / <see cref="position"/> for <paramref name="count"/> underlying rows.</summary>
        private void RebuildOrder(int count)
        {
            var list = new List<int>(count);
            Func<int, bool>? predicate = filter;
            for (int i = 0; i < count; i++)
            {
                int row = i;
                if (predicate == null || Raise("Filter", () => predicate(row), true))
                {
                    list.Add(i);
                }
            }

            DataGridColumn? column = FindColumnInternal(sortColumn);
            if (column != null && list.Count > 1)
            {
                SortRows(list, column);
            }

            order = list.ToArray();
            position = new int[count];
            Array.Fill(position, -1);
            for (int p = 0; p < order.Length; p++)
            {
                position[order[p]] = p;
            }
        }

        /// <summary>Stable sort of <paramref name="list"/> by the column's keys (numeric when <see cref="IUIDataGridColumn.SortNumber"/> is set).</summary>
        private void SortRows(List<int> list, DataGridColumn column)
        {
            int n = list.Count;
            var index = new int[n];
            for (int k = 0; k < n; k++)
            {
                index[k] = k;
            }

            Comparison<int> compare = column.SortNumberFunc != null
                ? ByNumber(NumberKeys(list, column))
                : ByString(StringKeys(list, column));
            int sign = sortDescending ? -1 : 1;
            Array.Sort(index, (a, b) =>
            {
                int c = compare(a, b) * sign;
                return c != 0 ? c : a.CompareTo(b);
            });

            int[] sorted = new int[n];
            for (int k = 0; k < n; k++)
            {
                sorted[k] = list[index[k]];
            }

            list.Clear();
            list.AddRange(sorted);
        }

        private double[] NumberKeys(List<int> list, DataGridColumn column)
        {
            Func<int, double> key = column.SortNumberFunc!;
            var keys = new double[list.Count];
            for (int k = 0; k < keys.Length; k++)
            {
                int row = list[k];
                keys[k] = GuardColumn(column, "SortNumber", () => key(row), 0d);
            }
            return keys;
        }

        private string[] StringKeys(List<int> list, DataGridColumn column)
        {
            Func<int, string>? key = column.SortKeyFunc ?? column.TextFunc;
            string eventName = column.SortKeyFunc != null ? "SortKey" : "Text";
            var keys = new string[list.Count];
            for (int k = 0; k < keys.Length; k++)
            {
                int row = list[k];
                keys[k] = key == null ? string.Empty : GuardColumn(column, eventName, () => key(row), string.Empty) ?? string.Empty;
            }
            return keys;
        }

        private static Comparison<int> ByNumber(double[] keys) => (a, b) => keys[a].CompareTo(keys[b]);

        private static Comparison<int> ByString(string[] keys) => (a, b) => string.Compare(keys[a], keys[b], StringComparison.CurrentCultureIgnoreCase);

        // ---------------------------------------------------------------------------------------------------------
        //  Rows
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Largest valid <see cref="FirstVisibleIndex"/> for the cached order.</summary>
        private int MaxFirstIndex => Math.Max(0, order.Length - visibleRows);

        /// <summary>Whether the scrollbar is drawn / interactive.</summary>
        private bool ScrollbarVisible => order.Length > visibleRows;

        /// <summary>Create / remove row containers so exactly <see cref="VisibleRows"/> exist as children.</summary>
        private void EnsureRowContainers()
        {
            while (rows.Count < visibleRows)
            {
                var row = new DataGridRow(this, $"{Id}.r{rows.Count}");
                rows.Add(row);
                Add(row);
            }
            while (rows.Count > visibleRows)
            {
                int last = rows.Count - 1;
                DataGridRow row = rows[last];
                rows.RemoveAt(last);
                Remove(row);
            }
        }

        /// <summary>Run <see cref="Refresh"/> if the grid was never refreshed (so index math sees a real count).</summary>
        private void EnsureFresh()
        {
            if (needsRefresh)
            {
                Refresh();
            }
        }

        /// <summary>Point row slot <paramref name="slot"/> at display position <paramref name="pos"/> and rebuild its cells.</summary>
        private void BuildRow(int slot, int pos)
        {
            DataGridRow row = rows[slot];
            row.SyncCells(columns.Count);
            int item = pos >= 0 && pos < order.Length ? order[pos] : -1;
            row.Item = item;
            row.Visible = item >= 0;
            row.RichTooltip = item >= 0 && RowTooltip != null ? Raise("RowTooltip", () => RowTooltip(item), null) as RichTooltip : null;
            for (int j = 0; j < columns.Count; j++)
            {
                BuildCell(row.Cells[j], columns[j], item);
            }
        }

        /// <summary>Fill a cell for an underlying row: the consumer's renderer, or a label bound to the column text.</summary>
        private void BuildCell(DataGridCell cell, DataGridColumn column, int item)
        {
            cell.Clear();
            cell.Tooltip = null;
            if (item < 0)
            {
                return;
            }

            Func<int, string>? tooltip = column.CellTooltipFunc;
            if (tooltip != null)
            {
                cell.Tooltip = () => GuardColumn(column, "CellTooltip", () => tooltip(item), string.Empty);
            }

            Action<int, IUIContainer>? build = column.BuildCellFunc;
            if (build != null)
            {
                GuardColumn(column, "BuildCell", () => build(item, cell));
                return;
            }

            Func<int, string>? text = column.TextFunc;
            var label = new Label(cell.Id + ".text", text == null ? null : () => GuardColumn(column, "Text", () => text(item), string.Empty) ?? string.Empty)
            {
                HorizontalAlign = UIAlign.Stretch,
                TextAlign = column.Align == UIAlign.Stretch ? UIAlign.Start : column.Align
            };
            cell.Add(label);
        }

        /// <summary>Clamp and apply a first display position; rebuilds the rows whose item changed and raises <see cref="OnScroll"/>. Returns true if it changed.</summary>
        private bool SetFirstVisible(int value)
        {
            EnsureFresh();
            value = Math.Clamp(value, 0, MaxFirstIndex);
            if (firstVisible == value)
            {
                return false;
            }

            int delta = value - firstVisible;
            firstVisible = value;
            for (int i = 0; i < rows.Count; i++)
            {
                int pos = firstVisible + i;
                int item = pos < order.Length ? order[pos] : -1;
                if (rows[i].Item != item)
                {
                    BuildRow(i, pos);
                }
            }
            InvalidateLayout();
            if (OnScroll != null)
            {
                Action<int> cb = OnScroll;
                Raise("OnScroll", () => cb(delta));
            }
            return true;
        }

        /// <summary>Scroll so display position <paramref name="pos"/> is inside the visible window.</summary>
        private void EnsureVisible(int pos)
        {
            if (pos < firstVisible)
            {
                SetFirstVisible(pos);
            }
            else if (pos >= firstVisible + visibleRows)
            {
                SetFirstVisible(pos - visibleRows + 1);
            }
            else
            {
                // already visible
            }
        }

        public void ScrollToRow(int row)
        {
            EnsureFresh();
            if (row >= 0 && row < position.Length && position[row] >= 0)
            {
                EnsureVisible(position[row]);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Layout
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Absolute rectangle holding the header and rows (bounds minus the scrollbar column).</summary>
        private Rectangle ContentRect => new(Bounds.X, Bounds.Y, Math.Max(0, Bounds.Width - ScrollbarGadget.ReservedWidth), Bounds.Height);

        private Rectangle HeaderRect => new(Bounds.X, Bounds.Y, ContentRect.Width, headerHeight);

        private Rectangle RowsRect => new(Bounds.X, Bounds.Y + headerHeight, ContentRect.Width, visibleRows * EffectiveRowHeight);

        /// <summary>Width available to a cell's content (column width minus the cell padding).</summary>
        internal int CellContentWidth(int column) => Math.Max(0, columns[column].ResolvedWidth - (2 * CellPadX));

        /// <summary>Absolute rectangle of a cell inside a row.</summary>
        internal Rectangle CellRect(int column, Rectangle rowBounds)
        {
            return new Rectangle(columns[column].ResolvedX + CellPadX, rowBounds.Y, CellContentWidth(column), rowBounds.Height);
        }

        protected override Vector2 MeasureCore(Vector2 available)
        {
            headerHeight = (int)Math.Ceiling(UIServices.Text.LineHeight(UIFont.Small)) + (2 * HeaderPadY);
            const int reserved = ScrollbarGadget.ReservedWidth;
            bool unbounded = float.IsInfinity(available.X) || float.IsNaN(available.X);
            float contentWidth = unbounded ? float.PositiveInfinity : Math.Max(0, available.X - reserved);
            float total = ResolveColumns(contentWidth);
            var rowAvailable = new Vector2(total, EffectiveRowHeight);
            foreach (DataGridRow row in rows)
            {
                row.Measure(rowAvailable);
            }

            float width = unbounded ? total + reserved : Math.Max(available.X, total + reserved);
            return new Vector2(width, headerHeight + (visibleRows * EffectiveRowHeight));
        }

        /// <summary>Resolve every column's width against <paramref name="contentWidth"/> (star columns absorb the rest); returns the total.</summary>
        private float ResolveColumns(float contentWidth)
        {
            var tracks = new GridTrack[columns.Count];
            var auto = new float[columns.Count];
            for (int j = 0; j < columns.Count; j++)
            {
                tracks[j] = columns[j].Track;
                auto[j] = tracks[j].Type == GridTrack.Kind.Pixels ? 0 : AutoWidth(j);
            }

            float[] sizes = LayoutEngine.ResolveTracks(tracks, auto, contentWidth);
            float total = 0;
            for (int j = 0; j < columns.Count; j++)
            {
                columns[j].ResolvedWidth = (int)Math.Round(Math.Max(sizes[j], columns[j].MinWidth));
                total += columns[j].ResolvedWidth;
            }
            return total;
        }

        /// <summary>Content width of a column: the header (plus sort arrow) or the widest visible cell, with padding.</summary>
        private float AutoWidth(int column)
        {
            DataGridColumn col = columns[column];
            float width = UIServices.Text.Measure(UIFont.Small, col.HeaderText, 1f).X;
            if (col.Sortable)
            {
                width += ArrowGap + (Theme.ScrollUpArrow.Width * ArrowScale);
            }

            var unbounded = new Vector2(float.PositiveInfinity, EffectiveRowHeight);
            foreach (DataGridRow row in rows)
            {
                if (row.Visible && column < row.Cells.Count)
                {
                    width = Math.Max(width, row.Cells[column].Measure(unbounded).X);
                }
            }
            return width + (2 * CellPadX);
        }

        protected override void ArrangeCore()
        {
            Rectangle content = ContentRect;
            float total = ResolveColumns(content.Width);
            int x = content.X;
            foreach (DataGridColumn column in columns)
            {
                column.ResolvedX = x;
                x += column.ResolvedWidth;
            }
            // the final widths can differ from the measure pass (stretch); re-measure the cells at their real width
            var rowAvailable = new Vector2(total, EffectiveRowHeight);
            foreach (DataGridRow row in rows)
            {
                row.Measure(rowAvailable);
            }

            int y = content.Y + headerHeight;
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].Arrange(new Rectangle(content.X, y + (i * EffectiveRowHeight), content.Width, EffectiveRowHeight));
            }
            // any other child a consumer added directly overlaps the rows area
            foreach (UIElement child in Children)
            {
                if (child is not DataGridRow)
                {
                    child.Arrange(RowsRect);
                }
            }

            int max = MaxFirstIndex;
            scrollbar.Layout(Bounds.Right - ScrollbarGadget.Width, y, Math.Max(0, Bounds.Height - headerHeight), max > 0 ? firstVisible / (float)max : 0f);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Update / draw
        // ---------------------------------------------------------------------------------------------------------

        internal override void Update(double elapsedMs)
        {
            // rebuild before (never while) the children are iterated
            if (needsRefresh || SourceCount != lastCount)
            {
                Refresh();
            }

            base.Update(elapsedMs);
        }

        protected override void DrawCore(SpriteBatch b)
        {
            DrawHeader(b);
            DrawRowBackgrounds(b);
            DrawChildren(b);
            DrawDividers(b);
            if (ScrollbarVisible)
            {
                scrollbar.Draw(b);
            }

            if (IsFocused)
            {
                DrawHelper.Outline(b, RowsRect, DividerActiveColor * 0.6f, 2);
            }
        }

        /// <summary>Header box, hover tint on sortable headers, bold titles and the sort arrow.</summary>
        private void DrawHeader(SpriteBatch b)
        {
            Rectangle header = HeaderRect;
            if (header.Width <= 0 || header.Height <= 0)
            {
                return;
            }

            DrawHelper.Box(b, Game1.mouseCursors, Theme.DropdownBoxSource, header, Color.White, Theme.PixelScale);
            int hovered = HoveredHeaderColumn;
            ResolvedStyle style = Style;
            for (int j = 0; j < columns.Count; j++)
            {
                DataGridColumn column = columns[j];
                var cell = new Rectangle(column.ResolvedX, header.Y, column.ResolvedWidth, header.Height);
                if (column.Sortable && j == hovered && ResizeColumnAt(CursorX) < 0)
                {
                    DrawHelper.Fill(b, new Rectangle(cell.X + 2, cell.Y + 4, Math.Max(0, cell.Width - 4), Math.Max(0, cell.Height - 8)), HeaderHoverColor);
                }

                DrawHeaderText(b, column, cell, style);
            }
        }

        /// <summary>Bold header title aligned like the column, followed by the sort arrow when sorted by it.</summary>
        private void DrawHeaderText(SpriteBatch b, DataGridColumn column, Rectangle cell, ResolvedStyle style)
        {
            string text = column.HeaderText;
            bool sorted = column.Id == sortColumn;
            int arrowWidth = sorted ? ArrowGap + (Theme.ScrollUpArrow.Width * ArrowScale) : 0;
            Vector2 size = UIServices.Text.Measure(UIFont.Small, text, 1f);
            int available = Math.Max(0, cell.Width - (2 * CellPadX) - arrowWidth);
            UIAlign align = column.Align == UIAlign.Stretch ? UIAlign.Start : column.Align;
            int textX = cell.X + CellPadX + LayoutEngine.AlignOffset(align, available, (int)size.X);
            int textY = cell.Y + ((cell.Height - (int)size.Y) / 2);
            if (text.Length > 0)
            {
                Utility.drawBoldText(b, text, Game1.smallFont, new Vector2(textX, textY), style.TextColor, Theme.FontScale);
            }

            if (sorted)
            {
                Rectangle arrow = sortDescending ? Theme.ScrollDownArrow : Theme.ScrollUpArrow;
                int arrowY = cell.Y + ((cell.Height - (arrow.Height * ArrowScale)) / 2);
                b.Draw(Game1.mouseCursors, new Vector2(textX + size.X + ArrowGap, arrowY), arrow, Color.White, 0f, Vector2.Zero, ArrowScale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>Zebra tint, then the selection or hover highlight, behind the visible rows.</summary>
        private void DrawRowBackgrounds(SpriteBatch b)
        {
            UIElement? hovered = OwnerMenu?.Hovered;
            for (int i = 0; i < rows.Count; i++)
            {
                DataGridRow row = rows[i];
                if (!row.Visible)
                {
                    continue;
                }

                if ((firstVisible + i) % 2 == 1)
                {
                    DrawHelper.Fill(b, row.Bounds, ZebraColor);
                }

                if (selectable && selected.Contains(row.Item))
                {
                    DrawHelper.Fill(b, row.Bounds, SelectionColor);
                }
                else if (selectable && hovered != null && hovered.IsSelfOrDescendantOf(row))
                {
                    DrawHelper.Fill(b, row.Bounds, HoverRowColor);
                }
                else
                {
                    // plain row
                }
            }
        }

        /// <summary>Vertical lines between columns (the divider under the cursor or being dragged is highlighted).</summary>
        private void DrawDividers(SpriteBatch b)
        {
            int top = Bounds.Y + 4;
            int height = headerHeight + (visibleRows * EffectiveRowHeight) - 8;
            int active = resizingColumn >= 0 ? resizingColumn : (IsHovered && HeaderRect.Contains(CursorX, CursorY) ? ResizeColumnAt(CursorX) : -1);
            for (int j = 0; j < columns.Count - 1; j++)
            {
                Color color = j == active ? DividerActiveColor : DividerColor;
                DrawHelper.Fill(b, new Rectangle(columns[j].ResolvedRight - 1, top, 2, height), color);
            }

            if (active == columns.Count - 1 && active >= 0)
            {
                DrawHelper.Fill(b, new Rectangle(columns[active].ResolvedRight - 1, top, 2, height), DividerActiveColor);
            }
        }
    }
}
