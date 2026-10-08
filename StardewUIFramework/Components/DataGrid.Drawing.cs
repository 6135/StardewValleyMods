using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    internal sealed partial class DataGrid
    {
        // ---------------------------------------------------------------------------------------------------------
        //  Update / draw
        // ---------------------------------------------------------------------------------------------------------

        internal override void Update(double elapsedMs)
        {
            // rebuild before (never while) the children are iterated
            if (needsRefresh || SourceCount != lastCount)
            {
                Refresh();
            }

            base.Update(elapsedMs);
        }

        protected override void DrawCore(SpriteBatch b)
        {
            // columns wider than the content area are cut at its edge instead of running over the scrollbar / frame
            Rectangle content = ContentRect;
            if (content.Width > 0 && content.Height > 0)
            {
                DrawHelper.WithScissor(b, content, () => DrawContent(b));
            }

            if (ScrollbarVisible)
            {
                scrollbar.Draw(b);
            }

            if (IsFocused)
            {
                DrawHelper.Outline(b, RowsRect, DividerActiveColor * 0.6f, 2);
            }
        }

        /// <summary>Header, row backgrounds, cells and column dividers (drawn clipped to <see cref="ContentRect"/>).</summary>
        private void DrawContent(SpriteBatch b)
        {
            DrawHeader(b);
            DrawRowBackgrounds(b);
            DrawChildren(b);
            DrawDividers(b);
        }

        /// <summary>Header box, hover tint on sortable headers, bold titles and the sort arrow.</summary>
        private void DrawHeader(SpriteBatch b)
        {
            Rectangle header = HeaderRect;
            if (header.Width <= 0 || header.Height <= 0)
            {
                return;
            }

            DrawHelper.Box(b, Game1.mouseCursors, Theme.DropdownBoxSource, header, Color.White, Theme.PixelScale);
            int hovered = HoveredHeaderColumn;
            ResolvedStyle style = Style;
            for (int j = 0; j < columns.Count; j++)
            {
                DataGridColumn column = columns[j];
                var cell = new Rectangle(column.ResolvedX, header.Y, column.ResolvedWidth, header.Height);
                if (column.Sortable && j == hovered && ResizeColumnAt(CursorX) < 0)
                {
                    DrawHelper.Fill(b, new Rectangle(cell.X + 2, cell.Y + 4, Math.Max(0, cell.Width - 4), Math.Max(0, cell.Height - 8)), HeaderHoverColor);
                }

                DrawHeaderText(b, column, cell, style);
            }
        }

        /// <summary>Bold header title aligned like the column, followed by the sort arrow when sorted by it.</summary>
        private void DrawHeaderText(SpriteBatch b, DataGridColumn column, Rectangle cell, ResolvedStyle style)
        {
            string text = column.HeaderText;
            bool sorted = column.Id == sortColumn;
            int arrowWidth = sorted ? ArrowGap + (Theme.ScrollUpArrow.Width * ArrowScale) : 0;
            Vector2 size = UIServices.Text.Measure(UIFont.Small, text, 1f);
            int available = Math.Max(0, cell.Width - (2 * CellPadX) - arrowWidth);
            UIAlign align = column.Align == UIAlign.Stretch ? UIAlign.Start : column.Align;
            int textY = cell.Y + ((cell.Height - (int)size.Y) / 2);
            float drawnWidth = Math.Min(size.X, available);
            if (text.Length > 0)
            {
                // titles wider than their column shrink, then truncate, instead of running into the next header
                var textRect = new Rectangle(cell.X + CellPadX, textY, available, (int)size.Y);
                drawnWidth = DrawHelper.FitBoldText(b, text, UIFont.Small, textRect, style.TextColor, 1f, align);
            }

            if (sorted)
            {
                int textX = cell.X + CellPadX + LayoutEngine.AlignOffset(align, available, (int)drawnWidth);
                Rectangle arrow = sortDescending ? Theme.ScrollDownArrow : Theme.ScrollUpArrow;
                int arrowY = cell.Y + ((cell.Height - (arrow.Height * ArrowScale)) / 2);
                b.Draw(Game1.mouseCursors, new Vector2(textX + drawnWidth + ArrowGap, arrowY), arrow, Color.White, 0f, Vector2.Zero, ArrowScale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>Zebra tint, then the selection or hover highlight, behind the visible rows.</summary>
        private void DrawRowBackgrounds(SpriteBatch b)
        {
            UIElement? hovered = OwnerMenu?.Hovered;
            for (int i = 0; i < rows.Count; i++)
            {
                DataGridRow row = rows[i];
                if (!row.Visible)
                {
                    continue;
                }

                if ((firstVisible + i) % 2 != 0)
                {
                    DrawHelper.Fill(b, row.Bounds, ZebraColor);
                }

                if (selectable && selected.Contains(row.Item))
                {
                    DrawHelper.Fill(b, row.Bounds, SelectionColor);
                }
                else if (selectable && hovered != null && hovered.IsSelfOrDescendantOf(row))
                {
                    DrawHelper.Fill(b, row.Bounds, HoverRowColor);
                }
                else
                {
                    // plain row
                }
            }
        }

        /// <summary>Vertical lines between columns (the divider under the cursor or being dragged is highlighted).</summary>
        private void DrawDividers(SpriteBatch b)
        {
            int top = Bounds.Y + 4;
            int height = headerHeight + (visibleRows * EffectiveRowHeight) - 8;
            int active = resizingColumn >= 0 ? resizingColumn : (IsHovered && HeaderRect.Contains(CursorX, CursorY) ? ResizeColumnAt(CursorX) : -1);
            for (int j = 0; j < columns.Count - 1; j++)
            {
                Color color = j == active ? DividerActiveColor : DividerColor;
                DrawHelper.Fill(b, new Rectangle(columns[j].ResolvedRight - 1, top, 2, height), color);
            }

            if (active == columns.Count - 1 && active >= 0)
            {
                DrawHelper.Fill(b, new Rectangle(columns[active].ResolvedRight - 1, top, 2, height), DividerActiveColor);
            }
        }
    }
}
