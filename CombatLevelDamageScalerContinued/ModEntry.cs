using HarmonyLib;

using StardewModdingAPI.Events;

namespace CombatLevelDamageScaler;

/// <inheritdoc />
internal sealed class ModEntry : Mod
{
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
            this.Monitor.Log($"Could not read config, using defaults:\n{ex}", LogLevel.Warn);
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
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        IGenericModConfigMenuApi? gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null)
        {
            return;
        }

        gmcm.Register(
            this.ModManifest,
            reset: () => Config = new(),
            save: () => this.Helper.WriteConfig(Config));
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => (int)Math.Round(Config.DamageScalePerLevel * 100),
            setValue: value => Config.DamageScalePerLevel = value / 100f,
            name: () => this.Helper.Translation.Get("config.damage-scale.name"),
            tooltip: () => this.Helper.Translation.Get("config.damage-scale.tooltip"));
    }
}
