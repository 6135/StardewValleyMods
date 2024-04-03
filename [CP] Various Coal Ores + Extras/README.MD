# Various Coal Ores

Original mod: Various Coal Ores by Dojando
﻿All Credits go to him, including assets. All I did was update the mod to use the new Item Extensions Framework﻿.

This mod adds different Variations of Coal ore into the mines, as well as three other nodes.

Those Variations Include the default stone one added by Custom Ore Nodes mod by aedenthorn but also a lava rock and ice rock version for better looks, as well as a pure coal ore node that drops some extra coal. 

The 3 other nodes, that are inspired by "Gems and Ore Nodes" are:

  1. Mudstone: Drops clay, with the chance of dropping an actual Mudstone
  2. Obsidian: Drops Obsidian. Has a 1 in 10 chance to also drop a diamond
  3. Prismatic Ore: Drops a prismatic shard.

## Installation (Vortex)

1. Install [Generic Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098). Optional but recommended to allow for more customization of settings.
2. Install [Content Patcher](https://www.nexusmods.com/stardewvalley/mods/1915).
3. Install [Item Extensions Framework](https://www.nexusmods.com/stardewvalley/mods/20357).
5. Drop the contents of the provided folder into your `Stardew Valley/Mods` folder, or install from Nexus.

## Configuration

Configuration can now be done via [GMCM](https://www.nexusmods.com/stardewvalley/mods/5098), 0.00 means it won't spawn and 1.00 means every stone in the floor range (indicated by the configuration name and in the tooltip) will become of that stone.

### But it can also be done manually.

  1. To do this access the mod folder, usually it should be in the same directory as the game (Steam\steamapps\common\Stardew Valley\mods for the defaut steam install location)
  2. Open file called content.json with the text editor of your choice (notepad, vscode, notepad++ or something similar)
  3. Find the ore you want to edit.
  4. If you want to edit the chances, find the floors you want to edit (In case of the common coal ore, there's 3 levels, from floors 1-20, 21 to 39 and 121 to -999, floors above 120 apply to skull cavern levels)
  5. Change the SpawnFrequency value, which ranges from 1 (all stones will be converter to coal ores) or 0 (no coal ores) this can be a small value like 0.005 which would mean 0.5% of stones in that floor range would become coal ores.



