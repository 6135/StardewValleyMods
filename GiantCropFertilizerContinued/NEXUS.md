# Giant Crop Fertilizer Continued

## Nexus description (BBCode)

```
[size=5][b]Giant Crop Fertilizer Continued[/b][/size]

Grow giant crops on purpose instead of hoping for a 1% roll. A continuation of atravita's [url=https://www.nexusmods.com/stardewvalley/mods/11620]Giant Crop Fertilizer[/url], updated for Stardew Valley 1.6.15 and no longer requiring Json Assets, SpaceCore or AtraCore.

[size=4][b]How it works[/b][/size]
[list]
[*]Plant a 3x3 patch (or a larger custom footprint) of a crop that can become giant.
[*]Put one Giant Crop Fertilizer on any tile of that patch. Fertilized soil is tinted purple.
[*]The night the crop is mature and watered, a giant crop forms (chance configurable; default 1.1 = guaranteed).
[*]The fertilizer is consumed when the giant crop forms. Multiple fertilizers in the same footprint are all consumed.
[*]If the roll fails, the vanilla 1% chance still applies and the fertilizer stays for the next night.
[/list]
Works with every crop in Data/GiantCrops, including custom ones (6480's Giant Crops, powdermelon, etc.).

[size=4][b]Getting the fertilizer[/b][/size]
[list]
[*]Mr. Qi's gem shop: 5 Qi gems each.
[*]Crafting: 1 Iridium Bar + 3 Quality Fertilizer = 1 Giant Crop Fertilizer. Recipe learned at Farming level 10, or bought from Mr. Qi's gem shop for 100 Qi gems.
[*]5% drop from Big Slimes on Skull Cavern floor 120+ while dangerous mines are active.
[/list]

[size=4][b]Configuration[/b][/size]
Edit config.json or use Generic Mod Config Menu.
[list]
[*][b]GiantCropChance[/b] (0 to 1.1, default 1.1): chance a fertilized patch turns giant each night. 1.1 means guaranteed.
[*][b]AllowGiantCropsOffFarm[/b]: sets the AllowGiantCrops map property everywhere, so giant crops can form off the farm (Ginger Island, greenhouse, modded maps). Applies without restarting.
[/list]

[size=4][b]Requirements[/b][/size]
[list]
[*]Stardew Valley 1.6.15, SMAPI 4.0+.
[*]Optional: Generic Mod Config Menu.
[*]Multiplayer: all players need the mod.
[/list]

[size=4][b]Install[/b][/size]
Unzip into Stardew Valley/Mods and run the game through SMAPI. Remove the original Giant Crop Fertilizer if you have it.

[b]Upgrading from 1.5 saves:[/b] the new item ID is 6135.GiantCropFertilizer_Fertilizer. Old Json Assets fertilizer items and fertilized tiles do not carry over.

[size=4][b]Compatibility[/b][/size]
[list]
[*]Works with custom giant crops added through Data/GiantCrops.
[*]MultiFertilizer and More Giant Crops integrations were dropped (both mods are dead).
[*]Looking for something else? [b]Grow That Giant Crop[/b] or [b]Configurable Giant Crops[/b] (more cheat-style) are alternatives.
[/list]

[size=4][b]Translations[/b][/size]
English, Russian, Chinese (from the original mod). New translations welcome.

[size=4][b]Credits[/b][/size]
Original mod by [b]atravita[/b], MIT licensed with "modify OK, credit me" permissions. Thank you! Port and maintenance by 6135.
Source: [url=https://github.com/6135/StardewValleyMods]https://github.com/6135/StardewValleyMods[/url]
```

## Additional changes vs original
- No Json Assets, SpaceCore or AtraCore: the fertilizer is a native 1.6 item.
- New crafting recipe: 1 Iridium Bar + 3 Quality Fertilizer, learned at Farming 10 or bought from Qi for 100 Qi gems.
- The fertilizer works on any tile of the giant crop's footprint, including larger custom giant crops.
- Fixed the fertilizer not being consumed when a giant crop formed.
- Changing the off-farm option in GMCM now applies without restarting.
- Dropped the dead MultiFertilizer and More Giant Crops integrations.

## Changelog
### 1.0.0
- Ported to Stardew Valley 1.6.15 and SMAPI 4.
- Removed the Json Assets, SpaceCore and AtraCore requirements.
- New item ID 6135.GiantCropFertilizer_Fertilizer; 1.5 fertilizer items don't carry over.
- Added a crafting recipe (1 Iridium Bar + 3 Quality Fertilizer), learned at Farming 10.
- Added the recipe to Mr. Qi's gem shop for 100 Qi gems.
- The fertilizer now works on any tile of a giant crop's footprint.
- Supports every crop in Data/GiantCrops, including larger custom giant crops.
- Fixed the fertilizer not being consumed when the giant crop formed.
- The off-farm option now applies without restarting.
- Removed the MultiFertilizer and More Giant Crops integrations.

## Comment for the original mod page
Hi! I've put together a continuation of this mod for Stardew Valley 1.6.15: Giant Crop Fertilizer Continued (https://www.nexusmods.com/stardewvalley/mods/53558). It no longer needs Json Assets, SpaceCore or AtraCore and works with custom giant crops from Data/GiantCrops. Huge thanks to atravita for the original mod and for the open permissions. Full credit is on the page.
