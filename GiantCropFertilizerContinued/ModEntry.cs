using HarmonyLib;

using StardewModdingAPI.Events;

namespace GiantCropFertilizer;

/// <inheritdoc />
internal sealed class ModEntry : Mod
{
    /// <summary>
    /// The <see cref="Item.ItemId"/> of the giant crop fertilizer.
    /// </summary>
    internal const string GiantCropFertilizerID = "6135.GiantCropFertilizer_Fertilizer";

    /// <summary>
    /// The <see cref="Item.QualifiedItemId" /> of the giant crop fertilizer.
    /// </summary>
    internal const string QualifiedGiantCropFertilizerID = $"{ItemRegistry.type_object}{GiantCropFertilizerID}";

    /// <summary>
    /// Gets the logger for this mod.
    /// </summary>
    internal static IMonitor ModMonitor { get; private set; } = null!;

    /// <summary>
    /// Gets the translation helper for this mod.
    /// </summary>
    internal static ITranslationHelper I18n { get; private set; } = null!;

    /// <summary>
    /// Gets the config instance for this mod.
    /// </summary>
    internal static ModConfig Config { get; private set; } = null!;

    /// <inheritdoc />
    public override void Entry(IModHelper helper)
    {
        ModMonitor = this.Monitor;
        I18n = helper.Translation;

        try
        {
            Config = helper.ReadConfig<ModConfig>();
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"{I18n.Get("IllFormatedConfig")}\n{ex}", LogLevel.Warn);
            Config = new();
        }

        AssetManager.Init(helper.GameContent);
        helper.Events.Content.AssetRequested += static (_, e) => AssetManager.Apply(e);
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        try
        {
            new Harmony(this.ModManifest.UniqueID).PatchAll(typeof(ModEntry).Assembly);
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Mod crashed while applying Harmony patches:\n\n{ex}", LogLevel.Error);
        }

        this.RegisterGmcm();
        this.RegisterUltimateFertilizer();
    }

    /// <summary>
    /// Registers the fertilizer as its own type, so Ultimate Fertilizer's one-per-type modes stack it with others instead of dropping it.
    /// </summary>
    private void RegisterUltimateFertilizer()
    {
        this.Helper.ModRegistry.GetApi<IUltimateFertilizerApi>("fox_white25.ultimate_fertilizer")?.RegisterFertilizerType(new[] { QualifiedGiantCropFertilizerID });
    }

    private void RegisterGmcm()
    {
        IGenericModConfigMenuApi? gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null)
        {
            return;
        }

        gmcm.Register(
            mod: this.ModManifest,
            reset: static () => Config = new(),
            save: () =>
            {
                this.Helper.WriteConfig(Config);
                this.Helper.GameContent.InvalidateCache(static asset => asset.DataType == typeof(xTile.Map));
            });
        gmcm.AddParagraph(this.ModManifest, static () => I18n.Get("mod-description"));
        gmcm.AddNumberOption(
            mod: this.ModManifest,
            getValue: static () => (float)Config.GiantCropChance,
            setValue: static value => Config.GiantCropChance = value,
            name: static () => I18n.Get("GiantCropChance.title"),
            tooltip: static () => I18n.Get("GiantCropChance.description"),
            min: 0f,
            max: 1.1f,
            interval: 0.01f);
        gmcm.AddBoolOption(
            mod: this.ModManifest,
            getValue: static () => Config.AllowGiantCropsOffFarm,
            setValue: static value => Config.AllowGiantCropsOffFarm = value,
            name: static () => I18n.Get("AllowGiantCropsOffFarm.title"),
            tooltip: static () => I18n.Get("AllowGiantCropsOffFarm.description"));
    }
}
