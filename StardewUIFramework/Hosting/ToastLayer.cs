using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Hosting
{
    /// <summary>
    /// Stacked notifications in the bottom-left corner, drawn by the framework (9-slice box, small font, optional
    /// icon). Each toast fades in and out over <see cref="FadeMs"/>; older toasts slide up when a newer one arrives.
    /// One instance per screen (split-screen).
    /// </summary>
    internal sealed class ToastLayer
    {
        /// <summary>Most toasts shown at once; the oldest is dropped when a sixth arrives.</summary>
        internal const int MaxVisible = 5;

        internal const int DefaultDurationMs = 3500;
        private const int FadeMs = 200;
        private const int MarginX = 16;
        private const int MarginBottom = 32;
        private const int Gap = 8;
        private const int Padding = 16;
        private const int IconSize = 32;
        private const int MaxTextWidth = 400;
        private const float SlidePerMs = 0.6f; // pixels per millisecond while sliding up

        /// <summary>One notification and its animation state.</summary>
        private sealed class Toast
        {
            internal string Text = string.Empty;
            internal Texture2D? Icon;
            internal Rectangle? Source;
            internal double DurationMs;
            internal double ElapsedMs;
            internal int Width, Height;
            /// <summary>Distance from the bottom stack origin to this toast's bottom edge (animated towards the target).</summary>
            internal float Offset;
            internal float TargetOffset;
        }

        private readonly List<Toast> toasts = new(); // index 0 = newest (bottom)

        internal bool IsEmpty => toasts.Count == 0;

        /// <summary>Queue a toast; older ones slide up.</summary>
        internal void Add(string text, Texture2D? icon, Rectangle? source, int durationMs)
        {
            var toast = new Toast
            {
                Text = UIServices.Text.Wrap(UIFont.Small, text ?? string.Empty, MaxTextWidth),
                Icon = icon,
                Source = source,
                DurationMs = Math.Max(FadeMs * 2, durationMs)
            };
            Vector2 size = UIServices.Text.Measure(UIFont.Small, toast.Text, 1f);
            int iconW = icon == null ? 0 : IconSize + (Padding / 2);
            toast.Width = (int)Math.Ceiling(size.X) + iconW + (2 * Padding);
            toast.Height = Math.Max(icon == null ? 0 : IconSize, (int)Math.Ceiling(size.Y)) + (2 * Padding);

            toasts.Insert(0, toast);
            while (toasts.Count > MaxVisible)
            {
                toasts.RemoveAt(toasts.Count - 1);
            }

            RetargetOffsets();
        }

        /// <summary>Stack positions: newest at the bottom, each older toast above the previous one.</summary>
        private void RetargetOffsets()
        {
            float offset = 0;
            foreach (Toast toast in toasts)
            {
                toast.TargetOffset = offset;
                offset += toast.Height + Gap;
            }
        }

        /// <summary>Advance timers and slide animations; drop expired toasts.</summary>
        internal void Update(double elapsedMs)
        {
            bool removed = false;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                Toast toast = toasts[i];
                toast.ElapsedMs += elapsedMs;
                if (toast.ElapsedMs >= toast.DurationMs)
                {
                    toasts.RemoveAt(i);
                    removed = true;
                    continue;
                }

                float step = (float)(SlidePerMs * elapsedMs);
                toast.Offset = toast.Offset < toast.TargetOffset ? Math.Min(toast.TargetOffset, toast.Offset + step) : Math.Max(toast.TargetOffset, toast.Offset - step);
            }

            if (removed)
            {
                RetargetOffsets();
            }
        }

        internal void Draw(SpriteBatch b)
        {
            Point vp = UIServices.ViewportSize();
            int originBottom = vp.Y - MarginBottom;
            foreach (Toast toast in toasts)
            {
                float alpha = Alpha(toast);
                var box = new Rectangle(MarginX, originBottom - (int)toast.Offset - toast.Height, toast.Width, toast.Height);
                DrawHelper.PanelBox(b, box, Color.White * alpha);
                int textX = box.X + Padding;
                if (toast.Icon != null)
                {
                    DrawIcon(b, toast, new Rectangle(textX, box.Y + ((box.Height - IconSize) / 2), IconSize, IconSize), alpha);
                    textX += IconSize + (Padding / 2);
                }

                DrawHelper.Text(b, toast.Text, UIFont.Small, new Vector2(textX, box.Y + Padding), Theme.TextColor * alpha, false, 1f);
            }
        }

        /// <summary>Fade in over the first <see cref="FadeMs"/> and out over the last.</summary>
        private static float Alpha(Toast toast)
        {
            double remaining = toast.DurationMs - toast.ElapsedMs;
            double t = Math.Min(toast.ElapsedMs, remaining) / FadeMs;
            return (float)Math.Clamp(t, 0, 1);
        }

        private static void DrawIcon(SpriteBatch b, Toast toast, Rectangle slot, float alpha)
        {
            Rectangle source = toast.Source ?? toast.Icon!.Bounds;
            if (source.Width <= 0 || source.Height <= 0)
            {
                return;
            }

            float scale = Math.Min((float)slot.Width / source.Width, (float)slot.Height / source.Height);
            var position = new Vector2(slot.X + ((slot.Width - (source.Width * scale)) / 2f), slot.Y + ((slot.Height - (source.Height * scale)) / 2f));
            b.Draw(toast.Icon, position, source, Color.White * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
