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
        // END THEME config

        // BEGIN HUD config
        // END HUD config

        // BEGIN TOOLS config
        // END TOOLS config
    }
}
