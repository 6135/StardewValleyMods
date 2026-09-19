using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Rendering
{
    /// <summary>What a <see cref="RichRun"/> holds.</summary>
    internal enum RichRunKind
    {
        /// <summary>A piece of text with one style.</summary>
        Text,
        /// <summary>An inline item sprite (<c>[icon=(O)24]</c>).</summary>
        Icon
    }

    /// <summary>One styled piece of a parsed markup string.</summary>
    internal sealed class RichRun
    {
        internal RichRun(RichRunKind kind, string text, Color? color, bool bold, string? link)
        {
            Kind = kind;
            Text = text;
            Color = color;
            Bold = bold;
            Link = link;
        }

        internal RichRunKind Kind { get; }

        /// <summary>The text (<see cref="RichRunKind.Text"/>) or the qualified item id (<see cref="RichRunKind.Icon"/>).</summary>
        internal string Text { get; set; }

        internal Color? Color { get; }
        internal bool Bold { get; }

        /// <summary>Link name when inside <c>[link=name]…[/link]</c>.</summary>
        internal string? Link { get; }
    }

    /// <summary>A parsed markup string: the runs in order plus a few facts the components need.</summary>
    internal sealed class RichDocument
    {
        internal static readonly RichDocument Empty = new(Array.Empty<RichRun>());

        internal RichDocument(RichRun[] runs)
        {
            Runs = runs;
            foreach (RichRun run in runs)
            {
                HasLinks |= run.Link != null;
            }
        }

        internal RichRun[] Runs { get; }

        /// <summary>True when at least one run is a link (the owning label then takes clicks).</summary>
        internal bool HasLinks { get; }
    }

    /// <summary>A run (or part of one) placed on a line.</summary>
    internal sealed class RichFragment
    {
        internal RichFragment(RichRun run, string text)
        {
            Run = run;
            Text = text;
        }

        internal RichRun Run { get; }
        internal string Text { get; set; }

        /// <summary>Left edge relative to the line start.</summary>
        internal float X { get; set; }

        internal float Width { get; set; }
    }

    /// <summary>One laid-out line.</summary>
    internal sealed class RichLine
    {
        internal List<RichFragment> Fragments { get; } = new();
        internal float Width { get; set; }

        /// <summary>Top edge relative to the layout origin.</summary>
        internal float Y { get; set; }
    }

    /// <summary>The result of laying a document out: lines, fragments and the total size.</summary>
    internal sealed class RichLayout
    {
        internal RichLayout(RichDocument document, UIFont font, float scale, float lineHeight)
        {
            Document = document;
            Font = font;
            Scale = scale;
            LineHeight = lineHeight;
        }

        internal RichDocument Document { get; }
        internal UIFont Font { get; }
        internal float Scale { get; }
        internal float LineHeight { get; }
        internal List<RichLine> Lines { get; } = new();
        internal Vector2 Size { get; set; }

        /// <summary>The link under a point, given the rectangle and alignment the layout is drawn with, or null.</summary>
        internal string? LinkAt(Rectangle bounds, UIAlign align, int px, int py)
        {
            if (!Document.HasLinks || !bounds.Contains(px, py))
            {
                return null;
            }

            float relY = py - bounds.Y;
            foreach (RichLine line in Lines)
            {
                if (relY < line.Y || relY >= line.Y + LineHeight)
                {
                    continue;
                }

                float relX = px - bounds.X - RichText.LineOffset(align, bounds.Width, line.Width);
                foreach (RichFragment f in line.Fragments)
                {
                    if (f.Run.Link != null && relX >= f.X && relX < f.X + f.Width)
                    {
                        return f.Run.Link;
                    }
                }
            }

            return null;
        }
    }

    /// <summary>
    /// A tiny markup language for labels, buttons and tooltip lines:
    /// <c>[color=#RRGGBB]…[/color]</c> / <c>[color=red]</c>, <c>[b]…[/b]</c>, <c>[icon=(O)24]</c>,
    /// <c>[link=name]…[/link]</c>, and <c>[[</c> / <c>]]</c> for literal brackets. Unknown tags are drawn verbatim.
    /// Parsing and layout only use <see cref="UIServices.Text"/>, so both run without the game.
    /// </summary>
    internal static class RichText
    {
        /// <summary>Extra width of a bold fragment (<c>Utility.drawBoldText</c> repeats the text ±1 px).</summary>
        private const int BoldExtra = 2;

        /// <summary>Gap between an inline icon and the text around it.</summary>
        private const int IconGap = 4;

        private const int UnderlineThickness = 2;

        internal static readonly Color LinkColor = Color.RoyalBlue;
        internal static readonly Color LinkHoverColor = Color.DarkOrange;

        // ---------------------------------------------------------------------------------------------------------
        //  Parsing
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Parse <paramref name="markup"/> into runs (pseudo-localizing the plain text when the mode is on).</summary>
        internal static RichDocument Parse(string? markup)
        {
            if (string.IsNullOrEmpty(markup))
            {
                return RichDocument.Empty;
            }

            var state = new ParseState();
            int i = 0;
            while (i < markup.Length)
            {
                char c = markup[i];
                if ((c == '[' || c == ']') && i + 1 < markup.Length && markup[i + 1] == c)
                {
                    state.Append(c);
                    i += 2;
                    continue;
                }

                if (c == '[' && TryReadTag(markup, i, out string tag, out int end) && state.ApplyTag(tag))
                {
                    i = end;
                    continue;
                }

                state.Append(c);
                i++;
            }

            List<RichRun> runs = state.Finish();
            if (Pseudo.Enabled)
            {
                PseudoLocalize(runs);
            }

            return new RichDocument(runs.ToArray());
        }

        /// <summary>Read the tag body between <c>[</c> at <paramref name="start"/> and the next <c>]</c>.</summary>
        private static bool TryReadTag(string markup, int start, out string tag, out int end)
        {
            int close = markup.IndexOf(']', start + 1);
            tag = string.Empty;
            end = start;
            if (close < 0 || close == start + 1)
            {
                return false;
            }

            tag = markup.Substring(start + 1, close - start - 1);
            end = close + 1;
            return true;
        }

        /// <summary>Mutable style stack used while parsing; owns the run list and the pending text.</summary>
        private sealed class ParseState
        {
            private readonly List<RichRun> runs = new();
            private readonly StringBuilder text = new();
            private readonly Stack<Color?> colors = new();
            private readonly Stack<string?> links = new();
            private int bold;

            private Color? CurrentColor => colors.Count > 0 ? colors.Peek() : null;
            private string? CurrentLink => links.Count > 0 ? links.Peek() : null;

            internal void Append(char c) => text.Append(c);

            /// <summary>Apply a tag (after flushing the pending text); returns false for an unknown tag, which the caller draws verbatim.</summary>
            internal bool ApplyTag(string tag)
            {
                Action? change = ResolveTag(tag);
                if (change == null)
                {
                    return false;
                }

                Flush();
                change();
                return true;
            }

            /// <summary>The state change a tag stands for, or null when it is not a tag we know.</summary>
            private Action? ResolveTag(string tag)
            {
                string lower = tag.ToLowerInvariant();
                switch (lower)
                {
                    case "b":
                        return () => bold++;
                    case "/b":
                        return () => bold = Math.Max(0, bold - 1);
                    case "/color":
                        return () => Pop(colors);
                    case "/link":
                        return () => Pop(links);
                }

                if (lower.StartsWith("icon=", StringComparison.Ordinal))
                {
                    string id = tag.Substring(5).Trim();
                    return () => runs.Add(new RichRun(RichRunKind.Icon, id, CurrentColor, bold > 0, CurrentLink));
                }

                if (lower.StartsWith("color=", StringComparison.Ordinal) && TryParseColor(tag.Substring(6).Trim(), out Color color))
                {
                    return () => colors.Push(color);
                }

                if (lower.StartsWith("link=", StringComparison.Ordinal))
                {
                    string name = tag.Substring(5).Trim();
                    return () => links.Push(name);
                }

                return null;
            }

            private static void Pop<T>(Stack<T> stack)
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }
            }

            /// <summary>Emit the pending text as a run with the current style.</summary>
            private void Flush()
            {
                if (text.Length == 0)
                {
                    return;
                }

                runs.Add(new RichRun(RichRunKind.Text, text.ToString(), CurrentColor, bold > 0, CurrentLink));
                text.Clear();
            }

            /// <summary>Flush the tail and return the runs.</summary>
            internal List<RichRun> Finish()
            {
                Flush();
                return runs;
            }
        }

        /// <summary><c>#RRGGBB</c>, <c>#RRGGBBAA</c> or a named color.</summary>
        internal static bool TryParseColor(string value, out Color color)
        {
            color = Color.White;
            if (value.Length > 0 && value[0] == '#')
            {
                return TryParseHex(value.Substring(1), out color);
            }

            Color? named = value.ToLowerInvariant() switch
            {
                "red" => Color.Red,
                "green" => Color.Green,
                "blue" => Color.Blue,
                "gray" or "grey" => Color.Gray,
                "white" => Color.White,
                "black" => Color.Black,
                "yellow" => Color.Goldenrod,
                "orange" => Color.DarkOrange,
                "purple" => Color.Purple,
                _ => null
            };
            color = named ?? color;
            return named.HasValue;
        }

        private static bool TryParseHex(string hex, out Color color)
        {
            color = Color.White;
            if ((hex.Length != 6 && hex.Length != 8) || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
            {
                return false;
            }

            if (hex.Length == 6)
            {
                value = (value << 8) | 0xFF;
            }

            color = new Color((int)(value >> 24) & 0xFF, (int)(value >> 16) & 0xFF, (int)(value >> 8) & 0xFF, (int)value & 0xFF);
            return true;
        }

        /// <summary>Accent every text run and bracket / pad the whole document (tags, ids and link names are untouched).</summary>
        private static void PseudoLocalize(List<RichRun> runs)
        {
            RichRun? first = null;
            RichRun? last = null;
            int length = 0;
            foreach (RichRun run in runs)
            {
                if (run.Kind != RichRunKind.Text)
                {
                    continue;
                }

                first ??= run;
                last = run;
                length += run.Text.Length;
                run.Text = Pseudo.Accent(run.Text);
            }

            if (first == null || last == null)
            {
                return;
            }

            first.Text = "[" + first.Text;
            last.Text += Pseudo.Padding(length) + "]";
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Layout / measure
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Measure <paramref name="markup"/> as the given font would draw it, wrapped to <paramref name="maxWidth"/> (≤ 0 = no wrapping).</summary>
        internal static Vector2 Measure(string? markup, UIFont font, float scale, int maxWidth)
        {
            return Layout(Parse(markup), font, scale, maxWidth).Size;
        }

        /// <summary>Break the document into lines: wraps on spaces, a span may continue across lines, icons are line-height squares.</summary>
        internal static RichLayout Layout(RichDocument document, UIFont font, float scale, int maxWidth)
        {
            float lineHeight = UIServices.Text.LineHeight(font) * scale;
            var layout = new RichLayout(document, font, scale, lineHeight);
            var builder = new LineBuilder(layout, maxWidth);
            foreach (RichRun run in document.Runs)
            {
                if (run.Kind == RichRunKind.Icon)
                {
                    builder.Add(run, run.Text, lineHeight + IconGap);
                    continue;
                }

                foreach (string token in Tokenize(run.Text))
                {
                    if (token == "\n")
                    {
                        builder.NewLine();
                        continue;
                    }

                    builder.Add(run, token, MeasureText(run, token, font, scale));
                }
            }

            builder.Finish();
            return layout;
        }

        /// <summary>Width of a text piece as drawn (bold adds the offset copies).</summary>
        private static float MeasureText(RichRun run, string text, UIFont font, float scale)
        {
            float w = UIServices.Text.Measure(font, text, scale).X;
            return run.Bold ? w + BoldExtra : w;
        }

        /// <summary>Split text into words, single spaces and newlines so wrapping can happen between them.</summary>
        private static IEnumerable<string> Tokenize(string text)
        {
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != ' ' && c != '\n')
                {
                    continue;
                }

                if (i > start)
                {
                    yield return text.Substring(start, i - start);
                }

                yield return c == '\n' ? "\n" : " ";
                start = i + 1;
            }

            if (start < text.Length)
            {
                yield return text.Substring(start);
            }
        }

        /// <summary>Greedy line filler that merges consecutive tokens of the same run into one fragment.</summary>
        private sealed class LineBuilder
        {
            private readonly RichLayout layout;
            private readonly int maxWidth;
            private RichLine current = new();
            private float cursor;

            internal LineBuilder(RichLayout layout, int maxWidth)
            {
                this.layout = layout;
                this.maxWidth = maxWidth;
            }

            internal void Add(RichRun run, string token, float width)
            {
                bool space = run.Kind == RichRunKind.Text && token == " ";
                if (space && current.Fragments.Count == 0)
                {
                    return; // no leading spaces after a wrap
                }

                if (!space && maxWidth > 0 && current.Fragments.Count > 0 && cursor + width > maxWidth)
                {
                    NewLine();
                }

                RichFragment? tail = current.Fragments.Count > 0 ? current.Fragments[^1] : null;
                if (tail != null && tail.Run == run && run.Kind == RichRunKind.Text)
                {
                    tail.Text += token;
                    tail.Width += width;
                }
                else
                {
                    current.Fragments.Add(new RichFragment(run, token) { X = cursor, Width = width });
                }

                cursor += width;
            }

            internal void NewLine()
            {
                TrimTrailingSpace();
                Commit();
                current = new RichLine();
                cursor = 0;
            }

            internal void Finish()
            {
                TrimTrailingSpace();
                Commit();
                float width = 0;
                foreach (RichLine line in layout.Lines)
                {
                    width = Math.Max(width, line.Width);
                }

                layout.Size = new Vector2(width, Math.Max(1, layout.Lines.Count) * layout.LineHeight);
            }

            /// <summary>Re-measure merged text fragments so kerning across tokens is exact, then store the line.</summary>
            private void Commit()
            {
                float x = 0;
                foreach (RichFragment f in current.Fragments)
                {
                    if (f.Run.Kind == RichRunKind.Text)
                    {
                        f.Width = MeasureText(f.Run, f.Text, layout.Font, layout.Scale);
                    }

                    f.X = x;
                    x += f.Width;
                }

                current.Width = x;
                current.Y = layout.Lines.Count * layout.LineHeight;
                layout.Lines.Add(current);
            }

            private void TrimTrailingSpace()
            {
                if (current.Fragments.Count == 0)
                {
                    return;
                }

                RichFragment tail = current.Fragments[^1];
                if (tail.Run.Kind == RichRunKind.Text && tail.Text.EndsWith(' '))
                {
                    tail.Text = tail.Text.TrimEnd(' ');
                    if (tail.Text.Length == 0)
                    {
                        current.Fragments.RemoveAt(current.Fragments.Count - 1);
                    }
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Drawing
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Horizontal offset of a line inside a width for an alignment.</summary>
        internal static float LineOffset(UIAlign align, int width, float lineWidth)
        {
            return LayoutEngine.AlignOffset(align == UIAlign.Stretch ? UIAlign.Start : align, width, (int)lineWidth);
        }

        /// <summary>Draw a layout inside <paramref name="bounds"/>; <paramref name="hoveredLink"/> is highlighted.</summary>
        internal static void Draw(SpriteBatch b, RichLayout layout, Rectangle bounds, Color color, bool shadow, UIAlign align, string? hoveredLink)
        {
            SpriteFont font = GameTextMeasurer.GetFont(layout.Font);
            foreach (RichLine line in layout.Lines)
            {
                float lineX = bounds.X + LineOffset(align, bounds.Width, line.Width);
                float lineY = bounds.Y + line.Y;
                foreach (RichFragment f in line.Fragments)
                {
                    var pos = new Vector2((int)(lineX + f.X), (int)lineY);
                    if (f.Run.Kind == RichRunKind.Icon)
                    {
                        ItemSprite.Draw(b, f.Run.Text, new Rectangle((int)pos.X + (IconGap / 2), (int)pos.Y, (int)layout.LineHeight, (int)layout.LineHeight));
                        continue;
                    }

                    DrawFragment(b, font, f, pos, FragmentColor(f.Run, color, hoveredLink), shadow, layout);
                }
            }
        }

        private static Color FragmentColor(RichRun run, Color fallback, string? hoveredLink)
        {
            if (run.Link != null)
            {
                return hoveredLink != null && run.Link == hoveredLink ? LinkHoverColor : run.Color ?? LinkColor;
            }

            return run.Color ?? fallback;
        }

        private static void DrawFragment(SpriteBatch b, SpriteFont font, RichFragment f, Vector2 pos, Color color, bool shadow, RichLayout layout)
        {
            // the layout was measured through UIServices.Text, which applies the theme / accessibility text scale
            float scale = layout.Scale * Theme.FontScale;
            if (f.Run.Bold)
            {
                Utility.drawBoldText(b, f.Text, font, pos + new Vector2(1, 0), color, scale);
            }
            else if (shadow)
            {
                Utility.drawTextWithShadow(b, f.Text, font, pos, color, scale);
            }
            else
            {
                b.DrawString(font, f.Text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            if (f.Run.Link != null)
            {
                DrawHelper.Fill(b, new Rectangle((int)pos.X, (int)(pos.Y + layout.LineHeight - (UnderlineThickness * 2)), (int)f.Width, UnderlineThickness), color);
            }
        }
    }

    /// <summary>Draws a vanilla item's sprite (by qualified id) scaled to fit a square, via <see cref="ItemRegistry"/>.</summary>
    internal static class ItemSprite
    {
        /// <summary>Item data for an id (the error item when unknown).</summary>
        internal static ParsedItemData Resolve(string qualifiedItemId) => ItemRegistry.GetDataOrErrorItem(qualifiedItemId ?? string.Empty);

        /// <summary>Draw the item's sprite centered and scaled to fit <paramref name="dest"/>.</summary>
        internal static void Draw(SpriteBatch b, string qualifiedItemId, Rectangle dest)
        {
            Draw(b, Resolve(qualifiedItemId), dest, Color.White);
        }

        /// <summary>Draw <paramref name="data"/>'s sprite centered and scaled to fit <paramref name="dest"/>.</summary>
        internal static void Draw(SpriteBatch b, ParsedItemData data, Rectangle dest, Color tint)
        {
            if (dest.Width <= 0 || dest.Height <= 0)
            {
                return;
            }

            Texture2D texture = data.GetTexture();
            Rectangle source = data.GetSourceRect();
            float scale = Math.Min(dest.Width / (float)Math.Max(1, source.Width), dest.Height / (float)Math.Max(1, source.Height));
            int w = (int)(source.Width * scale);
            int h = (int)(source.Height * scale);
            b.Draw(texture, new Rectangle(dest.X + ((dest.Width - w) / 2), dest.Y + ((dest.Height - h) / 2), w, h), source, tint);
        }
    }
}
