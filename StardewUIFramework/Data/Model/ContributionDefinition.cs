using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework.Data.Model
{
    /// <summary>
    /// One entry of the <c>Mods/6135.UIFramework/Contributions</c> asset (v1.7), keyed <c>&lt;contributor&gt;/&lt;name&gt;</c>:
    /// what a mod or content pack adds to another mod's menu (C# or data) through its own API instance, the data form of
    /// <c>ContributeTo</c>, <c>OnScreenBuilt</c> and <c>IUIScreenContext.Subscribe</c>. Expressions read the owner's
    /// exposed values as <c>ctx.*</c>; <c>_Invoke ctx.&lt;cmd&gt;</c> runs its exposed commands.
    /// </summary>
    internal sealed class ContributionDefinition
    {
        /// <summary>The menu: "&lt;owner mod id&gt;/&lt;menu id&gt;".</summary>
        public string? Target { get; set; }

        /// <summary>The id of the Slot the Children go into.</summary>
        public string? Slot { get; set; }

        /// <summary>Order among the slot's contributions (lower first, default 0).</summary>
        public string? Priority { get; set; }

        /// <summary>The elements contributed to the Slot (built every time the menu opens).</summary>
        public List<ElementDefinition>? Children { get; set; }

        /// <summary>Actions run when the menu's owner publishes an event: { "saved": [ ... ] } (one subscription per contributor, menu and event).</summary>
        [JsonProperty(ItemConverterType = typeof(ActionListConverter))]
        public Dictionary<string, List<ActionDefinition>>? On { get; set; }

        /// <summary>Edits of the menu's tree applied every time it opens: [{ "Op": "Hide", "Target": "id" }, { "Op": "InsertAfter", "Target": "id", "Children": [...] }, ...]. Sealed elements refuse them.</summary>
        public List<DecorationOp>? Decorate { get; set; }

        /// <summary>Fields that match no member (typos).</summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? Unknown { get; set; }
    }
}
