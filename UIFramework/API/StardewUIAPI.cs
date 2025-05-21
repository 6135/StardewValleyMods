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

        public BaseMenu CreateMenu(string id, MenuConfig config)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException(nameof(id), "Menu ID cannot be null or empty");

            if (_menus.ContainsKey(id))
                throw new ArgumentException($"A menu with ID '{id}' already exists", nameof(id));

            var menu = new BaseMenu(id, config);
            return menu;
        }

        // If ToggleKey is set, it automatically registers it as Open + MenuID
        public void RegisterMenu(BaseMenu menu)
        {
            if (menu == null)
                throw new ArgumentNullException(nameof(menu));

            if (string.IsNullOrEmpty(menu.Id))
                throw new ArgumentException("Menu must have a valid ID", nameof(menu));

            if (_menus.ContainsKey(menu.Id))
                _menus.Remove(menu.Id);

            _menus[menu.Id] = menu;

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

        public Button CreateButton(string id, string text, Vector2 position, Action onClick)
        {
            var button = new Button(id, position, new Vector2(120, 48), text);

            if (onClick != null)
            {
                button.Clicked += (e) => onClick();
            }

            return button;
        }

        public Label CreateLabel(string id, string text, Vector2 position)
        {
            return new Label(id, position, text);
        }

        public TextInput CreateTextInput(string id, Vector2 position, string initialValue, Action<string> onValueChanged)
        {
            var textInput = new TextInput(id, position, new Vector2(200, 40), initialValue);

            if (onValueChanged != null)
            {
                textInput.TextChanged += onValueChanged;
            }

            return textInput;
        }

        // Layout methods

        public GridLayout CreateGridLayout(int columns, int rows, int cellWidth, int cellHeight)
        {
            return new GridLayout(columns, rows, cellWidth, cellHeight);
        }

        public RelativeLayout CreateRelativeLayout()
        {
            return new RelativeLayout();
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

        public void RegisterClickHandler(string componentId, Action<ClickEventArgs> handler)
        {
            if (string.IsNullOrEmpty(componentId) || handler == null)
                return;

            foreach (var menu in _menus.Values)
            {
                var component = menu.GetComponent(componentId) as BaseClickableComponent;
                if (component != null)
                {
                    component.Clicked += handler;
                    break;
                }
            }
        }

        public void RegisterInputHandler(string componentId, Action<InputEventArgs> handler)
        {
            if (string.IsNullOrEmpty(componentId) || handler == null)
                return;

            foreach (var menu in _menus.Values)
            {
                var component = menu.GetComponent(componentId) as BaseInputComponent;
                if (component != null)
                {
                    component.ValueChanged += handler;
                    break;
                }
            }
        }

        // Event handlers

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

            // Handle other registered hotkeys (existing code)
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
                    menu.gameWindowSizeChanged
                        (new Rectangle(
                            0,
                            0,
                            e.OldSize.X,
                            e.OldSize.Y
                        ),
                        new Rectangle(
                            0,
                            0,
                            e.NewSize.X,
                            e.NewSize.Y
                            )
                        );
                }
            }
        }
    }
}