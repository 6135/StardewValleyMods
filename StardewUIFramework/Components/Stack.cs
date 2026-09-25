using System;
using Microsoft.Xna.Framework;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    /// <summary>
    /// Lays children out in a column (default) or a row with fixed spacing. A row with <see cref="Wrap"/> set breaks
    /// onto further lines instead of overflowing when its children do not fit the width it is given; a plain row that
    /// is narrower than its children's natural widths shares its width by <see cref="LayoutEngine.DistributeWidth"/>
    /// (every child its minimum first, then the rest by how much each wants beyond it).
    /// </summary>
    internal class Stack : UIContainer, IUIStack
    {
        private bool horizontal;
        private int spacing;
        private UIAlign alignment = UIAlign.Start;
        private bool wrap;

        // plain row: per child (by index) widths carried from measure to arrange, reused between measures
        private float[] rowNatural = Array.Empty<float>();
        private float[] rowMin = Array.Empty<float>();
        private float[] rowWidth = Array.Empty<float>();

        /// <summary>Width each child of a plain row was last measured at.</summary>
        private float[] rowMeasuredAt = Array.Empty<float>();

        /// <summary>Children the row buffers describe (0 when the last measure was not a plain row).</summary>
        private int rowCount;

        /// <summary>Whether <see cref="rowMin"/> holds the children's minimums since the last measure.</summary>
        private bool rowMinValid;

        /// <summary>Whether <see cref="rowWidth"/> holds shared widths (the row did not fit its children's natural widths).</summary>
        private bool rowShared;

        /// <summary>Main-axis width (gaps excluded) the shared widths were computed for, and their whole-pixel total.</summary>
        private float rowRoom, rowTotal;

        internal Stack(string id, bool horizontal, int spacing) : base(id)
        {
            this.horizontal = horizontal;
            this.spacing = Math.Max(0, spacing);
        }

        public bool Horizontal
        {
            get => horizontal;
            set
            {
                if (horizontal == value)
                {
                    return;
                }

                horizontal = value;
                InvalidateLayout();
            }
        }

        public int Spacing
        {
            get => spacing;
            set
            {
                value = Math.Max(0, value);
                if (spacing == value)
                {
                    return;
                }

                spacing = value;
                InvalidateLayout();
            }
        }

        /// <summary>Cross-axis alignment for children that did not set their own.</summary>
        public UIAlign Alignment
        {
            get => alignment;
            set
            {
                if (alignment == value)
                {
                    return;
                }

                alignment = value;
                InvalidateLayout();
            }
        }

        /// <summary>
        /// Row only (ignored by a column): start a new line whenever the next visible child (its desired width,
        /// margins included) would not fit in the rest of the current one. <see cref="Spacing"/> separates children on
        /// a line and consecutive lines alike; each line is as tall as its tallest child, which is the cross-axis slot
        /// its children align in (<see cref="Alignment"/> or their own VerticalAlign), exactly like the single line of a
        /// plain row. Along the main axis every line starts at the stack's left edge, as a plain row does; a child wider
        /// than the stack is measured at the stack's width (so wrapping text wraps) and takes a line of its own.
        /// </summary>
        public bool Wrap
        {
            get => wrap;
            set
            {
                if (wrap == value)
                {
                    return;
                }

                wrap = value;
                InvalidateLayout();
            }
        }

        /// <summary>Spacing actually used: the consumer's value scaled by the theme.</summary>
        private int EffectiveSpacing => Theme.Space(spacing);

        /// <summary>Whether the children are laid out on wrapping lines (only a row wraps).</summary>
        private bool Wrapping => horizontal && wrap;

        // a stack is layout-only: clicks on the gaps fall through to whatever is behind it (unless it has a handler)
        protected override bool IsHitTestVisible => HasPointerHandlers;

        internal override UIAlign DefaultChildHorizontalAlign(UIElement child) => horizontal ? UIAlign.Start : alignment;
        internal override UIAlign DefaultChildVerticalAlign(UIElement child) => horizontal ? alignment : UIAlign.Start;

        protected override Vector2 MeasureCore(Vector2 available)
        {
            if (Wrapping)
            {
                return MeasureWrapped(available);
            }

            float main = 0, cross = 0;
            int visible = 0;
            int gap = EffectiveSpacing;
            int count = Children.Count;
            rowCount = 0;
            if (horizontal)
            {
                EnsureRowBuffers(count);
                rowCount = count;
                rowMinValid = false;
                rowShared = false;
            }

            for (int k = 0; k < count; k++)
            {
                UIElement child = Children[k];
                if (!child.Visible)
                {
                    if (horizontal)
                    {
                        rowNatural[k] = 0;
                        rowMeasuredAt[k] = 0;
                    }
                    continue;
                }

                // each child is offered what is left along the main axis, so wrapping labels and fill-what-you-get
                // children (star grids, lists) stop pushing later siblings out of the container
                float used = main + (visible > 0 ? gap : 0);
                Vector2 remaining = horizontal
                    ? new Vector2(Math.Max(0, available.X - used), available.Y)
                    : new Vector2(available.X, Math.Max(0, available.Y - used));
                Vector2 size = child.Measure(remaining);
                if (horizontal)
                {
                    rowNatural[k] = size.X;
                    rowMeasuredAt[k] = remaining.X;
                    main += size.X;
                    cross = Math.Max(cross, size.Y);
                }
                else
                {
                    main += size.Y;
                    cross = Math.Max(cross, size.X);
                }
                visible++;
            }

            float gaps = gap * Math.Max(0, visible - 1);
            main += gaps;
            bool bounded = !float.IsInfinity(available.X) && !float.IsNaN(available.X);
            if (horizontal && bounded && main > available.X)
            {
                // the natural widths do not fit: share the row by the layout rule instead of first come, first served
                ShareRow(available.X - gaps, available.Y);
                main = rowTotal + gaps;
                cross = 0;
                foreach (UIElement child in Children)
                {
                    if (child.Visible)
                    {
                        cross = Math.Max(cross, child.DesiredSize.Y);
                    }
                }
            }

            return horizontal ? new Vector2(main, cross) : new Vector2(cross, main);
        }

        /// <summary>Size the plain-row buffers for <paramref name="count"/> children (reallocated only when too small).</summary>
        private void EnsureRowBuffers(int count)
        {
            if (rowNatural.Length >= count)
            {
                return;
            }

            rowNatural = new float[count];
            rowMin = new float[count];
            rowWidth = new float[count];
            rowMeasuredAt = new float[count];
        }

        /// <summary>
        /// Plain row whose children's natural widths do not fit <paramref name="room"/> (gaps excluded): every visible
        /// child gets its minimum width, the rest is shared by how much each wants beyond it
        /// (<see cref="LayoutEngine.DistributeWidth"/>), snapped to whole pixels. A child given a width it was not
        /// measured at is measured again at that width (so wrapping text wraps and reports its height) when the change
        /// matters: it gets less than its natural width, or it was measured narrower than that.
        /// </summary>
        private void ShareRow(float room, float height)
        {
            int count = rowCount;
            if (!rowMinValid)
            {
                for (int k = 0; k < count; k++)
                {
                    rowMin[k] = Children[k].Visible ? Children[k].MeasureMinWidth() : 0;
                }
                rowMinValid = true;
            }

            LayoutEngine.DistributeWidth(rowMin.AsSpan(0, count), rowNatural.AsSpan(0, count), room, rowWidth.AsSpan(0, count));

            // whole pixels from the running total, so the widths add up to the rounded total
            float sum = 0;
            int edge = 0;
            for (int k = 0; k < count; k++)
            {
                sum += rowWidth[k];
                int next = (int)Math.Round(sum);
                rowWidth[k] = next - edge;
                edge = next;
            }

            rowShared = true;
            rowRoom = room;
            rowTotal = edge;
            for (int k = 0; k < count; k++)
            {
                UIElement child = Children[k];
                float width = rowWidth[k];
                if (!child.Visible || width == rowMeasuredAt[k] || (width >= rowNatural[k] && rowMeasuredAt[k] >= rowNatural[k]))
                {
                    continue;
                }

                child.Measure(new Vector2(width, height));
                rowMeasuredAt[k] = width;
            }
        }

        /// <summary>
        /// A column (and a wrapping row, whose other children can move to further lines) is as narrow as its widest
        /// child; a plain row needs every visible child's minimum plus the gaps between them.
        /// </summary>
        protected override float MinWidthCore()
        {
            if (!horizontal || wrap)
            {
                return MaxChildMinWidth();
            }

            float total = 0;
            int visible = 0;
            foreach (UIElement child in Children)
            {
                if (!child.Visible)
                {
                    continue;
                }

                total += child.MeasureMinWidth();
                visible++;
            }

            return total + (EffectiveSpacing * Math.Max(0, visible - 1));
        }

        protected override void ArrangeCore()
        {
            if (Wrapping)
            {
                ArrangeWrapped();
                return;
            }

            int cursor = horizontal ? Bounds.X : Bounds.Y;
            int end = horizontal ? Bounds.Right : Bounds.Bottom;
            int gap = EffectiveSpacing;
            bool shared = horizontal && ShareRowForArrange(gap);
            for (int k = 0; k < Children.Count; k++)
            {
                UIElement child = Children[k];
                if (!child.Visible)
                {
                    child.Arrange(new Rectangle(Bounds.X, Bounds.Y, 0, 0));
                    continue;
                }
                // a child never extends past the container; what does not fit is cut, not spilled over the box
                int start = Math.Min(cursor, end);
                int wanted = shared ? (int)rowWidth[k] : (int)Math.Ceiling(horizontal ? child.DesiredSize.X : child.DesiredSize.Y);
                int extent = Math.Min(wanted, end - start);
                Rectangle slot = horizontal
                    ? new Rectangle(start, Bounds.Y, extent, Bounds.Height)
                    : new Rectangle(Bounds.X, start, Bounds.Width, extent);
                child.Arrange(slot);
                cursor += extent + gap;
            }
        }

        /// <summary>
        /// Plain row: whether the children are placed at shared widths (<see cref="rowWidth"/>) rather than their
        /// desired widths. The measure's sharing is kept when the final width matches it; a row arranged at another
        /// width than it was measured for shares again from <see cref="UIElement.Bounds"/> when its children's natural
        /// widths do not fit there (or were already shared), re-measuring only the children <see cref="ShareRow"/> must.
        /// </summary>
        private bool ShareRowForArrange(int gap)
        {
            if (rowCount != Children.Count || rowCount == 0)
            {
                // arranged without a matching measure (should not happen); place at the desired widths
                return false;
            }

            int visible = 0;
            float naturalTotal = 0;
            for (int k = 0; k < rowCount; k++)
            {
                if (Children[k].Visible)
                {
                    naturalTotal += rowNatural[k];
                    visible++;
                }
            }

            float room = Bounds.Width - (gap * Math.Max(0, visible - 1));
            if (rowShared && (room == rowTotal || room == rowRoom))
            {
                return true;
            }

            if (!rowShared && naturalTotal <= room)
            {
                return false;
            }

            ShareRow(room, Bounds.Height);
            return true;
        }

        /// <summary>Wrapping row: as wide as its widest line, as tall as its lines plus the spacing between them.</summary>
        private Vector2 MeasureWrapped(Vector2 available)
        {
            // every child is offered the full width: whichever line it lands on, that is the most it can get
            foreach (UIElement child in Children)
            {
                if (child.Visible)
                {
                    child.Measure(available);
                }
            }

            float width = 0, height = 0;
            int lines = 0;
            for (int first = 0; first < Children.Count;)
            {
                first = NextLine(first, available.X, out float lineWidth, out float lineHeight, out bool any);
                if (!any)
                {
                    continue;
                }

                width = Math.Max(width, lineWidth);
                height += lineHeight;
                lines++;
            }

            if (lines > 1)
            {
                height += EffectiveSpacing * (lines - 1);
            }

            return new Vector2(width, height);
        }

        /// <summary>Wrapping row: re-break the lines at the final width and place each one below the previous.</summary>
        private void ArrangeWrapped()
        {
            int gap = EffectiveSpacing;
            int top = Bounds.Y;
            for (int first = 0; first < Children.Count;)
            {
                int end = NextLine(first, Bounds.Width, out _, out float lineHeight, out bool any);
                int y = Math.Min(top, Bounds.Bottom);
                int height = Math.Min((int)Math.Ceiling(lineHeight), Bounds.Bottom - y);
                int cursor = Bounds.X;
                for (int i = first; i < end; i++)
                {
                    UIElement child = Children[i];
                    if (!child.Visible)
                    {
                        child.Arrange(new Rectangle(Bounds.X, Bounds.Y, 0, 0));
                        continue;
                    }

                    // as on a single line, nothing spills past the container's box
                    int start = Math.Min(cursor, Bounds.Right);
                    int extent = Math.Min((int)Math.Ceiling(child.DesiredSize.X), Bounds.Right - start);
                    child.Arrange(new Rectangle(start, y, extent, height));
                    cursor += extent + gap;
                }

                if (any)
                {
                    top += (int)Math.Ceiling(lineHeight) + gap;
                }

                first = end;
            }
        }

        /// <summary>
        /// Break one line of a wrapping row, starting at child <paramref name="first"/>, from the children's measured
        /// sizes: visible children are taken while they fit in <paramref name="width"/> (the first one always does).
        /// Returns the index after the line's last child; <paramref name="any"/> is false when only hidden children were left.
        /// </summary>
        private int NextLine(int first, float width, out float lineWidth, out float lineHeight, out bool any)
        {
            int gap = EffectiveSpacing;
            lineWidth = 0;
            lineHeight = 0;
            any = false;
            int i = first;
            for (; i < Children.Count; i++)
            {
                UIElement child = Children[i];
                if (!child.Visible)
                {
                    continue;
                }

                // whole pixels, as the arrange pass uses them, so measuring and arranging break at the same children
                float childWidth = (float)Math.Ceiling(child.DesiredSize.X);
                if (any && lineWidth + gap + childWidth > width)
                {
                    break;
                }

                lineWidth += (any ? gap : 0) + childWidth;
                lineHeight = Math.Max(lineHeight, child.DesiredSize.Y);
                any = true;
            }

            return i;
        }
    }
}
