using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using StardewValley;
using StardewValley.Triggers;
using UIFramework.Data.Building;
using UIFramework.Data.Expressions;
using UIFramework.Data.Model;
using UIFramework.Data.State;
using UIFramework.Rendering;

namespace UIFramework.Data.Loading
{
    /// <summary>Typos: members a definition model does not have, with suggestions by edit distance.</summary>
    internal sealed partial class DataValidator
    {
        /// <summary>The members of a definition model a JSON file may set (not the extension data bag), reflected once per model.</summary>
        internal static IReadOnlyList<PropertyInfo> ModelProperties(Type model)
        {
            if (!ModelPropertyCache.TryGetValue(model, out PropertyInfo[]? properties))
            {
                ModelPropertyCache[model] = properties = model.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.Name != "Unknown" && p.CanWrite).ToArray();
            }

            return properties;
        }

        /// <summary>The names of <see cref="ModelProperties"/>, built once per model.</summary>
        private static string[] ModelPropertyNames(Type model)
        {
            if (!ModelNameCache.TryGetValue(model, out string[]? names))
            {
                ModelNameCache[model] = names = ModelProperties(model).Select(p => p.Name).ToArray();
            }

            return names;
        }

        private static void CheckUnknown(IDictionary<string, JToken>? unknown, Type model, DataPath path, DataMessageLog log)
        {
            if (unknown == null || unknown.Count == 0)
            {
                return;
            }

            string[] known = ModelPropertyNames(model);
            foreach (string name in unknown.Keys)
            {
                if (name.StartsWith('$'))
                {
                    continue; // "$schema" and other editor metadata
                }

                string? suggestion = Suggest(name, known);
                log.Warn(path.Field(name), $"unknown field '{name}'{(suggestion != null ? $" (did you mean '{suggestion}'?)" : string.Empty)}; it is ignored.");
            }
        }

        /// <summary>The closest candidate by edit distance (case-insensitive), or null when none is close.</summary>
        internal static string? Suggest(string text, IEnumerable<string> candidates)
        {
            string lower = text.ToLowerInvariant();
            string? best = null;
            int bestDistance = int.MaxValue;
            foreach (string candidate in candidates)
            {
                int distance = Distance(lower, candidate.ToLowerInvariant());
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best != null && bestDistance <= Math.Max(2, text.Length / 3) ? best : null;
        }

        private static int Distance(string a, string b)
        {
            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++)
            {
                previous[j] = j;
            }

            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                }

                (previous, current) = (current, previous);
            }

            return previous[b.Length];
        }
    }
}
