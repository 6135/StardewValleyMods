using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework.Data.Model
{
    /// <summary>
    /// Reads an action list from any of its authoring forms: a single trigger action string, an object
    /// (<see cref="ActionDefinition"/>), or an array mixing both. Writes the plain entries back as strings.
    /// </summary>
    internal sealed class ActionListConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(List<ActionDefinition>);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.ReadFrom(reader);
            return Read(token, serializer);
        }

        /// <summary>Convert a token (string / object / array / null) to an action list.</summary>
        internal static List<ActionDefinition>? Read(JToken? token, JsonSerializer serializer)
        {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
            {
                return null;
            }

            var list = new List<ActionDefinition>();
            if (token is JArray array)
            {
                foreach (JToken item in array)
                {
                    ActionDefinition? entry = ReadEntry(item, serializer);
                    if (entry != null)
                    {
                        list.Add(entry);
                    }
                }
            }
            else
            {
                ActionDefinition? entry = ReadEntry(token, serializer);
                if (entry != null)
                {
                    list.Add(entry);
                }
            }

            return list;
        }

        private static ActionDefinition? ReadEntry(JToken token, JsonSerializer serializer)
        {
            switch (token.Type)
            {
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return null;
                case JTokenType.Object:
                    return token.ToObject<ActionDefinition>(serializer);
                case JTokenType.Array:
                    // a nested array is a group of actions without a condition
                    return new ActionDefinition { Actions = Read(token, serializer) };
                default:
                    string text = JsonText.Of(token);
                    return string.IsNullOrWhiteSpace(text) ? null : new ActionDefinition { Action = text };
            }
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is not List<ActionDefinition> list)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartArray();
            foreach (ActionDefinition entry in list)
            {
                if (entry.IsPlain)
                {
                    writer.WriteValue(entry.Action);
                }
                else
                {
                    serializer.Serialize(writer, entry);
                }
            }
            writer.WriteEndArray();
        }
    }

    /// <summary>
    /// Reads a list of strings written either as an array or as one comma-separated string (<c>"spring, summer"</c>).
    /// Use the array form when a value itself contains a comma.
    /// </summary>
    internal sealed class StringListConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(List<string>);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.ReadFrom(reader);
            switch (token.Type)
            {
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return null;
                case JTokenType.Array:
                    var items = new List<string>();
                    foreach (JToken item in token)
                    {
                        items.Add(item.Type == JTokenType.Null ? string.Empty : JsonText.Of(item));
                    }
                    return items;
                default:
                    var split = new List<string>();
                    foreach (string part in JsonText.Of(token).Split(','))
                    {
                        string trimmed = part.Trim();
                        if (trimmed.Length > 0)
                        {
                            split.Add(trimmed);
                        }
                    }
                    return split;
            }
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is not List<string> list)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartArray();
            foreach (string item in list)
            {
                writer.WriteValue(item);
            }
            writer.WriteEndArray();
        }
    }

    /// <summary>
    /// Reads a string value that may also be written as a JSON array or object (a <c>State</c> value holding a list,
    /// e.g. <c>"items": [ { "name": "A" } ]</c>): structures are kept as their compact JSON text, which the
    /// <c>State</c> data source parses back. Scalars are read as their text, like every other value field.
    /// </summary>
    internal sealed class JsonTextConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(string);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.ReadFrom(reader);
            return token.Type switch
            {
                JTokenType.Null or JTokenType.Undefined => null,
                JTokenType.Array or JTokenType.Object => token.ToString(Formatting.None),
                JTokenType.Boolean => (bool)token ? "True" : "False",
                _ => JsonText.Of(token)
            };
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            writer.WriteValue(value as string);
        }
    }

    /// <summary>Reads an element list written either as an array or as one element object (<c>"Cell": { "Label": "..." }</c>).</summary>
    internal sealed class ElementListConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(List<ElementDefinition>);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.ReadFrom(reader);
            switch (token.Type)
            {
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return null;
                case JTokenType.Array:
                    var list = new List<ElementDefinition>();
                    foreach (JToken item in token)
                    {
                        list.Add(item.Type == JTokenType.Null ? null! : item.ToObject<ElementDefinition>(serializer)!);
                    }

                    return list;
                default:
                    return new List<ElementDefinition> { token.ToObject<ElementDefinition>(serializer)! };
            }
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is not List<ElementDefinition> list)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartArray();
            foreach (ElementDefinition item in list)
            {
                serializer.Serialize(writer, item);
            }
            writer.WriteEndArray();
        }
    }
}
