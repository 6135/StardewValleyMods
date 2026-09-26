using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    /// <summary>
    /// Rows and columns with <c>auto</c>, pixel and star tracks (see <see cref="LayoutEngine.ParseTracks"/>).
    /// Children pick their cell through <see cref="UIElement.Row"/> / <see cref="UIElement.Column"/> and the span
    /// properties; a child placed past the defined tracks extends the grid with implicit <c>auto</c> tracks.
    /// <para>
    /// WPF-lite: <see cref="MeasureCore"/> first measures pixel and auto cells for their natural width, derives the
    /// columns' content sizes, shares the room the other tracks' minimums leave between the auto columns when they want
    /// more (<see cref="LayoutEngine.DistributeWidth"/>, so they wrap rather than starve their neighbours) and resolves
    /// them against the available width; star and spanning cells are then measured at their resolved
    /// width, and the rows are sized from those final heights (spanning children spread their excess over the spanned
    /// weighted star tracks, else evenly over the non-pixel ones). <see cref="ArrangeCore"/> resolves them again against the final
    /// <see cref="UIElement.Bounds"/> so star tracks fill the real width / height, then hands each child its cell.
    /// A child's own alignment places it inside the cell.
    /// </para>
    /// </summary>
    internal sealed class Grid : UIContainer, IUIGrid
    {
        private string columns;
        private string rows;
        private int columnSpacing;
        private int rowSpacing;
        private List<GridTrack> columnTracks;
        private List<GridTrack> rowTracks;

        // state carried from measure to arrange
        private List<GridTrack> effectiveColumns;
        private List<GridTrack> effectiveRows;
        private float[] columnAuto = Array.Empty<float>();
        private float[] rowAuto = Array.Empty<float>();
        private float[] columnSizes = Array.Empty<float>();
        private float[] rowSizes = Array.Empty<float>();

        // measure scratch, reused between measures (hidden children take no cell)
        private float[] columnMin = Array.Empty<float>();
        private float[] fitMin = Array.Empty<float>();
        private float[] fitNatural = Array.Empty<float>();
        private float[] fitWidth = Array.Empty<float>();

        /// <summary>Width each child (by index) was last measured at in this measure; NaN when it waited for pass 2.</summary>
        private float[] measuredWidth = Array.Empty<float>();

        internal Grid(string id, string columns, string rows) : base(id)
        {
            this.columns = columns ?? "*";
            this.rows = rows ?? "auto";
            columnTracks = LayoutEngine.ParseTracks(this.columns);
            rowTracks = LayoutEngine.ParseTracks(this.rows);
            effectiveColumns = columnTracks;
            effectiveRows = rowTracks;
        }

        /// <summary>Column definitions, comma separated (<c>auto</c>, <c>120px</c>, <c>*</c>, <c>2*</c>).</summary>
        public string Columns
        {
            get => columns;
            set
            {
                value ??= "*";
                if (columns == value)
                {
                    return;
                }

                columns = value;
                columnTracks = LayoutEngine.ParseTracks(value);
                InvalidateLayout();
            }
        }

        /// <summary>Row definitions, comma separated (<c>auto</c>, <c>48px</c>, <c>*</c>).</summary>
        public string Rows
        {
            get => rows;
            set
            {
                value ??= "auto";
                if (rows == value)
                {
                    return;
                }

                rows = value;
                rowTracks = LayoutEngine.ParseTracks(value);
                InvalidateLayout();
            }
        }

        public int ColumnSpacing
        {
            get => columnSpacing;
            set
            {
                value = Math.Max(0, value);
                if (columnSpacing == value)
                {
                    return;
                }

                columnSpacing = value;
                InvalidateLayout();
            }
        }

        public int RowSpacing
        {
            get => rowSpacing;
            set
            {
                value = Math.Max(0, value);
                if (rowSpacing == value)
                {
                    return;
                }

                rowSpacing = value;
                InvalidateLayout();
            }
        }

        /// <summary>Spacing actually used (theme scaled).</summary>
        private int ColSpace => Theme.Space(columnSpacing);

        private int RowSpace => Theme.Space(rowSpacing);

        // a grid is layout-only: clicks on the gaps fall through unless it has a handler / tooltip
        protected override bool IsHitTestVisible => HasPointerHandlers;

        /// <summary>Resolved column widths from the last arrange (inspector overlay).</summary>
        internal float[] ColumnSizes => columnSizes;

        /// <summary>Resolved row heights from the last arrange (inspector overlay).</summary>
        internal float[] RowSizes => rowSizes;

        // ---------------------------------------------------------------------------------------------------------
        //  Tracks
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The defined tracks plus implicit auto tracks up to <paramref name="needed"/>.</summary>
        private static List<GridTrack> Extend(List<GridTrack> defined, int needed)
        {
            if (needed <= defined.Count)
            {
                return defined;
            }

            var result = new List<GridTrack>(needed);
            result.AddRange(defined);
            while (result.Count < needed)
            {
                result.Add(GridTrack.Auto);
            }

            return result;
        }

        /// <summary>Column start / span of a child, clamped into [0, <paramref name="count"/>).</summary>
        private static void ColumnCell(UIElement child, int count, out int start, out int span)
        {
            start = Math.Clamp(child.Column, 0, Math.Max(0, count - 1));
            span = Math.Clamp(child.ColumnSpan, 1, Math.Max(1, count - start));
        }

        /// <summary>Row start / span of a child, clamped into [0, <paramref name="count"/>).</summary>
        private static void RowCell(UIElement child, int count, out int start, out int span)
        {
            start = Math.Clamp(child.Row, 0, Math.Max(0, count - 1));
            span = Math.Clamp(child.RowSpan, 1, Math.Max(1, count - start));
        }

        /// <summary>True if every track in [start, start + span) is a pixel track.</summary>
        private static bool AllPixels(List<GridTrack> tracks, int start, int span)
        {
            for (int i = start; i < start + span && i < tracks.Count; i++)
            {
                if (tracks[i].Type != GridTrack.Kind.Pixels)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Sum of the pixel tracks in [start, start + span) plus the spacing between them.</summary>
        private static float PixelSpan(List<GridTrack> tracks, int start, int span, int spacing)
        {
            float total = 0;
            int end = Math.Min(tracks.Count, start + span);
            for (int i = start; i < end; i++)
            {
                total += tracks[i].Value;
            }

            return total + (Math.Max(0, end - start - 1) * spacing);
        }

        /// <summary>True if [start, start + span) holds a star track with a positive weight (one that shares the leftover space).</summary>
        private static bool SpansWeightedStar(List<GridTrack> tracks, int start, int span)
        {
            for (int i = start; i < start + span && i < tracks.Count; i++)
            {
                if (tracks[i].Type == GridTrack.Kind.Star && tracks[i].Value > 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Grow the sizes of the spanned tracks so the span fits <paramref name="desired"/>. When the span holds a
        /// weighted star track the excess goes to its star tracks by weight (they absorb the leftover space, so the
        /// auto tracks keep their own content size); otherwise it is spread evenly over the non-pixel tracks.
        /// </summary>
        private static void DistributeSpan(List<GridTrack> tracks, float[] auto, int start, int span, int spacing, float desired)
        {
            float current = LayoutEngine.SpanSize(auto, start, span, spacing);
            float excess = desired - current;
            if (excess <= 0)
            {
                return;
            }

            int end = Math.Min(tracks.Count, start + span);
            int flexible = 0;
            float starWeight = 0;
            for (int i = start; i < end; i++)
            {
                if (tracks[i].Type != GridTrack.Kind.Pixels)
                {
                    flexible++;
                }

                if (tracks[i].Type == GridTrack.Kind.Star && tracks[i].Value > 0)
                {
                    starWeight += tracks[i].Value;
                }
            }

            if (starWeight > 0)
            {
                for (int i = start; i < end; i++)
                {
                    if (tracks[i].Type == GridTrack.Kind.Star && tracks[i].Value > 0)
                    {
                        auto[i] += excess * tracks[i].Value / starWeight;
                    }
                }
                return;
            }

            if (flexible == 0)
            {
                return;
            }

            float share = excess / flexible;
            for (int i = start; i < end; i++)
            {
                if (tracks[i].Type != GridTrack.Kind.Pixels)
                {
                    auto[i] += share;
                }
            }
        }

        private static float Sum(float[] sizes)
        {
            float total = 0;
            foreach (float s in sizes)
            {
                total += s;
            }

            return total;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Layout
        // ---------------------------------------------------------------------------------------------------------

        protected override Vector2 MeasureCore(Vector2 available)
        {
            ResolveEffectiveTracks();
            float colSpacingTotal = ColSpace * Math.Max(0, effectiveColumns.Count - 1);
            float rowSpacingTotal = RowSpace * Math.Max(0, effectiveRows.Count - 1);

            // pass 1: the columns' content sizes, the content-sized columns fitted into the room the other tracks leave,
            // then the columns resolved against the available width
            bool bounded = !float.IsInfinity(available.X) && !float.IsNaN(available.X);
            float slack = 0;
            if (bounded)
            {
                columnMin = PixelSizes(effectiveColumns, columnMin);
                FillColumnMinimums(effectiveColumns, columnMin);
                slack = available.X - colSpacingTotal - MinimumTotal(effectiveColumns, columnMin);
            }

            MeasureColumnContent(available, bounded, Math.Max(0, slack));
            ComputeColumnSizes();
            if (bounded)
            {
                FitContentColumns(slack, available.Y);
            }

            float[] colSizes = LayoutEngine.ResolveTracks(effectiveColumns, columnAuto, available.X - colSpacingTotal);

            // pass 2: star / spanning children at their final cell width, then the rows from the final heights
            RemeasureAtCellWidths(colSizes, available.Y);
            ComputeRowSizes();
            float[] rowSizes = LayoutEngine.ResolveTracks(effectiveRows, rowAuto, available.Y - rowSpacingTotal);
            return new Vector2(Sum(colSizes) + colSpacingTotal, Sum(rowSizes) + rowSpacingTotal);
        }

        /// <summary>Step 1: the explicit tracks, extended with implicit auto tracks for children placed beyond them; pixel tracks pre-fill their auto size.</summary>
        private void ResolveEffectiveTracks()
        {
            int neededColumns = columnTracks.Count;
            int neededRows = rowTracks.Count;
            for (int k = 0; k < Children.Count; k++)
            {
                UIElement child = Children[k];
                if (child.Visible)
                {
                    neededColumns = Math.Max(neededColumns, child.Column + child.ColumnSpan);
                    neededRows = Math.Max(neededRows, child.Row + child.RowSpan);
                }
            }
            effectiveColumns = Extend(columnTracks, neededColumns);
            effectiveRows = Extend(rowTracks, neededRows);
            columnAuto = PixelSizes(effectiveColumns, columnAuto);
            rowAuto = PixelSizes(effectiveRows, rowAuto);
        }

        /// <summary>
        /// Pixel tracks sized up front (so their auto size still reads sensibly); everything else 0. Fills
        /// <paramref name="reuse"/> when it has the right length, otherwise a new array.
        /// </summary>
        private static float[] PixelSizes(List<GridTrack> tracks, float[]? reuse)
        {
            float[] sizes = reuse != null && reuse.Length == tracks.Count ? reuse : new float[tracks.Count];
            for (int i = 0; i < tracks.Count; i++)
            {
                sizes[i] = tracks[i].Type == GridTrack.Kind.Pixels ? tracks[i].Value : 0;
            }

            return sizes;
        }

        /// <summary>
        /// Raise the non-pixel entries of <paramref name="min"/> (pre-filled by <see cref="PixelSizes"/>) to the widest
        /// minimum width of their single-track children, then let spanning children spread what their span still lacks
        /// (<see cref="DistributeSpan"/>). Pure with respect to the children.
        /// </summary>
        private void FillColumnMinimums(List<GridTrack> tracks, float[] min)
        {
            int count = tracks.Count;
            for (int k = 0; k < Children.Count; k++)
            {
                UIElement child = Children[k];
                if (!child.Visible)
                {
                    continue;
                }

                ColumnCell(child, count, out int col, out int span);
                if (span == 1 && tracks[col].Type != GridTrack.Kind.Pixels)
                {
                    min[col] = Math.Max(min[col], child.MeasureMinWidth());
                }
            }

            for (int k = 0; k < Children.Count; k++)
            {
                UIElement child = Children[k];
                if (!child.Visible)
                {
                    continue;
                }

                ColumnCell(child, count, out int col, out int span);
                if (span > 1)
                {
                    DistributeSpan(tracks, min, col, span, ColSpace, child.MeasureMinWidth());
                }
            }
        }

        /// <summary>
        /// Narrowest total of the tracks (spacing excluded) for the per-track minimums <paramref name="min"/>: pixel,
        /// auto and weightless star tracks at their minimum; weighted star tracks split the space left by weight
        /// (<see cref="LayoutEngine.ResolveTracks"/>), so that space must give every one of them at least its minimum.
        /// </summary>
        private static float MinimumTotal(List<GridTrack> tracks, float[] min)
        {
            float fixedTotal = 0, starTotal = 0, starMinTotal = 0;
            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].Type == GridTrack.Kind.Star)
                {
                    starTotal += tracks[i].Value;
                    starMinTotal += min[i];
                }
                else
                {
                    fixedTotal += min[i];
                }
            }

            // with no weight to share by, star tracks fall back to their content size
            float starSpace = starTotal > 0 ? 0 : starMinTotal;
            for (int i = 0; i < tracks.Count && starTotal > 0; i++)
            {
                if (tracks[i].Type == GridTrack.Kind.Star && tracks[i].Value > 0)
                {
                    starSpace = Math.Max(starSpace, min[i] * starTotal / tracks[i].Value);
                }
            }

            return fixedTotal + starSpace;
        }

        /// <summary>
        /// Pass 1: measure every child whose width does not depend on the star tracks' share, for the natural width of
        /// its column. Pixel cells give their size. Other cells are offered their own minimum plus
        /// <paramref name="slack"/>, whatever the whole grid has beyond its minimum (<see cref="MinimumTotal"/>): the
        /// most the cell could get with every other track at its minimum. Children spanning a weighted star track are
        /// left for <see cref="RemeasureAtCellWidths"/> (their width only settles once the stars are resolved), unless
        /// the width is unbounded and the stars fall back to their content size.
        /// </summary>
        private void MeasureColumnContent(Vector2 available, bool bounded, float slack)
        {
            int colCount = effectiveColumns.Count;
            int rowCount = effectiveRows.Count;
            if (measuredWidth.Length < Children.Count)
            {
                measuredWidth = new float[Children.Count];
            }

            for (int k = 0; k < Children.Count; k++)
            {
                UIElement child = Children[k];
                measuredWidth[k] = float.NaN;
                if (!child.Visible)
                {
                    continue;
                }

                ColumnCell(child, colCount, out int col, out int colSpan);
                RowCell(child, rowCount, out int row, out int rowSpan);
                float cellW;
                if (AllPixels(effectiveColumns, col, colSpan))
                {
                    cellW = PixelSpan(effectiveColumns, col, colSpan, ColSpace);
                }
                else if (!bounded)
                {
                    cellW = available.X;
                }
                else if (SpansWeightedStar(effectiveColumns, col, colSpan))
                {
                    continue;
                }
                else
                {
                    cellW = LayoutEngine.SpanSize(columnMin, col, colSpan, ColSpace) + slack;
                }

                measuredWidth[k] = cellW;
                child.Measure(new Vector2(cellW, CellHeight(row, rowSpan, available.Y)));
            }
        }

        /// <summary>
        /// True for a column sized by its content: an auto column, or a star column when no star track has a positive
        /// weight (<see cref="LayoutEngine.ResolveTracks"/> then falls back to their content size).
        /// </summary>
        private static bool SizesToContent(List<GridTrack> tracks, int index, bool weightedStars)
        {
            return tracks[index].Type == GridTrack.Kind.Auto || (tracks[index].Type == GridTrack.Kind.Star && !weightedStars);
        }

        /// <summary>
        /// Bounded width, after <see cref="ComputeColumnSizes"/>: when the content-sized columns
        /// (<see cref="SizesToContent"/>) want more than the room the other tracks' minimums and the spacing leave
        /// (their own minimums plus <paramref name="slack"/>), share that room by
        /// <see cref="LayoutEngine.DistributeWidth"/> — every column its minimum first, then the rest by how much its
        /// content wants beyond it — and measure again, at its column's width, each single-column child that wanted
        /// more (so wrapping text wraps and reports its height). With room to spare the content sizes are kept as they are.
        /// </summary>
        private void FitContentColumns(float slack, float availableHeight)
        {
            int colCount = effectiveColumns.Count;
            bool weightedStars = false;
            for (int c = 0; c < colCount; c++)
            {
                weightedStars |= effectiveColumns[c].Type == GridTrack.Kind.Star && effectiveColumns[c].Value > 0;
            }

            if (fitWidth.Length < colCount)
            {
                fitMin = new float[colCount];
                fitNatural = new float[colCount];
                fitWidth = new float[colCount];
            }

            float minTotal = 0, naturalTotal = 0;
            for (int c = 0; c < colCount; c++)
            {
                bool fits = SizesToContent(effectiveColumns, c, weightedStars);
                fitMin[c] = fits ? columnMin[c] : 0;
                fitNatural[c] = fits ? columnAuto[c] : 0;
                minTotal += fitMin[c];
                naturalTotal += fitNatural[c];
            }

            float room = minTotal + slack;
            if (naturalTotal <= room)
            {
                return;
            }

            LayoutEngine.DistributeWidth(fitMin.AsSpan(0, colCount), fitNatural.AsSpan(0, colCount), room, fitWidth.AsSpan(0, colCount));
            for (int c = 0; c < colCount; c++)
            {
                if (SizesToContent(effectiveColumns, c, weightedStars))
                {
                    columnAuto[c] = fitWidth[c];
                }
            }

            int rowCount = effectiveRows.Count;
            for (int k = 0; k < Children.Count; k++)
            {
                UIElement child = Children[k];
                if (!child.Visible || float.IsNaN(measuredWidth[k]))
                {
                    continue;
                }

                ColumnCell(child, colCount, out int col, out int colSpan);
                if (colSpan != 1 || !SizesToContent(effectiveColumns, col, weightedStars) || child.DesiredSize.X <= fitWidth[col])
                {
                    continue;
                }

                RowCell(child, rowCount, out int row, out int rowSpan);
                child.Measure(new Vector2(fitWidth[col], CellHeight(row, rowSpan, availableHeight)));
                measuredWidth[k] = fitWidth[col];
            }
        }

        /// <summary>Height a cell offers while measuring: pixel rows give their size, anything else the whole available height.</summary>
        private float CellHeight(int row, int rowSpan, float availableHeight)
        {
            return AllPixels(effectiveRows, row, rowSpan) ? PixelSpan(effectiveRows, row, rowSpan, RowSpace) : availableHeight;
        }

        /// <summary>Content size per column from the pass 1 measures — single-track children first, then spanning children spread their excess.</summary>
        private void ComputeColumnSizes()
        {
            int colCount = effectiveColumns.Count;
            for (int k = 0; k < Children.Count; k++)
            {
                UIElement child = Children[k];
                if (!child.Visible || float.IsNaN(measuredWidth[k]))
                {
                    continue;
                }

                ColumnCell(child, colCount, out int col, out int colSpan);
                if (colSpan == 1 && effectiveColumns[col].Type != GridTrack.Kind.Pixels)
                {
                    columnAuto[col] = Math.Max(columnAuto[col], child.DesiredSize.X);
                }
            }

            for (int k = 0; k < Children.Count; k++)
            {
                UIElement child = Children[k];
                if (!child.Visible || float.IsNaN(measuredWidth[k]))
                {
                    continue;
                }

                ColumnCell(child, colCount, out int col, out int colSpan);
                if (colSpan > 1)
                {
                    DistributeSpan(effectiveColumns, columnAuto, col, colSpan, ColSpace, child.DesiredSize.X);
                }
            }
        }

        /// <summary>
        /// Pass 2: star-column and spanning children measured again at their resolved cell width (the sum of the
        /// spanned tracks plus spacing), so wrapping content wraps at the width it is drawn in and reports the matching
        /// height. Skipped when the cell equals the width the child was measured at, or when that was an unbounded
        /// width the cell already fits. Auto-column children keep their measure from pass 1 (or
        /// <see cref="FitContentColumns"/>): their column is sized from it.
        /// </summary>
        private void RemeasureAtCellWidths(float[] colSizes, float availableHeight)
        {
            int colCount = effectiveColumns.Count;
            int rowCount = effectiveRows.Count;
            for (int k = 0; k < Children.Count; k++)
            {
                UIElement child = Children[k];
                if (!child.Visible)
                {
                    continue;
                }

                ColumnCell(child, colCount, out int col, out int colSpan);
                if (AllPixels(effectiveColumns, col, colSpan) || (colSpan == 1 && effectiveColumns[col].Type != GridTrack.Kind.Star))
                {
                    continue;
                }

                float cellW = LayoutEngine.SpanSize(colSizes, col, colSpan, ColSpace);
                float measured = measuredWidth[k];
                if (cellW == measured || (float.IsInfinity(measured) && cellW >= child.DesiredSize.X))
                {
                    continue;
                }

                RowCell(child, rowCount, out int row, out int rowSpan);
                child.Measure(new Vector2(cellW, CellHeight(row, rowSpan, availableHeight)));
            }
        }

        /// <summary>Content size per row from the final measures — single-track children first, then spanning children spread their excess.</summary>
        private void ComputeRowSizes()
        {
            int rowCount = effectiveRows.Count;
            for (int k = 0; k < Children.Count; k++)
            {
                UIElement child = Children[k];
                if (!child.Visible)
                {
                    continue;
                }

                RowCell(child, rowCount, out int row, out int rowSpan);
                if (rowSpan == 1 && effectiveRows[row].Type != GridTrack.Kind.Pixels)
                {
                    rowAuto[row] = Math.Max(rowAuto[row], child.DesiredSize.Y);
                }
            }

            for (int k = 0; k < Children.Count; k++)
            {
                UIElement child = Children[k];
                if (!child.Visible)
                {
                    continue;
                }

                RowCell(child, rowCount, out int row, out int rowSpan);
                if (rowSpan > 1)
                {
                    DistributeSpan(effectiveRows, rowAuto, row, rowSpan, RowSpace, child.DesiredSize.Y);
                }
            }
        }

        /// <summary>
        /// Column tracks resolved on minimums instead of desired sizes, into local arrays (the measure state is left
        /// alone): pixel tracks at their size, auto and star tracks at the widest minimum of their single-track
        /// children, spanning children spread what their span still lacks (<see cref="FillColumnMinimums"/>), totalled
        /// by <see cref="MinimumTotal"/> — the same minimums <see cref="MeasureCore"/> reserves.
        /// </summary>
        protected override float MinWidthCore()
        {
            int needed = columnTracks.Count;
            for (int k = 0; k < Children.Count; k++)
            {
                UIElement child = Children[k];
                if (child.Visible)
                {
                    needed = Math.Max(needed, child.Column + child.ColumnSpan);
                }
            }

            List<GridTrack> tracks = Extend(columnTracks, needed);
            float[] min = PixelSizes(tracks, null);
            FillColumnMinimums(tracks, min);
            return MinimumTotal(tracks, min) + (ColSpace * Math.Max(0, tracks.Count - 1));
        }

        protected override void ArrangeCore()
        {
            int colCount = effectiveColumns.Count;
            int rowCount = effectiveRows.Count;
            if (columnAuto.Length != colCount || rowAuto.Length != rowCount)
            {
                // arranged without a matching measure (should not happen); size everything to content
                columnAuto = new float[colCount];
                rowAuto = new float[rowCount];
            }

            float colSpacingTotal = ColSpace * Math.Max(0, colCount - 1);
            float rowSpacingTotal = RowSpace * Math.Max(0, rowCount - 1);
            float[] colSizes = LayoutEngine.ResolveTracks(effectiveColumns, columnAuto, Bounds.Width - colSpacingTotal);
            float[] rowSizes = LayoutEngine.ResolveTracks(effectiveRows, rowAuto, Bounds.Height - rowSpacingTotal);
            columnSizes = colSizes;
            this.rowSizes = rowSizes;

            foreach (UIElement child in Children)
            {
                if (!child.Visible)
                {
                    child.Arrange(new Rectangle(Bounds.X, Bounds.Y, 0, 0));
                    continue;
                }
                ColumnCell(child, colCount, out int col, out int colSpan);
                RowCell(child, rowCount, out int row, out int rowSpan);

                float x0 = LayoutEngine.TrackOffset(colSizes, col, ColSpace);
                float y0 = LayoutEngine.TrackOffset(rowSizes, row, RowSpace);
                float x1 = x0 + LayoutEngine.SpanSize(colSizes, col, colSpan, ColSpace);
                float y1 = y0 + LayoutEngine.SpanSize(rowSizes, row, rowSpan, RowSpace);

                int left = Bounds.X + (int)Math.Round(x0);
                int top = Bounds.Y + (int)Math.Round(y0);
                int right = Bounds.X + (int)Math.Round(x1);
                int bottom = Bounds.Y + (int)Math.Round(y1);
                child.Arrange(new Rectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top)));
            }
        }
    }
}
