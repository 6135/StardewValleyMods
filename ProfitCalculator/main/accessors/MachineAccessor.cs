using ProfitCalculator.main.memory;
using ProfitCalculator.main.models;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Machines;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using static ProfitCalculator.Utils;
using SObject = StardewValley.Object;

#nullable enable

namespace ProfitCalculator.main.accessors
{
    /// <summary>
    /// What a machine makes from one kind of plant drop: the product, its value per input item and how long it takes.
    /// </summary>
    public sealed class ProductInfo
    {
        /// <summary> The main product (the most likely output when the machine picks one at random). </summary>
        public Item Output { get; }

        /// <summary> Sale value of the products per input item, after the sale profession bonuses and divided by <see cref="RequiredCount"/>. </summary>
        public double ValuePerInput { get; }

        /// <summary> Number of input items the machine takes for one batch (for example 5 fruit in a Dehydrator). </summary>
        public int RequiredCount { get; }

        /// <summary> Expected number of products per batch. </summary>
        public double ExpectedStack { get; }

        /// <summary> Expected number of products per input item: <see cref="ExpectedStack"/> divided by <see cref="RequiredCount"/>. </summary>
        public double ProductsPerInput => ExpectedStack / RequiredCount;

        /// <summary> Days the machine (or machines, for a chained option) takes to make a batch. </summary>
        public double ProcessingDays { get; }

        /// <summary>
        /// Creates the product information.
        /// </summary>
        /// <param name="output"> The main product. </param>
        /// <param name="valuePerInput"> Sale value per input item. </param>
        /// <param name="requiredCount"> Input items per batch. </param>
        /// <param name="expectedStack"> Expected products per batch. </param>
        /// <param name="processingDays"> Days to make a batch. </param>
        public ProductInfo(Item output, double valuePerInput, int requiredCount, double expectedStack, double processingDays)
        {
            Output = output;
            ValuePerInput = valuePerInput;
            RequiredCount = Math.Max(1, requiredCount);
            ExpectedStack = expectedStack;
            ProcessingDays = processingDays;
        }
    }

    /// <summary>
    /// Reads <c>Data/Machines</c> and works out what every machine makes from the drops of the plants the
    /// <see cref="Calculator"/> knows. The results are cached; call <see cref="InvalidateCaches"/> when the plants or the
    /// game data change and the cache rebuilds on the next access.
    /// </summary>
    /// <remarks>
    /// Produce type ids are <see cref="RawProduceType"/>, a machine's qualified id (for example <c>(BC)12</c>) or a chained
    /// aging id <c>first&gt;aging machine</c> (for example <c>(BC)12&gt;(BC)163</c>, Keg then Cask).
    /// </remarks>
    public class MachineAccessor
    {
        /// <summary> Separator between the two machine ids of a chained produce type id. </summary>
        public const char ChainSeparator = '>';

        /// <summary> Days a cask takes to age an item to iridium quality at an aging multiplier of 1. </summary>
        private const double CaskDaysToIridium = 56;

        /// <summary> Minutes in a day as counted by machine timers. </summary>
        private const double MinutesPerDay = 1440;

        private const int ProbeStack = 999;

        private readonly Cache<MachineCatalog> catalog;

        /// <summary>
        /// Initializes a new instance of the <see cref="MachineAccessor"/> class. The cache is built right away and is
        /// empty until a save with plants is loaded.
        /// </summary>
        public MachineAccessor()
        {
            catalog = new(BuildCatalog);
        }

        /// <summary>
        /// Invalidates the cached machine products; they are rebuilt on the next access.
        /// </summary>
        public void InvalidateCaches()
        {
            catalog.InvalidateCache();
        }

        /// <summary>
        /// The produce types the player can choose: <see cref="RawProduceType"/> first, then
        /// <see cref="FruitTreesProduceType"/> and <see cref="WildTreesProduceType"/>, then every machine (and chained
        /// aging option) that accepts at least one plant's drop, sorted by display name. Wild tree tapper products never
        /// create or join a machine option.
        /// </summary>
        /// <returns> The produce type ids with their display labels. </returns>
        public IReadOnlyList<(string Id, string Label)> GetProduceOptions()
        {
            return catalog.GetCache().Options;
        }

        /// <summary>
        /// Gets what the machine (or chained option) <paramref name="produceId"/> makes from <paramref name="drop"/>.
        /// Produce types sold raw (<see cref="Utils.IsSoldRaw"/>) have no machine product.
        /// </summary>
        /// <param name="drop"> The plant drop put in the machine. </param>
        /// <param name="produceId"> The produce type id. </param>
        /// <param name="info"> The product, when the machine accepts the drop. </param>
        /// <returns> Whether the machine accepts the drop. </returns>
        public bool TryGetProduct(DropInformation.Drop drop, string produceId, [NotNullWhen(true)] out ProductInfo? info)
        {
            info = null;
            if (drop?.Item is null || string.IsNullOrEmpty(produceId) || IsSoldRaw(produceId))
            {
                return false;
            }
            if (!catalog.GetCache().Recipes.TryGetValue(produceId, out var byInput) || !byInput.TryGetValue(InputKey(drop.Item), out Recipe? recipe))
            {
                return false;
            }
            info = recipe.ToProductInfo();
            return true;
        }

        #region Cache building

        /// <summary> One possible output of a recipe. </summary>
        private sealed class Output
        {
            public Item Item = null!;
            public int BasePrice;
            public double Weight;
            public double ExpectedStack;
            public double ProcessingDays;
        }

        /// <summary> What a machine does with one input item: the ratio and the (weighted) outputs. </summary>
        private sealed class Recipe
        {
            public int RequiredCount = 1;
            public List<Output> Outputs = new();

            public ProductInfo ToProductInfo()
            {
                double value = 0;
                double stack = 0;
                double days = 0;
                foreach (Output output in Outputs)
                {
                    value += output.Weight * ApplySaleBonuses(output.Item, output.BasePrice) * output.ExpectedStack;
                    stack += output.Weight * output.ExpectedStack;
                    days += output.Weight * output.ProcessingDays;
                }
                Item main = Outputs.OrderByDescending(o => o.Weight).First().Item;
                return new ProductInfo(main, value / Math.Max(1, RequiredCount), RequiredCount, stack, days);
            }
        }

        /// <summary> The cached results. </summary>
        private sealed class MachineCatalog
        {
            /// <summary> Produce type id → input key → recipe. </summary>
            public Dictionary<string, Dictionary<string, Recipe>> Recipes = new();

            public List<(string Id, string Label)> Options = new();
        }

        /// <summary> A machine loaded from <c>Data/Machines</c>. </summary>
        private sealed class LoadedMachine
        {
            public string Id = "";
            public SObject Machine = null!;
            public MachineData Data = null!;
        }

        /// <summary>
        /// The key a drop is looked up by. The price is part of it because flavored outputs take the input's price,
        /// and manual crops can override it.
        /// </summary>
        private static string InputKey(Item item)
        {
            return item is SObject obj ? $"{item.QualifiedItemId}|{obj.Price}" : item.QualifiedItemId;
        }

        private static IMonitor? Monitor => Container.Instance.GetInstance<IMonitor>(ModEntry.UniqueID);

        private static MachineCatalog BuildCatalog()
        {
            MachineCatalog result = new();
            try
            {
                BuildCatalog(result);
            }
            catch (Exception e)
            {
                Monitor?.Log($"Failed to read the machine data: {e.Message}", LogLevel.Error);
                result.Recipes.Clear();
            }
            result.Options = BuildOptions(result);
            return result;
        }

        private static void BuildCatalog(MachineCatalog result)
        {
            Calculator? calculator = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID);
            if (!Context.IsWorldReady || calculator is null || calculator.Crops.Count == 0 || Game1.player is null)
            {
                return;
            }
            GameLocation location = Game1.getFarm();
            List<LoadedMachine> machines = LoadMachines(location);

            // one input per distinct drop; tapper products never go into machines
            Dictionary<string, Item> inputs = new();
            foreach (PlantData plant in calculator.Crops.Values)
            {
                if (plant is WildTreeData)
                {
                    continue;
                }
                foreach (DropInformation.Drop drop in plant.DropInformation?.Drops ?? new List<DropInformation.Drop>())
                {
                    if (drop?.Item is not null)
                    {
                        inputs.TryAdd(InputKey(drop.Item), drop.Item);
                    }
                }
            }

            foreach (LoadedMachine machine in machines)
            {
                Dictionary<string, Recipe> byInput = new();
                foreach ((string key, Item item) in inputs)
                {
                    Recipe? recipe = TryBuildRecipe(machine, item, location);
                    if (recipe is not null)
                    {
                        byInput[key] = recipe;
                    }
                }
                if (byInput.Count > 0)
                {
                    result.Recipes[machine.Id] = byInput;
                }
            }

            AddAgingChains(result, machines, location, calculator);
        }

        /// <summary> Every machine that can take items, created on the farm so location based conditions work. </summary>
        private static List<LoadedMachine> LoadMachines(GameLocation location)
        {
            List<LoadedMachine> machines = new();
            foreach ((string id, MachineData data) in DataLoader.Machines(Game1.content))
            {
                if (data is null || data.IsIncubator || data.OutputRules is null)
                {
                    continue;
                }
                try
                {
                    SObject? machine = ItemRegistry.Create<SObject>(id, allowNull: true);
                    if (machine is null)
                    {
                        continue;
                    }
                    machine.Location = location;
                    machines.Add(new LoadedMachine { Id = id, Machine = machine, Data = data });
                }
                catch (Exception e)
                {
                    Monitor?.Log($"Skipping machine {id}: {e.Message}", LogLevel.Trace);
                }
            }
            return machines;
        }

        /// <summary> What <paramref name="machine"/> makes from <paramref name="item"/>, or null when it rejects it. </summary>
        private static Recipe? TryBuildRecipe(LoadedMachine machine, Item item, GameLocation location)
        {
            try
            {
                // a large stack so RequiredCount never blocks the match
                Item input = item.getOne();
                input.Stack = ProbeStack;
                if (!MachineDataUtility.TryGetMachineOutputRule(machine.Machine, machine.Data, MachineOutputTrigger.ItemPlacedInMachine, input, Game1.player, location, out MachineOutputRule rule, out MachineOutputTriggerRule trigger, out _, out _)
                    || rule?.OutputItem is null)
                {
                    return null;
                }

                Item single = item.getOne();
                List<Output> outputs = new();
                foreach (MachineItemOutput outputData in rule.OutputItem)
                {
                    if (outputData is null || !GameStateQuery.CheckConditions(outputData.Condition, location, Game1.player, inputItem: single))
                    {
                        continue;
                    }
                    Output? output = TryGetOutput(machine, rule, outputData, single);
                    if (output is null)
                    {
                        continue;
                    }
                    outputs.Add(output);
                    if (rule.UseFirstValidOutput)
                    {
                        break;
                    }
                }
                if (outputs.Count == 0)
                {
                    return null;
                }
                // the game picks one valid output at random
                foreach (Output output in outputs)
                {
                    output.Weight = 1.0 / outputs.Count;
                }
                return new Recipe { RequiredCount = Math.Max(1, trigger?.RequiredCount ?? 1), Outputs = outputs };
            }
            catch (Exception e)
            {
                Monitor?.Log($"Failed to check {item.QualifiedItemId} in machine {machine.Id}: {e.Message}", LogLevel.Trace);
                return null;
            }
        }

        private static Output? TryGetOutput(LoadedMachine machine, MachineOutputRule rule, MachineItemOutput outputData, Item single)
        {
            Item? product;
            int? overrideMinutes;
            try
            {
                // applies FLAVORED_ITEM, CopyPrice and PriceModifiers
                product = MachineDataUtility.GetOutputItem(machine.Machine, outputData, single, Game1.player, true, out overrideMinutes);
            }
            catch (Exception e)
            {
                Monitor?.Log($"Failed to probe an output of machine {machine.Id}: {e.Message}", LogLevel.Trace);
                return null;
            }
            if (product is null)
            {
                return null;
            }
            double days;
            if (overrideMinutes is > 0)
            {
                days = overrideMinutes.Value / MinutesPerDay;
            }
            else if (rule.DaysUntilReady > 0)
            {
                days = rule.DaysUntilReady;
            }
            else
            {
                days = Math.Max(0, rule.MinutesUntilReady) / MinutesPerDay;
            }
            return new Output
            {
                Item = product,
                BasePrice = BasePrice(product),
                ExpectedStack = ExpectedStack(outputData),
                ProcessingDays = days
            };
        }

        /// <summary> The average of the output's stack range, or 1 when no range is set. </summary>
        private static double ExpectedStack(MachineItemOutput output)
        {
            if (output.MinStack <= 0)
            {
                return 1;
            }
            int max = output.MaxStack < output.MinStack ? output.MinStack : output.MaxStack;
            return (output.MinStack + max) / 2.0;
        }

        /// <summary> The price before profession bonuses. </summary>
        private static int BasePrice(Item item)
        {
            return item is SObject obj ? obj.Price : item.sellToStorePrice();
        }

        /// <summary>
        /// Adds a "first machine + aging machine" option for every aging machine (a rule output using <c>OutputCask</c>)
        /// that accepts a product of a machine already listed. The product is aged to iridium quality.
        /// </summary>
        private static void AddAgingChains(MachineCatalog result, List<LoadedMachine> machines, GameLocation location, Calculator calculator)
        {
            List<LoadedMachine> agingMachines = machines.Where(IsAgingMachine).ToList();
            if (agingMachines.Count == 0)
            {
                return;
            }
            double[] multipliers = calculator.PriceMultipliers;
            double iridiumMultiplier = multipliers.Length > 3 ? multipliers[3] : 2.0;

            foreach ((string firstId, Dictionary<string, Recipe> firstRecipes) in result.Recipes.ToList())
            {
                foreach (LoadedMachine aging in agingMachines)
                {
                    if (aging.Id == firstId)
                    {
                        continue;
                    }
                    Dictionary<string, Recipe> chained = new();
                    foreach ((string inputKey, Recipe first) in firstRecipes)
                    {
                        Recipe? recipe = TryChain(first, aging, location, iridiumMultiplier);
                        if (recipe is not null)
                        {
                            chained[inputKey] = recipe;
                        }
                    }
                    if (chained.Count > 0)
                    {
                        result.Recipes[$"{firstId}{ChainSeparator}{aging.Id}"] = chained;
                    }
                }
            }
        }

        private static bool IsAgingMachine(LoadedMachine machine)
        {
            return machine.Data.OutputRules.Any(rule => rule?.OutputItem?.Any(IsAgingOutput) ?? false);
        }

        private static bool IsAgingOutput(MachineItemOutput? output)
        {
            return output?.OutputMethod?.Contains("OutputCask", StringComparison.OrdinalIgnoreCase) ?? false;
        }

        /// <summary>
        /// Chains <paramref name="first"/> into <paramref name="aging"/>: outputs the aging machine accepts are aged to
        /// iridium quality, the others are sold as they are. Null when no output is accepted.
        /// </summary>
        private static Recipe? TryChain(Recipe first, LoadedMachine aging, GameLocation location, double iridiumMultiplier)
        {
            List<Output> outputs = new();
            bool anyAged = false;
            foreach (Output output in first.Outputs)
            {
                Output? aged = TryAge(output, aging, location, iridiumMultiplier);
                if (aged is not null)
                {
                    anyAged = true;
                    outputs.Add(aged);
                }
                else
                {
                    outputs.Add(output);
                }
            }
            return anyAged ? new Recipe { RequiredCount = first.RequiredCount, Outputs = outputs } : null;
        }

        private static Output? TryAge(Output output, LoadedMachine aging, GameLocation location, double iridiumMultiplier)
        {
            try
            {
                Item input = output.Item.getOne();
                input.Stack = ProbeStack;
                if (!MachineDataUtility.TryGetMachineOutputRule(aging.Machine, aging.Data, MachineOutputTrigger.ItemPlacedInMachine, input, Game1.player, location, out MachineOutputRule rule, out MachineOutputTriggerRule trigger, out _, out _)
                    || rule?.OutputItem is null)
                {
                    return null;
                }
                MachineItemOutput? agingOutput = rule.OutputItem.FirstOrDefault(IsAgingOutput);
                if (agingOutput is null)
                {
                    return null;
                }
                double agingMultiplier = 1;
                if (agingOutput.CustomData is not null
                    && agingOutput.CustomData.TryGetValue("AgingMultiplier", out string? raw)
                    && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                    && parsed > 0)
                {
                    agingMultiplier = parsed;
                }

                Item aged = output.Item.getOne();
                aged.Quality = SObject.bestQuality;
                int requiredCount = Math.Max(1, trigger?.RequiredCount ?? 1);
                return new Output
                {
                    Item = aged,
                    BasePrice = (int)(BasePrice(output.Item) * iridiumMultiplier),
                    Weight = output.Weight,
                    ExpectedStack = output.ExpectedStack / requiredCount,
                    ProcessingDays = output.ProcessingDays + (CaskDaysToIridium / agingMultiplier)
                };
            }
            catch (Exception e)
            {
                Monitor?.Log($"Failed to check {output.Item.QualifiedItemId} in machine {aging.Id}: {e.Message}", LogLevel.Trace);
                return null;
            }
        }

        /// <summary> Raw first, then the fruit tree and wild tree views, then the listed machines and chains sorted by label. </summary>
        private static List<(string Id, string Label)> BuildOptions(MachineCatalog result)
        {
            IModHelper? helper = Container.Instance.GetInstance<IModHelper>(ModEntry.UniqueID);
            List<(string Id, string Label)> machines = result.Recipes.Keys
                .Select(id => (Id: id, Label: MachineLabel(id, helper)))
                .OrderBy(option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            List<(string Id, string Label)> options = new()
            {
                (RawProduceType, helper?.Translation.Get("raw").ToString() ?? RawProduceType),
                (FruitTreesProduceType, helper?.Translation.Get("fruit-trees").ToString() ?? FruitTreesProduceType),
                (WildTreesProduceType, helper?.Translation.Get("wild-trees").ToString() ?? WildTreesProduceType)
            };
            options.AddRange(machines);
            return options;
        }

        private static string MachineLabel(string produceId, IModHelper? helper)
        {
            int separator = produceId.IndexOf(ChainSeparator);
            if (separator < 0)
            {
                return DisplayName(produceId);
            }
            string first = DisplayName(produceId[..separator]);
            string second = DisplayName(produceId[(separator + 1)..]);
            return helper?.Translation.Get("produce-chain", new { first, second }).ToString() ?? $"{first} + {second}";
        }

        private static string DisplayName(string itemId)
        {
            try
            {
                return ItemRegistry.GetDataOrErrorItem(itemId).DisplayName;
            }
            catch (Exception)
            {
                return itemId;
            }
        }

        #endregion Cache building
    }
}
