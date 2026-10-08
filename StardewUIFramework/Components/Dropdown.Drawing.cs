using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    internal sealed partial class Dropdown
    {
        // ---------------------------------------------------------------------------------------------------------
        //  Layout / draw
        // ---------------------------------------------------------------------------------------------------------

        // DefaultWidth is a preference: a narrower slot gets a narrower dropdown, and Stretch (handled by Arrange) fills it
        protected override Vector2 MeasureCore(Vector2 available) => new(Math.Max(0, Math.Min(available.X, DefaultWidth)), RowHeight);

        // the arrow button plus a text box wide enough for every option whole, capped at DefaultWidth (the natural width
        // MeasureCore reports when there is room, so the minimum never exceeds it: options too long for that are fitted
        // even then). With Shrink, the text box only needs the narrowest fitted form (FitText) of the widest-reaching
        // option. Either way never below MinTextWidth, so the 3-slice box keeps its corners and some room for "..."
        protected override float MinWidthCore()
        {
            UIFont font = Style.Font;
            float text = MinTextWidth;
            for (int i = 0; i < choices.Length; i++)
            {
                string label = Pseudo.Transform(GetLabel(i));
                text = Math.Max(text, shrink ? DrawHelper.FitTextMinWidth(label, font, 1f) : (float)Math.Ceiling(UIServices.Text.Measure(font, label, 1f).X));
            }

            float width = ButtonWidth + (2 * TextPadX) + text;
            return shrink ? width : Math.Min(width, DefaultWidth);
        }

        /// <summary>Row height including the extra room scaled text needs.</summary>
        private int RowHeight => RowHeightFor(Style.Font);

        private static int RowHeightFor(UIFont font) => DropdownRowHeight + Theme.ExtraTextHeight(font);

        /// <summary>Width of the text box part (the arrow button takes the rest).</summary>
        private int BoxWidth => Math.Max(0, Bounds.Width - ButtonWidth);

        /// <summary>
        /// Width the open list needs to show every label unshrunk (text + padding + the scroll indicator's strip when
        /// it overflows), capped at the text box width of a <see cref="DefaultWidth"/> dropdown so an unsqueezed dropdown
        /// opens exactly as before. Measured once per <see cref="Open()"/>, so a squeezed dropdown still opens a readable list.
        /// </summary>
        private int NaturalListWidth()
        {
            UIFont font = Style.Font;
            float widest = 0;
            for (int i = 0; i < choices.Length; i++)
            {
                widest = Math.Max(widest, UIServices.Text.Measure(font, Pseudo.Transform(GetLabel(i)), 1f).X);
            }

            int reserved = HasOverflow ? IndicatorWidth + IndicatorGap : 0;
            int natural = (int)Math.Ceiling(widest) + (2 * TextPadX) + reserved;
            return Math.Min(natural, DefaultWidth - ButtonWidth);
        }

        /// <summary>
        /// Absolute bounds of the open list: as wide as the text box, or wider when a squeezed box is narrower than the
        /// labels need (the popup is an overlay, so it can outgrow the control), clamped so it never leaves the screen.
        /// </summary>
        private Rectangle ListBounds => ListBoundsFor(RowHeight);

        private Rectangle ListBoundsFor(int rowHeight)
        {
            Point viewport = UIServices.ViewportSize();
            int rows = Math.Min(maxVisible, choices.Length);
            int height = rows * rowHeight;
            int width = Math.Min(Math.Max(BoxWidth, openListWidth), viewport.X);
            int x = Math.Min(Bounds.X, viewport.X - width);
            int y = Math.Min(Bounds.Y, viewport.Y - height);
            return new Rectangle(Math.Max(0, x), Math.Max(0, y), width, height);
        }

        protected internal override Rectangle PopupBounds => IsOpen ? ListBounds : Rectangle.Empty;

        protected override void DrawCore(SpriteBatch b)
        {
            SyncChoices();
            ResolvedStyle style = Style;
            bool highlighted = Enabled && (IsHovered || IsFocused || IsOpen);
            Color tint = Theme.StateTint(Enabled, highlighted, style.HoverColor);
            Color textColor = Enabled ? style.TextColor : style.DisabledTextColor;

            DrawHelper.ThemedBox(b, Game1.mouseCursors, Theme.DropdownBoxSource, new Rectangle(Bounds.X, Bounds.Y, BoxWidth, Bounds.Height), tint, SpriteScale);
            var textRect = new Rectangle(Bounds.X + TextPadX, Bounds.Y + TextPadY, Math.Max(0, BoxWidth - (2 * TextPadX)), Bounds.Height);
            DrawHelper.FitText(b, Pseudo.Transform(GetLabel(SelectedIndex)), style.Font, textRect, textColor, style.TextShadow, 1f, UIAlign.Start);
            b.Draw(Game1.mouseCursors, new Vector2(Bounds.X + Bounds.Width - ButtonWidth, Bounds.Y), Theme.DropdownButtonSource, tint, 0f, Vector2.Zero, SpriteScale, SpriteEffects.None, 0f);
        }

        protected internal override void DrawPopup(SpriteBatch b)
        {
            if (!IsOpen || choices.Length == 0)
            {
                return;
            }

            ResolvedStyle style = Style;
            int rowHeight = RowHeightFor(style.Font);
            Rectangle list = ListBoundsFor(rowHeight);
            DrawHelper.ThemedBox(b, Game1.mouseCursors, Theme.DropdownBoxSource, list, Color.White, SpriteScale);

            var interior = new Rectangle(list.X + HighlightInset, list.Y + HighlightInset, list.Width - (2 * HighlightInset), list.Height - (2 * HighlightInset));
            int start = ActivePosition;
            int end = Math.Min(choices.Length, start + maxVisible);
            int highlight = highlightIndex >= 0 ? highlightIndex : SelectedIndex;
            // rows give up the indicator's strip when the list overflows
            int reserved = HasOverflow ? IndicatorWidth + IndicatorGap : 0;
            for (int i = start; i < end; i++)
            {
                int rowY = list.Y + ((i - start) * rowHeight);
                if (i == highlight)
                {
                    var row = new Rectangle(list.X + HighlightInset, rowY, list.Width - (2 * HighlightInset) - reserved, rowHeight);
                    DrawHelper.Fill(b, Rectangle.Intersect(row, interior), style.HoverColor);
                }
                var rowText = new Rectangle(list.X + TextPadX, rowY + TextPadY, Math.Max(0, list.Width - (2 * TextPadX) - reserved), rowHeight);
                DrawHelper.FitText(b, Pseudo.Transform(GetLabel(i)), style.Font, rowText, style.TextColor, style.TextShadow, 1f, UIAlign.Start);
            }

            if (HasOverflow)
            {
                DrawIndicator(b, list);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Scroll indicator
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Whether some choices do not fit in the open list (the scroll indicator is shown).</summary>
        private bool HasOverflow => choices.Length > maxVisible;

        /// <summary>The indicator's strip along the right edge of <paramref name="list"/>, or empty when nothing overflows.</summary>
        private Rectangle IndicatorBounds(Rectangle list)
        {
            if (!HasOverflow)
            {
                return Rectangle.Empty;
            }

            return new Rectangle(list.Right - HighlightInset - IndicatorWidth, list.Y + HighlightInset, IndicatorWidth, Math.Max(0, list.Height - (2 * HighlightInset)));
        }

        /// <summary>Whether the strip is tall enough for both arrows plus some track; otherwise only the track is drawn.</summary>
        private static bool HasArrows(Rectangle strip) => strip.Height >= (2 * IndicatorArrowHeight) + IndicatorMinThumb;

        private static Rectangle UpArrowBounds(Rectangle strip) => new(strip.X, strip.Y, IndicatorWidth, IndicatorArrowHeight);

        private static Rectangle DownArrowBounds(Rectangle strip) => new(strip.X, strip.Bottom - IndicatorArrowHeight, IndicatorWidth, IndicatorArrowHeight);

        /// <summary>The track between the arrows (the whole strip when there is no room for them).</summary>
        private static Rectangle TrackBounds(Rectangle strip)
        {
            int x = strip.X + ((IndicatorWidth - IndicatorTrackWidth) / 2);
            if (!HasArrows(strip))
            {
                return new Rectangle(x, strip.Y, IndicatorTrackWidth, strip.Height);
            }

            int top = strip.Y + IndicatorArrowHeight + IndicatorGap;
            int bottom = strip.Bottom - IndicatorArrowHeight - IndicatorGap;
            return new Rectangle(x, top, IndicatorTrackWidth, Math.Max(0, bottom - top));
        }

        /// <summary>The thumb: as tall as the visible share of the list, placed by the scroll position.</summary>
        private Rectangle ThumbBounds(Rectangle track)
        {
            int height = Math.Clamp((int)Math.Round(track.Height * (maxVisible / (float)choices.Length)), Math.Min(IndicatorMinThumb, track.Height), track.Height);
            int range = track.Height - height;
            int max = MaxPosition;
            int y = track.Y + (max > 0 ? (int)Math.Round(range * (ActivePosition / (float)max)) : 0);
            return new Rectangle(track.X, y, track.Width, height);
        }

        private void DrawIndicator(SpriteBatch b, Rectangle list)
        {
            Rectangle strip = IndicatorBounds(list);
            Texture2D texture = Game1.mouseCursors;
            Color tint = Theme.ScrollbarTint;
            Rectangle track = TrackBounds(strip);

            if (HasArrows(strip))
            {
                Rectangle up = UpArrowBounds(strip);
                Rectangle down = DownArrowBounds(strip);
                Color upTint = ActivePosition > 0 ? tint : tint * DisabledArrowAlpha;
                Color downTint = ActivePosition < MaxPosition ? tint : tint * DisabledArrowAlpha;
                b.Draw(texture, new Vector2(up.X, up.Y), Theme.ScrollUpArrow, upTint, 0f, Vector2.Zero, IndicatorScale, SpriteEffects.None, 0f);
                b.Draw(texture, new Vector2(down.X, down.Y), Theme.ScrollDownArrow, downTint, 0f, Vector2.Zero, IndicatorScale, SpriteEffects.None, 0f);
            }

            if (track.Height <= 0)
            {
                return;
            }

            IClickableMenu.drawTextureBox(b, texture, Theme.ScrollTrack, track.X, track.Y, track.Width, track.Height, tint, IndicatorScale, false);
            Rectangle thumb = ThumbBounds(track);
            IClickableMenu.drawTextureBox(b, texture, Theme.ScrollThumb, thumb.X, thumb.Y, thumb.Width, thumb.Height, tint, IndicatorScale, false);
        }

        /// <summary>
        /// Clicks on the indicator: an arrow scrolls one row, the track moves the thumb's center to the click. Returns
        /// false when the point is not on the indicator.
        /// </summary>
        private bool HandleIndicatorClick(int px, int py)
        {
            Rectangle strip = IndicatorBounds(ListBounds);
            if (!strip.Contains(px, py))
            {
                return false;
            }

            int before = ActivePosition;
            Rectangle track = TrackBounds(strip);
            if (HasArrows(strip) && UpArrowBounds(strip).Contains(px, py))
            {
                ActivePosition--;
            }
            else if (HasArrows(strip) && DownArrowBounds(strip).Contains(px, py))
            {
                ActivePosition++;
            }
            else if (track.Height > 0)
            {
                Rectangle thumb = ThumbBounds(track);
                int range = track.Height - thumb.Height;
                float fraction = range > 0 ? Math.Clamp((py - track.Y - (thumb.Height / 2f)) / range, 0f, 1f) : 0f;
                ActivePosition = (int)Math.Round(fraction * MaxPosition);
            }
            else
            {
                // degenerate strip without a track: nothing to scroll
            }

            if (ActivePosition != before)
            {
                UIServices.PlaySound(Theme.ScrollSound);
            }

            return true;
        }
    }
}
