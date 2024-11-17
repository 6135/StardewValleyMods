using CoreUtils.management.memory;
using ProfitCalculator.apis;
using ProfitCalculator.main;
using ProfitCalculator.main.accessors;
using ProfitCalculator.main.builders;
using ProfitCalculator.main.models;
using ProfitCalculator.main.ui;
using ProfitCalculator.main.ui.menus;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using CropData = ProfitCalculator.main.models.CropData;

#nullable enable

namespace ProfitCalculator
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        private ModConfig? Config;
        private ProfitCalculatorMainMenu? mainMenu;
        internal static readonly string UniqueID = "6135.ProfitCalculator";

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Container.Instance.RegisterInstance<Calculator>(UniqueID);
            Container.Instance.RegisterInstance(helper, UniqueID);
            Container.Instance.RegisterInstance(this.Monitor, UniqueID);

            //read config
            Config = helper.ReadConfig<ModConfig>();
            if (Config is null || Helper is null)
            {
                return;
            }

            //hook events
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.GameLaunched += OnGameLaunchedAPIs;
            helper.Events.GameLoop.GameLaunched += OnGameLaunchedAddGenericModConfigMenu;
            helper.Events.GameLoop.SaveLoaded += OnSaveGameLoaded;
            helper.Events.Input.MouseWheelScrolled += this.OnMouseWheelScrolled;
            helper.Events.GameLoop.DayStarted += OnDayStartedResetCache;
        }

        /*********
        ** Private methods
        *********/

        [EventPriority(EventPriority.Low - 9999)]
        private void OnDayStartedResetCache(object? sender, DayStartedEventArgs? e)
        {
            Container.Instance.GetInstance<ShopAccessor>(ModEntry.UniqueID)?.ForceRebuildCache();
        }

        private void OnGameLaunchedAPIs(object? sender, GameLaunchedEventArgs? e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");

            if (configMenu is null)
            {
                Monitor.Log($"Generic Mod Config Menu not found", LogLevel.Debug);
            }
            Container.Instance.RegisterInstance(configMenu, UniqueID);
        }

        private void OnGameLaunchedAddGenericModConfigMenu(object? sender, GameLaunchedEventArgs? e)
        {
            //register config menu if generic mod config menu is installed
            var configMenu = Container.Instance.GetInstance<IGenericModConfigMenuApi>(UniqueID);
            if (configMenu is null)
                return;
            // register mod
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config!)
            );

            // add keybinding setting
            configMenu.AddKeybind(
                mod: this.ModManifest,
                getValue: () => this.Config?.HotKey ?? SButton.F8,
                setValue: value =>
                {
                    if (this.Config != null)
                        this.Config.HotKey = value;
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
                        this.Config.ToolTipDelay = value;
                },
                min: 0,
                max: 1000
            );
        }

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

            if (Context.IsWorldReady)
            {
                mainMenu = new ProfitCalculatorMainMenu();
            }
            var Calculator = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID);
            if (Calculator is null)
            {
                Monitor.Log("Calculator is null", LogLevel.Error);
                return;
            }
            List<IDataBuilder> builder = new()
            {
                new CropBuilder(),
                new FruitTreeBuilder(),
            };
            if (CustomBushAPI != null)
            {
                builder.Add(new CustomBushBuilder());
            }
            //linq for each builder, call build crops and add to calculator
            builder.ForEach(b =>
            {
                try
                {
                    b.BuildCrops().ToList().ForEach(c => Calculator.AddCrop(c.Key, c.Value));
                }
                catch (NotImplementedException e)
                {
                    Monitor.Log($"Error building crops: {e.Message}", LogLevel.Error);
                }
            }
            );
        }

        /// <summary>Raised after the player presses a button on the keyboard, controller, or mouse.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnButtonPressed(object? sender, ButtonPressedEventArgs? e)
        {
            // ignore if player hasn't loaded a save yet
            if (!Context.IsWorldReady || e == null)
                return;

            //check if button pressed is button in config
            if (e.Button == (Config?.HotKey ?? SButton.None))
            {
                //open menu if not already open else close
                if (mainMenu?.IsProfitCalculatorOpen != null && !mainMenu.IsProfitCalculatorOpen)
                {
                    mainMenu.IsProfitCalculatorOpen = true;
                    mainMenu.UpdateMenu();
                    Game1.activeClickableMenu = mainMenu;
                    Game1.playSound("bigSelect");
                }
                else if (mainMenu?.IsProfitCalculatorOpen != null)
                {
                    mainMenu.IsProfitCalculatorOpen = false;
                    mainMenu.UpdateMenu();
                    DropdownOption.ActiveDropdown = null;
                    Game1.activeClickableMenu = null;
                    Game1.playSound("bigDeSelect");
                }
            }
        }

        private void OnMouseWheelScrolled(object? sender, MouseWheelScrolledEventArgs? e)
        {
            if (e != null)
                DropdownOption.ActiveDropdown?.ReceiveScrollWheelAction(e.Delta);
        }

        /// <summary>
        /// Adds a crop to the Profit Calculator.
        /// </summary>
        /// <param name="id"> The id of the crop. Must be unique.</param>
        /// <param name="crop"> The crop to add. <see cref="CropData"/> </param>
        public static void AddCrop(string id, CropData crop)
        {
            var Calculator = Container.Instance.GetInstance<Calculator>(UniqueID);
            Calculator?.AddCrop(id, crop);
        }
    }
}