using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework.Data.Model
{
    /// <summary>
    /// One entry of the <c>Mods/6135.UIFramework/Owners</c> asset, keyed by mod id: settings shared by every data (and
    /// C#) UI of that owner, the data form of <c>SetTooltipDelay</c>, <c>SetDefaultStyle</c> and <c>RegisterHotkey</c>.
    /// </summary>
    internal sealed class OwnerDefinition
    {
        /// <summary>Tooltip delay in milliseconds for this owner's UIs (empty = the player's setting).</summary>
        public string? TooltipDelayMs { get; set; }

        /// <summary>Default style of every element of this owner.</summary>
        public StyleDefinition? DefaultStyle { get; set; }

        /// <summary>Named styles used by elements' Class ("Class": "header big" merges header then big, then the inline Style).</summary>
        public Dictionary<string, StyleDefinition>? Classes { get; set; }

        /// <summary>Global hotkeys: { "id": { "Keys": "LeftControl + J", "Actions": [...] } }, active in the world and in menus.</summary>
        public Dictionary<string, HotkeyDefinition>? Hotkeys { get; set; }

        /// <summary>Owner-wide templates by name (v1.7), usable in every data UI of this owner (a menu's own Templates win).</summary>
        public Dictionary<string, TemplateDefinition>? Templates { get; set; }

        /// <summary>Named rich tooltips, used by any RichTooltip / RowTooltip of this owner with <c>{ "From": "name" }</c>.</summary>
        public Dictionary<string, TooltipDefinition>? Tooltips { get; set; }

        /// <summary>Fields that match no member (typos).</summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? Unknown { get; set; }
    }

    /// <summary>A global hotkey of an owner.</summary>
    internal sealed class HotkeyDefinition
    {
        /// <summary>SMAPI keybind list, e.g. "F9" or "LeftControl + J, ControllerBack".</summary>
        public string? Keys { get; set; }

        /// <summary>A game state query; the actions only run while it matches.</summary>
        public string? Condition { get; set; }

        /// <summary>Actions run when the keys are pressed (in the owner's scope: session.*, config.*, player.* work unqualified).</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? Actions { get; set; }

        /// <summary>Fields that match no member (typos).</summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? Unknown { get; set; }
    }
}
