using CoreUtils.management.memory;
using StardewModdingAPI;
using StardewValley;
using System;
using SObject = StardewValley.Object;

#nullable enable
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace ProfitCalculator
{
    /// <summary>
    /// Provides a set of tools to be used by multiple classes of the mod.
    /// </summary>
    public class Utils
    {
        /// <summary>
        /// Gets the days of a Season. Unused.
        /// </summary>
        /// <param name="season"> The Season to get the days of.</param>
        /// <returns> The number of days in the Season.</returns>
        public static int GetSeasonDays(UtilsSeason season)
        {
            return season switch
            {
                UtilsSeason.Spring => 28,
                UtilsSeason.Summer => 28,
                UtilsSeason.Fall => 28,
                UtilsSeason.Winter => 28,
                UtilsSeason.Greenhouse => 112,
                _ => 0,
            };
        }

        /// <summary>
        /// UtilsSeason enum.
        /// </summary>
        public enum UtilsSeason
        {
            /// <summary> Spring Season. </summary>
            Spring = 0,

            /// <summary> Summer Season. </summary>
            Summer = 1,

            /// <summary> Fall Season. </summary>
            Fall = 2,

            /// <summary> Winter Season. </summary>
            Winter = 3,

            /// <summary> Greenhouse Season. </summary>
            Greenhouse = 4
        }

        /// <summary>
        /// Produce type enum.
        /// </summary>
        public enum ProduceType
        {
            /// <summary> Crops. </summary>
            Raw,

            /// <summary> Artisan goods. </summary>
            Keg,

            /// <summary> Artisan goods. </summary>
            Cask
        }

        /// <summary>
        /// Fertilizer quality enum.
        /// </summary>
        public enum FertilizerQuality
        {
            /// <summary> No fertilizer. </summary>
            None = 0,

            /// <summary> Basic fertilizer. </summary>
            Basic = 1,

            /// <summary> Quality fertilizer. </summary>
            Quality = 2,

            /// <summary> Deluxe fertilizer. </summary>
            Deluxe = 3,

            /// <summary> Speed-Gro fertilizer. </summary>
            SpeedGro = -1,

            /// <summary> Deluxe Speed-Gro fertilizer. </summary>
            DeluxeSpeedGro = -2,

            /// <summary> Hyper Speed-Gro fertilizer. </summary>
            HyperSpeedGro = -3
        }

        //get Season translated names
        private static string GetTranslatedName(string str)
        {
            //convert string to lowercase
            str = str.ToLower();
            var Helper = Container.Instance.GetInstance<IModHelper>(ModEntry.UniqueID);
            return Helper?.Translation.Get(str) ?? "Error";
        }

        /// <summary>
        /// Get translated Season name.
        /// </summary>
        /// <param name="season"> The Season to get the translated name of.</param>
        /// <returns> The translated name of the Season.</returns>
        public static string GetTranslatedSeason(UtilsSeason season)
        {
            return GetTranslatedName(season.ToString());
        }

        /// <summary>
        /// Get translated produce type name.
        /// </summary>
        /// <param name="produceType"> The produce type to get the translated name of.</param>
        /// <returns> The translated name of the produce type.</returns>
        public static string GetTranslatedProduceType(ProduceType produceType)
        {
            return GetTranslatedName(produceType.ToString());
        }

        /// <summary>
        /// Get translated fertilizer quality name.
        /// </summary>
        /// <param name="fertilizerQuality"> The fertilizer quality to get the translated name of.</param>
        /// <returns> The translated name of the fertilizer quality.</returns>
        public static string GetTranslatedFertilizerQuality(FertilizerQuality fertilizerQuality)
        {
            return GetTranslatedName(fertilizerQuality.ToString());
        }

        /// <summary>
        /// Get translated Season name. All seasons.
        /// </summary>
        /// <returns> Array of all translated Season names.</returns>
        public static string[] GetAllTranslatedSeasons()
        {
            string[] names = Enum.GetNames(typeof(UtilsSeason));
            string[] translatedNames = new string[names.Length];
            foreach (string name in names)
            {
                translatedNames[Array.IndexOf(names, name)] = GetTranslatedName(name);
            }
            return translatedNames;
        }

        /// <summary>
        /// Get all translated produce type names.
        /// </summary>
        /// <returns> Array of all translated produce type names.</returns>
        public static string[] GetAllTranslatedProduceTypes()
        {
            string[] names = Enum.GetNames(typeof(ProduceType));
            string[] translatedNames = new string[names.Length];
            foreach (string name in names)
            {
                translatedNames[Array.IndexOf(names, name)] = GetTranslatedName(name);
            }
            return translatedNames;
        }

        /// <summary>
        /// Get all translated fertilizer quality names.
        /// </summary>
        /// <returns> Array of all translated fertilizer quality names.</returns>
        public static string[] GetAllTranslatedFertilizerQualities()
        {
            string[] names = Enum.GetNames(typeof(FertilizerQuality));
            string[] translatedNames = new string[names.Length];
            foreach (string name in names)
            {
                translatedNames[Array.IndexOf(names, name)] = GetTranslatedName(name);
            }
            return translatedNames;
        }

        /// <summary>
        /// Get prices of each fertilizer quality.
        /// </summary>
        /// <param name="fq"> The fertilizer quality to get the price of.</param>
        /// <returns> The price of the fertilizer quality.</returns>
        public static int FertilizerPrices(FertilizerQuality fq)
        {
            return fq switch
            {
                FertilizerQuality.None => 0,
                FertilizerQuality.Basic => 100,
                FertilizerQuality.Quality => 150,
                FertilizerQuality.Deluxe => 200,
                FertilizerQuality.SpeedGro => 100,
                FertilizerQuality.DeluxeSpeedGro => 150,
                FertilizerQuality.HyperSpeedGro => 200,
                _ => 0,
            };
        }

        public static int PriceFromObjectID(string id)
        {
            var obj = new SObject(id, 1);
            return obj.Price;
        }

        public static Season SeasonFromUtilsSeason(UtilsSeason season)
        {
            return season switch
            {
                UtilsSeason.Spring => Season.Spring,
                UtilsSeason.Summer => Season.Summer,
                UtilsSeason.Fall => Season.Fall,
                UtilsSeason.Winter => Season.Winter,
                UtilsSeason.Greenhouse => throw new NotSupportedException("Season from StardewValley.Season enum doesn't contain Greenhouse"),
                _ => Season.Spring
            };
        }
    }
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member