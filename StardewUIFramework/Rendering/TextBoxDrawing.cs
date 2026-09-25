using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Rendering
{
    /// <summary>
    /// Vanilla text box look shared by <see cref="Components.TextInput"/> and <see cref="Components.NumberInput"/>: the three-slice draw of
    /// <c>LooseSprites\textBox</c> (16px caps), text at (+16, +12), a 4x32 caret blinking on a one second cycle and
    /// text scrolled in from the left when it no longer fits.
    /// </summary>
    internal static class TextBoxDrawing
    {
        /// <summary>Width of the left / right caps of the box texture.</summary>
        internal const int CapWidth = 16;

        /// <summary>Text offset from the box origin (vanilla <c>TextBox</c>).</summary>
        internal const int TextOffsetX = 16;
        internal const int TextOffsetY = 12;

        /// <summary>Caret rectangle (vanilla <c>TextBox</c>).</summary>
        internal const int CaretOffsetY = 8;
        internal const int CaretWidth = 4;
        internal const int CaretHeight = 32;

        /// <summary>Horizontal room reserved for the caps and caret when clipping text (<c>TextOption</c>'s write bar offset).</summary>
        internal const int TextInset = 26;

        /// <summary>Height of the vanilla box; the text / caret offsets above are relative to it.</summary>
        internal const int VanillaHeight = 48;

        private static Point vanillaSize = new(192, VanillaHeight);

        /// <summary>
        /// Size of the vanilla text box texture. Assumed to be 192x48 until the texture is first drawn so layout
        /// never needs the game content; <see cref="Resolve"/> corrects it if a retexture changed the size.
        /// </summary>
        internal static Point DefaultSize => vanillaSize;

        /// <summary>Natural size of a box using <paramref name="custom"/> (or the themed / vanilla texture when null), grown so scaled text still fits.</summary>
        internal static Vector2 Measure(Texture2D? custom, UIFont font)
        {
            Vector2 size = custom != null ? new Vector2(custom.Width, custom.Height) : vanillaSize.ToVector2();
            size.X = Theme.ScaleForText((int)size.X);
            size.Y += Theme.ExtraTextHeight(font);
            return size;
        }

        /// <summary>Characters the narrowest box still shows (the text clips from the left, keeping the caret end visible).</summary>
        private const string MinVisibleText = "000";

        /// <summary>
        /// Narrowest box width: both caps (or the caps-and-caret inset the text is clipped to, if wider) plus room for
        /// <see cref="MinVisibleText"/> in <paramref name="font"/>. Pure.
        /// </summary>
        internal static float MinWidth(UIFont font) => Math.Max(2 * CapWidth, TextInset) + UIServices.Text.Measure(font, MinVisibleText, 1f).X;

        /// <summary>The texture to draw: <paramref name="custom"/> or the vanilla one. <paramref name="sizeChanged"/> is true when the vanilla size assumption was wrong (re-layout).</summary>
        internal static Texture2D Resolve(Texture2D? custom, out bool sizeChanged)
        {
            sizeChanged = false;
            if (custom != null)
            {
                return custom;
            }

            Texture2D tex = Theme.TextBoxTexture;
            var size = new Point(tex.Width, tex.Height);
            if (size != vanillaSize)
            {
                vanillaSize = size;
                sizeChanged = true;
            }
            return tex;
        }

        /// <summary>
        /// Draw the box as three slices (left cap, stretched middle, right cap) into <paramref name="bounds"/>, tinted
        /// by the theme; a theme with a solid box fill draws a filled, outlined rectangle instead.
        /// </summary>
        internal static void DrawBox(SpriteBatch b, Texture2D texture, Rectangle bounds, Color tint)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            Color? fill = Theme.BoxFill;
            if (fill.HasValue)
            {
                DrawHelper.Fill(b, bounds, fill.Value);
                DrawHelper.Outline(b, bounds, tint == Color.White ? Theme.BorderColor : tint, Theme.BorderThickness);
                return;
            }

            tint = DrawHelper.Multiply(tint, Theme.BoxTint);
            int width = Math.Max(bounds.Width, 2 * CapWidth);
            int texH = texture.Height;
            b.Draw(texture, new Rectangle(bounds.X, bounds.Y, CapWidth, bounds.Height), new Rectangle(0, 0, CapWidth, texH), tint);
            b.Draw(texture, new Rectangle(bounds.X + CapWidth, bounds.Y, width - (2 * CapWidth), bounds.Height), new Rectangle(CapWidth, 0, 4, texH), tint);
            b.Draw(texture, new Rectangle(bounds.X + width - CapWidth, bounds.Y, CapWidth, bounds.Height), new Rectangle(texture.Width - CapWidth, 0, CapWidth, texH), tint);
        }

        /// <summary>
        /// The longest end of <paramref name="text"/> that fits in <paramref name="maxWidth"/> pixels (characters dropped
        /// from the left). The width only shrinks as the start moves right, so the start is binary-searched.
        /// </summary>
        internal static string ClipLeft(UIFont font, string text, int maxWidth)
        {
            if (text.Length == 0 || UIServices.Text.Measure(font, text, 1f).X <= maxWidth)
            {
                return text;
            }

            // lo: a start known too wide; hi: a start known to fit (the empty end always does)
            int lo = 0, hi = text.Length;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (UIServices.Text.Measure(font, text.Substring(mid), 1f).X <= maxWidth)
                {
                    hi = mid;
                }
                else
                {
                    lo = mid;
                }
            }

            return text.Substring(hi);
        }

        /// <summary>Whether the caret is in the visible half of its blink cycle (always visible under reduced motion).</summary>
        internal static bool CaretVisible => Theme.ReducedMotion || UIServices.NowMs() % 1000 >= 500;

        /// <summary>Draw the (clipped) text and, when <paramref name="caret"/> is setter, the blinking caret after it.</summary>
        internal static void DrawText(SpriteBatch b, Rectangle bounds, string text, UIFont font, Color color, bool shadow, bool caret)
        {
            int centerShift = (bounds.Height - VanillaHeight) / 2;
            string visible = ClipLeft(font, text, bounds.Width - TextInset);
            Vector2 size = visible.Length > 0 ? UIServices.Text.Measure(font, visible, 1f) : Vector2.Zero;

            if (caret && CaretVisible)
            {
                int caretHeight = CaretHeight + Theme.ExtraTextHeight(font);
                var caretRect = new Rectangle(bounds.X + TextOffsetX + (int)size.X + 2, bounds.Y + CaretOffsetY + centerShift, CaretWidth, caretHeight);
                b.Draw(Game1.staminaRect, caretRect, color);
            }
            if (visible.Length > 0)
            {
                DrawHelper.Text(b, visible, font, new Vector2(bounds.X + TextOffsetX, bounds.Y + TextOffsetY + centerShift), color, shadow, 1f);
            }
        }

        /// <summary>
        /// Keys a text field consumes as typing (letters, digits, punctuation, space, backspace, delete). Returning
        /// true for them from <c>HandleKey</c> keeps the game's menu button from closing the menu mid-sentence.
        /// </summary>
        internal static bool IsTypingKey(Keys key)
        {
            bool letterOrDigit = (key >= Keys.A && key <= Keys.Z) || (key >= Keys.D0 && key <= Keys.D9);
            bool numPad = key >= Keys.NumPad0 && key <= Keys.Divide;
            return letterOrDigit || numPad || PunctuationKeys.Contains(key);
        }

        /// <summary>Space, editing keys and the OEM punctuation keys.</summary>
        private static readonly HashSet<Keys> PunctuationKeys = new()
        {
            Keys.Space, Keys.Back, Keys.Delete,
            Keys.OemSemicolon, Keys.OemPlus, Keys.OemComma, Keys.OemMinus, Keys.OemPeriod, Keys.OemQuestion, Keys.OemTilde,
            Keys.OemOpenBrackets, Keys.OemPipe, Keys.OemCloseBrackets, Keys.OemQuotes, Keys.Oem8, Keys.OemBackslash
        };
    }
}
