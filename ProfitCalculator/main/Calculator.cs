using ProfitCalculator.main.memory;
using ProfitCalculator.main.models;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using static ProfitCalculator.Utils;

namespace ProfitCalculator.main
{
    /// <summary>
    /// Class used to calculate the profits for crops. Contains all the settings for the calculator and the functions used to calculate the profits. Also contains the list of crops and the crop parsers. <see cref="PlantData.TotalCropProfit()"/> and <see cref="PlantData.TotalCropProfitPerDay()"/>, <see cref="PlantData.TotalFertilizerCost()"/>, <see cref="PlantData.TotalFertilzerCostPerDay()"/>, <see cref="PlantData.TotalSeedsCost()"/>, <see cref="PlantData.TotalSeedsCostPerDay()"/> are the main functions used to calculate the profits. <see cref="RetrieveCropsAsOrderderList"/> and <see cref="RetrieveCropInfos"/> are the main functions used to retrieve the list of crops and crop infos.
    /// </summary>
    public class Calculator
    {
        #region properties

        // Properties
        /// <summary>
        /// List of all crops in the game
        /// </summary>
        public Dictionary<string, PlantData> Crops { get; }

        /// <summary>
        /// Day of the Season
        /// </summary>
        public uint Day { get; set; }

        /// <summary>
        /// Max days of a Season
        /// </summary>
        public uint MaxDay { get; set; }

        /// <summary>
        /// Min days of a Season
        /// </summary>
        public uint MinDay { get; set; }

        /// <summary>
        /// UtilsSeason of the year selected
        /// </summary>
        public UtilsSeason Season { get; set; }

        /// <summary>
        /// Type of produce selected
        /// TODO: Implement this.
        /// </summary>
        public ProduceType ProduceType { get; set; }

        /// <summary>
        /// Type of fertilizer selected
        /// </summary>
        public FertilizerQuality FertilizerQuality { get; set; }

        /// <summary>
        /// Whether or not the player pays for seeds
        /// </summary>
        public bool PayForSeeds { get; set; }

        /// <summary>
        /// Whether or not the player pays for fertilizer
        /// </summary>
        public bool PayForFertilizer { get; set; }

        /// <summary>
        /// Max money the player is willing to spend on seeds or fertilizer
        /// </summary>
        public uint MaxMoney { get; set; }

        /// <summary>
        /// Whether or not to use base stats for the player or the current stats
        /// </summary>
        public bool UseBaseStats { get; set; }

        /// <summary>
        /// Whether or not to calculate crops that are not available for the current Season. If false, then crops that are not available for the current Season will not be calculated. Not used.
        /// </summary>
        public bool CrossSeason { get; set; }

        /// <summary>
        /// Price multipliers for the different qualities of crops
        /// </summary>
        public double[] PriceMultipliers { get; set; } = { 1.0, 1.25, 1.5, 2.0 };

        /// <summary>
        /// Farming level of the player
        /// </summary>
        public int FarmingLevel { get; set; }

        #endregion properties

        /// <summary>
        /// Constructor for the calculator, initializes the list of crops and crop parsers. Instantiates the calculator with default values.
        /// </summary>
        public Calculator()
        {
            Crops = new Dictionary<string, PlantData>();
        }

        /// <summary>
        /// Sets the settings for the calculator to use when calculating profits.
        /// </summary>
        /// <param name="settings"> The settings to calculate with. </param>
        /// <param name="crossSeason"><see cref="CrossSeason"/></param>
        public void SetSettings(ProfitCalculatorSettings settings, bool crossSeason)
        {
            Day = settings.Day;
            MaxDay = settings.MaxDay;
            MinDay = settings.MinDay;
            Season = settings.Season;
            ProduceType = settings.ProduceType;
            FertilizerQuality = settings.FertilizerQuality;
            PayForSeeds = settings.PayForSeeds;
            PayForFertilizer = settings.PayForFertilizer;
            MaxMoney = settings.MaxMoney;
            UseBaseStats = settings.UseBaseStats;
            CrossSeason = crossSeason;
            if (settings.UseBaseStats)
            {
                FarmingLevel = 0;
            }
            else
            {
                FarmingLevel = Game1.player.FarmingLevel;
            }
        }

        /// <summary>
        /// Sets the settings for the calculator to use when calculating profits, with <see cref="CrossSeason"/> enabled.
        /// </summary>
        /// <param name="settings"> The settings to calculate with. </param>
        public void SetSettings(ProfitCalculatorSettings settings) => SetSettings(settings, true);

        /// <summary>
        /// Clears the list of crops.
        /// </summary>
        public void ClearCrops()
        {
            Crops.Clear();
        }

        /// <summary>
        /// Retrieves the list of crops as an ordered list by profit.
        /// </summary>
        /// <returns> List of crops ordered by profit </returns>
        public IReadOnlyList<PlantData> RetrieveCropsAsOrderderList()
        {
            // sort crops by profit
            // return list
            List<PlantData> cropList = new();
            foreach (KeyValuePair<string, PlantData> crop in Crops)
            {
                cropList.Add(crop.Value);
            }
            cropList.Sort((x, y) => y.DropInformation.AveragePrice(Season).CompareTo(x.DropInformation.AveragePrice(Season)));
            return cropList;
        }

        /// <summary>
        /// Retrieves the list of <see cref="CropInfo"/> as an ordered list by profit.
        /// </summary>
        /// <returns> List of <see cref="CropInfo"/> ordered by profit </returns>
        public IReadOnlyList<CropInfo> RetrieveCropInfos()
        {
            List<CropInfo> cropInfos = new();
            foreach (PlantData crop in Crops.Values)
            {
                CropInfo ci = RetrieveCropInfo(crop);
                if (ci.TotalHarvests >= 1)
                {
                    if (!PayForSeeds || ci.TotalSeedLoss <= MaxMoney)
                    {
                        cropInfos.Add(ci);
                    }
                }
            }
            cropInfos.Sort((x, y) => y.ProfitPerDay.CompareTo(x.ProfitPerDay));
            return cropInfos;
        }

        /// <summary>
        /// Retrieves the <see cref="CropInfo"/> for a specific crop, calculated with the current settings.
        /// </summary>
        /// <param name="crop"> CropDataExpanded to retrieve <see cref="CropInfo"/> for </param>
        /// <returns> <see cref="CropInfo"/> for the crop </returns>
        private CropInfo RetrieveCropInfo(PlantData crop)
        {
            return new CropInfo(crop, this);
        }

        /// <summary>
        /// Adds a crop to the list of crops.
        /// </summary>
        /// <param name="id"> Id of the crop </param>
        /// <param name="crop"> CropDataExpanded to add </param>
        public void AddCrop(string id, PlantData crop)
        {
            //check if already exists
            if (!Crops.ContainsKey(id))
            {
                try
                {
                    Crops.Add(id, crop);
                }
                catch (Exception)
                {
                    Container.Instance.Resolve<IMonitor>(ModEntry.UniqueID)?.Log("Failed to add\n" + crop.ToString(), LogLevel.Debug);
                }
            }
        }
    }
}