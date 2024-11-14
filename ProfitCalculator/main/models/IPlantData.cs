using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using System;
using System.Collections.Generic;
using static CoreUtils.Utils;

#nullable enable

namespace ProfitCalculator.main.models
{
    /// <summary>
    /// Class <c>CropDataExpanded</c> models a crop from the game storing all relevant information about it.
    /// </summary>
    public interface IPlantData
    {
        /// <value>Property <c>Seed</c> represents the Seed of the crop.</value>
        public Item Seed { get; }

        /// <value>Property <c>dropInformation</c> represents the drop information of the Plant data.</value>
        public DropInformation DropInformation { get; set; }

        /// <value>Property <c></c> represents the Seed of the crop.</value>

        /// <value>Property <c>affectByQuality</c> represents whether the crop is affected by fertilizer quality or not. Some crops like Tea aren't affected by this. </value>
        public bool AffectByQuality { get; set; }

        /// <value>Property <c>affectByFertilizer</c> represents whether the crop is affected by fertilizer or not.</value>
        public bool AffectByFertilizer { get; set; }

        /// <value>Property <c>Price</c> represents the price of the crop. Without Shop Modifiers </value>
        public int SeedPrice
        {
            get; set;
        }

        /// <value>Property <c>Days</c> represents the crop's total days to grow excluding <see cref="RegrowDays"/>.</value>
        public int Days { get; set; }

        public int RegrowDays { get; set; }

        /// <value>Property <c>MinHarvests</c> represents the crop's minimum drops.</value>
        public int MinHarvests { get; set; }

        /// <value>Property <c>MaxHarvests</c> represents the crop's maximum drops.</value>
        public int MaxHarvests { get; set; }

        /// <value>Property <c>MaxHarvestIncreasePerFarmingLevel</c> represents the crop's maximum drops increase per farming level.</value>
        public float MaxHarvestIncreasePerFarmingLevel { get; set; }

        /// <value>Property <c>ChanceForExtraCrops</c> represents the crop's chance for extra crops.</value>
        public double ChanceForExtraCrops { get; set; }

        /// <value>Property <c>DisplayName</c> represents the crop's name.</value>
        public string DisplayName { get; set; }

        /// <value>Property <c>Sprite</c> represents the crop's sprite. It's unused as of now.</value>
        public Tuple<Texture2D, Rectangle> Sprite { get; set; }

        /// <value>Property <c>Seasons</c> available seasons.</value>
        public List<Season> Seasons { get; set; }

        #region Growth Values Calculations

        /// <summary>
        /// Calculates the average growth speed value for the crop.
        /// It's calculated by adding fertilizer modifiers to 1.0f and finally adding 0.25f if the crop is a paddy crop and 0.1f if the player has the agriculturist profession.
        /// </summary>
        /// <param name="fertilizerQuality"> Quality of the used Fertilizer</param>
        /// <returns> Average growth speed value for the crop. <c>float</c></returns>
        public float GetAverageGrowthSpeedValueForCrop(FertilizerQuality fertilizerQuality);

        /// <summary>
        /// Checks whether the crop is available for the current Season.
        /// </summary>
        /// <param name="currentSeason"></param>
        /// <returns> Whether the crop is available for the current Season or not.</returns>
        public bool IsAvailableForCurrentSeason(UtilsSeason currentSeason);

        /// <summary>
        /// Returns the total available days for planting and harvesting the crop. Depends on which seasons the crop can grow.
        /// </summary>
        /// <param name="currentSeason">Current Season of type UtilsSeason <see cref="UtilsSeason"/></param>
        /// <param name="day">Current day as int, can be from 0 to 1</param>
        /// <returns> Total available days for planting and harvesting the crop. <c>int</c></returns>
        public int TotalAvailableDays(UtilsSeason currentSeason, int day);

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
        public int TotalHarvestsWithRemainingDays(UtilsSeason currentSeason, FertilizerQuality fertilizerQuality, int day);

        /// <summary>
        /// How many extra crops can be harvested from the crop. Depends on farming level and extra per level defined. Currently Unused
        /// </summary>
        /// <returns> Number of extra crops that can be harvested from the crop. <c>int</c></returns>
        public int ExtraCropsFromFarmingLevel();

        /// <summary>
        /// Meant to calculate the average extra crops from luck if any. Currently Unused
        /// </summary>
        /// <returns> Average extra crops from luck. <c>double</c></returns>
        public double AverageExtraCropsFromRandomness();

        #endregion Growth Values Calculations

        #region Crop Profit Calculations

        /// <summary>
        /// Calculates the total profit for a crop. See <see cref="GetAverageValueForCropAfterModifiers"/>, <see cref="IPlantData.AverageExtraCropsFromRandomness"/>, <see cref="IPlantData.TotalHarvestsWithRemainingDays"/> for more information.
        /// </summary>
        /// <returns> Total profit for the crop </returns>
        public double TotalCropProfit();

        /// <summary>
        /// Calculates the total profit per day for a crop. See <see cref="TotalCropProfit"/> for more information. Simply devides the total profit by the total available days for the crop.
        /// </summary>
        /// <returns> Total profit per day for the crop </returns>
        public double TotalCropProfitPerDay();

        /// <summary>
        /// Total fertilizer needed for a crop. If planted in greenhouse or if the crop only grows in one Season, then only 1 fertilizer is needed. Otherwise, the total number of days the crop is available is divided by 28 and rounded up to get the total number of fertilizer needed.
        /// </summary>
        /// <returns> Total fertilizer needed for the crop </returns>
        public int TotalFertilizerNeeded();

        /// <summary>
        /// Total fertilizer cost for a crop. See <see cref="TotalFertilizerNeeded"/> and <see cref="Utils.FertilizerPrices(FertilizerQuality)"/> for more information.
        /// </summary>
        /// <returns> Total fertilizer cost for the crop </returns>
        public int TotalFertilizerCost();

        /// <summary>
        /// Total fertilizer cost per day for a crop. See <see cref="TotalFertilizerCost"/> for more information. Simply devides the total fertilizer cost by the total available days for the crop.
        /// </summary>
        /// <returns> Total fertilizer cost per day for the crop </returns>
        public double TotalFertilzerCostPerDay();

        /// <summary>
        /// Total seeds needed for a crop. If the crop regrows, then only 1 seed is needed. Otherwise, the total number of harvests is calculated and multiplied by the number of seeds needed per harvest.
        /// </summary>
        /// <returns> Total seeds needed for the crop </returns>
        public int TotalSeedsNeeded();

        /// <summary>
        /// Total seeds cost for a crop. See <see cref="TotalSeedsNeeded"/>
        /// </summary>
        /// <returns> Total seeds cost for the crop </returns>
        public int TotalSeedsCost();

        /// <summary>
        /// Total seeds cost per day for a crop. See <see cref="TotalSeedsCost"/> for more information. Simply devides the total seeds cost by the total available days for the crop.
        /// </summary>
        /// <returns></returns>
        public double TotalSeedsCostPerDay();

        #endregion Crop Profit Calculations

        #region Crop Modifer Value Calculations

        /// <summary>
        /// Prints the average crop value modifier for current farming level and fertilizer type. Used for debugging. Uses <see cref="GetCropGoldQualityChance"/>, <see cref="GetCropSilverQualityChance"/>, <see cref="GetCropIridiumQualityChance"/>, <see cref="GetCropBaseQualityChance"/>. <see cref="GetCropBaseQualityChance"/> and PriceMultipliers to calculate the average value modifier for the crop.
        /// </summary>
        /// <returns> Average crop value modifier for current farming level and fertilizer type </returns>
        public double GetAverageValueMultiplierForCrop();

        /// <summary>
        /// Calculates the average crop value modifier applying relevant skill modifiers. See <see cref="GetAverageValueMultiplierForCrop"/> for more information.
        /// </summary>
        /// <returns> Average crop value modifier applying relevant skill modifiers </returns>
        public double GetAverageValueForCropAfterModifiers();

        /// <summary>
        /// Calculates the base chance for gold quality. See <see cref="StardewValley.Crop.harvest"/> for more information.
        /// </summary>
        /// <param name="limit"> Limit for the chance</param>
        /// <returns> Base chance for gold quality </returns>
        public double GetCropBaseGoldQualityChance(double limit = 9999999999);

        /// <summary>
        /// Calculates the chance for normal quality. See <see cref="StardewValley.Crop.harvest"/> for more information.
        /// </summary>
        /// <returns> Chance for normal quality </returns>
        public double GetCropBaseQualityChance();

        /// <summary>
        /// Calculates the chance for silver quality. See <see cref="StardewValley.Crop.harvest"/> for more information.
        /// </summary>
        /// <returns> Chance for silver quality </returns>
        public double GetCropSilverQualityChance();

        /// <summary>
        /// Calculates the chance for gold quality. See <see cref="StardewValley.Crop.harvest"/> for more information.
        /// </summary>
        /// <returns> Chance for gold quality </returns>
        public double GetCropGoldQualityChance();

        /// <summary>
        /// Calculates the chance for iridium quality. See <see cref="StardewValley.Crop.harvest"/> for more information.
        /// </summary>
        /// <returns> Chance for iridium quality </returns>
        public double GetCropIridiumQualityChance();

        #endregion Crop Modifer Value Calculations
    }
}