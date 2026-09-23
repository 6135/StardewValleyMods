using ProfitCalculator.main.accessors;
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
        public Dictionary<string, PlantData> Crops { get; set; }

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
        /// Id of the produce type selected: <see cref="RawProduceType"/>, <see cref="FruitTreesProduceType"/>, <see cref="WildTreesProduceType"/>, a machine's qualified id or a chained aging id (see <see cref="accessors.MachineAccessor"/>).
        /// </summary>
        public string ProduceType { get; set; } = RawProduceType;

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
        /// Whether a crop keeps growing into the following seasons it can grow in. If false, only the days left in the selected Season are counted.
        /// </summary>
        public bool CrossSeason { get; set; }

        /// <summary>
        /// Number of years (112 days each) trees are simulated over: the window of <see cref="FruitTreesProduceType"/>,
        /// <see cref="WildTreesProduceType"/> and of fruit trees in machine lists. From 1 to 10.
        /// </summary>
        public uint Years { get; set; } = 1;

        /// <summary>
        /// Whether wild trees are tapped with a heavy tapper, which halves the time between products.
        /// </summary>
        public bool HeavyTapper { get; set; }

        /// <summary>
        /// Whether wild trees are grown with tree fertilizer, which raises their daily growth chance.
        /// </summary>
        public bool TreeFertilizer { get; set; }

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
        /// <param name="day"><see cref="Day"/></param>
        /// <param name="maxDay"><see cref="MaxDay"/></param>
        /// <param name="minDay"><see cref="MinDay"/></param>
        /// <param name="_season"><see cref="StardewValley.Season"/></param>
        /// <param name="produceType"><see cref="ProduceType"/></param>
        /// <param name="fertilizerQuality"> <see cref="FertilizerQuality"/></param>
        /// <param name="payForSeeds"> <see cref="PayForSeeds"/></param>
        /// <param name="payForFertilizer"> <see cref="PayForFertilizer"/></param>
        /// <param name="maxMoney"> <see cref="MaxMoney"/></param>
        /// <param name="useBaseStats"> <see cref="UseBaseStats"/></param>
        /// <param name="crossSeason"> <see cref="CrossSeason"/></param>
        /// <param name="years"> <see cref="Years"/>, clamped to 1-10</param>
        /// <param name="heavyTapper"> <see cref="HeavyTapper"/></param>
        /// <param name="treeFertilizer"> <see cref="TreeFertilizer"/></param>
        public void SetSettings(uint day, uint maxDay, uint minDay, UtilsSeason _season, string produceType, FertilizerQuality fertilizerQuality, bool payForSeeds, bool payForFertilizer, uint maxMoney, bool useBaseStats, bool crossSeason, uint years, bool heavyTapper, bool treeFertilizer)
        {
            Years = Math.Clamp(years, 1u, 10u);
            HeavyTapper = heavyTapper;
            TreeFertilizer = treeFertilizer;
            Day = day;
            MaxDay = maxDay;
            MinDay = minDay;
            Season = _season;
            ProduceType = produceType;
            FertilizerQuality = fertilizerQuality;
            PayForSeeds = payForSeeds;
            PayForFertilizer = payForFertilizer;
            MaxMoney = maxMoney;
            UseBaseStats = useBaseStats;
            CrossSeason = crossSeason;
            if (useBaseStats)
            {
                FarmingLevel = 0;
            }
            else
            {
                FarmingLevel = Game1.player.FarmingLevel;
            }
        }

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
        public List<PlantData> RetrieveCropsAsOrderderList()
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
        public List<CropInfo> RetrieveCropInfos()
        {
            List<CropInfo> cropInfos = new();
            foreach (PlantData crop in Crops.Values)
            {
                if (!IsListed(crop))
                {
                    continue;
                }
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
        /// Whether <paramref name="crop"/> belongs in the list of the selected <see cref="ProduceType"/>: Raw lists every
        /// plant but trees, the tree views list only their own kind, and a machine lists the plants it accepts (fruit
        /// trees included, wild tree tapper products never).
        /// </summary>
        /// <param name="crop"> The plant to check. </param>
        /// <returns> Whether the plant is listed. </returns>
        private bool IsListed(PlantData crop)
        {
            return ProduceType switch
            {
                RawProduceType => crop is not TreeData and not WildTreeData,
                FruitTreesProduceType => crop is TreeData,
                WildTreesProduceType => crop is WildTreeData,
                _ => crop is not WildTreeData && crop.CanProduce(ProduceType)
            };
        }

        /// <summary>
        /// Retrieves the <see cref="CropInfo"/> for a specific crop. Uses information obtained by calling internal functions to calculate the values and build the object.
        /// </summary>
        /// <param name="crop"> CropDataExpanded to retrieve <see cref="CropInfo"/> for </param>
        /// <returns> <see cref="CropInfo"/> for the crop </returns>
        private CropInfo RetrieveCropInfo(PlantData crop)
        {
            double totalProfit = crop.TotalCropProfit();
            double profitPerDay = crop.TotalCropProfitPerDay();
            double totalSeedLoss = crop.TotalSeedsCost();
            double seedLossPerDay = crop.TotalSeedsCostPerDay();
            double totalFertilizerLoss = crop.TotalFertilizerCost();
            double fertilizerLossPerDay = crop.TotalFertilzerCostPerDay();
            string produceType = ProduceType;
            int duration = crop.TotalAvailableDays(Season, (int)Day);
            int totalHarvests = crop.TotalHarvestsWithRemainingDays(Season, FertilizerQuality, (int)Day);

            int growthTime = crop.GrowingDays(FertilizerQuality);
            int regrowthTime = crop.RegrowDays;
            int productCount = crop.MinHarvests;
            double chanceOfExtraProduct = crop.AverageExtraCropsFromRandomness();
            double chanceOfNormalQuality = crop.GetCropBaseQualityChance();
            double chanceOfSilverQuality = crop.GetCropSilverQualityChance();
            double chanceOfGoldQuality = crop.GetCropGoldQualityChance();
            double chanceOfIridiumQuality = crop.GetCropIridiumQualityChance();

            string produceName = crop.ProduceName(produceType);
            double produceCount = crop.TotalProduceCount(produceType);
            int inputsPerProduct = 1;
            double processingDays = 0;
            Item produceItem = null;
            if (!IsSoldRaw(produceType) && crop.TryGetMainProduct(produceType, out ProductInfo product))
            {
                inputsPerProduct = product.RequiredCount;
                processingDays = product.ProcessingDays;
                produceItem = product.Output;
            }
            Item inputItem = crop.MainDrop(Season)?.Item ?? crop.DropInformation.Drops[0].Item;
            int seedsNeeded = crop.TotalSeedsNeeded();
            int fertilizerNeeded = FertilizerQuality == FertilizerQuality.None ? 0 : crop.TotalFertilizerNeeded();
            int paybackDay = crop.PaybackDay();

            return new CropInfo(crop, totalProfit, profitPerDay, totalSeedLoss, seedLossPerDay, totalFertilizerLoss, fertilizerLossPerDay, produceType, duration, totalHarvests, growthTime, regrowthTime, productCount, chanceOfExtraProduct, chanceOfNormalQuality, chanceOfSilverQuality, chanceOfGoldQuality, chanceOfIridiumQuality, produceName, produceCount, inputsPerProduct, processingDays, seedsNeeded, fertilizerNeeded, produceItem, inputItem, paybackDay);
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
                    Container.Instance.GetInstance<IMonitor>(ModEntry.UniqueID)?.Log("Failed to add\n" + crop.ToString(), LogLevel.Debug);
                }
            }
        }
    }
}