using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework.Data.Model
{
    /// <summary>
    /// One entry of the <c>Mods/6135.UIFramework/Huds</c> asset, keyed <c>&lt;owner&gt;/&lt;hudId&gt;</c>: a HUD widget (the
    /// data form of <c>CreateHud</c>) with its options, state and element tree. Shown in the world while no menu is
    /// open; <c>ShowHud</c> / <c>HideHud</c> / <c>ToggleHud</c> and the <see cref="Hotkey"/> switch it per player.
    /// </summary>
    internal sealed class HudDefinition
    {
        /// <summary>Whether the widget starts shown for each player (default true).</summary>
        public string? Visible { get; set; }

        /// <summary>A live bool expression; the widget is only shown while it is true (combined with the per-player visibility).</summary>
        public string? ShowWhen { get; set; }

        /// <summary>Keybind list that toggles the widget for the current player.</summary>
        public string? Hotkey { get; set; }

        /// <summary>Screen anchor (TopLeft default): Center, TopLeft, TopCenter, TopRight, MiddleLeft, MiddleRight, BottomLeft, BottomCenter, BottomRight or Explicit.</summary>
        public string? Anchor { get; set; }

        /// <summary>Offset from the anchor (or the position with Explicit).</summary>
        public string? X { get; set; }

        /// <summary>Offset from the anchor (or the position with Explicit).</summary>
        public string? Y { get; set; }

        /// <summary>Fixed width (empty = fit the content).</summary>
        public string? Width { get; set; }

        /// <summary>Fixed height (empty = fit the content).</summary>
        public string? Height { get; set; }

        /// <summary>Draw the box behind the widget (default true).</summary>
        public string? DrawBox { get; set; }

        /// <summary>Opacity of the box, 0-1.</summary>
        public string? Opacity { get; set; }

        /// <summary>Take hover / clicks (and let the player drag the widget).</summary>
        public string? Interactive { get; set; }

        /// <summary>Stay drawn on top of an open menu (like toasts) instead of hiding while one is open; input stays world-only.</summary>
        public string? ShowOverMenus { get; set; }

        /// <summary>Lay the root's children out in a row.</summary>
        public string? Horizontal { get; set; }

        /// <summary>Pixels between the root's children.</summary>
        public string? Spacing { get; set; }

        /// <summary>Default cross-axis alignment of the root's children.</summary>
        public string? Alignment { get; set; }

        /// <summary>Defaults of the widget's menu.* values (a JSON array or object is kept as its JSON text, for State sources).</summary>
        [JsonProperty(ItemConverterType = typeof(JsonTextConverter))]
        public Dictionary<string, string>? State { get; set; }

        /// <summary>Named row sources the widget's collections reference by name ({ "fruits": { "Rows": [...] } }).</summary>
        public Dictionary<string, SourceDefinition>? Sources { get; set; }

        /// <summary>Derived values readable as menu.&lt;name&gt;.</summary>
        public Dictionary<string, string>? Computed { get; set; }

        /// <summary>Actions run when a state value changes.</summary>
        [JsonProperty(ItemConverterType = typeof(ActionListConverter))]
        public Dictionary<string, List<ActionDefinition>>? Watch { get; set; }

        /// <summary>Actions run while the widget is shown, every UpdateIntervalMs (default: every tick).</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnUpdate { get; set; }

        /// <summary>Milliseconds between OnUpdate runs (0 = every tick).</summary>
        public string? UpdateIntervalMs { get; set; }

        /// <summary>The elements of the widget's root stack.</summary>
        public List<ElementDefinition>? Children { get; set; }

        /// <summary>Fields that match no member (typos).</summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? Unknown { get; set; }
    }
}
