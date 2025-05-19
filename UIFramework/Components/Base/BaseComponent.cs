using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace UIFramework.Components.Base
{
    public abstract class BaseComponent
    {
        public string Id { get; set; }
        public Vector2 Position { get; set; }
        public Vector2 Size { get; set; }
        public Rectangle Bounds => new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y);
        public bool Visible { get; set; } = true;
        public bool Enabled { get; set; } = true;
        public string Tooltip { get; set; }
        public float Layer { get; set; } = 0.5f;
        public float Scale { get; set; } = 1f;

        protected bool _isHovered;
        protected int _hoverTime;
        protected int _tooltipDelay = 30;

        protected BaseComponent(string id, Vector2 position, Vector2 size)
        {
            Id = id;
            Position = position;
            Size = size;
        }

        public abstract void Draw(SpriteBatch b);

        public virtual void Update(GameTime time)
        {
            if (_isHovered && !string.IsNullOrEmpty(Tooltip))
            {
                _hoverTime++;
            }
            else
            {
                _hoverTime = 0;
            }
        }

        public virtual bool Contains(int x, int y)
        {
            return Bounds.Contains(x, y);
        }

        public virtual void OnResize(Rectangle oldBounds, Rectangle newBounds)
        {
            float xRatio = newBounds.Width / (float)oldBounds.Width;
            float yRatio = newBounds.Height / (float)oldBounds.Height;

            Position = new Vector2(
                Position.X * xRatio,
                Position.Y * yRatio
            );

            Size = new Vector2(
                Size.X * xRatio,
                Size.Y * yRatio
            );
        }

        public virtual void DrawTooltip(SpriteBatch b)
        {
            if (!string.IsNullOrEmpty(Tooltip) && _hoverTime > _tooltipDelay)
            {
                Vector2 mousePos = new Vector2(StardewValley.Game1.getMouseX(), StardewValley.Game1.getMouseY());
                Vector2 textSize = StardewValley.Game1.smallFont.MeasureString(Tooltip);

                Rectangle tooltipBounds = new Rectangle(
                    (int)mousePos.X + 16,
                    (int)mousePos.Y + 16,
                    (int)textSize.X + 16,
                    (int)textSize.Y + 16
                );

                if (tooltipBounds.Right > StardewValley.Game1.viewport.Width)
                    tooltipBounds.X = StardewValley.Game1.viewport.Width - tooltipBounds.Width;

                if (tooltipBounds.Bottom > StardewValley.Game1.viewport.Height)
                    tooltipBounds.Y = StardewValley.Game1.viewport.Height - tooltipBounds.Height;

                b.Draw(
                    StardewValley.Game1.menuTexture,
                    tooltipBounds,
                    new Rectangle(0, 256, 60, 60),
                    Color.White,
                    0f,
                    Vector2.Zero,
                    SpriteEffects.None,
                    0.99f
                );

                b.DrawString(
                    StardewValley.Game1.smallFont,
                    Tooltip,
                    new Vector2(tooltipBounds.X + 8, tooltipBounds.Y + 8),
                    StardewValley.Game1.textColor,
                    0f,
                    Vector2.Zero,
                    1f,
                    SpriteEffects.None,
                    0.995f
                );
            }
        }

        public virtual void SetTooltipDelay(int delay)
        {
            _tooltipDelay = delay;
        }
    }
}