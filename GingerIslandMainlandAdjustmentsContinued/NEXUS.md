# Ginger Island Mainland Adjustments Continued

## Short description

Adds dialogue keys for NPCs headed to and returning from Ginger Island, evening schedules *after* island trips, and an optional island scheduler with NPC groups and more visitors. A continuation of atravita's Ginger Island Mainland Adjustments for Stardew Valley 1.6.15, no AtraCore needed.

## Full description

A continuation of atravita's [Ginger Island Mainland Adjustments](https://www.nexusmods.com/stardewvalley/mods/10004), updated for Stardew Valley 1.6.15 and no longer requiring AtraCore. The included content pack is bundled in the same download.

Ginger Island isn't well integrated with the rest of NPC scheduling and dialogue. Pierre can tell you he'd rather be outside than running his shop one minute, and the next you'll find him at the Resort. Jodi might tell you to wipe your feet while she's out on the beach. This mod adds **specific dialogue keys** that are only used while an NPC is on the mainland and headed to or back from Ginger Island. A few characters have lines in the included content pack; the [documentation](https://github.com/6135/StardewValleyMods/blob/master/GingerIslandMainlandAdjustmentsContinued/docs/GIDialogueKeys.MD) explains how to add more.

In vanilla, NPCs head straight to bed after an island trip (except Gus), skipping their evening plans and leaving, say, Demetrius dancing alone if Robin went to the island. **GIRemainder schedules** now take over for the evening, so characters can make a stop or two before going home. The included content pack has them for the vanilla cast (and some SVE characters); [here's how to add more](https://github.com/6135/StardewValleyMods/blob/master/GingerIslandMainlandAdjustmentsContinued/docs/GIRemainderSchedules.md).

The mod can also replace the vanilla island scheduler (on by default) to add:
- **Custom NPC groups.** Vanilla NPCs visit in groups; those groups now live in a data asset, so mod authors can add more. Ridgeside Village and Jorts and Jean use this.
- **More visitors.** Raise `Capacity` (up to 15) to send more NPCs each day. Great for gifting runs.
- **Exploring.** Visitors can wander to the Island North and South East areas, and the occasional adventurous group goes further.
- **Gus's day.** Pick a day of the week when Gus is more likely to go.
- **Willy, Sandy, George, Evelyn and the Wizard can visit** (configurable). Willy and Sandy leave a box on their counter so you can still shop. For Sandy beach sprites, see violetlizabet's mod.

### Install
1. Install [SMAPI](https://smapi.io) and [Content Patcher](https://www.nexusmods.com/stardewvalley/mods/1915).
2. Unzip into `Stardew Valley/Mods`. You get the code mod and its content pack.
3. Remove the original Ginger Island Mainland Adjustments (and its content pack) and AtraCore if nothing else needs it.

### Configuration
Run the game once to create `config.json`, or use Generic Mod Config Menu.
- **UseThisScheduler**: use this mod's island scheduler. The options below only apply when it's on.
- **Capacity**: maximum number of resort visitors per day (0–15, default 6).
- **StageFarNpcsAtSaloon**: NPCs who live far away start the day at the Saloon.
- **GroupChance** / **ExplorerChance**: chance a group visits the resort / explores the north of the island.
- **GusDay** / **GusChance**: Gus's day and how likely he is to go that day (`None` to disable).
- **AllowWilly**, **AllowSandy**, **AllowWizard** (Yes / No / If married) and **AllowGeorgeAndEvelyn**.
- **WearIslandClothing**: whether visitors change into beachwear.
- **RequireResortDialogue**: only NPCs with a `Resort` dialogue line can visit.
- **EnforceGITiming**: warns about and skips schedule points that are too close together. Useful when writing schedules.
- **Schedule strictness** (per NPC, in-game menu): whether an NPC with a special schedule for the day may still visit.

### Console commands
- `av.gima.get_islanders`: who is going to the island today.
- `av.gima.get_schedule <NPC>`: an NPC's schedule for the day.
- `av.gima.get_locations_list`: dumps the NPC route list.

### For mod authors
Everything from the original is kept: the `Mods/atravita_Ginger_Island_Adjustments_groups`, `_explorers`, `_bartenders`, `_musicians` and `_exclusions` assets, all dialogue and schedule keys, and the event and mail IDs. Existing content packs keep working.

One change: the Content Patcher tokens moved with the mod ID. Replace `atravita.GingerIslandMainlandAdjustments/` with `6135.GingerIslandMainlandAdjustments/` in `IslandOpen`, `Bartender`, `Musician` and `Islanders`.

### Compatibility
- Stardew Valley 1.6.15, SMAPI 4.0+. Works in single player, multiplayer and split-screen.
- Works with Holiday Sales Continued.
- Content packs that set `CanVisitIsland` in `Data/Characters` are respected.
- Mods that change the Ginger Island maps may conflict, since this mod (like vanilla) uses fixed spots on the island.

### Credits
Original mod by atravita, MIT licensed; atravita allowed anyone to remove the AtraCore dependency and re-upload. Port and maintenance by 6135. Source: https://github.com/6135/StardewValleyMods
