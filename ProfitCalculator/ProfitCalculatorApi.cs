using ProfitCalculator.apis;
using ProfitCalculator.main;
using ProfitCalculator.main.builders;
using ProfitCalculator.main.memory;
using ProfitCalculator.main.models;
using StardewModdingAPI;

#nullable enable

namespace ProfitCalculator
{
    /// <summary>
    /// Implementation of <see cref="IProfitCalculatorApi"/>. Stores entries in the <see cref="ManualCropRegistry"/> held in the <see cref="Container"/>,
    /// and updates the <see cref="Calculator"/> right away when a save is loaded.
    /// </summary>
    public class ProfitCalculatorApi : IProfitCalculatorApi
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProfitCalculatorApi"/> class.
        /// </summary>
        public ProfitCalculatorApi()
        { }

        /// <inheritdoc/>
        public bool AddCrop(string seedItemId, string harvestItemId, int growthDays, int regrowDays, string seasons)
        {
            var Monitor = Container.Instance.GetInstance<IMonitor>(ModEntry.UniqueID);
            var registry = Container.Instance.GetInstance<ManualCropRegistry>(ModEntry.UniqueID);
            if (registry == null)
                return false;
            if (string.IsNullOrWhiteSpace(seedItemId) || string.IsNullOrWhiteSpace(harvestItemId) || growthDays <= 0)
            {
                Monitor?.Log($"API AddCrop rejected: seed '{seedItemId}', harvest '{harvestItemId}', growth days {growthDays}.", LogLevel.Warn);
                return false;
            }

            seedItemId = ManualCropRegistry.NormalizeId(seedItemId);
            ManualCropDefinition definition = new()
            {
                HarvestID = harvestItemId,
                GrowthTime = growthDays,
                RegrowthTime = regrowDays,
                Seasons = seasons
            };

            if (Context.IsWorldReady)
            {
                PlantData? plant = ManualCropBuilder.BuildCrop(seedItemId, definition);
                if (plant == null)
                    return false;
                registry.SetCrop(seedItemId, definition);
                var calculator = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID);
                if (calculator != null)
                    calculator.Crops[seedItemId] = plant;
                return true;
            }

            registry.SetCrop(seedItemId, definition);
            return true;
        }

        /// <inheritdoc/>
        public bool RemoveCrop(string seedItemId)
        {
            var registry = Container.Instance.GetInstance<ManualCropRegistry>(ModEntry.UniqueID);
            if (registry == null || string.IsNullOrWhiteSpace(seedItemId))
                return false;
            seedItemId = ManualCropRegistry.NormalizeId(seedItemId);
            bool removed = registry.RemoveCrop(seedItemId);
            if (removed && Context.IsWorldReady)
            {
                Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.Crops.Remove(seedItemId);
            }
            return removed;
        }

        /// <inheritdoc/>
        public void SetSeedPrice(string seedItemId, int price)
        {
            var registry = Container.Instance.GetInstance<ManualCropRegistry>(ModEntry.UniqueID);
            if (registry == null || string.IsNullOrWhiteSpace(seedItemId))
                return;
            registry.SetSeedPrice(seedItemId, price);

            // Manual crops store their price on the plant, which ShopAccessor doesn't see, so update loaded plants directly.
            if (price >= 0 && Context.IsWorldReady)
            {
                var calculator = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID);
                if (calculator == null)
                    return;
                foreach (string key in ManualCropRegistry.CandidateKeys(seedItemId))
                {
                    if (calculator.Crops.TryGetValue(key, out PlantData? plant))
                        plant.SeedPrice = price;
                }
            }
        }
    }
}
