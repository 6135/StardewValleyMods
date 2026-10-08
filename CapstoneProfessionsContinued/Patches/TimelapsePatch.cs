using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

namespace CapstoneProfessions.Patches;

/// <summary>Makes the day longer for each player with the Timelapse profession.</summary>
internal static class TimelapsePatch
{
    private static readonly FieldInfo IntervalField = AccessTools.Field(typeof(Game1), nameof(Game1.realMilliSecondsPerGameTenMinutes));

    internal static void Apply(Harmony harmony)
    {
        MethodInfo method = AccessTools.Method(typeof(Game1), nameof(Game1.UpdateGameClock))
            ?? throw new InvalidOperationException("Could not find Game1.UpdateGameClock.");
        harmony.Patch(method, transpiler: new HarmonyMethod(typeof(TimelapsePatch), nameof(Transpiler)));
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher matcher = new(instructions);
        MethodInfo replacement = AccessTools.Method(typeof(TimelapsePatch), nameof(GetTenMinuteInterval));
        int count = 0;
        while (matcher.MatchStartForward(new CodeMatch(OpCodes.Ldsfld, IntervalField)).IsValid)
        {
            matcher.SetInstruction(new CodeInstruction(OpCodes.Call, replacement));
            matcher.Advance(1);
            count++;
        }

        if (count < 1)
        {
            ModEntry.ModMonitor.Log("Timelapse: could not find Game1.realMilliSecondsPerGameTenMinutes in UpdateGameClock; days will not be lengthened.", LogLevel.Warn);
        }

        return matcher.InstructionEnumeration();
    }

    private static int GetTenMinuteInterval()
    {
        float mult = 1;
        foreach (Farmer player in Game1.getAllFarmers())
        {
            if (player.professions.Contains(ModEntry.ProfessionTime))
            {
                mult += 0.2f;
            }
        }

        return (int)(Game1.realMilliSecondsPerGameTenMinutes * mult);
    }
}
