using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework.Data.Model
{
    /// <summary>
    /// One field of a data <c>Form</c>: a row with a caption and an input bound to a state value. The form keeps the
    /// usual auto-form behavior (snapshot for Cancel, undo / redo history, dirty tracking, Save / Cancel / Undo / Redo
    /// buttons); edits write straight into state.
    /// </summary>
    internal sealed class FormFieldDefinition
    {
        /// <summary>Field id (the input's id is &lt;form id&gt;.&lt;Id&gt;). Default: the last part of Bind.</summary>
        public string? Id { get; set; }

        /// <summary>The state value the field edits (menu.x, config.x, player.x, .x inside a With). Default: menu.&lt;Id&gt;.</summary>
        public string? Bind { get; set; }

        /// <summary>Input kind: Checkbox, Number, Integer, Text or Dropdown (default: Dropdown with Choices, else Text).</summary>
        public string? Kind { get; set; }

        /// <summary>Caption text (default: the id split into words).</summary>
        public string? Label { get; set; }

        /// <summary>Tooltip of the input.</summary>
        public string? Tooltip { get; set; }

        /// <summary>Starts a new section with this title above the field.</summary>
        public string? Section { get; set; }

        /// <summary>Default value (only used while the state value does not exist yet).</summary>
        public string? Value { get; set; }

        /// <summary>Number: minimum.</summary>
        public string? Min { get; set; }

        /// <summary>Number: maximum.</summary>
        public string? Max { get; set; }

        /// <summary>Dropdown: choice values (array, or one comma-separated string).</summary>
        [JsonConverter(typeof(StringListConverter))]
        public List<string>? Choices { get; set; }

        /// <summary>Show the value without allowing edits.</summary>
        public string? ReadOnly { get; set; }

        /// <summary>An expression checked for every new value (event.value): false or a message rejects it, shown under the field.</summary>
        public string? Validate { get; set; }

        /// <summary>Fields that match no member (typos); reported by the validator with a suggestion.</summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? Unknown { get; set; }
    }
}
