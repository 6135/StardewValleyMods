using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    /// <summary>
    /// Clips its children to a fixed-height viewport and scrolls them vertically. Children are stacked one below the
    /// other at the full content width (most consumers add a single child). A vanilla scrollbar (arrows, track,
    /// draggable thumb — the Profit Calculator look) sits on the right; the wheel scrolls by <see cref="ScrollStep"/>
    /// and falls through to the menu once the end is reached. Children are only hit-testable inside the viewport.
    /// </summary>
    internal sealed class ScrollView : UIContainer, IUIScrollView
    {
        private readonly ScrollbarGadget scrollbar = new();
        private int viewportHeight;
        private int scrollOffset;
        private int scrollStep = 64;
        private bool showScrollbar = true;
        private int contentHeight;
        private bool dragging;

        internal ScrollView(string id, int viewportHeight) : base(id)
        {
            this.viewportHeight = Math.Max(0, viewportHeight);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Properties
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Visible height in UI pixels; an explicit <see cref="UIElement.Height"/> overrides it.</summary>
        public int ViewportHeight
        {
            get => viewportHeight;
            set
            {
                value = Math.Max(0, value);
                if (viewportHeight == value)
                {
                    return;
                }

                viewportHeight = value;
                InvalidateLayout();
            }
        }

        /// <summary>Current offset in pixels (0 = top), clamped to [0, <see cref="MaxScroll"/>]. Changing it re-arranges the children and raises <see cref="OnScroll"/>.</summary>
        public int ScrollOffset
        {
            get => scrollOffset;
            set => SetOffset(value);
        }

        public int MaxScroll => Math.Max(0, contentHeight - EffectiveViewportHeight);

        public int ScrollStep
        {
            get => scrollStep;
            set => scrollStep = Math.Max(1, value);
        }

        /// <summary>Reserve space for and draw the scrollbar (it is still hidden while there is nothing to scroll).</summary>
        public bool ShowScrollbar
        {
            get => showScrollbar;
            set
            {
                if (showScrollbar == value)
                {
                    return;
                }

                showScrollbar = value;
                InvalidateLayout();
            }
        }

        internal Action<int>? OnScroll { get; set; }

        Action<int> IUIScrollView.OnScroll { get => OnScroll!; set => OnScroll = value; }

        public void ScrollTo(int offset) => SetOffset(offset);

        public void ScrollBy(int delta) => SetOffset(scrollOffset + delta);

        // ---------------------------------------------------------------------------------------------------------
        //  Geometry
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Height of the viewport: the arranged height once laid out, otherwise the configured one.</summary>
        private int EffectiveViewportHeight => Bounds.Height > 0 ? Bounds.Height : Height ?? viewportHeight;

        private int ReservedWidth => showScrollbar ? ScrollbarGadget.ReservedWidth : 0;

        /// <summary>Whether the scrollbar is drawn / interactive.</summary>
        private bool ScrollbarVisible => showScrollbar && MaxScroll > 0;

        /// <summary>Absolute clip rectangle of the content.</summary>
        private Rectangle ViewportRect => new(Bounds.X, Bounds.Y, Math.Max(0, Bounds.Width - ReservedWidth), Bounds.Height);

        // ---------------------------------------------------------------------------------------------------------
        //  Scrolling
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Clamp and apply an offset; returns true if it changed. Raises <see cref="OnScroll"/> with the delta.</summary>
        private bool SetOffset(int value)
        {
            value = Math.Clamp(value, 0, MaxScroll);
            if (scrollOffset == value)
            {
                return false;
            }

            int delta = value - scrollOffset;
            scrollOffset = value;
            InvalidateLayout();
            if (OnScroll != null)
            {
                Action<int> cb = OnScroll;
                Raise("OnScroll", () => cb(delta));
            }
            return true;
        }

        /// <summary>Scroll so the thumb's center follows the cursor (thumb drag / track click).</summary>
        private void SetOffsetFromY(int py)
        {
            int target = (int)Math.Round(scrollbar.FractionFromY(py) * MaxScroll);
            if (SetOffset(target))
            {
                UIServices.PlaySound(Theme.ScrollSound);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Layout
        // ---------------------------------------------------------------------------------------------------------

        protected override Vector2 MeasureCore(Vector2 available)
        {
            int reserved = ReservedWidth;
            var inner = new Vector2(Math.Max(0, available.X - reserved), float.PositiveInfinity);
            float w = 0, h = 0;
            foreach (UIElement child in Children)
            {
                if (!child.Visible)
                {
                    continue;
                }

                Vector2 size = child.Measure(inner);
                w = Math.Max(w, size.X);
                h += size.Y;
            }
            contentHeight = (int)Math.Ceiling(h);
            return new Vector2(w + reserved, viewportHeight);
        }

        protected override void ArrangeCore()
        {
            // the final height is known now; keep the offset valid before positioning anything
            scrollOffset = Math.Clamp(scrollOffset, 0, MaxScroll);

            Rectangle viewport = ViewportRect;
            int y = viewport.Y - scrollOffset;
            foreach (UIElement child in Children)
            {
                if (!child.Visible)
                {
                    child.Arrange(new Rectangle(viewport.X, viewport.Y, 0, 0));
                    continue;
                }
                int h = (int)Math.Ceiling(child.DesiredSize.Y);
                child.Arrange(new Rectangle(viewport.X, y, viewport.Width, h));
                y += h;
            }

            int max = MaxScroll;
            scrollbar.Layout(Bounds.Right - ScrollbarGadget.Width, Bounds.Y, Bounds.Height, max > 0 ? scrollOffset / (float)max : 0f);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Draw
        // ---------------------------------------------------------------------------------------------------------

        protected override void DrawCore(SpriteBatch b)
        {
            Rectangle viewport = ViewportRect;
            if (viewport.Width > 0 && viewport.Height > 0)
            {
                DrawHelper.WithScissor(b, viewport, () => DrawChildren(b));
            }

            if (ScrollbarVisible)
            {
                scrollbar.Draw(b);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Hit testing / input
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Children are only reachable inside the viewport; the scrollbar counts as the scroll view itself.</summary>
        internal override UIElement? HitTest(int px, int py)
        {
            if (!Visible || !Enabled || !Bounds.Contains(px, py))
            {
                return null;
            }

            if (ScrollbarVisible && scrollbar.Contains(px, py))
            {
                return this;
            }

            if (ViewportRect.Contains(px, py))
            {
                for (int i = Children.Count - 1; i >= 0; i--)
                {
                    UIElement? hit = Children[i].HitTest(px, py);
                    if (hit != null)
                    {
                        return hit;
                    }
                }
            }
            return IsHitTestVisible ? this : null;
        }

        protected internal override bool HandleClick(UIClickEvent e)
        {
            if (e.Target == this && e.Button == UIMouseButton.Left && ScrollbarVisible && HandleScrollbarClick(e.X, e.Y))
            {
                return true;
            }

            return base.HandleClick(e);
        }

        /// <summary>Arrows step, the thumb starts a drag, the track jumps. Returns false when no scrollbar part was hit.</summary>
        private bool HandleScrollbarClick(int px, int py)
        {
            switch (scrollbar.HitTest(px, py))
            {
                case ScrollbarGadget.Part.UpArrow:
                    ScrollWithSound(-scrollStep);
                    return true;
                case ScrollbarGadget.Part.DownArrow:
                    ScrollWithSound(scrollStep);
                    return true;
                case ScrollbarGadget.Part.Thumb:
                    dragging = true;
                    return true;
                case ScrollbarGadget.Part.Track:
                    dragging = true;
                    SetOffsetFromY(py);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Scroll by <paramref name="delta"/> pixels, with the vanilla scroll sound when something moved.</summary>
        private void ScrollWithSound(int delta)
        {
            if (SetOffset(scrollOffset + delta))
            {
                UIServices.PlaySound(Theme.ScrollSound);
            }
        }

        protected internal override void HandleClickHeld(int px, int py)
        {
            if (dragging)
            {
                SetOffsetFromY(py);
            }
        }

        protected internal override void HandleClickRelease(int px, int py)
        {
            dragging = false;
        }

        /// <summary>Wheel: scroll by <see cref="ScrollStep"/>; unhandled (falls through) when already at the end.</summary>
        protected internal override bool HandleScroll(int direction)
        {
            if (direction == 0)
            {
                return false;
            }

            if (!SetOffset(scrollOffset + (direction > 0 ? -scrollStep : scrollStep)))
            {
                return false;
            }

            UIServices.PlaySound(Theme.ScrollSound);
            return true;
        }
    }
}
