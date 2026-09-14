using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    /// <summary>Text. The text delegate is re-evaluated every frame; a change in size re-flows the layout.</summary>
    internal sealed class Label : UIElement, IUILabel
    {
        private Func<string>? text;
        private UIFont? font;
        private bool wrap;
        private float scale = 1f;
        private string measuredText = string.Empty;
        private string displayText = string.Empty;
        private int wrapWidth = -1;

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

        /// <summary>Current (unwrapped) text.</summary>
        internal string CurrentText => Raise("Text", text, string.Empty) ?? string.Empty;

        // labels only take clicks / hover when they have a reason to
        protected override bool IsHitTestVisible => HasPointerHandlers;

        protected override Vector2 MeasureCore(Vector2 available)
        {
            measuredText = CurrentText;
            UIFont f = Font;
            if (wrap)
            {
                wrapWidth = (int)Math.Floor(available.X);
                displayText = wrapWidth > 0 ? UIServices.Text.Wrap(f, measuredText, (int)(wrapWidth / scale)) : measuredText;
            }
            else
            {
                wrapWidth = -1;
                displayText = measuredText;
            }
            return UIServices.Text.Measure(f, displayText, scale);
        }

        protected override void DrawCore(SpriteBatch b)
        {
            string current = CurrentText;
            if (current != measuredText)
            {
                // bound text changed since layout: draw the new text now, re-flow next frame
                measuredText = current;
                displayText = wrap && wrapWidth > 0 ? UIServices.Text.Wrap(Font, current, (int)(wrapWidth / scale)) : current;
                InvalidateLayout();
            }
            ResolvedStyle style = Style;
            DrawHelper.TextInRect(b, displayText, Font, Bounds, Color ?? style.TextColor, Shadow || style.TextShadow, scale, TextAlign, UIAlign.Start);
        }
    }
}
