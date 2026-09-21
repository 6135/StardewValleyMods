using System;
using Microsoft.Xna.Framework;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    /// <summary>Lays children out in a column (default) or a row with fixed spacing.</summary>
    internal class Stack : UIContainer, IUIStack
    {
        private bool horizontal;
        private int spacing;
        private UIAlign alignment = UIAlign.Start;

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

        /// <summary>Spacing actually used: the consumer's value scaled by the theme.</summary>
        private int EffectiveSpacing => Theme.Space(spacing);

        // a stack is layout-only: clicks on the gaps fall through to whatever is behind it (unless it has a handler)
        protected override bool IsHitTestVisible => HasPointerHandlers;

        internal override UIAlign DefaultChildHorizontalAlign(UIElement child) => horizontal ? UIAlign.Start : alignment;
        internal override UIAlign DefaultChildVerticalAlign(UIElement child) => horizontal ? alignment : UIAlign.Start;

        protected override Vector2 MeasureCore(Vector2 available)
        {
            float main = 0, cross = 0;
            int visible = 0;
            int gap = EffectiveSpacing;
            foreach (UIElement child in Children)
            {
                if (!child.Visible)
                {
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
            if (visible > 1)
            {
                main += gap * (visible - 1);
            }

            return horizontal ? new Vector2(main, cross) : new Vector2(cross, main);
        }

        protected override void ArrangeCore()
        {
            int cursor = horizontal ? Bounds.X : Bounds.Y;
            int end = horizontal ? Bounds.Right : Bounds.Bottom;
            int gap = EffectiveSpacing;
            foreach (UIElement child in Children)
            {
                if (!child.Visible)
                {
                    child.Arrange(new Rectangle(Bounds.X, Bounds.Y, 0, 0));
                    continue;
                }
                // a child never extends past the container; what does not fit is cut, not spilled over the box
                int start = Math.Min(cursor, end);
                int extent = Math.Min((int)Math.Ceiling(horizontal ? child.DesiredSize.X : child.DesiredSize.Y), end - start);
                Rectangle slot = horizontal
                    ? new Rectangle(start, Bounds.Y, extent, Bounds.Height)
                    : new Rectangle(Bounds.X, start, Bounds.Width, extent);
                child.Arrange(slot);
                cursor += extent + gap;
            }
        }
    }
}
