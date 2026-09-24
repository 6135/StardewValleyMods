using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework.Data.Model
{
    /// <summary>
    /// One entry of an action list (an event handler written as data). The short form is a plain trigger action string
    /// (<c>"AddMoney 100"</c>); the object form adds a game state query <see cref="Condition"/>, a <see cref="When"/>
    /// expression, nested <see cref="Actions"/> and an <see cref="Else"/> branch.
    /// </summary>
    internal sealed class ActionDefinition
    {
        /// <summary>A trigger action to run (vanilla, this framework's <c>6135.UIFramework_*</c> or another mod's), e.g. <c>AddMoney 100</c>.</summary>
        public string? Action { get; set; }

        /// <summary>A game state query; the entry (its action and nested actions) only runs when it matches, otherwise <see cref="Else"/> runs.</summary>
        public string? Condition { get; set; }

        /// <summary>A bool value or expression (expressions arrive in v1.4); combined with <see cref="Condition"/>.</summary>
        public string? When { get; set; }

        /// <summary>Actions run after <see cref="Action"/> when the entry's conditions hold.</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? Actions { get; set; }

        /// <summary>Actions run instead when <see cref="Condition"/> / <see cref="When"/> do not hold.</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? Else { get; set; }

        /// <summary>Fields that match no member (typos); reported by the validator.</summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? Unknown { get; set; }

        /// <summary>True when only <see cref="Action"/> is set (written back as a plain string).</summary>
        [JsonIgnore]
        internal bool IsPlain => Condition == null && When == null && Actions == null && Else == null && (Unknown == null || Unknown.Count == 0);
    }
}
