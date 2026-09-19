using System.IO;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Hosting;

namespace UIFramework
{
    /// <summary>The mod entry point. Wires the framework services and hands out one <see cref="IStardewUIApi"/> per consumer mod.</summary>
    public class ModEntry : Mod
    {
        private readonly MenuRegistry menus = new();
        private HotkeyService hotkeys = null!;
        private ModConfig config = new();

        public override void Entry(IModHelper helper)
        {
            config = helper.ReadConfig<ModConfig>();

            UIServices.Monitor = Monitor;
            UIServices.Config = config;
            UIServices.ModContent = helper.ModContent;
            UIServices.GameContent = helper.GameContent;
            UIServices.Translation = helper.Translation;

            hotkeys = new HotkeyService(helper.Events);

            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => menus.CloseAll();
            helper.Events.GameLoop.SaveLoaded += (_, _) => menus.CloseAll();
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;

            helper.ConsoleCommands.Add("ui_debug", "Toggle the UI Framework debug overlay (element bounds and ids).", (_, _) =>
            {
                config.DebugOverlay = !config.DebugOverlay;
                Monitor.Log($"Debug overlay {(config.DebugOverlay ? "enabled" : "disabled")}.", LogLevel.Info);
            });

            // v1.1 wiring (one region per feature; see architecture.md §16)

            // BEGIN SLOTS entry
            // END SLOTS entry

            // BEGIN COMPOSITES entry
            // END COMPOSITES entry

            // BEGIN RICHTEXT entry
            // END RICHTEXT entry

            // BEGIN THEME entry
            // END THEME entry

            // BEGIN SIGNALS entry
            // END SIGNALS entry

            // BEGIN HUD entry
            // END HUD entry

            // BEGIN TOOLS entry
            Inspector.ExportDirectory = Path.Combine(helper.DirectoryPath, "export");
            new DebugConsole(menus, Monitor).Register(helper.ConsoleCommands);
            helper.Events.Input.ButtonsChanged += (_, _) => Inspector.OnButtonsChanged();
            // END TOOLS entry
        }

        /// <summary>One API instance per consumer so ids, hotkeys and styles are namespaced and can be torn down together.</summary>
        public override object GetApi(IModInfo mod)
        {
            var consumer = new ConsumerContext(mod.Manifest.UniqueID);
            Monitor.Log($"API requested by {mod.Manifest.UniqueID}.", LogLevel.Trace);
            return new StardewUIApi(consumer, menus, hotkeys);
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            menus.ValidateOpenMenus();
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null)
            {
                return;
            }

            gmcm.Register(ModManifest, () => UIServices.Config = config = new ModConfig(), () => Helper.WriteConfig(config), titleScreenOnly: false);
            gmcm.AddNumberOption(ModManifest,
                getValue: () => config.TooltipDelayMs,
                setValue: v => config.TooltipDelayMs = v,
                name: () => Helper.Translation.Get("config.tooltip-delay"),
                tooltip: () => Helper.Translation.Get("config.tooltip-delay.desc"),
                min: 0, max: 3000, interval: 50, formatValue: null, fieldId: null);
            gmcm.AddBoolOption(ModManifest,
                getValue: () => config.DebugOverlay,
                setValue: v => config.DebugOverlay = v,
                name: () => Helper.Translation.Get("config.debug-overlay"),
                tooltip: () => Helper.Translation.Get("config.debug-overlay.desc"),
                fieldId: null);
            gmcm.AddBoolOption(ModManifest,
                getValue: () => config.LogCallbacks,
                setValue: v => config.LogCallbacks = v,
                name: () => Helper.Translation.Get("config.log-callbacks"),
                tooltip: () => Helper.Translation.Get("config.log-callbacks.desc"),
                fieldId: null);

            // BEGIN RICHTEXT gmcm
            // END RICHTEXT gmcm

            // BEGIN THEME gmcm
            // END THEME gmcm

            // BEGIN HUD gmcm
            // END HUD gmcm

            // BEGIN TOOLS gmcm
            gmcm.AddKeybindList(ModManifest,
                getValue: () => Inspector.ParseHotkey(config.InspectorHotkey),
                setValue: v => config.InspectorHotkey = v.ToString(),
                name: () => Helper.Translation.Get("config.inspector-hotkey"),
                tooltip: () => Helper.Translation.Get("config.inspector-hotkey.desc"),
                fieldId: null);
            // END TOOLS gmcm
        }
    }
}
