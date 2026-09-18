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
        private readonly CompositeRegistry composites;
        private readonly ExtensionRegistry extensions;

        internal StardewUIApi(ConsumerContext consumer, MenuRegistry menus, HotkeyService hotkeys, CompositeRegistry composites, ExtensionRegistry extensions)
        {
            this.consumer = consumer;
            this.menus = menus;
            this.hotkeys = hotkeys;
            this.composites = composites;
            this.extensions = extensions;
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

            // another mod's menu: sealed subtrees are hidden (see Sealing)
            return (m.Consumer.ModId == consumer.ModId ? m.Root.FindById(id ?? string.Empty) : Sealing.FindReachable(m.Root, id ?? string.Empty, consumer))!;
        }

        public void Remove(IUIElement element)
        {
            UIElement e = UIContainer.Unwrap(element);
            RequireWriteAccess(e);
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

        public IUISlot AddSlot(IUIContainer parent, string id)
        {
            return Attach(parent, new Slot(RequireId(id)));
        }

        public IUISlotInfo[] ListSlots(string ownerModId) => extensions.ListSlots(ownerModId ?? string.Empty);

        public void ContributeTo(string ownerModId, string menuId, string slotId, Action<IUIContainer, IUIScreenContext> build)
        {
            ContributeTo(ownerModId, menuId, slotId, 0, build);
        }

        public void ContributeTo(string ownerModId, string menuId, string slotId, int priority, Action<IUIContainer, IUIScreenContext> build)
        {
            ArgumentNullException.ThrowIfNull(build);

            extensions.Contribute(consumer, RequireId(ownerModId), RequireId(menuId), RequireId(slotId), priority, build);
        }

        public void RemoveContribution(string ownerModId, string menuId, string slotId)
        {
            extensions.RemoveContribution(consumer, ownerModId ?? string.Empty, menuId ?? string.Empty, slotId ?? string.Empty);
        }

        public void Expose(IUIMenu menu, string key, Func<string> value)
        {
            extensions.ExposuresOf(RequireOwnMenu(menu)).SetString(RequireId(key), value);
        }

        public void ExposeNumber(IUIMenu menu, string key, Func<double> value)
        {
            extensions.ExposuresOf(RequireOwnMenu(menu)).SetNumber(RequireId(key), value);
        }

        public void ExposeBool(IUIMenu menu, string key, Func<bool> value)
        {
            extensions.ExposuresOf(RequireOwnMenu(menu)).SetBool(RequireId(key), value);
        }

        public void ExposeCommand(IUIMenu menu, string key, Action command)
        {
            extensions.ExposuresOf(RequireOwnMenu(menu)).SetCommand(RequireId(key), command);
        }

        public void Publish(IUIMenu menu, string eventName)
        {
            extensions.ExposuresOf(RequireOwnMenu(menu)).Publish(RequireId(eventName));
        }

        public void OnScreenBuilt(string ownerModId, string menuId, Action<IUIMenu> decorate)
        {
            extensions.SetDecorator(consumer, RequireId(ownerModId), RequireId(menuId), decorate);
        }

        // END SLOTS facade

        // BEGIN COMPOSITES facade

        public IUICompositeArgs CreateCompositeArgs() => new CompositeArgs();

        public void DefineComposite(string name, Action<IUICompositeHost, IUICompositeArgs> build)
        {
            ArgumentNullException.ThrowIfNull(build);

            composites.Define(consumer, RequireId(name), build);
        }

        public bool HasComposite(string name) => composites.Has(name ?? string.Empty);

        public string[] ListComposites() => composites.List();

        public void UndefineComposite(string name) => composites.Undefine(consumer, name ?? string.Empty);

        public IUIComposite AddComposite(IUIContainer parent, string id, string compositeName, IUICompositeArgs args)
        {
            RequireId(compositeName);
            CompositeArgs bag = args switch
            {
                null => new CompositeArgs(),
                CompositeArgs own => own,
                _ => throw new ArgumentException("The arguments were not created by CreateCompositeArgs().", nameof(args))
            };
            Composite composite = Attach(parent, new Composite(RequireId(id), compositeName, bag, composites));
            composite.Build();
            return composite;
        }

        public IUIElement AddCustom(IUIContainer parent, string id, IUICustomComponent implementation, Action<IUIContainer> build)
        {
            ArgumentNullException.ThrowIfNull(implementation);
            ArgumentNullException.ThrowIfNull(build);

            CustomHostAdapter adapter = Attach(parent, new CustomHostAdapter(RequireId(id), implementation));
            adapter.Build(build);
            return adapter;
        }

        /// <summary>True when this consumer defined the composite that <paramref name="container"/> (or an ancestor) hosts.</summary>
        private bool FillsComponentOf(UIContainer container)
        {
            for (UIElement? e = container; e != null; e = e.ParentElement)
            {
                if (e is UIContainer c && c.ComponentOwner?.ModId == consumer.ModId)
                {
                    return true;
                }
            }
            return false;
        }

        // END COMPOSITES facade

        // BEGIN RICHTEXT facade
        // END RICHTEXT facade

        // BEGIN THEME facade
        // END THEME facade

        // BEGIN DATAGRID facade

        public IUIDataGrid AddDataGrid(IUIContainer parent, string id, int rowHeight, int visibleRows, Func<int> rowCount)
        {
            ArgumentNullException.ThrowIfNull(rowCount);

            return Attach(parent, new DataGrid(RequireId(id), rowHeight, visibleRows, rowCount));
        }

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

            if (!FillsComponentOf(container))
            {
                RequireWriteAccess(container);
            }

            if (container.OwnerMenu != null && container.OwnerMenu.Root.FindById(element.Id) != null)
            {
                UIServices.Log($"[{consumer.ModId}] element id '{element.Id}' is already used in menu '{container.OwnerMenu.Id}'; Find() will return the first one.", LogLevel.Debug);
            }

            container.Add(element);
            return element;
        }

        /// <summary>Owner, contributor (inside its container) or decorator (outside sealed subtrees) may edit; see <see cref="Sealing"/>.</summary>
        private void RequireWriteAccess(UIElement element)
        {
            Sealing.RequireWriteAccess(element, consumer, element.OwnerMenu != null && extensions.IsDecorator(consumer, element.OwnerMenu));
        }

        /// <summary>A menu of this consumer, unwrapped.</summary>
        private UIMenu RequireOwnMenu(IUIMenu menu)
        {
            if (menu is not UIMenu m)
            {
                throw new ArgumentException("The menu was not created by this framework.", nameof(menu));
            }

            if (m.Consumer.ModId != consumer.ModId)
            {
                throw new InvalidOperationException($"Menu '{m.Id}' belongs to another mod ({m.Consumer.ModId}).");
            }

            return m;
        }
    }
}
