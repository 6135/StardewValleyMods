using ProfitCalculator.main.models;
using System;
using System.Collections.Generic;
using StardewValley;

#nullable enable

namespace ProfitCalculator.main
{
    /// <summary>
    /// Contains information about a crop and its profit.
    /// </summary>
    public class CropInfo
    {
        ///<summary> The crop. </summary>
        public PlantData Crop { get; }

        /// <summary> The total profit. </summary>
        public double TotalProfit { get; }

        /// <summary> The profit per day. </summary>
        public double ProfitPerDay { get; }

        /// <summary> The total seed loss. </summary>
        public double TotalSeedLoss { get; }

        /// <summary> The seed loss per day. </summary>
        public double SeedLossPerDay { get; }

        /// <summary> The total fertilizer loss. </summary>
        public double TotalFertilizerLoss { get; }

        /// <summary> The fertilizer loss per day. </summary>
        public double FertilizerLossPerDay { get; }

        /// <summary> The produce type id (see <see cref="Utils.RawProduceType"/>). </summary>
        public string ProduceType { get; }

        /// <summary> The duration. </summary>
        public int Duration { get; }

        /// <summary> The total harvests. </summary>
        public int TotalHarvests { get; }

        /// <summary> The growth time. </summary>
        public int GrowthTime { get; }

        /// <summary> The regrowth time. </summary>
        public int RegrowthTime { get; }

        /// <summary> The chance of normal quality. </summary>
        public double ChanceOfNormalQuality { get; }

        /// <summary> The chance of silver quality. </summary>
        public double ChanceOfSilverQuality { get; }

        /// <summary> The chance of gold quality. </summary>
        public double ChanceOfGoldQuality { get; }

        /// <summary> The chance of iridium quality. </summary>
        public double ChanceOfIridiumQuality { get; }

        /// <summary> Display name of what is sold: "Raw" or the machine product's name. </summary>
        public string ProduceName { get; }

        /// <summary> Expected number of items sold over the run (machine products after dividing by the required input count). </summary>
        public double ProduceCount { get; }

        /// <summary> Input items the machine takes per batch; 1 when sold raw. </summary>
        public int InputsPerProduct { get; }

        /// <summary> Days the machine takes per batch; 0 when sold raw. </summary>
        public double ProcessingDays { get; }

        /// <summary> Seeds needed over the run. </summary>
        public int SeedsNeeded { get; }

        /// <summary> Fertilizer needed over the run. </summary>
        public int FertilizerNeeded { get; }

        /// <summary> The machine's main product; null when sold raw. </summary>
        public Item? ProduceItem { get; }

        /// <summary> The harvested item that goes into the machine (the crop's main drop). </summary>
        public Item InputItem { get; }

        /// <summary> Whether the harvest is sold as is (no machine product), see <see cref="Utils.IsSoldRaw"/>. </summary>
        public bool IsSoldRaw => Utils.IsSoldRaw(ProduceType);

        /// <summary> Whether the row comes from a tree view (<see cref="Utils.FruitTreesProduceType"/> or <see cref="Utils.WildTreesProduceType"/>). </summary>
        public bool IsTreeView => Utils.IsTreeView(ProduceType);

        /// <summary> First day (counted from planting) on which the running profit covers the seed cost, or -1 if never / not applicable. See <see cref="PlantData.PaybackDay"/>. </summary>
        public int PaybackDay { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CropInfo"/> class.
        /// </summary>
        /// <param name="crop"> The crop. </param>
        /// <param name="totalProfit"> The total profit. Calculated from <c>totalProfit = totalProfit - (totalSeedLoss + totalFertilizerLoss)</c> </param>
        /// <param name="profitPerDay"> The profit per day. </param>
        /// <param name="totalSeedLoss"> The total seed loss. </param>
        /// <param name="seedLossPerDay"> The seed loss per day. </param>
        /// <param name="totalFertilizerLoss"> The total fertilizer loss. </param>
        /// <param name="fertilizerLossPerDay"> The fertilizer loss per day. </param>
        /// <param name="produceType"> The produce type. </param>
        /// <param name="duration"> The duration. </param>
        /// <param name="totalHarvests"> The total harvests. </param>
        /// <param name="growthTime"> The growth time. </param>
        /// <param name="regrowthTime"> The regrowth time. </param>
        /// <param name="chanceOfNormalQuality"> The chance of normal quality. </param>
        /// <param name="chanceOfSilverQuality"> The chance of silver quality. </param>
        /// <param name="chanceOfGoldQuality"> The chance of gold quality. </param>
        /// <param name="chanceOfIridiumQuality"> The chance of iridium quality. </param>
        /// <param name="produceName"> Display name of what is sold. </param>
        /// <param name="produceCount"> Expected number of items sold over the run. </param>
        /// <param name="inputsPerProduct"> Input items the machine takes per batch. </param>
        /// <param name="processingDays"> Days the machine takes per batch. </param>
        /// <param name="seedsNeeded"> Seeds needed over the run. </param>
        /// <param name="fertilizerNeeded"> Fertilizer needed over the run. </param>
        /// <param name="produceItem"> The machine's main product, or null when sold raw. </param>
        /// <param name="inputItem"> The harvested item that goes into the machine. </param>
        /// <param name="paybackDay"> First day the running profit covers the seed cost, or -1 if never / not applicable. </param>
        public CropInfo(PlantData crop, double totalProfit, double profitPerDay, double totalSeedLoss, double seedLossPerDay, double totalFertilizerLoss, double fertilizerLossPerDay, string produceType, int duration, int totalHarvests, int growthTime, int regrowthTime, double chanceOfNormalQuality, double chanceOfSilverQuality, double chanceOfGoldQuality, double chanceOfIridiumQuality, string produceName, double produceCount, int inputsPerProduct, double processingDays, int seedsNeeded, int fertilizerNeeded, Item? produceItem, Item inputItem, int paybackDay = -1)
        {
            PaybackDay = paybackDay;
            Crop = crop;
            TotalProfit = totalProfit - totalSeedLoss - totalFertilizerLoss;
            ProfitPerDay = profitPerDay - seedLossPerDay - fertilizerLossPerDay;
            TotalSeedLoss = totalSeedLoss;
            SeedLossPerDay = seedLossPerDay;
            TotalFertilizerLoss = totalFertilizerLoss;
            FertilizerLossPerDay = fertilizerLossPerDay;
            ProduceType = produceType;
            Duration = duration;
            TotalHarvests = totalHarvests;
            GrowthTime = growthTime;
            RegrowthTime = regrowthTime;
            ChanceOfNormalQuality = chanceOfNormalQuality;
            ChanceOfSilverQuality = chanceOfSilverQuality;
            ChanceOfGoldQuality = chanceOfGoldQuality;
            ChanceOfIridiumQuality = chanceOfIridiumQuality;
            ProduceName = produceName;
            ProduceCount = produceCount;
            InputsPerProduct = inputsPerProduct;
            ProcessingDays = processingDays;
            SeedsNeeded = seedsNeeded;
            FertilizerNeeded = fertilizerNeeded;
            ProduceItem = produceItem;
            InputItem = inputItem;
        }

        #region Overloads and Overrides

        /// <summary>
        /// Returns a <see cref="string"/> that represents the current <see cref="CropInfo"/> in json format.
        /// </summary>
        /// <returns> A <see cref="string"/> that represents the current <see cref="CropInfo"/> in json format. </returns>
        public override string ToString()
        { //return object in json format
            return "{" +
                $"\"CropDataExpanded\": {Crop.DropInformation}," +
                $"\"TotalProfit\": {TotalProfit}," +
                $"\"ProfitPerDay\": {ProfitPerDay}," +
                $"\"TotalSeedLoss\": {TotalSeedLoss}," +
                $"\"SeedLossPerDay\": {SeedLossPerDay}," +
                $"\"TotalFertilizerLoss\": {TotalFertilizerLoss}," +
                $"\"FertilizerLossPerDay\": {FertilizerLossPerDay}," +
                $"\"ProduceType\": \"{ProduceType}\"," +
                $"\"ProduceName\": \"{ProduceName}\"," +
                $"\"ProduceCount\": {ProduceCount}," +
                $"\"InputsPerProduct\": {InputsPerProduct}," +
                $"\"ProcessingDays\": {ProcessingDays}," +
                $"\"SeedsNeeded\": {SeedsNeeded}," +
                $"\"FertilizerNeeded\": {FertilizerNeeded}," +
                $"\"Duration\": {Duration}," +
                $"\"TotalHarvests\": {TotalHarvests}," +
                $"\"GrowthTime\": {GrowthTime}," +
                $"\"RegrowthTime\": {RegrowthTime}," +
                $"\"ChanceOfNormalQuality\": {ChanceOfNormalQuality}," +
                $"\"ChanceOfSilverQuality\": {ChanceOfSilverQuality}," +
                $"\"ChanceOfGoldQuality\": {ChanceOfGoldQuality}," +
                $"\"ChanceOfIridiumQuality\": {ChanceOfIridiumQuality}," +
                $"\"PaybackDay\": {PaybackDay}" +
                "}";
        }

        /// <summary>
        /// Determines whether the specified <see cref="CropInfo"/> is equal to the current <see cref="CropInfo"/>.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns> <c>true</c> if the specified <see cref="CropInfo"/> is equal to the current <see cref="CropInfo"/>; otherwise, <c>false</c>. </returns>
        public override bool Equals(object? obj)
        {
            if (obj is not CropInfo cropInfo)
            {
                return false;
            }

            static bool AreDoublesEqual(double a, double b, double tolerance) => Math.Abs(a - b) < tolerance;

            (double, double)[] doubleProperties =
            {
                (TotalProfit, cropInfo.TotalProfit),
                (ProfitPerDay, cropInfo.ProfitPerDay),
                (TotalSeedLoss, cropInfo.TotalSeedLoss),
                (SeedLossPerDay, cropInfo.SeedLossPerDay),
                (TotalFertilizerLoss, cropInfo.TotalFertilizerLoss),
                (FertilizerLossPerDay, cropInfo.FertilizerLossPerDay),
                (ChanceOfNormalQuality, cropInfo.ChanceOfNormalQuality),
                (ChanceOfSilverQuality, cropInfo.ChanceOfSilverQuality),
                (ChanceOfGoldQuality, cropInfo.ChanceOfGoldQuality),
                (ChanceOfIridiumQuality, cropInfo.ChanceOfIridiumQuality),
                (ProduceCount, cropInfo.ProduceCount),
                (ProcessingDays, cropInfo.ProcessingDays)
            };

            foreach (var (prop, cropProp) in doubleProperties)
            {
                if (!AreDoublesEqual(prop, cropProp, 0.0001))
                {
                    return false;
                }
            }

            bool part1 =
                    EqualityComparer<PlantData>.Default.Equals(Crop, cropInfo.Crop) &&
                    ProduceType == cropInfo.ProduceType &&
                    Duration == cropInfo.Duration;

            bool parte2 =
                    TotalHarvests == cropInfo.TotalHarvests &&
                    GrowthTime == cropInfo.GrowthTime &&
                    RegrowthTime == cropInfo.RegrowthTime;

            bool part3 =
                    ProduceName == cropInfo.ProduceName &&
                    InputsPerProduct == cropInfo.InputsPerProduct &&
                    SeedsNeeded == cropInfo.SeedsNeeded &&
                    FertilizerNeeded == cropInfo.FertilizerNeeded;

            return part1 && parte2 && part3 && PaybackDay == cropInfo.PaybackDay;
        }

        /// <summary>
        /// Returns the hash code for this <see cref="CropInfo"/>.
        /// </summary>
        /// <returns> A 32-bit signed integer hash code. </returns>
        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(Crop);
            hash.Add(TotalProfit);
            hash.Add(ProfitPerDay);
            hash.Add(TotalSeedLoss);
            hash.Add(SeedLossPerDay);
            hash.Add(TotalFertilizerLoss);
            hash.Add(FertilizerLossPerDay);
            hash.Add(ProduceType);
            hash.Add(Duration);
            hash.Add(TotalHarvests);
            hash.Add(GrowthTime);
            hash.Add(RegrowthTime);
            hash.Add(ChanceOfNormalQuality);
            hash.Add(ChanceOfSilverQuality);
            hash.Add(ChanceOfGoldQuality);
            hash.Add(ChanceOfIridiumQuality);
            hash.Add(ProduceName);
            hash.Add(ProduceCount);
            hash.Add(InputsPerProduct);
            hash.Add(ProcessingDays);
            hash.Add(SeedsNeeded);
            hash.Add(FertilizerNeeded);
            hash.Add(PaybackDay);
            return hash.ToHashCode();
        }

        #endregion Overloads and Overrides
    }
}