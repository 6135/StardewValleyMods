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
        private readonly CompositeRegistry composites = new();
        private HotkeyService hotkeys = null!;
        private ExtensionRegistry extensions = null!;
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
            extensions = new ExtensionRegistry(menus);
            helper.ConsoleCommands.Add("ui_slots", "Print the extension slots of every open UI Framework menu (hints and contributors).", (_, _) =>
            {
                Monitor.Log(extensions.DescribeOpenMenus(), LogLevel.Info);
            });
            // END SLOTS entry

            // BEGIN COMPOSITES entry
            helper.ConsoleCommands.Add("ui_composites", "List the composite components defined through the UI Framework.", (_, _) =>
            {
                string[] names = composites.List();
                Monitor.Log(names.Length == 0 ? "No composites are defined." : $"Composites:\n  {string.Join("\n  ", names)}", LogLevel.Info);
            });
            // END COMPOSITES entry

            // BEGIN RICHTEXT entry
            helper.ConsoleCommands.Add("ui_pseudoloc", "Toggle UI Framework pseudo-localization (accented, padded strings in every framework menu).", (_, _) =>
            {
                config.PseudoLocalize = !config.PseudoLocalize;
                Monitor.Log($"Pseudo-localization {(config.PseudoLocalize ? "enabled" : "disabled")}.", LogLevel.Info);
            });
            // END RICHTEXT entry

            // BEGIN THEME entry
            // END THEME entry

            // BEGIN SIGNALS entry
            // END SIGNALS entry

            // BEGIN HUD entry
            // END HUD entry

            // BEGIN TOOLS entry
            // END TOOLS entry
        }

        /// <summary>One API instance per consumer so ids, hotkeys and styles are namespaced and can be torn down together.</summary>
        public override object GetApi(IModInfo mod)
        {
            var consumer = new ConsumerContext(mod.Manifest.UniqueID);
            Monitor.Log($"API requested by {mod.Manifest.UniqueID}.", LogLevel.Trace);
            return new StardewUIApi(consumer, menus, hotkeys, composites, extensions);
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
            gmcm.AddBoolOption(ModManifest,
                getValue: () => config.PseudoLocalize,
                setValue: v => config.PseudoLocalize = v,
                name: () => Helper.Translation.Get("config.pseudo-localize"),
                tooltip: () => Helper.Translation.Get("config.pseudo-localize.desc"),
                fieldId: null);
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
