using System.Reflection.Emit;

using HarmonyLib;

using Microsoft.Xna.Framework;

using StardewValley.TerrainFeatures;

namespace GiantCropFertilizer.HarmonyPatches;

/// <summary>
/// Tints the fertilizer purple on the soil.
/// </summary>
[HarmonyPatch(typeof(HoeDirt), nameof(HoeDirt.DrawOptimized))]
internal static class HoeDirtDrawTranspiler
{
    private static Color ReplaceColor(Color prev, HoeDirt dirt)
        => ModEntry.IsGiantCropFertilizer(dirt.fertilizer.Value) ? Color.Purple : prev;

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> original = instructions.ToList();
        CodeMatcher matcher = new CodeMatcher(original)
            .MatchStartForward(new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(HoeDirt), nameof(HoeDirt.GetFertilizerSourceRect))))
            .MatchStartForward(new CodeMatch(OpCodes.Call, AccessTools.PropertyGetter(typeof(Color), nameof(Color.White))));

        if (matcher.IsInvalid)
        {
            ModEntry.ModMonitor.Log("Couldn't find the fertilizer draw call; the fertilizer won't be tinted.", LogLevel.Warn);
            return original;
        }

        return matcher
            .Advance(1)
            .Insert(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(HoeDirtDrawTranspiler), nameof(ReplaceColor))))
            .InstructionEnumeration();
    }
}
