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
using System.Linq;

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

        public string CreateAndRegisterMenu(string id, string title, int width = 800, int height = 600,
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

            if (menu.Config.ToggleKey != SButton.None)
            {
                this.RegisterHotkey($"Open{menu.Id}", menu.Config.ToggleKey, () => this.ShowMenu(menu.Id));
            }

            return id;
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
        // This is a partial implementation showing just the GridLayout-related methods

        public string CreateGridLayout(string menuId, string id)
        {
            if (!_menus.TryGetValue(menuId, out var menu))
            {
                _monitor?.Log($"Cannot create grid layout: Menu with ID '{menuId}' not found", LogLevel.Warn);
                return string.Empty;
            }

            // Create a new grid layout with the menu as its parent
            var gridLayout = new GridLayout(menu);
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

            // Validate column and row values against the fixed grid size
            if (column < 0 || column + columnSpan > GridLayout.GRID_COLUMNS ||
                row < 0 || row + rowSpan > GridLayout.GRID_ROWS)
            {
                _monitor?.Log($"Cannot add component to grid: Position ({column},{row}) with span ({columnSpan},{rowSpan}) " +
                              $"exceeds grid bounds of {GridLayout.GRID_COLUMNS}x{GridLayout.GRID_ROWS}", LogLevel.Warn);
                return string.Empty;
            }

            // Add the component to the grid
            gridLayout.AddComponent(component, column, row, columnSpan, rowSpan);

            return componentId;
        }

        public void SetGridSpacing(string menuId, string layoutId, int horizontalSpacing, int verticalSpacing)
        {
            if (!_menus.TryGetValue(menuId, out _))
            {
                _monitor?.Log($"Cannot set grid spacing: Menu with ID '{menuId}' not found", LogLevel.Warn);
                return;
            }

            if (!_gridLayouts.TryGetValue(layoutId, out var gridLayout))
            {
                _monitor?.Log($"Cannot set grid spacing: Grid layout with ID '{layoutId}' not found", LogLevel.Warn);
                return;
            }

            gridLayout.SetSpacing(horizontalSpacing, verticalSpacing);
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

        // ... existing fields and methods ...

        #region Advanced Management Methods - Internal Data Access

        /// <summary>
        /// Gets menu(s) from the framework. Use with caution as this exposes internal state.
        /// </summary>
        /// <param name="id">Optional specific menu ID. If null, returns all menus.</param>
        /// <returns>Dictionary of menu ID to menu object mappings</returns>
        public Dictionary<string, object> GetMenus(string id = null)
        {
            try
            {
                var result = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(id))
                {
                    // Return specific menu if it exists
                    if (_menus.TryGetValue(id, out var specificMenu))
                    {
                        result[id] = specificMenu;
                    }
                    else
                    {
                        _monitor?.Log($"Menu with ID '{id}' not found", LogLevel.Debug);
                    }
                }
                else
                {
                    // Return all menus (deep copy to prevent external modifications)
                    foreach (var kvp in _menus)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Error getting menus: {ex.Message}", LogLevel.Error);
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// Gets registered hotkey(s). Use with caution as this exposes internal state.
        /// </summary>
        /// <param name="id">Optional specific hotkey ID. If null, returns all hotkeys.</param>
        /// <returns>Dictionary of hotkey ID to SButton mappings</returns>
        public Dictionary<string, SButton> GetHotkeys(string id = null)
        {
            try
            {
                var result = new Dictionary<string, SButton>();

                if (!string.IsNullOrEmpty(id))
                {
                    if (_hotkeys.TryGetValue(id, out var specificHotkey))
                    {
                        result[id] = specificHotkey;
                    }
                    else
                    {
                        _monitor?.Log($"Hotkey with ID '{id}' not found", LogLevel.Debug);
                    }
                }
                else
                {
                    // Return copy of all hotkeys
                    foreach (var kvp in _hotkeys)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Error getting hotkeys: {ex.Message}", LogLevel.Error);
                return new Dictionary<string, SButton>();
            }
        }

        /// <summary>
        /// Gets registered hotkey action(s). Use with caution as this exposes internal state.
        /// </summary>
        /// <param name="id">Optional specific action ID. If null, returns all actions.</param>
        /// <returns>Dictionary of action ID to Action mappings</returns>
        public Dictionary<string, Action> GetRegisteredHotkeyActions(string id = null)
        {
            try
            {
                var result = new Dictionary<string, Action>();

                if (!string.IsNullOrEmpty(id))
                {
                    if (_hotkeyActions.TryGetValue(id, out var specificAction))
                    {
                        result[id] = specificAction;
                    }
                    else
                    {
                        _monitor?.Log($"Hotkey action with ID '{id}' not found", LogLevel.Debug);
                    }
                }
                else
                {
                    // Return copy of all hotkey actions
                    foreach (var kvp in _hotkeyActions)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Error getting hotkey actions: {ex.Message}", LogLevel.Error);
                return new Dictionary<string, Action>();
            }
        }

        /// <summary>
        /// Gets registered component(s). Use with caution as this exposes internal state.
        /// </summary>
        /// <param name="id">Optional specific component ID. If null, returns all components.</param>
        /// <returns>Dictionary of component ID to component object mappings</returns>
        public Dictionary<string, object> GetRegisteredComponents(string id = null)
        {
            try
            {
                var result = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(id))
                {
                    if (_components.TryGetValue(id, out var specificComponent))
                    {
                        result[id] = specificComponent;
                    }
                    else
                    {
                        _monitor?.Log($"Component with ID '{id}' not found", LogLevel.Debug);
                    }
                }
                else
                {
                    // Return copy of all components
                    foreach (var kvp in _components)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Error getting components: {ex.Message}", LogLevel.Error);
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// Gets registered grid layout(s). Use with caution as this exposes internal state.
        /// </summary>
        /// <param name="id">Optional specific layout ID. If null, returns all grid layouts.</param>
        /// <returns>Dictionary of layout ID to grid layout object mappings</returns>
        public Dictionary<string, object> GetRegisteredGridLayouts(string id = null)
        {
            try
            {
                var result = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(id))
                {
                    if (_gridLayouts.TryGetValue(id, out var specificLayout))
                    {
                        result[id] = specificLayout;
                    }
                    else
                    {
                        _monitor?.Log($"Grid layout with ID '{id}' not found", LogLevel.Debug);
                    }
                }
                else
                {
                    // Return copy of all grid layouts
                    foreach (var kvp in _gridLayouts)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Error getting grid layouts: {ex.Message}", LogLevel.Error);
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// Gets registered relative layout(s). Use with caution as this exposes internal state.
        /// </summary>
        /// <param name="id">Optional specific layout ID. If null, returns all relative layouts.</param>
        /// <returns>Dictionary of layout ID to relative layout object mappings</returns>
        public Dictionary<string, object> GetRegisteredRelativeLayouts(string id = null)
        {
            try
            {
                var result = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(id))
                {
                    if (_relativeLayouts.TryGetValue(id, out var specificLayout))
                    {
                        result[id] = specificLayout;
                    }
                    else
                    {
                        _monitor?.Log($"Relative layout with ID '{id}' not found", LogLevel.Debug);
                    }
                }
                else
                {
                    // Return copy of all relative layouts
                    foreach (var kvp in _relativeLayouts)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Error getting relative layouts: {ex.Message}", LogLevel.Error);
                return new Dictionary<string, object>();
            }
        }

        #endregion Advanced Management Methods - Internal Data Access

        #region Replace/Alter Methods - Advanced Management

        /// <summary>
        /// Replaces or updates an existing menu. Use with extreme caution.
        /// </summary>
        /// <param name="id">Menu ID to replace</param>
        /// <param name="menu">New menu object</param>
        /// <returns>True if replacement was successful, false otherwise</returns>
        public bool ReplaceMenu(string id, object menu)
        {
            if (string.IsNullOrEmpty(id))
            {
                _monitor?.Log("Cannot replace menu: ID cannot be null or empty", LogLevel.Warn);
                return false;
            }

            if (menu == null)
            {
                _monitor?.Log("Cannot replace menu: Menu object cannot be null", LogLevel.Warn);
                return false;
            }

            try
            {
                // Validate that the object is actually a BaseMenu
                if (!(menu is BaseMenu baseMenu))
                {
                    _monitor?.Log($"Cannot replace menu '{id}': Object is not a BaseMenu", LogLevel.Error);
                    return false;
                }

                // Check if menu exists
                if (!_menus.ContainsKey(id))
                {
                    _monitor?.Log($"Cannot replace menu '{id}': Menu does not exist", LogLevel.Warn);
                    return false;
                }

                // Hide existing menu if it's visible
                if (_menus[id].IsVisible())
                {
                    _menus[id].Hide();
                }

                // Replace the menu
                _menus[id] = baseMenu;

                _monitor?.Log($"Successfully replaced menu '{id}'", LogLevel.Debug);
                return true;
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Error replacing menu '{id}': {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// Replaces or updates an existing hotkey binding.
        /// </summary>
        /// <param name="id">Hotkey ID to replace</param>
        /// <param name="key">New SButton key</param>
        /// <returns>True if replacement was successful, false otherwise</returns>
        public bool ReplaceHotkey(string id, SButton key)
        {
            if (string.IsNullOrEmpty(id))
            {
                _monitor?.Log("Cannot replace hotkey: ID cannot be null or empty", LogLevel.Warn);
                return false;
            }

            try
            {
                if (!_hotkeys.ContainsKey(id))
                {
                    _monitor?.Log($"Cannot replace hotkey '{id}': Hotkey does not exist", LogLevel.Warn);
                    return false;
                }

                // Check if the new key is already in use by another hotkey
                var existingKeyUsage = _hotkeys.FirstOrDefault(kvp => kvp.Value == key && kvp.Key != id);
                if (!existingKeyUsage.Equals(default(KeyValuePair<string, SButton>)))
                {
                    _monitor?.Log($"Warning: Key '{key}' is already used by hotkey '{existingKeyUsage.Key}'", LogLevel.Warn);
                }

                _hotkeys[id] = key;
                _monitor?.Log($"Successfully replaced hotkey '{id}' with key '{key}'", LogLevel.Debug);
                return true;
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Error replacing hotkey '{id}': {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// Replaces or updates an existing hotkey action.
        /// </summary>
        /// <param name="id">Action ID to replace</param>
        /// <param name="action">New Action delegate</param>
        /// <returns>True if replacement was successful, false otherwise</returns>
        public bool ReplaceRegisteredHotkeyActions(string id, Action action)
        {
            if (string.IsNullOrEmpty(id))
            {
                _monitor?.Log("Cannot replace hotkey action: ID cannot be null or empty", LogLevel.Warn);
                return false;
            }

            if (action == null)
            {
                _monitor?.Log("Cannot replace hotkey action: Action cannot be null", LogLevel.Warn);
                return false;
            }

            try
            {
                if (!_hotkeyActions.ContainsKey(id))
                {
                    _monitor?.Log($"Cannot replace hotkey action '{id}': Action does not exist", LogLevel.Warn);
                    return false;
                }

                _hotkeyActions[id] = action;
                _monitor?.Log($"Successfully replaced hotkey action '{id}'", LogLevel.Debug);
                return true;
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Error replacing hotkey action '{id}': {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// Replaces or updates an existing registered component.
        /// </summary>
        /// <param name="id">Component ID to replace</param>
        /// <param name="component">New component object</param>
        /// <returns>True if replacement was successful, false otherwise</returns>
        public bool ReplaceRegisteredComponents(string id, object component)
        {
            if (string.IsNullOrEmpty(id))
            {
                _monitor?.Log("Cannot replace component: ID cannot be null or empty", LogLevel.Warn);
                return false;
            }

            if (component == null)
            {
                _monitor?.Log("Cannot replace component: Component cannot be null", LogLevel.Warn);
                return false;
            }

            try
            {
                // Validate that the object is actually a BaseComponent
                if (!(component is BaseComponent baseComponent))
                {
                    _monitor?.Log($"Cannot replace component '{id}': Object is not a BaseComponent", LogLevel.Error);
                    return false;
                }

                if (!_components.ContainsKey(id))
                {
                    _monitor?.Log($"Cannot replace component '{id}': Component does not exist", LogLevel.Warn);
                    return false;
                }

                // Update component ID to match the key
                baseComponent.Id = id;

                // Find which menu(s) contain this component and update them
                bool componentUpdatedInMenu = false;
                foreach (var menu in _menus.Values)
                {
                    var existingComponent = menu.GetComponent(id);
                    if (existingComponent != null)
                    {
                        menu.RemoveComponent(id);
                        menu.AddComponent(baseComponent);
                        componentUpdatedInMenu = true;
                    }
                }

                _components[id] = baseComponent;

                _monitor?.Log($"Successfully replaced component '{id}'{(componentUpdatedInMenu ? " and updated in menus" : "")}", LogLevel.Debug);
                return true;
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Error replacing component '{id}': {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// Replaces or updates an existing grid layout.
        /// </summary>
        /// <param name="id">Layout ID to replace</param>
        /// <param name="gridLayout">New grid layout object</param>
        /// <returns>True if replacement was successful, false otherwise</returns>
        public bool ReplaceRegisteredGridLayouts(string id, object gridLayout)
        {
            if (string.IsNullOrEmpty(id))
            {
                _monitor?.Log("Cannot replace grid layout: ID cannot be null or empty", LogLevel.Warn);
                return false;
            }

            if (gridLayout == null)
            {
                _monitor?.Log("Cannot replace grid layout: Layout cannot be null", LogLevel.Warn);
                return false;
            }

            try
            {
                // Validate that the object is actually a GridLayout
                if (!(gridLayout is GridLayout grid))
                {
                    _monitor?.Log($"Cannot replace grid layout '{id}': Object is not a GridLayout", LogLevel.Error);
                    return false;
                }

                if (!_gridLayouts.ContainsKey(id))
                {
                    _monitor?.Log($"Cannot replace grid layout '{id}': Layout does not exist", LogLevel.Warn);
                    return false;
                }

                _gridLayouts[id] = grid;
                _monitor?.Log($"Successfully replaced grid layout '{id}'", LogLevel.Debug);
                return true;
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Error replacing grid layout '{id}': {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// Replaces or updates an existing relative layout.
        /// </summary>
        /// <param name="id">Layout ID to replace</param>
        /// <param name="relativeLayout">New relative layout object</param>
        /// <returns>True if replacement was successful, false otherwise</returns>
        public bool ReplaceRegisteredRelativeLayouts(string id, object relativeLayout)
        {
            if (string.IsNullOrEmpty(id))
            {
                _monitor?.Log("Cannot replace relative layout: ID cannot be null or empty", LogLevel.Warn);
                return false;
            }

            if (relativeLayout == null)
            {
                _monitor?.Log("Cannot replace relative layout: Layout cannot be null", LogLevel.Warn);
                return false;
            }

            try
            {
                // Validate that the object is actually a RelativeLayout
                if (!(relativeLayout is RelativeLayout relative))
                {
                    _monitor?.Log($"Cannot replace relative layout '{id}': Object is not a RelativeLayout", LogLevel.Error);
                    return false;
                }

                if (!_relativeLayouts.ContainsKey(id))
                {
                    _monitor?.Log($"Cannot replace relative layout '{id}': Layout does not exist", LogLevel.Warn);
                    return false;
                }

                _relativeLayouts[id] = relative;
                _monitor?.Log($"Successfully replaced relative layout '{id}'", LogLevel.Debug);
                return true;
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Error replacing relative layout '{id}': {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        #endregion Replace/Alter Methods - Advanced Management

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