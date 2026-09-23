#nullable enable

namespace ProfitCalculator.main.models
{
    /// <summary>
    /// A crop described by hand, either in the <c>Mods/6135.ProfitCalculator/ManualCrops</c> asset (<c>assets/ManualCrops.json</c>) or through the mod API.
    /// The dictionary key of the asset is the seed item id.
    /// </summary>
    public class ManualCropDefinition
    {
        /// <summary>Display name of the crop. When empty, the harvest item's display name is used.</summary>
        public string? Name { get; set; }

        /// <summary>Id of the harvested item, qualified (<c>(O)815</c>) or not (<c>815</c>). Numbers in JSON are accepted too.</summary>
        public string? HarvestID { get; set; }

        /// <summary>Days needed for the crop to grow before the first harvest.</summary>
        public int GrowthTime { get; set; }

        /// <summary>Days needed to regrow after a harvest. Zero or less means the crop doesn't regrow.</summary>
        public int RegrowthTime { get; set; } = -1;

        /// <summary>Space-separated list of seasons the crop grows in, for example <c>"spring summer"</c>. Case-insensitive.</summary>
        public string? Seasons { get; set; }

        /// <summary>Sale price of the harvested item. Zero or less keeps the item's own price.</summary>
        public int SalePrice { get; set; }

        /// <summary>Purchase price of the seed. Zero or less uses the shop or <c>SeedPrices</c> price.</summary>
        public int PurchasePrice { get; set; }

        /// <summary>Minimum number of items per harvest.</summary>
        public int MinimumHarvests { get; set; } = 1;

        /// <summary>Maximum number of items per harvest.</summary>
        public int MaximumHarvests { get; set; } = 1;

        /// <summary>Increase of the maximum harvest per farming level.</summary>
        public float MaxHarvestsIncreasePerFarmingLevel { get; set; }

        /// <summary>Chance of an extra item on each harvest.</summary>
        public double ExtraHarvestChance { get; set; }

        /// <summary>Whether the harvest can have a quality.</summary>
        public bool HasQuality { get; set; } = true;

        /// <summary>Whether the crop accepts fertilizer.</summary>
        public bool AcceptsFertilizer { get; set; } = true;

        /// <summary>Whether the crop is a paddy crop (grows faster near water).</summary>
        public bool IsPaddyCrop { get; set; }

        /// <summary>Whether the crop is a raised (trellis) crop. Parsed but currently unused.</summary>
        public bool IsRaisedCrop { get; set; }

        /// <summary>Whether the crop is a bush crop. Parsed but currently unused.</summary>
        public bool IsBushCrop { get; set; }

        /// <summary>Whether the crop can become a giant crop. Parsed but currently unused.</summary>
        public bool IsGiantCrop { get; set; }
    }
}
