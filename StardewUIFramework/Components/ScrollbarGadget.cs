using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    /// <summary>
    /// The vanilla vertical scrollbar used by <see cref="ScrollView"/> and <see cref="ListView"/>: an up arrow, a
    /// down arrow, a 9-slice track between them and a thumb whose position is a fraction in [0, 1]. Pure geometry,
    /// drawing and hit-testing; the owner maps the fraction to pixels / rows and owns the drag state.
    /// Matches the look of the Profit Calculator results list (arrows 44x48, track 24 wide, thumb 24x40, all at 4x).
    /// </summary>
    internal sealed class ScrollbarGadget
    {
        /// <summary>Which part of the scrollbar a point is over.</summary>
        internal enum Part
        {
            None,
            UpArrow,
            DownArrow,
            Thumb,
            Track
        }

        /// <summary>Width of the scrollbar column in UI pixels (the arrow sprite at 4x).</summary>
        internal const int Width = 44;

        /// <summary>Gap between the content and the scrollbar column.</summary>
        internal const int Gap = 4;

        /// <summary>Horizontal space an owner reserves for the scrollbar (<see cref="Gap"/> + <see cref="Width"/>).</summary>
        internal const int ReservedWidth = Width + Gap;

        private const int ArrowHeight = 48;
        private const int TrackWidth = 24;
        private const int TrackInset = 12;
        private const int TrackGap = 4;
        private const int ThumbHeight = 40;
        private const float Scale = 4f;

        /// <summary>Absolute rectangle of the whole gadget (arrows + track).</summary>
        internal Rectangle Bounds { get; private set; }

        internal Rectangle UpArrow { get; private set; }
        internal Rectangle DownArrow { get; private set; }
        internal Rectangle Track { get; private set; }
        internal Rectangle Thumb { get; private set; }

        /// <summary>Thumb position in [0, 1] (0 = top).</summary>
        internal float Fraction { get; private set; }

        /// <summary>Place the gadget in a column of <see cref="Width"/> pixels at (<paramref name="x"/>, <paramref name="y"/>) spanning <paramref name="height"/> pixels.</summary>
        internal void Layout(int x, int y, int height, float fraction)
        {
            height = Math.Max(0, height);
            Bounds = new Rectangle(x, y, Width, height);
            UpArrow = new Rectangle(x, y, Width, ArrowHeight);
            DownArrow = new Rectangle(x, y + height - ArrowHeight, Width, ArrowHeight);
            int trackY = UpArrow.Bottom + TrackGap;
            int trackHeight = Math.Max(0, DownArrow.Y - TrackGap - trackY);
            Track = new Rectangle(x + TrackInset, trackY, TrackWidth, trackHeight);
            SetFraction(fraction);
        }

        /// <summary>Move the thumb to <paramref name="fraction"/> of the track.</summary>
        internal void SetFraction(float fraction)
        {
            Fraction = float.IsNaN(fraction) ? 0f : Math.Clamp(fraction, 0f, 1f);
            int range = Math.Max(0, Track.Height - ThumbHeight);
            Thumb = new Rectangle(Track.X, Track.Y + (int)Math.Round(range * Fraction), TrackWidth, ThumbHeight);
        }

        /// <summary>Fraction in [0, 1] that puts the thumb's center at <paramref name="py"/> (used while dragging / clicking the track).</summary>
        internal float FractionFromY(int py)
        {
            int range = Track.Height - ThumbHeight;
            if (range <= 0)
            {
                return 0f;
            }

            return Math.Clamp((py - Track.Y - (ThumbHeight / 2f)) / range, 0f, 1f);
        }

        /// <summary>True if the point is anywhere in the scrollbar column.</summary>
        internal bool Contains(int px, int py) => Bounds.Contains(px, py);

        /// <summary>Which part is under the point.</summary>
        internal Part HitTest(int px, int py)
        {
            if (!Bounds.Contains(px, py))
            {
                return Part.None;
            }

            if (UpArrow.Contains(px, py))
            {
                return Part.UpArrow;
            }

            if (DownArrow.Contains(px, py))
            {
                return Part.DownArrow;
            }

            if (Thumb.Contains(px, py))
            {
                return Part.Thumb;
            }

            if (Track.Contains(px, py))
            {
                return Part.Track;
            }
            // the strip beside the track (between the arrows) behaves like the track so clicks there are not lost
            return py >= Track.Y && py < Track.Bottom ? Part.Track : Part.None;
        }

        /// <summary>Draw arrows, track and thumb with the vanilla sprites.</summary>
        internal void Draw(SpriteBatch b)
        {
            Texture2D texture = Game1.mouseCursors;
            b.Draw(texture, new Vector2(UpArrow.X, UpArrow.Y), Theme.ScrollUpArrow, Color.White, 0f, Vector2.Zero, Scale, SpriteEffects.None, 0f);
            b.Draw(texture, new Vector2(DownArrow.X, DownArrow.Y), Theme.ScrollDownArrow, Color.White, 0f, Vector2.Zero, Scale, SpriteEffects.None, 0f);
            if (Track.Height > 0)
            {
                IClickableMenu.drawTextureBox(b, texture, Theme.ScrollTrack, Track.X, Track.Y, Track.Width, Track.Height, Color.White, Scale, false);
                if (Track.Height >= ThumbHeight)
                {
                    b.Draw(texture, new Vector2(Thumb.X, Thumb.Y), Theme.ScrollThumb, Color.White, 0f, Vector2.Zero, Scale, SpriteEffects.None, 0f);
                }
            }
        }
    }
}
