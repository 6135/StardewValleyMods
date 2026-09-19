using ProfitCalculator.main.models;
using System;
using System.Collections.Generic;

namespace ProfitCalculator.main
{
    /// <summary>
    /// Contains information about a crop and its profit.
    /// </summary>
    public class CropInfo
    {
        ///<summary> The crop. </summary>
        public PlantData Crop { get; }

        /// <summary> The total profit. Calculated from <c>totalProfit = totalProfit - (totalSeedLoss + totalFertilizerLoss)</c> </summary>
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

        /// <summary> The produce type. </summary>
        public Utils.ProduceType ProduceType { get; }

        /// <summary> The duration. </summary>
        public int Duration { get; }

        /// <summary> The total harvests. </summary>
        public int TotalHarvests { get; }

        /// <summary> The growth time. </summary>
        public int GrowthTime { get; }

        /// <summary> The regrowth time. </summary>
        public int RegrowthTime { get; }

        /// <summary> The product count. </summary>
        public int ProductCount { get; }

        /// <summary> The chance of extra product. </summary>
        public double ChanceOfExtraProduct { get; }

        /// <summary> The chance of normal quality. </summary>
        public double ChanceOfNormalQuality { get; }

        /// <summary> The chance of silver quality. </summary>
        public double ChanceOfSilverQuality { get; }

        /// <summary> The chance of gold quality. </summary>
        public double ChanceOfGoldQuality { get; }

        /// <summary> The chance of iridium quality. </summary>
        public double ChanceOfIridiumQuality { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CropInfo"/> class, calculating every value for the crop with the settings of <paramref name="calculator"/>.
        /// </summary>
        /// <param name="crop"> The crop. </param>
        /// <param name="calculator"> The calculator whose settings (season, day, fertilizer, ...) the values are calculated with. </param>
        public CropInfo(PlantData crop, Calculator calculator)
        {
            Crop = crop;
            TotalSeedLoss = crop.TotalSeedsCost();
            SeedLossPerDay = crop.TotalSeedsCostPerDay();
            TotalFertilizerLoss = crop.TotalFertilizerCost();
            FertilizerLossPerDay = crop.TotalFertilzerCostPerDay();
            TotalProfit = crop.TotalCropProfit() - TotalSeedLoss - TotalFertilizerLoss;
            ProfitPerDay = crop.TotalCropProfitPerDay() - SeedLossPerDay - FertilizerLossPerDay;
            ProduceType = calculator.ProduceType;
            Duration = crop.TotalAvailableDays(calculator.Season, (int)calculator.Day);
            TotalHarvests = crop.TotalHarvestsWithRemainingDays(calculator.Season, calculator.FertilizerQuality, (int)calculator.Day);

            float averageGrowthSpeedValueForCrop = crop.GetAverageGrowthSpeedValueForCrop(calculator.FertilizerQuality);
            int daysToRemove = (int)Math.Ceiling((float)crop.Days * averageGrowthSpeedValueForCrop);
            GrowthTime = Math.Max(crop.Days - daysToRemove, 1);

            RegrowthTime = crop.RegrowDays;
            ProductCount = crop.MinHarvests;
            ChanceOfExtraProduct = crop.AverageExtraCropsFromRandomness();
            ChanceOfNormalQuality = crop.GetCropBaseQualityChance();
            ChanceOfSilverQuality = crop.GetCropSilverQualityChance();
            ChanceOfGoldQuality = crop.GetCropGoldQualityChance();
            ChanceOfIridiumQuality = crop.GetCropIridiumQualityChance();
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
                $"\"ProduceType\": {ProduceType}," +
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
        public override bool Equals(object obj)
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
                (ChanceOfExtraProduct, cropInfo.ChanceOfExtraProduct),
                (ChanceOfNormalQuality, cropInfo.ChanceOfNormalQuality),
                (ChanceOfSilverQuality, cropInfo.ChanceOfSilverQuality),
                (ChanceOfGoldQuality, cropInfo.ChanceOfGoldQuality),
                (ChanceOfIridiumQuality, cropInfo.ChanceOfIridiumQuality)
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

            return part1 && parte2 && ProductCount == cropInfo.ProductCount;
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
            return hash.ToHashCode();
        }

        #endregion Overloads and Overrides
    }
}
