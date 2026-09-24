using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Rendering
{
    /// <summary>
    /// Draws a <see cref="RichTooltip"/> next to the cursor: a vanilla 9-slice box (<c>Game1.menuTexture</c> 0,256,60,60)
    /// sized to its content, positioned and clamped like <c>IClickableMenu.drawHoverText</c>. Rows are laid out with
    /// <see cref="UIServices.Text"/> so sizing runs without the game; only <see cref="Draw"/> touches <see cref="Game1"/>.
    /// </summary>
    internal static class TooltipRenderer
    {
        private const int Padding = 16;
        private const int RowGap = 4;
        private const int IconGap = 8;
        private const int DividerHeight = 2;
        private const int DividerMargin = 4;
        private const int CursorOffset = 32;
        private const int EdgeNudge = 16;

        /// <summary>Space kept between the tooltip and the screen edge when a wrap width is derived from the viewport.</summary>
        private const int ViewportSlack = 64;

        /// <summary>A measured row and how to draw it at a top-left position, given the box's content width.</summary>
        private sealed class Row
        {
            internal Row(float width, float height, Action<SpriteBatch, Vector2, int> draw)
            {
                Width = width;
                Height = height;
                Draw = draw;
            }

            internal float Width { get; }
            internal float Height { get; }
            internal Action<SpriteBatch, Vector2, int> Draw { get; }
        }

        /// <summary>Lay out and draw <paramref name="tooltip"/> for <paramref name="element"/> at the menu's cursor.</summary>
        internal static void Draw(SpriteBatch b, UIMenu menu, UIElement element, RichTooltip tooltip)
        {
            Point vp = UIServices.ViewportSize();
            int wrap = Math.Max(1, vp.X - (2 * Padding) - ViewportSlack);
            if (tooltip.MaxWidthPx > 0)
            {
                wrap = Math.Min(wrap, tooltip.MaxWidthPx);
            }

            List<Row> rows = BuildRows(element, tooltip, wrap);
            if (rows.Count == 0)
            {
                return;
            }

            Point size = MeasureRows(rows);
            Point at = Place(menu.CursorX, menu.CursorY, size, vp);
            var box = new Rectangle(at.X, at.Y, size.X, size.Y);
            DrawHelper.Box(b, Game1.menuTexture, Theme.PanelBoxSource, box, Color.White, 1f, shadow: true);

            float y = box.Y + Padding;
            foreach (Row row in rows)
            {
                row.Draw(b, new Vector2(box.X + Padding, (int)y), size.X - (2 * Padding));
                y += row.Height + RowGap;
            }
        }

        /// <summary>Box size for the rows: the widest row plus padding, the rows stacked with gaps.</summary>
        private static Point MeasureRows(List<Row> rows)
        {
            float width = 0;
            float height = 0;
            foreach (Row row in rows)
            {
                width = Math.Max(width, row.Width);
                height += row.Height;
            }

            height += RowGap * (rows.Count - 1);
            return new Point((int)Math.Ceiling(width) + (2 * Padding), (int)Math.Ceiling(height) + (2 * Padding));
        }

        /// <summary>Top-left corner: below-right of the cursor, pushed back inside the viewport the way vanilla does.</summary>
        internal static Point Place(int cursorX, int cursorY, Point size, Point viewport)
        {
            int x = cursorX + CursorOffset;
            int y = cursorY + CursorOffset;
            if (x + size.X > viewport.X)
            {
                x = viewport.X - size.X;
                y += EdgeNudge;
            }

            if (y + size.Y > viewport.Y)
            {
                x += EdgeNudge;
                if (x + size.X > viewport.X)
                {
                    x = viewport.X - size.X;
                }

                y = viewport.Y - size.Y;
            }

            return new Point(Math.Max(0, x), Math.Max(0, y));
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Rows
        // ---------------------------------------------------------------------------------------------------------

        private static List<Row> BuildRows(UIElement element, RichTooltip tooltip, int wrap)
        {
            var rows = new List<Row>();
            IReadOnlyList<TooltipBlock> blocks = tooltip.Blocks;
            for (int i = 0; i < blocks.Count; i++)
            {
                // rows are built once per frame and both sized and drawn from this list, so a hidden block drops out of both
                if (!IsShown(element, blocks[i], i))
                {
                    continue;
                }

                Row? row = BuildRow(element, blocks[i], i, wrap);
                if (row != null)
                {
                    rows.Add(row);
                }
            }

            return rows;
        }

        private static Row? BuildRow(UIElement element, TooltipBlock block, int index, int wrap)
        {
            return block.Kind switch
            {
                TooltipBlockKind.Title => TextRow(Evaluate(element, block, "RichTooltip.Title#" + index), UIFont.Dialogue, ColorOf(element, block, index), wrap),
                TooltipBlockKind.Line => TextRow(Evaluate(element, block, "RichTooltip.Line#" + index), UIFont.Small, ColorOf(element, block, index), wrap),
                TooltipBlockKind.Icon => IconRow(block),
                TooltipBlockKind.Item => ItemRow(block),
                TooltipBlockKind.ItemInstance => ItemInstanceRow(element, block, index),
                TooltipBlockKind.Divider => DividerRow(),
                TooltipBlockKind.Money => MoneyRow(element, block, index),
                _ => null
            };
        }

        private static string Evaluate(UIElement element, TooltipBlock block, string eventName)
        {
            return element.Consumer.Invoke(element.Id, eventName, block.Text, string.Empty) ?? string.Empty;
        }

        /// <summary>The block's <see cref="TooltipBlock.When"/> condition (true when unset; a throwing condition hides the block).</summary>
        private static bool IsShown(UIElement element, TooltipBlock block, int index)
        {
            return block.When == null || element.Consumer.Invoke(element.Id, "RichTooltip.When#" + index, block.When, false);
        }

        /// <summary>Text color: <see cref="TooltipBlock.ColorFunc"/> when set and non-null, else the static <see cref="TooltipBlock.Color"/>.</summary>
        private static Color? ColorOf(UIElement element, TooltipBlock block, int index)
        {
            if (block.ColorFunc == null)
            {
                return block.Color;
            }

            return element.Consumer.Invoke(element.Id, "RichTooltip.Color#" + index, block.ColorFunc, null) ?? block.Color;
        }

        /// <summary>A (rich, wrapped) text row; skipped when empty.</summary>
        private static Row? TextRow(string markup, UIFont font, Color? color, int wrap)
        {
            if (string.IsNullOrEmpty(markup))
            {
                return null;
            }

            RichLayout layout = RichText.Layout(RichText.Parse(markup), font, 1f, wrap);
            return new Row(layout.Size.X, layout.Size.Y, (b, at, _) =>
            {
                var rect = new Rectangle((int)at.X, (int)at.Y, (int)Math.Ceiling(layout.Size.X), (int)Math.Ceiling(layout.Size.Y));
                RichText.Draw(b, layout, rect, color ?? Theme.TextColor, true, UIAlign.Start, null);
            });
        }

        /// <summary>A block icon on its own row.</summary>
        private static Row? IconRow(TooltipBlock block)
        {
            Texture2D? texture = block.Texture;
            if (texture == null)
            {
                return null;
            }

            Rectangle source = block.Source ?? texture.Bounds;
            float w = source.Width * block.Scale;
            float h = source.Height * block.Scale;
            return new Row(w, h, (b, at, _) => b.Draw(texture, new Rectangle((int)at.X, (int)at.Y, (int)w, (int)h), source, Color.White));
        }

        /// <summary>Item sprite (scaled to the line height) followed by its display name.</summary>
        private static Row ItemRow(TooltipBlock block)
        {
            ParsedItemData data = ItemSprite.Resolve(block.ItemId);
            float lineHeight = UIServices.Text.LineHeight(UIFont.Small);
            string name = Pseudo.Transform(data.DisplayName);
            Vector2 nameSize = UIServices.Text.Measure(UIFont.Small, name, 1f);
            int icon = (int)lineHeight;
            return new Row(icon + IconGap + nameSize.X, Math.Max(icon, nameSize.Y), (b, at, _) =>
            {
                ItemSprite.Draw(b, data, new Rectangle((int)at.X, (int)at.Y, icon, icon), Color.White);
                DrawHelper.Text(b, name, UIFont.Small, new Vector2(at.X + icon + IconGap, at.Y), Theme.TextColor, true, 1f);
            });
        }

        /// <summary>An item instance (drawn with <c>drawInMenu</c>, so tints are kept) scaled to the line height, followed by its display name; skipped when the getter returns null.</summary>
        private static Row? ItemInstanceRow(UIElement element, TooltipBlock block, int index)
        {
            Item? item = element.Consumer.Invoke<Item?>(element.Id, "RichTooltip.ItemInstance#" + index, block.ItemGetter, null);
            if (item == null)
            {
                return null;
            }

            float lineHeight = UIServices.Text.LineHeight(UIFont.Small);
            string name = Pseudo.Transform(item.DisplayName);
            Vector2 nameSize = UIServices.Text.Measure(UIFont.Small, name, 1f);
            int icon = (int)lineHeight;
            return new Row(icon + IconGap + nameSize.X, Math.Max(icon, nameSize.Y), (b, at, _) =>
            {
                element.Consumer.Invoke(element.Id, "RichTooltip.ItemInstanceDraw#" + index,
                    () => Components.ItemImage.DrawItem(b, item, at, icon, 1f, UIItemStack.Hide, Color.White, false));
                DrawHelper.Text(b, name, UIFont.Small, new Vector2(at.X + icon + IconGap, at.Y), Theme.TextColor, true, 1f);
            });
        }

        private static Row DividerRow()
        {
            return new Row(0, DividerHeight + (2 * DividerMargin), (b, at, width) =>
                DrawHelper.Fill(b, new Rectangle((int)at.X, (int)at.Y + DividerMargin, width, DividerHeight), Game1.textShadowColor * 0.6f));
        }

        /// <summary>Coin sprite (<c>Game1.debrisSpriteSheet</c> tile 8) followed by the amount.</summary>
        private static Row MoneyRow(UIElement element, TooltipBlock block, int index)
        {
            int amount = element.Consumer.Invoke(element.Id, "RichTooltip.Money#" + index, block.Amount, 0);
            string text = amount.ToString(CultureInfo.CurrentCulture);
            float lineHeight = UIServices.Text.LineHeight(UIFont.Small);
            Vector2 textSize = UIServices.Text.Measure(UIFont.Small, text, 1f);
            int icon = (int)lineHeight;
            return new Row(icon + IconGap + textSize.X, Math.Max(icon, textSize.Y), (b, at, _) =>
            {
                Rectangle coin = Game1.getSourceRectForStandardTileSheet(Game1.debrisSpriteSheet, 8, 16, 16);
                b.Draw(Game1.debrisSpriteSheet, new Rectangle((int)at.X, (int)at.Y, icon, icon), coin, Color.White);
                DrawHelper.Text(b, text, UIFont.Small, new Vector2(at.X + icon + IconGap, at.Y), Theme.TextColor, true, 1f);
            });
        }
    }
}
