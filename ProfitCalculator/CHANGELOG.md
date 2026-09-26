# Changelog

## 2.1.0

- The calculator's screens (settings, results and machine results) are now UI Framework data in `assets/ui.json`
  instead of C# code. The C# side only supplies what it computes: the settings model, the produce types, the result
  rows, the Calculate / Reset commands and the translation and number formatting functions.
- The results show each crop's own sprite, and hovering any part of a row (including the sprite) shows the row's
  details. Both results screens share one tooltip definition.
- Requires UI Framework 1.8.0 or later.
- Split-screen: each player has their own settings and results; one player's Calculate no longer replaces the other's
  results. The plant list is built once, by the main screen.
- One broken (usually modded) plant no longer stops the calculation or its whole builder: it is skipped and logged once
  with the full error.
- Content Patcher edits to `Data/Crops`, `Data/FruitTrees`, `Data/WildTrees` or the `ManualCrops` asset made while a save
  is loaded now apply from the next calculation (or the next day); edits to `Data/Shops`, `SeedPrices` and
  `Data/Machines` apply right away.
- Fixed the farming-level bonus to the maximum harvest (`HarvestMaxIncreasePerFarmingLevel`): it is now multiplied by the
  farming level instead of dividing it, and also applies to crops whose base stack is 1. Vanilla crops are unaffected.
- The default Max money is now the player's own wallet when the farm uses separate wallets.
- A manual crop with `"AcceptsFertilizer": false` now ignores fertilizer entirely: no quality boost and no fertilizer
  cost, not just no speed boost.
- API: `SetSeedPrice(id, -1)` now restores the shop price (or the manual crop's `PurchasePrice`) for the loaded save too;
  `AddCrop` / `RemoveCrop` during play update the machine list right away, and a crop that `RemoveCrop` removed brings
  back the built-in one it replaced before the next calculation.
- The produce type dropdown follows a language change right away.
- The raw results screen draws the harvest item like the machine screen does.
- Wild trees are simulated once per calculation instead of five times, and their tooltip values no longer depend on the
  order they are read in.
- The tooltip delay option now says its unit: frames (60 = 1 second).
- French: added the 12 missing translations of the tree and machine features.
- The settings screen refreshes only when a setting changes (`ProfitCalculatorSettings` raises `PropertyChanged`).

### Removed

- The C# menu builders `ProfitCalculatorMainMenu` and `ProfitCalculatorResultsMenu`, replaced by the data screens.
- The `UseDataUI` config option (and its Generic Mod Config Menu entry and translations) that switched between the two.
- `Utils.IsTreeProduceType`, only used by the removed results screen.
- The unused manual crop fields `IsRaisedCrop`, `IsBushCrop` and `IsGiantCrop` (they are ignored if still present).
- `Utils.GetTranslatedSeason`, `GetTranslatedFertilizerQuality`, `GetAllTranslatedSeasons`,
  `GetAllTranslatedFertilizerQualities`, `GetSeasonDays` and `PriceFromObjectID`, unused since the port.
- `PlantData.Sprite`, `CropInfo.ProductCount`, `CropInfo.ChanceOfExtraProduct`, `Calculator.MinDay` / `MaxDay`,
  `Calculator.ClearCrops` and `Calculator.AddCrop` (replaced by `RebuildCrops`, `SetCrop` and `RemoveCrop`).
- The translation keys `autumn`, `spring-summer` and `summer-fall`.
- Leftover files: the "Profit Calculator Vintage" manifest, `README.txt`, `assets/readme.md` and
  `assets/text_box_small.png`; the old screenshots moved to `Page Files/ProfitCalculator/`.
- The XML documentation file is no longer shipped in the mod folder, and Harmony is no longer enabled.
