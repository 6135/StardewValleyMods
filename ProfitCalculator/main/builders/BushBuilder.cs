using ProfitCalculator.main.memory;
using ProfitCalculator.main.models;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;

#nullable enable

namespace ProfitCalculator.main.builders
{
    /// <summary>
    /// The BushBuilder class builds the vanilla bushes, which aren't part of <c>Data/Crops</c>. Currently that's only the tea bush.
    /// </summary>
    public class BushBuilder : IDataBuilder
    {
        /// <summary> Unqualified id of the Tea Sapling, also used as the dictionary key. </summary>
        private const string TeaSaplingId = "251";

        /// <summary> Day of the month from which the tea bush produces, see <c>Bush.inBloom()</c>. </summary>
        private const int TeaDayToBeginProducing = 22;

        /// <inheritdoc/>
        public Dictionary<string, PlantData> BuildCrops()
        {
            Dictionary<string, PlantData> bushes = new();
            var Monitor = Container.Instance.GetInstance<IMonitor>(ModEntry.UniqueID);
            try
            {
                Item? seed = ItemRegistry.Create("(O)" + TeaSaplingId, allowNull: true);
                Item? teaLeaves = ItemRegistry.Create("(O)815", allowNull: true);
                if (seed is null || teaLeaves is null)
                {
                    Monitor?.Log("Tea Sapling or Tea Leaves could not be created; skipping the tea bush.", LogLevel.Warn);
                    return bushes;
                }
                DropInformation dropInformation = new();
                dropInformation.Drops.Add(new DropInformation.Drop(teaLeaves, 1, 1.0));
                bushes.Add(
                    TeaSaplingId,
                    new BushData(
                        teaLeaves.DisplayName,
                        seed,
                        dropInformation,
                        Bush.daysToMatureGreenTeaBush,
                        TeaDayToBeginProducing,
                        new List<Season> { Season.Spring, Season.Summer, Season.Fall }
                        )
                    );
            }
            catch (Exception e)
            {
                Monitor?.Log($"Error building the tea bush: {e.Message}", LogLevel.Error);
            }
            Monitor?.Log($"Bushes loaded: {bushes.Count}", LogLevel.Debug);
            return bushes;
        }
    }
}
