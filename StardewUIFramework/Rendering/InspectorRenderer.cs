using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UIFramework.Api;
using UIFramework.Components;
using UIFramework.Core;
using UIFramework.Hosting;

namespace UIFramework.Rendering
{
    /// <summary>
    /// Draws the <see cref="Inspector"/> overlay for a menu: every element's bounds (containers blue, leaves green,
    /// the inspected element orange with a translucent fill), margins as tinted bands, grid tracks as dotted lines,
    /// and — in the overlay pass — the info panel, placed on the side of the screen away from the cursor.
    /// </summary>
    internal static class InspectorRenderer
    {
        private const int PanelPadding = 16;
        private const int PanelMargin = 16;
        private const int LineGap = 2;
        private const int Dash = 4;

        private static readonly Color ContainerColor = Color.DodgerBlue * 0.8f;
        private static readonly Color LeafColor = Color.LimeGreen * 0.8f;
        private static readonly Color SubjectColor = Color.Orange;
        private static readonly Color SubjectFill = Color.Orange * 0.2f;
        private static readonly Color MarginColor = Color.Gold * 0.25f;
        private static readonly Color TrackColor = Color.MediumPurple * 0.9f;

        /// <summary>Called by <see cref="UIMenu.Draw"/> after the tree and before the overlay pass. No-op unless the inspector is on.</summary>
        internal static void Draw(UIMenu menu, SpriteBatch b)
        {
            if (!Inspector.Enabled)
            {
                return;
            }

            UIElement? subject = Inspector.Subject(menu);
            foreach (UIElement element in menu.Root.SelfAndDescendants())
            {
                if (element.Visible)
                {
                    DrawElement(b, element, element == subject);
                }
            }

            if (subject != null)
            {
                menu.Overlay.RegisterDraw(sb => DrawInfoPanel(sb, menu, subject));
            }
        }

        private static void DrawElement(SpriteBatch b, UIElement element, bool subject)
        {
            DrawMargins(b, element);
            if (element is Grid grid)
            {
                DrawTracks(b, grid);
            }

            Rectangle bounds = element.Bounds;
            if (subject)
            {
                DrawHelper.Fill(b, bounds, SubjectFill);
                DrawHelper.Outline(b, bounds, SubjectColor, 2);
            }
            else
            {
                DrawHelper.Outline(b, bounds, element is UIContainer ? ContainerColor : LeafColor);
            }
        }

        /// <summary>Tint the bands between the element's slot (bounds + margins) and its bounds.</summary>
        private static void DrawMargins(SpriteBatch b, UIElement e)
        {
            Rectangle r = e.Bounds;
            int l = Math.Max(0, e.MarginLeft), t = Math.Max(0, e.MarginTop), rt = Math.Max(0, e.MarginRight), bt = Math.Max(0, e.MarginBottom);
            if (l + t + rt + bt == 0)
            {
                return;
            }

            DrawHelper.Fill(b, new Rectangle(r.X - l, r.Y - t, r.Width + l + rt, t), MarginColor);
            DrawHelper.Fill(b, new Rectangle(r.X - l, r.Bottom, r.Width + l + rt, bt), MarginColor);
            DrawHelper.Fill(b, new Rectangle(r.X - l, r.Y, l, r.Height), MarginColor);
            DrawHelper.Fill(b, new Rectangle(r.Right, r.Y, rt, r.Height), MarginColor);
        }

        /// <summary>Dotted lines on the interior column / row boundaries of a grid.</summary>
        private static void DrawTracks(SpriteBatch b, Grid grid)
        {
            Rectangle r = grid.Bounds;
            float[] columns = grid.ColumnSizes;
            for (int i = 1; i < columns.Length; i++)
            {
                int x = r.X + (int)Math.Round(LayoutEngine.TrackOffset(columns, i, grid.ColumnSpacing) - (grid.ColumnSpacing / 2f));
                DottedLine(b, x, r.Y, r.Height, vertical: true);
            }

            float[] rows = grid.RowSizes;
            for (int i = 1; i < rows.Length; i++)
            {
                int y = r.Y + (int)Math.Round(LayoutEngine.TrackOffset(rows, i, grid.RowSpacing) - (grid.RowSpacing / 2f));
                DottedLine(b, r.X, y, r.Width, vertical: false);
            }
        }

        private static void DottedLine(SpriteBatch b, int x, int y, int length, bool vertical)
        {
            for (int offset = 0; offset < length; offset += 2 * Dash)
            {
                int run = Math.Min(Dash, length - offset);
                Rectangle dash = vertical ? new Rectangle(x, y + offset, 1, run) : new Rectangle(x + offset, y, run, 1);
                DrawHelper.Fill(b, dash, TrackColor);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Info panel
        // ---------------------------------------------------------------------------------------------------------

        private static void DrawInfoPanel(SpriteBatch b, UIMenu menu, UIElement subject)
        {
            string[] lines = Inspector.Describe(subject);
            float lineHeight = UIServices.Text.LineHeight(UIFont.Small) + LineGap;
            int width = 0;
            foreach (string line in lines)
            {
                width = Math.Max(width, (int)Math.Ceiling(UIServices.Text.Measure(UIFont.Small, line, 1f).X));
            }

            Rectangle panel = PanelRect(menu, width + (2 * PanelPadding), (int)(lines.Length * lineHeight) + (2 * PanelPadding));
            DrawHelper.PanelBox(b, panel, Color.White);

            float y = panel.Y + PanelPadding;
            for (int i = 0; i < lines.Length; i++)
            {
                Color color = i == 0 ? SubjectColor : (i == lines.Length - 1 ? Theme.TextColor * 0.6f : Theme.TextColor);
                DrawHelper.Text(b, lines[i], UIFont.Small, new Vector2(panel.X + PanelPadding, (int)y), color, false, 1f);
                y += lineHeight;
            }
        }

        /// <summary>Place the panel on the horizontal half of the screen opposite to the cursor, clamped to the viewport.</summary>
        private static Rectangle PanelRect(UIMenu menu, int width, int height)
        {
            Point vp = UIServices.ViewportSize();
            width = Math.Min(width, Math.Max(0, vp.X - (2 * PanelMargin)));
            height = Math.Min(height, Math.Max(0, vp.Y - (2 * PanelMargin)));
            bool cursorLeft = menu.CursorX < vp.X / 2;
            int x = cursorLeft ? vp.X - PanelMargin - width : PanelMargin;
            int y = Math.Clamp(menu.CursorY - (height / 2), PanelMargin, Math.Max(PanelMargin, vp.Y - PanelMargin - height));
            return new Rectangle(x, y, width, height);
        }
    }
}
