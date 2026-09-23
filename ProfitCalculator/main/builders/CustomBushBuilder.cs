using ProfitCalculator.apis;
using ProfitCalculator.main.memory;
using ProfitCalculator.main.models;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TokenizableStrings;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ProfitCalculator.main.builders
{
    /// <summary>
    /// The CustomBushBuilder class builds the bushes added through the Custom Bush mod (furyx639.CustomBush).
    /// Returns nothing when the mod isn't installed.
    /// </summary>
    public class CustomBushBuilder : IDataBuilder
    {
        /// <inheritdoc/>
        public Dictionary<string, PlantData> BuildCrops()
        {
            Dictionary<string, PlantData> bushes = new();
            var Monitor = Container.Instance.GetInstance<IMonitor>(ModEntry.UniqueID);
            var api = Container.Instance.GetInstance<ICustomBushApi>(ModEntry.UniqueID);
            if (api is null)
            {
                return bushes;
            }

            IEnumerable<ICustomBushData> allBushes;
            try
            {
                allBushes = api.GetAllBushes()?.ToList() ?? new List<ICustomBushData>();
            }
            catch (Exception e)
            {
                Monitor?.Log($"Error reading bushes from Custom Bush: {e.Message}", LogLevel.Error);
                return bushes;
            }

            foreach (ICustomBushData bush in allBushes)
            {
                try
                {
                    if (bush is null || string.IsNullOrEmpty(bush.Id) || bushes.ContainsKey(bush.Id))
                    {
                        continue;
                    }
                    BushData? built = BuildCrop(api, bush);
                    if (built is not null)
                    {
                        bushes.Add(bush.Id, built);
                    }
                }
                catch (Exception e)
                {
                    Monitor?.Log($"Error building custom bush {bush?.Id}: {e.Message}", LogLevel.Error);
                }
            }
            Monitor?.Log($"Custom bushes loaded: {bushes.Count}", LogLevel.Debug);
            return bushes;
        }

        /// <summary>
        /// Builds a BushData object from the given Custom Bush data.
        /// </summary>
        /// <param name="api">The Custom Bush API, used to read the drops.</param>
        /// <param name="bush">The Custom Bush data.</param>
        /// <returns>The BushData object, or null if the seed doesn't resolve or the bush has no valid drops.</returns>
        private static BushData? BuildCrop(ICustomBushApi api, ICustomBushData bush)
        {
            Item? seed = ItemRegistry.Create(bush.Id, allowNull: true);
            if (seed is null)
            {
                return null;
            }

            if (!api.TryGetDrops(bush.Id, out IList<ICustomBushDrop>? drops) || drops is null)
            {
                return null;
            }

            DropInformation dropInformation = new();
            foreach (ICustomBushDrop drop in drops)
            {
                if (drop is null || string.IsNullOrEmpty(drop.ItemId))
                {
                    continue;
                }
                Item? item = ItemRegistry.Create(drop.ItemId, allowNull: true);
                if (item is null)
                {
                    continue;
                }
                double averageStack = AverageStack(drop.MinStack, drop.MaxStack);
                double chance = drop.Chance;
                int quantity = (int)averageStack;
                // Drop.Quantity is an int, so a fractional average stack is folded into the chance to keep the expected value exact.
                if (quantity != averageStack)
                {
                    chance *= averageStack;
                    quantity = 1;
                }
                dropInformation.Drops.Add(new DropInformation.Drop(item, quantity, chance, drop.Season));
            }
            if (dropInformation.Drops.Count == 0)
            {
                return null;
            }

            string displayName = string.IsNullOrEmpty(bush.DisplayName)
                ? dropInformation.Drops[0].Item.DisplayName
                : TokenParser.ParseText(bush.DisplayName);

            return new BushData(
                displayName,
                seed,
                dropInformation,
                bush.AgeToProduce,
                bush.DayToBeginProducing,
                ResolveSeasons(bush)
                );
        }

        /// <summary>
        /// Average stack size of a spawned item. A max stack at or below the min stack means the min stack is always used.
        /// </summary>
        /// <param name="minStack">Minimum stack size.</param>
        /// <param name="maxStack">Maximum stack size, or -1 if unset.</param>
        /// <returns>The expected stack size, at least 1.</returns>
        private static double AverageStack(int minStack, int maxStack)
        {
            int min = Math.Max(1, minStack);
            if (maxStack > min)
            {
                return (min + maxStack) / 2.0;
            }
            return min;
        }

        /// <summary>
        /// Gets the seasons the bush produces in. Uses <see cref="ICustomBushData.Seasons"/> if set, otherwise parses
        /// <c>SEASON</c> and <c>LOCATION_SEASON</c> game state queries from <see cref="ICustomBushData.ConditionsToProduce"/>,
        /// and falls back to all four seasons.
        /// </summary>
        /// <param name="bush">The Custom Bush data.</param>
        /// <returns>The list of seasons.</returns>
        private static List<Season> ResolveSeasons(ICustomBushData bush)
        {
            List<Season> seasons = bush.Seasons?.Distinct().ToList() ?? new List<Season>();
            if (seasons.Count > 0)
            {
                return seasons;
            }

            foreach (string condition in bush.ConditionsToProduce ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(condition))
                {
                    continue;
                }
                // Several queries can be combined with commas in a single condition string.
                foreach (string query in condition.Split(','))
                {
                    string[] tokens = query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length < 2)
                    {
                        continue;
                    }
                    int firstSeasonToken;
                    if (tokens[0].Equals("SEASON", StringComparison.OrdinalIgnoreCase))
                    {
                        firstSeasonToken = 1;
                    }
                    else if (tokens[0].Equals("LOCATION_SEASON", StringComparison.OrdinalIgnoreCase))
                    {
                        // LOCATION_SEASON <location> <seasons>+
                        firstSeasonToken = 2;
                    }
                    else
                    {
                        // Negated (!SEASON) and other queries are ignored.
                        continue;
                    }
                    for (int i = firstSeasonToken; i < tokens.Length; i++)
                    {
                        if (Enum.TryParse(tokens[i], true, out Season season) && Enum.IsDefined(season) && !seasons.Contains(season))
                        {
                            seasons.Add(season);
                        }
                    }
                }
            }

            if (seasons.Count == 0)
            {
                seasons = new List<Season> { Season.Spring, Season.Summer, Season.Fall, Season.Winter };
            }
            return seasons;
        }
    }
}
