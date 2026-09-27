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
4. Drop the contents of the provided folder into your `Stardew Valley/Mods` folder, or install from Nexus.

## Configuration

Every option can be changed in-game with [GMCM](https://www.nexusmods.com/stardewvalley/mods/5098), or in the `config.json` file that appears in the mod folder after the first launch.

- **Spawn rates**, per node and floor range: 0 means the node never spawns and 1 means every stone on those floors becomes that node. Rates below 0.10 go in steps of 0.0025. Skull Cavern floor N counts as mine floor 120 + N.
- **Mining experience** given by each node.

Rates overlap instead of adding up. Each stone rolls once, and the node with the lowest rate above the roll wins (nodes with the same rate split it). So the rarest node on a floor gets exactly its rate, and a more common node gets its rate minus the rates of the rarer ones.

For example, on Skull Cavern floors with the default settings, prismatic stones (0.025) take 2.5% of the stones, obsidian (0.05) another 2.5%, pure coal (0.07) 2% and coal (0.10) 3%.
