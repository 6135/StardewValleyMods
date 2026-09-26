using System;
using System.Collections.Generic;
using System.Globalization;
using UIFramework.Api;

namespace UIFramework.Core
{
    /// <summary>A grid track definition: <c>auto</c>, fixed pixels, or a star (proportional) weight.</summary>
    internal readonly record struct GridTrack
    {
        internal enum Kind { Auto, Pixels, Star }

        internal readonly Kind Type;
        internal readonly float Value;

        internal GridTrack(Kind type, float value)
        {
            Type = type;
            Value = value;
        }

        internal static GridTrack Auto => new(Kind.Auto, 0);
        internal static GridTrack Px(float pixels) => new(Kind.Pixels, pixels);
        internal static GridTrack Star(float weight) => new(Kind.Star, weight);

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
        internal static int AlignOffset(UIAlign align, int available, int size)
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
        internal static List<GridTrack> ParseTracks(string? definition)
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
                {
                    continue;
                }

                if (token == "auto")
                {
                    tracks.Add(GridTrack.Auto);
                }
                else if (token.EndsWith('*'))
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
            {
                tracks.Add(GridTrack.Star(1));
            }

            return tracks;
        }

        private static float ParseFloat(string text, float fallback)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
        }

        /// <summary>
        /// Resolve track sizes. <paramref name="autoSizes"/> holds the largest desired size of the content in each track
        /// (already computed by the caller for auto and star tracks); <paramref name="available"/> is the total space
        /// (excluding spacing). Star tracks share what is left after auto and pixel tracks, by weight. When pixel and
        /// auto tracks already use all of <paramref name="available"/>, star tracks get 0, continuing the shrinking share
        /// they get as the remaining space runs out: a star track never makes the total exceed a finite
        /// <paramref name="available"/>. Keeping star columns at their content minimum is the container's
        /// <c>MinWidthCore</c> concern, which keeps the offered width from dropping that low. Star tracks fall back to
        /// their content size only when there is nothing to share by: an unbounded <paramref name="available"/> or no
        /// positive weight.
        /// </summary>
        internal static float[] ResolveTracks(IReadOnlyList<GridTrack> tracks, float[] autoSizes, float available)
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
                {
                    continue;
                }

                if (infinite || starTotal <= 0)
                {
                    sizes[i] = autoSizes[i];
                }
                else if (remaining <= 0)
                {
                    sizes[i] = 0;
                }
                else
                {
                    sizes[i] = remaining * (tracks[i].Value / starTotal);
                }
            }
            return sizes;
        }

        /// <summary>
        /// The one rule for sharing too little width between items that sit side by side: minimums first, then the rest
        /// shared by how much each wants beyond its minimum. Every item gets <paramref name="min"/>[i] (a natural width
        /// below it counts as the minimum); when <paramref name="available"/> holds every natural width (or is
        /// unbounded) each item gets <paramref name="natural"/>[i]; otherwise what <paramref name="available"/> has
        /// beyond the minimums is split in proportion to each item's slack (natural minus minimum). When
        /// <paramref name="available"/> does not even hold the minimums, every item stays at its minimum (the total then
        /// exceeds it). Writes <paramref name="result"/>[i] for every index of <paramref name="min"/>; it may be the same
        /// buffer as <paramref name="natural"/>. No allocations.
        /// </summary>
        internal static void DistributeWidth(ReadOnlySpan<float> min, ReadOnlySpan<float> natural, float available, Span<float> result)
        {
            float minTotal = 0, naturalTotal = 0;
            for (int i = 0; i < min.Length; i++)
            {
                float low = Math.Max(0, min[i]);
                minTotal += low;
                naturalTotal += Math.Max(low, natural[i]);
            }

            bool unbounded = float.IsInfinity(available) || float.IsNaN(available);
            float extra = available - minTotal;
            float slackTotal = naturalTotal - minTotal;
            float share = unbounded || available >= naturalTotal ? 1
                : extra <= 0 || slackTotal <= 0 ? 0
                : extra / slackTotal;
            for (int i = 0; i < min.Length; i++)
            {
                float low = Math.Max(0, min[i]);
                float high = Math.Max(low, natural[i]);
                result[i] = share >= 1 ? high : low + ((high - low) * share);
            }
        }

        /// <summary>Sum of the tracks in [start, start + span) plus the spacing between them.</summary>
        internal static float SpanSize(float[] sizes, int start, int span, int spacing)
        {
            float total = 0;
            int end = Math.Min(sizes.Length, start + span);
            for (int i = start; i < end; i++)
            {
                total += sizes[i];
            }

            int gaps = Math.Max(0, end - start - 1);
            return total + (gaps * spacing);
        }

        /// <summary>Offset of track <paramref name="index"/> from the start, including spacing.</summary>
        internal static float TrackOffset(float[] sizes, int index, int spacing)
        {
            float offset = 0;
            for (int i = 0; i < index && i < sizes.Length; i++)
            {
                offset += sizes[i] + spacing;
            }

            return offset;
        }
    }
}
