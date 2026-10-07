using HarmonyLib;

using Microsoft.Xna.Framework;

using StardewValley.Extensions;
using StardewValley.Monsters;

using SObject = StardewValley.Object;

namespace GiantCropFertilizer.HarmonyPatches;

/// <summary>
/// Gives Big Slimes deep in the dangerous Skull Cavern a chance to hold the fertilizer.
/// </summary>
[HarmonyPatch(typeof(BigSlime), MethodType.Constructor, typeof(Vector2), typeof(int))]
internal static class PostfixBigSlimeConstructor
{
    private static void Postfix(BigSlime __instance, int mineArea)
    {
        try
        {
            if (__instance.heldItem is not null
                && __instance.heldItem.Value is null
                && mineArea >= 120
                && Game1.mine?.GetAdditionalDifficulty() > 0
                && Game1.random.NextBool(0.05))
            {
                __instance.heldItem.Value = new SObject(ModEntry.GiantCropFertilizerID, 1);
            }
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Failed adding fertilizer to big slime:\n\n{ex}", LogLevel.Error);
        }
    }
}
