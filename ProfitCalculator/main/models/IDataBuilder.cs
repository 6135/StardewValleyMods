using System.Collections.Generic;

namespace ProfitCalculator.main.models
{
    public interface IDataBuilder
    {
        /// <summary>
        /// Builds a dictionary of crops from the game files. Accesses the data from the game files and parses them into a dictionary.
        /// </summary>
        /// <returns> A dictionary of crops. </returns>
        public Dictionary<string, IPlantData> BuildCrops();
    }
}