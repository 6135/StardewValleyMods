using ProfitCalculator.main;
using ProfitCalculator.main.memory;
using StardewValley;
using System;
using SObject = StardewValley.Object;

#nullable enable
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace ProfitCalculator
{
    /// <summary>
    /// Provides a set of tools to be used by multiple classes of the mod.
    /// </summary>
    public class Utils
    {
        /// <summary>
        /// UtilsSeason enum.
        /// </summary>
        public enum UtilsSeason
        {
            /// <summary> Spring Season. </summary>
            Spring = 0,

            /// <summary> Summer Season. </summary>
            Summer = 1,

            /// <summary> Fall Season. </summary>
            Fall = 2,

            /// <summary> Winter Season. </summary>
            Winter = 3,

            /// <summary> Greenhouse Season. </summary>
            Greenhouse = 4
        }

        /// <summary>
        /// Produce type id for selling the harvest as is. Any other produce type id is a machine's qualified id
        /// (for example <c>(BC)12</c>) or a chained aging id (for example <c>(BC)12&gt;(BC)163</c>), see <see cref="main.accessors.MachineAccessor"/>.
        /// </summary>
        public const string RawProduceType = "Raw";

        /// <summary>
        /// Produce type id listing only fruit trees, over <see cref="Calculator.Years"/> years. The fruit is sold raw.
        /// </summary>
        public const string FruitTreesProduceType = "FruitTrees";

        /// <summary>
        /// Produce type id listing only wild trees with a tapper, over <see cref="Calculator.Years"/> years. The tapper
        /// products are sold raw.
        /// </summary>
        public const string WildTreesProduceType = "WildTrees";

        /// <summary>
        /// Whether <paramref name="produceType"/> sells the harvest as is (no machine product): <see cref="RawProduceType"/>,
        /// <see cref="FruitTreesProduceType"/> and <see cref="WildTreesProduceType"/>.
        /// </summary>
        /// <param name="produceType"> The produce type id. </param>
        /// <returns> Whether the harvest is sold raw. </returns>
        public static bool IsSoldRaw(string? produceType)
        {
            return produceType is RawProduceType or FruitTreesProduceType or WildTreesProduceType;
        }

        /// <summary>
        /// Whether <paramref name="produceType"/> is one of the tree views: <see cref="FruitTreesProduceType"/> or
        /// <see cref="WildTreesProduceType"/>.
        /// </summary>
        /// <param name="produceType"> The produce type id. </param>
        /// <returns> Whether it lists trees only. </returns>
        public static bool IsTreeView(string? produceType)
        {
            return produceType is FruitTreesProduceType or WildTreesProduceType;
        }

        /// <summary>
        /// Fertilizer quality enum.
        /// </summary>
        public enum FertilizerQuality
        {
            /// <summary> No fertilizer. </summary>
            None = 0,

            /// <summary> Basic fertilizer. </summary>
            Basic = 1,

            /// <summary> Quality fertilizer. </summary>
            Quality = 2,

            /// <summary> Deluxe fertilizer. </summary>
            Deluxe = 3,

            /// <summary> Speed-Gro fertilizer. </summary>
            SpeedGro = -1,

            /// <summary> Deluxe Speed-Gro fertilizer. </summary>
            DeluxeSpeedGro = -2,

            /// <summary> Hyper Speed-Gro fertilizer. </summary>
            HyperSpeedGro = -3
        }

        /// <summary>
        /// Get prices of each fertilizer quality.
        /// </summary>
        /// <param name="fq"> The fertilizer quality to get the price of.</param>
        /// <returns> The price of the fertilizer quality.</returns>
        public static int FertilizerPrices(FertilizerQuality fq)
        {
            return fq switch
            {
                FertilizerQuality.None => 0,
                FertilizerQuality.Basic => 100,
                FertilizerQuality.Quality => 150,
                FertilizerQuality.Deluxe => 200,
                FertilizerQuality.SpeedGro => 100,
                FertilizerQuality.DeluxeSpeedGro => 150,
                FertilizerQuality.HyperSpeedGro => 200,
                _ => 0,
            };
        }

        /// <summary>
        /// Whether the Tiller profession raises the sell price of an item (vegetables, fruits and flowers).
        /// </summary>
        /// <param name="item"> The item to check.</param>
        /// <returns> Whether Tiller applies to the item.</returns>
        public static bool IsAffectedByTiller(Item item)
        {
            return item.Category is SObject.VegetableCategory or SObject.FruitsCategory or SObject.flowersCategory;
        }

        /// <summary>
        /// Applies the sale price profession bonuses the game applies in <c>Object.getPriceAfterMultipliers</c>: Tiller
        /// (x1.1 for vegetables, fruits and flowers), Artisan (x1.4 for artisan goods) and Tapper (x1.25 for syrups,
        /// category -27). All are skipped when the calculator is set to use base stats.
        /// </summary>
        /// <param name="item"> The item being sold, used for its category.</param>
        /// <param name="basePrice"> The price before profession bonuses.</param>
        /// <returns> The sale price after the bonuses that apply.</returns>
        public static int ApplySaleBonuses(Item item, int basePrice)
        {
            bool useBaseStats = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.UseBaseStats ?? false;
            var professions = Game1.player?.professions;
            if (useBaseStats || professions is null)
            {
                return basePrice;
            }
            float multiplier = 1f;
            if (IsAffectedByTiller(item) && professions.Contains(Farmer.tiller))
            {
                multiplier *= 1.1f;
            }
            if (item.Category == SObject.artisanGoodsCategory && professions.Contains(Farmer.artisan))
            {
                multiplier *= 1.4f;
            }
            if (item.Category == SObject.syrupCategory && professions.Contains(Farmer.tapper))
            {
                multiplier *= 1.25f;
            }
            return (int)(basePrice * multiplier);
        }

        public static Season SeasonFromUtilsSeason(UtilsSeason season)
        {
            return season switch
            {
                UtilsSeason.Spring => Season.Spring,
                UtilsSeason.Summer => Season.Summer,
                UtilsSeason.Fall => Season.Fall,
                UtilsSeason.Winter => Season.Winter,
                UtilsSeason.Greenhouse => throw new NotSupportedException("Season from StardewValley.Season enum doesn't contain Greenhouse"),
                _ => Season.Spring
            };
        }
    }
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member