using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using Microsoft.Xna.Framework.Content;

namespace HolidaySales;

/// <summary>
/// Utilities for this mod.
/// </summary>
internal static class HSUtils
{
    private static readonly MethodInfo IsFestivalDay = AccessTools.Method(typeof(Utility), nameof(Utility.isFestivalDay), Type.EmptyTypes)
        ?? throw new MissingMethodException(nameof(Utility), nameof(Utility.isFestivalDay));

    private static readonly MethodInfo IsFestivalDayAdjusted = AccessTools.Method(typeof(HSUtils), nameof(IsFestivalDayAdjustedForConfig))
        ?? throw new MissingMethodException(nameof(HSUtils), nameof(IsFestivalDayAdjustedForConfig));

    /// <summary>
    /// Replaces every call to <c>Utility.isFestivalDay()</c> with <c>IsFestivalDayAdjustedForConfig("Town")</c>.
    /// </summary>
    /// <param name="instructions">The original instructions.</param>
    /// <param name="original">The method being patched, for logging.</param>
    /// <returns>The adjusted instructions.</returns>
    internal static IEnumerable<CodeInstruction> AdjustIsFestivalCallForTown(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        int count = 0;
        CodeMatcher matcher = new CodeMatcher(instructions)
            .MatchStartForward(new CodeMatch(OpCodes.Call, IsFestivalDay))
            .Repeat(m =>
            {
                // the ldstr takes over the call's labels so jumps to the call still push the argument.
                List<Label> labels = m.Instruction.ExtractLabels();
                m.Insert(new CodeInstruction(OpCodes.Ldstr, "Town").WithLabels(labels))
                    .Advance(1)
                    .SetOperandAndAdvance(IsFestivalDayAdjusted);
                count++;
            });

        if (count == 0)
        {
            ModEntry.ModMonitor.Log($"Found no Utility.isFestivalDay() call in {original.FullDescription()}; it was left unchanged.", LogLevel.Warn);
        }
        return matcher.InstructionEnumeration();
    }

    /// <summary>
    /// Whether or not stores at the given map are closed for the festival after adjustments.
    /// </summary>
    /// <param name="mapname">The map to check.</param>
    /// <returns>true if the store is closed, false otherwise.</returns>
    internal static bool StoresClosedForFestival(string mapname)
        => IsFestivalDayAdjustedForConfig(mapname) && Utility.getStartTimeOfFestival() < 1900;

    /// <summary>
    /// Whether or not it should be considered a festival day for the given map, given the config.
    /// </summary>
    /// <param name="mapname">Map to search for.</param>
    /// <returns>If it should be considered a festival day for this specific config.</returns>
    internal static bool IsFestivalDayAdjustedForConfig(string mapname)
    {
        return ModEntry.Config.StoreFestivalBehavior switch
        {
            FestivalsShopBehavior.Open => false,
            FestivalsShopBehavior.Closed => Utility.isFestivalDay(),
            FestivalsShopBehavior.MapDependent => IsFestivalDayForMap(Game1.dayOfMonth, Game1.season, mapname),
            _ => throw new ArgumentOutOfRangeException(nameof(ModEntry.Config.StoreFestivalBehavior), ModEntry.Config.StoreFestivalBehavior, null),
        };
    }

    /// <summary>
    /// Whether or not it should be considered a festival date for that particular map.
    /// </summary>
    /// <param name="day">day.</param>
    /// <param name="season">season.</param>
    /// <param name="mapname">the map name.</param>
    /// <returns>true if it should be considered a festival day.</returns>
    internal static bool IsFestivalDayForMap(int day, Season season, string mapname)
    {
        string s = Utility.getSeasonKey(season) + day;
        if (!DataLoader.Festivals_FestivalDates(Game1.temporaryContent).ContainsKey(s))
        {
            return false;
        }

        string mapRegion = GetMapRegion(mapname);

        try
        {
            Dictionary<string, string> festivaldata = Game1.temporaryContent.Load<Dictionary<string, string>>($@"Data\Festivals\{s}");
            if (festivaldata.TryGetValue("conditions", out string? conditionsStr))
            {
                string conditions = conditionsStr.Split('/', 2)[0].Trim();

                ModEntry.ModMonitor.VerboseLog($"Testing {conditions} against {mapRegion}");
                return FestivalRegions.ConditionsMatchRegion(conditions, mapRegion);
            }
        }
        catch (ContentLoadException)
        {
            ModEntry.ModMonitor.Log($"Festival data for {season} {day} was not found.", LogLevel.Warn);
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Failed loading festival data for {season} {day}: {ex}", LogLevel.Error);
        }
        return false;
    }

    /// <summary>
    /// Gets the region a map belongs to: its root location context if not the default one, else derived from the map name.
    /// </summary>
    /// <param name="mapname">The map name.</param>
    /// <returns>The region name.</returns>
    private static string GetMapRegion(string mapname)
    {
        if (Game1.getLocationFromName(mapname) is { } loc)
        {
            string contextId = loc.GetLocationContextId();
            if (Game1.locationContextData.TryGetValue(contextId, out var context))
            {
                HashSet<string> visited = new() { contextId };
                while (context.CopyWeatherFromLocation is { } next && visited.Add(next))
                {
                    if (!Game1.locationContextData.TryGetValue(next, out var nextData))
                    {
                        ModEntry.ModMonitor.Log($"Could not find location data corresponding to {next}, skipping.");
                        break;
                    }
                    contextId = next;
                    context = nextData;
                }

                if (contextId != "Default")
                {
                    return contextId;
                }
            }
        }

        return FestivalRegions.RegionFromMapName(mapname);
    }
}
