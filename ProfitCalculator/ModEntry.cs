using ProfitCalculator.main.memory;
using ProfitCalculator.apis;
using ProfitCalculator.main;
using ProfitCalculator.main.accessors;
using ProfitCalculator.main.models;
using ProfitCalculator.main.ui;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using System.Collections.Generic;
using System.Linq;
using UIFramework.Api;

#nullable enable

namespace ProfitCalculator
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        private ModConfig? Config;
        private IStardewUIApi? uiApi;
        private ProfitCalculatorDataUI? dataUI;
        internal static readonly string UniqueID = "6135.ProfitCalculator";
        private const string UIFrameworkId = "6135.UIFramework";

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Container.Instance.RegisterInstance<Calculator>(UniqueID);
            Container.Instance.RegisterInstance(helper, UniqueID);
            Container.Instance.RegisterInstance(this.Monitor, UniqueID);
            Container.Instance.RegisterInstance<ManualCropRegistry>(UniqueID);

            //read config
            Config = helper.ReadConfig<ModConfig>();
            if (Config is null || Helper is null)
            {
                return;
            }

            //hook events
            helper.Events.GameLoop.GameLaunched += OnGameLaunchedAPIs;
            helper.Events.GameLoop.GameLaunched += OnGameLaunchedAddGenericModConfigMenu;
            helper.Events.GameLoop.GameLaunched += OnGameLaunchedBuildMenus;
            helper.Events.GameLoop.SaveLoaded += OnSaveGameLoaded;
            helper.Events.GameLoop.DayStarted += OnDayStartedResetCache;
            helper.Events.Content.AssetRequested += OnAssetRequested;
            helper.Events.Content.AssetsInvalidated += OnAssetsInvalidated;
            helper.Events.Content.LocaleChanged += OnLocaleChanged;
        }

        /// <summary>The API other mods get through <c>Helper.ModRegistry.GetApi</c>.</summary>
        /// <returns>The <see cref="IProfitCalculatorApi"/> implementation.</returns>
        public override object GetApi()
        {
            return new ProfitCalculatorApi();
        }

        /*********
        ** Private methods
        *********/

        [EventPriority(EventPriority.Low - 9999)]
        private void OnDayStartedResetCache(object? sender, DayStartedEventArgs? e)
        {
            Container.Instance.GetInstance<ShopAccessor>(ModEntry.UniqueID)?.ForceRebuildCache();
            Container.Instance.GetInstance<MachineAccessor>(ModEntry.UniqueID)?.InvalidateCaches();
            // the plants are shared by all split-screen players, so only the main screen (id 0) rebuilds them
            if (Context.ScreenId == 0)
            {
                Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID)?.RebuildCropsIfDirty();
            }
        }

        /// <summary>Names of the assets the plant list is built from; an edit to one rebuilds the plants before the next calculation.</summary>
        private static readonly string[] PlantAssets = { "Data/Crops", "Data/FruitTrees", "Data/WildTrees", ManualCropRegistry.ManualCropsAsset };

        /// <summary>Keep the plants, shops and machines in step with game data edited mid-save (for example by Content Patcher).</summary>
        private void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
        {
            if (e.NamesWithoutLocale.Any(name => PlantAssets.Any(asset => name.IsEquivalentTo(asset))))
            {
                Container.Instance.GetInstance<Calculator>(UniqueID)?.MarkCropsDirty();
            }
            if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo("Data/Shops") || name.IsEquivalentTo(ManualCropRegistry.SeedPricesAsset)))
            {
                Container.Instance.GetInstance<ShopAccessor>(UniqueID)?.InvalidateCaches();
            }
            if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo("Data/Machines")))
            {
                Container.Instance.GetInstance<MachineAccessor>(UniqueID)?.InvalidateCaches();
            }
        }

        /// <summary>Provide the manual crops and seed prices as game assets, so content packs can edit them.</summary>
        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo(ManualCropRegistry.ManualCropsAsset))
            {
                e.LoadFromModFile<Dictionary<string, ManualCropDefinition>>("assets/ManualCrops.json", AssetLoadPriority.Exclusive);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo(ManualCropRegistry.SeedPricesAsset))
            {
                e.LoadFromModFile<Dictionary<string, int>>("assets/SeedPrices.json", AssetLoadPriority.Exclusive);
            }
        }

        private void OnGameLaunchedAPIs(object? sender, GameLaunchedEventArgs? e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");

            if (configMenu is null)
            {
                Monitor.Log($"Generic Mod Config Menu not found", LogLevel.Debug);
            }
            else
            {
                Container.Instance.RegisterInstance(configMenu, UniqueID);
            }
        }

        private void OnGameLaunchedAddGenericModConfigMenu(object? sender, GameLaunchedEventArgs? e)
        {
            //register config menu if generic mod config menu is installed
            var configMenu = Container.Instance.GetInstance<IGenericModConfigMenuApi>(UniqueID);
            if (configMenu is null)
            {
                return;
            }
            // register mod
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () =>
                {
                    this.Helper.WriteConfig(this.Config!);
                    ApplyConfig();
                }
            );

            // add keybinding setting
            configMenu.AddKeybind(
                mod: this.ModManifest,
                getValue: () => this.Config?.HotKey ?? SButton.F8,
                setValue: value =>
                {
                    if (this.Config != null)
                    {
                        this.Config.HotKey = value;
                    }
                },
                name: () => (this.Helper.Translation.Get("open") + " " + this.Helper.Translation.Get("app-name")).ToString(),
                tooltip: () => this.Helper.Translation.Get("hot-key-tooltip")
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("tooltip-delay"),
                tooltip: () => this.Helper.Translation.Get("tooltip-delay-desc"),
                getValue: () => this.Config?.ToolTipDelay ?? 30,
                setValue: value =>
                {
                    if (this.Config != null)
                    {
                        this.Config.ToolTipDelay = value;
                    }
                },
                min: 0,
                max: 1000
            );
        }

        #region UI Framework

        /// <summary>Request the UI Framework API and build the screens with it.</summary>
        private void OnGameLaunchedBuildMenus(object? sender, GameLaunchedEventArgs? e)
        {
            uiApi = this.Helper.ModRegistry.GetApi<IStardewUIApi>(UIFrameworkId);
            if (uiApi is null)
            {
                Monitor.Log($"UI Framework ({UIFrameworkId}) is not installed; the calculator cannot open its menus.", LogLevel.Error);
                return;
            }
            // the screens are UI Framework data (assets/ui.json); ProfitCalculatorDataUI supplies what only C# computes
            dataUI = new ProfitCalculatorDataUI(uiApi, Helper, Monitor);
            ApplyConfig();
        }

        /// <summary>Push the hotkey and tooltip delay from the config to the framework.</summary>
        private void ApplyConfig()
        {
            if (uiApi is null || dataUI is null)
            {
                return;
            }

            dataUI.BindHotkey((Config?.HotKey ?? SButton.F8).ToString());
            // the legacy delay was counted in frames (60 per second); the framework takes milliseconds
            uiApi.SetTooltipDelay((Config?.ToolTipDelay ?? 30) * 1000 / 60);
        }

        /// <summary>Re-translate the data screens (their text comes from the <c>@t</c> function) and the produce type labels.</summary>
        private void OnLocaleChanged(object? sender, LocaleChangedEventArgs e)
        {
            Container.Instance.GetInstance<MachineAccessor>(UniqueID)?.InvalidateCaches();
            dataUI?.RegisterFunctions();
        }

        #endregion UI Framework

        [EventPriority(EventPriority.Low - 9999)]
        private void OnSaveGameLoaded(object? sender, SaveLoadedEventArgs? e)
        {
            Container.Instance.RegisterInstance<ShopAccessor>(UniqueID);
            Container.Instance.RegisterInstance<MachineAccessor>(UniqueID);
            var CustomBushAPI = Helper.ModRegistry.GetApi<ICustomBushApi>("furyx639.CustomBush");
            if (CustomBushAPI != null)
            {
                Container.Instance.RegisterInstance(CustomBushAPI, UniqueID);
            }

            // take the day, season and money defaults from the save that was just loaded (this screen's settings only)
            dataUI?.Settings.Reset();

            // the plants are shared by all split-screen players: only the main screen (id 0) builds them, a joining screen keeps them
            if (Context.ScreenId != 0)
            {
                return;
            }
            var Calculator = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID);
            if (Calculator is null)
            {
                Monitor.Log("Calculator is null", LogLevel.Error);
                return;
            }
            Calculator.RebuildCrops();
        }
    }
}
