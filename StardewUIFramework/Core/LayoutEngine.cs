using System;
using System.Collections.Generic;
using System.Globalization;
using UIFramework.Api;

namespace UIFramework.Core
{
    /// <summary>A grid track definition: <c>auto</c>, fixed pixels, or a star (proportional) weight.</summary>
    internal readonly struct GridTrack
    {
        public enum Kind { Auto, Pixels, Star }

        public readonly Kind Type;
        public readonly float Value;

        public GridTrack(Kind type, float value)
        {
            Type = type;
            Value = value;
        }

        public static GridTrack Auto => new(Kind.Auto, 0);
        public static GridTrack Px(float px) => new(Kind.Pixels, px);
        public static GridTrack Star(float weight) => new(Kind.Star, weight);

        public override string ToString() => Type switch
        {
            Kind.Auto => "auto",
            Kind.Pixels => Value.ToString(CultureInfo.InvariantCulture) + "px",
            _ => Value.ToString(CultureInfo.InvariantCulture) + "*"
        };
    }

    /// <summary>Pure layout helpers shared by containers (no game dependencies, unit-testable).</summary>
    internal static class LayoutEngine
    {
        /// <summary>Offset of a child of size <paramref name="size"/> inside a slot of <paramref name="available"/> for the alignment.</summary>
        public static int AlignOffset(UIAlign align, int available, int size)
        {
            return align switch
            {
                UIAlign.Center => Math.Max(0, (available - size) / 2),
                UIAlign.End => Math.Max(0, available - size),
                _ => 0
            };
        }

        /// <summary>
        /// Parse a comma separated track list: <c>auto</c>, <c>120px</c>, <c>120</c>, <c>*</c>, <c>2*</c>.
        /// Whitespace is ignored; an empty string yields a single star track. Unknown tokens are treated as auto.
        /// </summary>
        public static List<GridTrack> ParseTracks(string? definition)
        {
            var tracks = new List<GridTrack>();
            if (string.IsNullOrWhiteSpace(definition))
            {
                tracks.Add(GridTrack.Star(1));
                return tracks;
            }

            foreach (string raw in definition.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string token = raw.Trim().ToLowerInvariant();
                if (token.Length == 0)
                    continue;
                if (token == "auto")
                {
                    tracks.Add(GridTrack.Auto);
                }
                else if (token.EndsWith("*"))
                {
                    string weightText = token[..^1];
                    float weight = weightText.Length == 0 ? 1 : ParseFloat(weightText, 1);
                    tracks.Add(GridTrack.Star(Math.Max(0, weight)));
                }
                else
                {
                    string pxText = token.EndsWith("px") ? token[..^2] : token;
                    tracks.Add(GridTrack.Px(Math.Max(0, ParseFloat(pxText, 0))));
                }
            }

            if (tracks.Count == 0)
                tracks.Add(GridTrack.Star(1));
            return tracks;
        }

        private static float ParseFloat(string text, float fallback)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
        }

        /// <summary>
        /// Resolve track sizes. <paramref name="autoSizes"/> holds the largest desired size of the content in each track
        /// (already computed by the caller for auto and star tracks); <paramref name="available"/> is the total space
        /// (excluding spacing). Star tracks share what is left after auto and pixel tracks; if there is no remaining
        /// space star tracks fall back to their content size.
        /// </summary>
        public static float[] ResolveTracks(IReadOnlyList<GridTrack> tracks, float[] autoSizes, float available)
        {
            var sizes = new float[tracks.Count];
            float used = 0;
            float starTotal = 0;
            for (int i = 0; i < tracks.Count; i++)
            {
                switch (tracks[i].Type)
                {
                    case GridTrack.Kind.Pixels:
                        sizes[i] = tracks[i].Value;
                        used += sizes[i];
                        break;
                    case GridTrack.Kind.Auto:
                        sizes[i] = autoSizes[i];
                        used += sizes[i];
                        break;
                    default:
                        starTotal += tracks[i].Value;
                        break;
                }
            }

            float remaining = available - used;
            bool infinite = float.IsInfinity(available) || float.IsNaN(available);
            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].Type != GridTrack.Kind.Star)
                    continue;
                if (infinite || remaining <= 0 || starTotal <= 0)
                    sizes[i] = autoSizes[i];
                else
                    sizes[i] = remaining * (tracks[i].Value / starTotal);
            }
            return sizes;
        }

        /// <summary>Sum of the tracks in [start, start + span) plus the spacing between them.</summary>
        public static float SpanSize(float[] sizes, int start, int span, int spacing)
        {
            float total = 0;
            int end = Math.Min(sizes.Length, start + span);
            for (int i = start; i < end; i++)
                total += sizes[i];
            int gaps = Math.Max(0, end - start - 1);
            return total + gaps * spacing;
        }

        /// <summary>Offset of track <paramref name="index"/> from the start, including spacing.</summary>
        public static float TrackOffset(float[] sizes, int index, int spacing)
        {
            float offset = 0;
            for (int i = 0; i < index && i < sizes.Length; i++)
                offset += sizes[i] + spacing;
            return offset;
        }
    }
}
