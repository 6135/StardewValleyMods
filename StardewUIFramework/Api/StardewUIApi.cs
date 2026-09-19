using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using UIFramework.Components;
using UIFramework.Core;
using UIFramework.Hosting;
using UIFramework.Rendering;

namespace UIFramework.Api
{
    /// <summary>
    /// The per-consumer facade handed out by <see cref="ModEntry.GetApi(IModInfo)"/>. Validates arguments, keeps ids
    /// namespaced to the consumer, and creates the internal element classes behind the public interfaces.
    /// </summary>
    public sealed class StardewUIApi : IStardewUIApi
    {
        public static string Version => "1.0.0";

        private readonly ConsumerContext consumer;
        private readonly MenuRegistry menus;
        private readonly HotkeyService hotkeys;

        internal StardewUIApi(ConsumerContext consumer, MenuRegistry menus, HotkeyService hotkeys)
        {
            this.consumer = consumer;
            this.menus = menus;
            this.hotkeys = hotkeys;
        }

        public string ApiVersion => Version;

        // ---------------------------------------------------------------------------------------------------------
        //  Menus
        // ---------------------------------------------------------------------------------------------------------

        public IUIMenuOptions CreateMenuOptions() => new UIMenuOptions();

        public IUIMenu CreateMenu(string id) => CreateMenu(id, new UIMenuOptions());

        public IUIMenu CreateMenu(string id, IUIMenuOptions options)
        {
            RequireId(id);
            options ??= new UIMenuOptions();

            UIMenu? existing = menus.Get(consumer.ModId, id);
            if (existing != null)
            {
                UIServices.Log($"[{consumer.ModId}] menu '{id}' already exists; it is replaced.", LogLevel.Debug);
                menus.Unregister(consumer.ModId, id);
            }

            var menu = new UIMenu(id, consumer, menus)
            {
                TitleFunc = options.Title,
                Width = options.Width,
                Height = options.Height,
                ShowCloseButton = options.ShowCloseButton,
                Modal = options.Modal,
                DimBackground = options.DimBackground,
                Anchor = options.Anchor,
                X = options.X,
                Y = options.Y,
                DrawBox = options.DrawBox,
                Padding = options.Padding,
                CloseOnEscape = options.CloseOnEscape
            };
            menus.Register(consumer.ModId, menu);
            return menu;
        }

        public IUIMenu GetMenu(string id) => menus.Get(consumer.ModId, id ?? string.Empty)!;

        public void DestroyMenu(string id) => menus.Unregister(consumer.ModId, id ?? string.Empty);

        public void OpenMenu(string id) => menus.Get(consumer.ModId, id ?? string.Empty)?.Open(false);

        public void CloseMenu(string id) => menus.Get(consumer.ModId, id ?? string.Empty)?.Close();

        public bool IsOpen(string id) => menus.Get(consumer.ModId, id ?? string.Empty)?.IsOpen ?? false;

        // ---------------------------------------------------------------------------------------------------------
        //  Containers
        // ---------------------------------------------------------------------------------------------------------

        public IUIStack AddStack(IUIContainer parent, string id, bool horizontal, int spacing)
        {
            return Attach(parent, new Stack(RequireId(id), horizontal, spacing));
        }

        public IUIGrid AddGrid(IUIContainer parent, string id, string columns, string rows)
        {
            return Attach(parent, new Grid(RequireId(id), columns ?? "*", rows ?? "auto"));
        }

        public IUIPanel AddPanel(IUIContainer parent, string id, bool drawBox, int padding)
        {
            return Attach(parent, new Panel(RequireId(id), drawBox, padding));
        }

        public IUICanvas AddCanvas(IUIContainer parent, string id)
        {
            return Attach(parent, new Canvas(RequireId(id)));
        }

        public IUIScrollView AddScrollView(IUIContainer parent, string id, int viewportHeight)
        {
            return Attach(parent, new ScrollView(RequireId(id), viewportHeight));
        }

        public IUIList AddList(IUIContainer parent, string id, int rowHeight, int visibleRows, Func<int> itemCount, Action<int, IUIContainer> buildRow)
        {
            ArgumentNullException.ThrowIfNull(itemCount);

            ArgumentNullException.ThrowIfNull(buildRow);

            return Attach(parent, new ListView(RequireId(id), rowHeight, visibleRows, itemCount, buildRow));
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Leaves
        // ---------------------------------------------------------------------------------------------------------

        public IUILabel AddLabel(IUIContainer parent, string id, Func<string> text)
        {
            return Attach(parent, new Label(RequireId(id), text ?? (() => string.Empty)));
        }

        public IUIImage AddImage(IUIContainer parent, string id, Texture2D texture, Rectangle? source, float scale)
        {
            return Attach(parent, new Image(RequireId(id), texture, source, scale));
        }

        public IUIButton AddButton(IUIContainer parent, string id, Func<string> text, Action<IUIClickEvent> onClick)
        {
            return Attach(parent, new Button(RequireId(id), text, onClick));
        }

        public IUICheckbox AddCheckbox(IUIContainer parent, string id, Func<bool> get, Action<bool> set)
        {
            return Attach(parent, new Checkbox(RequireId(id), get, set));
        }

        public IUITextInput AddTextInput(IUIContainer parent, string id, Func<string> get, Action<string> set)
        {
            return Attach(parent, new TextInput(RequireId(id), get, set));
        }

        public IUINumberInput AddNumberInput(IUIContainer parent, string id, Func<double> get, Action<double> set, double min, double max, double step, bool clamp)
        {
            if (max < min)
            {
                (min, max) = (max, min);
            }

            return Attach(parent, new NumberInput(RequireId(id), get, set, min, max, step, clamp));
        }

        public IUIDropdown AddDropdown(IUIContainer parent, string id, Func<string[]> choices, Func<string[]> labels, Func<string> get, Action<string> set)
        {
            ArgumentNullException.ThrowIfNull(choices);

            return Attach(parent, new Dropdown(RequireId(id), choices, labels, get, set));
        }

        public IUISlider AddSlider(IUIContainer parent, string id, Func<double> get, Action<double> set, double min, double max)
        {
            if (max < min)
            {
                (min, max) = (max, min);
            }

            return Attach(parent, new Slider(RequireId(id), get, set, min, max));
        }

        public IUISpacer AddSpacer(IUIContainer parent, string id, int width, int height)
        {
            return Attach(parent, new Spacer(RequireId(id), Math.Max(0, width), Math.Max(0, height)));
        }

        public IUIElement AddCustom(IUIContainer parent, string id, IUICustomComponent implementation)
        {
            ArgumentNullException.ThrowIfNull(implementation);

            return Attach(parent, new CustomElementAdapter(RequireId(id), implementation));
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Lookup / tree
        // ---------------------------------------------------------------------------------------------------------

        public IUIElement Find(IUIMenu menu, string id)
        {
            if (menu is not UIMenu m)
            {
                throw new ArgumentException("The menu was not created by this framework.", nameof(menu));
            }

            return m.Root.FindById(id ?? string.Empty)!;
        }

        public void Remove(IUIElement element)
        {
            UIElement e = UIContainer.Unwrap(element);
            e.ParentElement?.Remove(e);
        }

        public void InvalidateLayout(IUIMenu menu)
        {
            if (menu is UIMenu m)
            {
                m.InvalidateLayout();
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Input
        // ---------------------------------------------------------------------------------------------------------

        public void RegisterHotkey(string id, string keybindList, Action onPressed)
        {
            RequireId(id);
            ArgumentNullException.ThrowIfNull(onPressed);

            hotkeys.Register(consumer, id, keybindList, onPressed);
        }

        public void UnregisterHotkey(string id) => hotkeys.Unregister(consumer, id ?? string.Empty);

        public void BindToggleHotkey(IUIMenu menu, string keybindList)
        {
            if (menu is not UIMenu m)
            {
                throw new ArgumentException("The menu was not created by this framework.", nameof(menu));
            }

            string id = "__toggle:" + m.Id;
            if (string.IsNullOrWhiteSpace(keybindList))
            {
                hotkeys.Unregister(consumer, id);
                return;
            }
            hotkeys.Register(consumer, id, keybindList, () =>
            {
                if (m.IsOpen)
                {
                    m.Close();
                }
                else
                {
                    m.Open(false);
                }
            });
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Style / config
        // ---------------------------------------------------------------------------------------------------------

        public void SetTooltipDelay(int milliseconds) => consumer.TooltipDelayMs = milliseconds < 0 ? null : milliseconds;

        public IUIStyle CreateStyle() => new UIStyle();

        public void SetDefaultStyle(IUIStyle style) => consumer.DefaultStyle = style as UIStyle;

        // ---------------------------------------------------------------------------------------------------------
        //  v1.1 features (one region per feature; see architecture.md §16)
        // ---------------------------------------------------------------------------------------------------------

        // BEGIN SLOTS facade
        // END SLOTS facade

        // BEGIN COMPOSITES facade
        // END COMPOSITES facade

        // BEGIN RICHTEXT facade
        // END RICHTEXT facade

        // BEGIN THEME facade

        public string[] ListThemes() => Theme.ThemeNames;

        public string ActiveTheme => Theme.ActiveName;

        public void SetTheme(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A theme name is required.", nameof(name));
            }

            ThemeSwitcher.Apply(name, consumer.ModId);
        }

        public Color ThemeColor(string key)
        {
            return (key ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "disabled-text" => Theme.DisabledTextColor,
                "hover" => Theme.HoverColor,
                "scrollbar" => Theme.ScrollbarTint,
                "border" => Theme.BorderColor,
                _ => Theme.TextColor
            };
        }

        public bool ReducedMotion => Theme.ReducedMotion;

        public void Announce(string text) => Accessibility.Announce(text);

        // END THEME facade

        // BEGIN DATAGRID facade
        // END DATAGRID facade

        // BEGIN SIGNALS facade
        // END SIGNALS facade

        // BEGIN HUD facade
        // END HUD facade

        // BEGIN TOOLS facade
        // END TOOLS facade

        // ---------------------------------------------------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------------------------------------------------

        private static string RequireId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("An element / menu id is required.", nameof(id));
            }

            return id;
        }

        private T Attach<T>(IUIContainer parent, T element) where T : UIElement
        {
            ArgumentNullException.ThrowIfNull(parent);

            if (parent is not UIContainer container)
            {
                throw new ArgumentException("The parent container was not created by this framework.", nameof(parent));
            }

            if (container.OwnerMenu != null && container.OwnerMenu.Consumer != consumer)
            {
                throw new InvalidOperationException($"'{container.Id}' belongs to another mod's menu.");
            }

            if (container.OwnerMenu != null && container.OwnerMenu.Root.FindById(element.Id) != null)
            {
                UIServices.Log($"[{consumer.ModId}] element id '{element.Id}' is already used in menu '{container.OwnerMenu.Id}'; Find() will return the first one.", LogLevel.Debug);
            }

            container.Add(element);
            return element;
        }
    }
}
