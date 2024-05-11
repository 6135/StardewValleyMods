using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.GameData.Crops;
using System;
using static ProfitCalculator.Utils;
using CropDataExpanded = ProfitCalculator.main.CropDataExpanded;

#nullable enable

namespace ProfitCalculator
{
    /// <summary>
    /// API for the Profit Calculator mod.
    /// </summary>
    public interface IProfitCalculatorApi
    {
        /// <summary>
        /// Adds a crop to the Profit Calculator.
        /// </summary>
        void AddCrop
            (
                CropData cropData,
                Item item,
                Item seeds,
                bool affectByQuality = true,
                bool affectByFertilizer = true
            );
    }

    /// <summary>
    /// API for the Profit Calculator mod.
    /// </summary>
    public class ModApi : IProfitCalculatorApi
    {
        private readonly ModEntry Mod;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModApi"/> class.
        /// </summary>
        /// <param name="mod"></param>
        public ModApi(ModEntry mod)
        {
            this.Mod = mod;
        }

        /// <inheritdoc/>
        public void AddCrop(
                CropData cropData,
                Item item,
                Item seeds,
                bool affectByQuality = true,
                bool affectByFertilizer = true
            )
        {
            //convert string[] to UtilsSeason[]
            UtilsSeason[] seasonsEnum = new UtilsSeason[cropData.Seasons.Count];

            for (int i = 0; i < cropData.Seasons.Count; i++)
            {
                seasonsEnum[i] = (UtilsSeason)Enum.Parse(typeof(UtilsSeason), cropData.Seasons[i].ToString(), true);
            }
            CropDataExpanded crop = new(cropData, item, seeds, -1, affectByQuality, affectByFertilizer);
            Mod.AddCrop(cropData.HarvestItemId, crop);
        }
    }
}