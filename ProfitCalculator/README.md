# Profit Calculator

This is a simple profit calculator that calculates the profit of a product based on the cost of buying the seed, selling price and the number of units sold and their quality.

Provides the ability to select whether the user wants to buy seeds or fertilizer and the quality of said fertilizer. The user can select the day of the season and the season itself.

## Installation

1. Install [SMAPI](https://smapi.io/).
2. Install [Generic Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098). Optional but recommended to allow for more customization of settings.	
3. Drop the contents of the provided folder into your `Stardew Valley/Mods` folder, or install from Nexus.
4. Run the game using SMAPI.
5. Press `F8` to open the calculator. This can be changed in the config file or in the Generic Config Menu.

## Configuration

The config file is located in `Stardew Valley/Mods/ProfitCalculator/config.json`. It allows you to change the keybind to open the calculator and the time for the tooltip to appear.

## Seed Price Override

The mod will automatically calculate the seed price based on shop stock. However, if you want to override the seed price, you can do so by adding a `price` to the `SeedPrices.json` file in the `assets` folder. The `price` field should be a number. For example, if you want to override the seed price for the potato crop, you would add the following to the `SeedPrices.json` file:

```json
{
  //"SeedID": "price"
  "475": 50,
}
```

## Manual Crops 

If you want to add a crop that is not in the game, you can do so by adding a `crop` to the `ManualCrops.json` file in the `assets` folder. The `crop` field should be an object with the following fields (this example is for the tea bush crop):

```json
{
  "215": {
    "Name": "Tea Leaves", // Name
    "HarvestID": 815, // Harvest ID (Id of item that drops)
    "GrowthTime": 20, //growth time
    "RegrowthTime": 1, //regrowth time
    "Seasons": "spring summer fall", //seasons
    "SalePrice": 50, //sale price
    "PurchasePrice": 1500, //purchase price
    "MinimumHarvests": 1, //min harvest
    "MaximumHarvests": 1, //max harvest
    "MaxHarvestsIncreasePerFarmingLevel": 0, //max harvest increase per farming level
    "ExtraHarvestChance": 0.0, //chance for extra harvest
    "HasQuality": false, //affected by quality
    "AcceptsFertilizer": false, //affected by fertilizer
    "IsRaisedCrop": false, //raised crop
    "IsBushCrop": true, //bush crop
    "IsPaddyCrop": false, //paddy crop
    "IsGiantCrop": false //giant crop
  }
}
```

## Know Issues

1. The mod does not take into account the farming level buffs. This is because I don't know how to get the farming level buffs. If anyone knows how to get them, please let me know.
2. The mod does not take into account the luck based chances of getting extra items.
3. Some extra large text may be too small to read. This is because the mod lowers the size of huge text to make it fit in the crop box. If you notice this, please let me know which crop it is and from which mod it is from so I can figure out a proper fix.
4. When changing the scale of the game, the main options menu will be scaled but the options will be in the original positions, to fix this you need to close and reopen the options menu. 

### TODO:

- [X] Take Fertilizer into account.
- [X] Take Quality into account.
- [ ] Add proper scaling support for options menu.
- [X] Obtain Seed prices from stores
- [ ] Add support to multi-drop crops.
- [ ] Add support for fruit trees.
- [ ] Add options to disable cross season crops.
- [ ] Add Support for different types of output. (i.e. Jelly, Wine, Juice, etc.)
- [ ] Possibly add easer ways to add manual crops and seed prices maybe through a config menu or command.
