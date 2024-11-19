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
    public class TreeData : IPlantData
    {
        /// <inheritdoc/>
        public DropInformation DropInformation { get; set; }

        /// <inheritdoc/>
        public Item Seed { get; set; }

        /// <inheritdoc/>
        public bool AffectByQuality { get; set; }

        /// <inheritdoc/>
        public bool AffectByFertilizer { get; set; }

        /// <inheritdoc/>
        public int Days { get; set; }

        /// <inheritdoc/>
        public int RegrowDays { get; set; }

        /// <inheritdoc/>
        public int MinHarvests { get; set; }

        /// <inheritdoc/>
        public int MaxHarvests { get; set; }

        /// <inheritdoc/>
        public float MaxHarvestIncreasePerFarmingLevel { get; set; }

        /// <inheritdoc/>
        public double ChanceForExtraCrops { get; set; }

        /// <inheritdoc/>
        public string DisplayName { get; set; }

        /// <inheritdoc/>
        public Tuple<Texture2D, Rectangle> Sprite { get; set; }

        /// <inheritdoc/>
        public bool IsPaddyCrop { get; set; }

        /// <inheritdoc/>
        public List<Season> Seasons { get; set; }

        /// <inheritdoc/>
        public int SeedPrice
        {
            get => Container.Instance.GetInstance<ShopAccessor>(ModEntry.UniqueID)?.GetCheapestSeedPrice(Seed.QualifiedItemId) ?? 0;
            set => throw new NotImplementedException();
        }

        /// <summary>
        /// Constructor for <c>CropDataExpanded</c> class. It's used to create a new instance of the class.
        /// </summary>
        /// <param name="_cropData">Crop's full Data</param>
        /// <param name="_seed" >Seed Item</param>
        /// <param name="dropInformation">Drop Information for the crop</param>
        public TreeData(FruitTreeData _cropData, Item _seed, DropInformation dropInformation)
        {
            Days = 28;
            RegrowDays = 1;
            MinHarvests = 1;
            MaxHarvests = 1;
            MaxHarvestIncreasePerFarmingLevel = 0f;
            ChanceForExtraCrops = 0f;
            IsPaddyCrop = false;
            Seasons = _cropData.Seasons;
            Item item = dropInformation.Drops[0].Item;
            DisplayName = item.DisplayName;
            Seed = _seed;
            AffectByQuality = false;
            AffectByFertilizer = false;

            DropInformation = dropInformation;

            Texture2D spriteSheet;
            try
            {
                spriteSheet = ItemRegistry.GetData(item.itemId.Value).GetTexture();
            }
            catch (Exception e)
            {
                Container.Instance.GetInstance<IMonitor>(ModEntry.UniqueID)?.Log($"Error loading sprite for {item.DisplayName}: {e.Message}", LogLevel.Error);
                spriteSheet = Game1.objectSpriteSheet;
            }

            Sprite = new(
                spriteSheet,
                Game1.getSourceRectForStandardTileSheet(
                    spriteSheet,
                    item.ParentSheetIndex,
                    SObject.spriteSheetTileSize,
                    SObject.spriteSheetTileSize
                    )
                );
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
            return 0.0f;
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
            int totalAvailableDays = TotalAvailableDays(currentSeason, day);
            int daysToRegrow = RegrowDays;
            int growingDays = 0;
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

        /// <summary>
        /// How many extra crops can be harvested from the crop. Depends on farming level and extra per level defined. Currently Unused
        /// </summary>
        /// <returns> Number of extra crops that can be harvested from the crop. <c>int</c></returns>
        public int ExtraCropsFromFarmingLevel()
        {
            return 0;
        }

        /// <summary>
        /// Meant to calculate the average extra crops from luck if any. Currently Unused
        /// </summary>
        /// <returns> Average extra crops from luck. <c>double</c></returns>
        public double AverageExtraCropsFromRandomness()
        {
            return 0f;
        }

        #endregion Growth Values Calculations

        #region Crop Profit Calculations

        /// <inheritdoc/>
        public double TotalCropProfit()
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
        public double TotalCropProfitPerDay()
        {
            UtilsSeason season = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.Season ?? UtilsSeason.Spring;
            uint day = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.Day ?? 0;
            double totalProfit = TotalCropProfit();
            if (Math.Abs(totalProfit) < 0.0001)
                return 0;
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