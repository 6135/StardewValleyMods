using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProfitCalculator.main.accessors;
using ProfitCalculator.main.memory;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.FruitTrees;
using System;
using System.Collections.Generic;
using static ProfitCalculator.Utils;
using SObject = StardewValley.Object;

#nullable enable

namespace ProfitCalculator.main.models
{
    /// <summary>
    /// Class <c>CropDataExpanded</c> models a crop from the game storing all relevant information about it.
    /// </summary>
    public class TreeData : PlantData
    {
        /// <summary>
        /// Constructor for <c>CropDataExpanded</c> class. It's used to create a new instance of the class.
        /// </summary>
        /// <param name="_cropData">Crop's full Data</param>
        /// <param name="_seed" >Seed Item</param>
        /// <param name="dropInformation">Drop Information for the crop</param>
        public TreeData(FruitTreeData _cropData, Item _seed, DropInformation dropInformation)
            : base(
                  28,
                  1,
                  1,
                  1,
                  0f,
                  0f,
                  dropInformation.Drops[0].Item.DisplayName,
                  _cropData.Seasons,
                  _seed,
                  false,
                  false,
                  dropInformation
                  )
        {
        }

        /// <summary>
        /// Returns the total harvests for the crop for the available time. Depends on which seasons the crop can grow, the current day , and the fertilizer quality.
        /// </summary>
        /// <param name="currentSeason"> Current Season of type UtilsSeason <see cref="UtilsSeason"/></param>
        /// <param name="fertilizerQuality"> Quality of the used Fertilizer of type FertilizerQuality <see cref="FertilizerQuality"/></param>
        /// <param name="day"> Current day as int, can be from 0 to 1</param>
        /// <returns> Total number of harvests for the crop for the available time. <c>int</c></returns>
        public override int TotalHarvestsWithRemainingDays(UtilsSeason currentSeason, FertilizerQuality fertilizerQuality, int day)
        {
            int totalHarvestTimes = 0;
            int totalAvailableDays = TotalAvailableDays(currentSeason, day);
            int daysToRegrow = RegrowDays;
            const int growingDays = 0;
            if (IsAvailableForCurrentSeason(currentSeason) || currentSeason == UtilsSeason.Greenhouse)
            {
                if (totalAvailableDays < growingDays)
                    return 0;
                //if the crop regrows, then the total harvest times are 1 for the first harvest and then the number of times it can regrow in the remaining days. We always need to subtract one to account for the day lost in the planting day.
                if (daysToRegrow > 0)
                {
                    totalHarvestTimes = (int)(1 + ((totalAvailableDays - growingDays) / (double)daysToRegrow));
                }
                else
                    totalHarvestTimes = totalAvailableDays / growingDays;
            }
            return totalHarvestTimes;
        }

        /// <inheritdoc/>
        public override int ExtraCropsFromFarmingLevel()
        {
            return 0;
        }

        /// <inheritdoc/>
        public override double TotalCropProfit()
        {
            UtilsSeason season = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.Season ?? UtilsSeason.Spring;
            bool UseBaseStats = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.UseBaseStats ?? false;
            FertilizerQuality fertilizerQuality = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.FertilizerQuality ?? FertilizerQuality.None;
            uint day = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.Day ?? 0;
            double totalProfitFromFirstProduce = DropInformation.AveragePrice(season); //Price(season) already returns the average value of produced items for 1 harvest.

            if (!UseBaseStats && Game1.player.professions.Contains(Farmer.tiller))
            {
                totalProfitFromFirstProduce *= 1.1f;
            }
            double result = totalProfitFromFirstProduce * TotalHarvestsWithRemainingDays(season, fertilizerQuality, (int)day);
            return result;
        }

        /// <inheritdoc/>
        public override double GetCropBaseGoldQualityChance(double limit = 9999999999)
        {
            return 0f;
        }

        /// <inheritdoc/>
        public override double GetCropBaseQualityChance()
        {
            return 1f;
        }

        /// <inheritdoc/>
        public override double GetCropSilverQualityChance()
        {
            return 0f;
        }

        /// <inheritdoc/>
        public override double GetCropGoldQualityChance()
        {
            return 0f;
        }

        /// <inheritdoc/>
        public override double GetCropIridiumQualityChance()
        {
            return 0f;
        }
    }
}