using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework.Data.Model
{
    /// <summary>
    /// Where the rows of a collection (<c>List</c>, <c>DataGrid</c>, <c>Repeat</c>, dropdown <c>ChoicesSource</c>)
    /// come from. Sources are resolved when the UI opens, on <c>_Refresh</c> / <c>_Rebuild</c> and when what they
    /// read changes (never every frame); <see cref="Filter"/> and <see cref="Sort"/> run over the resolved rows.
    /// <para>
    /// Shorthand (a string instead of an object): <c>"${outer.items}"</c> (an expression giving a list, e.g. an outer
    /// row's field in a nested Repeat), <c>"themes"</c>, <c>"range:1..10"</c> (or <c>1..10..2</c>),
    /// <c>"query:ALL_ITEMS (O)"</c>, <c>"asset:Data/Machines"</c>, <c>"hook:Name"</c>, the name of an entry of the
    /// menu's <c>Sources</c>, or a state key holding a JSON array (<c>"menu.items"</c>).
    /// </para>
    /// </summary>
    [JsonConverter(typeof(SourceConverter))]
    internal sealed class SourceDefinition
    {
        /// <summary>Source kind: Rows, Range, ItemQuery, State, Value, Themes, Asset, Hook or Named (inferred from the other members when omitted).</summary>
        public string? Type { get; set; }

        /// <summary>Rows: inline rows; each row is a JSON object (read as row.&lt;field&gt;) or a plain value (read as row).</summary>
        public List<JToken>? Rows { get; set; }

        /// <summary>Range: first number (an expression is allowed).</summary>
        public string? From { get; set; }

        /// <summary>Range: last number, inclusive.</summary>
        public string? To { get; set; }

        /// <summary>Range: step (default 1; negative counts down).</summary>
        public string? Step { get; set; }

        /// <summary>ItemQuery: a vanilla item query (ALL_ITEMS (O), FLAVORED_ITEM Wine (O)398, RANDOM_ITEMS (O)...). Rows expose id, qualifiedId, name, displayName, description, price, category, quality, stack, type and item. ${...} is allowed.</summary>
        public string? Query { get; set; }

        /// <summary>ItemQuery: a game state query each item must match (ITEM_CATEGORY Target -75 ...).</summary>
        public string? PerItemCondition { get; set; }

        /// <summary>Value: an expression giving a list (e.g. "${season.crops}", an outer row's field inside a nested Repeat); re-checked when state changes.</summary>
        public string? Value { get; set; }

        /// <summary>State: the state key holding a JSON array (menu.items, config.favorites...).</summary>
        public string? State { get; set; }

        /// <summary>Asset: a Dictionary&lt;string, string&gt; asset (Data/Machines-like string data); rows expose key, value and fields (value split on '/').</summary>
        public string? Asset { get; set; }

        /// <summary>Hook: a row source registered from C# (arrives in UI Framework 1.6).</summary>
        public string? Hook { get; set; }

        /// <summary>Named: an entry of the menu's Sources (its Filter / Sort / Limit are applied first).</summary>
        public string? Name { get; set; }

        /// <summary>An expression over the row (row.x, or the collection's As name); rows where it is false are left out.</summary>
        public string? Filter { get; set; }

        /// <summary>An expression over the row giving its sort key (numbers sort numerically, text alphabetically).</summary>
        public string? Sort { get; set; }

        /// <summary>Sort descending (default false).</summary>
        public string? SortDescending { get; set; }

        /// <summary>At most this many rows (after Filter and Sort).</summary>
        public string? Limit { get; set; }

        /// <summary>Fields that match no member (typos); reported by the validator with a suggestion.</summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? Unknown { get; set; }

        /// <summary>The text of the string form (<c>"menu.items"</c>, or an Image's <c>"x,y,w,h"</c> rectangle), or null for the other forms.</summary>
        internal string? Shorthand => Type == null && Name != null && Name == State ? Name : null;

        /// <summary>The effective kind (explicit <see cref="Type"/>, else inferred), or null when nothing says where the rows come from.</summary>
        internal string? Kind
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Type))
                {
                    return Type.Trim();
                }

                if (Rows != null)
                {
                    return SourceKinds.Rows;
                }

                if (From != null || To != null)
                {
                    return SourceKinds.Range;
                }

                if (Query != null)
                {
                    return SourceKinds.ItemQuery;
                }

                if (Value != null)
                {
                    return SourceKinds.Value;
                }

                if (State != null)
                {
                    return SourceKinds.State;
                }

                if (Asset != null)
                {
                    return SourceKinds.Asset;
                }

                if (Hook != null)
                {
                    return SourceKinds.Hook;
                }

                return Name != null ? SourceKinds.Named : null;
            }
        }
    }

    /// <summary>The source kinds.</summary>
    internal static class SourceKinds
    {
        internal const string Rows = "Rows";
        internal const string Range = "Range";
        internal const string ItemQuery = "ItemQuery";
        internal const string State = "State";
        internal const string Value = "Value";
        internal const string Themes = "Themes";
        internal const string Asset = "Asset";
        internal const string Hook = "Hook";
        internal const string Named = "Named";

        internal static readonly string[] All = { Rows, Range, ItemQuery, State, Value, Themes, Asset, Hook, Named };

        /// <summary>The canonical spelling of a kind, or null.</summary>
        internal static string? Canonical(string? kind)
        {
            if (kind == null)
            {
                return null;
            }

            foreach (string known in All)
            {
                if (string.Equals(known, kind.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return known;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Reads a <see cref="SourceDefinition"/> from an object or from its string shorthand (see the type's summary).
    /// A bare name is kept as <see cref="SourceDefinition.Name"/> plus <see cref="SourceDefinition.State"/>; the
    /// builder decides which one applies (an entry of the menu's <c>Sources</c> wins over a state key).
    /// </summary>
    internal sealed class SourceConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(SourceDefinition);

        public override bool CanWrite => false;

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.ReadFrom(reader);
            switch (token.Type)
            {
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return null;
                case JTokenType.Object:
                {
                    // Populate fills the members without going through this (type-level) converter again
                    var result = new SourceDefinition();
                    using JsonReader objectReader = token.CreateReader();
                    serializer.Populate(objectReader, result);
                    return result;
                }

                case JTokenType.Array:
                    return new SourceDefinition { Type = SourceKinds.Rows, Rows = new List<JToken>(token.Children()) };
                default:
                    return Parse(token.ToString());
            }
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }

        /// <summary>The definition a shorthand string stands for.</summary>
        internal static SourceDefinition Parse(string text)
        {
            string trimmed = text.Trim();
            if (trimmed.StartsWith("${", StringComparison.Ordinal) || trimmed.StartsWith("$:{", StringComparison.Ordinal))
            {
                return new SourceDefinition { Type = SourceKinds.Value, Value = trimmed };
            }

            if (trimmed.Equals("themes", StringComparison.OrdinalIgnoreCase))
            {
                return new SourceDefinition { Type = SourceKinds.Themes };
            }

            int colon = trimmed.IndexOf(':');
            if (colon > 0)
            {
                string prefix = trimmed.Substring(0, colon).Trim().ToLowerInvariant();
                string rest = trimmed.Substring(colon + 1).Trim();
                switch (prefix)
                {
                    case "range":
                    {
                        string[] parts = rest.Split("..", StringSplitOptions.TrimEntries);
                        return new SourceDefinition
                        {
                            Type = SourceKinds.Range,
                            From = parts.Length > 0 ? parts[0] : "0",
                            To = parts.Length > 1 ? parts[1] : parts[0],
                            Step = parts.Length > 2 ? parts[2] : null
                        };
                    }

                    case "query":
                        return new SourceDefinition { Type = SourceKinds.ItemQuery, Query = rest };
                    case "asset":
                        return new SourceDefinition { Type = SourceKinds.Asset, Asset = rest };
                    case "hook":
                        return new SourceDefinition { Type = SourceKinds.Hook, Hook = rest };
                    case "state":
                        return new SourceDefinition { Type = SourceKinds.State, State = rest };
                    case "source":
                        return new SourceDefinition { Type = SourceKinds.Named, Name = rest };
                }
            }

            // a named source or a state key (decided at build time), or an Image's source rectangle: kept as written
            return new SourceDefinition { Name = text, State = text };
        }
    }
}
