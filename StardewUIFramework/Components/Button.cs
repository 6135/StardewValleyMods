using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    /// <summary>
    /// A vanilla-looking button: 9-slice box (hover tint), optional icon, centered text. Focusable; Enter / Space /
    /// gamepad A activate it. With <see cref="RichText"/> on, the text is drawn through <see cref="Rendering.RichText"/>.
    /// </summary>
    internal sealed class Button : UIElement, IUIButton
    {
        private const int PadX = 24;
        private const int PadY = 12;
        private const int IconGap = 8;
        private const int MinHeight = 64;

        private Func<string>? text;
        private UIFont? font;
        private Texture2D? icon;
        private Rectangle? iconSource;
        private float iconScale = 4f;
        private bool richText;
        private RichLayout? richLayout;
        private string measuredText = string.Empty;

        internal Button(string id, Func<string>? text, Action<IUIClickEvent>? onClick) : base(id)
        {
            this.text = text;
            OnClick = onClick;
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

        Func<string> IUIButton.Text { get => text!; set => TextFunc = value; }

        public UIFont Font
        {
            get => font ?? Style.Font;
            set
            {
                font = value;
                InvalidateLayout();
            }
        }

        internal Texture2D? Icon
        {
            get => icon;
            set
            {
                icon = value;
                InvalidateLayout();
            }
        }

        Texture2D IUIButton.Icon { get => icon!; set => Icon = value; }

        public Rectangle? IconSource
        {
            get => iconSource;
            set
            {
                iconSource = value;
                InvalidateLayout();
            }
        }

        public float IconScale
        {
            get => iconScale;
            set
            {
                iconScale = Math.Max(0.05f, value);
                InvalidateLayout();
            }
        }

        /// <summary>null = theme default, empty = silent.</summary>
        internal string? ClickSound { get; set; }

        internal string? HoverSound { get; set; }

        string IUIButton.ClickSound { get => ClickSound!; set => ClickSound = value; }
        string IUIButton.HoverSound { get => HoverSound!; set => HoverSound = value; }

        public bool DrawBox { get; set; } = true;

        public bool RichText
        {
            get => richText;
            set
            {
                richText = value;
                InvalidateLayout();
            }
        }

        internal override bool Focusable => true;
        internal override bool ActivateOnEnter => true;

        protected override string? HoverSoundCue => HoverSound ?? Style.HoverSound ?? Theme.HoverSound;

        /// <summary>Current text; pseudo-localized here for plain buttons, by the markup parser for rich ones.</summary>
        internal string CurrentText
        {
            get
            {
                string raw = Raise("Text", text, string.Empty) ?? string.Empty;
                return richText ? raw : Pseudo.Transform(raw);
            }
        }

        /// <summary>Size of <paramref name="current"/> as it will be drawn (plain or rich); a rich layout is kept for the draw.</summary>
        private Vector2 MeasureText(string current)
        {
            richLayout = null;
            if (current.Length == 0)
            {
                return Vector2.Zero;
            }

            if (!richText)
            {
                return UIServices.Text.Measure(Font, current, 1f);
            }

            richLayout = Rendering.RichText.Layout(Rendering.RichText.Parse(current), Font, 1f, 0);
            return richLayout.Size;
        }

        internal override string AccessibleDescription => Accessibility.Compose(Accessibility.Text("button", "Button"), CurrentText, Enabled ? null : Accessibility.Text("disabled", "disabled"));

        private Vector2 IconSize => icon == null ? Vector2.Zero : new Vector2((iconSource?.Width ?? icon.Width) * iconScale, (iconSource?.Height ?? icon.Height) * iconScale);

        protected override Vector2 MeasureCore(Vector2 available)
        {
            measuredText = CurrentText;
            Vector2 textSize = MeasureText(measuredText);
            Vector2 iconSize = IconSize;
            float w = textSize.X + iconSize.X + (textSize.X > 0 && iconSize.X > 0 ? Theme.Space(IconGap) : 0) + (DrawBox ? (2 * Theme.Space(PadX)) : 0);
            float h = Math.Max(textSize.Y, iconSize.Y) + (DrawBox ? (2 * Theme.Space(PadY)) : 0);
            if (DrawBox)
            {
                h = Math.Max(h, MinHeight);
            }

            return new Vector2(w, h);
        }

        protected override void DrawCore(SpriteBatch b)
        {
            ResolvedStyle style = Style;
            if (DrawBox)
            {
                bool highlighted = Enabled && (IsHovered || IsFocused);
                DrawHelper.StyledBox(b, style, button: true, Bounds, Theme.StateTint(Enabled, highlighted, style.HoverColor));
            }

            string current = CurrentText;
            if (current != measuredText)
            {
                measuredText = current;
                InvalidateLayout();
            }

            DrawContent(b, style, current);
        }

        /// <summary>Icon then text, centered as one block inside the bounds.</summary>
        private void DrawContent(SpriteBatch b, ResolvedStyle style, string current)
        {
            Vector2 textSize = MeasureText(current);
            Vector2 iconSize = IconSize;
            float gap = textSize.X > 0 && iconSize.X > 0 ? Theme.Space(IconGap) : 0;
            // text wider than the box (fixed Width, or squeezed by the parent) is shrunk / truncated to what is left
            int pad = DrawBox ? Theme.Space(PadX) : 0;
            float maxText = Math.Max(0, Bounds.Width - (2 * pad) - iconSize.X - gap);
            textSize.X = Math.Min(textSize.X, maxText);
            float x = Bounds.X + ((Bounds.Width - (textSize.X + iconSize.X + gap)) / 2f);

            if (icon != null)
            {
                int iconY = (int)(Bounds.Y + ((Bounds.Height - iconSize.Y) / 2f));
                var dest = new Rectangle((int)x, iconY, (int)iconSize.X, (int)iconSize.Y);
                b.Draw(icon, dest, iconSource, Enabled ? Color.White : Color.White * 0.5f);
                x += iconSize.X + gap;
            }

            if (current.Length == 0)
            {
                return;
            }

            Color textColor = Enabled ? style.TextColor : style.DisabledTextColor;
            int textY = (int)(Bounds.Y + ((Bounds.Height - textSize.Y) / 2f));
            if (richLayout != null)
            {
                Rendering.RichText.Draw(b, richLayout, new Rectangle((int)x, textY, (int)Math.Ceiling(textSize.X), (int)Math.Ceiling(textSize.Y)), textColor, style.TextShadow, UIAlign.Start, null);
            }
            else
            {
                DrawHelper.FitText(b, current, Font, new Rectangle((int)x, textY, (int)Math.Ceiling(textSize.X), (int)Math.Ceiling(textSize.Y)), textColor, style.TextShadow, 1f, UIAlign.Start);
            }
        }

        protected internal override bool HandleClick(UIClickEvent e)
        {
            if (e.Target == this && e.Button == UIMouseButton.Left)
            {
                UIServices.PlaySound(ClickSound ?? Style.ClickSound ?? Theme.ButtonClickSound);
            }

            return base.HandleClick(e);
        }

        protected internal override bool HandleActivate()
        {
            if (!Enabled || !Visible)
            {
                return false;
            }
            // synthesize a click at the center so bubbling / callbacks behave exactly like a mouse click
            var e = new UIClickEvent(this, Bounds.Center.X, Bounds.Center.Y, UIMouseButton.Left);
            EventRouter.Bubble(e);
            return true;
        }
    }
}
