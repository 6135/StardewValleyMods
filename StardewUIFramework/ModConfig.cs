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
    }
}
