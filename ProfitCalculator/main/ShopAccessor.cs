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
    public class ShopAccessor
    {
        private readonly Cache<Dictionary<string, int>> seedPriceCache;
        private readonly Cache<Dictionary<string, Dictionary<ISalable, ItemStockInformation>>> shopStock;

        public ShopAccessor()
        {
            //Helper?.ModContent.Load<Dictionary<string, int>>(Path.Combine("assets", "SeedPrices.json"))
            seedPriceCache = new(
                    () => Helper?.ModContent.Load<Dictionary<string, int>>(Path.Combine("assets", "SeedPrices.json"))
                );
            shopStock = new(BuildCache);
        }

        private Dictionary<string, Dictionary<ISalable, ItemStockInformation>> BuildCache()
        {
            Dictionary<string, ShopData> shopData = DataLoader.Shops(Game1.content);
            Dictionary<string, Dictionary<ISalable, ItemStockInformation>> cache = new();
            //for each shop get shop stock
            foreach (var shop in shopData.Where(x => x.Value.Currency == 0))
            {
                cache.Add(shop.Key, GetShopStock(shop.Key, shop.Value));
            }
            return cache;
        }

        public static Dictionary<ISalable, ItemStockInformation> GetShopStock(string shopId, ShopData shop)
        {
            Dictionary<ISalable, ItemStockInformation> stock = new Dictionary<ISalable, ItemStockInformation>();
            List<ShopItemData> items = shop.Items;
            if (items != null && items.Count > 0)
            {
                Random shopRandom = Utility.CreateDaySaveRandom();
                HashSet<string> stockedItemIds = new HashSet<string>();
                ItemQueryContext itemQueryContext = new ItemQueryContext(Game1.currentLocation, Game1.player, shopRandom);

                HashSet<string> syncKeys = new HashSet<string>();
                foreach (ShopItemData itemData in shop.Items)
                {
                    if (!syncKeys.Add(itemData.Id))
                    {
                        Monitor?.Log($"Shop {shopId} has multiple items with entry ID '{itemData.Id}'. This may cause unintended behavior.", LogLevel.Debug);
                    }
                    var isItemOutOfSeason = false;
                    IList<ItemQueryResult> list = ItemQueryResolver.TryResolve(itemData, itemQueryContext, ItemQuerySearchMode.All, itemData.AvoidRepeat, itemData.AvoidRepeat ? stockedItemIds : null, null, delegate (string query, string message)
                    {
                        Monitor?.Log($"Failed parsing shop item query '{query}' for the '{shopId}' shop: {message}.", LogLevel.Debug);
                    });
                    int i = 0;
                    foreach (ItemQueryResult shopItem in list)
                    {
                        ISalable item = shopItem.Item;
                        item.Stack = shopItem.OverrideStackSize ?? item.Stack;
                        float price = ShopBuilder.GetBasePrice(shopItem, shop, itemData, item, isItemOutOfSeason, itemData.UseObjectDataPrice);
                        int availableStock = shopItem.OverrideShopAvailableStock ?? itemData.AvailableStock;
                        LimitedStockMode availableStockLimit = itemData.AvailableStockLimit;
                        string tradeItemId = shopItem.OverrideTradeItemId ?? itemData.TradeItemId;
                        int? tradeItemAmount = ((shopItem.OverrideTradeItemAmount > 0) ? shopItem.OverrideTradeItemAmount : new int?(itemData.TradeItemAmount));
                        if (tradeItemId == null || tradeItemAmount < 0)
                        {
                            tradeItemId = null;
                            tradeItemAmount = null;
                        }
                        if (itemData.IsRecipe)
                        {
                            item.Stack = 1;
                            availableStockLimit = LimitedStockMode.None;
                            availableStock = 1;
                        }
                        if (!itemData.IgnoreShopPriceModifiers)
                        {
                            price = Utility.ApplyQuantityModifiers(price, shop.PriceModifiers, shop.PriceModifierMode, null, null, item as Item, null, shopRandom);
                        }
                        price = Utility.ApplyQuantityModifiers(price, itemData.PriceModifiers, itemData.PriceModifierMode, null, null, item as Item, null, shopRandom);
                        if (!itemData.IsRecipe)
                        {
                            availableStock = (int)Utility.ApplyQuantityModifiers(availableStock, itemData.AvailableStockModifiers, itemData.AvailableStockModifierMode, null, null, item as Item, null, shopRandom);
                        }
                        if (!ShopBuilder.TrackSeenItems(stockedItemIds, item) || !itemData.AvoidRepeat)
                        {
                            if (availableStock < 0)
                            {
                                availableStock = int.MaxValue;
                            }
                            string syncKey = itemData.Id;
                            if (++i > 1)
                            {
                                syncKey += i;
                            }
                            int price2 = (int)price;
                            int stock2 = availableStock;
                            string tradeItem = tradeItemId;
                            int? tradeItemCount = tradeItemAmount;
                            LimitedStockMode stockMode = availableStockLimit;
                            string syncedKey = syncKey;
                            Item syncStacksWith = shopItem.SyncStacksWith;
                            List<string> actionsOnPurchase = itemData.ActionsOnPurchase;
                            stock.Add(item, new ItemStockInformation(price2, stock2, tradeItem, tradeItemCount, stockMode, syncedKey, syncStacksWith, null, actionsOnPurchase));
                        }
                    }
                }
            }
            Game1.player.team.synchronizedShopStock.UpdateLocalStockWithSyncedQuanitities(shopId, stock);
            return stock;
        }

        public void InvalidateCaches()
        {
            seedPriceCache.InvalidateCache();
            shopStock.InvalidateCache();
        }

        public void ForceRebuildCache()
        {
            seedPriceCache.RebuildCache();
            shopStock.RebuildCache();
        }

        public int GetCheapestSeedPrice(string cropId)
        {
            string unqualifiedId = cropId.TrimStart()[3..];
            if (seedPriceCache.GetCache().ContainsKey(unqualifiedId))
            {
                return seedPriceCache.GetCache()[unqualifiedId];
            }

            var chace = shopStock.GetCache();
            return chace
                .SelectMany(shop => shop.Value)
                .Where(item => item.Key.QualifiedItemId == cropId)
                .Select(item => item.Value.Price)
                .Where(x => x > 0)
                .DefaultIfEmpty(0)
                .Min();
        }

        public int GetExpensiveSeedPrice(string cropId)
        {
            string unqualifiedId = cropId.TrimStart()[3..];
            if (seedPriceCache.GetCache().ContainsKey(unqualifiedId))
            {
                return seedPriceCache.GetCache()[unqualifiedId];
            }
            var cache = shopStock.GetCache();
            return cache
                .SelectMany(shop => shop.Value)
                .Where(item => item.Key.QualifiedItemId == cropId)
                .Select(item => item.Value.Price)
                .Where(x => x > 0)
                .DefaultIfEmpty(0)
                .Max();
        }

        public int GetSpecificShopPrice(string cropId, string shopID)
        {
            return shopStock.GetCache()
                .Where(x => x.Key.Equals(shopID))
                .SelectMany(x => x.Value)
                .Where(shop => shop.Key.QualifiedItemId == cropId)
                .Select(shop => shop.Value.Price)
                .DefaultIfEmpty(-1)
                .FirstOrDefault();
        }
    }
}