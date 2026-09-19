using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    /// <summary>
    /// Virtualized list (the <c>ProfitCalculatorResultsList</c> pattern): exactly <see cref="VisibleRows"/> row
    /// containers exist, each <see cref="RowHeight"/> tall and the full content width. Row <c>i</c> shows item
    /// <see cref="FirstVisibleIndex"/> + <c>i</c>; whenever that mapping changes the row is cleared and the consumer's
    /// <c>buildRow(index, row)</c> fills it again. Rows are never rebuilt while the children are being updated or
    /// drawn. A vanilla scrollbar in row units sits on the right; the wheel and the arrows move one row.
    /// When <see cref="Selectable"/>, clicking anywhere in a row selects its item and raises <see cref="OnValueChanged"/>.
    /// </summary>
    internal sealed class ListView : UIContainer, IUIList
    {
        private readonly Func<int>? itemCount;
        private readonly Action<int, IUIContainer>? buildRow;
        private readonly List<Panel> rows = new();
        private readonly List<int> rowItems = new();
        private readonly ScrollbarGadget scrollbar = new();
        private int rowHeight;
        private int visibleRows;
        private int firstVisibleIndex;
        private int lastCount;
        private bool needsRefresh = true;
        private bool selectable;
        private int selectedIndex = -1;
        private bool dragging;

        internal ListView(string id, int rowHeight, int visibleRows, Func<int>? itemCount, Action<int, IUIContainer>? buildRow) : base(id)
        {
            this.rowHeight = Math.Max(1, rowHeight);
            this.visibleRows = Math.Max(1, visibleRows);
            this.itemCount = itemCount;
            this.buildRow = buildRow;
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

        /// <summary>Index of the item shown in the first row, clamped to [0, count - <see cref="VisibleRows"/>]. Changing it rebuilds the rows that moved and raises <see cref="OnScroll"/>.</summary>
        public int FirstVisibleIndex
        {
            get => firstVisibleIndex;
            set => SetFirstVisible(value);
        }

        /// <summary>The item count as the data source reports it right now (never negative).</summary>
        public int ItemCount => Math.Max(0, Raise("itemCount", itemCount, 0));

        public bool Selectable
        {
            get => selectable;
            set => selectable = value;
        }

        /// <summary>Selected item index or -1. Setting it does not raise <see cref="OnValueChanged"/>.</summary>
        public int SelectedIndex
        {
            get => selectedIndex;
            set => selectedIndex = Math.Max(-1, value);
        }

        internal Action<IUIValueEvent>? OnValueChanged { get; set; }

        Action<IUIValueEvent> IUIList.OnValueChanged { get => OnValueChanged!; set => OnValueChanged = value; }

        internal Action<int>? OnScroll { get; set; }

        Action<int> IUIList.OnScroll { get => OnScroll!; set => OnScroll = value; }

        public void ScrollTo(int firstIndex) => SetFirstVisible(firstIndex);

        internal override string AccessibleDescription
        {
            get
            {
                string items = Accessibility.Text("list-items", "{{count}} items").Replace("{{count}}", lastCount.ToString());
                return Accessibility.Compose(Accessibility.Text("list", "List"), items, selectedIndex >= 0 ? RowDescription(selectedIndex) : null);
            }
        }

        /// <summary>"Row n of count: &lt;row text&gt;" for an item, using the row container's labels when it is on screen.</summary>
        private string RowDescription(int item)
        {
            string row = Accessibility.Text("row", "Row {{index}} of {{count}}").Replace("{{index}}", (item + 1).ToString()).Replace("{{count}}", lastCount.ToString());
            int slot = rowItems.IndexOf(item);
            return Accessibility.Compose(row, slot >= 0 ? Accessibility.TextOf(rows[slot]) : null);
        }

        // rows fill their slot exactly
        internal override UIAlign DefaultChildHorizontalAlign(UIElement child) => UIAlign.Stretch;
        internal override UIAlign DefaultChildVerticalAlign(UIElement child) => UIAlign.Stretch;

        // ---------------------------------------------------------------------------------------------------------
        //  Rows
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Largest valid <see cref="FirstVisibleIndex"/> for the cached count.</summary>
        private int MaxFirstIndex => Math.Max(0, lastCount - visibleRows);

        /// <summary>Whether the scrollbar is drawn / interactive.</summary>
        private bool ScrollbarVisible => lastCount > visibleRows;

        /// <summary>Create / remove row containers so exactly <see cref="VisibleRows"/> exist as children.</summary>
        private void EnsureRowContainers()
        {
            while (rows.Count < visibleRows)
            {
                var row = new Panel($"{Id}.row{rows.Count}", drawBox: false, padding: 0);
                rows.Add(row);
                rowItems.Add(-1);
                Add(row);
            }
            while (rows.Count > visibleRows)
            {
                int last = rows.Count - 1;
                Panel row = rows[last];
                rows.RemoveAt(last);
                rowItems.RemoveAt(last);
                Remove(row);
            }
        }

        /// <summary>Re-query the item count and rebuild every row.</summary>
        public void Refresh()
        {
            needsRefresh = false;
            lastCount = ItemCount;
            firstVisibleIndex = Math.Clamp(firstVisibleIndex, 0, MaxFirstIndex);
            for (int i = 0; i < rows.Count; i++)
            {
                BuildRow(i, firstVisibleIndex + i);
            }

            InvalidateLayout();
        }

        /// <summary>Run <see cref="Refresh"/> if the list was never refreshed (so index math sees a real count).</summary>
        private void EnsureFresh()
        {
            if (needsRefresh)
            {
                Refresh();
            }
        }

        /// <summary>Point row <paramref name="row"/> at item <paramref name="item"/>: clear it, hide it past the end, otherwise let the consumer fill it.</summary>
        private void BuildRow(int row, int item)
        {
            Panel container = rows[row];
            container.Clear();
            if (item < 0 || item >= lastCount)
            {
                rowItems[row] = -1;
                container.Visible = false;
                return;
            }
            rowItems[row] = item;
            container.Visible = true;
            if (buildRow != null)
            {
                Action<int, IUIContainer> build = buildRow;
                Raise("buildRow", () => build(item, container));
            }
        }

        /// <summary>Clamp and apply a first index; rebuilds the rows whose item changed and raises <see cref="OnScroll"/>. Returns true if it changed.</summary>
        private bool SetFirstVisible(int value)
        {
            EnsureFresh();
            value = Math.Clamp(value, 0, MaxFirstIndex);
            if (firstVisibleIndex == value)
            {
                return false;
            }

            int delta = value - firstVisibleIndex;
            firstVisibleIndex = value;
            for (int i = 0; i < rows.Count; i++)
            {
                int item = firstVisibleIndex + i;
                if (rowItems[i] != (item < lastCount ? item : -1))
                {
                    BuildRow(i, item);
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

        /// <summary>Scroll so the thumb's center follows the cursor (thumb drag / track click).</summary>
        private void SetFirstVisibleFromY(int py)
        {
            int target = (int)Math.Round(scrollbar.FractionFromY(py) * MaxFirstIndex);
            if (SetFirstVisible(target))
            {
                UIServices.PlaySound(Theme.ScrollSound);
            }
        }

        /// <summary>Select an item from a click and raise <see cref="OnValueChanged"/>.</summary>
        private void Select(int item)
        {
            if (selectedIndex == item)
            {
                return;
            }

            int old = selectedIndex;
            selectedIndex = item;
            if (OnValueChanged != null)
            {
                Action<IUIValueEvent> cb = OnValueChanged;
                UIValueEvent e = UIValueEvent.Index(this, old, item, old.ToString(), item.ToString());
                Raise("OnValueChanged", () => cb(e));
            }
            if (Accessibility.Enabled)
            {
                Accessibility.Announce(RowDescription(item));
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Layout
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Absolute rectangle holding the rows (bounds minus the scrollbar column).</summary>
        private Rectangle ContentRect => new(Bounds.X, Bounds.Y, Math.Max(0, Bounds.Width - ScrollbarGadget.ReservedWidth), Bounds.Height);

        protected override Vector2 MeasureCore(Vector2 available)
        {
            const int reserved = ScrollbarGadget.ReservedWidth;
            var rowAvailable = new Vector2(Math.Max(0, available.X - reserved), rowHeight);
            float maxRowWidth = 0;
            foreach (UIElement child in Children)
            {
                if (!child.Visible)
                {
                    continue;
                }

                maxRowWidth = Math.Max(maxRowWidth, child.Measure(rowAvailable).X);
            }
            // fill the available width so rows are wide; size to content only when the width is unbounded
            float width = float.IsInfinity(available.X) || float.IsNaN(available.X) ? maxRowWidth + reserved : Math.Max(available.X, maxRowWidth + reserved);
            return new Vector2(width, visibleRows * rowHeight);
        }

        protected override void ArrangeCore()
        {
            Rectangle content = ContentRect;
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].Arrange(new Rectangle(content.X, content.Y + (i * rowHeight), content.Width, rowHeight));
            }
            // any other child a consumer added directly overlaps the content area
            foreach (UIElement child in Children)
            {
                if (child is not Panel p || !rows.Contains(p))
                {
                    child.Arrange(content);
                }
            }

            int max = MaxFirstIndex;
            scrollbar.Layout(Bounds.Right - ScrollbarGadget.Width, Bounds.Y, Bounds.Height, max > 0 ? firstVisibleIndex / (float)max : 0f);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Update / draw
        // ---------------------------------------------------------------------------------------------------------

        internal override void Update(double elapsedMs)
        {
            // rebuild before (never while) the children are iterated
            if (needsRefresh || ItemCount != lastCount)
            {
                Refresh();
            }

            base.Update(elapsedMs);
        }

        protected override void DrawCore(SpriteBatch b)
        {
            if (selectable && selectedIndex >= 0)
            {
                Color selection = Style.HoverColor * 0.5f;
                for (int i = 0; i < rows.Count; i++)
                {
                    if (rowItems[i] == selectedIndex && rows[i].Visible)
                    {
                        DrawHelper.Fill(b, rows[i].Bounds, selection);
                    }
                }
            }
            DrawChildren(b);
            if (ScrollbarVisible)
            {
                scrollbar.Draw(b);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Input
        // ---------------------------------------------------------------------------------------------------------

        protected internal override bool HandleClick(UIClickEvent e)
        {
            if (e.Button != UIMouseButton.Left)
            {
                return base.HandleClick(e);
            }

            if (e.Target == this && ScrollbarVisible && HandleScrollbarClick(e.X, e.Y))
            {
                return true;
            }

            // a click on a row (or bubbling up from anything inside it) selects that row's item
            if (selectable && ContentRect.Contains(e.X, e.Y))
            {
                SelectRowAt(e.Y);
            }

            return base.HandleClick(e);
        }

        /// <summary>Arrows step one row, the thumb starts a drag, the track jumps. Returns false when no scrollbar part was hit.</summary>
        private bool HandleScrollbarClick(int px, int py)
        {
            switch (scrollbar.HitTest(px, py))
            {
                case ScrollbarGadget.Part.UpArrow:
                    ScrollWithSound(-1);
                    return true;
                case ScrollbarGadget.Part.DownArrow:
                    ScrollWithSound(1);
                    return true;
                case ScrollbarGadget.Part.Thumb:
                    dragging = true;
                    return true;
                case ScrollbarGadget.Part.Track:
                    dragging = true;
                    SetFirstVisibleFromY(py);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Scroll by <paramref name="rows"/> rows, with the vanilla scroll sound when something moved.</summary>
        private void ScrollWithSound(int rows)
        {
            if (SetFirstVisible(firstVisibleIndex + rows))
            {
                UIServices.PlaySound(Theme.ScrollSound);
            }
        }

        /// <summary>Select the item shown in the row under <paramref name="py"/>, if any.</summary>
        private void SelectRowAt(int py)
        {
            int row = (py - Bounds.Y) / rowHeight;
            if (row >= 0 && row < rows.Count && rowItems[row] >= 0)
            {
                Select(rowItems[row]);
            }
        }

        protected internal override void HandleClickHeld(int px, int py)
        {
            if (dragging)
            {
                SetFirstVisibleFromY(py);
            }
        }

        protected internal override void HandleClickRelease(int px, int py)
        {
            dragging = false;
        }

        /// <summary>Wheel: one row per notch; unhandled (falls through) when already at the end.</summary>
        protected internal override bool HandleScroll(int direction)
        {
            if (direction == 0)
            {
                return false;
            }

            if (!SetFirstVisible(firstVisibleIndex + (direction > 0 ? -1 : 1)))
            {
                return false;
            }

            UIServices.PlaySound(Theme.ScrollSound);
            return true;
        }
    }
}
