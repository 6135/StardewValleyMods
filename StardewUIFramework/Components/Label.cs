using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    /// <summary>
    /// Text. The text delegate is re-evaluated every frame; a change in size re-flows the layout. With
    /// <see cref="RichText"/> on, the text is parsed as markup (colors, bold, item icons, links) and laid out by
    /// <see cref="Rendering.RichText"/>; clicks on a link span raise <see cref="OnLink"/>.
    /// </summary>
    internal sealed class Label : UIElement, IUILabel
    {
        private Func<string>? text;
        private UIFont? font;
        private bool wrap;
        private bool shrink;
        private bool richText;
        private float scale = 1f;
        private string measuredText = string.Empty;
        private string displayText = string.Empty;
        private RichLayout? richLayout;
        private int wrapWidth = -1;

        /// <summary>Width of the text as last wrapped / laid out by <see cref="Reflow"/>.</summary>
        private float laidOutWidth;

        internal Label(string id, Func<string>? text) : base(id)
        {
            this.text = text;
        }

        internal Func<string>? TextFunc
        {
            get => text;
            set
            {
                text = value;
                InvalidateLayout();
            }
        }

        Func<string> IUILabel.Text { get => text!; set => TextFunc = value; }

        /// <summary>Explicit font, or the style's font when unset.</summary>
        public UIFont Font
        {
            get => font ?? Style.Font;
            set
            {
                font = value;
                InvalidateLayout();
            }
        }

        public Color? Color { get; set; }
        public bool Shadow { get; set; }

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

        /// <summary>
        /// Single line only: let the minimum width drop to the fitted text (<see cref="DrawHelper.FitTextMinWidth"/>)
        /// instead of the whole line. Wrapping and rich-text labels ignore it.
        /// </summary>
        public bool Shrink
        {
            get => shrink;
            set
            {
                if (shrink == value)
                {
                    return;
                }

                shrink = value;
                InvalidateLayout();
            }
        }

        public UIAlign TextAlign { get; set; } = UIAlign.Start;

        public float Scale
        {
            get => scale;
            set
            {
                scale = Math.Max(0.05f, value);
                InvalidateLayout();
            }
        }

        public bool RichText
        {
            get => richText;
            set
            {
                if (richText == value)
                {
                    return;
                }

                richText = value;
                richLayout = null;
                InvalidateLayout();
            }
        }

        internal Action<string>? OnLink { get; set; }

        Action<string> IUILabel.OnLink { get => OnLink!; set => OnLink = value; }

        /// <summary>Current (unwrapped) text; pseudo-localized here for plain labels, by the parser for rich ones.</summary>
        internal string CurrentText
        {
            get
            {
                string raw = Raise("Text", text, string.Empty) ?? string.Empty;
                return richText ? raw : Pseudo.Transform(raw);
            }
        }

        internal override string AccessibleDescription => Accessibility.Compose(Accessibility.Text("label", "Label"), CurrentText);

        // labels only take clicks / hover when they have a reason to
        protected override bool IsHitTestVisible => HasPointerHandlers || (richText && OnLink != null && richLayout?.Document.HasLinks == true);

        // ---------------------------------------------------------------------------------------------------------
        //  Layout / draw
        // ---------------------------------------------------------------------------------------------------------

        protected override Vector2 MeasureCore(Vector2 available)
        {
            measuredText = CurrentText;
            wrapWidth = wrap ? (int)Math.Floor(available.X) : -1;
            return Reflow(measuredText);
        }

        /// <summary>Wrap / lay out <paramref name="current"/> for the last wrap width and return its size.</summary>
        private Vector2 Reflow(string current)
        {
            UIFont f = Font;
            Vector2 size;
            if (richText)
            {
                richLayout = Rendering.RichText.Layout(Rendering.RichText.Parse(current), f, scale, wrapWidth);
                size = richLayout.Size;
            }
            else
            {
                displayText = wrapWidth > 0 ? UIServices.Text.Wrap(f, current, (int)(wrapWidth / scale)) : current;
                size = UIServices.Text.Measure(f, displayText, scale);
            }

            laidOutWidth = size.X;
            return size;
        }

        // wrapped: the widest unbreakable piece; single line: the whole line (the natural width MeasureCore reports),
        // or with Shrink the fitted minimum of a plain line (drawing shrinks / truncates it with FitText); rich layouts
        // are drawn as laid out and never truncate, so a rich single line always keeps the whole line
        protected override float MinWidthCore()
        {
            string current = CurrentText;
            UIFont f = Font;
            if (richText)
            {
                return wrap ? Rendering.RichText.MinWidth(current, f, scale) : Rendering.RichText.Measure(current, f, scale, 0).X;
            }

            if (wrap)
            {
                return UIServices.Text.LongestWord(f, current, scale);
            }

            return shrink ? DrawHelper.FitTextMinWidth(current, f, scale) : (float)Math.Ceiling(UIServices.Text.Measure(f, current, scale).X);
        }

        // a wrapping label breaks its lines at the width it was measured with; when the parent arranges it narrower
        // than its widest wrapped line (squeezed below its desired size), re-wrap at the final width so no line draws
        // past it. Layout-time only, and only when the lines would actually overflow.
        protected override void ArrangeCore()
        {
            if (!wrap || Bounds.Width <= 0 || Bounds.Width >= Math.Ceiling(laidOutWidth))
            {
                return;
            }

            wrapWidth = Bounds.Width;
            Reflow(measuredText);
        }

        protected override void DrawCore(SpriteBatch b)
        {
            string current = CurrentText;
            if (current != measuredText || (richText && richLayout == null))
            {
                // bound text changed since layout: draw the new text now, re-flow next frame
                measuredText = current;
                Reflow(current);
                InvalidateLayout();
            }

            ResolvedStyle style = Style;
            Color color = Color ?? style.TextColor;
            bool shadow = Shadow || style.TextShadow;
            if (richText && richLayout != null)
            {
                Rendering.RichText.Draw(b, richLayout, Bounds, color, shadow, TextAlign, HoveredLink());
                return;
            }

            if (Wrap)
            {
                DrawHelper.TextInRect(b, displayText, Font, Bounds, color, shadow, scale, TextAlign);
                return;
            }

            // a single line that got less width than it wanted shrinks a little, then truncates
            DrawHelper.FitText(b, displayText, Font, Bounds, color, shadow, scale, TextAlign);
        }

        /// <summary>The link under the cursor while hovered, or null.</summary>
        private string? HoveredLink()
        {
            if (!IsHovered || OwnerMenu == null || richLayout == null)
            {
                return null;
            }

            return richLayout.LinkAt(Bounds, TextAlign, OwnerMenu.CursorX, OwnerMenu.CursorY);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Input
        // ---------------------------------------------------------------------------------------------------------

        protected internal override bool HandleClick(UIClickEvent e)
        {
            if (e.Target == this && e.Button == UIMouseButton.Left && richText && OnLink != null && richLayout != null)
            {
                string? link = richLayout.LinkAt(Bounds, TextAlign, e.X, e.Y);
                if (link != null)
                {
                    Action<string> cb = OnLink;
                    Raise("OnLink", () => cb(link));
                    e.Handled = true;
                }
            }

            return base.HandleClick(e);
        }
    }
}
