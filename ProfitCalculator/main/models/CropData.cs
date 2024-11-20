using ProfitCalculator.main.memory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProfitCalculator.main.accessors;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using static ProfitCalculator.Utils;
using SCropData = StardewValley.GameData.Crops.CropData;
using SObject = StardewValley.Object;

#nullable enable

namespace ProfitCalculator.main.models
{
    /// <summary>
    /// Class <c>CropDataExpanded</c> models a crop from the game storing all relevant information about it.
    /// </summary>
    public class CropData : PlantData
    {
        /// <value>Property <c>IsPaddyCrop</c> represents whether the crop is a paddy crop or not.</value>
        public bool IsPaddyCrop { get; set; }

        /// <summary>
        /// Constructor for <c>CropDataExpanded</c> class. It's used to create a new instance of the class.
        /// </summary>
        /// <param name="_cropData">Crop's full Data</param>
        /// <param name="_seed" >Seed Item</param>
        /// <param name="_dropInformation">Information about the crop's drops</param>
        /// <param name="_affectedByFertilizer" >Whether the crop is affected by fertilizer or not</param>
        /// <param name="_affectedByQuality" >Whether the crop is affected by fertilizer quality or not</param>
        public CropData(
            SCropData _cropData,
            Item _seed,
            DropInformation _dropInformation,
            bool _affectedByQuality,
            bool _affectedByFertilizer
            ) : base
            (
                _cropData.DaysInPhase.Sum(),
                _cropData.RegrowDays,
                _cropData.HarvestMinStack,
                _cropData.HarvestMinStack,
                _cropData.HarvestMaxIncreasePerFarmingLevel,
                _cropData.ExtraHarvestChance,
                _dropInformation.Drops[0].Item.DisplayName,
                _cropData.Seasons,
                _seed,
                _affectedByQuality,
                _affectedByFertilizer,
                _dropInformation
            )
        {
            IsPaddyCrop = _cropData.IsPaddyCrop;
        }

        /// <summary>
        /// Constructor for <c>CropDataExpanded</c> class. It's used to create a new instance of the class.
        /// </summary>
        /// <param name="_cropData">Crop's full Data</param>
        /// <param name="_seed" >Seed Item</param>
        /// <param name="_dropInformation">Information about the crop's drops</param>
        public CropData(
            SCropData _cropData,
            Item _seed,
            DropInformation _dropInformation
            ) : base
            (
                _cropData.DaysInPhase.Sum(),
                _cropData.RegrowDays,
                _cropData.HarvestMinStack,
                _cropData.HarvestMinStack,
                _cropData.HarvestMaxIncreasePerFarmingLevel,
                _cropData.ExtraHarvestChance,
                _dropInformation.Drops[0].Item.DisplayName,
                _cropData.Seasons,
                _seed,
                true,
                true,
                _dropInformation
            )
        {
            IsPaddyCrop = _cropData.IsPaddyCrop;
        }

        /// <summary>
        /// Calculates the average growth speed value for the crop.
        /// It's calculated by adding fertilizer modifiers to 1.0f and finally adding 0.25f if the crop is a paddy crop and 0.1f if the player has the agriculturist profession.
        /// </summary>
        /// <param name="fertilizerQuality"> Quality of the used Fertilizer</param>
        /// <returns> Average growth speed value for the crop. <c>float</c></returns>
        public override float GetAverageGrowthSpeedValueForCrop(FertilizerQuality fertilizerQuality)
        {
            float speedIncreaseModifier = 0.0f;
            if (!AffectByFertilizer)
            {
                speedIncreaseModifier = 1.0f;
            }
            else if ((int)fertilizerQuality == -1)
            {
                speedIncreaseModifier += 0.1f;
            }
            else if ((int)fertilizerQuality == -2)
            {
                speedIncreaseModifier += 0.25f;
            }
            else if ((int)fertilizerQuality == -3)
            {
                speedIncreaseModifier += 0.33f;
            }
            else
            {
                throw new InvalidOperationException();
            }
            //if paddy crop then add 0.25f and if profession is agriculturist then add 0.1f
            if (IsPaddyCrop)
            {
                speedIncreaseModifier += 0.25f;
            }
            if (Game1.player.professions.Contains(Farmer.agriculturist))
            {
                speedIncreaseModifier += 0.1f;
            }
            return speedIncreaseModifier;
        }
    }
}