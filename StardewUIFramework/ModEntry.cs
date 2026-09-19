using System.Linq;
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
            helper.ConsoleCommands.Add("ui_list", "List the UI Framework menus that are currently open.", (_, _) =>
            {
                string list = string.Join("\n", menus.OpenMenus.Select(m => $"  {m}"));
                Monitor.Log(menus.OpenMenus.Count == 0 ? "No framework menus are open." : $"Open menus:\n{list}", LogLevel.Info);
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
            var layouts = new WindowLayoutStore(helper.Data);
            var hud = new HudService(helper, menus);
            UIServices.Layouts = layouts;
            UIServices.Hud = hud;
            helper.Events.GameLoop.SaveLoaded += (_, _) =>
            {
                layouts.Load();
                hud.ApplyLayouts();
            };
            helper.Events.GameLoop.Saving += (_, _) => layouts.Save();
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => layouts.Clear();
            helper.ConsoleCommands.Add("ui_toast", "Show a test toast: ui_toast <text>.", (_, args) =>
            {
                hud.ShowToast(args.Length == 0 ? "Hello from the UI Framework!" : string.Join(" ", args), null, null, ToastLayer.DefaultDurationMs);
            });
            helper.ConsoleCommands.Add("ui_layout_reset", "Forget every player-adjusted window position / size / collapsed state for the current save.", (_, _) =>
            {
                int count = layouts.Count;
                layouts.Clear();
                if (Context.IsWorldReady)
                {
                    layouts.Save();
                }

                Monitor.Log($"Cleared {count} saved window layout(s).", LogLevel.Info);
            });
            // END HUD entry

            // BEGIN TOOLS entry
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
            // END TOOLS gmcm
        }
    }
}
