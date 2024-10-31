using ProfitCalculator.apis;
using ProfitCalculator.main.models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitCalculator.main.builders
{
    //TODO: Implement the CustomBushBuilder class using the IDataBuilder interface and custom bush api
    internal class CustomBushBuilder : IDataBuilder
    {
        public Dictionary<string, IPlantData> BuildCrops()
        {
            throw new NotImplementedException();
        }

        private static IPlantData BuildCrop(ICustomBush cropData, string id)
        {
            throw new NotImplementedException();
        }
    }
}