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

    private const char FertilizerSeparator = '|';

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

    /// <summary>
    /// Checks whether a fertilizer ID (qualified or not) is the giant crop fertilizer.
    /// </summary>
    /// <param name="fertilizer">Fertilizer to check.</param>
    /// <returns>True if matches, false otherwise.</returns>
    internal static bool IsGiantCropFertilizer(string? fertilizer)
        => fertilizer is GiantCropFertilizerID or QualifiedGiantCropFertilizerID;

    /// <summary>
    /// Checks whether a <see cref="StardewValley.TerrainFeatures.HoeDirt.fertilizer"/> value holds the giant crop fertilizer.
    /// Ultimate Fertilizer stores several fertilizers as <c>a|b|c</c>.
    /// </summary>
    /// <param name="value">The soil's fertilizer value.</param>
    /// <returns>True if the giant crop fertilizer is one of them.</returns>
    internal static bool HasGiantCropFertilizer(string? value)
        => value is not null && value.Split(FertilizerSeparator).Any(IsGiantCropFertilizer);

    /// <summary>
    /// Removes the giant crop fertilizer from a soil's fertilizer value, keeping any others.
    /// </summary>
    /// <param name="value">The soil's fertilizer value.</param>
    /// <returns>The remaining fertilizers, or null if none are left.</returns>
    internal static string? RemoveGiantCropFertilizer(string? value)
    {
        string remaining = string.Join(FertilizerSeparator, (value ?? string.Empty).Split(FertilizerSeparator).Where(id => id.Length > 0 && !IsGiantCropFertilizer(id)));
        return remaining.Length > 0 ? remaining : null;
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
