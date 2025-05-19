using Microsoft.Xna.Framework;
using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UIFramework.API;
using UIFramework.Config;

namespace UIFramework.Example
{
    public class SimpleMenuMod : Mod
    {
        private IStardewUIAPI _uiApi;

        public override void Entry(IModHelper helper)
        {
            // Get the API from our UI framework mod
            _uiApi = helper.ModRegistry.GetApi<IStardewUIAPI>("author.StardewUI");

            // Setup configuration
            _uiApi.SetGlobalTooltipDelay(300);
            _uiApi.RegisterHotkey("OpenMainMenu", SButton.F8, OpenMainMenu);

            // Create a menu
            var menuConfig = new MenuConfig
            {
                Title = "My Simple Menu",
                Width = 500,
                Height = 400
            };

            var mainMenu = _uiApi.CreateMenu("MainMenu", menuConfig);

            // Add components
            var layout = _uiApi.CreateGridLayout(2, 4, 200, 50);

            var titleLabel = _uiApi.CreateLabel("TitleLabel", "Simple Menu Example", new Vector2(150, 20));
            mainMenu.AddComponent(titleLabel);

            var nameInput = _uiApi.CreateTextInput("NameInput", new Vector2(0, 0), "", OnNameChanged);
            layout.AddComponent(nameInput, 0, 0, 2, 1);

            //var enableCheckbox = _uiApi.CreateCheckbox("EnableCheckbox", new Vector2(0, 0), true, OnEnableChanged);
            //layout.AddComponent(enableCheckbox, 0, 1);

            //var optionsDropdown = _uiApi.CreateDropdown("OptionsDropdown", new Vector2(0, 0),
            //    new[] { "Option 1", "Option 2", "Option 3" }, 0, OnOptionSelected);
            //layout.AddComponent(optionsDropdown, 1, 1);

            //var countInput = _uiApi.CreateNumberInput("CountInput", new Vector2(0, 0), 1, 0, 100, OnCountChanged);
            //layout.AddComponent(countInput, 0, 2);

            var submitButton = _uiApi.CreateButton("SubmitButton", "Submit", new Vector2(0, 0), OnSubmit);
            layout.AddComponent(submitButton, 1, 3);

            // Register the menu
            _uiApi.RegisterMenu(mainMenu);
        }

        private void OpenMainMenu()
        {
            _uiApi.ShowMenu("MainMenu");
        }

        private void OnNameChanged(string value)
        {
            // Handle name input change
        }

        private void OnEnableChanged(bool value)
        {
            // Handle checkbox change
        }

        private void OnOptionSelected(int index)
        {
            // Handle dropdown selection
        }

        private void OnCountChanged(int value)
        {
            // Handle number input change
        }

        private void OnSubmit()
        {
            // Handle button click
            _uiApi.HideMenu("MainMenu");
        }
    }
}