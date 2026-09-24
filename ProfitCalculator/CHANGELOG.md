# Changelog

## 2.1.0

- The calculator's screens (settings, results and machine results) are now UI Framework data in `assets/ui.json`
  instead of C# code. The C# side only supplies what it computes: the settings model, the produce types, the result
  rows, the Calculate / Reset commands and the translation and number formatting functions.
- The results show each crop's own sprite, and hovering any part of a row (including the sprite) shows the row's
  details. Both results screens share one tooltip definition.
- Requires UI Framework 1.8.0 or later.

### Removed

- The C# menu builders `ProfitCalculatorMainMenu` and `ProfitCalculatorResultsMenu`, replaced by the data screens.
- The `UseDataUI` config option (and its Generic Mod Config Menu entry and translations) that switched between the two.
- `Utils.IsTreeProduceType`, only used by the removed results screen.
