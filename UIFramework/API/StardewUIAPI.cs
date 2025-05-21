using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using UIFramework.Components;
using UIFramework.Components.Base;
using UIFramework.Config;
using UIFramework.Events;
using UIFramework.Layout;
using UIFramework.Menus;
using UIFramework.memory;

namespace UIFramework.API
{
    public class StardewUIAPI : IStardewUIAPI
    {
        private UIConfig _config;
        private readonly Dictionary<string, BaseMenu> _menus = new Dictionary<string, BaseMenu>();
        private readonly Dictionary<string, SButton> _hotkeys = new Dictionary<string, SButton>();
        private readonly Dictionary<string, Action> _hotkeyActions = new Dictionary<string, Action>();
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly LayoutManager _layoutManager = new LayoutManager();

        // Track components by ID for external reference
        private readonly Dictionary<string, BaseComponent> _components = new Dictionary<string, BaseComponent>();

        private readonly Dictionary<string, GridLayout> _gridLayouts = new Dictionary<string, GridLayout>();
        private readonly Dictionary<string, RelativeLayout> _relativeLayouts = new Dictionary<string, RelativeLayout>();

        public StardewUIAPI()
        {
            _helper = Container.Instance.GetInstance<IModHelper>(ModEntry.UniqueID);
            _monitor = Container.Instance.GetInstance<IMonitor>(ModEntry.UniqueID);
            _config = Container.Instance.GetInstance<UIConfig>(ModEntry.UniqueID);

            // Register event handlers
            _helper.Events.Input.ButtonPressed += OnButtonPressed;
            _helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            _helper.Events.Display.WindowResized += OnWindowResized;
        }

        // IStardewUIAPI implementation methods

        public string CreateMenu(string id, string title, int width = 800, int height = 600,
            bool draggable = false, bool resizable = false, bool modal = true,
            bool showCloseButton = true, SButton toggleKey = SButton.None)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException(nameof(id), "Menu ID cannot be null or empty");

            if (_menus.ContainsKey(id))
                throw new ArgumentException($"A menu with ID '{id}' already exists", nameof(id));

            var config = new MenuConfig
            {
                Title = title,
                Width = width,
                Height = height,
                Draggable = draggable,
                Resizable = resizable,
                Modal = modal,
                ShowCloseButton = showCloseButton,
                ToggleKey = toggleKey
            };

            var menu = new BaseMenu(id, config);
            _menus[id] = menu;

            return id;
        }

        public void RegisterMenu(string menuId)
        {
            if (!_menus.TryGetValue(menuId, out var menu))
            {
                _monitor?.Log($"Cannot register menu: Menu with ID '{menuId}' not found", LogLevel.Warn);
                return;
            }

            if (menu.Config.ToggleKey != SButton.None)
            {
                this.RegisterHotkey($"Open{menu.Id}", menu.Config.ToggleKey, () => this.ShowMenu(menu.Id));
            }
        }

        public void ShowMenu(string menuId)
        {
            if (!_menus.TryGetValue(menuId, out var menu))
            {
                _monitor?.Log($"Cannot show menu: Menu with ID '{menuId}' not found", LogLevel.Warn);
                return;
            }

            menu.Show();
        }

        public void HideMenu(string menuId)
        {
            if (!_menus.TryGetValue(menuId, out var menu))
            {
                _monitor?.Log($"Cannot hide menu: Menu with ID '{menuId}' not found", LogLevel.Warn);
                return;
            }

            menu.Hide();
        }

        // Component creation methods

        public string CreateButton(string menuId, string id, string text, int x, int y, int width = 120, int height = 48,
            Action onClick = null)
        {
            if (!_menus.TryGetValue(menuId, out var menu))
            {
                _monitor?.Log($"Cannot create button: Menu with ID '{menuId}' not found", LogLevel.Warn);
                return string.Empty;
            }

            var button = new Button(id, new Vector2(x, y), new Vector2(width, height), text);

            if (onClick != null)
            {
                button.Clicked += (e) => onClick();
            }

            menu.AddComponent(button);
            _components[id] = button;

            return id;
        }

        public string CreateLabel(string menuId, string id, string text, int x, int y)
        {
            if (!_menus.TryGetValue(menuId, out var menu))
            {
                _monitor?.Log($"Cannot create label: Menu with ID '{menuId}' not found", LogLevel.Warn);
                return string.Empty;
            }

            var label = new Label(id, new Vector2(x, y), text);
            menu.AddComponent(label);
            _components[id] = label;

            return id;
        }

        public string CreateTextInput(string menuId, string id, int x, int y, int width = 200, int height = 40,
            string initialValue = "", Action<string> onValueChanged = null)
        {
            if (!_menus.TryGetValue(menuId, out var menu))
            {
                _monitor?.Log($"Cannot create text input: Menu with ID '{menuId}' not found", LogLevel.Warn);
                return string.Empty;
            }

            var textInput = new TextInput(id, new Vector2(x, y), new Vector2(width, height), initialValue);

            if (onValueChanged != null)
            {
                textInput.TextChanged += onValueChanged;
            }

            menu.AddComponent(textInput);
            _components[id] = textInput;

            return id;
        }

        // Layout methods

        public string CreateGridLayout(string menuId, string id, int columns, int rows, int cellWidth, int cellHeight)
        {
            if (!_menus.TryGetValue(menuId, out var menu))
            {
                _monitor?.Log($"Cannot create grid layout: Menu with ID '{menuId}' not found", LogLevel.Warn);
                return string.Empty;
            }

            var gridLayout = new GridLayout(columns, rows, cellWidth, cellHeight);
            _gridLayouts[id] = gridLayout;

            return id;
        }

        public string AddComponentToGrid(string menuId, string layoutId, string componentId, int column, int row,
            int columnSpan = 1, int rowSpan = 1)
        {
            if (!_menus.TryGetValue(menuId, out var menu))
            {
                _monitor?.Log($"Cannot add component to grid: Menu with ID '{menuId}' not found", LogLevel.Warn);
                return string.Empty;
            }

            if (!_gridLayouts.TryGetValue(layoutId, out var gridLayout))
            {
                _monitor?.Log($"Cannot add component to grid: Grid layout with ID '{layoutId}' not found", LogLevel.Warn);
                return string.Empty;
            }

            if (!_components.TryGetValue(componentId, out var component))
            {
                _monitor?.Log($"Cannot add component to grid: Component with ID '{componentId}' not found", LogLevel.Warn);
                return string.Empty;
            }

            gridLayout.AddComponent(component, column, row, columnSpan, rowSpan);
            menu.PositionGridLayout(gridLayout);

            return componentId;
        }

        public string CreateRelativeLayout(string menuId, string id)
        {
            if (!_menus.TryGetValue(menuId, out _))
            {
                _monitor?.Log($"Cannot create relative layout: Menu with ID '{menuId}' not found", LogLevel.Warn);
                return string.Empty;
            }

            var relativeLayout = new RelativeLayout();
            _relativeLayouts[id] = relativeLayout;

            return id;
        }

        public string AddComponentToRelativeLayout(string menuId, string layoutId, string componentId,
            string anchorPoint = "TopLeft", int offsetX = 0, int offsetY = 0)
        {
            if (!_menus.TryGetValue(menuId, out _))
            {
                _monitor?.Log($"Cannot add component to layout: Menu with ID '{menuId}' not found", LogLevel.Warn);
                return string.Empty;
            }

            if (!_relativeLayouts.TryGetValue(layoutId, out var relativeLayout))
            {
                _monitor?.Log($"Cannot add component to layout: Relative layout with ID '{layoutId}' not found", LogLevel.Warn);
                return string.Empty;
            }

            if (!_components.TryGetValue(componentId, out var component))
            {
                _monitor?.Log($"Cannot add component to layout: Component with ID '{componentId}' not found", LogLevel.Warn);
                return string.Empty;
            }

            // Parse anchor point from string
            if (!Enum.TryParse<RelativeLayout.AnchorPoint>(anchorPoint, out var anchor))
            {
                anchor = RelativeLayout.AnchorPoint.TopLeft;
            }

            relativeLayout.AddComponent(component, anchor, new Vector2(offsetX, offsetY));

            return componentId;
        }

        public string AddComponentRelativeToAnother(string menuId, string layoutId, string componentId,
            string relativeToId, string anchorPoint = "TopLeft", int offsetX = 0, int offsetY = 0)
        {
            if (!_menus.TryGetValue(menuId, out _))
            {
                _monitor?.Log($"Cannot add component to layout: Menu with ID '{menuId}' not found", LogLevel.Warn);
                return string.Empty;
            }

            if (!_relativeLayouts.TryGetValue(layoutId, out var relativeLayout))
            {
                _monitor?.Log($"Cannot add component to layout: Relative layout with ID '{layoutId}' not found", LogLevel.Warn);
                return string.Empty;
            }

            if (!_components.TryGetValue(componentId, out var component))
            {
                _monitor?.Log($"Cannot add component to layout: Component with ID '{componentId}' not found", LogLevel.Warn);
                return string.Empty;
            }

            if (!_components.TryGetValue(relativeToId, out var relativeTo))
            {
                _monitor?.Log($"Cannot add component to layout: RelativeTo component with ID '{relativeToId}' not found", LogLevel.Warn);
                return string.Empty;
            }

            // Parse anchor point from string
            if (!Enum.TryParse<RelativeLayout.AnchorPoint>(anchorPoint, out var anchor))
            {
                anchor = RelativeLayout.AnchorPoint.TopLeft;
            }

            relativeLayout.AddComponent(component, relativeTo, anchor, new Vector2(offsetX, offsetY));

            return componentId;
        }

        // Configuration methods

        public void SetGlobalTooltipDelay(int delay)
        {
            _config.DefaultTooltipDelay = delay;
        }

        public void RegisterHotkey(string id, SButton key, Action onPressed)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException(nameof(id));

            if (onPressed == null)
                throw new ArgumentNullException(nameof(onPressed));

            _hotkeys[id] = key;
            _hotkeyActions[id] = onPressed;
        }

        // Event registration methods

        public void RegisterClickHandler(string componentId, Action<int, int, string> handler)
        {
            if (string.IsNullOrEmpty(componentId) || handler == null)
                return;

            if (!_components.TryGetValue(componentId, out var component) || !(component is BaseClickableComponent clickable))
            {
                _monitor?.Log($"Cannot register click handler: Component with ID '{componentId}' not found or is not clickable", LogLevel.Warn);
                return;
            }

            clickable.Clicked += (e) => handler(e.X, e.Y, e.Button.ToString());
        }

        public void RegisterInputHandler(string componentId, Action<string, string> handler)
        {
            if (string.IsNullOrEmpty(componentId) || handler == null)
                return;

            if (!_components.TryGetValue(componentId, out var component) || !(component is BaseInputComponent input))
            {
                _monitor?.Log($"Cannot register input handler: Component with ID '{componentId}' not found or is not an input component", LogLevel.Warn);
                return;
            }

            input.ValueChanged += (e) => handler(e.OldValue, e.NewValue);
        }

        // Component customization

        public void SetComponentTooltip(string menuId, string componentId, string tooltip)
        {
            if (!_menus.TryGetValue(menuId, out var menu))
            {
                _monitor?.Log($"Cannot set tooltip: Menu with ID '{menuId}' not found", LogLevel.Warn);
                return;
            }

            var component = menu.GetComponent(componentId);
            if (component == null)
            {
                _monitor?.Log($"Cannot set tooltip: Component with ID '{componentId}' not found", LogLevel.Warn);
                return;
            }

            component.Tooltip = tooltip;
        }

        public void SetButtonColors(string menuId, string buttonId, Color? textColor = null,
            Color? backgroundColor = null, Color? hoverColor = null, Color? pressedColor = null)
        {
            if (!_menus.TryGetValue(menuId, out var menu))
            {
                _monitor?.Log($"Cannot set button colors: Menu with ID '{menuId}' not found", LogLevel.Warn);
                return;
            }

            var component = menu.GetComponent(buttonId);
            if (component == null || !(component is Button button))
            {
                _monitor?.Log($"Cannot set button colors: Button with ID '{buttonId}' not found", LogLevel.Warn);
                return;
            }

            if (textColor.HasValue)
                button.TextColor = textColor.Value;

            if (backgroundColor.HasValue)
                button.BackgroundColor = backgroundColor.Value;

            if (hoverColor.HasValue)
                button.HoverColor = hoverColor.Value;

            if (pressedColor.HasValue)
                button.PressedColor = pressedColor.Value;
        }

        public void SetLabelText(string menuId, string labelId, string text)
        {
            if (!_menus.TryGetValue(menuId, out var menu))
            {
                _monitor?.Log($"Cannot set label text: Menu with ID '{menuId}' not found", LogLevel.Warn);
                return;
            }

            var component = menu.GetComponent(labelId);
            if (component == null || !(component is Label label))
            {
                _monitor?.Log($"Cannot set label text: Label with ID '{labelId}' not found", LogLevel.Warn);
                return;
            }

            label.SetText(text);
        }

        // Event handlers (unchanged from original)

        private void OnButtonPressed(object sender, StardewModdingAPI.Events.ButtonPressedEventArgs e)
        {
            // Skip if we're not in a world or if a menu is already active
            if (!Context.IsWorldReady)
                return;

            // Check if any registered menu has this key as its toggle key
            foreach (var menu in _menus.Values)
            {
                if (menu.Config.ToggleKey == e.Button)
                {
                    if (menu.IsVisible())
                        menu.Hide();
                    else
                        menu.Show();

                    // Break to prevent multiple menus with the same key from toggling
                    break;
                }
            }

            // Handle other registered hotkeys
            foreach (var entry in _hotkeys)
            {
                if (e.Button == entry.Value && _hotkeyActions.TryGetValue(entry.Key, out var action))
                {
                    action();
                    break;
                }
            }
        }

        private void OnUpdateTicked(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
        {
            // Nothing to do here for now
        }

        private void OnWindowResized(object sender, StardewModdingAPI.Events.WindowResizedEventArgs e)
        {
            // Handle window resize by notifying all menus
            foreach (var menu in _menus.Values)
            {
                if (menu.IsVisible())
                {
                    menu.gameWindowSizeChanged(
                        new Rectangle(0, 0, e.OldSize.X, e.OldSize.Y),
                        new Rectangle(0, 0, e.NewSize.X, e.NewSize.Y)
                    );
                }
            }
        }
    }
}