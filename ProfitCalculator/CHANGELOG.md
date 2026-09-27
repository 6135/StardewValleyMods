# Changelog

## 2.1.0

Everything since 1.3.2.

**Requirements:** SMAPI 4.0 or later (was 3.0) and [UI Framework](https://www.nexusmods.com/stardewvalley/mods/52945)
1.8.0 or later, a new required mod that draws the calculator's menus.

### New

- **New menus.** The settings and results screens are rebuilt with UI Framework:
  - themes, text scaling, and keyboard and gamepad navigation;
  - windows you can drag, collapse and resize, with the position remembered per save;
  - `Enter` calculates and `Escape` goes back.
- **Results table.** The results are a table sorted by profit per day; click a column header to sort by it.
  - Each row shows the crop's sprite.
  - Hovering any part of a row shows the full breakdown: seed and fertilizer cost, growth and regrowth time, harvests,
    drop counts, quality chances, what the crop is sold as, and the day it pays back its seed.
- **Machines.** The produce type lists every machine that accepts a crop, read from the game's machine data: Keg,
  Preserves Jar, Dehydrator, Mill, Oil Maker, Seed Maker and modded machines.
  - "Machine + Cask" options cover keg goods aged to iridium quality.
  - Recipes that need several items (5 fruit for the Dehydrator) count that.
  - The Artisan profession applies.
  - The machine results show the product, the inputs per product and the processing time.
- **Fruit trees** (their own produce type): 28 days to mature, then one fruit a day in season (every day in the
  greenhouse). Fruit quality rises with the tree's age.
- **Wild trees with a tapper** (their own produce type): oak, maple, pine, mystic, mushroom and more.
  - Growth time is averaged from the tree's growth chance.
  - New `Heavy tapper` and `Tree fertilizer` options.
  - The Tapper profession applies, and winter stumps produce nothing.
- **Years** (1 to 10): how long trees and machine chains are counted. The tooltip shows the payback day, or that the
  plant never pays back within that time.
- **More plants:** the tea bush, crops that drop several different items, and the current Custom Bush API (optional).
- **Cross season:** a crop that also grows in the following seasons keeps producing into them (off by default).
- **Harvest size:** farming-level buffs and the extra-harvest chance are counted.
- **Content Patcher support.** `ManualCrops` and `SeedPrices` are game assets
  (`Mods/6135.ProfitCalculator/ManualCrops` and `.../SeedPrices`), so Content Patcher packs can edit them.
  - Manual crop keys can be bare or qualified item ids.
  - Edits made while a save is loaded, to these assets or to the game's crop, tree, shop and machine data, apply
    without reloading the save.
- **Mod API:** other mods can add or remove crops and override seed prices (`IProfitCalculatorApi`: `AddCrop`,
  `RemoveCrop`, `SetSeedPrice`).
- **Split-screen:** each player has their own settings and results.
- **French:** translations for all the new features.

### Changed

- The tooltip delay option now says its unit: frames (60 = 1 second).
- The default Max money is the player's own wallet when the farm uses separate wallets.
- A manual crop with `"AcceptsFertilizer": false` ignores fertilizer entirely: no speed boost, no quality boost and no
  fertilizer cost.

### Fixed

- The farming-level bonus to the maximum harvest (`MaxHarvestsIncreasePerFarmingLevel`) is multiplied by the farming
  level instead of divided by it, and it also applies to crops whose base stack is 1. Vanilla crops are unaffected.
- One broken plant (usually from another mod) no longer stops the whole calculation. It is skipped and logged.
- Long crop names no longer shrink to unreadable text.
- The settings menu no longer has to be reopened after the game window or UI scale changes.

### Removed

- The placeholder `Keg` and `Cask` produce types ("Not implemented"), replaced by the real machine list.
- The manual crop fields `IsRaisedCrop`, `IsBushCrop` and `IsGiantCrop`, which did nothing. Files that still have them
  load fine.
