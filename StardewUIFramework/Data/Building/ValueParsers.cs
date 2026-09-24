using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Utilities;
using UIFramework.Api;
using UIFramework.Rendering;

namespace UIFramework.Data.Building
{
    /// <summary>
    /// The literal forms data fields accept. Case-insensitive; Newtonsoft turns JSON <c>5</c> / <c>true</c> into
    /// <c>"5"</c> / <c>"True"</c>, so every kind parses strings. Colors reuse <see cref="ThemeData.ParseColor"/> and
    /// <see cref="RichText.TryParseColor"/>, rectangles <see cref="ThemeData.ParseRectangle"/>; grid tracks are passed
    /// through unchanged (the grid parses them).
    /// </summary>
    internal static class ValueParsers
    {
        internal static readonly ValueKind<string> Text = new("text", (string text, out string value) =>
        {
            value = text;
            return true;
        });

        /// <summary>Any value, kept as the expression gives it (items and rows stay objects); a literal is its text.</summary>
        internal static readonly ValueKind<Expressions.DataValue> Raw = new("a value", (string text, out Expressions.DataValue value) =>
        {
            value = Expressions.DataValue.FromString(text);
            return true;
        });

        internal static readonly ValueKind<bool> Bool = new("true or false", TryParseBool, allowsBareExpression: true);

        internal static readonly ValueKind<int> Int = new("a whole number", TryParseInt, allowsBareExpression: true);

        internal static readonly ValueKind<double> Number = new("a number", TryParseDouble, allowsBareExpression: true);

        internal static readonly ValueKind<float> Float = new("a number", (string text, out float value) =>
        {
            bool ok = TryParseDouble(text, out double d);
            value = (float)d;
            return ok;
        }, allowsBareExpression: true);

        /// <summary>A size: a whole number, or <c>auto</c> for none.</summary>
        internal static readonly ValueKind<int?> OptionalInt = new("a whole number or 'auto'", (string text, out int? value) =>
        {
            value = null;
            string trimmed = text.Trim();
            if (trimmed.Length == 0 || trimmed.Equals("auto", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            bool ok = TryParseInt(trimmed, out int parsed);
            value = parsed;
            return ok;
        }, allowsBareExpression: true);

        internal static readonly ValueKind<Color> ColorValue = new("a color (#RRGGBB, #RRGGBBAA, R,G,B[,A] or a color name)", TryParseColor);

        internal static readonly ValueKind<Rectangle> RectangleValue = new("a rectangle 'x,y,width,height'", (string text, out Rectangle value) =>
        {
            Rectangle? parsed = ThemeData.ParseRectangle(text);
            value = parsed ?? Rectangle.Empty;
            return parsed.HasValue;
        });

        internal static readonly ValueKind<UIAlign> Align = new("Start, Center, End or Stretch", TryParseAlign);

        internal static readonly ValueKind<UIFont> Font = new("small, dialogue or tiny", TryParseEnum);

        internal static readonly ValueKind<UIAnchor> Anchor = new("an anchor (Center, TopLeft, TopCenter, TopRight, MiddleLeft, MiddleRight, BottomLeft, BottomCenter, BottomRight or Explicit)", TryParseEnum);

        internal static readonly ValueKind<UIItemStack> ItemStack = new("Hide, Quality or NumberAndQuality", TryParseEnum);

        /// <summary>Margin shorthand: <c>all</c>, <c>horizontal,vertical</c> or <c>left,top,right,bottom</c> (as left, top, right, bottom).</summary>
        internal static readonly ValueKind<int[]> Margin = new("'all', 'horizontal,vertical' or 'left,top,right,bottom'", (string text, out int[] value) =>
        {
            value = Array.Empty<int>();
            if (!TryParseInts(text, out int[] parts))
            {
                return false;
            }

            switch (parts.Length)
            {
                case 1:
                    value = new[] { parts[0], parts[0], parts[0], parts[0] };
                    return true;
                case 2:
                    value = new[] { parts[0], parts[1], parts[0], parts[1] };
                    return true;
                case 4:
                    value = parts;
                    return true;
                default:
                    return false;
            }
        });

        /// <summary>Two whole numbers <c>a,b</c> (grid cell and span shorthands).</summary>
        internal static readonly ValueKind<Point> Pair = new("two whole numbers 'a,b'", (string text, out Point value) =>
        {
            value = Point.Zero;
            if (!TryParseInts(text, out int[] parts) || parts.Length != 2)
            {
                return false;
            }

            value = new Point(parts[0], parts[1]);
            return true;
        });

        /// <summary>A SMAPI keybind list (<c>F10</c>, <c>LeftControl + U, ControllerBack</c>).</summary>
        internal static readonly ValueKind<string> Keybind = new("a keybind list (e.g. 'F10' or 'LeftControl + U')", (string text, out string value) =>
        {
            value = text;
            return KeybindList.TryParse(text, out KeybindList? parsed, out _) && parsed.IsBound;
        });

        internal static bool TryParseBool(string text, out bool value)
        {
            switch (text.Trim().ToLowerInvariant())
            {
                case "true":
                case "yes":
                case "1":
                    value = true;
                    return true;
                case "false":
                case "no":
                case "0":
                    value = false;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        internal static bool TryParseInt(string text, out int value)
        {
            if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            // "12.0" from a float literal
            if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && Math.Abs(d - Math.Round(d)) < 1e-9 && Math.Abs(d) <= int.MaxValue)
            {
                value = (int)Math.Round(d);
                return true;
            }

            return false;
        }

        internal static bool TryParseDouble(string text, out double value)
        {
            return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        internal static bool TryParseColor(string text, out Color value)
        {
            Color? parsed = ThemeData.ParseColor(text);
            if (parsed.HasValue)
            {
                value = parsed.Value;
                return true;
            }

            return RichText.TryParseColor(text.Trim(), out value);
        }

        private static bool TryParseAlign(string text, out UIAlign value)
        {
            switch (text.Trim().ToLowerInvariant())
            {
                case "left":
                case "top":
                    value = UIAlign.Start;
                    return true;
                case "right":
                case "bottom":
                    value = UIAlign.End;
                    return true;
                case "middle":
                case "centre":
                    value = UIAlign.Center;
                    return true;
                default:
                    return TryParseEnum(text, out value);
            }
        }

        private static bool TryParseEnum<TEnum>(string text, out TEnum value) where TEnum : struct, Enum
        {
            string trimmed = text.Trim();
            if (trimmed.Length > 0 && !char.IsDigit(trimmed[0]) && trimmed[0] != '-' && Enum.TryParse(trimmed, ignoreCase: true, out value) && Enum.IsDefined(value))
            {
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryParseInts(string text, out int[] values)
        {
            string[] tokens = text.Split(',');
            values = new int[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!TryParseInt(tokens[i], out values[i]))
                {
                    return false;
                }
            }

            return tokens.Length > 0;
        }
    }
}
