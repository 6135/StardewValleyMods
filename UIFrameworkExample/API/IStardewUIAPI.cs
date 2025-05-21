using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UIFramework.Components;
using UIFramework.Config;
using UIFramework.Events;
using UIFramework.Layout;
using UIFramework.Menus;
using Label = UIFramework.Components.Label;

namespace UIFrameworkExample.API
{
    internal interface IStardewUIAPI
    {
        // Menu Management
        Object CreateMenu(string id, Object config);

        void RegisterMenu(Object menu);

        void ShowMenu(string menuId);

        void HideMenu(string menuId);

        // Component Creation
        Object CreateButton(string id, string text, Vector2 position, Action onClick);

        Object CreateLabel(string id, string text, Vector2 position);

        Object CreateTextInput(string id, Vector2 position, string initialValue, Action<string> onValueChanged);

        //NumberInput CreateNumberInput(string id, Vector2 position, int initialValue, int min, int max, Action<int> onValueChanged);

        //Checkbox CreateCheckbox(string id, Vector2 position, bool initialValue, Action<bool> onValueChanged);

        //Dropdown CreateDropdown(string id, Vector2 position, string[] options, int selectedIndex, Action<int> onSelectionChanged);

        // Layout
        Object CreateGridLayout(int columns, int rows, int cellWidth, int cellHeight);

        Object CreateRelativeLayout();

        // Configuration
        void SetGlobalTooltipDelay(int delay);

        void RegisterHotkey(string id, SButton key, Action onPressed);

        // Event Registration
        void RegisterClickHandler(string componentId, Action<ClickEventArgs> handler);

        void RegisterInputHandler(string componentId, Action<InputEventArgs> handler);
    }
}