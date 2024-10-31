﻿using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Crops;
using System.Collections.Generic;
using static ProfitCalculator.Utils;
using SObject = StardewValley.Object;
using PlantData = ProfitCalculator.main.CropData;
using ProfitCalculator.main.models;

#nullable enable

namespace ProfitCalculator.main.builders
{
    /// <summary>
    /// Parses the vanilla crops from the game files. Also parses crops from the ManualCrops.json file.
    /// </summary>
    internal class CropBuilder : IDataBuilder
    {
        /// <inheritdoc/>
        /// <summary>
        /// Builds a dictionary of crops from the game files. Accesses the crops from the game files (@"Data\Crops) and parses them into a dictionary.
        /// </summary>
        /// <returns> A dictionary of crops. </returns>
        public virtual Dictionary<string, IPlantData> BuildCrops()
        {
            Dictionary<string, StardewValley.GameData.Crops.CropData> loadedCrops = DataLoader.Crops(Game1.content);
            Dictionary<string, IPlantData> crops = new();
            var Monitor = Container.Instance.GetInstance<IMonitor>();
            Monitor?.Log($"Crops loaded: {loadedCrops.Count}", LogLevel.Debug);
            foreach (var crop in loadedCrops)
            {
                IPlantData? cropData = BuildCrop(crop.Value, crop.Key);
                if (cropData != null)
                {
                    crops.TryAdd(crop.Key, cropData);
                }
            }
            return crops;
        }

        /// <summary>
        /// Builds a crop from the given data. The data is split by the '/' character. The data is then parsed into a crop. The crop is then returned.  Thanks to Klhoe Leclair for this code.
        ///
        /// </summary>
        /// <param name="cropData"> The data of the crop. </param>
        /// <param name="id"> The id of the crop. </param>
        /// <returns> The crop that was built. </returns>
        private static IPlantData? BuildCrop(StardewValley.GameData.Crops.CropData cropData, string id)
        {
            Item seed = new SObject(id, 1);
            Item item = new SObject(cropData.HarvestItemId == "23" ? id : cropData.HarvestItemId, 1);
            return new CropData(cropData, item, seed);
        }
    }
}