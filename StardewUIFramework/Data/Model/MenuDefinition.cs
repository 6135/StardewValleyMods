using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework.Data.Model
{
    /// <summary>
    /// One entry of the <c>Mods/6135.UIFramework/Menus</c> asset, keyed <c>&lt;owner&gt;/&lt;menuId&gt;</c> where the owner is
    /// a loaded mod or content pack id. Holds the menu options (the data form of <c>IUIMenuOptions</c>), the root
    /// stack's layout, menu events and the element tree.
    /// </summary>
    internal sealed class MenuDefinition
    {
        /// <summary>
        /// Optional asset to read the definition from (a standalone file loaded with Content Patcher <c>Load</c>, e.g.
        /// <c>Mods/{{ModId}}/UI/Main</c>). Members set on the entry itself override the file's.
        /// </summary>
        public string? From { get; set; }

        /// <summary>A game state query checked when the menu is opened through data (actions, tile actions, console); the menu does not open while it fails.</summary>
        public string? Condition { get; set; }

        /// <summary>Keybind list (e.g. "F10" or "LeftControl + U") that toggles the menu.</summary>
        public string? Hotkey { get; set; }

        // ---------------------------------------------------------------------------------------------------------
        //  Options
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Title shown in the scroll banner above the box.</summary>
        public string? Title { get; set; }

        /// <summary>Fixed width in pixels (empty = fit the content).</summary>
        public string? Width { get; set; }

        /// <summary>Fixed height in pixels (empty = fit the content).</summary>
        public string? Height { get; set; }

        /// <summary>Show the red close button (default true).</summary>
        public string? ShowCloseButton { get; set; }

        /// <summary>Block input to the game while open (default true).</summary>
        public string? Modal { get; set; }

        /// <summary>Dim the game behind the menu (default true).</summary>
        public string? DimBackground { get; set; }

        /// <summary>Screen anchor: Center, TopLeft, TopCenter, TopRight, MiddleLeft, MiddleRight, BottomLeft, BottomCenter, BottomRight or Explicit.</summary>
        public string? Anchor { get; set; }

        /// <summary>X position when Anchor is Explicit.</summary>
        public string? X { get; set; }

        /// <summary>Y position when Anchor is Explicit.</summary>
        public string? Y { get; set; }

        /// <summary>Draw the dialogue box chrome (default true).</summary>
        public string? DrawBox { get; set; }

        /// <summary>Padding inside the chrome.</summary>
        public string? Padding { get; set; }

        /// <summary>Close on Escape / B (default true).</summary>
        public string? CloseOnEscape { get; set; }

        /// <summary>Let the player move / resize / collapse the window (default true).</summary>
        public string? PlayerLayout { get; set; }

        /// <summary>Let the player resize the window even when it sizes to its content (default false; fixed-size windows are always resizable).</summary>
        public string? Resizable { get; set; }

        /// <summary>Lay the root's children out in a row instead of a column.</summary>
        public string? Horizontal { get; set; }

        /// <summary>Pixels between the root's children (default 8).</summary>
        public string? Spacing { get; set; }

        /// <summary>Default cross-axis alignment of the root's children.</summary>
        public string? Alignment { get; set; }

        /// <summary>Id of the button activated by Enter.</summary>
        public string? DefaultButton { get; set; }

        /// <summary>Id of the button activated by Escape.</summary>
        public string? CancelButton { get; set; }

        // ---------------------------------------------------------------------------------------------------------
        //  State (v1.4)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Defaults of the menu's menu.* values ({ "count": 0, "name": "Farmer" }); only added for values that do not exist yet. A value may be a one-time "$:{...}" expression, or a JSON array (kept as its JSON text) read by a State source.</summary>
        [JsonProperty(ItemConverterType = typeof(JsonTextConverter))]
        public Dictionary<string, string>? State { get; set; }

        /// <summary>Named row sources the menu's collections reference by name: { "fruits": { "Rows": [...] }, "wines": "query:FLAVORED_ITEM Wine (O)398" }.</summary>
        public Dictionary<string, SourceDefinition>? Sources { get; set; }

        /// <summary>How long menu.* values live: Session (default, until the return to title) or Open (reset every time the menu opens).</summary>
        public string? StateLifetime { get; set; }

        /// <summary>Derived values readable as menu.&lt;name&gt;: { "total": "menu.price * menu.count" }.</summary>
        public Dictionary<string, string>? Computed { get; set; }

        /// <summary>Actions run when a state value changes: { "menu.count": [ ... ] } (event.old, event.new, event.key).</summary>
        [JsonProperty(ItemConverterType = typeof(ActionListConverter))]
        public Dictionary<string, List<ActionDefinition>>? Watch { get; set; }

        // ---------------------------------------------------------------------------------------------------------
        //  Cross-mod (v1.7)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Menu-local templates by name, expanded where an element's Type (or Template) names them.</summary>
        public Dictionary<string, TemplateDefinition>? Templates { get; set; }

        /// <summary>Values shared with contributors (ctx.&lt;key&gt; in their data, IUIScreenContext.GetString / GetNumber / GetBool in C#): { "name": "menu.name" }.</summary>
        public Dictionary<string, string>? Expose { get; set; }

        /// <summary>Commands shared with contributors (_Invoke ctx.&lt;key&gt;, IUIScreenContext.Invoke): { "log": [ ... ] }, run in the menu's scope.</summary>
        [JsonProperty(ItemConverterType = typeof(ActionListConverter))]
        public Dictionary<string, List<ActionDefinition>>? Commands { get; set; }

        // ---------------------------------------------------------------------------------------------------------
        //  Events
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Actions run while the menu is open, every UpdateIntervalMs (default: every tick); event.elapsed is the time since the last run.</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnUpdate { get; set; }

        /// <summary>Milliseconds between OnUpdate runs (0 = every tick).</summary>
        public string? UpdateIntervalMs { get; set; }

        /// <summary>Menu-level key handlers: [{ Key, Shift, Ctrl, Alt, Actions }].</summary>
        public List<KeyBindingDefinition>? Keys { get; set; }

        /// <summary>Actions run after the menu opened.</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnOpen { get; set; }

        /// <summary>Actions run after the menu closed.</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnClose { get; set; }

        /// <summary>Actions run when the menu's viewport scrolls.</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnScroll { get; set; }

        // ---------------------------------------------------------------------------------------------------------
        //  Tree
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The elements of the menu's root stack.</summary>
        public List<ElementDefinition>? Children { get; set; }

        /// <summary>Fields that match no member (typos); reported by the validator with a suggestion.</summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? Unknown { get; set; }
    }
}
