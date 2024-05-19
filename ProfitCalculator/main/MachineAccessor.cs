using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Shops;
using StardewValley.Internal;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ProfitCalculator.Utils;
using SObject = StardewValley.Object;

namespace ProfitCalculator.main
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
        }

        public int GetCheapestSeedPrice(string cropId)
        {
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