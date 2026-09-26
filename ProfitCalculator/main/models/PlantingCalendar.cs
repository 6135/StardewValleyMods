using StardewValley;
using static ProfitCalculator.Utils;

#nullable enable

namespace ProfitCalculator.main.models
{
    /// <summary>
    /// Class <c>PlantingCalendar</c> converts "days after planting" into the in-game date, for plants simulated day by day
    /// (bushes, trees). Day <c>t</c> after planting on day <c>d</c> of season <c>s</c> falls on day of month
    /// <c>(d - 1 + t) % 28 + 1</c> of season <c>(s + (d - 1 + t) / 28) % 4</c>.
    /// </summary>
    public static class PlantingCalendar
    {
        /// <summary> Days in a season. </summary>
        internal const int DaysPerSeason = 28;

        /// <summary> Days in a year (4 seasons). </summary>
        internal const int DaysPerYear = DaysPerSeason * 4;

        /// <summary>
        /// Day of the month (1 to 28) <paramref name="daysAfterPlanting"/> days after planting on <paramref name="plantingDay"/>.
        /// </summary>
        /// <param name="plantingDay"> Planting day of the month (1 to 28).</param>
        /// <param name="daysAfterPlanting"> Days elapsed since planting (0 is the planting day).</param>
        /// <returns> The day of the month. <c>int</c></returns>
        public static int DayOfMonth(int plantingDay, int daysAfterPlanting)
        {
            return ((plantingDay - 1 + daysAfterPlanting) % DaysPerSeason) + 1;
        }

        /// <summary>
        /// Season <paramref name="daysAfterPlanting"/> days after planting on <paramref name="plantingDay"/> of <paramref name="plantingSeason"/>.
        /// In the greenhouse the starting season is irrelevant (seasons are ignored there), so spring is used.
        /// </summary>
        /// <param name="plantingSeason"> Planting season of type UtilsSeason <see cref="UtilsSeason"/>.</param>
        /// <param name="plantingDay"> Planting day of the month (1 to 28).</param>
        /// <param name="daysAfterPlanting"> Days elapsed since planting (0 is the planting day).</param>
        /// <returns> The game season of that day. <c>Season</c></returns>
        public static Season SeasonAt(UtilsSeason plantingSeason, int plantingDay, int daysAfterPlanting)
        {
            int start = plantingSeason == UtilsSeason.Greenhouse ? 0 : (int)plantingSeason;
            return (Season)((start + ((plantingDay - 1 + daysAfterPlanting) / DaysPerSeason)) % 4);
        }

        /// <summary>
        /// Like <see cref="SeasonAt"/>, but as a <see cref="UtilsSeason"/>: <see cref="UtilsSeason.Greenhouse"/> stays
        /// Greenhouse on every day, so drop and price lookups keep ignoring seasons there.
        /// </summary>
        /// <param name="plantingSeason"> Planting season of type UtilsSeason <see cref="UtilsSeason"/>.</param>
        /// <param name="plantingDay"> Planting day of the month (1 to 28).</param>
        /// <param name="daysAfterPlanting"> Days elapsed since planting (0 is the planting day).</param>
        /// <returns> The season of that day. <c>UtilsSeason</c></returns>
        public static UtilsSeason UtilsSeasonAt(UtilsSeason plantingSeason, int plantingDay, int daysAfterPlanting)
        {
            if (plantingSeason == UtilsSeason.Greenhouse)
            {
                return UtilsSeason.Greenhouse;
            }
            return (UtilsSeason)(int)SeasonAt(plantingSeason, plantingDay, daysAfterPlanting);
        }
    }
}
