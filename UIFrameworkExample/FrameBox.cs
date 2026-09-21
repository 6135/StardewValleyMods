using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using UIFramework.Api;

namespace UIFrameworkExample
{
    /// <summary>
    /// A custom component that only draws chrome: a vanilla 9-slice frame around whatever built-in elements were
    /// added to its host through the <c>AddCustom(parent, id, implementation, build)</c> overload. It reports no
    /// size of its own, so the element takes the size of the embedded content.
    /// </summary>
    internal sealed class FrameBox : IUICustomComponent
    {
        private bool hovered;

        public bool WantsFocus => false;

        public bool WantsOverlay => false;

        /// <summary>Zero: the framework then sizes the element to its embedded content.</summary>
        public Vector2 Measure(Vector2 available) => Vector2.Zero;

        public void Draw(SpriteBatch b, Rectangle bounds)
        {
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), bounds.X, bounds.Y, bounds.Width, bounds.Height, hovered ? Color.Wheat : Color.White, 1f, false);
        }

        public void Update(Rectangle bounds, double elapsedMs)
        {
        }

        /// <summary>Clicks on the frame itself (not on an embedded element) are not handled.</summary>
        public bool OnClick(int x, int y, bool rightButton) => false;

        public void OnHover(int x, int y, bool entered) => hovered = entered;

        public bool OnKey(Keys key, bool shift, bool ctrl) => false;
    }
}
