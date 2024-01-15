﻿using System;
using System.IO;
using GenericModConfigMenu;
using JsonAssets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProfitCalculator.menus;
using ProfitCalculator.UI;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Monsters;
using DynamicGameAssets;
using ProfitCalculator.main;

#nullable enable

namespace ProfitCalculator
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        private ModConfig Config;
        private ProfitCalculatorMainMenu mainMenu;
        public static Calculator? Calculator;
        private IModHelper helper;
        private IGenericModConfigMenuApi configMenu;
        private IApi? JApi;
        private IDynamicGameAssetsApi? DApi;
        /*********

        ** Public methods
        *********/

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Monitor.Log($"Helpers initialized", LogLevel.Debug);
            this.helper = helper;

            //read config
            Config = Helper.ReadConfig<ModConfig>();
            //hook events
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.GameLoop.GameLaunched += onGameLaunchedAddGenericModConfigMenu;
            helper.Events.GameLoop.GameLaunched += onGameLaunchedAPIs;
            helper.Events.GameLoop.SaveLoaded += onSaveGameLoaded;
            helper.Events.Input.MouseWheelScrolled += this.OnMouseWheelScrolled;
        }

        /*********
        ** Private methods
        *********/

        private void onGameLaunchedAPIs(object sender, GameLaunchedEventArgs e)
        {
            JApi = Helper.ModRegistry.GetApi<IApi>("spacechase0.JsonAssets");
            configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            DApi = this.Helper.ModRegistry.GetApi<IDynamicGameAssetsApi>("spacechase0.DynamicGameAssets");

            //if not send message to use about it being not found
            if (JApi is null)
            {
                Monitor.Log($"Json Assets not found", LogLevel.Debug);
            }
            if (configMenu is null)
            {
                Monitor.Log($"Generic Mod Config Menu not found", LogLevel.Debug);
            }
            if (DApi is null)
            {
                Monitor.Log($"Dynamic Game Assets not found", LogLevel.Debug);
            }

            Utils.Initialize(helper, Monitor, JApi, DApi);
            Calculator = new();
            //register app to mobile phone if mobile phone mod is installed
            //TODO Maybe make mobile phone mod optional and add an app
        }

        private void onGameLaunchedAddGenericModConfigMenu(object sender, GameLaunchedEventArgs e)
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
                getValue: () => this.Config.HotKey,
                setValue: value => this.Config.HotKey = value,
                name: () => (this.Helper.Translation.Get("open") + " " + this.Helper.Translation.Get("app-name")).ToString(),
                tooltip: () => this.Helper.Translation.Get("hot-key-tooltip")
            );

            configMenu.AddTextOption(
                mod: this.ModManifest,
                name: () => "Example dropdown",
                getValue: () => "Test",
                setValue: (value) => { },
                allowedValues: new string[] { "choice A", "choice B", "choice C" }
            );
            configMenu.AddTextOption(
                mod: this.ModManifest,
                name: () => "Example dropdown",
                getValue: () => "Test",
                setValue: (value) => { },
                allowedValues: new string[] { "choice A", "choice B", "choice C" }
            );
        }

        private void onSaveGameLoaded(object sender, SaveLoadedEventArgs e)
        {
            if (Context.IsWorldReady)
                mainMenu = new ProfitCalculatorMainMenu(Helper, Monitor, Config);
        }

        /// <summary>Raised after the player presses a button on the keyboard, controller, or mouse.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            // ignore if player hasn't loaded a save yet
            if (!Context.IsWorldReady)
                return;

            //check if button pressed is button in config
            if (e.Button == Config.HotKey)
            {
                //open menu if not already open else close
                if (!mainMenu.isProfitCalculatorOpen)
                {
                    mainMenu.isProfitCalculatorOpen = true;
                    mainMenu.updateMenu();
                    Game1.activeClickableMenu = mainMenu;
                    Game1.playSound("bigSelect");
                }
                else
                {
                    mainMenu.isProfitCalculatorOpen = false;
                    mainMenu.updateMenu();
                    DropdownOption.ActiveDropdown = null;
                    Game1.activeClickableMenu = null;
                    Game1.playSound("bigDeSelect");
                }
            }
        }

        private void OnMouseWheelScrolled(object sender, MouseWheelScrolledEventArgs e)
        {
            DropdownOption.ActiveDropdown?.ReceiveScrollWheelAction(e.Delta);
        }
    }
}