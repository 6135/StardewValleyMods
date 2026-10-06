using System.Reflection;

using HarmonyLib;

using StardewValley.Locations;

namespace HolidaySales.HarmonyPatches;

/// <summary>
/// Makes the "is a festival happening" check in these methods only consider festivals happening "in town".
/// </summary>
[HarmonyPatch]
internal static class PatchTownMethods
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Forest), "resetSharedState");
        yield return AccessTools.Method(typeof(IslandSouth), nameof(IslandSouth.SetupIslandSchedules));
        yield return AccessTools.Method(typeof(NPC), nameof(NPC.tryToReceiveActiveObject));
        yield return AccessTools.Method(typeof(Farmer), nameof(Farmer.showToolUpgradeAvailability));

        if (AccessTools.TypeByName("GingerIslandMainlandAdjustments.ScheduleManager.GIScheduler") is Type gima
            && AccessTools.Method(gima, "GenerateAllSchedules") is MethodInfo gimaMethod)
        {
            yield return gimaMethod;
        }
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        => HSUtils.AdjustIsFestivalCallForTown(instructions, original);
}
