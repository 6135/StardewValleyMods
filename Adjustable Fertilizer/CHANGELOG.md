# Changelog

## 1.2.0

Updated for Stardew Valley 1.6.

### Fixed

- Recipes kept their 1.5 ingredients: Quality Fertilizer cost 2 Sap instead of 4, Speed-Gro took a Clam instead of 5
  Moss, and Deluxe Speed-Gro took a Coral instead of 5 Bone Fragments and unlocked at Farming 9 instead of 8. The mod
  now changes only how many items a craft makes, so ingredients and unlock levels always match the game.
- Quality Fertilizer defaulted to 1 per craft; the game makes 2.
- The manifest still had the template's name, author and ID. It now uses the original mod's ID
  (`UnknownLegacy.AdjustableFertilizer`), so this version replaces existing installs and keeps their settings.

### Changed

- Each amount is picked from 1 to 50 in the config menu instead of free text that could break the recipe.
