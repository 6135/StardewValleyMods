using GingerIslandMainlandAdjustments.Configuration;

namespace GingerIslandMainlandAdjustments.Integrations;

/// <summary>
/// Class that generates the GMCM for this mod.
/// </summary>
internal static class GenerateGMCM
{
    private const string StrictnessPage = "strictness";

    private static IGenericModConfigMenuApi? api;
    private static IManifest manifest = null!;
    private static ITranslationHelper translation = null!;

    /// <summary>
    /// Grabs the GMCM API.
    /// </summary>
    /// <param name="manifest">This mod's manifest.</param>
    /// <param name="translation">The translation helper.</param>
    internal static void Initialize(IManifest manifest, ITranslationHelper translation)
    {
        GenerateGMCM.manifest = manifest;
        GenerateGMCM.translation = translation;
        api = Globals.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
    }

    /// <summary>
    /// Builds the main GMCM page.
    /// </summary>
    internal static void Build()
    {
        if (api is null)
        {
            return;
        }

        api.Unregister(manifest);
        api.Register(manifest, reset: static () => Globals.Config = new ModConfig(), save: SaveConfig);
        api.AddParagraph(manifest, I18n.ModDescription);
        api.AddBoolOption(
            manifest,
            name: I18n.Config_EnforceGITiming_Title,
            getValue: static () => Globals.Config.EnforceGITiming,
            setValue: static value => Globals.Config.EnforceGITiming = value,
            tooltip: I18n.Config_EnforceGITiming_Description);
        api.AddBoolOption(
            manifest,
            name: I18n.Config_RequireResortDialogue_Title,
            getValue: static () => Globals.Config.RequireResortDialogue,
            setValue: static value => Globals.Config.RequireResortDialogue = value,
            tooltip: I18n.Config_RequireResortDialogue_Description);
        AddEnumOption(
            name: I18n.Config_WearIslandClothing_Title,
            getValue: static () => Globals.Config.WearIslandClothing,
            setValue: static value => Globals.Config.WearIslandClothing = value,
            tooltip: I18n.Config_WearIslandClothing_Description);
        api.AddBoolOption(
            manifest,
            name: I18n.Config_Scheduler_Title,
            getValue: static () => Globals.Config.UseThisScheduler,
            setValue: static value => Globals.Config.UseThisScheduler = value,
            tooltip: I18n.Config_Scheduler_Description);
        api.AddParagraph(manifest, I18n.Config_Scheduler_Otheroptions);
        api.AddNumberOption(
            manifest,
            name: I18n.Config_Capacity_Title,
            getValue: static () => Globals.Config.Capacity,
            setValue: static value => Globals.Config.Capacity = value,
            tooltip: I18n.Config_Capacity_Description,
            min: 0,
            max: 15);
        api.AddBoolOption(
            manifest,
            name: I18n.Config_Stage_Title,
            getValue: static () => Globals.Config.StageFarNpcsAtSaloon,
            setValue: static value => Globals.Config.StageFarNpcsAtSaloon = value,
            tooltip: I18n.Config_Stage_Description);
        AddChanceOption(
            name: I18n.Config_GroupChance_Title,
            getValue: static () => Globals.Config.GroupChance,
            setValue: static value => Globals.Config.GroupChance = value,
            tooltip: I18n.Config_GroupChance_Description);
        AddChanceOption(
            name: I18n.Config_ExplorerChance_Title,
            getValue: static () => Globals.Config.ExplorerChance,
            setValue: static value => Globals.Config.ExplorerChance = value,
            tooltip: I18n.Config_ExplorerChance_Description);
        AddEnumOption(
            name: I18n.Config_GusDay_Title,
            getValue: static () => Globals.Config.GusDay,
            setValue: static value => Globals.Config.GusDay = value,
            tooltip: I18n.Config_GusDay_Description);
        AddChanceOption(
            name: I18n.Config_GusChance_Title,
            getValue: static () => Globals.Config.GusChance,
            setValue: static value => Globals.Config.GusChance = value,
            tooltip: I18n.Config_GusChance_Description);

        AddExclusionOption(nameof(ModConfig.AllowWilly), static () => Globals.Config.AllowWilly, static value => Globals.Config.AllowWilly = value);
        AddExclusionOption(nameof(ModConfig.AllowSandy), static () => Globals.Config.AllowSandy, static value => Globals.Config.AllowSandy = value);
        api.AddBoolOption(
            manifest,
            name: () => translation.Get($"{nameof(ModConfig.AllowGeorgeAndEvelyn)}.title"),
            getValue: static () => Globals.Config.AllowGeorgeAndEvelyn,
            setValue: static value => Globals.Config.AllowGeorgeAndEvelyn = value,
            tooltip: () => translation.Get($"{nameof(ModConfig.AllowGeorgeAndEvelyn)}.description"));
        AddExclusionOption(nameof(ModConfig.AllowWizard), static () => Globals.Config.AllowWizard, static value => Globals.Config.AllowWizard = value);
    }

    /// <summary>
    /// Adds the per-NPC schedule strictness page, once the save's NPCs are known.
    /// </summary>
    internal static void BuildNPCDictionary()
    {
        if (api is null)
        {
            return;
        }

        Globals.Config.PopulateScheduleStrictness();

        api.AddPageLink(manifest, StrictnessPage, I18n.ScheduleStrictness, I18n.ScheduleStrictness_Description);
        api.AddPage(manifest, StrictnessPage, I18n.ScheduleStrictness);
        api.AddParagraph(manifest, I18n.ScheduleStrictness_Description);
        foreach (string name in Globals.Config.ScheduleStrictness.Keys)
        {
            AddEnumOption(
                name: () => Game1.getCharacterFromName(name)?.displayName ?? name,
                getValue: () => Globals.Config.ScheduleStrictness.TryGetValue(name, out ScheduleStrictness val) ? val : ScheduleStrictness.Default,
                setValue: value => Globals.Config.ScheduleStrictness[name] = value);
        }

        SaveConfig();
    }

    private static void SaveConfig() => Globals.Helper.WriteConfig(Globals.Config);

    private static void AddChanceOption(Func<string> name, Func<float> getValue, Action<float> setValue, Func<string> tooltip)
        => api!.AddNumberOption(manifest, getValue, setValue, name, tooltip, min: 0f, max: 1f, interval: 0.01f, formatValue: static f => $"{f:f2}");

    private static void AddExclusionOption(string property, Func<VillagerExclusionOverride> getValue, Action<VillagerExclusionOverride> setValue)
        => AddEnumOption(
            name: () => translation.Get($"{property}.title"),
            getValue: getValue,
            setValue: setValue,
            tooltip: () => translation.Get($"{property}.description"));

    private static void AddEnumOption<TEnum>(Func<string> name, Func<TEnum> getValue, Action<TEnum> setValue, Func<string>? tooltip = null)
        where TEnum : struct, Enum
        => api!.AddTextOption(
            manifest,
            getValue: () => getValue().ToString(),
            setValue: value => setValue(Enum.Parse<TEnum>(value)),
            name: name,
            tooltip: tooltip,
            allowedValues: Enum.GetNames<TEnum>(),
            formatAllowedValue: static value => translation.Get($"config.{typeof(TEnum).Name}.{value}"));
}
