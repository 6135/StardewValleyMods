using ProfitCalculator.main.models;
using System.Collections.Generic;

#nullable enable

namespace ProfitCalculator.main
{
    /// <summary>
    /// Holds the manual crops and seed-price overrides added at runtime through the mod API.
    /// Entries here take precedence over the <see cref="ManualCropsAsset"/> and <see cref="SeedPricesAsset"/> assets.
    /// </summary>
    public class ManualCropRegistry
    {
        /// <summary>Asset name of the manual crops data (<c>Dictionary&lt;string, ManualCropDefinition&gt;</c>, keyed by seed id).</summary>
        public const string ManualCropsAsset = "Mods/6135.ProfitCalculator/ManualCrops";

        /// <summary>Asset name of the seed price data (<c>Dictionary&lt;string, int&gt;</c>, keyed by seed id).</summary>
        public const string SeedPricesAsset = "Mods/6135.ProfitCalculator/SeedPrices";

        private readonly Dictionary<string, ManualCropDefinition> crops = new();
        private readonly Dictionary<string, int> seedPrices = new();
        private readonly object _lock = new();

        /// <summary>
        /// Initializes a new, empty instance of the <see cref="ManualCropRegistry"/> class.
        /// </summary>
        public ManualCropRegistry()
        { }

        /// <summary>
        /// Adds or replaces a crop definition.
        /// </summary>
        /// <param name="seedItemId">Seed item id, qualified or not.</param>
        /// <param name="definition">The crop definition.</param>
        public void SetCrop(string seedItemId, ManualCropDefinition definition)
        {
            lock (_lock)
            {
                crops[NormalizeId(seedItemId)] = definition;
            }
        }

        /// <summary>
        /// Removes a crop definition.
        /// </summary>
        /// <param name="seedItemId">Seed item id, qualified or not.</param>
        /// <returns>True if a definition was removed.</returns>
        public bool RemoveCrop(string seedItemId)
        {
            lock (_lock)
            {
                return crops.Remove(NormalizeId(seedItemId));
            }
        }

        /// <summary>
        /// Gets a copy of all registered crop definitions, keyed by seed id.
        /// </summary>
        /// <returns>A new dictionary with the registered definitions.</returns>
        public Dictionary<string, ManualCropDefinition> GetCrops()
        {
            lock (_lock)
            {
                return new Dictionary<string, ManualCropDefinition>(crops);
            }
        }

        /// <summary>
        /// Sets a seed-price override. A negative price removes the override.
        /// </summary>
        /// <param name="seedItemId">Seed item id, qualified or not.</param>
        /// <param name="price">The seed price.</param>
        public void SetSeedPrice(string seedItemId, int price)
        {
            lock (_lock)
            {
                if (price < 0)
                {
                    foreach (string key in CandidateKeys(seedItemId))
                    {
                        seedPrices.Remove(key);
                    }
                }
                else
                {
                    seedPrices[NormalizeId(seedItemId)] = price;
                }
            }
        }

        /// <summary>
        /// Looks up a seed-price override. Accepts the id qualified (<c>(O)472</c>) or not (<c>472</c>), whatever form it was registered with.
        /// </summary>
        /// <param name="seedItemId">Seed item id.</param>
        /// <param name="price">The override, when found.</param>
        /// <returns>True if an override exists.</returns>
        public bool TryGetSeedPrice(string seedItemId, out int price)
        {
            lock (_lock)
            {
                foreach (string key in CandidateKeys(seedItemId))
                {
                    if (seedPrices.TryGetValue(key, out price))
                    {
                        return true;
                    }
                }
            }
            price = 0;
            return false;
        }

        /// <summary>
        /// The key a seed id is stored under: trimmed, without the <c>(O)</c> prefix. <c>Data/Crops</c> is keyed by bare
        /// object ids, so <c>(O)472</c> and <c>472</c> must land on the same key for a manual crop to replace a built-in one.
        /// </summary>
        /// <param name="id">Seed item id, qualified or not.</param>
        /// <returns>The normalized id.</returns>
        public static string NormalizeId(string id)
        {
            string trimmed = id.Trim();
            return trimmed.StartsWith("(O)", System.StringComparison.OrdinalIgnoreCase) ? trimmed[3..] : trimmed;
        }

        /// <summary>
        /// Returns the id as given, its unqualified form and its <c>(O)</c>-qualified form.
        /// </summary>
        /// <param name="id">The item id.</param>
        /// <returns>The candidate keys, without duplicates.</returns>
        internal static IEnumerable<string> CandidateKeys(string id)
        {
            string trimmed = id.Trim();
            yield return trimmed;
            int close = trimmed.StartsWith('(') ? trimmed.IndexOf(')') : -1;
            if (close > 0)
            {
                yield return trimmed[(close + 1)..];
            }
            else
            {
                yield return "(O)" + trimmed;
            }
        }
    }
}
