

using GingerIslandMainlandAdjustments.AssetManagers;
using GingerIslandMainlandAdjustments.Configuration;

using HarmonyLib;

using Microsoft.Xna.Framework.Graphics;

using StardewValley.Locations;

namespace GingerIslandMainlandAdjustments.ScheduleManager;

/// <summary>
/// Patches for the IslandSouth class.
/// </summary>
[HarmonyPatch(typeof(IslandSouth))]
internal static class IslandSouthPatches
{
    /// <summary>
    /// Dictionary of NPCs and custom exclusions.
    /// </summary>
    /// <remarks>null is cache miss: reload if ever null.</remarks>
    private static Dictionary<NPC, string[]>? exclusions = null;

    /// <summary>
    /// Gets dictionary of NPCs and custom exclusions.
    /// </summary>
    /// <remarks>Cached, will reload automatically if not currently cached.</remarks>
    internal static Dictionary<NPC, string[]> Exclusions
        => exclusions ??= AssetLoader.GetExclusions();

    /// <summary>
    /// Clears/resets the Exclusions cache.
    /// </summary>
    internal static void ClearCache() => exclusions = null;

    /// <summary>
    /// Override the vanilla schedules if told to.
    /// </summary>
    /// <returns>False to skip vanilla function, true otherwise.</returns>
    /// <remarks>Setting my harmony priority low to try to be run **after** Custom NPC Exclusions.</remarks>
    [HarmonyPrefix]
    [HarmonyPriority(Priority.LowerThanNormal)]
    [HarmonyPatch(nameof(IslandSouth.SetupIslandSchedules))]
    private static bool OverRideSetUpIslandSchedules()
    {
        if (Globals.Config.UseThisScheduler)
        {
            try
            {
                GIScheduler.GenerateAllSchedules();
                return false;
            }
            catch (Exception ex)
            {
                Globals.ModMonitor.LogError("generating island schedules", ex);
            }
        }
        return true;
    }

    /// <summary>
    /// Extends CanVisitIslandToday for custom exclusions as well.
    /// </summary>
    /// <param name="npc">the NPC to check.</param>
    /// <param name="__result">True if the NPC can go to the island, false otherwise.</param>
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(nameof(IslandSouth.CanVisitIslandToday))]
    private static void ExtendCanGoToIsland(NPC npc, ref bool __result)
    {
        try
        {
            if (!__result)
            {
                if (!IsVanillaExclusionOverridden(npc))
                {
                    // already false in code, ignore me for everyone else
                    return;
                }
                __result = true;
            }

            if (Globals.Config.RequireResortDialogue && !npc.Dialogue.ContainsKey("Resort"))
            {
                Globals.ModMonitor.Log($"{npc.Name} appears to lack resort dialogue, removing from pool.", LogLevel.Info);
                __result = false;
                return;
            }

            // if an NPC has a schedule for the specific day, don't allow them to go to the resort.
            if (npc.HasSpecificSchedule() && IsBlockedBySpecificSchedule(npc))
            {
                __result = false;
                return;
            }

            if (IsExcludedToday(npc))
            {
                __result = false;
            }
        }
        catch (Exception ex)
        {
            Globals.ModMonitor.LogError("adjusting CanVisitIslandToday", ex);
        }
        return;
    }

    /// <summary>
    /// Checks whether a vanilla-excluded NPC (Sandy, George, Evelyn, Willy, Wizard) is allowed by config.
    /// </summary>
    /// <param name="npc">the NPC to check.</param>
    /// <returns>True if the NPC should be allowed to the island anyways.</returns>
    private static bool IsVanillaExclusionOverridden(NPC npc)
    {
        Farmer? spouse = npc.getSpouse();
        if (IsOverrideAllowed(Globals.Config.AllowSandy, spouse)
            && Globals.Config.UseThisScheduler
            && npc.Name.Equals("Sandy", StringComparison.OrdinalIgnoreCase)
            && Game1.dayOfMonth != 15
            && !Game1.IsFall)
        {
            return true; // let Sandy come to the resort!
        }
        if (Globals.Config.AllowGeorgeAndEvelyn
            && Globals.Config.UseThisScheduler
            && (npc.Name.Equals("George", StringComparison.OrdinalIgnoreCase) || npc.Name.Equals("Evelyn", StringComparison.OrdinalIgnoreCase)))
        {
            return true; // let George & Evelyn come too!
        }
        if (Globals.Config.UseThisScheduler
            && IsOverrideAllowed(Globals.Config.AllowWilly, spouse)
            && npc.Name.Equals("Willy", StringComparison.OrdinalIgnoreCase))
        {
            return true; // Allow Willy access to resort as well.
        }
        return Globals.Config.UseThisScheduler
            && IsOverrideAllowed(Globals.Config.AllowWizard, spouse)
            && npc.Name.Equals("Wizard", StringComparison.OrdinalIgnoreCase); // Allow the Wizard access to the result.
    }

    /// <summary>
    /// Checks whether a <see cref="VillagerExclusionOverride"/> setting allows the NPC.
    /// </summary>
    /// <param name="setting">The config setting.</param>
    /// <param name="spouse">The NPC's spouse, if any.</param>
    /// <returns>True if allowed.</returns>
    private static bool IsOverrideAllowed(VillagerExclusionOverride setting, Farmer? spouse)
        => setting == VillagerExclusionOverride.Yes
            || (setting == VillagerExclusionOverride.IfMarried && spouse is not null);

    /// <summary>
    /// Checks whether an NPC with a day-specific schedule should be kept off the island, per schedule strictness.
    /// </summary>
    /// <param name="npc">the NPC to check.</param>
    /// <returns>True if the NPC should not go to the island.</returns>
    private static bool IsBlockedBySpecificSchedule(NPC npc)
    {
        switch (Globals.Config.ScheduleStrictness.TryGetValue(npc.Name, out ScheduleStrictness strictness) ? strictness : ScheduleStrictness.Default)
        {
            case ScheduleStrictness.Default:
                return !Exclusions.TryGetValue(npc, out string[]? npcExclusions)
                    || !npcExclusions.Any(static (a) => a.Equals("AllowOnSpecialDays", StringComparison.OrdinalIgnoreCase));
            case ScheduleStrictness.Strict:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Checks the NPC's custom exclusions against today's date.
    /// </summary>
    /// <param name="npc">the NPC to check.</param>
    /// <returns>True if one of the NPC's exclusions applies today.</returns>
    private static bool IsExcludedToday(NPC npc)
    {
        if (!Exclusions.TryGetValue(npc, out string[]? checkset))
        { // I don't have an entry for you.
            return false;
        }
        foreach (string condition in checkset)
        {
            if ((int.TryParse(condition, out int day) && day == Game1.dayOfMonth)
                || Game1.currentSeason.Equals(condition, StringComparison.OrdinalIgnoreCase)
                || Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth).Equals(condition, StringComparison.OrdinalIgnoreCase)
                || $"{Game1.currentSeason}_{Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth)}".Equals(condition, StringComparison.OrdinalIgnoreCase)
                || $"{Game1.currentSeason}_{Game1.dayOfMonth}".Equals(condition, StringComparison.OrdinalIgnoreCase)
                || (!Globals.Config.UseThisScheduler && "neveralone".Equals(condition, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Prefixes HasIslandAttire to allow the player choice in whether the NPCs should wear their island attire.
    /// </summary>
    /// <param name="character">NPC in question.</param>
    /// <param name="__result">Result returned to original function.</param>
    /// <returns>True to continue to the vanilla function, false otherwise.</returns>
    [HarmonyPrefix]
    [HarmonyPriority(Priority.VeryLow)]
    [HarmonyPatch(nameof(IslandSouth.HasIslandAttire))]
    private static bool PrefixHasIslandAttire(NPC character, ref bool __result)
    {
        try
        {
            switch (Globals.Config.WearIslandClothing)
            {
                case WearIslandClothing.Default:
                    return true;
                case WearIslandClothing.All:
                    if (character.Name.Equals("Lewis", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            Game1.temporaryContent.Load<Texture2D>($"Characters\\{NPC.getTextureNameForCharacter(character.Name)}_Beach");
                            __result = true;
                            return false;
                        }
                        catch (Exception)
                        {
                            // Texture missing: fall through to the default clothing handling.
                        }
                    }
                    return true;
                case WearIslandClothing.None:
                    __result = false;
                    return false;
                default:
                    return true;
            }
        }
        catch (Exception ex)
        {
            Globals.ModMonitor.LogError($"adjusting HasIslandAttire for {character.Name}", ex);
        }
        return true;
    }
}
