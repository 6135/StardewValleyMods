using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    /// <summary>
    /// An item instance drawn with the game's own <see cref="Item.drawInMenu(SpriteBatch, Vector2, float, float, float, StackDrawType, Color, bool)"/>,
    /// so flavored goods (wine, jelly, pickles...) keep their color tint and preserve overlay. Measures 16 x 16 at
    /// <see cref="Scale"/> 1; with an explicit <see cref="UIElement.Width"/> / <see cref="UIElement.Height"/> the item
    /// is drawn as a square fitting the bounds, centered. A null item draws nothing.
    /// </summary>
    internal sealed class ItemImage : UIElement, IUIItemImage
    {
        /// <summary>Unscaled sprite size of an item.</summary>
        private const int SpriteSize = 16;

        /// <summary>Size of the menu tile <c>drawInMenu</c> lays an item out in at scale 1 (the sprite is 16 px drawn 4x).</summary>
        private const float MenuTileSize = 64f;

        /// <summary>Layer depth vanilla menus draw items at; batches are deferred, so it only orders the item's own parts.</summary>
        private const float LayerDepth = 0.86f;

        /// <summary>Below this size (scale 2) the stack number and quality star are only a few smeared pixels, so they are hidden.</summary>
        private const float MinOverlaySize = 32f;

        /// <summary>Vanilla overlay scale in slot space (8 px star / 5x7 digits drawn 3x).</summary>
        private const float OverlayScale = 3f;

        /// <summary>Center of the quality star in slot space, as vanilla draws it.</summary>
        private const float StarCenterX = 12f;
        private const float StarCenterY = 52f;

        private float scale;

        internal ItemImage(string id, Func<Item> item, float scale) : base(id)
        {
            Item = item;
            this.scale = scale <= 0 ? 1f : scale;
        }

        public Func<Item> Item { get; set; }

        public float Scale
        {
            get => scale;
            set
            {
                scale = Math.Max(0.01f, value);
                InvalidateLayout();
            }
        }

        public UIItemStack Stack { get; set; } = UIItemStack.Hide;

        public bool DrawShadow { get; set; }

        public float Alpha { get; set; } = 1f;

        public Color Tint { get; set; } = Color.White;

        protected override Vector2 MeasureCore(Vector2 available)
        {
            return new Vector2(SpriteSize * scale, SpriteSize * scale);
        }

        // drawn at its natural scale unless an explicit size (already handled by the caller) sets the square
        protected override float MinWidthCore() => SpriteSize * scale;

        protected override void DrawCore(SpriteBatch b)
        {
            if (Bounds.Width <= 0 || Bounds.Height <= 0)
            {
                return;
            }

            Item? item = Consumer.Invoke<Item?>(Id, "ItemImage.Item", Item, null);
            if (item == null)
            {
                return;
            }

            float size = Width.HasValue || Height.HasValue ? Math.Min(Bounds.Width, Bounds.Height) : SpriteSize * scale;
            var square = new Vector2(Bounds.X + ((Bounds.Width - size) / 2f), Bounds.Y + ((Bounds.Height - size) / 2f));
            float alpha = Math.Clamp(Alpha, 0f, 1f) * (Enabled ? 1f : 0.5f);
            UIItemStack stack = size < MinOverlaySize ? UIItemStack.Hide : Stack;

            // items from other mods can throw while drawing; the guard logs and mutes instead of breaking the menu
            Consumer.Invoke(Id, "ItemImage.Draw", () => DrawItem(b, item, square, size, alpha, stack, Tint, DrawShadow));
        }

        /// <summary>
        /// Draw <paramref name="item"/> filling the <paramref name="size"/> square at <paramref name="square"/>. Items only
        /// lay out correctly at <c>drawInMenu</c> scale 1 (a 64 px slot): below it plain objects shrink toward the
        /// center and colored ones toward the top-left. So the item is drawn at scale 1 at the origin and a transform maps
        /// that slot onto the square; the overlays are drawn in the same slot space. At the native 64 px size the slot is
        /// the square, so it is drawn in place without restarting the batch.
        /// </summary>
        internal static void DrawItem(SpriteBatch b, Item item, Vector2 square, float size, float alpha, UIItemStack stack, Color tint, bool shadow)
        {
            if (Math.Abs(size - MenuTileSize) < 0.01f)
            {
                item.drawInMenu(b, square, 1f, alpha, LayerDepth, StackDrawType.Hide, tint, shadow);
                DrawOverlays(b, item, square, alpha, stack, tint);
                return;
            }

            Matrix transform = Matrix.CreateScale(size / MenuTileSize) * Matrix.CreateTranslation(square.X, square.Y, 0f);
            DrawHelper.WithTransform(b, transform, () =>
            {
                item.drawInMenu(b, Vector2.Zero, 1f, alpha, LayerDepth, StackDrawType.Hide, tint, shadow);
                DrawOverlays(b, item, Vector2.Zero, alpha, stack, tint);
            });
        }

        /// <summary>
        /// The quality star and stack number in the 64 px slot at <paramref name="slot"/>. The star is where vanilla draws
        /// it (bottom-left); the number is right-aligned inside the slot with its bottom level with the star's, instead of
        /// vanilla's spot a few pixels lower and past the right edge.
        /// </summary>
        private static void DrawOverlays(SpriteBatch b, Item item, Vector2 slot, float alpha, UIItemStack stack, Color tint)
        {
            if (stack == UIItemStack.Hide)
            {
                return;
            }

            if (item.Quality > 0)
            {
                int quality = item.Quality;
                Rectangle source = quality < 4 ? new Rectangle(338 + ((quality - 1) * 8), 400, 8, 8) : new Rectangle(346, 392, 8, 8);
                // iridium bobs like in vanilla
                float bob = quality < 4 ? 0f : ((float)Math.Cos(Game1.currentGameTime.TotalGameTime.Milliseconds * Math.PI / 512.0) + 1f) * 0.05f;
                b.Draw(Game1.mouseCursors, slot + new Vector2(StarCenterX, StarCenterY + bob), source, tint * alpha, 0f, new Vector2(4f, 4f), OverlayScale * (1f + bob), SpriteEffects.None, LayerDepth);
            }

            int count = item.Stack;
            if (stack == UIItemStack.NumberAndQuality && count > 1 && count != int.MaxValue && item.maximumStackSize() > 1)
            {
                // drawTinyDigits advances 5 * scale - 1 per digit; the last digit is 5 * scale wide
                int digits = count.ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
                float width = ((digits - 1) * ((5f * OverlayScale) - 1f)) + (5f * OverlayScale);
                const float height = 7f * OverlayScale;
                const float starBottom = StarCenterY + (4f * OverlayScale);
                Utility.drawTinyDigits(count, b, slot + new Vector2(MenuTileSize - width, starBottom - height), OverlayScale, Math.Min(1f, LayerDepth + 1E-06f), tint * alpha);
            }
        }
    }
}
