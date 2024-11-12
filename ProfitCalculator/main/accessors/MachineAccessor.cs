using StardewValley;
using System.Collections.Generic;

namespace ProfitCalculator.main.accessors
{
    public class MachineAccessor
    {
        public MachineAccessor()
        {
        }

        private Dictionary<string, Dictionary<ISalable, ItemStockInformation>> BuildCache()
        {
            return new();
        }

        public void InvalidateCaches()
        {
            //TODO
        }

        public int GetCheapestSeedPrice(string cropId)
        {
            //TODO
            return 0;
        }

        public int GetExpensiveSeedPrice(string cropId)
        {
            return 0;
        }

        public int GetSpecificShopPrice(string cropId, string shopID)
        {
            return 0;
        }
    }
}