using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework.Data.Model
{
    /// <summary>
    /// One column. On a <c>DataGrid</c>: header, width and the per-row values (expressions over the row, read as
    /// <c>row.x</c> or the grid's <c>As</c> name). On a <c>Grid</c>: only <see cref="Width"/> (a track), which is what the
    /// string form <c>"Columns": "auto, *, 120"</c> expands to.
    /// </summary>
    internal sealed class ColumnDefinition
    {
        /// <summary>DataGrid: column id (sorting, _Sort, OnColumnResized's event.column). Content Patcher TargetField reaches the column by it.</summary>
        public string? Id { get; set; }

        /// <summary>DataGrid: header text.</summary>
        public string? Header { get; set; }

        /// <summary>Track width: auto, 120 / 120px, * or 2* (star columns share the leftover width).</summary>
        public string? Width { get; set; }

        /// <summary>DataGrid: smallest width in pixels (resize floor).</summary>
        public string? MinWidth { get; set; }

        /// <summary>DataGrid: alignment of the header and the text cells (Start, Center, End).</summary>
        public string? Align { get; set; }

        /// <summary>DataGrid: clicking the header sorts by this column.</summary>
        public string? Sortable { get; set; }

        /// <summary>DataGrid: the divider right of the header can be dragged.</summary>
        public string? Resizable { get; set; }

        /// <summary>DataGrid: cell text for a row, e.g. "${row.name}" (also the default sort key).</summary>
        public string? Text { get; set; }

        /// <summary>DataGrid: an expression giving the text sort key of a row (instead of the Text).</summary>
        public string? SortKey { get; set; }

        /// <summary>DataGrid: an expression giving the numeric sort key of a row; when set the column sorts numerically.</summary>
        public string? SortNumber { get; set; }

        /// <summary>DataGrid: elements built into each cell instead of a text label (an element or an array); ids are prefixed with the cell's id.</summary>
        [JsonConverter(typeof(ElementListConverter))]
        public List<ElementDefinition>? Cell { get; set; }

        /// <summary>DataGrid: tooltip text of a cell (an empty result hides it for that row).</summary>
        public string? Tooltip { get; set; }

        /// <summary>Fields that match no member (typos); reported by the validator with a suggestion.</summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? Unknown { get; set; }
    }

    /// <summary>
    /// Reads <c>Columns</c> as an array of <see cref="ColumnDefinition"/> or as a Grid track string
    /// (<c>"auto, *, 2*, 120"</c>), which becomes one column per track. A track string with an expression
    /// (<c>${...}</c>) stays whole, as one column whose width the grid evaluates.
    /// </summary>
    internal sealed class ColumnListConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(List<ColumnDefinition>);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.ReadFrom(reader);
            switch (token.Type)
            {
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return null;
                case JTokenType.Array:
                {
                    var list = new List<ColumnDefinition>();
                    foreach (JToken item in token)
                    {
                        list.Add(item.Type switch
                        {
                            JTokenType.Object => item.ToObject<ColumnDefinition>(serializer)!,
                            JTokenType.Null => null!,
                            _ => new ColumnDefinition { Width = item.ToString() }
                        });
                    }

                    return list;
                }

                default:
                    return FromTracks(token.ToString());
            }
        }

        /// <summary>One column per track of a Grid track string.</summary>
        internal static List<ColumnDefinition> FromTracks(string tracks)
        {
            var list = new List<ColumnDefinition>();
            if (tracks.Contains("${", StringComparison.Ordinal) || tracks.Contains("$:{", StringComparison.Ordinal))
            {
                list.Add(new ColumnDefinition { Width = tracks });
                return list;
            }

            foreach (string part in tracks.Split(','))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0)
                {
                    list.Add(new ColumnDefinition { Width = trimmed });
                }
            }

            return list;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is not List<ColumnDefinition> list)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartArray();
            foreach (ColumnDefinition item in list)
            {
                serializer.Serialize(writer, item);
            }
            writer.WriteEndArray();
        }
    }
}
