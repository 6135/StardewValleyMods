using System.Diagnostics;

using StardewValley.Pathfinding;

namespace GingerIslandMainlandAdjustments.Framework;

/// <summary>
/// Small helpers that replace the parts of AtraCore/AtraShared this mod used.
/// Adapted from atravita's AtraShared (MIT).
/// </summary>
internal static class Extensions
{
    /// <summary>Logs only in DEBUG builds.</summary>
    [Conditional("DEBUG")]
    internal static void DebugOnlyLog(this IMonitor monitor, string message, LogLevel level = LogLevel.Debug)
        => monitor.Log(message, level);

    /// <summary>Logs at Debug level in DEBUG builds, Trace otherwise.</summary>
    internal static void DebugLog(this IMonitor monitor, string message)
#if DEBUG
        => monitor.Log(message, LogLevel.Debug);
#else
        => monitor.Log(message, LogLevel.Trace);
#endif

    /// <summary>Logs an error that happened while doing something.</summary>
    internal static void LogError(this IMonitor monitor, string action, Exception ex)
        => monitor.Log($"Mod failed while {action}, see log for details.\n\n{ex}", LogLevel.Error);

    /// <summary>Estimated travel time, in game minutes, for a schedule path.</summary>
    internal static int GetExpectedRouteTime(this SchedulePathDescription path)
        => path.route.Count * 32 / 42;

    /// <summary>Gets every villager in every location.</summary>
    internal static IEnumerable<NPC> GetVillagers()
    {
        List<NPC> villagers = [];
        Utility.ForEachVillager(npc =>
        {
            villagers.Add(npc);
            return true;
        });
        return villagers;
    }

    /// <summary>Gets a random seeded on the save, the day, and a string.</summary>
    internal static Random GetSeededRandom(int dayFactor, string initial)
        => Utility.CreateRandom(Game1.uniqueIDForThisGame, dayFactor * Game1.stats.DaysPlayed, Game1.hash.GetDeterministicHashCode(initial));

    /// <summary>Picks a random variant of a dialogue key (key, key_2, key_3...), or null if the NPC lacks the key.</summary>
    internal static string? GetRandomDialogue(this NPC npc, string? basekey, Random? random)
    {
        if (basekey is null || npc.Dialogue?.Count is null or 0 || !npc.Dialogue.ContainsKey(basekey))
        {
            return null;
        }
        random ??= Game1.random;
        int index = 1;
        while (npc.Dialogue.ContainsKey($"{basekey}_{++index}"))
        {
        }
        int selection = random.Next(1, index);
        return selection == 1 ? basekey : $"{basekey}_{selection}";
    }

    /// <summary>Gets a raw schedule entry from the NPC's master schedule.</summary>
    internal static bool TryGetScheduleEntry(this NPC npc, string? scheduleKey, [NotNullWhen(true)] out string? rawData)
    {
        rawData = null;
        return scheduleKey is not null && npc.getMasterScheduleRawData() is { } data && data.TryGetValue(scheduleKey, out rawData);
    }

    /// <summary>Clears the NPC's current dialogue and pushes the given key.</summary>
    internal static void ClearAndPushDialogue(this NPC npc, string dialogueKey)
    {
        if (!string.IsNullOrWhiteSpace(dialogueKey) && npc.Dialogue.TryGetValue(dialogueKey, out string? dialogue))
        {
            string endearment = npc.getTermOfSpousalEndearment();
            dialogue = dialogue.Replace(MarriageDialogueReference.ENDEARMENT_TOKEN_LOWER, endearment.ToLower(), StringComparison.Ordinal)
                               .Replace(MarriageDialogueReference.ENDEARMENT_TOKEN, endearment, StringComparison.Ordinal);
            npc.CurrentDialogue.Clear();
            npc.CurrentDialogue.Push(new Dialogue(npc, $"{npc.LoadedDialogueKey}:{dialogueKey}", dialogue) { removeOnNextMove = true });
        }
    }

    /// <summary>Pushes a marriage dialogue line if the NPC has it.</summary>
    internal static bool TryApplyMarriageDialogueIfExisting(this NPC npc, string dialogueKey, bool clearOnMovement = false)
    {
        if (npc.tryToGetMarriageSpecificDialogue(dialogueKey) is not Dialogue dialogue)
        {
            return false;
        }
        npc.CurrentDialogue.Clear();
        npc.currentMarriageDialogue.Clear();
        dialogue.removeOnNextMove = clearOnMovement;
        npc.CurrentDialogue.Push(dialogue);
        return true;
    }

    /// <summary>Invalidates an asset and its localized variant.</summary>
    internal static void InvalidateCacheAndLocalized(this IGameContentHelper helper, string assetName)
    {
        helper.InvalidateCache(assetName);
        if (helper.CurrentLocaleConstant != LocalizedContentManager.LanguageCode.en)
        {
            helper.InvalidateCache($"{assetName}.{helper.CurrentLocale}");
        }
    }

    /// <summary>Whether the player is in normal gameplay (no menu, event, etc.).</summary>
    internal static bool IsNormalGameplay()
        => Game1.keyboardDispatcher.Subscriber is null
            && Context.IsWorldReady && Context.CanPlayerMove && !Game1.player.isRidingHorse()
            && Game1.currentLocation is not null && !Game1.eventUp && !Game1.isFestival() && !Game1.IsFading()
            && Game1.activeClickableMenu is null;
}
