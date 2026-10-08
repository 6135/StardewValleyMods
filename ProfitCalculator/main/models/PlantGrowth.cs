using System.Linq;
using SCropData = StardewValley.GameData.Crops.CropData;

#nullable enable

namespace ProfitCalculator.main.models
{
    /// <summary>
    /// How a plant grows and how much it yields per harvest, passed to <see cref="PlantData"/>.
    /// </summary>
    /// <param name="Days">Total days to grow, excluding <paramref name="RegrowDays"/>.</param>
    /// <param name="RegrowDays">Days to regrow after a harvest; 0 if the plant doesn't regrow.</param>
    /// <param name="MinHarvests">Minimum drops per harvest.</param>
    /// <param name="MaxHarvests">Maximum drops per harvest.</param>
    /// <param name="MaxHarvestIncreasePerFarmingLevel">Maximum drops increase per farming level.</param>
    /// <param name="ChanceForExtraCrops">Chance for extra drops.</param>
    public sealed record PlantGrowth(
        int Days,
        int RegrowDays,
        int MinHarvests,
        int MaxHarvests,
        float MaxHarvestIncreasePerFarmingLevel,
        double ChanceForExtraCrops)
    {
        /// <summary> A plant that yields exactly one drop per harvest. </summary>
        /// <param name="days">Total days to grow.</param>
        /// <param name="regrowDays">Days to regrow after a harvest.</param>
        public static PlantGrowth SingleDrop(int days, int regrowDays) => new(days, regrowDays, 1, 1, 0f, 0f);

        /// <summary> Growth and yield of a crop from its game data. </summary>
        /// <param name="cropData">The crop's game data.</param>
        public static PlantGrowth FromCrop(SCropData cropData) => new(
            cropData.DaysInPhase.Sum(),
            cropData.RegrowDays,
            cropData.HarvestMinStack,
            cropData.HarvestMaxStack,
            cropData.HarvestMaxIncreasePerFarmingLevel,
            cropData.ExtraHarvestChance);
    }
}
