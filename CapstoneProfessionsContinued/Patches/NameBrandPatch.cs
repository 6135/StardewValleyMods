using System.Reflection;

using HarmonyLib;

using SObject = StardewValley.Object;

namespace CapstoneProfessions.Patches;

/// <summary>Raises shipping prices for each player with the Name Brand profession.</summary>
internal static class NameBrandPatch
{
    internal static void Apply(Harmony harmony)
    {
        MethodInfo method = AccessTools.Method(typeof(SObject), "getPriceAfterMultipliers")
            ?? throw new InvalidOperationException("Could not find Object.getPriceAfterMultipliers.");
        harmony.Patch(method, postfix: new HarmonyMethod(typeof(NameBrandPatch), nameof(Postfix)));
    }

    private static void Postfix(ref float __result)
    {
        float mult = 1;
        foreach (Farmer player in Game1.getAllFarmers())
        {
            if (player.professions.Contains(ModEntry.ProfessionProfit))
            {
                mult += 0.05f;
            }
        }

        __result *= mult;
    }
}
