using System.Text;
using System.Text.RegularExpressions;

using GingerIslandMainlandAdjustments.Framework;

using Microsoft.Xna.Framework;

using StardewModdingAPI.Utilities;

using StardewValley.Network;
using StardewValley.Pathfinding;

namespace GingerIslandMainlandAdjustments.ScheduleManager;

/// <summary>
/// Schedule parsing helpers, vendored and trimmed from atravita's AtraShared.Schedules (MIT).
/// </summary>
internal static class ScheduleParsing
{
    // <time> [location] <tileX> <tileY> [facingDirection] [animation] \"[dialogue]\"
    private static readonly Regex ScheduleRegex = new(
        @"(?<arrival>a)?(?<time>[0-9]{1,4})(?<location> \S+)*?(?<x> [0-9]{1,4})(?<y> [0-9]{1,4})(?<direction> [0-9])?(?<animation> [^\s\""]+)?(?<dialogue> \"".*\"")?",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(250));

    // <time> bed
    private static readonly Regex BedRegex = new(
        @"(?<arrival>a)?(?<time>[0-9]{1,4}) bed",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(250));

    private static IMonitor Monitor => Globals.ModMonitor;

    private static ITranslationHelper Translation => Globals.Helper.Translation;

    /// <summary>
    /// Follows GOTO / NOT friendship / MAIL redirects in a raw schedule string.
    /// </summary>
    internal static bool TryFindGOTOschedule(NPC npc, SDate date, string rawData, out string scheduleString)
    {
        scheduleString = string.Empty;
        string[] splits = rawData.Split('/', 3, StringSplitOptions.TrimEntries);
        string[] command = splits[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (command.Length == 0)
        {
            return false;
        }
        switch (command[0])
        {
            case "GOTO" when command.Length > 1:
                if (command[1].Equals("NO_SCHEDULE", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                string newKey = command[1].Equals("Season", StringComparison.OrdinalIgnoreCase) ? date.SeasonKey.ToLowerInvariant() : command[1];
                if (npc.TryGetScheduleEntry(newKey, out string? newSchedule))
                {
                    if (newSchedule.Equals(rawData, StringComparison.Ordinal))
                    {
                        Monitor.Log(Translation.Get("GOTO_INFINITE_LOOP").Default("Infinite loop detected, skipping this schedule."), LogLevel.Warn);
                        return false;
                    }
                    return TryFindGOTOschedule(npc, date, newSchedule, out scheduleString);
                }
                Monitor.Log(
                    Translation.Get("GOTO_SCHEDULE_NOT_FOUND").Default("GOTO {{scheduleKey}} not found for NPC {{npc}}")
                        .Tokens(new { scheduleKey = newKey, npc = npc.Name }),
                    LogLevel.Warn);
                return false;
            case "NOT" when command.Length > 3 && command[1].Equals("friendship", StringComparison.Ordinal):
                if (Game1.getCharacterFromName(command[2]) is not NPC friend)
                {
                    Monitor.Log(
                        Translation.Get("GOTO_FRIEND_NOT_FOUND").Default("NPC {{npc}} not found, friend requirement {{requirment}} cannot be evaluated: {{scheduleKey}}")
                            .Tokens(new { npc = command[2], requirment = splits[0], scheduleKey = rawData }),
                        LogLevel.Warn);
                    return false;
                }
                if (!int.TryParse(command[3], out int heartLevel))
                {
                    Monitor.Log(
                        Translation.Get("GOTO_ILL_FORMED_FRIENDSHIP").Default("Ill-formed friendship requirment {{requirment}} for {{npc}}: {{scheduleKey}}")
                            .Tokens(new { requirment = splits[0], npc = npc.Name, scheduleKey = rawData }),
                        LogLevel.Warn);
                    return false;
                }
                if (Utility.GetAllPlayerFriendshipLevel(friend) / 250 > heartLevel)
                {
                    Monitor.Log(
                        Translation.Get("GOTO_SCHEDULE_FRIENDSHIP").Default("Skipping due to friendship limit for {{npc}}: {{scheduleKey}}")
                            .Tokens(new { npc = npc.Name, scheduleKey = rawData }),
                        LogLevel.Trace);
                    return false;
                }
                scheduleString = rawData;
                return true;
            case "MAIL" when command.Length > 1 && splits.Length == 3:
                return Game1.MasterPlayer.mailReceived.Contains(command[1]) || NetWorldState.checkAnywhereForWorldStateID(command[1])
                    ? TryFindGOTOschedule(npc, date, splits[2], out scheduleString)
                    : TryFindGOTOschedule(npc, date, splits[1], out scheduleString);
            default:
                scheduleString = rawData;
                return true;
        }
    }

    /// <summary>
    /// Parses a schedule string starting from a given point, without warping the NPC to their default position first.
    /// </summary>
    /// <returns>The parsed schedule, or null if nothing could be parsed.</returns>
    internal static Dictionary<int, SchedulePathDescription>? ParseSchedule(
        string scheduleKey,
        string? schedule,
        NPC npc,
        string? prevMap = null,
        Point? prevStop = null,
        int prevtime = 610,
        bool enforceStrictTiming = false)
    {
        if (schedule is null)
        {
            return null;
        }

        string previousMap = prevMap ?? npc.DefaultMap;
        Point lastStop = prevStop ?? (npc.DefaultPosition / 64f).ToPoint();
        int lastx = lastStop.X;
        int lasty = lastStop.Y;
        int lasttime = prevtime;

        Dictionary<int, SchedulePathDescription> remainderSchedule = [];
        (string map, Vector2 tile)? warpPoint = null;

        foreach (string schedulepoint in schedule.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                Match match = ScheduleRegex.Match(schedulepoint);

                if (!match.Success && BedRegex.Match(schedulepoint) is { Success: true } bedmatch)
                {
                    // <time> bed: replace "bed" with the NPC's actual bed location.
                    string bedtime = bedmatch.Groups["arrival"].Success ? "a" + bedmatch.Groups["time"].Value : bedmatch.Groups["time"].Value;
                    if (npc.isMarried() || npc.DefaultMap.Equals("FarmHouse", StringComparison.OrdinalIgnoreCase))
                    {
                        match = ScheduleRegex.Match(bedtime + " BusStop -1 23 3");
                    }
                    else if (GetBedPoint(npc, "default") is string defaultbed)
                    {
                        match = ScheduleRegex.Match($"{bedtime} {defaultbed}");
                    }
                    else if (GetBedPoint(npc, "spring") is string springbed)
                    {
                        match = ScheduleRegex.Match($"{bedtime} {springbed}");
                    }
                }

                if (!match.Success)
                {
                    // Still unparsable: send the NPC straight home to bed.
                    Monitor.Log(
                        Translation.Get("SCHEDULE_REGEX_FAILURE").Default("{{schedulepoint}} seems unparsable by regex, sending NPC {{npc}} home to sleep")
                            .Tokens(new { schedulepoint, npc = npc.Name }),
                        LogLevel.Info);
                    return SendHomeToBed(scheduleKey, schedulepoint, npc, previousMap, lastx, lasty, lasttime, remainderSchedule, enforceStrictTiming);
                }

                int time = int.Parse(match.Groups["time"].Value);
                string location = match.Groups["location"].Success ? match.Groups["location"].Value.Trim() : previousMap;
                int x = int.Parse(match.Groups["x"].Value.Trim());
                int y = int.Parse(match.Groups["y"].Value.Trim());
                if (!match.Groups["direction"].Success || !int.TryParse(match.Groups["direction"].Value.Trim(), out int direction))
                {
                    direction = Game1.down;
                }

                // Adjust schedules for locations not being open.
                if (!Game1.isLocationAccessible(location))
                {
                    if (npc.TryGetScheduleEntry(location + "_Replacement", out string? replacement))
                    {
                        string[] replacementdata = replacement.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (replacementdata.Length < 2 || !int.TryParse(replacementdata[0], out x) || !int.TryParse(replacementdata[1], out y))
                        {
                            Monitor.Log($"Failed in parsing replacement {replacement}", LogLevel.Warn);
                            continue;
                        }
                        if (replacementdata.Length < 3 || !int.TryParse(replacementdata[2], out direction))
                        {
                            direction = Game1.down;
                        }
                    }
                    else
                    {
                        if (enforceStrictTiming)
                        {
                            Monitor.Log(
                                Translation.Get("NO_REPLACEMENT_LOCATION").Default("Location replacement for {{location}} requested but not found for {{npc}}")
                                    .Tokens(new { location, npc = npc.Name }),
                                LogLevel.Warn);
                        }
                        continue;
                    }
                }

                if (time == 0)
                {
                    // zero points only set the starting warp.
                    warpPoint = (location, new Vector2(x, y));
                    continue;
                }
                if (time <= lasttime)
                {
                    LogTooTight(time, schedule, npc);
                    continue;
                }

                string? animation = match.Groups["animation"].Success ? match.Groups["animation"].Value.Trim() : null;
                string? message = match.Groups["dialogue"].Success ? match.Groups["dialogue"].Value.Trim().Trim('"') : null;

                SchedulePathDescription newpath = npc.pathfindToNextScheduleLocation(scheduleKey, previousMap, lastx, lasty, location, x, y, direction, animation, message);

                if (match.Groups["arrival"].Success)
                {
                    time = Utility.ModifyTime(time, -newpath.GetExpectedRouteTime());
                }
                if (time <= lasttime)
                {
                    LogTooTight(time, schedule, npc);
                    continue;
                }

                remainderSchedule.Add(time, newpath);
                previousMap = location;
                lasttime = time;
                lastx = x;
                lasty = y;
                if (enforceStrictTiming)
                {
                    lasttime = Utility.ModifyTime(lasttime, newpath.GetExpectedRouteTime());
                }
            }
            catch (RegexMatchTimeoutException ex)
            {
                Monitor.Log(
                    Translation.Get("REGEX_TIMEOUT_ERROR").Default("Regex for schedule entry {{schedulePoint}} timed out:\n\n{{ex}}")
                        .Tokens(new { schedulePoint = schedulepoint, ex }),
                    LogLevel.Warn);
            }
        }

        if (remainderSchedule.Count == 0)
        {
            return null;
        }
        if (warpPoint is { } warp)
        {
            Game1.warpCharacter(npc, warp.map, warp.tile);
        }
        return remainderSchedule;
    }

    /// <summary>Appends a schedule point in raw schedule format.</summary>
    internal static StringBuilder AppendSchedulePoint(
        this StringBuilder sb,
        NPC npc,
        string map,
        int time,
        Point point,
        bool isArrivalTime,
        int direction,
        string? animation,
        string? dialogueKey)
    {
        if (isArrivalTime)
        {
            sb.Append('a');
        }
        sb.Append(time).Append(' ').Append(map).Append(' ').Append(point.X).Append(' ').Append(point.Y).Append(' ').Append(direction);
        if (animation is not null)
        {
            sb.Append(' ').Append(animation);
        }
        if (dialogueKey is not null)
        {
            sb.Append(" \"Characters\\Dialogue\\").Append(npc.Name).Append(':').Append(dialogueKey).Append('"');
        }
        return sb;
    }

    private static Dictionary<int, SchedulePathDescription>? SendHomeToBed(
        string scheduleKey,
        string schedulepoint,
        NPC npc,
        string previousMap,
        int lastx,
        int lasty,
        int lasttime,
        Dictionary<int, SchedulePathDescription> remainderSchedule,
        bool enforceStrictTiming)
    {
        string sleepanimation = npc.Name.ToLowerInvariant() + "_sleep";
        SchedulePathDescription path2bed = npc.pathfindToNextScheduleLocation(
            scheduleKey,
            previousMap,
            lastx,
            lasty,
            npc.DefaultMap,
            (int)npc.DefaultPosition.X / 64,
            (int)npc.DefaultPosition.Y / 64,
            Game1.up,
            DataLoader.AnimationDescriptions(Game1.content).ContainsKey(sleepanimation) ? sleepanimation : null,
            null);

        string originaltime = schedulepoint.Split(' ', 2)[0];
        bool isArrival = originaltime.StartsWith('a');
        if (!int.TryParse(isArrival ? originaltime[1..] : originaltime, out int path2bedtime))
        {
            Monitor.Log(
                Translation.Get("SCHEDULE_PARSE_FAILURE").Default("Failed in parsing schedulepoint {{schedulepoint}} for NPC {{npc}}")
                    .Tokens(new { schedulepoint, npc = npc.Name }),
                LogLevel.Warn);
            return null; // GIMA tries the next schedule.
        }
        if (isArrival)
        {
            path2bedtime = Utility.ModifyTime(path2bedtime, -path2bed.GetExpectedRouteTime());
        }
        if (path2bedtime <= lasttime)
        {
            // force the bed time to be after the previous point.
            int afterLast = Utility.ConvertTimeToMinutes(lasttime);
            if (enforceStrictTiming && remainderSchedule.TryGetValue(lasttime, out SchedulePathDescription? lastPoint))
            {
                afterLast += lastPoint.GetExpectedRouteTime();
            }
            path2bedtime = Utility.ConvertMinutesToTime((afterLast / 10 * 10) + 10);
        }
        remainderSchedule[path2bedtime] = path2bed;
        return remainderSchedule;
    }

    /// <summary>Gets the location part ("Map x y dir") of the last point of a schedule entry.</summary>
    private static string? GetBedPoint(NPC npc, string scheduleKey)
    {
        if (!npc.TryGetScheduleEntry(scheduleKey, out string? raw))
        {
            return null;
        }
        string lastPoint = raw.TrimEnd('/');
        lastPoint = lastPoint[(lastPoint.LastIndexOf('/') + 1)..];
        int space = lastPoint.IndexOf(' ');
        return space > 0 && space < lastPoint.Length - 1 ? lastPoint[(space + 1)..] : null;
    }

    private static void LogTooTight(int time, string schedule, NPC npc)
        => Monitor.Log(
            Translation.Get("TOO_TIGHT_TIMELINE").Default("{{time}} position in schedule {{scheduleKey}} for {{npc}} is too tight. Will be skipped.")
                .Tokens(new { time, scheduleKey = schedule, npc = npc.Name }),
            LogLevel.Warn);
}
