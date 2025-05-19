using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using UIFramework.API;
using UIFramework.Components;
using UIFramework.Components.Base;
using UIFramework.Config;
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
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Make API available to other mods
            Monitor.Log("Registering UI Framework API for other mods to use", LogLevel.Info);

            // Register with Generic Mod Config Menu if available
            RegisterWithGenericModConfigMenu();
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            // Initialize API
            if (_api == null)
            {
                _api = (IStardewUIAPI?)this.GetApi();
                if (_api == null)
                {
                    Monitor.Log("Failed to initialize UI Framework API", LogLevel.Error);
                    return;
                }
            }
            // Initialize example UI when a save is loaded
            if (!_exampleInitialized)
            {
                InitializeExampleUI();
                _exampleInitialized = true;
            }
        }

        private void InitializeExampleUI()
        {
            // Create menu configuration
            var menuConfig = new MenuConfig
            {
                Title = "UI Framework Example",
                Width = 600,
                Height = 400,
                ShowCloseButton = true,
                ToggleKey = SButton.F9
            };

            // Create main menu
            var mainMenu = _api!.CreateMenu("ExampleMenu", menuConfig);

            // Create layout
            var gridLayout = _api.CreateGridLayout(2, 5, 250, 50);

            // Add header label
            var headerLabel = _api.CreateLabel("HeaderLabel", "Welcome to UI Framework Demo", new Vector2(200, 50));
            headerLabel.Font = Game1.dialogueFont;
            headerLabel.Scale = 0.8f;
            mainMenu.AddComponent(headerLabel);

            // Create input field with label
            var nameLabel = _api.CreateLabel("NameLabel", "Enter your name:", new Vector2(0, 0));
            gridLayout.AddComponent(nameLabel, 0, 1);

            var nameInput = _api.CreateTextInput("NameInput", new Vector2(0, 0), "", OnNameChanged);
            gridLayout.AddComponent(nameInput, 1, 1);

            // Create description field with label
            var descLabel = _api.CreateLabel("DescLabel", "Enter description:", new Vector2(0, 0));
            gridLayout.AddComponent(descLabel, 0, 2);

            var descInput = _api.CreateTextInput("DescInput", new Vector2(0, 0), "", OnDescriptionChanged);
            gridLayout.AddComponent(descInput, 1, 2);

            // Add result label
            var resultLabel = _api.CreateLabel("ResultLabel", "Results will appear here", new Vector2(0, 0));
            resultLabel.WordWrap = true;
            resultLabel.MaxWidth = 500;
            gridLayout.AddComponent(resultLabel, 0, 3, 2, 1);

            // Add submit button
            var submitButton = _api.CreateButton("SubmitButton", "Submit", new Vector2(0, 0), OnSubmitClicked);
            gridLayout.AddComponent(submitButton, 0, 4);

            // Add close button
            var closeButton = _api.CreateButton("CloseButton", "Close", new Vector2(0, 0), () => _api.HideMenu("ExampleMenu"));
            gridLayout.AddComponent(closeButton, 1, 4);

            // Add all components from the grid layout to the menu
            foreach (var component in gridLayout.GetComponents())
            {
                mainMenu.AddComponent(component);
            }

            // Register menu with the framework
            _api.RegisterMenu(mainMenu);

            // Register hotkey to open menu
            _api.RegisterHotkey("OpenExampleMenu", SButton.F8, () => _api.ShowMenu("ExampleMenu"));

            Monitor.Log("UI Framework example menu initialized. Press F8 to open it.", LogLevel.Info);
        }

        private void OnNameChanged(string newValue)
        {
            UpdateResultLabel();
        }

        private void OnDescriptionChanged(string newValue)
        {
            UpdateResultLabel();
        }

        private void UpdateResultLabel()
        {
            var nameInput = FindComponentById("NameInput") as TextInput;
            var descInput = FindComponentById("DescInput") as TextInput;
            var resultLabel = FindComponentById("ResultLabel") as Label;

            if (nameInput != null && descInput != null && resultLabel != null)
            {
                string name = nameInput.Value;
                string desc = descInput.Value;

                if (!string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(desc))
                {
                    resultLabel.SetText($"Hello {name}! Description: {desc}");
                }
                else
                {
                    resultLabel.SetText("Please enter your information");
                }
            }
        }

        private BaseComponent? FindComponentById(string id)
        {
            if (Game1.activeClickableMenu is Menus.BaseMenu menu)
            {
                return menu.GetComponent(id);
            }
            return null;
        }

        private void OnSubmitClicked()
        {
            var nameInput = FindComponentById("NameInput") as TextInput;
            var descInput = FindComponentById("DescInput") as TextInput;

            if (nameInput != null && descInput != null)
            {
                string message = $"Form submitted with Name: {nameInput.Value}, Description: {descInput.Value}";
                Monitor.Log(message, LogLevel.Info);
                Game1.addHUDMessage(new HUDMessage(message, HUDMessage.newQuest_type));
            }
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