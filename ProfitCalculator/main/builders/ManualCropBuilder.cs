using ProfitCalculator.main.memory;
using ProfitCalculator.main.models;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using CropData = ProfitCalculator.main.models.CropData;
using SCropData = StardewValley.GameData.Crops.CropData;
using SObject = StardewValley.Object;

#nullable enable

namespace ProfitCalculator.main.builders
{
    /// <summary>
    /// Builds the manual crops from the <see cref="ManualCropRegistry.ManualCropsAsset"/> asset and from the crops added through the mod API.
    /// API entries replace asset entries with the same seed id.
    /// </summary>
    public class ManualCropBuilder : IDataBuilder
    {
        /// <inheritdoc/>
        /// <summary>
        /// Builds the manual crops, keyed by the seed id as given in the asset or the API call.
        /// </summary>
        /// <returns> A dictionary of crops. </returns>
        public virtual Dictionary<string, PlantData> BuildCrops()
        {
            var Monitor = Container.Instance.GetInstance<IMonitor>(ModEntry.UniqueID);
            var registry = Container.Instance.GetInstance<ManualCropRegistry>(ModEntry.UniqueID);

            Dictionary<string, ManualCropDefinition> definitions = new();
            try
            {
                var assetCrops = Game1.content.Load<Dictionary<string, ManualCropDefinition>>(ManualCropRegistry.ManualCropsAsset);
                if (assetCrops != null)
                {
                    foreach (var entry in assetCrops)
                    {
                        if (entry.Value != null)
                            definitions[ManualCropRegistry.NormalizeId(entry.Key)] = entry.Value;
                    }
                }
            }
            catch (Exception e)
            {
                Monitor?.Log($"Failed to load {ManualCropRegistry.ManualCropsAsset}: {e.Message}", LogLevel.Warn);
            }

            if (registry != null)
            {
                foreach (var entry in registry.GetCrops())
                {
                    definitions[entry.Key] = entry.Value;
                }
            }

            Dictionary<string, PlantData> crops = new();
            foreach (var entry in definitions)
            {
                PlantData? plant = BuildCrop(entry.Key, entry.Value);
                if (plant != null)
                {
                    crops.TryAdd(entry.Key, plant);
                }
            }
            Monitor?.Log($"Manual crops loaded: {crops.Count}", LogLevel.Debug);
            return crops;
        }

        /// <summary>
        /// Builds a single crop from a manual definition. Logs a warning and returns null when the harvest item is invalid or the growth time isn't positive.
        /// </summary>
        /// <param name="seedItemId"> Seed item id, qualified or not. If it doesn't resolve, the harvest item is used as the seed. </param>
        /// <param name="definition"> The crop definition. </param>
        /// <returns> The built crop, or null if the definition is invalid. </returns>
        public static PlantData? BuildCrop(string seedItemId, ManualCropDefinition definition)
        {
            var Monitor = Container.Instance.GetInstance<IMonitor>(ModEntry.UniqueID);

            Item? harvest = ResolveItem(definition.HarvestID);
            if (harvest == null)
            {
                Monitor?.Log($"Manual crop '{seedItemId}' skipped: harvest item '{definition.HarvestID}' is invalid.", LogLevel.Warn);
                return null;
            }
            if (definition.GrowthTime <= 0)
            {
                Monitor?.Log($"Manual crop '{seedItemId}' skipped: growth time must be positive (got {definition.GrowthTime}).", LogLevel.Warn);
                return null;
            }

            if (definition.SalePrice > 0 && harvest is SObject harvestObject)
            {
                harvestObject.Price = definition.SalePrice;
            }

            Item seed = ResolveItem(seedItemId) ?? harvest;

            int minStack = Math.Max(1, definition.MinimumHarvests);
            int maxStack = Math.Max(minStack, definition.MaximumHarvests);
            SCropData cropData = new()
            {
                DaysInPhase = new List<int> { definition.GrowthTime },
                RegrowDays = definition.RegrowthTime <= 0 ? -1 : definition.RegrowthTime,
                HarvestItemId = harvest.ItemId,
                HarvestMinStack = minStack,
                HarvestMaxStack = maxStack,
                HarvestMaxIncreasePerFarmingLevel = definition.MaxHarvestsIncreasePerFarmingLevel,
                ExtraHarvestChance = definition.ExtraHarvestChance,
                Seasons = ParseSeasons(definition.Seasons),
                IsPaddyCrop = definition.IsPaddyCrop
            };

            DropInformation dropInformation = new(seedItemId, new List<Item> { harvest }, new List<int> { 1 }, new List<double> { 1 });
            CropData crop = new(cropData, seed, dropInformation, definition.HasQuality, definition.AcceptsFertilizer);

            if (!string.IsNullOrWhiteSpace(definition.Name))
            {
                crop.DisplayName = definition.Name;
            }

            var registry = Container.Instance.GetInstance<ManualCropRegistry>(ModEntry.UniqueID);
            if (registry != null && registry.TryGetSeedPrice(seedItemId, out int overridePrice))
            {
                crop.SeedPrice = overridePrice;
            }
            else if (definition.PurchasePrice > 0)
            {
                crop.SeedPrice = definition.PurchasePrice;
            }

            return crop;
        }

        /// <summary>
        /// Resolves an item id leniently: qualified ids are used as given, bare ids are tried as objects (<c>(O)</c>) first.
        /// </summary>
        /// <param name="id"> The item id. </param>
        /// <returns> The item, or null if it doesn't resolve. </returns>
        private static Item? ResolveItem(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;
            id = id.Trim();
            try
            {
                if (id.StartsWith('('))
                    return ItemRegistry.Create(id, allowNull: true);
                return ItemRegistry.Create("(O)" + id, allowNull: true) ?? ItemRegistry.Create(id, allowNull: true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Parses a space-separated, case-insensitive list of seasons. Unknown tokens are ignored.
        /// </summary>
        /// <param name="seasons"> The seasons string, for example <c>"spring summer"</c>. </param>
        /// <returns> The parsed seasons. </returns>
        private static List<Season> ParseSeasons(string? seasons)
        {
            List<Season> result = new();
            if (string.IsNullOrWhiteSpace(seasons))
                return result;
            foreach (string token in seasons.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Enum.TryParse(token, true, out Season season) && Enum.IsDefined(season) && !result.Contains(season))
                {
                    result.Add(season);
                }
            }
            return result;
        }
    }
}
