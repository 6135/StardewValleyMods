using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static ProfitCalculator.Utils;
using SObject = StardewValley.Object;

#nullable enable

namespace ProfitCalculator.main
{
    /// <summary>
    /// Parses the vanilla crops from the game files. Also parses crops from the ManualCrops.json file.
    /// </summary>
    internal class CropBuilder
    {
        /// <inheritdoc/>
        /// <summary>
        /// Builds a dictionary of crops from the game files. Accesses the crops from the game files (@"Data\Crops) and parses them into a dictionary.
        /// </summary>
        /// <returns> A dictionary of crops. </returns>
        public virtual Dictionary<string, CropDataExpanded> BuildCrops()
        {
            Dictionary<string, CropData> loadedCrops = DataLoader.Crops(Game1.content);
            Dictionary<string, CropDataExpanded> crops = new();
            Monitor?.Log($"Crops loaded: {loadedCrops.Count}", LogLevel.Debug);
            foreach (var crop in loadedCrops)
            {
                CropDataExpanded? cropData = BuildCrop(crop.Value, crop.Key);
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
        private static CropDataExpanded? BuildCrop(CropData cropData, string id)
        {
            Item seed = new SObject(id, 1);
            Item item = new SObject(cropData.HarvestItemId == "23" ? id : cropData.HarvestItemId, 1);
            return new(cropData, item, seed);
        }
    }
}