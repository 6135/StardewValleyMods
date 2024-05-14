using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Crops;
using System;
using System.Collections.Immutable;
using System.Linq;
using static ProfitCalculator.Utils;
using SObject = StardewValley.Object;

#nullable enable

namespace ProfitCalculator.main
{
    /// <summary>
    /// Class <c>CropDataExpanded</c> models a crop from the game storing all relevant information about it.
    /// </summary>
    public class CropDataExpanded
    {
        /// <value>Property <c>CropData</c> represents the crop data of the crop.</value>
        public readonly CropData CropData;

        /// <value>Property <c>Seed</c> represents the Seed of the crop.</value>
        public readonly Item Seed;

        /// <value> Property <c>Item></c> represents the harvested item</value>
        public readonly Item Item;

        /// <value>Property <c>affectByQuality</c> represents whether the crop is affected by fertilizer quality or not. Some crops like Tea aren't affected by this. </value>
        public readonly bool AffectByQuality;

        /// <value>Property <c>affectByFertilizer</c> represents whether the crop is affected by fertilizer or not.</value>
        public readonly bool AffectByFertilizer;

        /// <value>Property <c>Price</c> represents the price of the crop. Without Shop Modifiers </value>
        public readonly int SeedPrice;

        /// <summary>
        /// Constructor for <c>CropDataExpanded</c> class. It's used to create a new instance of the class.
        /// </summary>
        /// <param name="_cropData">Crop's full Data</param>
        /// <param name="_item" >Harvested Item</param>
        /// <param name="_seed" >Seed Item</param>
        /// <param name="_affectedByFertilizer" >Whether the crop is affected by fertilizer or not</param>
        /// <param name="_affectedByQuality" >Whether the crop is affected by fertilizer quality or not</param>
        public CropDataExpanded(CropData _cropData, Item _item, Item _seed, bool _affectedByQuality = true, bool _affectedByFertilizer = true)
        {
            CropData = _cropData;
            Seed = _seed;
            Item = _item;
            AffectByQuality = _affectedByQuality;
            AffectByFertilizer = _affectedByFertilizer;
            SeedPrice = Utils.ShopAcessor?.GetCheapestSeedPrice(_seed.QualifiedItemId) ?? 0;

            Texture2D spriteSheet = ItemRegistry.GetData(Item.QualifiedItemId).GetTexture();

            Sprite = new(
                spriteSheet,
                Game1.getSourceRectForStandardTileSheet(
                    spriteSheet,
                    Item.ParentSheetIndex,
                    SObject.spriteSheetTileSize,
                    SObject.spriteSheetTileSize
                    )
                );
        }

        // get price by calling Item.SellToStorePrice()
        /// <value>Property <c>Price</c> represents the price of the crop. With Shop Modifiers </value>
        public int Price => Item.sellToStorePrice();

        /// <value>Property <c>Days</c> represents the crop's total days to grow excluding <see cref="RegrowDays"/>.</value>

        public int Days => CropData.DaysInPhase.Sum();

        /// <value>Property <c>RegrowDays</c> represents the crop's regrow time.</value>

        public int RegrowDays => CropData.RegrowDays;
        /// <value>Property <c>MinHarvests</c> represents the crop's minimum drops.</value>

        public int MinHarvests => CropData.HarvestMinStack;
        /// <value>Property <c>MaxHarvests</c> represents the crop's maximum drops.</value>

        public int MaxHarvests => CropData.HarvestMaxStack;
        /// <value>Property <c>MaxHarvestIncreasePerFarmingLevel</c> represents the crop's maximum drops increase per farming level.</value>

        public float MaxHarvestIncreasePerFarmingLevel => CropData.HarvestMaxIncreasePerFarmingLevel;
        /// <value>Property <c>ChanceForExtraCrops</c> represents the crop's chance for extra crops.</value>

        public double ChanceForExtraCrops => CropData.ExtraHarvestChance;
        /// <value>Property <c>DisplayName</c> represents the crop's name.</value>

        public string DisplayName => Item.DisplayName;
        /// <value>Property <c>Sprite</c> represents the crop's sprite. It's unused as of now.</value>

        public Tuple<Texture2D, Rectangle> Sprite { get; set; }

        /// <summary>
        /// Returns whether two crops are equal or not. Two crops are equal if they have the same ID.
        /// </summary>
        /// <returns> Whether two crops are equal or not.</returns>
        public override bool Equals(object? obj)
        {
            if (obj is CropDataExpanded crop)
            {
                return
                    crop.CropData.HarvestItemId == CropData.HarvestItemId &&
                    crop.CropData.SpriteIndex == CropData.SpriteIndex &&
                    crop.Seed == Seed;
            }
            else return false;
        }

        /// <summary>
        /// Returns a string representation of the crop.
        /// </summary>
        /// <returns> String representation of the crop.</returns>
        public override string? ToString()
        {
            return $"CropData: {CropData.HarvestItemId}, " +
                $"SpriteIndex: {CropData.SpriteIndex}, " +
                $"Seed: {Seed.DisplayName}";
        }

        /// <summary>
        /// Returns the hash code of the crop.
        /// </summary>
        /// <returns> Hash code of the crop. <c>int</c></returns>
        public override int GetHashCode()
        {
            //using FNV-1a hash
            unchecked
            {
                const int p = 16777619;
                int hash = (int)2166136261;

                hash = (hash ^ CropData.HarvestItemId.GetHashCode()) * p;
                hash = (hash ^ CropData.SpriteIndex.GetHashCode()) * p;
                hash = (hash ^ Seed.Name.GetHashCode()) * p;

                return hash;
            }
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
            //if paddy crop then add 0.25f and if profession is agriculturist then add 0.1f
            if (CropData.IsPaddyCrop)
            {
                speedIncreaseModifier += 0.25f;
            }
            if (Game1.player.professions.Contains(Farmer.agriculturist))
            {
                speedIncreaseModifier += 0.1f;
            }
            return speedIncreaseModifier;
        }

        /// <summary>
        /// Checks whether the crop is available for the current season.
        /// </summary>
        /// <param name="currentSeason"></param>
        /// <returns> Whether the crop is available for the current season or not.</returns>
        public bool IsAvailableForCurrentSeason(Utils.UtilsSeason currentSeason)
        {
            //match UtilsSeason with the crop's seasons from StardewValley, case insensitive
            int seasonNum = (int)currentSeason;
            try
            {
                return CropData.Seasons.Contains((Season)seasonNum);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the total available days for planting and harvesting the crop. Depends on which seasons the crop can grow.
        /// </summary>
        /// <param name="currentSeason">Current season of type UtilsSeason <see cref="UtilsSeason"/></param>
        /// <param name="day">Current day as int, can be from 0 to 1</param>
        /// <returns> Total available days for planting and harvesting the crop. <c>int</c></returns>
        public int TotalAvailableDays(UtilsSeason currentSeason, int day)
        {
            int totalAvailableDays = 0;
            if (IsAvailableForCurrentSeason(currentSeason))
            {
                //Each season has 28 days,
                //get index of current season
                int seasonIndex = (int)currentSeason;
                //iterate over the array and add the number of days for each season that is later than the current season
                for (int i = seasonIndex + 1; i < CropData.Seasons.Count; i++)
                {
                    totalAvailableDays += 28;
                }
                //add the number of days in the current season
                totalAvailableDays += TotalAvailableDaysInCurrentSeason(day);
            }
            if (currentSeason == Utils.UtilsSeason.Greenhouse)
            {
                totalAvailableDays = (28 * 4);
            }
            return totalAvailableDays;
        }

        /// <summary>
        /// Returns the total available days for planting and harvesting the crop for the current season. Depends on which seasons the crop can grow.
        /// </summary>
        /// <param name="day">Current day as int, can be from 0 to 1</param>
        /// <returns>Total available days for planting and harvesting the crop in current season. <c>int</c></returns>
        public static int TotalAvailableDaysInCurrentSeason(int day)
        {
            return 28 - day;
        }

        /// <summary>
        /// Returns the total harvests for the crop for the available time. Depends on which seasons the crop can grow, the current day , and the fertilizer quality.
        /// </summary>
        /// <param name="currentSeason"> Current season of type UtilsSeason <see cref="UtilsSeason"/></param>
        /// <param name="fertilizerQuality"> Quality of the used Fertilizer of type FertilizerQuality <see cref="FertilizerQuality"/></param>
        /// <param name="day"> Current day as int, can be from 0 to 1</param>
        /// <returns> Total number of harvests for the crop for the available time. <c>int</c></returns>
        public int TotalHarvestsWithRemainingDays(Utils.UtilsSeason currentSeason, FertilizerQuality fertilizerQuality, int day)
        {
            int totalHarvestTimes = 0;
            int totalAvailableDays = TotalAvailableDays(currentSeason, day);
            //season is Greenhouse
            float averageGrowthSpeedValueForCrop = GetAverageGrowthSpeedValueForCrop(fertilizerQuality);
            int days = CropData.DaysInPhase.Sum();
            int daysToRegrow = CropData.RegrowDays;
            int daysToRemove = (int)Math.Ceiling((float)days * averageGrowthSpeedValueForCrop);
            int growingDays = Math.Max(days - daysToRemove, 1);
            if (IsAvailableForCurrentSeason(currentSeason) || currentSeason == Utils.UtilsSeason.Greenhouse)
            {
                if (totalAvailableDays < growingDays)
                    return 0;
                //if the crop regrows, then the total harvest times are 1 for the first harvest and then the number of times it can regrow in the remaining days. We always need to subtract one to account for the day lost in the planting day.
                if (daysToRegrow > 0)
                {
                    totalHarvestTimes = ((int)(1 + ((totalAvailableDays - growingDays) / (double)daysToRegrow)));
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
            //TODO: Actually use this

            double totalCrops = MinHarvests;
            if (MinHarvests > 1 || MaxHarvests > 1)
            {
                int max_harvest_increase = 0;
                if (MaxHarvestIncreasePerFarmingLevel > 0)
                {
                    max_harvest_increase = (int)(Game1.player.FarmingLevel / MaxHarvestIncreasePerFarmingLevel);
                }
                totalCrops = (double)(MinHarvests + MaxHarvests + max_harvest_increase) / 2.0;
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

            double AverageExtraCrop = CropData.ExtraHarvestChance;

#pragma warning disable S125
            // Sections of code should not be commented out
            /*
                        if (ChanceForExtraCrops <= 0.0)
                            return AverageExtraCrop;

                        var items = Enumerable.Range(1, 2);
                        AverageExtraCrop += items.Select(i => Math.Pow(ChanceForExtraCrops, i)).Sum();
                        */

            //average extra crops, should be 0.111 for 0.1 chance and
            return AverageExtraCrop;
#pragma warning restore S125 // Sections of code should not be commented out
        }

        #endregion Growth Values Calculations
    }
}