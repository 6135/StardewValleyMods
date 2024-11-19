using ProfitCalculator.main.models;
using StardewValley;
using StardewValley.GameData.FruitTrees;
using System.Collections.Generic;
using SObject = StardewValley.Object;

namespace ProfitCalculator.main.builders
{
    /// <summary>
    /// The FruitTreeBuilder class is responsible for building a dictionary of fruit tree crops.
    /// </summary>
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

        /// <summary>
        /// Builds a TreeData object from the given FruitTreeData and id.
        /// </summary>
        /// <param name="cropData">The FruitTreeData object.</param>
        /// <param name="id">The id of the crop.</param>
        /// <returns>The TreeData object.</returns>
        private static TreeData BuildCrop(FruitTreeData cropData, string id)
        {
            Item seed = new SObject(id, 1);
            DropInformation dropInformation = new();
            foreach (var drop in cropData.Fruit)
            {
                string unQualifiedId = drop.ItemId;
                //remove everything between the first '(' and first ')'
                int firstParenthesis = unQualifiedId.IndexOf('(');
                int lastParenthesis = unQualifiedId.IndexOf(')');
                if (firstParenthesis != -1 && lastParenthesis != -1)
                {
                    unQualifiedId = unQualifiedId.Remove(firstParenthesis, lastParenthesis - firstParenthesis + 1);
                }

                dropInformation.Drops.Add(
                    new DropInformation.Drop(
                        new SObject(
                            unQualifiedId,
                            1
                            ),
                        1,
                        drop.Chance,
                        drop.Season
                        )
                    );
            }
            return new TreeData(cropData, seed, dropInformation);
        }
    }
}