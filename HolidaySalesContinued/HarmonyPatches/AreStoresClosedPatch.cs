using HarmonyLib;

namespace HolidaySales.HarmonyPatches;

/// <summary>
/// Adjusts whether stores should be closed for festivals. This also covers locked doors and the phone, which call the same method.
/// </summary>
[HarmonyPatch(typeof(GameLocation), nameof(GameLocation.AreStoresClosedForFestival))]
internal static class AreStoresClosedPatch
{
    private static bool Prefix(ref bool __result)
    {
        try
        {
            __result = HSUtils.StoresClosedForFestival(Game1.currentLocation?.Name ?? "Town");
            return false;
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Failed checking whether stores are closed: {ex}", LogLevel.Error);
            return true;
        }
    }
}
