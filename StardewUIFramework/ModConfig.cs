namespace UIFramework
{
    /// <summary>The framework's own <c>config.json</c>.</summary>
    public sealed class ModConfig
    {
        /// <summary>How long the cursor must rest on an element before its tooltip appears.</summary>
        public int TooltipDelayMs { get; set; } = 400;

        /// <summary>Draw element bounds and ids in every framework menu.</summary>
        public bool DebugOverlay { get; set; }

        /// <summary>Trace-log every consumer callback invocation.</summary>
        public bool LogCallbacks { get; set; }

        // v1.1 settings (one region per feature; see architecture.md §16)

        // BEGIN RICHTEXT config
        /// <summary>Accent and pad every framework-drawn string (e.g. <c>[Çálçúláté~~~]</c>) to catch layout overflow before translating.</summary>
        public bool PseudoLocalize { get; set; }
        // END RICHTEXT config

        // BEGIN THEME config

        /// <summary>Name of the active theme (a key of the <c>Mods/6135.UIFramework/Themes</c> asset).</summary>
        public string Theme { get; set; } = "default";

        /// <summary>Multiplier for all framework text (0.75-2.0), on top of the theme's font scale.</summary>
        public float TextScale { get; set; } = 1f;

        /// <summary>Disable animation: the text caret stays solid instead of blinking.</summary>
        public bool ReducedMotion { get; set; }

        // END THEME config

        // BEGIN TOOLS config

        /// <summary>Keybind list that toggles the in-game inspector (empty = none).</summary>
        public string InspectorHotkey { get; set; } = "F10";

        // END TOOLS config
    }
}
