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
    internal sealed partial class DataGrid
    {
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

        /// <summary>
        /// Resolve every column's width against <paramref name="contentWidth"/> (star columns absorb the rest); returns
        /// the total. Auto columns take their content width, unless together they want more than the star and pixel
        /// columns' minimums leave: then that room is shared by <see cref="FitAutoColumns"/>.
        /// </summary>
        private float ResolveColumns(float contentWidth)
        {
            int count = columns.Count;
            if (resolveTracks.Length != count)
            {
                resolveTracks = new GridTrack[count];
                resolveAuto = new float[count];
                fitMin = new float[count];
                fitNatural = new float[count];
            }

            GridTrack[] tracks = resolveTracks;
            float[] auto = resolveAuto;
            for (int j = 0; j < count; j++)
            {
                tracks[j] = columns[j].Track;
                auto[j] = tracks[j].Type == GridTrack.Kind.Pixels ? 0 : AutoWidth(j);
            }

            if (!float.IsInfinity(contentWidth) && !float.IsNaN(contentWidth))
            {
                FitAutoColumns(contentWidth);
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

        /// <summary>
        /// Keep the auto columns inside <paramref name="contentWidth"/>: when their content widths (raised to
        /// <see cref="DataGridColumn.MinWidth"/>) do not fit what the pixel columns and the star columns' minimums
        /// (<see cref="StarMinSpace"/>) leave, share that room by <see cref="LayoutEngine.DistributeWidth"/> — every
        /// auto column its minimum (<see cref="ColumnMinWidth"/>) first, then the rest by how much its content wants
        /// beyond it — and write the shares into the auto sizes <see cref="ResolveColumns"/> resolves. With room to
        /// spare nothing changes (and no minimum is computed).
        /// </summary>
        private void FitAutoColumns(float contentWidth)
        {
            int count = columns.Count;
            float reserved = 0, naturalTotal = 0;
            bool anyStar = false;
            for (int j = 0; j < count; j++)
            {
                DataGridColumn column = columns[j];
                switch (column.Track.Type)
                {
                    case GridTrack.Kind.Pixels:
                        reserved += ColumnMinWidth(j);
                        break;
                    case GridTrack.Kind.Auto:
                        naturalTotal += Math.Max(resolveAuto[j], column.MinWidth);
                        break;
                    default:
                        anyStar = true;
                        break;
                }
            }

            // star columns keep room for their minimums; without any, the pixel and auto columns alone decide
            if (!anyStar && reserved + naturalTotal <= contentWidth)
            {
                return;
            }

            if (anyStar)
            {
                reserved += StarMinSpace();
            }

            if (reserved + naturalTotal <= contentWidth)
            {
                return;
            }

            for (int j = 0; j < count; j++)
            {
                bool auto = columns[j].Track.Type == GridTrack.Kind.Auto;
                fitMin[j] = auto ? ColumnMinWidth(j) : 0;
                fitNatural[j] = auto ? Math.Max(resolveAuto[j], columns[j].MinWidth) : 0;
            }

            // the shares are written straight over the natural widths (the helper allows it)
            LayoutEngine.DistributeWidth(fitMin, fitNatural, contentWidth - reserved, fitNatural);
            for (int j = 0; j < count; j++)
            {
                if (columns[j].Track.Type == GridTrack.Kind.Auto)
                {
                    resolveAuto[j] = fitNatural[j];
                }
            }
        }

        /// <summary>Content width of a column: the header (plus sort arrow) or the widest visible cell, with padding.</summary>
        private float AutoWidth(int column)
        {
            float width = HeaderWidth(columns[column]);
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

        /// <summary>Width of a column's header title, plus the sort arrow when the column sorts (no padding).</summary>
        private float HeaderWidth(DataGridColumn column)
        {
            float width = UIServices.Text.Measure(UIFont.Small, column.HeaderText, 1f).X;
            if (column.Sortable)
            {
                width += ArrowGap + (Theme.ScrollUpArrow.Width * ArrowScale);
            }

            return width;
        }

        /// <summary>
        /// Narrowest width a column's header can take: its title fitted like <see cref="DrawHeaderText"/> draws it
        /// (<see cref="DrawHelper.FitTextMinWidth"/>), plus the sort arrow when the column sorts (no padding). Unlike the
        /// cells (single-line labels, which keep their whole text), a header may shorten: it only names the column, and
        /// <see cref="DataGridColumn.MinWidth"/> or a pixel column is how a consumer keeps it whole.
        /// </summary>
        private float HeaderMinWidth(DataGridColumn column)
        {
            float width = DrawHelper.FitTextMinWidth(column.HeaderText, UIFont.Small, 1f);
            if (column.Sortable)
            {
                width += ArrowGap + (Theme.ScrollUpArrow.Width * ArrowScale);
            }

            return width;
        }

        /// <summary>
        /// Narrowest total column width that fits, mirroring <see cref="ResolveColumns"/> without resolving anything:
        /// pixel columns at their width, auto and star columns at their content minimum (<see cref="MinContentWidth"/>),
        /// each raised to its <see cref="DataGridColumn.MinWidth"/>. Star columns split the space left by weight, so
        /// that space must give every star column at least its minimum width.
        /// </summary>
        internal float ColumnsMinWidth()
        {
            float fixedTotal = 0;
            for (int j = 0; j < columns.Count; j++)
            {
                if (columns[j].Track.Type != GridTrack.Kind.Star)
                {
                    fixedTotal += ColumnMinWidth(j);
                }
            }

            return (float)Math.Ceiling(fixedTotal + StarMinSpace());
        }

        /// <summary>
        /// Narrowest width of one column: a pixel column at its width, any other at its content minimum
        /// (<see cref="MinContentWidth"/>), raised to its <see cref="DataGridColumn.MinWidth"/>.
        /// </summary>
        private float ColumnMinWidth(int column)
        {
            DataGridColumn c = columns[column];
            float min = c.Track.Type == GridTrack.Kind.Pixels ? c.Track.Value : MinContentWidth(column);
            return Math.Max(min, c.MinWidth);
        }

        /// <summary>
        /// Width the star columns need: weighted ones split their space by weight, so it must give every one of them
        /// at least its minimum; a weightless star column never gets a share, only its minimum.
        /// </summary>
        private float StarMinSpace()
        {
            float starTotal = 0, weightless = 0, starSpace = 0;
            for (int j = 0; j < columns.Count; j++)
            {
                GridTrack track = columns[j].Track;
                if (track.Type != GridTrack.Kind.Star)
                {
                    continue;
                }

                if (track.Value > 0)
                {
                    starTotal += track.Value;
                }
                else
                {
                    weightless += ColumnMinWidth(j);
                }
            }

            for (int j = 0; j < columns.Count; j++)
            {
                GridTrack track = columns[j].Track;
                if (track.Type == GridTrack.Kind.Star && track.Value > 0)
                {
                    starSpace = Math.Max(starSpace, ColumnMinWidth(j) * starTotal / track.Value);
                }
            }

            return weightless + starSpace;
        }

        /// <summary>
        /// Minimum content width of a column: like <see cref="AutoWidth"/>, but with the header's fitted minimum
        /// (<see cref="HeaderMinWidth"/>) and asking the visible cells for their minimum instead of measuring them.
        /// </summary>
        private float MinContentWidth(int column)
        {
            float width = HeaderMinWidth(columns[column]);
            foreach (DataGridRow row in rows)
            {
                if (row.Visible && column < row.Cells.Count)
                {
                    width = Math.Max(width, row.Cells[column].MeasureMinWidth());
                }
            }
            return width + (2 * CellPadX);
        }

        /// <summary>The columns' minimum plus the always-reserved scrollbar column.</summary>
        protected override float MinWidthCore() => ColumnsMinWidth() + ScrollbarGadget.ReservedWidth;

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
    }
}
