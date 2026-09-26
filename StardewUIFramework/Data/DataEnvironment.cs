namespace UIFramework.Data
{
    /// <summary>Clock and screen values shared by the data layer (the tick keys volatile expression caches).</summary>
    internal static class DataEnvironment
    {
        /// <summary>The game tick (volatile values are cached within one tick).</summary>
        internal static long Tick => StardewValley.Game1.ticks;
    }
}
