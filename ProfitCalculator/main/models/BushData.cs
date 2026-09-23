using StardewValley;
using System.Collections.Generic;
using static ProfitCalculator.Utils;

#nullable enable

namespace ProfitCalculator.main.models
{
    /// <summary>
    /// Class <c>BushData</c> models a bush (the vanilla tea bush or a Custom Bush) that produces once per day while in bloom.
    /// A bush blooms once it is at least <see cref="AgeToProduce"/> days old, on or after <see cref="DayToBeginProducing"/> of the month,
    /// during one of its seasons (any season in the greenhouse). This matches <c>Bush.inBloom()</c> in the game.
    /// </summary>
    public class BushData : PlantData
    {
        /// <summary>
        /// Constructor for <c>BushData</c> class. It's used to create a new instance of the class.
        /// </summary>
        /// <param name="displayName">Display name of the bush.</param>
        /// <param name="seed">Item planted to grow the bush.</param>
        /// <param name="dropInformation">Drop Information for the bush. Must contain at least one drop.</param>
        /// <param name="ageToProduce">Age in days the bush needs to reach before it produces.</param>
        /// <param name="dayToBeginProducing">Day of the month from which the bush produces.</param>
        /// <param name="seasons">Seasons in which the bush produces outdoors.</param>
        public BushData(
            string displayName,
            Item seed,
            DropInformation dropInformation,
            int ageToProduce,
            int dayToBeginProducing,
            List<Season> seasons
            )
            : base(
                  ageToProduce,
                  1,
                  1,
                  1,
                  0f,
                  0f,
                  displayName,
                  seasons,
                  seed,
                  false,
                  false,
                  dropInformation
                  )
        {
            AgeToProduce = ageToProduce;
            DayToBeginProducing = dayToBeginProducing;
        }

        /// <summary>
        /// Age in days the bush needs to reach before it produces.
        /// </summary>
        public int AgeToProduce { get; }

        /// <summary>
        /// Day of the month from which the bush produces.
        /// </summary>
        public int DayToBeginProducing { get; }

        /// <summary>
        /// Counts the days in the available window on which the bush is in bloom, simulating each day after planting.
        /// Fertilizer doesn't affect bushes.
        /// </summary>
        /// <param name="currentSeason"> Planting Season of type UtilsSeason <see cref="UtilsSeason"/></param>
        /// <param name="fertilizerQuality"> Ignored, bushes can't be fertilized.</param>
        /// <param name="day"> Planting day of the month.</param>
        /// <returns> Total number of harvests for the bush for the available time. <c>int</c></returns>
        public override int TotalHarvestsWithRemainingDays(UtilsSeason currentSeason, FertilizerQuality fertilizerQuality, int day)
        {
            int window = TotalAvailableDays(currentSeason, day);
            if (window <= 0)
            {
                return 0;
            }
            bool greenhouse = currentSeason == UtilsSeason.Greenhouse;
            // In the greenhouse every season counts, so the starting season index is irrelevant.
            int plantingSeason = greenhouse ? 0 : (int)currentSeason;
            int harvests = 0;
            for (int t = 1; t <= window; t++)
            {
                int dayOfMonth = ((day - 1 + t) % 28) + 1;
                Season season = (Season)((plantingSeason + ((day - 1 + t) / 28)) % 4);
                if (t >= AgeToProduce && dayOfMonth >= DayToBeginProducing && (greenhouse || Seasons.Contains(season)))
                {
                    harvests++;
                }
            }
            return harvests;
        }

        /// <summary>
        /// Bushes can't be fertilized.
        /// </summary>
        /// <returns> Always 0. </returns>
        public override int TotalFertilizerNeeded()
        {
            return 0;
        }

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
    }
}
