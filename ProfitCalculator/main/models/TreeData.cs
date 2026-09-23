using ProfitCalculator.main.accessors;
using StardewValley;
using StardewValley.GameData.FruitTrees;
using System;
using System.Collections.Generic;
using System.Linq;
using static ProfitCalculator.Utils;

#nullable enable

namespace ProfitCalculator.main.models
{
    /// <summary>
    /// Class <c>TreeData</c> models a fruit tree over <see cref="Calculator.Years"/> years, simulated day by day.
    /// The sapling takes <see cref="MaturityDays"/> days to mature in any season, then drops one fruit per day while in one
    /// of its <see cref="PlantData.Seasons"/> (every day in the greenhouse). Fruit quality rises with the tree's age, as in
    /// <c>FruitTree.GetQuality()</c>: one quality level per <see cref="PlantingCalendar.DaysPerYear"/> days since maturing,
    /// up to iridium.
    /// </summary>
    public class TreeData : PlantData
    {
        /// <summary> Days a sapling needs to mature (<c>FruitTree.daysUntilMature</c> starts at 28), in any season. </summary>
        public const int MaturityDays = 28;

        /// <summary> Highest quality index (iridium) in <see cref="Calculator.PriceMultipliers"/>. </summary>
        private const int MaxQualityIndex = 3;

        /// <summary> Number of <see cref="UtilsSeason"/> values, Greenhouse included. </summary>
        private const int SeasonCount = 5;

        private static readonly double[] DefaultPriceMultipliers = { 1.0, 1.25, 1.5, 2.0 };

        /// <summary> Last simulation, reused while the planting date and years don't change. </summary>
        private Simulation? cachedSimulation;

        /// <summary>
        /// Constructor for <c>TreeData</c> class. It's used to create a new instance of the class.
        /// </summary>
        /// <param name="_cropData">Fruit tree's full Data</param>
        /// <param name="_seed" >Sapling Item</param>
        /// <param name="dropInformation">Drop Information for the tree</param>
        public TreeData(FruitTreeData _cropData, Item _seed, DropInformation dropInformation)
            : base(
                  MaturityDays,
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

        #region Simulation

        /// <summary>
        /// Fruit days of a tree planted on a given date, over a given window, grouped by the day's season and the fruit quality.
        /// </summary>
        private sealed class Simulation
        {
            public Simulation(UtilsSeason plantingSeason, int plantingDay, int window)
            {
                PlantingSeason = plantingSeason;
                PlantingDay = plantingDay;
                Window = window;
            }

            public UtilsSeason PlantingSeason { get; }

            public int PlantingDay { get; }

            public int Window { get; }

            /// <summary> Fruit days in planting order: days after planting, the day's season and the quality index. </summary>
            public List<(int Day, UtilsSeason Season, int Quality)> FruitDays { get; } = new();

            /// <summary> Fruit count by [season, quality index]. </summary>
            public int[,] Counts { get; } = new int[SeasonCount, MaxQualityIndex + 1];

            public int Harvests => FruitDays.Count;
        }

        /// <summary> Years in the time frame, from the calculator (at least 1). </summary>
        private static int Years => (int)Math.Max(1u, Calc?.Years ?? 1u);

        /// <summary>
        /// Simulates the window day by day. Day <c>t</c> goes from 0 (the planting day) to the window minus 1, so a window of
        /// 112 days covers exactly one year. The tree is mature from <c>t = </c><see cref="MaturityDays"/> on, and its fruit
        /// quality index is <c>(t - MaturityDays) / 112</c>, capped at iridium.
        /// </summary>
        /// <param name="plantingSeason"> Planting Season of type UtilsSeason <see cref="UtilsSeason"/></param>
        /// <param name="plantingDay"> Planting day of the month.</param>
        /// <returns> The simulation. </returns>
        private Simulation Simulate(UtilsSeason plantingSeason, int plantingDay)
        {
            int window = TotalAvailableDays(plantingSeason, plantingDay);
            Simulation? cached = cachedSimulation;
            if (cached is not null && cached.PlantingSeason == plantingSeason && cached.PlantingDay == plantingDay && cached.Window == window)
            {
                return cached;
            }

            Simulation simulation = new(plantingSeason, plantingDay, window);
            bool greenhouse = plantingSeason == UtilsSeason.Greenhouse;
            for (int t = MaturityDays; t < window; t++)
            {
                if (!greenhouse && !Seasons.Contains(PlantingCalendar.SeasonAt(plantingSeason, plantingDay, t)))
                {
                    continue;
                }
                UtilsSeason season = PlantingCalendar.UtilsSeasonAt(plantingSeason, plantingDay, t);
                int quality = Math.Min(MaxQualityIndex, (t - MaturityDays) / PlantingCalendar.DaysPerYear);
                simulation.FruitDays.Add((t, season, quality));
                simulation.Counts[(int)season, quality]++;
            }
            cachedSimulation = simulation;
            return simulation;
        }

        /// <summary> The simulation for the calculator's planting date. </summary>
        private Simulation CurrentSimulation()
        {
            return Simulate(Calc?.Season ?? UtilsSeason.Spring, (int)(Calc?.Day ?? 0));
        }

        /// <summary>
        /// Value of one fruit by [season, quality index] for <paramref name="produceType"/>: when sold raw, the season's drop
        /// price (with the Tiller bonus) times the quality multiplier; for a machine, the machine value per harvest in that
        /// season (machine products ignore quality). Only seasons with fruit in <paramref name="simulation"/> are filled.
        /// </summary>
        /// <param name="simulation"> The simulation. </param>
        /// <param name="produceType"> The produce type id. </param>
        /// <returns> The values. </returns>
        private double[,] FruitValues(Simulation simulation, string produceType)
        {
            double[] multipliers = Calc?.PriceMultipliers ?? DefaultPriceMultipliers;
            bool raw = IsSoldRaw(produceType);
            double cropsPerHarvest = TotalCropsPerHarvest();
            double[,] values = new double[SeasonCount, MaxQualityIndex + 1];
            for (int s = 0; s < SeasonCount; s++)
            {
                if (!HasFruitIn(simulation, s))
                {
                    continue;
                }
                UtilsSeason season = (UtilsSeason)s;
                double seasonValue = raw
                    ? DropInformation.AveragePrice(season, true) * cropsPerHarvest
                    : ArtisanValuePerHarvest(season, produceType);
                for (int q = 0; q <= MaxQualityIndex; q++)
                {
                    double multiplier = raw && q < multipliers.Length ? multipliers[q] : 1.0;
                    values[s, q] = seasonValue * multiplier;
                }
            }
            return values;
        }

        private static bool HasFruitIn(Simulation simulation, int season)
        {
            for (int q = 0; q <= MaxQualityIndex; q++)
            {
                if (simulation.Counts[season, q] > 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary> Share of the window's fruit at quality index <paramref name="quality"/>. </summary>
        private double QualityShare(int quality)
        {
            Simulation simulation = CurrentSimulation();
            if (simulation.Harvests == 0)
            {
                return quality == 0 ? 1 : 0;
            }
            int count = 0;
            for (int s = 0; s < SeasonCount; s++)
            {
                count += simulation.Counts[s, quality];
            }
            return count / (double)simulation.Harvests;
        }

        #endregion Simulation

        #region Growth Values Calculations

        /// <summary>
        /// A sapling can be planted in any season: it matures in any season and fruits once its season comes around.
        /// </summary>
        /// <param name="currentSeason"> Ignored. </param>
        /// <returns> Always true. </returns>
        public override bool IsAvailableForCurrentSeason(UtilsSeason currentSeason)
        {
            return true;
        }

        /// <summary>
        /// The time frame is <see cref="Calculator.Years"/> years of <see cref="PlantingCalendar.DaysPerYear"/> days, starting on the planting day.
        /// </summary>
        /// <param name="currentSeason"> Ignored. </param>
        /// <param name="day"> Ignored. </param>
        /// <returns> Total days in the time frame. <c>int</c></returns>
        public override int TotalAvailableDays(UtilsSeason currentSeason, int day)
        {
            return Years * PlantingCalendar.DaysPerYear;
        }

        /// <summary>
        /// Counts the fruit days in the time frame, simulating each day after planting: the tree is mature after
        /// <see cref="MaturityDays"/> days and fruits on days in one of its seasons (any day in the greenhouse).
        /// </summary>
        /// <param name="currentSeason"> Planting Season of type UtilsSeason <see cref="UtilsSeason"/></param>
        /// <param name="fertilizerQuality"> Ignored, fruit trees can't be fertilized.</param>
        /// <param name="day"> Planting day of the month.</param>
        /// <returns> Total number of fruit for the time frame. <c>int</c></returns>
        public override int TotalHarvestsWithRemainingDays(UtilsSeason currentSeason, FertilizerQuality fertilizerQuality, int day)
        {
            return Simulate(currentSeason, day).Harvests;
        }

        #endregion Growth Values Calculations

        #region Crop Profit Calculations

        /// <summary>
        /// Total sale value of the fruit over the time frame. Each fruit day counts the drop of that day's season: sold raw
        /// at its price times the quality multiplier for the tree's age that day, or through the selected machine.
        /// </summary>
        /// <returns> Total value of all fruit. <c>double</c></returns>
        public override double TotalCropProfit()
        {
            Simulation simulation = CurrentSimulation();
            double[,] values = FruitValues(simulation, Calc?.ProduceType ?? RawProduceType);
            double total = 0;
            for (int s = 0; s < SeasonCount; s++)
            {
                for (int q = 0; q <= MaxQualityIndex; q++)
                {
                    total += simulation.Counts[s, q] * values[s, q];
                }
            }
            return total;
        }

        /// <summary>
        /// Expected number of items sold over the time frame, counting each fruit day in its own season: fruit when sold
        /// raw, otherwise machine products.
        /// </summary>
        /// <param name="produceType"> The produce type id. </param>
        /// <returns> Expected number of items sold. <c>double</c></returns>
        public override double TotalProduceCount(string produceType)
        {
            Simulation simulation = CurrentSimulation();
            MachineAccessor? machines = Machines;
            bool raw = IsSoldRaw(produceType);
            double total = 0;
            for (int s = 0; s < SeasonCount; s++)
            {
                int count = 0;
                for (int q = 0; q <= MaxQualityIndex; q++)
                {
                    count += simulation.Counts[s, q];
                }
                if (count == 0)
                {
                    continue;
                }
                double perCrop = DropInformation.AverageValue((UtilsSeason)s, drop =>
                {
                    if (raw)
                    {
                        return 1;
                    }
                    return machines is not null && machines.TryGetProduct(drop, produceType, out ProductInfo? product) ? product.ProductsPerInput : null;
                });
                total += count * perCrop;
            }
            return total * TotalCropsPerHarvest();
        }

        /// <summary>
        /// Whether the produce type <paramref name="produceType"/> can process the fruit: every drop that counts in a season
        /// the tree fruits in during the time frame must be accepted by the machine. Sold raw is always possible.
        /// </summary>
        /// <param name="produceType"> The produce type id. </param>
        /// <returns> Whether the tree can be sold as that produce type. </returns>
        public override bool CanProduce(string produceType)
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
            Simulation simulation = CurrentSimulation();
            List<UtilsSeason> fruitSeasons = Enumerable.Range(0, SeasonCount)
                .Where(s => HasFruitIn(simulation, s))
                .Select(s => (UtilsSeason)s)
                .ToList();
            if (fruitSeasons.Count == 0)
            {
                return base.CanProduce(produceType);
            }
            List<DropInformation.Drop> counting = DropInformation.Drops
                .Where(drop => fruitSeasons.Any(season => drop.CountsIn(season)))
                .ToList();
            return counting.Count > 0 && counting.All(drop => machines.TryGetProduct(drop, produceType, out _));
        }

        /// <summary>
        /// First day (counted from planting) on which the running total of fruit value for the selected produce type reaches
        /// the sapling cost (<see cref="PlantData.TotalSeedsCost"/>). When seeds are free it's the first fruit day.
        /// </summary>
        /// <returns> Days after planting, or -1 if the tree doesn't pay back within the time frame. <c>int</c></returns>
        public override int PaybackDay()
        {
            Simulation simulation = CurrentSimulation();
            double[,] values = FruitValues(simulation, Calc?.ProduceType ?? RawProduceType);
            double cost = TotalSeedsCost();
            double cumulative = 0;
            foreach ((int day, UtilsSeason season, int quality) in simulation.FruitDays)
            {
                cumulative += values[(int)season, quality];
                if (cumulative >= cost)
                {
                    return day;
                }
            }
            return -1;
        }

        /// <summary>
        /// Fruit trees can't be fertilized.
        /// </summary>
        /// <returns> Always 0. </returns>
        public override int TotalFertilizerNeeded()
        {
            return 0;
        }

        #endregion Crop Profit Calculations

        #region Crop Modifer Value Calculations

        /// <summary> Share of the time frame's fruit at base quality (the tree's first year after maturing). </summary>
        /// <returns> The share, 1 when the tree doesn't fruit. </returns>
        public override double GetCropBaseQualityChance()
        {
            return QualityShare(0);
        }

        /// <summary> Share of the time frame's fruit at silver quality (the tree's second year after maturing). </summary>
        /// <returns> The share. </returns>
        public override double GetCropSilverQualityChance()
        {
            return QualityShare(1);
        }

        /// <summary> Share of the time frame's fruit at gold quality (the tree's third year after maturing). </summary>
        /// <returns> The share. </returns>
        public override double GetCropGoldQualityChance()
        {
            return QualityShare(2);
        }

        /// <summary> Share of the time frame's fruit at iridium quality (from the tree's fourth year after maturing). </summary>
        /// <returns> The share. </returns>
        public override double GetCropIridiumQualityChance()
        {
            return QualityShare(3);
        }

        #endregion Crop Modifer Value Calculations
    }
}
