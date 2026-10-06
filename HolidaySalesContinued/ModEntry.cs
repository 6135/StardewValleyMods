using HarmonyLib;

using StardewModdingAPI.Events;

namespace HolidaySales;

/// <inheritdoc />
internal sealed class ModEntry : Mod
{
    /// <summary>
    /// Gets the logger for this mod.
    /// </summary>
    internal static IMonitor ModMonitor { get; private set; } = null!;

    /// <summary>
    /// Gets the config instance for this mod.
    /// </summary>
    internal static ModConfig Config { get; private set; } = null!;

    /// <inheritdoc />
    public override void Entry(IModHelper helper)
    {
        ModMonitor = this.Monitor;

        try
        {
            Config = helper.ReadConfig<ModConfig>();
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"{helper.Translation.Get("IllFormatedConfig")}\n{ex}", LogLevel.Warn);
            Config = new();
        }

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunch;
    }

    private void OnGameLaunch(object? sender, GameLaunchedEventArgs e)
    {
        // delayed until GameLaunched so other mods' assemblies (GIMA) are loaded.
        try
        {
            new Harmony(this.ModManifest.UniqueID).PatchAll(typeof(ModEntry).Assembly);
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Mod crashed while applying Harmony patches:\n\n{ex}", LogLevel.Error);
        }

        this.RegisterGmcm();
    }

    private void RegisterGmcm()
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
            save: () => this.Helper.WriteConfig(Config));
        gmcm.AddTextOption(
            mod: this.ModManifest,
            getValue: static () => Config.StoreFestivalBehavior.ToString(),
            setValue: static value =>
            {
                if (Enum.TryParse(value, out FestivalsShopBehavior behavior))
                {
                    Config.StoreFestivalBehavior = behavior;
                }
            },
            name: () => i18n.Get("StoreFestivalBehavior.title"),
            tooltip: () => i18n.Get("StoreFestivalBehavior.description"),
            allowedValues: Enum.GetNames<FestivalsShopBehavior>(),
            formatAllowedValue: value => i18n.Get($"config.FestivalsShopBehavior.{value}"));
    }
}
