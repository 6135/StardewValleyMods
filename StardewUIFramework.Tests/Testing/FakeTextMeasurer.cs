using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using UIFramework.Api;
using UIFramework.Core;

namespace StardewUIFramework.Tests.Testing
{
    /// <summary>
    /// Monospace text measurement for headless tests: every character is <see cref="CharWidth"/> px wide (times the
    /// scale) and every line <see cref="LineHeightPx"/> px tall, whatever the font. Wrapping is greedy on spaces.
    /// </summary>
    public sealed class FakeTextMeasurer : ITextMeasurer
    {
        /// <summary>Width of one character at scale 1.</summary>
        public const int CharWidth = 8;

        /// <summary>Height of one line at scale 1.</summary>
        public const int LineHeightPx = 16;

        public Vector2 Measure(UIFont font, string text, float scale)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new Vector2(0, LineHeightPx * scale);
            }

            string[] lines = text.Split('\n');
            int longest = 0;
            foreach (string line in lines)
            {
                longest = Math.Max(longest, line.Length);
            }
            return new Vector2(longest * CharWidth * scale, lines.Length * LineHeightPx * scale);
        }

        public string Wrap(UIFont font, string text, int width)
        {
            int maxChars = Math.Max(1, width / CharWidth);
            var result = new StringBuilder();
            foreach (string paragraph in (text ?? string.Empty).Split('\n'))
            {
                if (result.Length > 0)
                {
                    result.Append('\n');
                }

                result.Append(string.Join('\n', WrapParagraph(paragraph, maxChars)));
            }
            return result.ToString();
        }

        private static IEnumerable<string> WrapParagraph(string paragraph, int maxChars)
        {
            var line = new StringBuilder();
            foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > maxChars)
                {
                    yield return line.ToString();
                    line.Clear();
                }

                if (line.Length > 0)
                {
                    line.Append(' ');
                }

                line.Append(word);
            }
            yield return line.ToString();
        }

        public float LineHeight(UIFont font) => LineHeightPx;
    }
}
