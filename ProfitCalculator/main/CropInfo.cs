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
        public readonly PlantData Crop;

        /// <summary> The total profit. </summary>
        public readonly double TotalProfit;

        /// <summary> The profit per day. </summary>
        public readonly double ProfitPerDay;

        /// <summary> The total seed loss. </summary>
        public readonly double TotalSeedLoss;

        /// <summary> The seed loss per day. </summary>
        public readonly double SeedLossPerDay;

        /// <summary> The total fertilizer loss. </summary>
        public readonly double TotalFertilizerLoss;

        /// <summary> The fertilizer loss per day. </summary>
        public readonly double FertilizerLossPerDay;

        /// <summary> The produce type id (see <see cref="Utils.RawProduceType"/>). </summary>
        public readonly string ProduceType;

        /// <summary> The duration. </summary>
        public readonly int Duration;

        /// <summary> The total harvests. </summary>
        public readonly int TotalHarvests;

        /// <summary> The growth time. </summary>
        public readonly int GrowthTime;

        /// <summary> The regrowth time. </summary>
        public readonly int RegrowthTime;

        /// <summary> The product count. </summary>
        public readonly int ProductCount;

        /// <summary> The chance of extra product. </summary>
        public readonly double ChanceOfExtraProduct;

        /// <summary> The chance of normal quality. </summary>
        public readonly double ChanceOfNormalQuality;

        /// <summary> The chance of silver quality. </summary>
        public readonly double ChanceOfSilverQuality;

        /// <summary> The chance of gold quality. </summary>
        public readonly double ChanceOfGoldQuality;

        /// <summary> The chance of iridium quality. </summary>
        public readonly double ChanceOfIridiumQuality;

        /// <summary> Display name of what is sold: "Raw" or the machine product's name. </summary>
        public readonly string ProduceName;

        /// <summary> Expected number of items sold over the run (machine products after dividing by the required input count). </summary>
        public readonly double ProduceCount;

        /// <summary> Input items the machine takes per batch; 1 when sold raw. </summary>
        public readonly int InputsPerProduct;

        /// <summary> Days the machine takes per batch; 0 when sold raw. </summary>
        public readonly double ProcessingDays;

        /// <summary> Seeds needed over the run. </summary>
        public readonly int SeedsNeeded;

        /// <summary> Fertilizer needed over the run. </summary>
        public readonly int FertilizerNeeded;

        /// <summary> The machine's main product; null when sold raw. </summary>
        public readonly Item? ProduceItem;

        /// <summary> The harvested item that goes into the machine (the crop's main drop). </summary>
        public readonly Item InputItem;

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
        /// <param name="productCount"> The product count. </param>
        /// <param name="chanceOfExtraProduct"> The chance of extra product. </param>
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
        public CropInfo(PlantData crop, double totalProfit, double profitPerDay, double totalSeedLoss, double seedLossPerDay, double totalFertilizerLoss, double fertilizerLossPerDay, string produceType, int duration, int totalHarvests, int growthTime, int regrowthTime, int productCount, double chanceOfExtraProduct, double chanceOfNormalQuality, double chanceOfSilverQuality, double chanceOfGoldQuality, double chanceOfIridiumQuality, string produceName, double produceCount, int inputsPerProduct, double processingDays, int seedsNeeded, int fertilizerNeeded, Item? produceItem, Item inputItem)
        {
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
            ProductCount = productCount;
            ChanceOfExtraProduct = chanceOfExtraProduct;
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
                $"\"ProductCount\": {ProductCount}," +
                $"\"ChanceOfExtraProduct\": {ChanceOfExtraProduct}," +
                $"\"ChanceOfNormalQuality\": {ChanceOfNormalQuality}," +
                $"\"ChanceOfSilverQuality\": {ChanceOfSilverQuality}," +
                $"\"ChanceOfGoldQuality\": {ChanceOfGoldQuality}," +
                $"\"ChanceOfIridiumQuality\": {ChanceOfIridiumQuality}" +
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
                return false;

            static bool AreDoublesEqual(double a, double b, double tolerance) => Math.Abs(a - b) < tolerance;

            (double, double)[] doubleProperties =
            {
                (TotalProfit, cropInfo.TotalProfit),
                (ProfitPerDay, cropInfo.ProfitPerDay),
                (TotalSeedLoss, cropInfo.TotalSeedLoss),
                (SeedLossPerDay, cropInfo.SeedLossPerDay),
                (TotalFertilizerLoss, cropInfo.TotalFertilizerLoss),
                (FertilizerLossPerDay, cropInfo.FertilizerLossPerDay),
                (ChanceOfExtraProduct, cropInfo.ChanceOfExtraProduct),
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
                    return false;
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

            return part1 && parte2 && part3 && ProductCount == cropInfo.ProductCount;
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
            hash.Add(ProductCount);
            hash.Add(ChanceOfExtraProduct);
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
            return hash.ToHashCode();
        }

        #endregion Overloads and Overrides
    }
}