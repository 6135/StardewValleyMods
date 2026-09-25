namespace ProfitCalculator.apis
{
    /// <summary>
    /// The API other mods can use to add crops to Profit Calculator. Get it with
    /// <c>Helper.ModRegistry.GetApi&lt;IProfitCalculatorApi&gt;("6135.ProfitCalculator")</c>.
    /// For the full crop schema (quality, fertilizer, stacks, sale price...), edit the
    /// <c>Mods/6135.ProfitCalculator/ManualCrops</c> asset instead.
    /// </summary>
    public interface IProfitCalculatorApi
    {
        /// <summary>
        /// Adds or replaces a crop. It replaces any built-in or asset crop with the same seed id.
        /// If a save is loaded, the crop is available immediately; otherwise it is built when the save loads.
        /// </summary>
        /// <param name="seedItemId">Seed item id, qualified (<c>(O)472</c>) or not (<c>472</c>).</param>
        /// <param name="harvestItemId">Harvested item id, qualified or not.</param>
        /// <param name="growthDays">Days until the first harvest. Must be positive.</param>
        /// <param name="regrowDays">Days to regrow after a harvest; zero or less if it doesn't regrow.</param>
        /// <param name="seasons">Space-separated seasons, for example <c>"spring summer"</c>.</param>
        /// <returns>True if the crop was registered.</returns>
        bool AddCrop(string seedItemId, string harvestItemId, int growthDays, int regrowDays, string seasons);

        /// <summary>
        /// Removes a crop previously added with <see cref="AddCrop"/>. A built-in or asset crop it replaced comes back before the next calculation.
        /// </summary>
        /// <param name="seedItemId">Seed item id used in <see cref="AddCrop"/>.</param>
        /// <returns>True if a crop was removed.</returns>
        bool RemoveCrop(string seedItemId);

        /// <summary>
        /// Overrides the seed price used for a crop. A negative price removes the override.
        /// </summary>
        /// <param name="seedItemId">Seed item id, qualified or not.</param>
        /// <param name="price">The seed price in gold.</param>
        void SetSeedPrice(string seedItemId, int price);
    }
}
