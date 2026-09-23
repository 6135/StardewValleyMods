using StardewValley;
using StardewValley.GameData;
using StardewValley.GameData.WildTrees;
using StardewValley.ItemTypeDefinitions;
using System;
using System.Collections.Generic;
using System.Linq;
using static ProfitCalculator.Utils;
using GameWildTreeData = StardewValley.GameData.WildTrees.WildTreeData;

#nullable enable

namespace ProfitCalculator.main.models
{
    /// <summary>
    /// Class <c>WildTreeData</c> models a wild tree (oak, maple, pine, mushroom, mahogany, mystic...) grown from its seed and
    /// tapped as soon as it matures, over <see cref="Calculator.Years"/> years, simulated day by day.
    /// <list type="bullet">
    /// <item><b>Growth</b> (<c>Tree.dayUpdate</c>): each day a tree below stage <see cref="MatureStage"/> grows a stage with
    /// probability <c>GrowthChance</c>, or <c>1 - (1 - GrowthChance)(1 - FertilizedGrowthChance)</c> with Tree Fertilizer. It
    /// doesn't grow in winter unless <c>GrowsInWinter</c> or fertilized (<c>Tree.IsInSeason</c>), and always grows in the
    /// greenhouse. The simulation adds the expected growth (the chance) each growing day and the tree matures once that
    /// reaches <see cref="MatureStage"/>, so the maturity day is an average.</item>
    /// <item><b>Tapper</b> (<c>Tree.TryGetTapperOutput</c>): the tapper is placed on the maturity day. Each time an output
    /// is picked (on placement, then every time the previous one is collected, on the day it is ready) the <c>TapItems</c>
    /// are checked in order; the first whose condition, previous item, season and chance pass wins. Its output is ready
    /// <c>max(1, floor(DaysUntilReady x multiplier))</c> days later, with a multiplier of 0.5 for the heavy tapper. If nothing
    /// matches after a previous output, the game retries as if there was none; if still nothing, the tapper stays empty and
    /// the game retries the next day. The simulation tracks the probability of each previous output, so chances and
    /// <c>PREVIOUS_OUTPUT_ID</c> (the mushroom tree repeating its last mushroom) are exact expected values.</item>
    /// <item>Conditions are evaluated on the simulated date for <c>SEASON</c>, <c>LOCATION_SEASON</c> and <c>DAY_OF_MONTH</c>;
    /// other queries are checked against the current game state.</item>
    /// <item>Trees that are a stump in winter (<c>IsStumpDuringWinter</c>) pick no output in winter outside the greenhouse.</item>
    /// <item>Outputs are sold at base quality, with the sale profession bonuses (Tapper) from <see cref="Utils.ApplySaleBonuses"/>.</item>
    /// </list>
    /// </summary>
    public class WildTreeData : PlantData
    {
        /// <summary> Growth stage at which a wild tree is fully grown and can be tapped (<c>Tree.treeStage</c>). </summary>
        public const int MatureStage = 5;

        /// <summary> Time multiplier of the heavy tapper (context tag <c>tapper_multiplier_2</c>, so 1 / 2). </summary>
        public const float HeavyTapperTimeMultiplier = 0.5f;

        /// <summary> Replacement token for the previous output in <c>TapItems</c> item ids. </summary>
        private const string PreviousOutputToken = "PREVIOUS_OUTPUT_ID";

        /// <summary> Probability mass below which a branch of the simulation is dropped. </summary>
        private const double Epsilon = 1e-9;

        /// <summary> Drops used to price each tapper output, by qualified item id. </summary>
        private readonly Dictionary<string, DropInformation.Drop> priceDrops = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Constructor for <c>WildTreeData</c> class. It's used to create a new instance of the class.
        /// </summary>
        /// <param name="treeId">Id of the tree in <c>Data/WildTrees</c>.</param>
        /// <param name="treeData">The tree's game data.</param>
        /// <param name="displayName">Display name of the tree.</param>
        /// <param name="seed">Seed item planted to grow the tree.</param>
        /// <param name="dropInformation">Drop Information for the tapper outputs. Must contain at least one drop.</param>
        public WildTreeData(string treeId, GameWildTreeData treeData, string displayName, Item seed, DropInformation dropInformation)
            : base(
                  0,
                  0,
                  1,
                  1,
                  0f,
                  0f,
                  displayName,
                  treeData.IsStumpDuringWinter
                    ? new List<Season> { Season.Spring, Season.Summer, Season.Fall }
                    : new List<Season> { Season.Spring, Season.Summer, Season.Fall, Season.Winter },
                  seed,
                  false,
                  false,
                  dropInformation
                  )
        {
            TreeId = treeId;
            TreeGameData = treeData;
            foreach (DropInformation.Drop drop in dropInformation.Drops)
            {
                priceDrops.TryAdd(drop.Item.QualifiedItemId, new DropInformation.Drop(drop.Item, 1, 1));
            }
            WildTreeTapItemData? first = treeData.TapItems?.FirstOrDefault();
            if (first is not null)
            {
                MinHarvests = Math.Max(1, first.MinStack);
                MaxHarvests = Math.Max(MinHarvests, first.MaxStack);
                RegrowDays = Math.Max(1, first.DaysUntilReady);
            }
        }

        /// <summary> Id of the tree in <c>Data/WildTrees</c>. </summary>
        public string TreeId { get; }

        /// <summary> The tree's game data. </summary>
        public GameWildTreeData TreeGameData { get; }

        #region Simulation

        /// <summary>
        /// Result of simulating a tree planted on a given date, with the current tapper and fertilizer settings.
        /// </summary>
        private sealed class Simulation
        {
            public Simulation(int window)
            {
                RevenueByDay = new double[window];
            }

            /// <summary> Days after planting on which the tree matures and the tapper is placed, or -1 if it doesn't mature in the window. </summary>
            public int MatureDay { get; set; } = -1;

            /// <summary> Expected number of tapper outputs collected in the window. </summary>
            public double Outputs { get; set; }

            /// <summary> Expected number of items collected in the window (outputs times their stack). </summary>
            public double Items { get; set; }

            /// <summary> Expected sale value of everything collected in the window. </summary>
            public double Value { get; set; }

            /// <summary> Days until ready of the first output picked, after the tapper multiplier. </summary>
            public int FirstInterval { get; set; }

            /// <summary> Expected sale value collected on each day after planting. </summary>
            public double[] RevenueByDay { get; }
        }

        /// <summary> Years in the time frame, from the calculator (at least 1). </summary>
        private static int Years => (int)Math.Max(1u, Calc?.Years ?? 1u);

        /// <summary> Time multiplier of the selected tapper. </summary>
        private static float TapperTimeMultiplier => (Calc?.HeavyTapper ?? false) ? HeavyTapperTimeMultiplier : 1f;

        /// <summary> Whether the tree is fertilized with Tree Fertilizer. </summary>
        private static bool Fertilized => Calc?.TreeFertilizer ?? false;

        /// <summary>
        /// Chance for the tree to grow a stage on a growing day: <c>GrowthChance</c>, or with Tree Fertilizer
        /// <c>1 - (1 - GrowthChance)(1 - FertilizedGrowthChance)</c>, since the game rolls both.
        /// </summary>
        /// <param name="fertilized"> Whether the tree is fertilized.</param>
        /// <returns> The daily growth chance. <c>double</c></returns>
        public double DailyGrowthChance(bool fertilized)
        {
            double chance = Math.Clamp(TreeGameData.GrowthChance, 0f, 1f);
            if (!fertilized)
            {
                return chance;
            }
            double fertilizedChance = Math.Clamp(TreeGameData.FertilizedGrowthChance, 0f, 1f);
            return 1 - ((1 - chance) * (1 - fertilizedChance));
        }

        /// <summary>
        /// Simulates the window day by day. Day <c>t</c> goes from 0 (the planting day) to the window minus 1. The tree grows
        /// overnight, so the first growth happens on day 1. Also updates <see cref="PlantData.Days"/> (days to mature) and
        /// <see cref="PlantData.RegrowDays"/> (days between outputs) so the results tooltip shows them.
        /// </summary>
        /// <param name="plantingSeason"> Planting Season of type UtilsSeason <see cref="UtilsSeason"/></param>
        /// <param name="plantingDay"> Planting day of the month.</param>
        /// <returns> The simulation. </returns>
        private Simulation Simulate(UtilsSeason plantingSeason, int plantingDay)
        {
            int window = TotalAvailableDays(plantingSeason, plantingDay);
            Simulation simulation = new(window);
            bool greenhouse = plantingSeason == UtilsSeason.Greenhouse;
            bool fertilized = Fertilized;
            float timeMultiplier = TapperTimeMultiplier;

            // growth: expected stages, one growth chance per growing day
            double growthChance = DailyGrowthChance(fertilized);
            bool growsInWinter = greenhouse || fertilized || TreeGameData.GrowsInWinter;
            double stage = 0;
            if (growthChance > 0)
            {
                for (int t = 1; t < window; t++)
                {
                    if (!growsInWinter && PlantingCalendar.SeasonAt(plantingSeason, plantingDay, t) == Season.Winter)
                    {
                        continue;
                    }
                    stage += growthChance;
                    if (stage >= MatureStage - Epsilon)
                    {
                        simulation.MatureDay = t;
                        break;
                    }
                }
            }
            Days = simulation.MatureDay >= 0 ? simulation.MatureDay : window;

            if (simulation.MatureDay < 0 || TreeGameData.TapItems is null)
            {
                return simulation;
            }

            // tapper: probability of an output being picked on each day, by previous output ("" for none)
            Dictionary<string, double>?[] picks = new Dictionary<string, double>?[window];
            AddPick(picks, simulation.MatureDay, "", 1);
            for (int t = simulation.MatureDay; t < window; t++)
            {
                Dictionary<string, double>? today = picks[t];
                if (today is null)
                {
                    continue;
                }
                picks[t] = null;
                Season season = PlantingCalendar.SeasonAt(plantingSeason, plantingDay, t);
                int dayOfMonth = PlantingCalendar.DayOfMonth(plantingDay, t);
                bool stump = !greenhouse && season == Season.Winter && TreeGameData.IsStumpDuringWinter;
                foreach ((string previous, double mass) in today)
                {
                    double remaining = mass;
                    if (!stump)
                    {
                        remaining = PickOutput(simulation, picks, plantingSeason, plantingDay, t, season, dayOfMonth, previous, remaining, timeMultiplier);
                        if (previous.Length > 0 && remaining > Epsilon)
                        {
                            remaining = PickOutput(simulation, picks, plantingSeason, plantingDay, t, season, dayOfMonth, "", remaining, timeMultiplier);
                        }
                    }
                    if (remaining > Epsilon)
                    {
                        // nothing to produce: the tapper stays empty and the game retries the next day with no previous output
                        AddPick(picks, t + 1, "", remaining);
                    }
                }
            }
            if (simulation.FirstInterval > 0)
            {
                RegrowDays = simulation.FirstInterval;
            }
            return simulation;
        }

        /// <summary>
        /// Picks the tapper output on day <paramref name="t"/> like <c>Tree.TryGetTapperOutput</c>: the first entry whose
        /// condition, previous item, season and chance pass wins. Records the collection of each picked output on the day it
        /// is ready (if inside the window) and schedules the next pick then.
        /// </summary>
        /// <returns> The probability mass for which no entry was picked. </returns>
        private double PickOutput(Simulation simulation, Dictionary<string, double>?[] picks, UtilsSeason plantingSeason, int plantingDay, int t, Season season, int dayOfMonth, string previous, double mass, float timeMultiplier)
        {
            foreach (WildTreeTapItemData entry in TreeGameData.TapItems)
            {
                if (mass <= Epsilon)
                {
                    break;
                }
                if (!CheckConditions(entry.Condition, season, dayOfMonth))
                {
                    continue;
                }
                if (entry.PreviousItemId is not null && !entry.PreviousItemId.Any(expected => string.IsNullOrEmpty(expected)
                    ? previous.Length == 0
                    : string.Equals(previous, ItemRegistry.QualifyItemId(expected) ?? expected, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                if (entry.Season.HasValue && entry.Season.Value != season)
                {
                    continue;
                }
                List<string> items = ResolveItems(entry, previous);
                if (items.Count == 0)
                {
                    continue;
                }
                double chance = Math.Clamp(entry.Chance, 0f, 1f);
                if (chance <= 0)
                {
                    continue;
                }
                double taken = mass * chance;
                mass -= taken;

                float daysUntilReady = ApplyModifiers(entry.DaysUntilReady, entry.DaysUntilReadyModifiers, entry.DaysUntilReadyModifierMode, season, dayOfMonth);
                int interval = (int)Math.Max(1.0, Math.Floor(daysUntilReady * timeMultiplier));
                if (simulation.FirstInterval == 0)
                {
                    simulation.FirstInterval = interval;
                }
                int readyDay = t + interval;
                if (readyDay >= simulation.RevenueByDay.Length)
                {
                    continue;
                }
                UtilsSeason readySeason = PlantingCalendar.UtilsSeasonAt(plantingSeason, plantingDay, readyDay);
                double stack = AverageStack(entry);
                double share = taken / items.Count;
                foreach (string item in items)
                {
                    double value = share * stack * ItemPrice(item, readySeason);
                    simulation.Outputs += share;
                    simulation.Items += share * stack;
                    simulation.Value += value;
                    simulation.RevenueByDay[readyDay] += value;
                    AddPick(picks, readyDay, item, share);
                }
            }
            return mass;
        }

        /// <summary> Adds <paramref name="mass"/> to the picks of day <paramref name="day"/> with previous output <paramref name="previous"/>, if inside the window. </summary>
        private static void AddPick(Dictionary<string, double>?[] picks, int day, string previous, double mass)
        {
            if (day >= picks.Length)
            {
                return;
            }
            Dictionary<string, double> dayPicks = picks[day] ??= new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            dayPicks[previous] = dayPicks.TryGetValue(previous, out double existing) ? existing + mass : mass;
        }

        /// <summary>
        /// Qualified ids of the object items an entry can produce: one of <c>RandomItemId</c> (equally likely) or
        /// <c>ItemId</c>, with <c>PREVIOUS_OUTPUT_ID</c> replaced by the previous output. Ids that don't resolve to an object
        /// are dropped, as the game skips the entry when it gets no item.
        /// </summary>
        private static List<string> ResolveItems(WildTreeTapItemData entry, string previous)
        {
            IEnumerable<string?> ids = entry.RandomItemId is { Count: > 0 } ? entry.RandomItemId : new List<string?> { entry.ItemId };
            List<string> items = new();
            foreach (string? raw in ids)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }
                string id = raw.Replace(PreviousOutputToken, previous);
                ItemMetadata? metadata = string.IsNullOrWhiteSpace(id) ? null : ItemRegistry.GetMetadata(id);
                if (metadata is not null && metadata.TypeIdentifier == ItemRegistry.type_object && metadata.Exists())
                {
                    items.Add(metadata.QualifiedItemId);
                }
            }
            return items;
        }

        /// <summary> Average stack of an entry: uniform between <c>MinStack</c> and <c>MaxStack</c>, 1 when unset. </summary>
        private static double AverageStack(WildTreeTapItemData entry)
        {
            int min = Math.Max(1, entry.MinStack);
            return entry.MaxStack > min ? (min + entry.MaxStack) / 2.0 : min;
        }

        /// <summary> Sale price of one <paramref name="qualifiedItemId"/> at base quality, with the sale profession bonuses. </summary>
        private double ItemPrice(string qualifiedItemId, UtilsSeason season)
        {
            if (!priceDrops.TryGetValue(qualifiedItemId, out DropInformation.Drop? drop))
            {
                Item? item = ItemRegistry.Create(qualifiedItemId, 1, 0, allowNull: true);
                if (item is null)
                {
                    return 0;
                }
                drop = new DropInformation.Drop(item, 1, 1);
                priceDrops[qualifiedItemId] = drop;
            }
            return drop.Price(season, true);
        }

        /// <summary>
        /// Applies quantity modifiers like <c>Utility.ApplyQuantityModifiers</c>, with conditions checked on the simulated
        /// date and <c>RandomAmount</c> taken as its average.
        /// </summary>
        private static float ApplyModifiers(float value, IList<QuantityModifier>? modifiers, QuantityModifier.QuantityModifierMode mode, Season season, int dayOfMonth)
        {
            if (modifiers is null || modifiers.Count == 0)
            {
                return value;
            }
            float? newValue = null;
            foreach (QuantityModifier modifier in modifiers)
            {
                if (!CheckConditions(modifier.Condition, season, dayOfMonth))
                {
                    continue;
                }
                float amount = modifier.RandomAmount is { Count: > 0 } ? modifier.RandomAmount.Average() : modifier.Amount;
                float applied = QuantityModifier.Apply(mode == QuantityModifier.QuantityModifierMode.Stack ? newValue ?? value : value, modifier.Modification, amount);
                newValue = mode switch
                {
                    QuantityModifier.QuantityModifierMode.Minimum => newValue is null || applied < newValue ? applied : newValue,
                    QuantityModifier.QuantityModifierMode.Maximum => newValue is null || applied > newValue ? applied : newValue,
                    _ => applied
                };
            }
            return newValue ?? value;
        }

        /// <summary>
        /// Checks game state queries on a simulated date. <c>SEASON</c>, <c>LOCATION_SEASON</c> and <c>DAY_OF_MONTH</c> use
        /// <paramref name="season"/> and <paramref name="dayOfMonth"/>; any other query is checked against the current game
        /// state (and counts as passing if it can't be checked).
        /// </summary>
        /// <param name="conditions"> Comma-separated game state queries, or null.</param>
        /// <param name="season"> The simulated season.</param>
        /// <param name="dayOfMonth"> The simulated day of the month.</param>
        /// <returns> Whether all the queries pass. </returns>
        private static bool CheckConditions(string? conditions, Season season, int dayOfMonth)
        {
            if (string.IsNullOrWhiteSpace(conditions))
            {
                return true;
            }
            foreach (string query in GameStateQuery.SplitRaw(conditions))
            {
                string trimmed = query.Trim();
                bool negated = trimmed.StartsWith('!');
                string[] args = (negated ? trimmed[1..] : trimmed).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (args.Length == 0)
                {
                    continue;
                }
                bool? result = args[0].ToUpperInvariant() switch
                {
                    "SEASON" => args.Skip(1).Any(arg => IsSeason(arg, season)),
                    "LOCATION_SEASON" => args.Skip(2).Any(arg => IsSeason(arg, season)),
                    "DAY_OF_MONTH" => args.Skip(1).Any(arg => IsDayOfMonth(arg, dayOfMonth)),
                    _ => null
                };
                bool passes;
                if (result.HasValue)
                {
                    passes = result.Value != negated;
                }
                else
                {
                    try
                    {
                        passes = GameStateQuery.CheckConditions(trimmed, Game1.currentLocation, Game1.player);
                    }
                    catch
                    {
                        passes = true;
                    }
                }
                if (!passes)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary> Whether the query argument <paramref name="arg"/> names <paramref name="season"/>. </summary>
        private static bool IsSeason(string arg, Season season)
        {
            return Enum.TryParse(arg, true, out Season parsed) && parsed == season;
        }

        /// <summary> Whether the query argument <paramref name="arg"/> (a day, <c>even</c> or <c>odd</c>) matches <paramref name="dayOfMonth"/>. </summary>
        private static bool IsDayOfMonth(string arg, int dayOfMonth)
        {
            if (int.TryParse(arg, out int day))
            {
                return day == dayOfMonth;
            }
            if (string.Equals(arg, "even", StringComparison.OrdinalIgnoreCase))
            {
                return dayOfMonth % 2 == 0;
            }
            if (string.Equals(arg, "odd", StringComparison.OrdinalIgnoreCase))
            {
                return dayOfMonth % 2 == 1;
            }
            return false;
        }

        /// <summary> The simulation for the calculator's planting date. </summary>
        private Simulation CurrentSimulation()
        {
            return Simulate(Calc?.Season ?? UtilsSeason.Spring, (int)(Calc?.Day ?? 0));
        }

        #endregion Simulation

        #region Growth Values Calculations

        /// <summary>
        /// Wild tree seeds can be planted in any season (they just don't grow in winter), so trees are always listed.
        /// </summary>
        /// <param name="currentSeason"> Ignored.</param>
        /// <returns> Always true. </returns>
        public override bool IsAvailableForCurrentSeason(UtilsSeason currentSeason)
        {
            return true;
        }

        /// <summary>
        /// The time frame is <see cref="Calculator.Years"/> years of <see cref="PlantingCalendar.DaysPerYear"/> days from
        /// the planting day, in any season and in the greenhouse.
        /// </summary>
        /// <param name="currentSeason"> Ignored.</param>
        /// <param name="day"> Ignored.</param>
        /// <returns> The window in days. <c>int</c></returns>
        public override int TotalAvailableDays(UtilsSeason currentSeason, int day)
        {
            return Years * PlantingCalendar.DaysPerYear;
        }

        /// <summary>
        /// Expected number of tapper outputs collected in the window, rounded to the nearest whole number. Wild trees can be
        /// planted in the greenhouse (<c>GameLocation.CanPlantTreesHere</c> allows it and its soil is diggable), where they
        /// also grow in winter and are never a stump.
        /// </summary>
        /// <param name="currentSeason"> Planting Season of type UtilsSeason <see cref="UtilsSeason"/></param>
        /// <param name="fertilizerQuality"> Ignored, the Tree Fertilizer setting is used instead.</param>
        /// <param name="day"> Planting day of the month.</param>
        /// <returns> Total number of tapper outputs for the available time. <c>int</c></returns>
        public override int TotalHarvestsWithRemainingDays(UtilsSeason currentSeason, FertilizerQuality fertilizerQuality, int day)
        {
            return (int)Math.Round(Simulate(currentSeason, day).Outputs);
        }

        /// <summary> Average items per tapper output (the stack of the outputs), 1 when nothing is produced. </summary>
        /// <returns> Average items per output. <c>double</c></returns>
        public override double AverageCropsPerHarvest()
        {
            Simulation simulation = CurrentSimulation();
            return simulation.Outputs > Epsilon ? simulation.Items / simulation.Outputs : Math.Max(1, MinHarvests);
        }

        /// <summary> Tapper outputs have no extra-crop chance. </summary>
        /// <returns> Always 0. </returns>
        public override double AverageExtraCropsFromRandomness()
        {
            return 0;
        }

        #endregion Growth Values Calculations

        #region Crop Profit Calculations

        /// <summary>
        /// Average sale price of one tapper item over the window (the tree's outputs vary by season for some trees), or the
        /// main drop's price when nothing is collected.
        /// </summary>
        /// <param name="season"> The selected season. </param>
        /// <returns> The average price. <c>int</c></returns>
        public override int Price(UtilsSeason season)
        {
            Simulation simulation = CurrentSimulation();
            if (simulation.Items > Epsilon)
            {
                return (int)Math.Round(simulation.Value / simulation.Items);
            }
            DropInformation.Drop main = MainDrop(season) ?? DropInformation.Drops[0];
            return main.Price(season, true);
        }

        /// <summary> Total expected sale value of the tapper outputs collected in the window. </summary>
        /// <returns> Total value. <c>double</c></returns>
        public override double TotalCropProfit()
        {
            return CurrentSimulation().Value;
        }

        /// <summary> Average sale value of one tapper output over the window. </summary>
        /// <param name="season"> Ignored, the simulation follows the seasons of the window.</param>
        /// <returns> Average value per output. <c>double</c></returns>
        public override double RawValuePerHarvest(UtilsSeason season)
        {
            Simulation simulation = CurrentSimulation();
            return simulation.Outputs > Epsilon ? simulation.Value / simulation.Outputs : 0;
        }

        /// <summary> Tapper products aren't processed by machines, so this is the raw value. </summary>
        /// <param name="season"> Ignored.</param>
        /// <param name="produceType"> Ignored.</param>
        /// <returns> Average value per output. <c>double</c></returns>
        public override double ArtisanValuePerHarvest(UtilsSeason season, string produceType)
        {
            return RawValuePerHarvest(season);
        }

        /// <summary> Tapper products are only sold raw (see <see cref="Utils.IsSoldRaw"/>), never listed for a machine. </summary>
        /// <param name="produceType"> The produce type id. </param>
        /// <returns> Whether the produce type sells the outputs raw. </returns>
        public override bool CanProduce(string produceType)
        {
            return IsSoldRaw(produceType);
        }

        /// <summary> Expected number of items collected in the window. </summary>
        /// <param name="produceType"> Ignored, the outputs are sold raw. </param>
        /// <returns> Expected number of items. <c>double</c></returns>
        public override double TotalProduceCount(string produceType)
        {
            return CurrentSimulation().Items;
        }

        /// <summary>
        /// First day after planting on which the running sale value of the collected outputs covers the seed cost, or -1 if it
        /// never does in the window.
        /// </summary>
        /// <returns> The payback day, or -1. <c>int</c></returns>
        public override int PaybackDay()
        {
            Simulation simulation = CurrentSimulation();
            double cost = TotalSeedsCost();
            double cumulative = 0;
            for (int t = 0; t < simulation.RevenueByDay.Length; t++)
            {
                if (simulation.RevenueByDay[t] <= 0)
                {
                    continue;
                }
                cumulative += simulation.RevenueByDay[t];
                if (cumulative >= cost)
                {
                    return t;
                }
            }
            return -1;
        }

        /// <summary> One seed grows the tree for the whole window. </summary>
        /// <returns> Always 1. </returns>
        public override int TotalSeedsNeeded()
        {
            return 1;
        }

        /// <summary> Crop fertilizer doesn't apply to wild trees (Tree Fertilizer is its own setting). </summary>
        /// <returns> Always 0. </returns>
        public override int TotalFertilizerNeeded()
        {
            return 0;
        }

        #endregion Crop Profit Calculations

        #region Crop Modifer Value Calculations

        /// <inheritdoc/>
        public override double GetCropBaseGoldQualityChance(double limit)
        {
            return 0f;
        }

        /// <inheritdoc/>
        public override double GetCropBaseQualityChance()
        {
            return 1f;
        }

        /// <inheritdoc/>
        public override double GetCropSilverQualityChance()
        {
            return 0f;
        }

        /// <inheritdoc/>
        public override double GetCropGoldQualityChance()
        {
            return 0f;
        }

        /// <inheritdoc/>
        public override double GetCropIridiumQualityChance()
        {
            return 0f;
        }

        #endregion Crop Modifer Value Calculations
    }
}
