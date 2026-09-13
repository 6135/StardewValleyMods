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
                return;

            gmcm.Register(ModManifest, () => UIServices.Config = config = new ModConfig(), () => Helper.WriteConfig(config));
            gmcm.AddNumberOption(ModManifest,
                getValue: () => config.TooltipDelayMs,
                setValue: v => config.TooltipDelayMs = v,
                name: () => Helper.Translation.Get("config.tooltip-delay"),
                tooltip: () => Helper.Translation.Get("config.tooltip-delay.desc"),
                min: 0, max: 3000, interval: 50);
            gmcm.AddBoolOption(ModManifest,
                getValue: () => config.DebugOverlay,
                setValue: v => config.DebugOverlay = v,
                name: () => Helper.Translation.Get("config.debug-overlay"),
                tooltip: () => Helper.Translation.Get("config.debug-overlay.desc"));
            gmcm.AddBoolOption(ModManifest,
                getValue: () => config.LogCallbacks,
                setValue: v => config.LogCallbacks = v,
                name: () => Helper.Translation.Get("config.log-callbacks"),
                tooltip: () => Helper.Translation.Get("config.log-callbacks.desc"));
        }
    }
}
