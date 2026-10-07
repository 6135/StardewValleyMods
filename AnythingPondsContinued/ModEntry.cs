using HarmonyLib;

using StardewModdingAPI.Events;

using StardewValley.Buildings;
using StardewValley.Extensions;
using StardewValley.GameData.FishPonds;

namespace AnythingPonds;

/// <inheritdoc />
internal sealed class ModEntry : Mod
{
    private const string EmptyDaysKey = "6135.AnythingPonds/EmptyDays";

    /// <summary>
    /// Gets the config instance for this mod.
    /// </summary>
    internal static ModConfig Config { get; private set; } = null!;

    /// <inheritdoc />
    public override void Entry(IModHelper helper)
    {
        try
        {
            Config = helper.ReadConfig<ModConfig>();
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"{helper.Translation.Get("IllFormatedConfig")}\n{ex}", LogLevel.Warn);
            Config = new();
        }

        try
        {
            new Harmony(this.ModManifest.UniqueID).PatchAll(typeof(ModEntry).Assembly);
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Mod crashed while applying Harmony patches:\n\n{ex}", LogLevel.Error);
        }

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.Content.AssetRequested += OnAssetRequested;
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo("Data/FishPondData"))
        {
            e.Edit(asset => PondData.Edit(asset.GetData<List<FishPondData>>(), Config), AssetEditPriority.Early);
        }
    }

    private static void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!Context.IsMainPlayer || !Config.EmptyPondsBecomeAlgae)
        {
            return;
        }

        Utility.ForEachBuilding<FishPond>(pond =>
        {
            if (pond.fishType.Value is not null || pond.currentOccupants.Value > 0)
            {
                pond.modData.Remove(EmptyDaysKey);
                return true;
            }

            int days = (pond.modData.TryGetValue(EmptyDaysKey, out string? raw) && int.TryParse(raw, out int parsed) ? parsed : 0) + 1;
            if (days < Config.DaysUntilAlgae)
            {
                pond.modData[EmptyDaysKey] = days.ToString();
                return true;
            }

            pond.modData.Remove(EmptyDaysKey);
            pond.fishType.Value = Game1.random.ChooseFrom(PondData.AlgaeIds);
            pond.currentOccupants.Value = 1;
            pond.UpdateMaximumOccupancy();
            return true;
        });
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        IGenericModConfigMenuApi? gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null)
        {
            return;
        }

        ITranslationHelper i18n = this.Helper.Translation;
        gmcm.Register(
            mod: this.ModManifest,
            reset: static () => Config = new(),
            save: () =>
            {
                this.Helper.WriteConfig(Config);
                this.Helper.GameContent.InvalidateCache("Data/FishPondData");
            });
        gmcm.AddBoolOption(this.ModManifest, static () => Config.EnableAlgaePonds, static v => Config.EnableAlgaePonds = v, () => i18n.Get("EnableAlgaePonds.title"), () => i18n.Get("EnableAlgaePonds.description"));
        gmcm.AddBoolOption(this.ModManifest, static () => Config.EnableGenericPonds, static v => Config.EnableGenericPonds = v, () => i18n.Get("EnableGenericPonds.title"), () => i18n.Get("EnableGenericPonds.description"));
        gmcm.AddBoolOption(this.ModManifest, static () => Config.EmptyPondsBecomeAlgae, static v => Config.EmptyPondsBecomeAlgae = v, () => i18n.Get("EmptyPondsBecomeAlgae.title"), () => i18n.Get("EmptyPondsBecomeAlgae.description"));
        gmcm.AddNumberOption(this.ModManifest, static () => Config.DaysUntilAlgae, static v => Config.DaysUntilAlgae = v, () => i18n.Get("DaysUntilAlgae.title"), () => i18n.Get("DaysUntilAlgae.description"), min: 1, max: 28);
    }
}
