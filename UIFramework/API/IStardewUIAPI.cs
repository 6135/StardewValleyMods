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
using UIFramework.Events;
using UIFramework.Layout;
using UIFramework.Menus;
using Label = UIFramework.Components.Label;

namespace UIFramework.API
{
    internal interface IStardewUIAPI
    {
        // Menu Management
        BaseMenu CreateMenu(string id, MenuConfig config);

        void RegisterMenu(BaseMenu menu);

        void ShowMenu(string menuId);

        void HideMenu(string menuId);

        // Component Creation
        Button CreateButton(string id, string text, Vector2 position, Action onClick);

        Label CreateLabel(string id, string text, Vector2 position);

        TextInput CreateTextInput(string id, Vector2 position, string initialValue, Action<string> onValueChanged);

        NumberInput CreateNumberInput(string id, Vector2 position, int initialValue, int min, int max, Action<int> onValueChanged);

        Checkbox CreateCheckbox(string id, Vector2 position, bool initialValue, Action<bool> onValueChanged);

        Dropdown CreateDropdown(string id, Vector2 position, string[] options, int selectedIndex, Action<int> onSelectionChanged);

        // Layout
        GridLayout CreateGridLayout(int columns, int rows, int cellWidth, int cellHeight);

        RelativeLayout CreateRelativeLayout();

        // Configuration
        void SetGlobalTooltipDelay(int delay);

        void RegisterHotkey(string id, SButton key, Action onPressed);

        // Event Registration
        void RegisterClickHandler(string componentId, Action<ClickEventArgs> handler);

        void RegisterInputHandler(string componentId, Action<InputEventArgs> handler);
    }
}