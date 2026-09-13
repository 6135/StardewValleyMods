using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Components
{
    /// <summary>
    /// A texture (or a region of one) drawn at <see cref="Scale"/> and tinted with <see cref="Tint"/>. With an explicit
    /// <see cref="UIElement.Width"/> / <see cref="UIElement.Height"/> the image is stretched into its bounds instead.
    /// A null texture draws nothing and measures 0x0. Clicks and tooltips work like on any other element.
    /// </summary>
    internal sealed class Image : UIElement, IUIImage
    {
        private Texture2D? texture;
        private Rectangle? source;
        private float scale;

        public Image(string id, Texture2D? texture, Rectangle? source, float scale) : base(id)
        {
            this.texture = texture;
            this.source = source;
            this.scale = scale <= 0 ? 1f : scale;
        }

        public Texture2D? Texture
        {
            get => texture;
            set
            {
                texture = value;
                InvalidateLayout();
            }
        }

        Texture2D IUIImage.Texture { get => texture!; set => Texture = value; }

        /// <summary>Region of <see cref="Texture"/> to draw, or null for the whole texture.</summary>
        public Rectangle? Source
        {
            get => source;
            set
            {
                source = value;
                InvalidateLayout();
            }
        }

        public float Scale
        {
            get => scale;
            set
            {
                scale = Math.Max(0.01f, value);
                InvalidateLayout();
            }
        }

        public Color Tint { get; set; } = Color.White;

        /// <summary>Pixel size of the drawn region before scaling (0x0 without a texture).</summary>
        private Point SourceSize
        {
            get
            {
                if (texture == null)
                    return Point.Zero;
                Rectangle src = source ?? texture.Bounds;
                return new Point(Math.Max(0, src.Width), Math.Max(0, src.Height));
            }
        }

        protected override Vector2 MeasureCore(Vector2 available)
        {
            Point size = SourceSize;
            return new Vector2(size.X * scale, size.Y * scale);
        }

        protected override void DrawCore(SpriteBatch b)
        {
            if (texture == null || Bounds.Width <= 0 || Bounds.Height <= 0)
                return;
            Point size = SourceSize;
            if (size.X <= 0 || size.Y <= 0)
                return;

            Color tint = Enabled ? Tint : Tint * 0.5f;
            Rectangle? src = source;

            if (Width.HasValue || Height.HasValue)
            {
                // explicit size: stretch into the bounds
                b.Draw(texture, Bounds, src, tint);
                return;
            }

            b.Draw(texture, new Vector2(Bounds.X, Bounds.Y), src, tint, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
