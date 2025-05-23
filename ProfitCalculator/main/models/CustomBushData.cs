using ProfitCalculator.main.memory;
using Microsoft.Xna.Framework.Graphics;
using ProfitCalculator.apis;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using static ProfitCalculator.Utils;
using SObject = StardewValley.Object;

#nullable enable
#pragma warning disable

namespace ProfitCalculator.main.models
{
    /// <summary>
    /// Class <c>CropDataExpanded</c> models a crop from the game storing all relevant information about it.
    /// </summary>
    public class CustomBushData : PlantData
    {
        /// <summary>
        /// List of fruits that the crop can drop.
        /// </summary>
        public List<ICustomBushDrop> Drops { get; set; }

        public int DaysToBeginProducing { get; private set; }
        public Item Item { get; private set; }

        /// <inheritdoc/>
        public override int Price(UtilsSeason season)
        {
            if (season == UtilsSeason.Greenhouse)
            {
                return (int)(from fruit in Drops
                             let price = PriceFromObjectID(fruit.ItemId) * fruit.Chance
                             select price).DefaultIfEmpty(0).Sum();
            }
            return (int)(from fruit in Drops
                         where fruit.Season == SeasonFromUtilsSeason(season)
                         let price = PriceFromObjectID(fruit.ItemId) * fruit.Chance
                         select price).DefaultIfEmpty(0).Sum();
        }

        /// <summary>
        /// Constructor for <c>CropDataExpanded</c> class. It's used to create a new instance of the class.
        /// </summary>
        /// <param name="_cropData">Crop's full Data</param>
        /// <param name="_drops">List of fruits</param>
        /// <param name="_seed" >Seed Item</param>
        public CustomBushData(ICustomBush _cropData, List<ICustomBushDrop> _drops, Item _seed)
            : base(
                  _cropData.AgeToProduce,
                  1,
                  1,
                  1,
                  0f,
                  0f,
                  _cropData.DisplayName,
                  _cropData.Seasons,
                  _seed,
                  false,
                  false,
                  new()
                  )
        {
            DaysToBeginProducing = _cropData.DayToBeginProducing;
            Drops = _drops;
            Item = _seed;
        }

        #region Growth Values Calculations

        /// <summary>
        /// Calculates the average growth speed value for the crop.
        /// It's calculated by adding fertilizer modifiers to 1.0f and finally adding 0.25f if the crop is a paddy crop and 0.1f if the player has the agriculturist profession.
        /// </summary>
        /// <param name="fertilizerQuality"> Quality of the used Fertilizer</param>
        /// <returns> Average growth speed value for the crop. <c>float</c></returns>
        public float GetAverageGrowthSpeedValueForCrop(FertilizerQuality fertilizerQuality)
        {
            float speedIncreaseModifier = 0.0f;
            if (!AffectByFertilizer)
            {
                speedIncreaseModifier = 1.0f;
            }
            else if ((int)fertilizerQuality == -1)
            {
                speedIncreaseModifier += 0.1f;
            }
            else if ((int)fertilizerQuality == -2)
            {
                speedIncreaseModifier += 0.25f;
            }
            else if ((int)fertilizerQuality == -3)
            {
                speedIncreaseModifier += 0.33f;
            }
            if (Game1.player.professions.Contains(Farmer.agriculturist))
            {
                speedIncreaseModifier += 0.1f;
            }
            return speedIncreaseModifier;
        }

        /// <summary>
        /// Checks whether the crop is available for the current Season.
        /// </summary>
        /// <param name="currentSeason"></param>
        /// <returns> Whether the crop is available for the current Season or not.</returns>
        public bool IsAvailableForCurrentSeason(UtilsSeason currentSeason)
        {
            //match UtilsSeason with the crop's seasons from StardewValley, case insensitive
            int seasonNum = (int)currentSeason;
            try
            {
                return Seasons.Contains((Season)seasonNum);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the total available days for planting and harvesting the crop. Depends on which seasons the crop can grow.
        /// </summary>
        /// <param name="currentSeason">Current Season of type UtilsSeason <see cref="UtilsSeason"/></param>
        /// <param name="day">Current day as int, can be from 0 to 1</param>
        /// <returns> Total available days for planting and harvesting the crop. <c>int</c></returns>
        public int TotalAvailableDays(UtilsSeason currentSeason, int day)
        {
            int totalAvailableDays = 0;
            if (IsAvailableForCurrentSeason(currentSeason))
            {
                //Each Season has 28 days,
                //get index of current Season
                int seasonIndex = (int)currentSeason;
                //iterate over the array and add the number of days for each Season that is later than the current Season
                for (int i = seasonIndex + 1; i < Seasons.Count; i++)
                {
                    totalAvailableDays += 28;
                }
                //add the number of days in the current Season
                totalAvailableDays += TotalAvailableDaysInCurrentSeason(day);
            }
            if (currentSeason == UtilsSeason.Greenhouse)
            {
                totalAvailableDays = 28 * 4;
            }
            return totalAvailableDays;
        }

        /// <summary>
        /// Returns the total available days for planting and harvesting the crop for the current Season. Depends on which seasons the crop can grow.
        /// </summary>
        /// <param name="day">Current day as int, can be from 0 to 1</param>
        /// <returns>Total available days for planting and harvesting the crop in current Season. <c>int</c></returns>
        public static int TotalAvailableDaysInCurrentSeason(int day)
        {
            return 28 - day;
        }

        /// <summary>
        /// Returns the total harvests for the crop for the available time. Depends on which seasons the crop can grow, the current day , and the fertilizer quality.
        /// </summary>
        /// <param name="currentSeason"> Current Season of type UtilsSeason <see cref="UtilsSeason"/></param>
        /// <param name="fertilizerQuality"> Quality of the used Fertilizer of type FertilizerQuality <see cref="FertilizerQuality"/></param>
        /// <param name="day"> Current day as int, can be from 0 to 1</param>
        /// <returns> Total number of harvests for the crop for the available time. <c>int</c></returns>
        public int TotalHarvestsWithRemainingDays(UtilsSeason currentSeason, FertilizerQuality fertilizerQuality, int day)
        {
            int totalHarvestTimes = 0;
            int daysToRegrow = RegrowDays;
            int availableDays = AvailableGrowingDays(currentSeason, day);
            if (IsAvailableForCurrentSeason(currentSeason) || currentSeason == UtilsSeason.Greenhouse)
            {
                //number of harvests are the number of available days divided by the number of days to grow the crop
                totalHarvestTimes = availableDays / daysToRegrow;
            }
            return totalHarvestTimes;
        }

        private int AvailableGrowingDays(UtilsSeason currentSeason, int day)
        {
            //Tree growth works like this
            //tree takes Day days to grow
            //tree takes RegrowDays to regrow
            //tree can only produce after DaysToBeginProducing of each season so if DaysToBeginProducing is 1 then it will produce on the second day of the season and every RegrowDays after that, repeats for each season
            //calculate the number of days available for growing the tree in all seasons if planted on day day
            int availableDays = 0;
            int daysToBeginProducing = DaysToBeginProducing;
            int daysToGrow = Days;

            //for the first season there's two conditions 1)
            int season1Days = 28 - daysToGrow - day;
            int season1DaysToBeginProducing = 28 - daysToBeginProducing;
            if (season1Days >= season1DaysToBeginProducing)
            {
                availableDays = season1Days;
            }
            else if (season1Days > 0)
            {
                availableDays = season1DaysToBeginProducing;
            }
            if (currentSeason == UtilsSeason.Greenhouse)
            {
                return availableDays + (3 * (28 - daysToBeginProducing));
            }
            for (int i = 1; i < Seasons.Count; i++)
            {
                availableDays += 28 - daysToBeginProducing;
            }
            return availableDays;
        }

        /// <summary>
        /// How many extra crops can be harvested from the crop. Depends on farming level and extra per level defined. Currently Unused
        /// </summary>
        /// <returns> Number of extra crops that can be harvested from the crop. <c>int</c></returns>
        public int ExtraCropsFromFarmingLevel()
        {
            //TODO: Actually use this

            double totalCrops = MinHarvests;
            if (MinHarvests > 1 || MaxHarvests > 1)
            {
                int max_harvest_increase = 0;
                if (MaxHarvestIncreasePerFarmingLevel > 0)
                {
                    max_harvest_increase = (int)(Game1.player.FarmingLevel / MaxHarvestIncreasePerFarmingLevel);
                }
                totalCrops = (MinHarvests + MaxHarvests + max_harvest_increase) / 2.0;
            }
            return (int)totalCrops;
        }

        /// <summary>
        /// Meant to calculate the average extra crops from luck if any. Currently Unused
        /// </summary>
        /// <returns> Average extra crops from luck. <c>double</c></returns>
        public double AverageExtraCropsFromRandomness()
        {
            //TODO: Verify this is correct

            return 0f;
        }

        #endregion Growth Values Calculations

        #region Crop Profit Calculations

        /// <inheritdoc/>
        public double TotalCropProfit()
        {
            UtilsSeason season = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.Season ?? UtilsSeason.Spring;
            FertilizerQuality fertilizerQuality = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.FertilizerQuality ?? FertilizerQuality.None;
            uint day = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.Day ?? 0;

            double totalProfitFromFirstProduce = Price(season);
            double result = totalProfitFromFirstProduce * TotalHarvestsWithRemainingDays(season, fertilizerQuality, (int)day);

            return result;
        }

        /// <inheritdoc/>
        public double TotalCropProfitPerDay()
        {
            UtilsSeason season = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.Season ?? UtilsSeason.Spring;
            uint day = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.Day ?? 0;
            double totalProfit = TotalCropProfit();
            if (totalProfit == 0)
            {
                return 0;
            }
            double totalCropProfitPerDay = totalProfit / TotalAvailableDays(season, (int)day);
            return totalCropProfitPerDay;
        }

        /// <inheritdoc/>
        public int TotalFertilizerNeeded()
        {
            UtilsSeason season = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.Season ?? UtilsSeason.Spring;
            uint day = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.Day ?? 0;
            if (season == UtilsSeason.Greenhouse || Seasons.Count == 1)
                return 1;
            else
            {
                return (int)Math.Ceiling(TotalAvailableDays(season, (int)day) / 28.0);
            }
        }

        /// <inheritdoc/>
        public int TotalFertilizerCost()
        {
            bool payForFertilizer = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.PayForFertilizer ?? false;
            FertilizerQuality fertilizerQuality = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.FertilizerQuality ?? FertilizerQuality.None;
            if (!payForFertilizer)
            {
                return 0;
            }
            int fertNeeded = TotalFertilizerNeeded();
            int fertCost = FertilizerPrices(fertilizerQuality);
            return fertNeeded * fertCost;
        }

        /// <inheritdoc/>
        public double TotalFertilzerCostPerDay()
        {
            UtilsSeason season = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.Season ?? UtilsSeason.Spring;
            uint day = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.Day ?? 0;
            int fertCost = TotalFertilizerCost();
            if (fertCost == 0)
            {
                return 0;
            }
            double totalFertilizerCostPerDay = fertCost / (double)TotalAvailableDays(season, (int)day);
            return totalFertilizerCostPerDay;
        }

        /// <inheritdoc/>
        public int TotalSeedsNeeded()
        {
            UtilsSeason season = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.Season ?? UtilsSeason.Spring;
            uint day = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.Day ?? 0;
            FertilizerQuality fertilizerQuality = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.FertilizerQuality ?? FertilizerQuality.None;
            if (RegrowDays > 0 && TotalAvailableDays(season, (int)day) > 0)
                return 1;
            else return TotalHarvestsWithRemainingDays(season, fertilizerQuality, (int)day);
        }

        /// <inheritdoc/>
        public int TotalSeedsCost()
        {
            bool payForSeeds = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.PayForSeeds ?? false;
            if (!payForSeeds)
                return 0;
            int seedsNeeded = TotalSeedsNeeded();
            int seedCost = SeedPrice;

            return seedsNeeded * seedCost;
        }

        /// <inheritdoc/>
        public double TotalSeedsCostPerDay()
        {
            UtilsSeason season = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.Season ?? UtilsSeason.Spring;
            uint day = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.Day ?? 0;
            int seedCost = TotalSeedsCost();
            if (seedCost == 0)
            {
                return 0;
            }
            double totalSeedsCostPerDay = seedCost / (double)TotalAvailableDays(season, (int)day);
            return totalSeedsCostPerDay;
        }

        #endregion Crop Profit Calculations

        #region Crop Modifer Value Calculations

        /// <inheritdoc/>
        public double GetAverageValueMultiplierForCrop()
        {
            double[] priceMultipliers = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.PriceMultipliers ?? new double[] { 1.0, 1.25, 1.5, 2.0 };

            //apply farm level quality modifiers
            double chanceForGoldQuality = GetCropGoldQualityChance();
            double chanceForSilverQuality = GetCropSilverQualityChance();
            double chanceForIridiumQuality = GetCropIridiumQualityChance();
            double chanceForBaseQuality = GetCropBaseQualityChance();
            //calculate average value modifier for price
            double averageValue = 0f;
            averageValue += chanceForBaseQuality * priceMultipliers[0];
            averageValue += chanceForSilverQuality * priceMultipliers[1];
            averageValue += chanceForGoldQuality * priceMultipliers[2];
            averageValue += chanceForIridiumQuality * priceMultipliers[3];
            return averageValue;
        }

        /// <inheritdoc/>
        public double GetAverageValueForCropAfterModifiers()
        {
            bool UseBaseStats = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.UseBaseStats ?? false;
            double averageValue = GetAverageValueMultiplierForCrop();
            if (!UseBaseStats && Game1.player.professions.Contains(Farmer.tiller))
            {
                averageValue *= 1.1f;
            }
            return Math.Round(averageValue, 2);
        }

        /// <inheritdoc/>
        public double GetCropBaseGoldQualityChance(double limit = 9999999999)
        {
            return 0f;
        }

        /// <inheritdoc/>
        public double GetCropBaseQualityChance()
        {
            return 1f;
        }

        /// <inheritdoc/>
        public double GetCropSilverQualityChance()
        {
            return 0f;
        }

        /// <inheritdoc/>
        public double GetCropGoldQualityChance()
        {
            return 0f;
        }

        /// <inheritdoc/>
        public double GetCropIridiumQualityChance()
        {
            return 0f;
        }

        #endregion Crop Modifer Value Calculations
    }
}

#pragma warning enable