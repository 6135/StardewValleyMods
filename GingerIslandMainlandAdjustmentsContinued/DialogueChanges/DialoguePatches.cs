namespace GingerIslandMainlandAdjustments.DialogueChanges;

using GingerIslandMainlandAdjustments.ScheduleManager;

using HarmonyLib;

using StardewModdingAPI.Utilities;

using StardewValley.Locations;

/// <summary>
/// Class to handle patching of NPCs for dialogue.
/// </summary>
[HarmonyPatch(typeof(NPC))]
internal static class DialoguePatches
{
    private const string ANTISOCIAL = "Resort_Antisocial";
    private const string ISLANDNORTH = "Resort_IslandNorth";
    private const string TOADVENTURE = "Resort_Adventure";
    private const string FROMADVENTURE = "Resort_AdventureReturn";

    private static readonly PerScreen<HashSet<string>> TalkedToTodayPerScreen = new(createNewState: () => []);

    private static HashSet<string> TalkedToToday => TalkedToTodayPerScreen.Value;

    /// <summary>
    /// Clears the record of whether or not you've talked to your spouse on the Island today.
    /// </summary>
    internal static void ClearTalkRecord() => TalkedToToday.Clear();

    /// <summary>
    /// Appends checkForNewCurrentDialogue to look for GI-specific dialogue.
    /// </summary>
    /// <param name="__instance">NPC instance.</param>
    /// <param name="__0">Heart level.</param>
    /// <param name="__1">Whether or not to have a season prefix.</param>
    /// <param name="__result">Whether or not new dialogue has been found.</param>
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NPC.checkForNewCurrentDialogue))]
    private static void DoCheckIslandDialogue(NPC __instance, int __0, bool __1, ref bool __result)
    { // __0 = heartlevel, as int. __1 = whether or not to have a season prefix?
        try
        {
            if (__instance.currentLocation is IslandLocation)
            {
                TalkedToToday.Add(__instance.Name);
            }
            if (__result || !Game1.IsVisitingIslandToday(__instance.Name) || __instance.currentLocation is FarmHouse)
            {
                return;
            }
            if (__instance.currentLocation is IslandLocation)
            {
                PushIslandLocationDialogue(__instance);
                return;
            }

            if (GetMainlandBaseKey(__instance, __1) is not string baseKey)
            {
                return;
            }
            __result = TryGetMainlandDialogue(__instance, baseKey, __0);
            return;
        }
        catch (Exception ex)
        {
            Globals.ModMonitor.LogError($"checking for island dialogue for NPC {__instance.Name}", ex);
        }
    }

    /// <summary>
    /// Pushes location-specific dialogue for an NPC currently on the island, if any exists.
    /// </summary>
    /// <param name="npc">NPC instance.</param>
    private static void PushIslandLocationDialogue(NPC npc)
    {
        if (GIScheduler.CurrentAdventurers?.Contains(npc) == true)
        {
            if (Game1.timeOfDay < 1200 && npc.Dialogue.ContainsKey(TOADVENTURE))
            {
                npc.ClearAndPushDialogue(TOADVENTURE);
                return;
            }
            else if (Game1.timeOfDay > 1700 && npc.Dialogue.ContainsKey(FROMADVENTURE))
            {
                npc.ClearAndPushDialogue(FROMADVENTURE);
                return;
            }
        }
        if (npc.currentLocation is IslandEast && npc.Dialogue.ContainsKey(ANTISOCIAL))
        {
            npc.ClearAndPushDialogue(ANTISOCIAL);
        }
        else if (npc.currentLocation is IslandNorth && npc.Dialogue.ContainsKey(ISLANDNORTH))
        {
            npc.ClearAndPushDialogue(ISLANDNORTH);
        }
    }

    /// <summary>
    /// Gets the base dialogue key for an island visitor on the mainland, based on time of day.
    /// </summary>
    /// <param name="npc">NPC instance.</param>
    /// <param name="noSeasonPrefix">Whether or not to skip the season prefix.</param>
    /// <returns>The base key, or null if no dialogue applies at this time.</returns>
    private static string? GetMainlandBaseKey(NPC npc, bool noSeasonPrefix)
    {
        string preface = noSeasonPrefix ? string.Empty : Game1.currentSeason;

        if (Game1.timeOfDay <= 1200)
        {
            return preface + "Resort_Approach";
        }
        if (Game1.timeOfDay >= 1800)
        {
            string baseKey = preface + "Resort_Left";
            if (!npc.currentLocation.IsOutdoors && npc.currentLocation is not FishShop)
            {
                baseKey = $"{baseKey}_{npc.currentLocation.Name}"; // use specific INDOOR keys.
            }
            return baseKey;
        }
        return null;
    }

    /// <summary>
    /// Tries the group, marriage, and plain variants of a mainland dialogue key, in that order.
    /// </summary>
    /// <param name="npc">NPC instance.</param>
    /// <param name="baseKey">Base dialogue key.</param>
    /// <param name="heartLevel">Heart level.</param>
    /// <returns>Whether or not dialogue was found.</returns>
    private static bool TryGetMainlandDialogue(NPC npc, string baseKey, int heartLevel)
    {
        // Handle group-specific dialogue.
        if (GIScheduler.CurrentGroup is not null
            && GIScheduler.CurrentVisitingGroup?.Contains(npc) == true
            && DialogueUtilities.TryGetIslandDialogue(npc, $"{baseKey}_{GIScheduler.CurrentGroup}", heartLevel))
        {
            return true;
        }

        if (npc.getSpouse() is Farmer spouse && spouse == Game1.player
            && DialogueUtilities.TryGetIslandDialogue(npc, baseKey + "_marriage", heartLevel))
        {
            return true;
        }
        return DialogueUtilities.TryGetIslandDialogue(npc, baseKey, heartLevel);
    }

    /// <summary>
    /// Appends spouse arrival back at farmhouse to replace with GI-specific dialogue.
    /// </summary>
    /// <param name="__instance">NPC instance.</param>
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NPC.arriveAtFarmHouse))]
    private static void AppendArrival(NPC __instance)
    {
        try
        {
            if (!Game1.IsVisitingIslandToday(__instance.Name))
            {
                return;
            }
            if (TalkedToToday.Contains(__instance.Name) && __instance.TryApplyMarriageDialogueIfExisting("GIReturn_Talked_" + __instance.Name, clearOnMovement: true))
            {
                Globals.ModMonitor.DebugOnlyLog($"Setting GIReturn_Talked_{__instance.Name}.", LogLevel.Debug);
            }
            else if (__instance.TryApplyMarriageDialogueIfExisting("GIReturn_" + __instance.Name, clearOnMovement: true))
            {
                Globals.ModMonitor.DebugOnlyLog($"Setting GIReturn_{__instance.Name}.", LogLevel.Debug);
            }
            else
            {
                __instance.CurrentDialogue.Clear();
                __instance.currentMarriageDialogue.Clear();
                Dialogue dialogue = Game1.player.getFriendshipHeartLevelForNPC(__instance.Name) > 9
                    ? new Dialogue(__instance, null, I18n.GIReturnDefaultHappy(__instance.getTermOfSpousalEndearment()))
                    : new Dialogue(__instance, null, I18n.GIReturnDefaultUnhappy());
                __instance.CurrentDialogue.Push(dialogue);
            }
        }
        catch (Exception ex)
        {
            Globals.ModMonitor.LogError($"setting GIReturn dialogue for {__instance.Name}", ex);
        }
    }
}