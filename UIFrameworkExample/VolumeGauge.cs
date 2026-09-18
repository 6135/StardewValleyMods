using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using UIFramework.Api;

namespace UIFrameworkExample
{
    /// <summary>
    /// A consumer-implemented component (<see cref="IUICustomComponent"/>): a bar that shows a 0-100 value and sets
    /// it where the player clicks. It only draws and reacts to input; layout, focus, hover and tooltips are the
    /// framework's job once it is attached with <see cref="IStardewUIApi.AddCustom"/>.
    /// </summary>
    internal sealed class VolumeGauge : IUICustomComponent
    {
        private const int Width = 240;
        private const int Height = 32;

        private readonly Func<double> getValue;
        private readonly Action<double> setValue;
        private bool hovered;
        private Rectangle lastBounds;

        public VolumeGauge(Func<double> getValue, Action<double> setValue)
        {
            this.getValue = getValue;
            this.setValue = setValue;
        }

        public bool WantsFocus => true;

        public bool WantsOverlay => false;

        public Vector2 Measure(Vector2 available) => new(Math.Min(Width, available.X), Height);

        public void Draw(SpriteBatch b, Rectangle bounds)
        {
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9), bounds.X, bounds.Y, bounds.Width, bounds.Height, hovered ? Color.Wheat : Color.White, 4f, false);
            int fill = (int)Math.Round((bounds.Width - 8) * Math.Clamp(getValue(), 0, 100) / 100.0);
            if (fill > 0)
            {
                b.Draw(Game1.staminaRect, new Rectangle(bounds.X + 4, bounds.Y + 4, fill, bounds.Height - 8), Color.LimeGreen * 0.8f);
            }
        }

        /// <summary>Nothing animates; the bounds are remembered because <see cref="OnClick"/> only receives the cursor position.</summary>
        public void Update(Rectangle bounds, double elapsedMs) => lastBounds = bounds;

        public bool OnClick(int x, int y, bool rightButton)
        {
            if (rightButton)
            {
                return false;
            }

            setValue(Math.Round(Math.Clamp((x - lastBounds.X - 4) * 100.0 / (lastBounds.Width - 8), 0, 100)));
            Game1.playSound("drumkit6");
            return true;
        }

        public void OnHover(int x, int y, bool entered) => hovered = entered;

        public bool OnKey(Keys key, bool shift, bool ctrl)
        {
            double step = shift ? 10 : 1;
            switch (key)
            {
                case Keys.Left:
                    setValue(Math.Max(0, getValue() - step));
                    return true;
                case Keys.Right:
                    setValue(Math.Min(100, getValue() + step));
                    return true;
                default:
                    return false;
            }
        }
    }
}
