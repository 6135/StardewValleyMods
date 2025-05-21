using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using UIFrameworkExample.API;

namespace UIFrameworkExample
{
    public class ModEntry : Mod
    {
        private IStardewUIAPI _uiApi;
        private string _mainMenuId = "ExampleMod_MainMenu";
        private bool _uiInitialized = false;

        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.DayStarted += OnDayStarted;

            // Register keybind in the config file
            helper.ConsoleCommands.Add("showui", "Shows the example UI", (s, args) => ShowUI());
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // Get API from the UIFramework mod
            _uiApi = Helper.ModRegistry.GetApi<IStardewUIAPI>("6135.UIFramework");

            if (_uiApi == null)
            {
                Monitor.Log("Failed to get UIFramework API. Make sure UIFramework is installed correctly.", LogLevel.Error);
                return;
            }

            Monitor.Log("UIFramework API loaded successfully!", LogLevel.Info);
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            // Initialize UI the first time
            if (_uiApi != null && !_uiInitialized)
            {
                InitializeUI();
                _uiInitialized = true;

                // Register hotkey to show/hide menu
                _uiApi.RegisterHotkey("ToggleExampleMenu", SButton.F8, ShowUI);
            }
        }

        private void InitializeUI()
        {
            try
            {
                // Create a main menu
                _uiApi.CreateMenu(
                    _mainMenuId,
                    "Example UI Framework Menu",
                    width: 600,
                    height: 400,
                    showCloseButton: true,
                    toggleKey: SButton.F8
                );

                // Register the menu with the framework
                _uiApi.RegisterMenu(_mainMenuId);

                // Add a label to the menu
                string welcomeLabelId = _uiApi.CreateLabel(
                    _mainMenuId,
                    "welcomeLabel",
                    "Welcome to the Example UI!",
                    x: 50,
                    y: 50
                );

                // Add a button that does something
                string actionButtonId = _uiApi.CreateButton(
                    _mainMenuId,
                    "actionButton",
                    "Click Me!",
                    x: 50,
                    y: 100,
                    onClick: OnActionButtonClicked
                );

                // Add a text input
                string nameInputId = _uiApi.CreateTextInput(
                    _mainMenuId,
                    "nameInput",
                    x: 50,
                    y: 150,
                    width: 200,
                    height: 40,
                    initialValue: "",
                    onValueChanged: OnNameInputChanged
                );

                // Set tooltips for components
                _uiApi.SetComponentTooltip(_mainMenuId, actionButtonId, "Click this button to perform an action");
                _uiApi.SetComponentTooltip(_mainMenuId, nameInputId, "Enter your name here");

                // Customize button colors
                _uiApi.SetButtonColors(
                    _mainMenuId,
                    actionButtonId,
                    textColor: Color.White,
                    backgroundColor: new Color(75, 105, 175),
                    hoverColor: new Color(100, 130, 200)
                );

                // Add event handlers for components
                _uiApi.RegisterClickHandler(actionButtonId, OnButtonClicked);
                _uiApi.RegisterInputHandler(nameInputId, OnInputChanged);

                // Create a grid layout
                string gridLayoutId = _uiApi.CreateGridLayout(_mainMenuId, "mainGrid");

                // Set grid spacing for better visual separation
                _uiApi.SetGridSpacing(_mainMenuId, gridLayoutId, 10, 10);

                // Add components to the grid
                // The grid is 12x12 cells by default (defined in GridLayout.GRID_COLUMNS and GridLayout.GRID_ROWS)

                // Add buttons to different cells in the grid
                for (int i = 0; i < 3; i++) // Using just the first 3 columns
                {
                    for (int j = 0; j < 3; j++) // And first 3 rows
                    {
                        if (i == 0 && j == 0) continue; // Skip first cell

                        string buttonId = _uiApi.CreateButton(
                            _mainMenuId,
                            $"gridButton_{i}_{j}",
                            $"Button {i},{j}",
                            x: 0, // Position will be set by the grid
                            y: 0,
                            width: 90,
                            height: 40
                        );

                        // Add to grid - based on the API, we specify column, row, columnSpan, rowSpan
                        _uiApi.AddComponentToGrid(_mainMenuId, gridLayoutId, buttonId, i, j);
                    }
                }

                // Add a button that spans multiple cells
                string wideButtonId = _uiApi.CreateButton(
                    _mainMenuId,
                    "wideButton",
                    "Wide Button",
                    x: 0,
                    y: 0,
                    width: 200,
                    height: 40
                );

                // Add to grid spanning 2 columns
                _uiApi.AddComponentToGrid(_mainMenuId, gridLayoutId, wideButtonId, 0, 5, 2, 1);

                // Add a tall button that spans multiple rows
                string tallButtonId = _uiApi.CreateButton(
                    _mainMenuId,
                    "tallButton",
                    "Tall",
                    x: 0,
                    y: 0,
                    width: 80,
                    height: 100
                );

                // Add to grid spanning 2 rows
                _uiApi.AddComponentToGrid(_mainMenuId, gridLayoutId, tallButtonId, 3, 0, 1, 2);

                Monitor.Log("UI Initialized successfully", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Error initializing UI: {ex.Message}", LogLevel.Error);
            }
        }

        public void ShowUI()
        {
            if (_uiApi != null)
            {
                _uiApi.ShowMenu(_mainMenuId);
            }
        }

        private void OnActionButtonClicked()
        {
            Game1.addHUDMessage(new HUDMessage("Button was clicked!", HUDMessage.newQuest_type));
        }

        private void OnNameInputChanged(string newValue)
        {
            Monitor.Log($"Name changed to: {newValue}", LogLevel.Debug);
        }

        private void OnButtonClicked(int x, int y, string button)
        {
            Monitor.Log($"Button clicked at position: ({x}, {y}) with {button} button", LogLevel.Debug);
        }

        private void OnInputChanged(string oldValue, string newValue)
        {
            Monitor.Log($"Input changed from '{oldValue}' to '{newValue}'", LogLevel.Debug);
        }
    }
}