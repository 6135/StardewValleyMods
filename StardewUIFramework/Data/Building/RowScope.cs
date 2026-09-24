using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Internal;
using UIFramework.Core;
using UIFramework.Data.Expressions;

namespace UIFramework.Data.Building
{
    /// <summary>
    /// Rows of collections as expression values: the local variables of a row (<c>row</c>, the collection's
    /// <c>As</c> name and <c>index</c>), JSON rows converted to <see cref="DataValue"/>s, item rows, and the item
    /// cache that turns item ids, item queries and item values into <see cref="Item"/> instances.
    /// </summary>
    internal static class RowScope
    {
        /// <summary>The local name every row is readable as.</summary>
        internal const string RowName = "row";

        /// <summary>The local name of the row's position in the (filtered, sorted) source.</summary>
        internal const string IndexName = "index";

        /// <summary><paramref name="scope"/> with <paramref name="row"/> as <c>row</c> (and <paramref name="alias"/>) and its position as <c>index</c>.</summary>
        internal static DataScope For(DataScope scope, DataValue row, int index, string? alias)
        {
            var locals = new Dictionary<string, DataValue>(StringComparer.Ordinal)
            {
                [RowName] = row,
                [IndexName] = DataValue.FromNumber(index)
            };
            if (!string.IsNullOrWhiteSpace(alias))
            {
                string name = alias.Trim();
                locals[name] = row;
                locals[name + "Index"] = DataValue.FromNumber(index);
            }

            return scope.WithLocals(locals);
        }

        /// <summary>A field row (case-insensitive keys), wrapped as an opaque value whose members expressions read.</summary>
        internal static DataValue Object(Dictionary<string, DataValue> fields) => DataValue.Opaque(new RowFields(fields));

        // ---------------------------------------------------------------------------------------------------------
        //  JSON
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Convert a JSON token: objects become field rows, arrays lists, scalars their value (text is typed like state: numbers and true / false).</summary>
        internal static DataValue FromJson(JToken? token, int depth = 0)
        {
            if (token == null || depth > ExpressionLimits.MaxDepth)
            {
                return DataValue.Null;
            }

            switch (token.Type)
            {
                case JTokenType.Object:
                {
                    var fields = new Dictionary<string, DataValue>(StringComparer.OrdinalIgnoreCase);
                    foreach (JProperty property in ((JObject)token).Properties())
                    {
                        fields[property.Name] = FromJson(property.Value, depth + 1);
                    }

                    return Object(fields);
                }

                case JTokenType.Array:
                {
                    var list = new List<DataValue>();
                    foreach (JToken item in token)
                    {
                        if (list.Count >= ExpressionLimits.MaxListLength)
                        {
                            break;
                        }

                        list.Add(FromJson(item, depth + 1));
                    }

                    return DataValue.FromList(list);
                }

                case JTokenType.Integer:
                case JTokenType.Float:
                    return DataValue.FromNumber(token.Value<double>());
                case JTokenType.Boolean:
                    return DataValue.FromBool(token.Value<bool>());
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return DataValue.Null;
                default:
                    return State.StateAddress.Infer(token.ToString());
            }
        }

        /// <summary>Parse JSON text holding a list (a State value; <c>[a, b]</c> without quotes also works); a non-JSON text is a one-row list of that text, empty text no rows.</summary>
        internal static IReadOnlyList<DataValue> ParseList(string? text, out string? error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<DataValue>();
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith('[') || trimmed.StartsWith('{'))
            {
                try
                {
                    DataValue parsed = FromJson(JToken.Parse(trimmed));
                    return parsed.AsList();
                }
                catch (Exception ex)
                {
                    // "[a, b, c]" without JSON quotes (trigger action arguments lose their quotes): split on commas
                    if (trimmed.StartsWith('[') && trimmed.EndsWith(']') && !trimmed.Contains('{'))
                    {
                        var values = new List<DataValue>();
                        foreach (string part in trimmed.Substring(1, trimmed.Length - 2).Split(','))
                        {
                            string value = part.Trim().Trim('"', '\'');
                            if (value.Length > 0)
                            {
                                values.Add(State.StateAddress.Infer(value));
                            }
                        }

                        return values;
                    }

                    error = $"not a JSON array: {ex.Message}";
                    return Array.Empty<DataValue>();
                }
            }

            return new[] { State.StateAddress.Infer(text) };
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Items
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The row of an item: id, qualifiedId, name, displayName, description, price, category, quality, stack, type and the item itself (item).</summary>
        internal static DataValue ForItem(Item item)
        {
            var fields = new Dictionary<string, DataValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = DataValue.FromString(item.ItemId),
                ["qualifiedId"] = DataValue.FromString(item.QualifiedItemId),
                ["name"] = DataValue.FromString(item.Name),
                ["displayName"] = DataValue.FromString(item.DisplayName),
                ["description"] = DataValue.FromString(SafeDescription(item)),
                ["price"] = DataValue.FromNumber(item.salePrice()),
                ["category"] = DataValue.FromNumber(item.Category),
                ["quality"] = DataValue.FromNumber(item.Quality),
                ["stack"] = DataValue.FromNumber(item.Stack),
                ["type"] = DataValue.FromString(item.TypeDefinitionId),
                ["item"] = DataValue.Opaque(item)
            };
            return Object(fields);
        }

        /// <summary>A member of an item value (<c>row.item.displayName</c>, <c>${someItem.price}</c>).</summary>
        internal static bool TryItemMember(Item item, string member, out DataValue value)
        {
            value = member.ToLowerInvariant() switch
            {
                "id" or "itemid" => DataValue.FromString(item.ItemId),
                "qualifiedid" => DataValue.FromString(item.QualifiedItemId),
                "name" => DataValue.FromString(item.Name),
                "displayname" => DataValue.FromString(item.DisplayName),
                "description" => DataValue.FromString(SafeDescription(item)),
                "price" => DataValue.FromNumber(item.salePrice()),
                "category" => DataValue.FromNumber(item.Category),
                "quality" => DataValue.FromNumber(item.Quality),
                "stack" => DataValue.FromNumber(item.Stack),
                "type" => DataValue.FromString(item.TypeDefinitionId),
                _ => DataValue.Null
            };
            return !value.IsNull;
        }

        private static string SafeDescription(Item item)
        {
            try
            {
                return item.getDescription() ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// The item a value stands for: an item value as is; text as a qualified item id or an item query
        /// (<c>FLAVORED_ITEM Wine (O)398</c>), created once per text (cached) with <paramref name="stack"/> /
        /// <paramref name="quality"/> applied; a field row by its <c>item</c> field. Null when nothing matches.
        /// </summary>
        internal static Item? ItemOf(DataValue value, int stack = 1, int quality = 0)
        {
            switch (value.AsObject())
            {
                case Item item:
                    return item;
                case RowFields row when row.TryGetValue("item", out DataValue inner) && inner.AsObject() is Item rowItem:
                    return rowItem;
                case Bridge.DataSourceHandle.SourceRow sourceRow when sourceRow.Source.TryGetField(sourceRow.Index, "item", out DataValue sourceItem) && sourceItem.AsObject() is Item hookItem:
                    return hookItem;
                case RowFields:
                case string:
                    break;
                case { } model when value.Kind == DataKind.Object && Bridge.ModelAccessor.ItemOf(model) is { } modelItem:
                    return modelItem; // a C# row object with an Item member (ExposeRows)
            }

            string text = value.Kind == DataKind.Object ? string.Empty : value.AsString().Trim();
            return text.Length == 0 ? null : ItemCache.Get(text, stack, quality);
        }

        /// <summary>Items created from text, memoized by (text, stack, quality); cleared on return to title and when it grows too large.</summary>
        internal static class ItemCache
        {
            private const int MaxEntries = 512;
            private static readonly Dictionary<(string, int, int), Item?> Items = new();

            internal static Item? Get(string text, int stack, int quality)
            {
                var key = (text, stack, quality);
                if (Items.TryGetValue(key, out Item? cached))
                {
                    return cached;
                }

                if (Items.Count >= MaxEntries)
                {
                    Items.Clear();
                }

                Item? item = Create(text);
                if (item != null)
                {
                    item.Stack = Math.Max(1, stack);
                    item.Quality = Math.Clamp(quality, 0, 4);
                }

                if (item != null || Context.IsWorldReady)
                {
                    Items[key] = item; // a miss before a save is loaded is retried (item queries need the world)
                }

                return item;
            }

            internal static void Clear() => Items.Clear();

            /// <summary>Resolve <paramref name="text"/> as an item id first, then as an item query (first result).</summary>
            private static Item? Create(string text)
            {
                if (!text.Contains(' ', StringComparison.Ordinal) && ItemRegistry.GetData(text) != null)
                {
                    return ItemRegistry.Create(text, allowNull: true);
                }

                foreach (Item item in Query(text, null, 1, out _))
                {
                    return item;
                }

                return ItemRegistry.Create(text, allowNull: true);
            }
        }

        /// <summary>Run a vanilla item query; returns the items (at most <paramref name="max"/>) and the first error.</summary>
        internal static List<Item> Query(string query, string? perItemCondition, int? max, out string? error)
        {
            var items = new List<Item>();
            string? firstError = null;
            try
            {
                var context = new ItemQueryContext(Game1.currentLocation, Game1.player, Game1.random, "UI Framework data source");
                ItemQueryResult[] results = ItemQueryResolver.TryResolve(query, context, ItemQuerySearchMode.All, perItemCondition, max, logError: (q, message) => firstError ??= $"'{q}': {message}");
                foreach (ItemQueryResult result in results)
                {
                    if (result.Item is Item item)
                    {
                        if (result.OverrideStackSize.HasValue)
                        {
                            item.Stack = result.OverrideStackSize.Value;
                        }

                        items.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                firstError ??= ex.Message;
                UIServices.Log($"Item query '{query}' failed: {ex}", LogLevel.Trace);
            }

            error = firstError;
            return items;
        }

        /// <summary>Invariant text of a number for ids and messages.</summary>
        internal static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The fields of a row (case-insensitive). Expressions read them as members (<c>row.name</c>).</summary>
    internal sealed class RowFields : Dictionary<string, DataValue>, IReadOnlyDictionary<string, DataValue>
    {
        internal RowFields(IDictionary<string, DataValue> fields) : base(fields, StringComparer.OrdinalIgnoreCase)
        {
        }

        /// <summary>A short text form (the name / id / displayName field when there is one), used when a row is shown as text.</summary>
        public override string ToString()
        {
            foreach (string key in new[] { "displayName", "label", "name", "id", "value", "key" })
            {
                if (TryGetValue(key, out DataValue value) && !value.IsNull)
                {
                    return value.AsString();
                }
            }

            return "{" + string.Join(", ", Keys) + "}";
        }
    }
}
