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

            // Determine button color based on state
            Color boxColor = Color.White;
            if (!Enabled)
                boxColor = Color.Gray;
            else if (isPressed)
                boxColor = PressedColor;
            else if (isHovering)
                boxColor = HoverColor;

            // Draw button background using Stardew's texture box method
            Utils.drawTextureBox(
                b,
                Game1.mouseCursors,
                new Rectangle(432, 439, 9, 9),
                (int)Position.X,
                (int)Position.Y,
                (int)Size.X,
                (int)Size.Y,
                boxColor,
                4f,
                false
            );

            // Draw text centered in the button
            b.DrawString(
                Font,
                Text,
                new Vector2(
                    Position.X + (Size.X / 2) - (Font.MeasureString(Text).X / 2),
                    Position.Y + (Size.Y / 2) - (Font.MeasureString(Text).Y / 2)
                ),
                Enabled ? TextColor : Color.DarkGray,
                0f,
                Vector2.Zero,
                Scale,
                SpriteEffects.None,
                0.9f
            );
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