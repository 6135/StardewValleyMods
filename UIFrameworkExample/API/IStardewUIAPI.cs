using Microsoft.Xna.Framework;
using StardewModdingAPI;
using System;

namespace UIFrameworkExample.API
{
    /// <summary>
    /// Public API interface for interacting with the UIFramework
    /// </summary>
    public interface IStardewUIAPI
    {
        // Menu Management
        string CreateMenu(string id, string title, int width = 800, int height = 600,
            bool draggable = false, bool resizable = false, bool modal = true,
            bool showCloseButton = true, SButton toggleKey = SButton.None);

        void RegisterMenu(string menuId);

        void ShowMenu(string menuId);

        void HideMenu(string menuId);

        // Component Creation
        string CreateButton(string menuId, string id, string text, int x, int y, int width = 120, int height = 48,
            Action onClick = null);

        string CreateLabel(string menuId, string id, string text, int x, int y);

        string CreateTextInput(string menuId, string id, int x, int y, int width = 200, int height = 40,
            string initialValue = "", Action<string> onValueChanged = null);

        // Layout
        string CreateGridLayout(string menuId, string id);

        string AddComponentToGrid(string menuId, string layoutId, string componentId, int column, int row,
            int columnSpan = 1, int rowSpan = 1);

        void SetGridSpacing(string menuId, string layoutId, int horizontalSpacing, int verticalSpacing);

        string CreateRelativeLayout(string menuId, string id);

        string AddComponentToRelativeLayout(string menuId, string layoutId, string componentId,
            string anchorPoint = "TopLeft", int offsetX = 0, int offsetY = 0);

        string AddComponentRelativeToAnother(string menuId, string layoutId, string componentId,
            string relativeToId, string anchorPoint = "TopLeft", int offsetX = 0, int offsetY = 0);

        // Configuration
        void SetGlobalTooltipDelay(int delay);

        void RegisterHotkey(string id, SButton key, Action onPressed);

        // Event Registration
        void RegisterClickHandler(string componentId, Action<int, int, string> handler);

        void RegisterInputHandler(string componentId, Action<string, string> handler);

        // Component customization
        void SetComponentTooltip(string menuId, string componentId, string tooltip);

        void SetButtonColors(string menuId, string buttonId, Color? textColor = null,
            Color? backgroundColor = null, Color? hoverColor = null, Color? pressedColor = null);

        void SetLabelText(string menuId, string labelId, string text);
    }
}