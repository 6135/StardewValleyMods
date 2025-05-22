using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using UIFramework.API;
using UIFramework.Components;
using UIFramework.Components.Base;
using UIFramework.Config;
using UIFramework.Layout;
using UIFramework.memory;

#nullable enable

namespace UIFramework
{
    public class ModEntry : Mod
    {
        public static ModConfig? Config;
        private bool _exampleInitialized = false;
        internal static readonly string UniqueID = "6135.UIFramework";
        private IStardewUIAPI? _api;

        public override object? GetApi()
        {
            return new StardewUIAPI();
        }

        public override void Entry(IModHelper helper)
        {
            // Register core services in container
            Container.Instance.RegisterInstance(helper, UniqueID);
            Container.Instance.RegisterInstance(this.Monitor, UniqueID);

            // Load configuration
            Config = helper.ReadConfig<ModConfig>();
            if (Config is null)
            {
                Config = new ModConfig();
                helper.WriteConfig(Config);
            }

            // Create UI config
            var uiConfig = new UIConfig
            {
                DefaultTooltipDelay = Config.ToolTipDelay,
                DefaultToggleMenuKey = Config.HotKey
            };
            Container.Instance.RegisterInstance(uiConfig, UniqueID);

            // Register API for other mods to access
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Make API available to other mods
            Monitor.Log("Registering UI Framework API for other mods to use", LogLevel.Info);

            // Register with Generic Mod Config Menu if available
            RegisterWithGenericModConfigMenu();
        }

        private void RegisterWithGenericModConfigMenu()
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config!)
            );

            configMenu.AddKeybind(
                mod: ModManifest,
                getValue: () => Config!.HotKey,
                setValue: value => Config!.HotKey = value,
                name: () => "Framework Toggle Key",
                tooltip: () => "Hotkey used to toggle UI Framework menus"
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                getValue: () => Config!.ToolTipDelay,
                setValue: value => Config!.ToolTipDelay = value,
                name: () => "Tooltip Delay",
                tooltip: () => "Delay in milliseconds before tooltips appear",
                min: 0,
                max: 2000
            );
        }
    }
}