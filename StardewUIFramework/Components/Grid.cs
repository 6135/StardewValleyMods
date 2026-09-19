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
    /// Two-pass, WPF-lite: <see cref="MeasureCore"/> measures every child in the space its cell can offer, derives
    /// each track's content size (spanning children spread their excess evenly over the spanned tracks) and resolves
    /// the tracks against the available size; <see cref="ArrangeCore"/> resolves them again against the final
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

        /// <summary>Grow the auto sizes of the spanned tracks so the span fits <paramref name="desired"/> (excess spread evenly over the non-pixel tracks).</summary>
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
            for (int i = start; i < end; i++)
            {
                if (tracks[i].Type != GridTrack.Kind.Pixels)
                {
                    flexible++;
                }
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
            MeasureChildren(available);
            ComputeAutoSizes();

            // resolve against the available size (star tracks share what is left)
            float colSpacingTotal = ColSpace * Math.Max(0, effectiveColumns.Count - 1);
            float rowSpacingTotal = RowSpace * Math.Max(0, effectiveRows.Count - 1);
            float[] colSizes = LayoutEngine.ResolveTracks(effectiveColumns, columnAuto, available.X - colSpacingTotal);
            float[] rowSizes = LayoutEngine.ResolveTracks(effectiveRows, rowAuto, available.Y - rowSpacingTotal);
            return new Vector2(Sum(colSizes) + colSpacingTotal, Sum(rowSizes) + rowSpacingTotal);
        }

        /// <summary>Visible children only (hidden ones take no cell).</summary>
        private IEnumerable<UIElement> VisibleChildren()
        {
            foreach (UIElement child in Children)
            {
                if (child.Visible)
                {
                    yield return child;
                }
            }
        }

        /// <summary>Step 1: the explicit tracks, extended with implicit auto tracks for children placed beyond them; pixel tracks pre-fill their auto size.</summary>
        private void ResolveEffectiveTracks()
        {
            int neededColumns = columnTracks.Count;
            int neededRows = rowTracks.Count;
            foreach (UIElement child in VisibleChildren())
            {
                neededColumns = Math.Max(neededColumns, child.Column + child.ColumnSpan);
                neededRows = Math.Max(neededRows, child.Row + child.RowSpan);
            }
            effectiveColumns = Extend(columnTracks, neededColumns);
            effectiveRows = Extend(rowTracks, neededRows);
            columnAuto = PixelSizes(effectiveColumns);
            rowAuto = PixelSizes(effectiveRows);
        }

        /// <summary>Pixel tracks sized up front (so their auto size still reads sensibly); everything else 0.</summary>
        private static float[] PixelSizes(List<GridTrack> tracks)
        {
            var sizes = new float[tracks.Count];
            for (int i = 0; i < tracks.Count; i++)
            {
                sizes[i] = tracks[i].Type == GridTrack.Kind.Pixels ? tracks[i].Value : 0;
            }

            return sizes;
        }

        /// <summary>Step 2: measure every child with what its cell can offer — pixel tracks give their size, anything else the whole available size.</summary>
        private void MeasureChildren(Vector2 available)
        {
            int colCount = effectiveColumns.Count;
            int rowCount = effectiveRows.Count;
            foreach (UIElement child in VisibleChildren())
            {
                ColumnCell(child, colCount, out int col, out int colSpan);
                RowCell(child, rowCount, out int row, out int rowSpan);
                float cellW = AllPixels(effectiveColumns, col, colSpan) ? PixelSpan(effectiveColumns, col, colSpan, ColSpace) : available.X;
                float cellH = AllPixels(effectiveRows, row, rowSpan) ? PixelSpan(effectiveRows, row, rowSpan, RowSpace) : available.Y;
                child.Measure(new Vector2(cellW, cellH));
            }
        }

        /// <summary>Step 3: content size per track — single-track children first, then spanning children spread their excess.</summary>
        private void ComputeAutoSizes()
        {
            int colCount = effectiveColumns.Count;
            int rowCount = effectiveRows.Count;
            foreach (UIElement child in VisibleChildren())
            {
                ColumnCell(child, colCount, out int col, out int colSpan);
                RowCell(child, rowCount, out int row, out int rowSpan);
                if (colSpan == 1 && effectiveColumns[col].Type != GridTrack.Kind.Pixels)
                {
                    columnAuto[col] = Math.Max(columnAuto[col], child.DesiredSize.X);
                }

                if (rowSpan == 1 && effectiveRows[row].Type != GridTrack.Kind.Pixels)
                {
                    rowAuto[row] = Math.Max(rowAuto[row], child.DesiredSize.Y);
                }
            }

            foreach (UIElement child in VisibleChildren())
            {
                ColumnCell(child, colCount, out int col, out int colSpan);
                RowCell(child, rowCount, out int row, out int rowSpan);
                if (colSpan > 1)
                {
                    DistributeSpan(effectiveColumns, columnAuto, col, colSpan, ColSpace, child.DesiredSize.X);
                }

                if (rowSpan > 1)
                {
                    DistributeSpan(effectiveRows, rowAuto, row, rowSpan, RowSpace, child.DesiredSize.Y);
                }
            }
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
