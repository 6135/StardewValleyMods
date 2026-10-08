using HarmonyLib;

using Microsoft.Xna.Framework;

using StardewValley.Extensions;
using StardewValley.GameData.GiantCrops;
using StardewValley.TerrainFeatures;

namespace GiantCropFertilizer.HarmonyPatches;

/// <summary>
/// Forces the giant crop chance roll when the crop's footprint holds the fertilizer, and consumes it once the giant crop grows.
/// </summary>
[HarmonyPatch(typeof(Crop), nameof(Crop.TryGrowGiantCrop))]
internal static class TryGrowGiantCropPatch
{
    private static void Prefix(Crop __instance, ref Random? random, out List<HoeDirt>? __state)
    {
        __state = null;
        try
        {
            GameLocation? location = __instance.currentLocation;
            if (location is null || ModEntry.Config.GiantCropChance <= 0 || !__instance.TryGetGiantCrops(out IReadOnlyList<KeyValuePair<string, GiantCropData>> giantCrops))
            {
                return;
            }

            // the calling crop is the top-left corner of the footprint.
            int width = 1;
            int height = 1;
            foreach ((_, GiantCropData data) in giantCrops)
            {
                width = Math.Max(width, data.TileSize.X);
                height = Math.Max(height, data.TileSize.Y);
            }

            Vector2 origin = __instance.tilePosition;
            List<HoeDirt> fertilized = new();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (location.terrainFeatures.TryGetValue(origin + new Vector2(x, y), out TerrainFeature? feature)
                        && feature is HoeDirt dirt
                        && FertilizerValues.HasGiantCropFertilizer(dirt.fertilizer.Value))
                    {
                        fertilized.Add(dirt);
                    }
                }
            }

            if (fertilized.Count == 0)
            {
                return;
            }

            Random roll = Utility.CreateDaySaveRandom(origin.X, origin.Y, 6135);
            if (ModEntry.Config.GiantCropChance > 1 || roll.NextBool(ModEntry.Config.GiantCropChance))
            {
                random = AlwaysPassRandom.Instance;
                __state = fertilized;
            }
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Failed checking giant crop fertilizer:\n\n{ex}", LogLevel.Error);
        }
    }

    private static void Postfix(bool __result, List<HoeDirt>? __state)
    {
        if (__result && __state is not null)
        {
            foreach (HoeDirt dirt in __state)
            {
                dirt.fertilizer.Value = FertilizerValues.RemoveGiantCropFertilizer(dirt.fertilizer.Value);
            }
        }
    }

    /// <summary>
    /// A random whose <see cref="Random.NextDouble"/> is always 0, so every <c>NextBool(chance)</c> with a positive chance passes.
    /// </summary>
    private sealed class AlwaysPassRandom : Random
    {
        internal static readonly AlwaysPassRandom Instance = new();

        protected override double Sample() => 0.0;

        public override double NextDouble() => 0.0;
    }
}
