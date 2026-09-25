using ProfitCalculator.main.accessors;
using ProfitCalculator.main.memory;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static ProfitCalculator.Utils;

#nullable enable

namespace ProfitCalculator.main.models
{
    /// <summary>
    /// Class <c>CropDataExpanded</c> models a crop from the game storing all relevant information about it.
    /// </summary>
    public abstract class PlantData
    {
        protected PlantData(
            int days,
            int regrowDays,
            int minHarvests,
            int maxHarvests,
            float maxHarvestIncreasePerFarmingLevel,
            double chanceForExtraCrops,
            string displayName,
            List<Season> seasons,
            Item seed,
            bool affectByQuality,
            bool affectByFertilizer,
            DropInformation dropInformation
        )
        {
            Days = days;
            RegrowDays = regrowDays;
            MinHarvests = minHarvests;
            MaxHarvests = maxHarvests;
            MaxHarvestIncreasePerFarmingLevel = maxHarvestIncreasePerFarmingLevel;
            ChanceForExtraCrops = chanceForExtraCrops;
            DisplayName = displayName;
            Seasons = seasons;
            Seed = seed;
            AffectByQuality = affectByQuality;
            AffectByFertilizer = affectByFertilizer;
            DropInformation = dropInformation;
        }

        /// <value>Property <c>Seed</c> represents the Seed of the crop.</value>
        public Item Seed { get; init; }

        /// <value>Property <c>dropInformation</c> represents the drop information of the Plant data.</value>
        public DropInformation DropInformation { get; set; }

        /// <value>Property <c>affectByQuality</c> represents whether the crop is affected by fertilizer quality or not. Some crops like Tea aren't affected by this. </value>
        public bool AffectByQuality { get; set; }

        /// <value>Property <c>affectByFertilizer</c> represents whether the crop is affected by fertilizer or not.</value>
        public bool AffectByFertilizer { get; set; }

        /// <summary> Seed price set explicitly (manual crops, the mod API); when null the cheapest shop price is used. </summary>
        protected int? SeedPriceOverride { get; set; }

        /// <value>Property <c>SeedPrice</c> represents the price of the seed: the explicit override when set, otherwise the cheapest shop price. </value>
        public virtual int SeedPrice
        {
            get
            {
                return SeedPriceOverride ?? Container.Instance.GetInstance<ShopAccessor>(ModEntry.UniqueID)?.GetCheapestSeedPrice(Seed.QualifiedItemId) ?? 0;
            }
            set => SeedPriceOverride = value;
        }

        /// <value>Property <c>Days</c> represents the crop's total days to grow excluding <see cref="RegrowDays"/>.</value>
        public int Days { get; set; }

        /// <value> Property <c>RegrowDays</c> represents the crop's regrow days. If the crop doesn't regrow, it's set to 0.</value>
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

        /// <value>Property <c>Seasons</c> available seasons.</value>
        public List<Season> Seasons { get; set; }

        /// <value>Property <c>Price</c> represents the crop's average sell price, including the sale profession bonuses (Tiller, Artisan) when the player has them and base stats aren't forced.</value>
        public virtual int Price(UtilsSeason season) => (int)Math.Round(DropInformation.AveragePrice(season, true));

        /// <summary> The calculator holding the current settings, if registered. </summary>
        protected static Calculator? Calc => Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID);

        /// <summary>
        /// The selected fertilizer as it applies to this plant: <see cref="FertilizerQuality.None"/> when the plant doesn't
        /// accept fertilizer (<see cref="AffectByFertilizer"/>), so neither its quality boost nor its cost counts.
        /// </summary>
        public FertilizerQuality AppliedFertilizerQuality => AffectByFertilizer ? Calc?.FertilizerQuality ?? FertilizerQuality.None : FertilizerQuality.None;

        #region Growth Values Calculations

        /// <summary>
        /// Calculates the average growth speed value for the crop.
        /// It's calculated by adding fertilizer modifiers to 1.0f and finally adding 0.25f if the crop is a paddy crop and 0.1f if the player has the agriculturist profession.
        /// </summary>
        /// <param name="fertilizerQuality"> Quality of the used Fertilizer</param>
        /// <returns> Average growth speed value for the crop. <c>float</c></returns>
        public virtual float GetAverageGrowthSpeedValueForCrop(FertilizerQuality fertilizerQuality)
        {
            return 0.0f;
        }

        /// <summary>
        /// Days from planting until the first harvest, after growth speed bonuses. Mirrors the game: <c>days - ceil(days * speed)</c>, never below 1.
        /// </summary>
        /// <param name="fertilizerQuality"> Quality of the used Fertilizer</param>
        /// <returns> Days until the first harvest. <c>int</c></returns>
        public int GrowingDays(FertilizerQuality fertilizerQuality)
        {
            int daysToRemove = (int)Math.Ceiling(Days * GetAverageGrowthSpeedValueForCrop(fertilizerQuality));
            return Math.Max(Days - daysToRemove, 1);
        }

        /// <summary>
        /// Checks whether the crop is available for the current Season.
        /// </summary>
        /// <param name="currentSeason"></param>
        /// <returns> Whether the crop is available for the current Season or not.</returns>
        public virtual bool IsAvailableForCurrentSeason(UtilsSeason currentSeason)
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
        public virtual int TotalAvailableDays(UtilsSeason currentSeason, int day)
        {
            if (currentSeason == UtilsSeason.Greenhouse)
            {
                return 28 * 4;
            }
            if (!IsAvailableForCurrentSeason(currentSeason))
            {
                return 0;
            }
            //days left in the current Season, plus 28 for every consecutive following Season the crop also grows in (when cross season is on)
            int totalAvailableDays = TotalAvailableDaysInCurrentSeason(day);
            if (!(Calc?.CrossSeason ?? false))
            {
                return totalAvailableDays;
            }
            for (int i = 1; i < 4; i++)
            {
                Season next = (Season)(((int)currentSeason + i) % 4);
                if (!Seasons.Contains(next))
                {
                    break;
                }
                totalAvailableDays += 28;
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
        public virtual int TotalHarvestsWithRemainingDays(UtilsSeason currentSeason, FertilizerQuality fertilizerQuality, int day)
        {
            int totalHarvestTimes = 0;
            int totalAvailableDays = TotalAvailableDays(currentSeason, day);
            int daysToRegrow = RegrowDays;
            int growingDays = GrowingDays(fertilizerQuality);
            if (IsAvailableForCurrentSeason(currentSeason) || currentSeason == UtilsSeason.Greenhouse)
            {
                if (totalAvailableDays < growingDays)
                {
                    return 0;
                }

                //if the crop regrows, then the total harvest times are 1 for the first harvest and then the number of times it can regrow in the remaining days. We always need to subtract one to account for the day lost in the planting day.
                if (daysToRegrow > 0)
                {
                    totalHarvestTimes = (int)(1 + ((totalAvailableDays - growingDays) / (double)daysToRegrow));
                }
                else
                {
                    totalHarvestTimes = totalAvailableDays / growingDays;
                }
            }
            return totalHarvestTimes;
        }

        /// <summary>
        /// Average number of crops per harvest before the extra-crop chance. The game rolls uniformly between the minimum stack and the maximum stack plus the farming level bonus.
        /// </summary>
        /// <returns> Average crops per harvest. <c>double</c></returns>
        public virtual double AverageCropsPerHarvest()
        {
            // HarvestMaxIncreasePerFarmingLevel is the number of extra items per farming level, added to the max stack
            int maxHarvestIncrease = 0;
            if (MaxHarvestIncreasePerFarmingLevel > 0)
            {
                maxHarvestIncrease = (int)((Calc?.FarmingLevel ?? 0) * MaxHarvestIncreasePerFarmingLevel);
            }
            if (MinHarvests <= 1 && MaxHarvests + maxHarvestIncrease <= 1)
            {
                return 1;
            }
            int max = Math.Max(MinHarvests, MaxHarvests + maxHarvestIncrease);
            return (MinHarvests + max) / 2.0;
        }

        /// <summary>
        /// Average extra crops per harvest from the extra-crop chance. The game keeps rolling while the roll succeeds (chance capped at 0.9), so the expected count is <c>p / (1 - p)</c>.
        /// </summary>
        /// <returns> Average extra crops from luck. <c>double</c></returns>
        public virtual double AverageExtraCropsFromRandomness()
        {
            double chance = Math.Min(0.9, ChanceForExtraCrops);
            return chance <= 0 ? 0 : chance / (1 - chance);
        }

        #endregion Growth Values Calculations

        #region Crop Profit Calculations

        /// <summary>
        /// Total sale value of the crop over the available time for the selected produce type, before seed and fertilizer costs.
        /// </summary>
        /// <returns> Total value of all harvests. <c>double</c></returns>
        public virtual double TotalCropProfit()
        {
            UtilsSeason season = Calc?.Season ?? UtilsSeason.Spring;
            FertilizerQuality fertilizerQuality = Calc?.FertilizerQuality ?? FertilizerQuality.None;
            uint day = Calc?.Day ?? 0;
            string produceType = Calc?.ProduceType ?? RawProduceType;
            double profitPerHarvest = IsSoldRaw(produceType)
                ? RawValuePerHarvest(season)
                : ArtisanValuePerHarvest(season, produceType);
            return profitPerHarvest * TotalHarvestsWithRemainingDays(season, fertilizerQuality, (int)day);
        }

        /// <summary>
        /// Average number of crops per harvest, including the extra-crop chance.
        /// </summary>
        /// <returns> Average crops per harvest. <c>double</c></returns>
        public virtual double TotalCropsPerHarvest()
        {
            return AverageCropsPerHarvest() + AverageExtraCropsFromRandomness();
        }

        /// <summary>
        /// Average sale value of one harvest sold raw, including the quality chances of the first produce.
        /// </summary>
        /// <param name="season"> The selected season. </param>
        /// <returns> Average value of one harvest. <c>double</c></returns>
        public virtual double RawValuePerHarvest(UtilsSeason season)
        {
            double price = Price(season); //already includes the Tiller bonus
            double cropsPerHarvest = TotalCropsPerHarvest();

            if (!AffectByQuality)
            {
                return price * cropsPerHarvest;
            }
            //only the first produce of a harvest rolls for quality, the rest is base quality
            double profitPerHarvest = price * GetAverageValueForCropAfterModifiers();
            profitPerHarvest += price * (cropsPerHarvest - 1);
            return profitPerHarvest;
        }

        /// <summary>
        /// Average sale value of one harvest processed by the machine <paramref name="produceType"/>. Crop quality is
        /// ignored because machine outputs don't copy it; drops the machine rejects count as 0.
        /// </summary>
        /// <param name="season"> The selected season. </param>
        /// <param name="produceType"> The produce type id (see <see cref="MachineAccessor"/>). </param>
        /// <returns> Average value of one harvest. <c>double</c></returns>
        public virtual double ArtisanValuePerHarvest(UtilsSeason season, string produceType)
        {
            MachineAccessor? machines = Machines;
            if (machines is null)
            {
                return 0;
            }
            return TotalCropsPerHarvest() * DropInformation.AverageValue(season, drop => machines.TryGetProduct(drop, produceType, out ProductInfo? product) ? product.ValuePerInput : null);
        }

        /// <summary>
        /// Whether the produce type <paramref name="produceType"/> can process this plant: every drop that counts in the
        /// selected season must be accepted by the machine. Selling raw (see <see cref="Utils.IsSoldRaw"/>) is always possible.
        /// </summary>
        /// <param name="produceType"> The produce type id. </param>
        /// <returns> Whether the plant can be sold as that produce type. </returns>
        public virtual bool CanProduce(string produceType)
        {
            if (IsSoldRaw(produceType))
            {
                return true;
            }
            MachineAccessor? machines = Machines;
            if (machines is null)
            {
                return false;
            }
            UtilsSeason season = Calc?.Season ?? UtilsSeason.Spring;
            List<DropInformation.Drop> counting = DropInformation.Drops.Where(drop => drop.CountsIn(season)).ToList();
            return counting.Count > 0 && counting.All(drop => machines.TryGetProduct(drop, produceType, out _));
        }

        /// <summary>
        /// The product made from the plant's main drop (the one that counts most in the selected season).
        /// </summary>
        /// <param name="produceType"> The produce type id. </param>
        /// <param name="product"> The product, when the machine accepts the main drop. </param>
        /// <returns> Whether a product was found. </returns>
        public bool TryGetMainProduct(string produceType, [NotNullWhen(true)] out ProductInfo? product)
        {
            product = null;
            DropInformation.Drop? main = MainDrop(Calc?.Season ?? UtilsSeason.Spring);
            MachineAccessor? machines = Machines;
            return main is not null && !IsSoldRaw(produceType) && machines is not null && machines.TryGetProduct(main, produceType, out product);
        }

        /// <summary>
        /// Name of what is sold: "Raw" for the harvest itself, otherwise the display name of the main product.
        /// </summary>
        /// <param name="produceType"> The produce type id. </param>
        /// <returns> The display name. </returns>
        public string ProduceName(string produceType)
        {
            if (!IsSoldRaw(produceType) && TryGetMainProduct(produceType, out ProductInfo? product))
            {
                return product.Output.DisplayName;
            }
            return Container.Instance.GetInstance<IModHelper>(ModEntry.UniqueID)?.Translation.Get("raw").ToString() ?? RawProduceType;
        }

        /// <summary>
        /// Expected number of items sold over the available time: crops when sold raw, otherwise machine products
        /// (each input's products divided by the machine's required input count).
        /// </summary>
        /// <param name="produceType"> The produce type id. </param>
        /// <returns> Expected number of items sold. <c>double</c></returns>
        public virtual double TotalProduceCount(string produceType)
        {
            UtilsSeason season = Calc?.Season ?? UtilsSeason.Spring;
            FertilizerQuality fertilizerQuality = Calc?.FertilizerQuality ?? FertilizerQuality.None;
            uint day = Calc?.Day ?? 0;
            MachineAccessor? machines = Machines;
            double perCrop = DropInformation.AverageValue(season, drop =>
            {
                if (IsSoldRaw(produceType))
                {
                    return 1;
                }
                return machines is not null && machines.TryGetProduct(drop, produceType, out ProductInfo? product) ? product.ProductsPerInput : null;
            });
            return perCrop * TotalCropsPerHarvest() * TotalHarvestsWithRemainingDays(season, fertilizerQuality, (int)day);
        }

        /// <summary> The drop that counts most (chance times quantity) in <paramref name="season"/>, if any. This is the input shown for machine products. </summary>
        /// <param name="season"> The selected season. </param>
        /// <returns> The main drop, or null when nothing drops in that season. </returns>
        public DropInformation.Drop? MainDrop(UtilsSeason season)
        {
            return DropInformation.Drops
                .Where(drop => drop.CountsIn(season))
                .OrderByDescending(drop => drop.Chance * drop.Quantity)
                .FirstOrDefault();
        }

        /// <summary> The machine accessor, if registered. </summary>
        protected static MachineAccessor? Machines => Container.Instance.GetInstance<MachineAccessor>(ModEntry.UniqueID);

        public virtual double TotalCropProfitPerDay()
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

        /// <summary>
        /// First day (counted from planting) on which the running profit covers the seed cost, or -1 if never / not applicable.
        /// </summary>
        /// <returns> The payback day, or -1. <c>int</c></returns>
        public virtual int PaybackDay() => -1;

        /// <summary>
        /// Fertilizer stays on the tile for as long as a crop is on it, so one is enough regardless of harvests or seasons.
        /// </summary>
        /// <returns> Fertilizer needed for the whole run. <c>int</c></returns>
        public virtual int TotalFertilizerNeeded()
        {
            return 1;
        }

        public virtual int TotalFertilizerCost()
        {
            bool payForFertilizer = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.PayForFertilizer ?? false;
            FertilizerQuality fertilizerQuality = AppliedFertilizerQuality;
            if (!payForFertilizer)
            {
                return 0;
            }
            int fertNeeded = TotalFertilizerNeeded();
            int fertCost = FertilizerPrices(fertilizerQuality);
            return fertNeeded * fertCost;
        }

        public virtual double TotalFertilzerCostPerDay()
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

        public virtual int TotalSeedsNeeded()
        {
            UtilsSeason season = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.Season ?? UtilsSeason.Spring;
            uint day = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.Day ?? 0;
            FertilizerQuality fertilizerQuality = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.FertilizerQuality ?? FertilizerQuality.None;
            if (RegrowDays > 0 && TotalAvailableDays(season, (int)day) > 0)
            {
                return 1;
            }
            else { return TotalHarvestsWithRemainingDays(season, fertilizerQuality, (int)day); }
        }

        public virtual int TotalSeedsCost()
        {
            bool payForSeeds = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.PayForSeeds ?? false;
            if (!payForSeeds)
            {
                return 0;
            }

            int seedsNeeded = TotalSeedsNeeded();
            int seedCost = SeedPrice;

            return seedsNeeded * seedCost;
        }

        public virtual double TotalSeedsCostPerDay()
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

        public virtual double GetAverageValueMultiplierForCrop()
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

        /// <summary>
        /// Average price multiplier of the first produce of a harvest, from the quality chances. Tiller is already part of <see cref="Price"/>.
        /// </summary>
        public virtual double GetAverageValueForCropAfterModifiers()
        {
            return GetAverageValueMultiplierForCrop();
        }

        public virtual double GetCropBaseGoldQualityChance(double limit)
        {
            FertilizerQuality FertilizerQuality = AppliedFertilizerQuality;

            var FarmingLevel = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.FarmingLevel ?? 0;
            int fertilizerQualityLevel = (int)FertilizerQuality > 0 ? (int)FertilizerQuality : 0;
            double part1 = (0.2 * (FarmingLevel / 10.0)) + 0.01;
            double part2 = 0.2 * (fertilizerQualityLevel * ((FarmingLevel + 2) / 12.0));
            return Math.Min(limit, part1 + part2);
        }

        public virtual double GetCropBaseGoldQualityChance() => GetCropBaseGoldQualityChance(9999999999);

        public virtual double GetCropBaseQualityChance()
        {
            FertilizerQuality FertilizerQuality = AppliedFertilizerQuality;
            return FertilizerQuality >= Utils.FertilizerQuality.Deluxe ? 0f : Math.Max(0f, 1f - (GetCropIridiumQualityChance() + GetCropGoldQualityChance() + GetCropSilverQualityChance()));
        }

        public virtual double GetCropSilverQualityChance()
        {
            FertilizerQuality FertilizerQuality = AppliedFertilizerQuality;
            return FertilizerQuality >= Utils.FertilizerQuality.Deluxe ? 1f - (GetCropIridiumQualityChance() + GetCropGoldQualityChance()) : (1f - GetCropIridiumQualityChance()) * (1f - GetCropBaseGoldQualityChance()) * Math.Min(0.75, 2 * GetCropBaseGoldQualityChance());
        }

        public virtual double GetCropGoldQualityChance()
        {
            return GetCropBaseGoldQualityChance(1f) * (1f - GetCropIridiumQualityChance());
        }

        public virtual double GetCropIridiumQualityChance()
        {
            FertilizerQuality FertilizerQuality = AppliedFertilizerQuality;

            return FertilizerQuality >= Utils.FertilizerQuality.Deluxe ? GetCropBaseGoldQualityChance() / 2.0 : 0f;
        }

        #endregion Crop Modifer Value Calculations
    }
}
