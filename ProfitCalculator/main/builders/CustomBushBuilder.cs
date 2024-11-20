using ProfitCalculator.apis;
using ProfitCalculator.main.models;
using System;
using System.Collections.Generic;

namespace ProfitCalculator.main.builders
{
    //TODO: Implement the CustomBushBuilder class using the IDataBuilder interface and custom bush api
    public class CustomBushBuilder : IDataBuilder
    {
        public Dictionary<string, PlantData> BuildCrops()
        {
            throw new NotImplementedException();
        }

        private static PlantData BuildCrop(ICustomBush cropData, string id)
        {
            throw new NotImplementedException();
        }
    }
}