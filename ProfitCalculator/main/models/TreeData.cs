using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProfitCalculator.main.accessors;
using ProfitCalculator.main.memory;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.FruitTrees;
using System;
using System.Collections.Generic;
using static ProfitCalculator.Utils;
using SObject = StardewValley.Object;

#nullable enable

namespace ProfitCalculator.main.models
{
    /// <summary>
    /// Class <c>CropDataExpanded</c> models a crop from the game storing all relevant information about it.
    /// </summary>
    public class TreeData : PlantData
    {
        /// <summary>
        /// Constructor for <c>CropDataExpanded</c> class. It's used to create a new instance of the class.
        /// </summary>
        /// <param name="_cropData">Crop's full Data</param>
        /// <param name="_seed" >Seed Item</param>
        /// <param name="dropInformation">Drop Information for the crop</param>
        public TreeData(FruitTreeData _cropData, Item _seed, DropInformation dropInformation)
            : base(
                  28,
                  1,
                  1,
                  1,
                  0f,
                  0f,
                  dropInformation.Drops[0].Item.DisplayName,
                  _cropData.Seasons,
                  _seed,
                  false,
                  false,
                  dropInformation
                  )
        {
        }

        // Harvests and profit use the base implementation: the sapling takes Days (28) to mature with no speed bonuses,
        // then drops one fruit per day (RegrowDays = 1) while in season.

        /// <summary>
        /// Fruit trees can't be fertilized.
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