using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Shops;
using StardewValley.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ProfitCalculator.main.accessors
{
    /// <summary>
    /// The ShopAccessor class provides methods to access and manage shop data, including seed prices and shop stock information.
    /// It uses caching to improve performance by storing frequently accessed data.
    /// </summary>
    public class ShopAccessor
    {
        // Cache for storing seed prices
        private readonly Cache<Dictionary<string, int>> seedPriceCache;

        // Cache for storing shop stock information
        private readonly Cache<Dictionary<string, Dictionary<ISalable, ItemStockInformation>>> shopStock;

        /// <summary>
        /// Initializes a new instance of the <see cref="ShopAccessor"/> class.
        /// </summary>
        public ShopAccessor()
        {
            var Helper = Container.Instance.GetInstance<IModHelper>();
            // Initialize seed price cache with data from SeedPrices.json
            seedPriceCache = new(
                    () => Helper?.ModContent.Load<Dictionary<string, int>>(Path.Combine("assets", "SeedPrices.json"))
                );
            // Initialize shop stock cache
            shopStock = new(BuildCache);
        }

        /// <summary>
        /// Builds the cache for shop stock information.
        /// </summary>
        /// <returns>A dictionary containing shop stock information.</returns>
        private Dictionary<string, Dictionary<ISalable, ItemStockInformation>> BuildCache()
        {
            Dictionary<string, ShopData> shopData = DataLoader.Shops(Game1.content);
            Dictionary<string, Dictionary<ISalable, ItemStockInformation>> cache = new();
            // For each shop, get shop stock
            foreach (var shop in shopData.Where(x => x.Value.Currency == 0))
            {
                cache.Add(shop.Key, GetShopStock(shop.Key, shop.Value));
            }
            return cache;
        }

        /// <summary>
        /// Gets the stock information for a specific shop.
        /// </summary>
        /// <param name="shopId">The ID of the shop.</param>
        /// <param name="shop">The shop data.</param>
        /// <returns>A dictionary containing the stock information for the shop.</returns>
        public static Dictionary<ISalable, ItemStockInformation> GetShopStock(string shopId, ShopData shop)
        {
            var Monitor = Container.Instance.GetInstance<IMonitor>();
            Dictionary<ISalable, ItemStockInformation> stock = new();
            List<ShopItemData> items = shop.Items;
            if (items != null && items.Count > 0)
            {
                Random shopRandom = Utility.CreateDaySaveRandom();
                HashSet<string> stockedItemIds = new();
                ItemQueryContext itemQueryContext = new(Game1.currentLocation, Game1.player, shopRandom);

                HashSet<string> syncKeys = new();
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
                        int? tradeItemAmount = shopItem.OverrideTradeItemAmount > 0 ? shopItem.OverrideTradeItemAmount : new int?(itemData.TradeItemAmount);
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

        /// <summary>
        /// Invalidates the caches for seed prices and shop stock.
        /// </summary>
        public void InvalidateCaches()
        {
            seedPriceCache.InvalidateCache();
            shopStock.InvalidateCache();
        }

        /// <summary>
        /// Forces a rebuild of the caches for seed prices and shop stock.
        /// </summary>
        public void ForceRebuildCache()
        {
            seedPriceCache.RebuildCache();
            shopStock.RebuildCache();
        }

        /// <summary>
        /// Gets the cheapest seed price for a given crop ID.
        /// </summary>
        /// <param name="cropId">The ID of the crop.</param>
        /// <returns>The cheapest seed price.</returns>
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

        /// <summary>
        /// Gets the most expensive seed price for a given crop ID.
        /// </summary>
        /// <param name="cropId">The ID of the crop.</param>
        /// <returns>The most expensive seed price.</returns>
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

        /// <summary>
        /// Gets the price of a specific crop in a specific shop.
        /// </summary>
        /// <param name="cropId">The ID of the crop.</param>
        /// <param name="shopID">The ID of the shop.</param>
        /// <returns>The price of the crop in the specified shop.</returns>
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