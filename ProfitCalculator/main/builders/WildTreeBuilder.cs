using ProfitCalculator.main.memory;
using ProfitCalculator.main.models;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.WildTrees;
using StardewValley.ItemTypeDefinitions;
using System;
using System.Collections.Generic;
using GameWildTreeData = StardewValley.GameData.WildTrees.WildTreeData;
using WildTreeData = ProfitCalculator.main.models.WildTreeData;

#nullable enable

namespace ProfitCalculator.main.builders
{
    /// <summary>
    /// The WildTreeBuilder class builds a dictionary of wild trees (<c>Data/WildTrees</c>) that can be planted from a seed
    /// and tapped. Trees without tapper outputs, with a seed that can't be planted or that doesn't resolve to an item are
    /// skipped. Keys are <c>"WildTree_" + tree id</c>, so they don't clash with crop keys (crops are keyed by seed id).
    /// </summary>
    public class WildTreeBuilder : IDataBuilder
    {
        /// <summary> Prefix of the keys of the built trees. </summary>
        internal const string KeyPrefix = "WildTree_";

        /// <inheritdoc/>
        public Dictionary<string, PlantData> BuildCrops()
        {
            Dictionary<string, GameWildTreeData> loadedTrees = DataLoader.WildTrees(Game1.content);
            Dictionary<string, PlantData> trees = new();
            var Monitor = Container.Instance.GetInstance<IMonitor>(ModEntry.UniqueID);
            Monitor?.Log($"Wild trees loaded: {loadedTrees.Count}", LogLevel.Debug);
            foreach (var tree in loadedTrees)
            {
                try
                {
                    WildTreeData? built = BuildCrop(tree.Value, tree.Key);
                    if (built is not null)
                    {
                        trees.Add(KeyPrefix + tree.Key, built);
                    }
                }
                catch (Exception e)
                {
                    Monitor?.Log($"Skipping wild tree '{tree.Key}': it could not be built.\n{e}", LogLevel.Warn);
                }
            }
            return trees;
        }

        /// <summary>
        /// Builds a WildTreeData object from the given game data and id, or null when the tree can't be planted and tapped.
        /// The drops are the object items its <c>TapItems</c> can produce (one per item, in data order). The display name
        /// is the seed's name followed by the first tapper product, for example "Acorn (Oak Resin)", since wild trees have
        /// no display name of their own.
        /// </summary>
        /// <param name="treeData">The tree's game data.</param>
        /// <param name="id">The id of the tree.</param>
        /// <returns>The WildTreeData object, or null.</returns>
        private static WildTreeData? BuildCrop(GameWildTreeData treeData, string id)
        {
            if (treeData.TapItems is null || treeData.TapItems.Count == 0 || !treeData.SeedPlantable || string.IsNullOrWhiteSpace(treeData.SeedItemId))
            {
                return null;
            }
            ItemMetadata? seedMetadata = ItemRegistry.GetMetadata(treeData.SeedItemId);
            if (seedMetadata is null || !seedMetadata.Exists())
            {
                return null;
            }
            Item? seed = ItemRegistry.Create(seedMetadata.QualifiedItemId, 1, 0, allowNull: true);
            if (seed is null)
            {
                return null;
            }

            DropInformation dropInformation = new() { Name = id };
            HashSet<string> added = new(StringComparer.OrdinalIgnoreCase);
            foreach (WildTreeTapItemData tapItem in treeData.TapItems)
            {
                IEnumerable<string?> ids = tapItem.RandomItemId is { Count: > 0 } ? tapItem.RandomItemId : new List<string?> { tapItem.ItemId };
                foreach (string? itemId in ids)
                {
                    // item queries and PREVIOUS_OUTPUT_ID don't resolve to an item here; the model handles the latter
                    ItemMetadata? metadata = string.IsNullOrWhiteSpace(itemId) ? null : ItemRegistry.GetMetadata(itemId);
                    if (metadata is null || metadata.TypeIdentifier != ItemRegistry.type_object || !metadata.Exists() || !added.Add(metadata.QualifiedItemId))
                    {
                        continue;
                    }
                    Item? item = ItemRegistry.Create(metadata.QualifiedItemId, 1, 0, allowNull: true);
                    if (item is null)
                    {
                        continue;
                    }
                    dropInformation.Drops.Add(new DropInformation.Drop(item, Math.Max(1, tapItem.MinStack), tapItem.Chance, tapItem.Season));
                }
            }
            if (dropInformation.Drops.Count == 0)
            {
                return null;
            }
            string displayName = $"{seed.DisplayName} ({dropInformation.Drops[0].Item.DisplayName})";
            return new WildTreeData(id, treeData, displayName, seed, dropInformation);
        }
    }
}
