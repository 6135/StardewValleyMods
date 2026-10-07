# Plan: update Giant Crop Fertilizer for Stardew Valley 1.6.15 without AtraCore or Json Assets

Giant Crop Fertilizer ([Nexus 11620](https://www.nexusmods.com/stardewvalley/mods/11620)) by atravita, MIT. 22k downloads. Last release: v0.2.2 (24 Mar 2023, game 1.5.6). Target: Stardew Valley 1.6.15, SMAPI 4.5.x, .NET 6. Same setup as `HolidaySalesContinued`.

## Continuations checked (7 Oct 2026)

**None found.**
- **Nexus:** name searches ("Giant Crop", "Giant Fertilizer", "Fertilizer", …) and description searches ("Giant Crop Fertilizer", "chance of giant", …) found no port or remake.
- **GitHub:** no fork has worked on `GiantCropFertilizer/`.
- **SMAPI compatibility list:** `atravita.GiantCropFertilizer` is marked broken in 1.6, "retest when AtraCore works", with no workaround.
- **Comments:** about a dozen update requests (Mar 2024 to Apr 2025). The author never replied.
- **Signs of demand elsewhere:** Grow That Giant Crop's page says it exists because "the mods that provide proper end-game ways to grow giant crops" weren't updated. A Giant Crop Ring user also pointed to this mod as the alternative.

Nearest alternatives, none of them a fertilizer:

| Mod | What it does | Difference |
|---|---|---|
| [Grow That Giant Crop](https://www.nexusmods.com/stardewvalley/mods/23760) (MercuryVN, 33k, May 2024) | hotkey turns a mature, watered 3×3 into a giant crop | cheat button, no item, no cost |
| [Giant Crop](https://www.nexusmods.com/stardewvalley/mods/47023) (Yaolurk, 0.5k, Jun 2026) | F6 hotkey, vanilla crops only | same, cheat |
| [Configurable Giant Crops](https://www.nexusmods.com/stardewvalley/mods/25310) (NoraCharles, 3.9k, Jul 2024) | global chance per vanilla crop (up to 100%), greenhouse / island | farm-wide, not per spot; vanilla crops only |
| [More Giant Crop Locations](https://www.nexusmods.com/stardewvalley/mods/22815) (66k, Mar 2026) | allows giant crops in greenhouse / island farm | location only |
| [Giant Crop Ring](https://www.nexusmods.com/stardewvalley/mods/1182) | ring raising the chance | also broken |

The mod's niche is an earned, per-spot item (Qi gem shop, rare slime drop) that guarantees a giant crop where you put it. No current mod fills it. Permission: Nexus "Modify OK, credit me" plus MIT source, and atravita has publicly invited people to strip AtraCore and re-upload.

## Features to keep

1. A fertilizer item, purple-tinted on the soil.
2. A fertilized tile gets a configurable giant-crop chance (default 1.1, so guaranteed) instead of the normal 1%.
3. The fertilizer is consumed when the giant crop forms.
4. It is sold in Mr. Qi's gem shop for 5 Qi gems.
5. 5% drop from Big Slimes on floor 120+ of the Skull Cavern with the Qi "dangerous" mines active.
6. Optional "allow giant crops off the farm".
7. GMCM options (chance and off-farm).

Drop these:
- Json Assets, PyTK-era save fixes (`FixSaveThing`) and the `RemoveFarmCheck` transpiler. 1.6 has the `AllowGiantCrops` map property.
- MultiFertilizer patches. MultiFertilizer is dead; its replacement is Ultimate Fertilizer (see tests).
- The More Giant Crops branch in GMCM (that mod is dead too).

## Starting point

Base the port on atravita's `alpha` branch, `GiantCropFertilizer/GiantCropFertilizer/` (v0.2.2-dev, commits 24 Nov – 28 Dec 2024, last message "the long and slow process of fixing transpilers"). It already moved to 1.6:
- **`AssetManager.cs` (85 lines):** adds the item to `Data/Objects` (category `-19`, texture `Mods/atravita/GiantCropFertilizer/Object`), adds it to the `QiGemShop` in `Data/Shops` (5 × `(O)858`), and sets `AllowGiantCrops` on maps when configured. **Keep as is.**
- **`CropTranspiler.cs`:** transpiler on `Crop.TryGrowGiantCrop`, replacing the chance and clearing the fertilizer.
- **`HoeDirtDrawTranspiler.cs`:** transpiler on `HoeDirt.DrawOptimized`, tinting the fertilizer purple.
- **`PostfixBigSlimeConstructor.cs`:** Big Slime drop.
- **`ModEntry.cs`:** entry, GMCM, migration manager.

About 450 lines. What still ties it to atravita's libraries:

| File | Dependency | Replace with |
|---|---|---|
| `GiantCropFertilizer.csproj` | `csproj_common`, project refs to **AtraCore** and **Pintail** | standalone SDK csproj like `HolidaySalesContinued` |
| `ModEntry.cs` | `BaseMod<T>`, `MiniAtraShared.Utils.GetConfigOrDefault`, `GMCMHelper`, `AsyncWriteConfig`, `harmony.Snitch`, `MultiplayerHelpers.AssertMultiplayerVersions`, `MigrationManager`, `TKConstants.Hot` | `Mod`, `ReadConfig`/`WriteConfig`, the `IGenericModConfigMenuApi` copy already in `HolidaySalesContinued` |
| `ModConfig.cs` | `GMCMRange`/`GMCMInterval` attributes | plain properties; set the range in the GMCM call |
| `CropTranspiler.cs` | `ILHelper`, `GetCachedField/Method` | **prefix + postfix, no transpiler** (step 4) |
| `HoeDirtDrawTranspiler.cs` | `ILHelper`, `GetCachedMethod/Property`, `StaticMethodNamed` | Harmony `CodeMatcher` (step 5) |
| `PostfixBigSlimeConstructor.cs` | `OfChance`, `LogError`, `StyleCopConstants` | `Game1.random.NextBool(0.05)` |
| `DataModels/GiantCropFertilizerIDStorage.cs` | (none) | only needed for the legacy migration (step 6) |

Game code checked against the decompiled 1.6.15 `Stardew Valley.dll`:
- **`Crop.TryGrowGiantCrop(bool checkPreconditions = true, Random random = null)` (`Crop.cs:919`)**
  - Called from `Crop.newDay` every night that the crop is watered or doesn't need water, including mature crops.
  - Takes the location check (`Farm` or the `AllowGiantCrops` map property), then the giant-crop entries from `Data/GiantCrops`.
  - For each entry it rolls `Chance < 1f && !random.NextBool(Chance)`, then checks `Condition`, checks a `TileSize` grid of the same crop with the **calling crop at the top-left**, clears the crops and adds a `GiantCrop`.
- **Fertilizer:** `HoeDirt.plant(id, who, isFertilizer: true)` accepts any item and stores the **qualified** ID (`(O)…`) in `HoeDirt.fertilizer`. `CheckApplyFertilizerRules` only blocks quality fertilizers after sprouting, so this item can be applied any time.
- **Drawing:** `HoeDirt.DrawOptimized` draws `Game1.mouseCursors` with `GetFertilizerSourceRect()` and `Color.White` (`HoeDirt.cs:1023`). This is the IL shape the alpha transpiler expects.
- **Big Slime:** `BigSlime(Vector2, int mineArea)` and `BigSlime.heldItem` (`NetRef<Item>`) still exist. `MineShaft.GetAdditionalDifficulty()` exists.

## Steps

### 1. Set up the project
- **Folder:** `GiantCropFertilizerContinued/`, added to `Stardew Mods.sln`. Copy `HolidaySalesContinued.csproj` and set `AssemblyName` to `GiantCropFertilizerContinued` and `RootNamespace` to `GiantCropFertilizer`.
- **Manifest:**
  - `UniqueID` `6135.GiantCropFertilizer`, `Author` "atravita, 6135", version 1.0.0, `MinimumApiVersion` 4.0.0.
  - Optional GMCM dependency only, no Json Assets or SpaceCore.
  - Update key after the Nexus page exists.
- **Licence and assets:**
  - Copy the MIT `LICENSE` with atravita's copyright, and add yours.
  - Take the sprite from `assets/json-assets/Objects/GiantCropFertilizer/` on `main` and save it as `assets/object.png`. Do the same with `i18n/`.

### 2. Item ID
- Use a new 1.6-style ID, `6135.GiantCropFertilizer_Fertilizer`, and derive the qualified form from `ItemRegistry.type_object`.
- Make one helper, `IsGiantCropFertilizer(string? id)`, that accepts both the qualified and unqualified forms, and use it everywhere.
- **Bug in the alpha code:** `RemoveFertilizer` compares `fertilizer.Value == GiantCropFertilizerID` (unqualified). The game stores the qualified ID, so the fertilizer would never be removed.
- Texture asset: `Mods/6135.GiantCropFertilizer/Object`.

### 3. Replace the mod base and config (`ModEntry.cs`, `ModConfig.cs`)
- **Entry point:** `ModEntry : Mod` with static `ModMonitor` and `Config`.
- **Config:** read it with `ReadConfig` in a try/catch that falls back to defaults.
- **GMCM:**
  - Number option for `GiantCropChance` (0 to 1.1, step 0.01) and a bool for `AllowGiantCropsOffFarm`.
  - On save, `Helper.GameContent.InvalidateCache` for maps (for example with a predicate on `xTile.Map`) so the off-farm setting applies without a restart.
- **Patching:** `harmony.PatchAll()` in `GameLaunched`.
- **Remove:** the migration manager and the multiplayer version assert. All players need the mod anyway, which SMAPI's own multiplayer mod sync makes visible.

### 4. Replace `CropTranspiler` with a prefix and a postfix on `Crop.TryGrowGiantCrop`
The method has a `Random random` parameter that the chance roll uses, so no transpiler is needed:

```csharp
[HarmonyPatch(typeof(Crop), nameof(Crop.TryGrowGiantCrop))]
static class TryGrowGiantCropPatch
{
    static void Prefix(Crop __instance, bool checkPreconditions, ref Random? random, out List<HoeDirt>? __state)
    {
        __state = null;
        // 1. Find every fertilized HoeDirt in the largest TileSize footprint of __instance.TryGetGiantCrops(...),
        //    starting at __instance.tilePosition (the calling crop is the top-left corner).
        // 2. If any, roll Config.GiantCropChance with Utility.CreateDaySaveRandom(x, y, salt) (deterministic like vanilla).
        // 3. On success: random = AlwaysPassRandom.Instance; __state = the fertilized dirts.
    }

    static void Postfix(bool __result, List<HoeDirt>? __state)
    {
        if (__result && __state is not null)
            foreach (var dirt in __state) dirt.fertilizer.Value = null; // consume
    }
}

sealed class AlwaysPassRandom : Random
{
    public static readonly AlwaysPassRandom Instance = new();
    protected override double Sample() => 0.0; // NextDouble() == 0 → NextBool(chance) is true for chance > 0
}
```

- **Footprint:** checking the whole footprint (not just one tile) keeps the old rule "apply to the middle of the 3×3", and also works for larger custom giant crops (6480's, JP's Ultra Giant Crops). The old `RemoveFertilizer` cleared every tile the giant crop replaced. Clear only the fertilized tiles found, or all tiles in the footprint (decide while testing).
- **Unchanged vanilla rules:** the location check, `Condition` queries and the "same crop in every tile" check stay. The patch only changes the chance.
- **When the roll succeeds:** every `GiantCropData` entry for that crop passes the chance step, so the first entry whose `Condition` and grid match wins, as in vanilla.
- **Chance above 1:** `GiantCropChance > 1` means "always". The prefix's own roll handles that, so the clamp to 1.1 can stay.
- **Multiplayer:** `Crop.newDay` runs for the host only, so there's no farmhand logic to sync.

### 5. Rewrite `HoeDirtDrawTranspiler` with `CodeMatcher`
- Target `AccessTools.Method(typeof(HoeDirt), nameof(HoeDirt.DrawOptimized))`.
- `MatchStartForward(new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(HoeDirt), nameof(HoeDirt.GetFertilizerSourceRect))))`.
- Then `MatchStartForward(new CodeMatch(OpCodes.Call, AccessTools.PropertyGetter(typeof(Color), nameof(Color.White))))`, `Advance(1)`.
- Insert `ldarg.0` and `call ReplaceColor(Color, HoeDirt)` (returns `Color.Purple` for our fertilizer).
- If a match fails, log a warning and return the original instructions. The tint is cosmetic, so a failure must not break drawing.
- Alternative with no transpiler: a postfix on `GetFertilizerSourceRect` returning a vanilla rect (for example Deluxe Speed-Gro's). That's simpler but not visually distinct; keep it as a fallback.

### 6. Legacy saves (optional, small)
1.5.6 saves stored the Json Assets numeric ID (`SavedObjectID` in save data / global data, `GiantCropFertilizerIDStorage`). After the 1.6 save migration those tiles and items are stale or Error Items.

On `SaveLoaded` (host only), if `Helper.Data.ReadSaveData<string>("SavedObjectID")` parses to an int N:
- replace `HoeDirt.fertilizer` values `N` / `(O)N` with the new qualified ID in every location (`Utility.ForEachLocation`);
- replace inventory and chest items named "Giant Crop Fertilizer" that are Error Items.

Skip this if testing shows the game already dropped them. Most players will have no fertilized tiles left.

### 7. Build and test on 1.6.15

| Case | Expected |
|---|---|
| Buy from Qi's gem shop | costs 5 Qi gems; check no gold price is added (compare `QiGemShop` entries with `patch export Data/Shops` and set `Price = 0` if needed) |
| Apply to the middle of a watered 3×3 of cauliflower / melon / pumpkin, sleep until mature | giant crop next growth night; fertilizer gone |
| Apply to a 3×3 that is already mature | giant crop the next night (1.6 rolls nightly) |
| Apply to the top-left tile, an edge tile, and outside the 3×3 | inside: works; outside: vanilla chance only |
| Powdermelon, Qi fruit, 6480's Giant Crops (custom crops) | works for all crops in `Data/GiantCrops` |
| `GiantCropChance` 0.5 over many tiles | about half; vanilla 1% still applies on a failed roll |
| `AllowGiantCropsOffFarm` on, garden pots / greenhouse / Ginger Island farm | allowed off farm; off by default |
| Soil tint | purple on the fertilized tile |
| Big Slime on Skull Cavern floor 120+ with dangerous mines | occasional drop (use `debug` spawn to test) |
| Ultimate Fertilizer installed | applying a second fertilizer doesn't erase ours, or document the conflict |
| Multiplayer host + farmhand, split-screen | giant crop and consumption visible to both |

Run `harmony_summary` to confirm one transpiler (draw) and the prefix/postfix applied.

### 8. Release
- **Credit and permission:** credit atravita (manifest `Author`, Nexus page, README). Permission is already granted (Nexus "Modify OK" + MIT + public note). Messaging them is a courtesy, as for Holiday Sales Continued.
- **Nexus page:** publish with the requirement "SMAPI only (no Json Assets, SpaceCore or AtraCore)". Link the original and mention Grow That Giant Crop / Configurable Giant Crops as the cheat-style alternatives.
- **Compatibility list:** PR on `Pathoschild/SmapiCompatibilityList` marking `atravita.GiantCropFertilizer` as `workaround: use Giant Crop Fertilizer Continued`.
- **Translations:** `i18n` already has the translations from the original. Re-add the keys the new GMCM text needs.

## Effort and risk

- **Effort:** small, about 1 evening. About 300 lines after cleanup; one cosmetic transpiler; the item, shop and map edits are already done in `AssetManager.cs`.
- **Ease 5, Risk 1** on the report's scale: Modify OK + MIT, author inactive, no competing item, no blocker, no save data except the optional legacy fix.
- **Risks:**
  - The footprint and top-left semantics differ from 1.5 (crop anchored at top-left now). Test edge placements.
  - Ultimate Fertilizer may store several fertilizers per tile differently. Check its `HoeDirt` patches.
  - The Qi gem shop entry's gold price.
