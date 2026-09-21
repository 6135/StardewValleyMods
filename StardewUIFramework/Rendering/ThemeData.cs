using System;
using System.Globalization;
using System.Reflection;
using Microsoft.Xna.Framework;

namespace UIFramework.Rendering
{
    /// <summary>
    /// One entry of the <c>Mods/6135.UIFramework/Themes</c> data asset (a <c>Dictionary&lt;string, ThemeData&gt;</c> keyed by
    /// theme name). Every member is optional: a null / default value keeps the vanilla look, so a Content Patcher pack
    /// can add a theme that only changes a couple of members. Colors and rectangles are strings so the file (and CP
    /// edits) need no custom converters: colors are <c>#RRGGBB</c>, <c>#RRGGBBAA</c>, <c>R,G,B[,A]</c> or an XNA color
    /// name (<c>Wheat</c>); rectangles are <c>x,y,w,h</c>; textures are game asset names (<c>LooseSprites/Cursors</c>).
    /// </summary>
    internal sealed class ThemeData
    {
        /// <summary>Text color (null = <c>Game1.textColor</c>).</summary>
        public string? TextColor { get; set; }

        /// <summary>Text color of disabled elements (null = the text color at half opacity).</summary>
        public string? DisabledTextColor { get; set; }

        /// <summary>Tint applied to boxes / sprites while hovered or focused (null = <c>Wheat</c>).</summary>
        public string? HoverColor { get; set; }

        /// <summary>Texture asset for panel boxes (null = <c>Game1.menuTexture</c>).</summary>
        public string? BoxTexture { get; set; }

        /// <summary>3x3 tile region of <see cref="BoxTexture"/> (null = the vanilla region, or the whole texture when a texture is given).</summary>
        public string? BoxSource { get; set; }

        /// <summary>Texture asset for button boxes (null = <c>Game1.mouseCursors</c>).</summary>
        public string? ButtonTexture { get; set; }

        /// <summary>3x3 tile region of <see cref="ButtonTexture"/>.</summary>
        public string? ButtonSource { get; set; }

        /// <summary>Texture asset for text boxes (null = <c>LooseSprites\textBox</c>).</summary>
        public string? TextBoxTexture { get; set; }

        /// <summary>Tint multiplied into every box texture (null = white, i.e. untinted). A dark tint makes vanilla boxes dark.</summary>
        public string? BoxTint { get; set; }

        /// <summary>When set, boxes are drawn as a solid fill of this color with a <see cref="BorderColor"/> outline instead of a texture (high contrast).</summary>
        public string? BoxFill { get; set; }

        /// <summary>Outline color of solid boxes (null = the text color).</summary>
        public string? BorderColor { get; set; }

        /// <summary>Outline thickness of solid boxes in pixels.</summary>
        public int BorderThickness { get; set; } = 4;

        /// <summary>Multiplier for spacing and padding (stacks, grids, panels, menu padding, button padding).</summary>
        public float SpacingScale { get; set; } = 1f;

        /// <summary>Multiplier for text size (combined with the player's text scale setting).</summary>
        public float FontScale { get; set; } = 1f;

        /// <summary>Draw text with the vanilla shadow.</summary>
        public bool ShadowEnabled { get; set; }

        /// <summary>Tint of scrollbar sprites (null = white).</summary>
        public string? ScrollbarTint { get; set; }

        /// <summary>Sound cue for button clicks (null = vanilla, empty = silent).</summary>
        public string? ClickSound { get; set; }

        /// <summary>Sound cue when the cursor enters a button (null = vanilla, empty = silent).</summary>
        public string? HoverSound { get; set; }

        /// <summary>Sound cue when a dropdown opens (null = vanilla, empty = silent).</summary>
        public string? OpenSound { get; set; }

        /// <summary>Sound cue when a dropdown closes (null = vanilla, empty = silent).</summary>
        public string? CloseSound { get; set; }

        // ---------------------------------------------------------------------------------------------------------
        //  Parsing
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Parse a color string (<c>#RRGGBB</c>, <c>#RRGGBBAA</c>, <c>R,G,B[,A]</c> or an XNA color name); null when empty or invalid.</summary>
        internal static Color? ParseColor(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            text = text.Trim();
            if (text.StartsWith('#'))
            {
                return ParseHexColor(text.Substring(1));
            }

            if (text.Contains(','))
            {
                int[]? parts = ParseInts(text);
                if (parts == null || parts.Length < 3 || parts.Length > 4)
                {
                    return null;
                }

                return new Color(Clamp255(parts[0]), Clamp255(parts[1]), Clamp255(parts[2]), parts.Length == 4 ? Clamp255(parts[3]) : 255);
            }

            PropertyInfo? named = typeof(Color).GetProperty(text, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
            return named?.PropertyType == typeof(Color) ? (Color?)named.GetValue(null) : null;
        }

        /// <summary>Parse a rectangle string (<c>x,y,w,h</c>); null when empty or invalid.</summary>
        internal static Rectangle? ParseRectangle(string? text)
        {
            int[]? parts = ParseInts(text);
            if (parts == null || parts.Length != 4)
            {
                return null;
            }

            return new Rectangle(parts[0], parts[1], Math.Max(0, parts[2]), Math.Max(0, parts[3]));
        }

        private static Color? ParseHexColor(string hex)
        {
            if ((hex.Length != 6 && hex.Length != 8) || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
            {
                return null;
            }

            if (hex.Length == 6)
            {
                value = (value << 8) | 0xFF;
            }

            return new Color((int)(value >> 24) & 0xFF, (int)(value >> 16) & 0xFF, (int)(value >> 8) & 0xFF, (int)value & 0xFF);
        }

        private static int[]? ParseInts(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            string[] tokens = text.Split(',');
            var result = new int[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!int.TryParse(tokens[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result[i]))
                {
                    return null;
                }
            }
            return result;
        }

        private static int Clamp255(int value) => Math.Clamp(value, 0, 255);
    }
}
