using Microsoft.Xna.Framework.Graphics;

using SObject = StardewValley.Object;

using StardewModdingAPI.Events;

using StardewValley.GameData.Objects;
using StardewValley.GameData.Shops;

namespace GiantCropFertilizer;

/// <summary>
/// Manages assets for this mod.
/// </summary>
internal static class AssetManager
{
    #region asset names
    private static IAssetName dataObjectInfo = null!;
    private static IAssetName dataShops = null!;
    private static IAssetName dataCraftingRecipes = null!;
    private static IAssetName textureLocation = null!;
    #endregion

    /// <summary>
    /// Initializes this asset manager.
    /// </summary>
    /// <param name="parser">Game content helper.</param>
    internal static void Init(IGameContentHelper parser)
    {
        dataObjectInfo = parser.ParseAssetName("Data/Objects");
        dataShops = parser.ParseAssetName("Data/Shops");
        dataCraftingRecipes = parser.ParseAssetName("Data/CraftingRecipes");
        textureLocation = parser.ParseAssetName("Mods/6135.GiantCropFertilizer/Object");
    }

    /// <inheritdoc cref="IContentEvents.AssetRequested"/>
    internal static void Apply(AssetRequestedEventArgs e)
    {
        if (ModEntry.Config.AllowGiantCropsOffFarm && e.DataType == typeof(xTile.Map))
        {
            e.Edit(static asset =>
            {
                asset.AsMap().Data.Properties["AllowGiantCrops"] = new("T");
            });
        }
        else if (e.NameWithoutLocale.IsEquivalentTo(textureLocation))
        {
            e.LoadFromModFile<Texture2D>("assets/object.png", AssetLoadPriority.Exclusive);
        }
        else if (e.NameWithoutLocale.IsEquivalentTo(dataObjectInfo))
        {
            e.Edit(
                apply: static (asset) =>
                {
                    asset.AsDictionary<string, ObjectData>().Data[ModEntry.GiantCropFertilizerID] = new()
                    {
                        Name = "Giant Crop Fertilizer",
                        Price = 100,
                        Edibility = -300,
                        Category = SObject.fertilizerCategory,
                        Type = "Basic",
                        DisplayName = ModEntry.I18n.Get("giant-crop-fertilizer.name"),
                        Description = ModEntry.I18n.Get("giant-crop-fertilizer.description"),
                        Texture = textureLocation.BaseName,
                        SpriteIndex = 0,
                    };
                },
                priority: AssetEditPriority.Early);
        }
        else if (e.NameWithoutLocale.IsEquivalentTo(dataShops))
        {
            e.Edit(static (asset) =>
            {
                if (!asset.AsDictionary<string, ShopData>().Data.TryGetValue("QiGemShop", out ShopData? shop))
                {
                    ModEntry.ModMonitor.Log("Could not find Qi Gem shop to edit.", LogLevel.Warn);
                    return;
                }

                shop.Items.Add(new()
                {
                    Id = ModEntry.GiantCropFertilizerID,
                    ItemId = ModEntry.QualifiedGiantCropFertilizerID,
                    TradeItemId = "(O)858",
                    TradeItemAmount = 5,
                });
                shop.Items.Add(new()
                {
                    Id = $"{ModEntry.GiantCropFertilizerID}_Recipe",
                    ItemId = ModEntry.QualifiedGiantCropFertilizerID,
                    IsRecipe = true,
                    TradeItemId = "(O)858",
                    TradeItemAmount = 100,
                });
            });
        }
        else if (e.NameWithoutLocale.IsEquivalentTo(dataCraftingRecipes))
        {
            e.Edit(static (asset) =>
            {
                // 1 iridium bar + 3 quality fertilizer -> 1, learned at Farming 10 or by buying the recipe from Qi. The key must match the item's internal name.
                asset.AsDictionary<string, string>().Data["Giant Crop Fertilizer"] =
                    $"337 1 369 3/Field/{ModEntry.GiantCropFertilizerID}/false/s Farming 10/{ModEntry.I18n.Get("giant-crop-fertilizer.name")}";
            });
        }
    }
}
