using ProfitCalculator.main.models;
using StardewValley;
using StardewValley.GameData.FruitTrees;
using System.Collections.Generic;
using SObject = StardewValley.Object;

namespace ProfitCalculator.main.builders
{
    public class FruitTreeBuilder : IDataBuilder
    {
        /// <inheritdoc/>
        public Dictionary<string, IPlantData> BuildCrops()
        {
            Dictionary<string, FruitTreeData> loadedTrees = DataLoader.FruitTrees(Game1.content);
            Dictionary<string, IPlantData> trees = new();
            foreach (var tree in loadedTrees)
            {
                trees.Add(tree.Key, BuildCrop(tree.Value, tree.Key));
            }
            return trees;
        }

        private static TreeData BuildCrop(FruitTreeData cropData, string id)
        {
            Item seed = new SObject(id, 1);
            return new TreeData(cropData, seed);
        }
    }
}