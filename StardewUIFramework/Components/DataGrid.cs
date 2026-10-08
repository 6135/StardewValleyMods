using System;
using System.Collections.Generic;
using System.Globalization;
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

        // column resolve scratch, reused between layouts (reallocated when the column count changes)
        private GridTrack[] resolveTracks = Array.Empty<GridTrack>();
        private float[] resolveAuto = Array.Empty<float>();
        private float[] fitMin = Array.Empty<float>();
        private float[] fitNatural = Array.Empty<float>();

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

        /// <summary>Display position of an underlying row (after filter and sort), or -1 when it is not shown.</summary>
        internal int DisplayPositionOf(int row) => row >= 0 && row < position.Length ? position[row] : -1;

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
            return Consumer.Invoke(column.GuardKey, eventName, func, fallback);
        }

        /// <summary>Run a column action through the consumer guard, keyed by grid id + column id.</summary>
        internal void GuardColumn(DataGridColumn column, string eventName, Action? action)
        {
            Consumer.Invoke(column.GuardKey, eventName, action);
        }

        /// <summary>
        /// The guard key of one row's per-row column delegates (<c>Text</c>, <c>CellTooltip</c>): a row that throws is
        /// muted alone instead of blanking the whole column.
        /// </summary>
        private static string RowGuardKey(DataGridColumn column, int row) => column.GuardKey + "#" + row.ToString(CultureInfo.InvariantCulture);

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
            Func<int, string>? sortKey = column.SortKeyFunc;
            Func<int, string>? text = column.TextFunc;
            var keys = new string[list.Count];
            for (int k = 0; k < keys.Length; k++)
            {
                int row = list[k];
                // the cell text is guarded per row, like the cells that show it
                keys[k] = sortKey != null ? GuardColumn(column, "SortKey", () => sortKey(row), string.Empty) ?? string.Empty
                    : text != null ? Consumer.Invoke(RowGuardKey(column, row), "Text", () => text(row), string.Empty) ?? string.Empty
                    : string.Empty;
            }
            return keys;
        }

        private static Comparison<int> ByNumber(double[] keys) => (a, b) => keys[a].CompareTo(keys[b]);

        private static Comparison<int> ByString(string[] keys) => (a, b) => string.Compare(keys[a], keys[b], StringComparison.CurrentCultureIgnoreCase);

        // ---------------------------------------------------------------------------------------------------------
        //  Rows
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Gamepad: the rows showing an item are the snap targets, so the cursor moves row by row and shows each row's tooltip.</summary>
        internal override IEnumerable<UIElement>? GamepadTargets
        {
            get
            {
                foreach (DataGridRow row in rows)
                {
                    if (row.Item >= 0 && row.Visible)
                    {
                        yield return row;
                    }
                }
            }
        }

        /// <summary>Gamepad up / down at the first / last visible row scroll the grid by one row (the cursor stays in place).</summary>
        protected internal override bool HandleGamepadTargetDirection(UIElement target, int dx, int dy)
        {
            int slot = target is DataGridRow row ? rows.IndexOf(row) : -1;
            if (slot < 0 || dy == 0)
            {
                return false;
            }

            bool atEdge = dy > 0 ? slot == visibleRows - 1 : slot == 0;
            return atEdge && SetFirstVisible(firstVisible + dy);
        }

        /// <summary>Largest valid <see cref="FirstVisibleIndex"/> for the cached order.</summary>
        private int MaxFirstIndex => Math.Max(0, order.Length - visibleRows);

        /// <summary>Whether the scrollbar is drawn / interactive.</summary>
        private bool ScrollbarVisible => order.Length > visibleRows;

        /// <summary>Create / remove row containers so exactly <see cref="VisibleRows"/> exist as children.</summary>
        private void EnsureRowContainers()
        {
            while (rows.Count < visibleRows)
            {
                var row = new DataGridRow(this, NewRowId());
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

        /// <summary>
        /// Id of a new row container: <c>{grid}.r{n}</c> with the lowest free n (scrolling rotates the rows, so the
        /// last one is not always the highest number).
        /// </summary>
        private string NewRowId()
        {
            int n = 0;
            string id = $"{Id}.r{n}";
            while (rows.Exists(r => r.Id == id))
            {
                n++;
                id = $"{Id}.r{n}";
            }

            return id;
        }

        /// <summary>
        /// Tree order follows the rows as shown (scrolling rotates the row containers without moving them among the
        /// children), so Tab reaches cell content top to bottom; any other child comes after.
        /// </summary>
        internal override IEnumerable<UIElement> SelfAndDescendants()
        {
            yield return this;
            foreach (DataGridRow row in rows)
            {
                foreach (UIElement e in row.SelfAndDescendants())
                {
                    yield return e;
                }
            }

            foreach (UIElement child in Children)
            {
                if (child is DataGridRow)
                {
                    continue;
                }

                foreach (UIElement e in child.SelfAndDescendants())
                {
                    yield return e;
                }
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

            // the per-frame delegates are built here once per row change: the guard key and the inner call are captured
            // instead of being allocated on every read
            string? rowKey = column.CellTooltipFunc != null || (column.BuildCellFunc == null && column.TextFunc != null) ? RowGuardKey(column, item) : null;
            Func<int, string>? tooltip = column.CellTooltipFunc;
            if (tooltip != null)
            {
                Func<string> readTooltip = () => tooltip(item);
                cell.Tooltip = () => Consumer.Invoke(rowKey!, "CellTooltip", readTooltip, string.Empty);
            }

            Action<int, IUIContainer>? build = column.BuildCellFunc;
            if (build != null)
            {
                GuardColumn(column, "BuildCell", () => build(item, cell));
                return;
            }

            Func<int, string>? text = column.TextFunc;
            Func<string>? readText = text == null ? null : () => text(item);
            var label = new Label(cell.Id + ".text", readText == null ? null : () => Consumer.Invoke(rowKey!, "Text", readText, string.Empty) ?? string.Empty)
            {
                HorizontalAlign = UIAlign.Stretch,
                TextAlign = column.Align == UIAlign.Stretch ? UIAlign.Start : column.Align
            };
            cell.Add(label);
        }

        /// <summary>Clamp and apply a first display position; rotates the rows still shown, rebuilds the rows whose item changed and raises <see cref="OnScroll"/>. Returns true if it changed.</summary>
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
            // a short scroll keeps the rows still on screen: rotate the containers and rebuild only the rows that scrolled in
            if (Math.Abs(delta) < rows.Count)
            {
                ListView.Rotate(rows, delta);
            }

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
    }
}
