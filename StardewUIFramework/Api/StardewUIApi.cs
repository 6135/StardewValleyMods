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
        public static string Version => "1.8.0";

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
                CloseOnEscape = options.CloseOnEscape,
                PlayerLayout = options.PlayerLayout, // HUD
                Resizable = options.Resizable
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

        public IUIItemImage AddItemImage(IUIContainer parent, string id, Func<StardewValley.Item> item, float scale)
        {
            return Attach(parent, new ItemImage(RequireId(id), item ?? (() => null!), scale));
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
            UIServices.Hooks?.NotifyStructureChanged(); // data composites of that name rebuild
        }

        public bool HasComposite(string name) => composites.Has(name ?? string.Empty);

        public string[] ListComposites() => composites.List();

        public void UndefineComposite(string name)
        {
            if (composites.Undefine(consumer, name ?? string.Empty))
            {
                UIServices.Hooks?.NotifyStructureChanged();
            }
        }

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
            composites.Track(composite);
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
        public IUITooltip CreateTooltip() => new RichTooltip();
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

        public IUIDataGrid AddDataGrid(IUIContainer parent, string id, int rowHeight, int visibleRows, Func<int> rowCount)
        {
            ArgumentNullException.ThrowIfNull(rowCount);

            return Attach(parent, new DataGrid(RequireId(id), rowHeight, visibleRows, rowCount));
        }

        // END DATAGRID facade

        // BEGIN SIGNALS facade

        public IUISignal Signal(string initial) => new Signal(consumer, ReactiveValue.FromText(initial));

        public IUISignal SignalNumber(double initial) => new Signal(consumer, ReactiveValue.FromNumber(initial));

        public IUISignal SignalBool(bool initial) => new Signal(consumer, ReactiveValue.FromFlag(initial));

        public IUIComputed Computed(Func<string> compute)
        {
            ArgumentNullException.ThrowIfNull(compute);

            return new Computed(consumer, () => ReactiveValue.FromText(compute()), ReactiveValue.FromText(string.Empty));
        }

        public IUIComputed ComputedNumber(Func<double> compute)
        {
            ArgumentNullException.ThrowIfNull(compute);

            return new Computed(consumer, () => ReactiveValue.FromNumber(compute()), ReactiveValue.FromNumber(0));
        }

        public IUIComputed ComputedBool(Func<bool> compute)
        {
            ArgumentNullException.ThrowIfNull(compute);

            return new Computed(consumer, () => ReactiveValue.FromFlag(compute()), ReactiveValue.FromFlag(false));
        }

        public void BindText(IUILabel label, IUIComputed source) => BindText(label, RequireReactive(source));

        public void BindTextToSignal(IUILabel label, IUISignal source) => BindText(label, RequireReactive(source));

        private void BindText(IUILabel label, Reactive source)
        {
            Label target = RequireElement<Label>(label);
            consumer.Bindings.Add(target, SignalBindings.TextKind, new TextBinding(target, source));
        }

        public void BindVisible(IUIElement element, IUIComputed source)
        {
            UIElement target = RequireElement<UIElement>(element);
            consumer.Bindings.Add(target, SignalBindings.VisibleKind, new FlagBinding(target, RequireReactive(source), (e, flag) => e.Visible = flag));
        }

        public void BindEnabled(IUIElement element, IUIComputed source)
        {
            UIElement target = RequireElement<UIElement>(element);
            consumer.Bindings.Add(target, SignalBindings.EnabledKind, new FlagBinding(target, RequireReactive(source), (e, flag) => e.Enabled = flag));
        }

        public void BindTextInput(IUITextInput input, IUISignal signal)
        {
            TextInput target = RequireElement<TextInput>(input);
            Signal source = RequireSignal(signal);
            BindValue(target, new ValueBinding<string>(target.BoundGetter, target.BoundSetter, target.Rebind, () => source.Value, v => source.Value = v));
        }

        public void BindNumberInput(IUINumberInput input, IUISignal signal)
        {
            NumberInput target = RequireElement<NumberInput>(input);
            Signal source = RequireSignal(signal);
            BindValue(target, new ValueBinding<double>(target.BoundGetter, target.BoundSetter, target.Rebind, () => source.Number, v => source.Number = v));
        }

        public void BindCheckbox(IUICheckbox input, IUISignal signal)
        {
            Checkbox target = RequireElement<Checkbox>(input);
            Signal source = RequireSignal(signal);
            BindValue(target, new ValueBinding<bool>(target.BoundGetter, target.BoundSetter, target.Rebind, () => source.Flag, v => source.Flag = v));
        }

        public void BindSlider(IUISlider input, IUISignal signal)
        {
            Slider target = RequireElement<Slider>(input);
            Signal source = RequireSignal(signal);
            BindValue(target, new ValueBinding<double>(target.BoundGetter, target.BoundSetter, target.Rebind, () => source.Number, v => source.Number = v));
        }

        public void BindDropdown(IUIDropdown input, IUISignal signal)
        {
            Dropdown target = RequireElement<Dropdown>(input);
            Signal source = RequireSignal(signal);
            BindValue(target, new ValueBinding<string>(target.BoundGetter, target.BoundSetter, target.Rebind, () => source.Value, v => source.Value = v));
        }

        private void BindValue(UIElement target, SignalBinding binding) => consumer.Bindings.Add(target, SignalBindings.ValueKind, binding);

        public void Unbind(IUIElement element) => consumer.Bindings.Drop(RequireElement<UIElement>(element));

        public IUIForm AddForm(IUIContainer parent, string id, object model)
        {
            ArgumentNullException.ThrowIfNull(model);

            return Attach(parent, new AutoForm(RequireId(id), model, consumer));
        }

        /// <summary><see cref="AddForm"/> over an explicit field list (accessor-backed fields) instead of the model's reflected properties.</summary>
        internal IUIForm AddFormInternal(IUIContainer parent, string id, object model, System.Collections.Generic.IReadOnlyList<FormProperty> properties)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(properties);

            return Attach(parent, new AutoForm(RequireId(id), model, properties, consumer));
        }

        private static T RequireElement<T>(IUIElement element) where T : UIElement
        {
            ArgumentNullException.ThrowIfNull(element);

            return element as T ?? throw new ArgumentException("The element was not created by this framework (or is not the expected kind).", nameof(element));
        }

        private static Reactive RequireReactive(object source)
        {
            ArgumentNullException.ThrowIfNull(source);

            return source as Reactive ?? throw new ArgumentException("The signal / computed was not created by this framework.", nameof(source));
        }

        private static Signal RequireSignal(IUISignal signal)
        {
            ArgumentNullException.ThrowIfNull(signal);

            return signal as Signal ?? throw new ArgumentException("The signal was not created by this framework.", nameof(signal));
        }

        // END SIGNALS facade

        // BEGIN HUD facade

        public IUIHud CreateHud(string id)
        {
            RequireId(id);
            return RequireHud().Create(consumer, id);
        }

        public IUIHud GetHud(string id) => RequireHud().Get(consumer.ModId, id ?? string.Empty)!;

        public void DestroyHud(string id) => RequireHud().Destroy(consumer.ModId, id ?? string.Empty);

        public void ShowToast(string text) => ShowToastWithIcon(text, null, null, ToastLayer.DefaultDurationMs);

        public void ShowToast(string text, int durationMs) => ShowToastWithIcon(text, null, null, durationMs);

        public void ShowToastWithIcon(string text, Texture2D? icon, Rectangle? source, int durationMs)
        {
            if (string.IsNullOrEmpty(text) && icon == null)
            {
                return;
            }

            RequireHud().ShowToast(text ?? string.Empty, icon, source, durationMs > 0 ? durationMs : ToastLayer.DefaultDurationMs);
        }

        public void ResetPlayerLayout(IUIMenu menu)
        {
            if (menu is not UIMenu m)
            {
                throw new ArgumentException("The menu was not created by this framework.", nameof(menu));
            }

            UIServices.Layouts?.Reset(m);
        }

        private static HudService RequireHud() => UIServices.Hud ?? throw new InvalidOperationException("The HUD service is not available yet (it is wired in the framework's Entry).");

        // END HUD facade

        // BEGIN DATA facade (v1.6)

        public bool RunAction(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                return false;
            }

            if (!Data.Actions.DataActionRunner.RunSingle(action, Data.DataScope.ForOwner(consumer.ModId), out string error))
            {
                UIServices.Log($"[{consumer.ModId}] RunAction '{action}': {error}", LogLevel.Warn);
                return false;
            }

            return true;
        }

        public void ImportData(string json)
        {
            if (!Data.Bridge.DataImport.Import(consumer.ModId, json, null, out string error))
            {
                UIServices.Log($"[{consumer.ModId}] ImportData: {error}", LogLevel.Error);
                return;
            }

            BuildImported();
        }

        /// <summary>
        /// Build what an import added right away, so the caller can use its menus (<c>GetMenu</c>, <c>BindToggleHotkey</c>)
        /// on the next line. Later reloads (Content Patcher edits, watched files) rebuild them in place: same menu objects.
        /// </summary>
        private static void BuildImported() => UIServices.Data?.Reload();

        public void ImportDataFile(string path, bool watch)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A file path is required.", nameof(path));
            }

            if (!Data.Bridge.DataImport.ImportFile(consumer.ModId, path, watch, out string error))
            {
                UIServices.Log($"[{consumer.ModId}] ImportDataFile: {error}", LogLevel.Error);
                return;
            }

            BuildImported();
        }

        public void RegisterCommand(string name, Action<IUIDataCall> run)
        {
            ArgumentNullException.ThrowIfNull(run);

            RequireHooks().RegisterCommand(consumer, name, run);
        }

        public void UnregisterCommand(string name) => RequireHooks().RegisterCommand(consumer, name, null);

        public void RegisterFunction(string name, Func<string[], string> function)
        {
            ArgumentNullException.ThrowIfNull(function);

            RequireHooks().RegisterFunction(consumer, name, function);
        }

        public IUIDataSource DefineDataSource(string name) => RequireHooks().DefineSource(consumer, name);

        public void ExposeSignal(string name, IUISignal signal) => RequireHooks().ExposeSignal(consumer, name, signal);

        public void ExposeComputed(string name, IUIComputed computed) => RequireHooks().ExposeComputed(consumer, name, computed);

        public void ExposeModel(string name, object model) => RequireHooks().ExposeModel(consumer, name, model);

        public void ExposeRows(string name, Func<object[]> rows) => RequireHooks().ExposeRows(consumer, name, rows);

        public void RegisterDrawHook(string name, Action<SpriteBatch, Rectangle, IUIDataCall> draw)
        {
            ArgumentNullException.ThrowIfNull(draw);

            RequireHooks().RegisterDrawHook(consumer, name, draw);
        }

        public IUISignal DataState(string key)
        {
            if (UIServices.Data == null)
            {
                throw new InvalidOperationException("Data UIs are not available yet (they are wired in the framework's Entry).");
            }

            if (!Data.State.StateAddress.TryParse(key, Data.DataScope.ForOwner(consumer.ModId), allowBare: false, out Data.State.StateAddress address, out string error))
            {
                throw new ArgumentException(error, nameof(key));
            }

            return new Data.Bridge.DataStateSignal(UIServices.Data.State, address);
        }

        private static HookRegistry RequireHooks() => UIServices.Hooks ?? throw new InvalidOperationException("The data hook registry is not available yet (it is wired in the framework's Entry).");

        // END DATA facade

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
