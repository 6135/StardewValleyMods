# Profit Calculator

This is a simple profit calculator that calculates the profit of a product based on the cost of buying the seed, selling price and the number of units sold and their quality.

Provides the ability to select whether the user wants to buy seeds or fertilizer and the quality of said fertilizer. The user can select the day of the season and the season itself.

## Installation

1. Install [SMAPI](https://smapi.io/).
2. Install [UI Framework](https://github.com/6135/StardewValleyMods/tree/master/StardewUIFramework) (`6135.UIFramework`). Required: the calculator's menus are built with it.
3. Install [Generic Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098). Optional but recommended to allow for more customization of settings.
3. Drop the contents of the provided folder into your `Stardew Valley/Mods` folder, or install from Nexus.
4. Run the game using SMAPI.
5. Press `F8` to open the calculator. This can be changed in the config file or in the Generic Config Menu.

## Configuration

The config file is located in `Stardew Valley/Mods/ProfitCalculator/config.json`. It allows you to change the keybind to open the calculator and the time for the tooltip to appear.

The look of the menus (theme, text scale, reduced motion) is configured in the UI Framework's own config / Generic Config Menu page and applies to every mod that uses it.

## Usage

Press the hotkey to open the settings screen, adjust the day, season, fertilizer and money options and press `Calculate` (or `Enter`). The results screen lists every crop that can still be harvested with those settings, sorted by profit per day; click a column header to sort by it, and hover a row for the full breakdown (seed / fertilizer cost, growth and regrowth time, harvests, drop counts, quality chances and what the crop is sold as). `Escape` returns to the settings screen. The windows can be dragged, collapsed and resized; their position is remembered per save.

Crops, fruit trees, the tea bush and bushes from the Custom Bush mod (optional) are all included.

### Cross season

When `Cross season` is on, a crop that also grows in the following seasons keeps producing into them. Turn it off to only count the days left in the selected season.

### Produce type

`Raw` sells the harvest as is. Every machine that accepts at least one crop (Keg, Preserves Jar, Dehydrator, Mill, Oil Maker, Seed Maker, modded machines...) is listed too, read straight from the game's machine data, and so are "machine + Cask" options for keg goods that can be aged to iridium quality. With a machine selected:

- crops the machine doesn't accept are hidden;
- when a recipe needs several items (the Dehydrator takes 5 fruit, the Keg takes 5 coffee beans), each crop is worth the product's price divided by that count;
- the Artisan profession bonus applies (unless base stats are used), and crop quality is ignored since machines don't keep it.

Machines are assumed to be unlimited: processing time is shown in the tooltip but doesn't slow anything down.

## Seed Price Override

The mod reads seed prices from shop stock. To override a price, add the unqualified seed id and its price to `assets/SeedPrices.json`, for example for potato seeds:

```json
{
  //"SeedID": price
  "475": 50
}
```

The file is loaded into the game asset `Mods/6135.ProfitCalculator/SeedPrices`, so a Content Patcher pack can add prices with `EditData` instead of editing the file.

## Manual Crops

To add a crop the game data doesn't describe, add it to `assets/ManualCrops.json` (loaded into the asset `Mods/6135.ProfitCalculator/ManualCrops`, which Content Patcher packs can also edit). The key is the seed's item id, either bare (`"472"`) or qualified (`"(O)472"`). Manual entries take precedence over built-in data.

```json
{
  "472": {
    "Name": "Parsnip",                  // optional, defaults to the harvest item's name
    "HarvestID": 24,                    // item that drops
    "GrowthTime": 4,                    // days to grow
    "RegrowthTime": -1,                 // days to regrow, -1 if it doesn't
    "Seasons": "spring",                // space separated
    "SalePrice": 0,                     // overrides the harvest's price when > 0
    "PurchasePrice": 20,                // seed price when > 0
    "MinimumHarvests": 1,
    "MaximumHarvests": 1,
    "MaxHarvestsIncreasePerFarmingLevel": 0,
    "ExtraHarvestChance": 0.0,
    "HasQuality": true,                 // affected by quality
    "AcceptsFertilizer": true,
    "IsPaddyCrop": false,               // grows faster near water
    "IsRaisedCrop": false,              // accepted but unused
    "IsBushCrop": false,                // accepted but unused
    "IsGiantCrop": false                // accepted but unused
  }
}
```

## Mod API

Other SMAPI mods can get `IProfitCalculatorApi` (see `apis/IProfitCalculatorApi.cs`) with `Helper.ModRegistry.GetApi<IProfitCalculatorApi>("6135.ProfitCalculator")`:

- `AddCrop(seedItemId, harvestItemId, growthDays, regrowDays, seasons)` adds or replaces a crop;
- `RemoveCrop(seedItemId)` removes a crop added through the API;
- `SetSeedPrice(seedItemId, price)` overrides a seed price (a negative price clears it).

For the full set of fields, edit the `ManualCrops` asset instead.

## Known Issues

1. Machines are assumed to be unlimited: processing time is shown but doesn't limit how many crops can be processed.
2. Fruit tree quality doesn't increase with the tree's age.

Farming-level buffs are counted (the game's farming level includes them), and the extra-harvest chance is modeled; luck doesn't affect crop yield.

### TODO

- [X] Take Fertilizer into account.
- [X] Take Quality into account.
- [X] Add proper scaling support for options menu.
- [X] Obtain Seed prices from stores
- [X] Add support to multi-drop crops.
- [X] Add support for fruit trees.
- [X] Add options to disable cross season crops.
- [X] Add Support for different types of output. (i.e. Jelly, Wine, Juice, etc.)
- [X] Easier ways to add manual crops and seed prices (content assets and a mod API).
