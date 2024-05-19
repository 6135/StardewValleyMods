using System;
using System.IO;
using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProfitCalculator.menus;
using ProfitCalculator.ui;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Monsters;
using ProfitCalculator.main;
using CropDataExpanded = ProfitCalculator.main.CropDataExpanded;

#nullable enable

namespace ProfitCalculator
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        private ModConfig? Config;
        private ProfitCalculatorMainMenu? mainMenu;

        /// <summary>The mod's calculator functions.</summary>
        public static Calculator Calculator { get; private set; }

        private IModHelper? helper;
        private IGenericModConfigMenuApi? configMenu;

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Calculator = new();
            Monitor.Log($"Helpers initialized", LogLevel.Debug);
            this.helper = helper;

            //read config
            Config = Helper.ReadConfig<ModConfig>();
            if (Config is null || Helper is null)
            {
                return;
            }

            //hook events
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.GameLaunched += OnGameLaunchedAPIs;
            helper.Events.GameLoop.GameLaunched += OnGameLaunchedAddGenericModConfigMenu;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoadedParseCrops;
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
            Utils.ShopAcessor?.ForceRebuildCache();
        }

        private void OnGameLaunchedAPIs(object? sender, GameLaunchedEventArgs? e)
        {
            configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");

            if (configMenu is null)
            {
                Monitor.Log($"Generic Mod Config Menu not found", LogLevel.Debug);
            }

            Utils.Initialize(helper, Monitor);
        }

        private void OnGameLaunchedAddGenericModConfigMenu(object? sender, GameLaunchedEventArgs? e)
        {
            //register config menu if generic mod config menu is installed

            if (configMenu is null)
                return;
            // register mod
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
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
        private void OnSaveLoadedParseCrops(object? sender, SaveLoadedEventArgs? e)
        {
            Utils.BuildAccessors();
            CropBuilder cropParser = new();
            foreach (var crop in cropParser.BuildCrops())
            {
                AddCrop(crop.Key, crop.Value);
            }
        }

        private void OnSaveGameLoaded(object? sender, SaveLoadedEventArgs? e)
        {
            if (Context.IsWorldReady)
                mainMenu = new ProfitCalculatorMainMenu();
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
        /// <param name="crop"> The crop to add. <see cref="CropDataExpanded"/> </param>
        public static void AddCrop(string id, CropDataExpanded crop)
        {
            Calculator?.AddCrop(id, crop);
        }
    }
}