using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework.Data.Model
{
    /// <summary>
    /// A rich tooltip (<c>RichTooltip</c> on any element, <c>RowTooltip</c> on a DataGrid): an optional wrap width and
    /// a list of blocks. May also be written as the block array alone.
    /// </summary>
    [JsonConverter(typeof(TooltipConverter))]
    internal sealed class TooltipDefinition
    {
        /// <summary>
        /// The name of one of the owner's <c>Tooltips</c> (<c>Mods/6135.UIFramework/Owners</c>) this tooltip starts from:
        /// its blocks come first, then this tooltip's own; this tooltip's <see cref="MaxWidth"/> wins when set.
        /// </summary>
        public string? From { get; set; }

        /// <summary>Wrap lines wider than this many pixels (0 = only the screen limits the width).</summary>
        public string? MaxWidth { get; set; }

        /// <summary>The blocks, top to bottom.</summary>
        public List<TooltipBlockDefinition>? Blocks { get; set; }

        /// <summary>Fields that match no member (typos); reported by the validator with a suggestion.</summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? Unknown { get; set; }
    }

    /// <summary>One block of a rich tooltip; which members apply depends on <see cref="Type"/>.</summary>
    internal sealed class TooltipBlockDefinition
    {
        /// <summary>Block kind: Title, Line, Icon, Item, Divider or Money.</summary>
        public string? Type { get; set; }

        /// <summary>Title / Line: the (rich) text; ${...} is live.</summary>
        public string? Text { get; set; }

        /// <summary>Title / Line: text color; may be an expression (e.g. "${row.profit &lt; 0 ? 'red' : 'green'}").</summary>
        public string? Color { get; set; }

        /// <summary>Icon: an image reference (sprite:Owner/name, item:(O)24, asset:Path@x,y,w,h).</summary>
        public string? Sprite { get; set; }

        /// <summary>Icon: source rectangle "x,y,w,h" (overrides the reference's own).</summary>
        public string? Source { get; set; }

        /// <summary>Icon: scale (default: the sprite's, else 1).</summary>
        public string? Scale { get; set; }

        /// <summary>Item: a qualified item id or item query ("(O)24", "FLAVORED_ITEM Wine (O)398"), or an expression giving an item ("${row.item}"); items keep their tint.</summary>
        public string? Item { get; set; }

        /// <summary>Money: the amount (an expression is allowed).</summary>
        public string? Amount { get; set; }

        /// <summary>An expression; the block is only shown while it is true.</summary>
        public string? When { get; set; }

        /// <summary>Fields that match no member (typos); reported by the validator with a suggestion.</summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? Unknown { get; set; }
    }

    /// <summary>The tooltip block kinds.</summary>
    internal static class TooltipBlockKinds
    {
        internal static readonly string[] All = { "Title", "Line", "Icon", "Item", "Divider", "Money" };

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

    /// <summary>Reads a <see cref="TooltipDefinition"/> from an object or from its block array.</summary>
    internal sealed class TooltipConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(TooltipDefinition);

        public override bool CanWrite => false;

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.ReadFrom(reader);
            switch (token.Type)
            {
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return null;
                case JTokenType.Array:
                    return new TooltipDefinition { Blocks = token.ToObject<List<TooltipBlockDefinition>>(serializer) };
                case JTokenType.Object:
                {
                    var result = new TooltipDefinition();
                    using JsonReader objectReader = token.CreateReader();
                    serializer.Populate(objectReader, result);
                    return result;
                }

                default:
                    // a plain string: one line
                    return new TooltipDefinition { Blocks = new List<TooltipBlockDefinition> { new() { Type = "Line", Text = JsonText.Of(token) } } };
            }
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }
    }
}
