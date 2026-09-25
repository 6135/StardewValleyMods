using StardewModdingAPI;
using StardewModdingAPI.Events;
using UIFramework.Core;
using UIFramework.Hosting;
using UIFramework.Rendering;

namespace UIFramework
{
    /// <summary>The framework's Generic Mod Config Menu page (optional: nothing happens when GMCM is not installed).</summary>
    internal static class ConfigMenu
    {
        /// <summary>
        /// Update ticks to wait after <c>GameLaunched</c> before the page is registered. The theme dropdown lists the
        /// themes in the theme asset when the page is registered, and Content Patcher applies its packs' edits only once
        /// it initialized on its first update tick; a few ticks later the packs' themes are in the asset. The whole page is
        /// registered then, so the option order does not depend on the delay.
        /// </summary>
        private const int RegisterDelayTicks = 5;

        /// <summary>Register the page <see cref="RegisterDelayTicks"/> update ticks from now.</summary>
        internal static void RegisterWhenReady(IModHelper helper, IManifest manifest)
        {
            var gmcm = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null)
            {
                return;
            }

            int ticksLeft = RegisterDelayTicks;
            void RegisterWhenDue(object? sender, UpdateTickedEventArgs e)
            {
                if (--ticksLeft > 0)
                {
                    return;
                }

                helper.Events.GameLoop.UpdateTicked -= RegisterWhenDue;
                Register(gmcm, helper, manifest);
            }
            helper.Events.GameLoop.UpdateTicked += RegisterWhenDue;
        }

        private static void Register(IGenericModConfigMenuApi gmcm, IModHelper helper, IManifest manifest)
        {
            ITranslationHelper i18n = helper.Translation;
            gmcm.Register(manifest, Reset, () => helper.WriteConfig(UIServices.Config), titleScreenOnly: false);
            gmcm.AddNumberOption(manifest,
                getValue: () => UIServices.Config.TooltipDelayMs,
                setValue: v => UIServices.Config.TooltipDelayMs = v,
                name: () => i18n.Get("config.tooltip-delay"),
                tooltip: () => i18n.Get("config.tooltip-delay.desc"),
                min: 0, max: 3000, interval: 50, formatValue: null, fieldId: null);
            gmcm.AddBoolOption(manifest,
                getValue: () => UIServices.Config.DebugOverlay,
                setValue: v => UIServices.Config.DebugOverlay = v,
                name: () => i18n.Get("config.debug-overlay"),
                tooltip: () => i18n.Get("config.debug-overlay.desc"),
                fieldId: null);
            gmcm.AddBoolOption(manifest,
                getValue: () => UIServices.Config.LogCallbacks,
                setValue: v => UIServices.Config.LogCallbacks = v,
                name: () => i18n.Get("config.log-callbacks"),
                tooltip: () => i18n.Get("config.log-callbacks.desc"),
                fieldId: null);

            // BEGIN RICHTEXT gmcm
            gmcm.AddBoolOption(manifest,
                getValue: () => UIServices.Config.PseudoLocalize,
                setValue: v => UIServices.Config.PseudoLocalize = v,
                name: () => i18n.Get("config.pseudo-localize"),
                tooltip: () => i18n.Get("config.pseudo-localize.desc"),
                fieldId: null);
            // END RICHTEXT gmcm

            // BEGIN THEME gmcm
            gmcm.AddTextOption(manifest,
                getValue: () => UIServices.Config.Theme,
                setValue: v => ThemeSwitcher.Apply(v ?? Theme.DefaultThemeName, manifest.UniqueID),
                name: () => i18n.Get("config.theme"),
                tooltip: () => i18n.Get("config.theme.desc"),
                allowedValues: ThemeSwitcher.Choices(), formatAllowedValue: null, fieldId: null);
            gmcm.AddNumberOption(manifest,
                getValue: () => UIServices.Config.TextScale,
                setValue: v =>
                {
                    UIServices.Config.TextScale = v;
                    ThemeSwitcher.Refresh();
                },
                name: () => i18n.Get("config.text-scale"),
                tooltip: () => i18n.Get("config.text-scale.desc"),
                min: 0.75f, max: 2f, interval: 0.05f, formatValue: v => v.ToString("0.00"), fieldId: null);
            gmcm.AddBoolOption(manifest,
                getValue: () => UIServices.Config.ReducedMotion,
                setValue: v => UIServices.Config.ReducedMotion = v,
                name: () => i18n.Get("config.reduced-motion"),
                tooltip: () => i18n.Get("config.reduced-motion.desc"),
                fieldId: null);
            // END THEME gmcm

            // BEGIN TOOLS gmcm
            gmcm.AddKeybindList(manifest,
                getValue: () => Inspector.ParseHotkey(UIServices.Config.InspectorHotkey),
                setValue: v => UIServices.Config.InspectorHotkey = v.ToString(),
                name: () => i18n.Get("config.inspector-hotkey"),
                tooltip: () => i18n.Get("config.inspector-hotkey.desc"),
                fieldId: null);
            // END TOOLS gmcm
        }

        /// <summary>GMCM "reset": default settings; the theme is re-read (theme name, text scale).</summary>
        private static void Reset()
        {
            UIServices.Config = new ModConfig();
            ThemeSwitcher.Refresh();
        }
    }
}
