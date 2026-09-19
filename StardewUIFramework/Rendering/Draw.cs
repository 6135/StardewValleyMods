using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Rendering
{
    /// <summary>SpriteBatch helpers: 9-slice boxes, text, scissor clipping, debug overlay.</summary>
    internal static class DrawHelper
    {
        private static readonly RasterizerState ScissorState = new() { ScissorTestEnable = true };

        /// <summary>Draw a 9-slice box. <paramref name="source"/> is the 3x3 tile region in <paramref name="texture"/>.</summary>
        internal static void Box(SpriteBatch b, Texture2D texture, Rectangle source, Rectangle rect, Color color, float scale, bool shadow = false)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            IClickableMenu.drawTextureBox(b, texture, source, rect.X, rect.Y, rect.Width, rect.Height, color, scale, shadow);
        }

        /// <summary>The theme's panel box (vanilla <c>Game1.menuTexture</c> by default), used by HUD widgets and toasts.</summary>
        internal static void PanelBox(SpriteBatch b, Rectangle rect, Color tint)
        {
            ThemedBox(b, Theme.PanelTexture, Theme.PanelBoxSource, rect, tint, 1f);
        }

        /// <summary>
        /// Draw a box the theme's way: a solid fill with an outline when the theme sets <see cref="Theme.BoxFill"/>
        /// (the outline takes <paramref name="tint"/> when it is not white, so hover / disabled states stay visible),
        /// otherwise the 9-slice <paramref name="texture"/> multiplied by <see cref="Theme.BoxTint"/>.
        /// </summary>
        internal static void ThemedBox(SpriteBatch b, Texture2D texture, Rectangle source, Rectangle rect, Color tint, float scale)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            Color? fill = Theme.BoxFill;
            if (fill.HasValue)
            {
                Fill(b, rect, fill.Value);
                Outline(b, rect, tint == Color.White ? Theme.BorderColor : tint, Theme.BorderThickness);
                return;
            }

            Box(b, texture, source, rect, Multiply(tint, Theme.BoxTint), scale);
        }

        /// <summary>
        /// The box behind a panel (<paramref name="button"/> = false) or a button: the element's own texture / source
        /// when its style sets one (drawn untouched, as before themes), otherwise the theme's box for that kind.
        /// </summary>
        internal static void StyledBox(SpriteBatch b, in ResolvedStyle style, bool button, Rectangle rect, Color tint)
        {
            float scale = style.BoxScale ?? (button ? 4f : 1f);
            if (style.BoxTexture != null)
            {
                Box(b, style.BoxTexture, style.BoxSource ?? style.BoxTexture.Bounds, rect, tint, scale);
                return;
            }

            Texture2D texture = button ? Theme.ButtonTexture : Theme.PanelTexture;
            Rectangle source = style.BoxSource ?? (button ? Theme.ButtonBoxSource : Theme.PanelBoxSource);
            ThemedBox(b, texture, source, rect, tint, scale);
        }

        /// <summary>Component-wise product of two colors (white is the identity).</summary>
        internal static Color Multiply(Color a, Color b)
        {
            if (b == Color.White)
            {
                return a;
            }

            return new Color(a.R * b.R / 255, a.G * b.G / 255, a.B * b.B / 255, a.A * b.A / 255);
        }

        /// <summary>Draw a text string at a position (optionally with the vanilla shadow). <paramref name="scale"/> is multiplied by the theme's font scale.</summary>
        internal static void Text(SpriteBatch b, string content, UIFont font, Vector2 position, Color color, bool shadow, float scale)
        {
            if (string.IsNullOrEmpty(content))
            {
                return;
            }

            scale *= Theme.FontScale;
            SpriteFont spriteFont = GameTextMeasurer.GetFont(font);
            if (shadow)
            {
                Utility.drawTextWithShadow(b, content, spriteFont, position, color, scale);
            }
            else
            {
                b.DrawString(spriteFont, content, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>Draw text aligned horizontally inside a rectangle (top-aligned vertically).</summary>
        internal static void TextInRect(SpriteBatch b, string text, UIFont font, Rectangle rect, Color color, bool shadow, float scale, UIAlign horizontal)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Vector2 size = UIServices.Text.Measure(font, text, scale);
            float x = rect.X + LayoutEngine.AlignOffset(horizontal == UIAlign.Stretch ? UIAlign.Start : horizontal, rect.Width, (int)size.X);
            Text(b, text, font, new Vector2((int)x, rect.Y), color, shadow, scale);
        }

        /// <summary>Solid rectangle (uses <c>Game1.staminaRect</c>).</summary>
        internal static void Fill(SpriteBatch b, Rectangle rect, Color color)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            b.Draw(Game1.staminaRect, rect, color);
        }

        /// <summary>1px outline.</summary>
        internal static void Outline(SpriteBatch b, Rectangle rect, Color color, int thickness = 1)
        {
            Fill(b, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            Fill(b, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            Fill(b, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            Fill(b, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        /// <summary>
        /// Run <paramref name="draw"/> with the scissor rectangle set to <paramref name="clip"/> (intersected with any
        /// outer clip). Ends the current batch and restarts it with the parameters the game uses for menus
        /// (Deferred / AlphaBlend / PointClamp), then restores them. The only place the framework calls End/Begin.
        /// </summary>
        internal static void WithScissor(SpriteBatch b, Rectangle clip, Action draw)
        {
            GraphicsDevice device = b.GraphicsDevice;
            Rectangle outer = device.ScissorRectangle;
            bool outerEnabled = device.RasterizerState?.ScissorTestEnable ?? false;
            Rectangle effective = outerEnabled ? Rectangle.Intersect(outer, clip) : clip;
            effective = Rectangle.Intersect(effective, device.Viewport.Bounds);
            if (effective.Width <= 0 || effective.Height <= 0)
            {
                return;
            }

            b.End();
            device.ScissorRectangle = effective;
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, ScissorState);
            try
            {
                draw();
            }
            finally
            {
                b.End();
                device.ScissorRectangle = outer;
                if (outerEnabled)
                {
                    b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, ScissorState);
                }
                else
                {
                    b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                }
            }
        }

        /// <summary>Debug overlay: bounds outline plus the id in tiny text.</summary>
        internal static void DebugBounds(SpriteBatch b, Rectangle rect, string id, Color? color = null)
        {
            Color c = color ?? Color.Lime * 0.8f;
            Outline(b, rect, c);
            if (!string.IsNullOrEmpty(id))
            {
                b.DrawString(Game1.tinyFont, id, new Vector2(rect.X + 2, rect.Y + 1), c, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            }
        }
    }
}
