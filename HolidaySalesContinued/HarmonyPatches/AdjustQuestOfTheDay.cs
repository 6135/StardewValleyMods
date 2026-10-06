using HarmonyLib;

using StardewValley.Quests;

namespace HolidaySales.HarmonyPatches;

/// <summary>
/// Allows the daily quest when the festival blocking it is outside of town.
/// </summary>
[HarmonyPatch(typeof(Game1), nameof(Game1.RefreshQuestOfTheDay))]
internal static class AdjustQuestOfTheDay
{
    private static void Postfix()
    {
        // only act when vanilla skipped the quest, and only if that was because of a festival today or tomorrow.
        if (ModEntry.Config.StoreFestivalBehavior == FestivalsShopBehavior.Closed
            || Game1.netWorldState.Value.QuestOfTheDay is not null)
        {
            return;
        }

        try
        {
            (Season tomorrowSeason, int tomorrowDay) = GetTomorrow();
            if (!Utility.isFestivalDay() && !Utility.isFestivalDay(tomorrowDay, tomorrowSeason))
            {
                return;
            }

            if (ModEntry.Config.StoreFestivalBehavior == FestivalsShopBehavior.MapDependent
                && (HSUtils.IsFestivalDayForMap(Game1.dayOfMonth, Game1.season, "Town")
                    || HSUtils.IsFestivalDayForMap(tomorrowDay, tomorrowSeason, "Town")))
            {
                return;
            }

            Quest? quest = Utility.getQuestOfTheDay();
            if (quest is null)
            {
                return;
            }
            quest.dailyQuest.Set(true);
            quest.reloadObjective();
            quest.reloadDescription();
            Game1.netWorldState.Value.SetQuestOfTheDay(quest);
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Failed adjusting the daily quest: {ex}", LogLevel.Error);
        }
    }

    private static (Season Season, int Day) GetTomorrow()
        => Game1.dayOfMonth >= 28
            ? ((Season)(((int)Game1.season + 1) % 4), 1)
            : (Game1.season, Game1.dayOfMonth + 1);
}
