using HarmonyLib;

using Microsoft.Xna.Framework.Graphics;

using CapstoneProfessions.Framework;
using StardewModdingAPI.Events;

namespace CapstoneProfessions;

/// <inheritdoc />
internal sealed class ModEntry : Mod
{
    /// <summary>Profession ID of Timelapse.</summary>
    public const int ProfessionTime = 1000;

    /// <summary>Profession ID of Name Brand.</summary>
    public const int ProfessionProfit = 1001;

    /// <summary>Gets the logger for this mod.</summary>
    internal static IMonitor ModMonitor { get; private set; } = null!;

    /// <summary>Gets the translation helper.</summary>
    internal static ITranslationHelper Translations { get; private set; } = null!;

    /// <summary>Gets the clock icon.</summary>
    internal static Texture2D ClockTex { get; private set; } = null!;

    /// <inheritdoc />
    public override void Entry(IModHelper helper)
    {
        ModMonitor = this.Monitor;
        Translations = helper.Translation;
        ClockTex = helper.ModContent.Load<Texture2D>("assets/clock.png");

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;

        Harmony harmony = new(this.ModManifest.UniqueID);
        try
        {
            Patches.TimelapsePatch.Apply(harmony);
            Patches.NameBrandPatch.Apply(harmony);
            Patches.NightEndMenusPatch.Apply(harmony);
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Failed to apply Harmony patches:\n{ex}", LogLevel.Error);
        }
    }

    internal static bool HasMaxedSkills()
    {
        // Base levels, so buffs don't count.
        Farmer p = Game1.player;
        return p.farmingLevel.Value >= 10
            && p.foragingLevel.Value >= 10
            && p.fishingLevel.Value >= 10
            && p.miningLevel.Value >= 10
            && p.combatLevel.Value >= 10;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        if (this.Helper.ModRegistry.IsLoaded("cantorsdust.AllProfessions"))
        {
            this.Helper.Events.Player.Warped += this.OnWarped;
        }
    }

    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (!e.IsLocalPlayer)
        {
            return;
        }

        bool time = e.Player.professions.Contains(ProfessionTime);
        bool profit = e.Player.professions.Contains(ProfessionProfit);
        if (time && !profit)
        {
            e.Player.professions.Add(ProfessionProfit);
        }
        else if (profit && !time)
        {
            e.Player.professions.Add(ProfessionTime);
        }
    }
}
