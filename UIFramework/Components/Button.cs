using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using System;
using UIFramework.Components.Base;
using UIFramework.Events;

namespace UIFramework.Components
{
    public class Button : BaseClickableComponent
    {
        public string Text { get; set; }
        public Color TextColor { get; set; } = Game1.textColor;
        public Color BackgroundColor { get; set; } = Color.White;
        public new Color HoverColor { get; set; } = Color.LightGray;
        public new Color PressedColor { get; set; } = Color.Gray;
        public Color BorderColor { get; set; } = Color.Black;
        public int BorderWidth { get; set; } = 2;
        public new float Scale { get; set; } = 1.0f;
        public SpriteFont Font { get; set; } = Game1.smallFont;

        private bool isPressed = false;
        private bool isHovering = false;

        public Button(string id, Vector2 position, Vector2 size, string text)
            : base(id, position, size)
        {
            Text = text;
        }

        public override void Draw(SpriteBatch b)
        {
            if (!Visible)
                return;

            // Draw background
            Color currentColor = BackgroundColor;
            if (!Enabled)
                currentColor = Color.Gray;
            else if (isPressed)
                currentColor = PressedColor;
            else if (isHovering)
                currentColor = HoverColor;

            // Draw button background
            b.Draw(
                Game1.staminaRect,
                new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y),
                currentColor
            );

            // Draw border
            if (BorderWidth > 0)
            {
                DrawBorder(b, new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y), BorderWidth, BorderColor);
            }

            // Draw text
            Vector2 textSize = Font.MeasureString(Text) * Scale;
            Vector2 textPosition = new Vector2(
                Position.X + (Size.X - textSize.X) / 2,
                Position.Y + (Size.Y - textSize.Y) / 2
            );

            b.DrawString(
                Font,
                Text,
                textPosition,
                Enabled ? TextColor : Color.DarkGray,
                0f,
                Vector2.Zero,
                Scale,
                SpriteEffects.None,
                0.9f
            );
        }

        private void DrawBorder(SpriteBatch b, Rectangle rect, int width, Color color)
        {
            // Top
            b.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Y, rect.Width, width), color);
            // Bottom
            b.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Y + rect.Height - width, rect.Width, width), color);
            // Left
            b.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Y + width, width, rect.Height - (width * 2)), color);
            // Right
            b.Draw(Game1.staminaRect, new Rectangle(rect.X + rect.Width - width, rect.Y + width, width, rect.Height - (width * 2)), color);
        }

        public override void OnClick(int x, int y)
        {
            if (!Enabled || !Visible) return;

            _isPressed = true;
            if (!string.IsNullOrEmpty(_clickSound))
                Game1.playSound(_clickSound);

            base.OnClick(x, y);
        }

        public override void OnRightClick(int x, int y)
        {
            if (!Enabled || !Visible) return;

            if (!string.IsNullOrEmpty(_clickSound))
                Game1.playSound(_clickSound);

            base.OnRightClick(x, y);
        }

        public override void OnHover(int x, int y)
        {
            if (!Enabled || !Visible) return;

            // Play hover sound only when first hovering
            if (!isHovering && Enabled)
            {
                Game1.playSound("hover");
            }
            isHovering = true;
            base.OnHover(x, y);
        }

        public override bool Contains(int x, int y)
        {
            return x >= Position.X && x <= Position.X + Size.X &&
                   y >= Position.Y && y <= Position.Y + Size.Y;
        }
    }
}