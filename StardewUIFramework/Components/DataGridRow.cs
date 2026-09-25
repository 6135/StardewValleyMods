using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Components
{
    /// <summary>
    /// One visible row of a <see cref="DataGrid"/>: a cell container per column, placed at the column offsets the
    /// grid resolved. The row itself is the click / hover target for its item (cells only take the pointer when a
    /// tooltip or handler was attached to them).
    /// </summary>
    internal sealed class DataGridRow : UIContainer
    {
        private readonly DataGrid owner;
        private readonly List<DataGridCell> cells = new();

        internal DataGridRow(DataGrid owner, string id) : base(id)
        {
            this.owner = owner;
        }

        /// <summary>Cell containers in column order.</summary>
        internal IReadOnlyList<DataGridCell> Cells => cells;

        /// <summary>Underlying row index shown, or -1 while the row is past the end.</summary>
        internal int Item { get; set; } = -1;

        // cells fill their column slot exactly (their children align themselves inside)
        internal override UIAlign DefaultChildHorizontalAlign(UIElement child) => UIAlign.Stretch;
        internal override UIAlign DefaultChildVerticalAlign(UIElement child) => UIAlign.Stretch;

        /// <summary>Create / remove cells so exactly <paramref name="count"/> exist.</summary>
        internal void SyncCells(int count)
        {
            while (cells.Count < count)
            {
                var cell = new DataGridCell($"{Id}.c{cells.Count}");
                cells.Add(cell);
                Add(cell);
            }
            while (cells.Count > count)
            {
                int last = cells.Count - 1;
                DataGridCell cell = cells[last];
                cells.RemoveAt(last);
                Remove(cell);
            }
        }

        protected override Vector2 MeasureCore(Vector2 available)
        {
            for (int j = 0; j < cells.Count && j < owner.ColumnCount; j++)
            {
                cells[j].Measure(new Vector2(owner.CellContentWidth(j), available.Y));
            }
            return new Vector2(available.X, available.Y);
        }

        /// <summary>The cells sit on the grid's columns, so a row is as narrow as the columns can get.</summary>
        protected override float MinWidthCore() => owner.ColumnsMinWidth();

        protected override void ArrangeCore()
        {
            // a column added / removed since the last refresh is reconciled by the next Refresh; skip the mismatch
            for (int j = 0; j < cells.Count && j < owner.ColumnCount; j++)
            {
                cells[j].Arrange(owner.CellRect(j, Bounds));
            }
        }
    }

    /// <summary>A cell of a <see cref="DataGridRow"/>: children overlap, filling the cell and centered vertically by default.</summary>
    internal sealed class DataGridCell : UIContainer
    {
        internal DataGridCell(string id) : base(id) { }

        internal override UIAlign DefaultChildVerticalAlign(UIElement child) => UIAlign.Center;

        // the row handles clicks; a cell only takes the pointer for its own tooltip / handlers
        protected override bool IsHitTestVisible => HasPointerHandlers;

        protected override Vector2 MeasureCore(Vector2 available)
        {
            float w = 0, h = 0;
            foreach (UIElement child in Children)
            {
                Vector2 size = child.Measure(available);
                w = Math.Max(w, size.X);
                h = Math.Max(h, size.Y);
            }
            return new Vector2(w, h);
        }

        /// <summary>Children overlap: as narrow as the widest child.</summary>
        protected override float MinWidthCore() => MaxChildMinWidth();

        protected override void ArrangeCore()
        {
            foreach (UIElement child in Children)
            {
                child.Arrange(Bounds);
            }
        }
    }
}
