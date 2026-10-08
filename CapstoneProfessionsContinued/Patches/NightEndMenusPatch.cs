using System.Reflection;

using HarmonyLib;

using CapstoneProfessions.Framework;

using StardewValley.Menus;

namespace CapstoneProfessions.Patches;

/// <summary>Queues the capstone profession menu at the end of the night once all skills are maxed.</summary>
internal static class NightEndMenusPatch
{
    internal static void Apply(Harmony harmony)
    {
        MethodInfo method = AccessTools.Method(typeof(Game1), nameof(Game1.showEndOfNightStuff))
            ?? throw new InvalidOperationException("Could not find Game1.showEndOfNightStuff.");
        harmony.Patch(method, prefix: new HarmonyMethod(typeof(NightEndMenusPatch), nameof(Prefix)));
    }

    private static void Prefix()
    {
        Farmer p = Game1.player;
        if (!ModEntry.HasMaxedSkills() || p.professions.Contains(ModEntry.ProfessionTime) || p.professions.Contains(ModEntry.ProfessionProfit))
        {
            return;
        }

        ModEntry.ModMonitor.Log("Doing profession menu", LogLevel.Debug);

        if (Game1.endOfNightMenus.Count == 0)
        {
            Game1.endOfNightMenus.Push(new SaveGameMenu());
        }

        Game1.endOfNightMenus.Push(new CapstoneProfessionMenu());
    }
}
