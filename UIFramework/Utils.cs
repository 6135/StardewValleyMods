using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System;
using UIFramework.memory;
using SObject = StardewValley.Object;

#nullable enable

namespace UIFramework
{
    /// <summary>
    /// Provides a set of tools to be used by multiple classes of the mod.
    /// </summary>
    public class Utils
    {
        public static void drawTextureBox(SpriteBatch b, Texture2D texture, Rectangle sourceRect, int x, int y, int width, int height, Color color, float scale = 1f, bool drawShadow = true, float draw_layer = -1f)
        {
            int num = sourceRect.Width / 3;
            float layerDepth = draw_layer - 0.03f;
            if (draw_layer < 0f)
            {
                draw_layer = 0.8f - ((float)y * 1E-06f);
                layerDepth = 0.77f;
            }

            if (drawShadow)
            {
                b.Draw(texture, new Vector2(x + width - (int)((float)num * scale) - 8, y + 8), new Rectangle(sourceRect.X + (num * 2), sourceRect.Y, num, num), Color.Black * 0.4f, 0f, Vector2.Zero, scale, SpriteEffects.None, layerDepth);
                b.Draw(texture, new Vector2(x - 8, y + height - (int)((float)num * scale) + 8), new Rectangle(sourceRect.X, (num * 2) + sourceRect.Y, num, num), Color.Black * 0.4f, 0f, Vector2.Zero, scale, SpriteEffects.None, layerDepth);
                b.Draw(texture, new Vector2(x + width - (int)((float)num * scale) - 8, y + height - (int)((float)num * scale) + 8), new Rectangle(sourceRect.X + (num * 2), (num * 2) + sourceRect.Y, num, num), Color.Black * 0.4f, 0f, Vector2.Zero, scale, SpriteEffects.None, layerDepth);
                b.Draw(texture, new Rectangle(x + (int)((float)num * scale) - 8, y + 8, width - ((int)((float)num * scale) * 2), (int)((float)num * scale)), new Rectangle(sourceRect.X + num, sourceRect.Y, num, num), Color.Black * 0.4f, 0f, Vector2.Zero, SpriteEffects.None, layerDepth);
                b.Draw(texture, new Rectangle(x + (int)((float)num * scale) - 8, y + height - (int)((float)num * scale) + 8, width - ((int)((float)num * scale) * 2), (int)((float)num * scale)), new Rectangle(sourceRect.X + num, (num * 2) + sourceRect.Y, num, num), Color.Black * 0.4f, 0f, Vector2.Zero, SpriteEffects.None, layerDepth);
                b.Draw(texture, new Rectangle(x - 8, y + (int)((float)num * scale) + 8, (int)((float)num * scale), height - ((int)((float)num * scale) * 2)), new Rectangle(sourceRect.X, num + sourceRect.Y, num, num), Color.Black * 0.4f, 0f, Vector2.Zero, SpriteEffects.None, layerDepth);
                b.Draw(texture, new Rectangle(x + width - (int)((float)num * scale) - 8, y + (int)((float)num * scale) + 8, (int)((float)num * scale), height - ((int)((float)num * scale) * 2)), new Rectangle(sourceRect.X + (num * 2), num + sourceRect.Y, num, num), Color.Black * 0.4f, 0f, Vector2.Zero, SpriteEffects.None, layerDepth);
                b.Draw(texture, new Rectangle((int)((float)num * scale / 2f) + x - 8, (int)((float)num * scale / 2f) + y + 8, width - (int)((float)num * scale), height - (int)((float)num * scale)), new Rectangle(num + sourceRect.X, num + sourceRect.Y, num, num), Color.Black * 0.4f, 0f, Vector2.Zero, SpriteEffects.None, layerDepth);
            }

            b.Draw(texture, new Rectangle((int)((float)num * scale) + x, (int)((float)num * scale) + y, width - (int)((float)num * scale * 2f), height - (int)((float)num * scale * 2f)), new Rectangle(num + sourceRect.X, num + sourceRect.Y, num, num), color, 0f, Vector2.Zero, SpriteEffects.None, draw_layer);
            b.Draw(texture, new Vector2(x, y), new Rectangle(sourceRect.X, sourceRect.Y, num, num), color, 0f, Vector2.Zero, scale, SpriteEffects.None, draw_layer);
            b.Draw(texture, new Vector2(x + width - (int)((float)num * scale), y), new Rectangle(sourceRect.X + (num * 2), sourceRect.Y, num, num), color, 0f, Vector2.Zero, scale, SpriteEffects.None, draw_layer);
            b.Draw(texture, new Vector2(x, y + height - (int)((float)num * scale)), new Rectangle(sourceRect.X, (num * 2) + sourceRect.Y, num, num), color, 0f, Vector2.Zero, scale, SpriteEffects.None, draw_layer);
            b.Draw(texture, new Vector2(x + width - (int)((float)num * scale), y + height - (int)((float)num * scale)), new Rectangle(sourceRect.X + (num * 2), (num * 2) + sourceRect.Y, num, num), color, 0f, Vector2.Zero, scale, SpriteEffects.None, draw_layer);
            b.Draw(texture, new Rectangle(x + (int)((float)num * scale), y, width - ((int)((float)num * scale) * 2), (int)((float)num * scale)), new Rectangle(sourceRect.X + num, sourceRect.Y, num, num), color, 0f, Vector2.Zero, SpriteEffects.None, draw_layer);
            b.Draw(texture, new Rectangle(x + (int)((float)num * scale), y + height - (int)((float)num * scale), width - ((int)((float)num * scale) * 2), (int)((float)num * scale)), new Rectangle(sourceRect.X + num, (num * 2) + sourceRect.Y, num, num), color, 0f, Vector2.Zero, SpriteEffects.None, draw_layer);
            b.Draw(texture, new Rectangle(x, y + (int)((float)num * scale), (int)((float)num * scale), height - ((int)((float)num * scale) * 2)), new Rectangle(sourceRect.X, num + sourceRect.Y, num, num), color, 0f, Vector2.Zero, SpriteEffects.None, draw_layer);
            b.Draw(texture, new Rectangle(x + width - (int)((float)num * scale), y + (int)((float)num * scale), (int)((float)num * scale), height - ((int)((float)num * scale) * 2)), new Rectangle(sourceRect.X + (num * 2), num + sourceRect.Y, num, num), color, 0f, Vector2.Zero, SpriteEffects.None, draw_layer);
        }
    }
}