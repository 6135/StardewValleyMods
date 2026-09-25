using System;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using UIFramework.Api;

namespace UIFrameworkExample
{
    /// <summary>The values the demo menu edits, one set per split-screen player (see <see cref="DemoMenu.State"/>).</summary>
    internal sealed class DemoState
    {
        public string Name = "Farmer";
        public double Day = 1;
        public string Season = "spring";
        public bool PayForSeeds = true;
        public double Volume = 50;
        public double Money = 500;
        public double Savings = 1200;
        public bool ShowTips = true;
        public bool PlaySounds;
        public int Clicks;
        public int Greetings;
        public int SelectedRow = -1;
    }

    /// <summary>
    /// The F9 demo menu, built entirely through the C# API: a form with one of every input, a list, a data grid,
    /// composites, rich text, signals and an auto-form, a theme row, buttons and an extension slot, plus a HUD widget.
    /// <para>
    /// Split-screen: the element delegates read <see cref="State"/>, a <see cref="PerScreen{T}"/> value, so each player
    /// sees and edits their own values (the framework calls the delegates in the screen being drawn or updated). The
    /// signals and the auto-form are bound to single objects when the menu is built, so they belong to the main screen.
    /// </para>
    /// </summary>
    internal sealed class DemoMenu
    {
        /// <summary>Global name of the composite this mod defines (convention: mod id + name).</summary>
        private const string MoneyFieldName = "6135.UIFrameworkExample.MoneyField";

        /// <summary>A data composite (v1.7) defined by the "[CP] UI Framework Example" pack's Composites entry, instantiated here from C#.</summary>
        private const string DataMoneyFieldName = "6135.UIFrameworkExample.CP.MoneyField";

        private readonly IStardewUIApi api;
        private readonly ITranslationHelper translations;
        private readonly IMonitor monitor;
        private readonly string modId;

        private readonly PerScreen<DemoState> state = new(() => new DemoState());
        private readonly PerScreen<DemoSettings> settings = new(() => new DemoSettings());

        // the Day / Season inputs of the form, bound to signals by the signals demo
        private IUINumberInput? dayInput;
        private IUIDropdown? seasonDropdown;

        /// <summary>The current screen's values.</summary>
        internal DemoState State => state.Value;

        /// <summary>The current screen's settings object, edited by the auto-form and exposed to data as a model.</summary>
        internal DemoSettings Settings => settings.Value;

        internal IUIMenu Menu { get; }

        internal IUIHud Hud { get; }

        internal DemoMenu(IStardewUIApi api, ITranslationHelper translations, IMonitor monitor, string modId)
        {
            this.api = api;
            this.translations = translations;
            this.monitor = monitor;
            this.modId = modId;

            IUIMenuOptions options = api.CreateMenuOptions();
            options.Title = () => T("demo.title");
            options.Width = 720;
            // the player can resize the window from its grip; narrow it to see the button rows wrap
            options.Resizable = true;
            Menu = api.CreateMenu("demo", options);
            Menu.OnOpen = _ =>
            {
                monitor.Log("Demo menu opened.", LogLevel.Debug);
                AddDataComposite();
            };
            Menu.OnClose = _ => monitor.Log("Demo menu closed.", LogLevel.Debug);

            // built top to bottom, in the order shown
            BuildForm(Menu.Root);
            BuildComposites();
            Divider(api, Menu.Root, "divider");
            BuildList(Menu.Root);
            BuildDataGrid(Menu.Root);
            BuildSignalsDemo();
            BuildThemeRow(Menu.Root);
            BuildButtons();
            BuildRichTooltip();
            BuildSlotDemo();
            Hud = BuildHud();
        }

        private string T(string key) => translations.Get(key).ToString();

        /// <summary>A full-width divider line (also used by <see cref="ItemImageDemo"/>).</summary>
        internal static void Divider(IStardewUIApi api, IUIContainer parent, string id)
        {
            IUISpacer spacer = api.AddSpacer(parent, id, 0, 8);
            spacer.Line = true;
        }

        /// <summary>
        /// Signals and auto-forms (architecture.md §16.2): a computed label that only re-renders when the Day / Season
        /// inputs above change, and a complete Save / Cancel / Undo / Redo form generated from <see cref="DemoSettings"/>.
        /// </summary>
        private void BuildSignalsDemo()
        {
            Divider(api, Menu.Root, "signals.divider");

            // two-way: the inputs now read / write the signals (their original setters still update the state above)
            IUISignal daySignal = api.SignalNumber(State.Day);
            IUISignal seasonSignal = api.Signal(State.Season);
            api.BindNumberInput(dayInput!, daySignal);
            api.BindDropdown(seasonDropdown!, seasonSignal);

            // the computed tracks whatever signals it reads; the label re-flows only when its version changes
            IUIComputed summary = api.Computed(() => $"Day {daySignal.Number:0} of {seasonSignal.Value}");
            IUILabel summaryLabel = api.AddLabel(Menu.Root, "signals.summary", () => string.Empty);
            summaryLabel.Font = UIFont.Dialogue;
            api.BindText(summaryLabel, summary);
            summary.Subscribe(() => monitor.Log($"Summary changed (v{summary.Version}): {summary.Value}", LogLevel.Trace));

            // a form generated from a POCO: attributes drive sections, ranges, choices and tooltips; Ctrl+Z / Ctrl+Y undo / redo
            DemoSettings model = Settings;
            IUIForm form = api.AddForm(Menu.Root, "settings", model);
            form.OnSaved = _ => monitor.Log($"Settings saved: {model.FarmName}, day {model.Day} of {model.Season}, pets={model.Pets}, {model.Difficulty}", LogLevel.Debug);
            form.OnCancelled = _ => monitor.Log("Settings cancelled.", LogLevel.Debug);
            form.OnChanged = f => monitor.Log($"Settings changed (dirty={f.IsDirty}, undo={f.CanUndo}, redo={f.CanRedo}).", LogLevel.Trace);
        }

        /// <summary>
        /// A HUD widget in the top-right corner (hidden until <c>uiex_hud</c>): the OK click counter and the current
        /// day. It is interactive, so it can be dragged (the position is saved with the game) and its button clicked.
        /// The framework draws HUDs only while a save is loaded.
        /// </summary>
        private IUIHud BuildHud()
        {
            IUIHud hud = api.CreateHud("demo-hud");
            hud.Anchor = UIAnchor.TopRight;
            hud.X = -16;
            hud.Y = 16;
            hud.Visible = false;
            hud.Interactive = true;
            hud.Opacity = 0.85f;

            IUIStack lines = api.AddStack(hud.Root, "hud.lines", false, 4);
            api.AddLabel(lines, "hud.clicks", () => $"Clicks: {State.Clicks}");
            api.AddLabel(lines, "hud.day", () => $"{Game1.currentSeason} {Game1.dayOfMonth}, year {Game1.year}");
            api.AddButton(lines, "hud.open", () => T("hud.open"), _ => Menu.Open(false));
            return hud;
        }

        /// <summary>A "Theme" dropdown bound to the framework's theme list: switching it restyles every framework menu (and persists).</summary>
        private void BuildThemeRow(IUIContainer parent)
        {
            IUIStack row = api.AddStack(parent, "theme-row", true, 24);
            row.VerticalAlign = UIAlign.Center;
            row.Sealed = true; // other mods may not edit this row (the CP pack can show its refused decoration of it)
            IUILabel label = api.AddLabel(row, "theme.label", () => "Theme:");
            label.VerticalAlign = UIAlign.Center;
            IUIDropdown theme = api.AddDropdown(row, "theme", api.ListThemes, api.ListThemes, () => api.ActiveTheme, api.SetTheme);
            theme.Tooltip = () => "Content Patcher packs can add themes to Mods/6135.UIFramework/Themes.";
            theme.AccessibleName = () => $"Theme dropdown: {api.ActiveTheme}";
            theme.OnValueChanged = e =>
            {
                monitor.Log($"Theme → {e.NewValue}", LogLevel.Debug);
                api.Announce($"Theme changed to {e.NewValue}");
            };
        }

        /// <summary>Label / control rows in a two-column grid, one of every input type.</summary>
        private void BuildForm(IUIContainer parent)
        {
            IUIGrid form = api.AddGrid(parent, "form", "auto,*", "auto,auto,auto,auto,auto,auto");
            form.ColumnSpacing = 24;
            form.RowSpacing = 12;

            IUITextInput nameInput = api.AddTextInput(form, "name", () => State.Name, v => State.Name = v);
            nameInput.MaxLength = 20;
            nameInput.Tooltip = () => "Letters only (validated).";
            nameInput.Validate = v => v.All(c => char.IsLetter(c) || c == ' ');
            AddFormRow(form, 0, "Name:", nameInput);

            dayInput = api.AddNumberInput(form, "day", () => State.Day, v => State.Day = v, 1, 28, 1, true);
            dayInput.Width = 120;
            dayInput.Tooltip = () => "1-28, Up/Down or wheel to step.";
            AddFormRow(form, 1, "Day:", dayInput);

            seasonDropdown = api.AddDropdown(form, "season",
                () => new[] { "spring", "summer", "fall", "winter" },
                () => new[] { "Spring", "Summer", "Fall", "Winter" },
                () => State.Season, v => State.Season = v);
            seasonDropdown.OnValueChanged = e => monitor.Log($"Season → {e.NewValue}", LogLevel.Debug);
            AddFormRow(form, 2, "Season:", seasonDropdown);

            IUICheckbox seeds = api.AddCheckbox(form, "seeds", () => State.PayForSeeds, v => State.PayForSeeds = v);
            AddFormRow(form, 3, "Pay for seeds:", seeds);

            IUISlider slider = api.AddSlider(form, "volume", () => State.Volume, v => State.Volume = v, 0, 100);
            slider.Step = 5;
            slider.VerticalAlign = UIAlign.Center;
            AddFormRow(form, 4, () => $"Volume ({State.Volume:0}):", slider);

            // a consumer-drawn component: shares the slider's value, click or Left/Right to change it
            IUIElement gauge = api.AddCustom(form, "gauge", new VolumeGauge(() => State.Volume, v => State.Volume = v));
            gauge.Tooltip = () => "Custom component (IUICustomComponent): click to set, Left/Right to nudge.";
            AddFormRow(form, 5, "Gauge:", gauge);
        }

        private void AddFormRow(IUIGrid form, int row, string label, IUIElement control)
        {
            AddFormRow(form, row, () => label, control);
        }

        /// <summary>Put a label in column 0 and <paramref name="control"/> in column 1 of <paramref name="row"/>.</summary>
        private void AddFormRow(IUIGrid form, int row, Func<string> label, IUIElement control)
        {
            IUILabel rowLabel = api.AddLabel(form, control.Id + ".label", label);
            rowLabel.Row = row;
            rowLabel.VerticalAlign = UIAlign.Center;
            control.Row = row;
            control.Column = 1;
        }

        /// <summary>
        /// A rich text header (bold, colored span, clickable link) over a scrollable list of virtualized rows (only the
        /// visible rows exist as elements).
        /// </summary>
        private void BuildList(IUIContainer parent)
        {
            IUILabel header = api.AddLabel(parent, "list.header", () => "[b]Items[/b] — [color=green]30[/color] rows, [link=help]help[/link]");
            header.RichText = true;
            header.OnLink = link => monitor.Log($"Link clicked: {link}", LogLevel.Debug);

            IUIList list = api.AddList(parent, "list", 44, 4, () => 30, (index, container) =>
            {
                IUIStack line = api.AddStack(container, $"row{index}", true, 12);
                line.VerticalAlign = UIAlign.Center;
                api.AddImage(line, $"row{index}.icon", Game1.mouseCursors, new Microsoft.Xna.Framework.Rectangle(128 + ((index % 4) * 16), 256, 16, 16), 2f);
                api.AddLabel(line, $"row{index}.text", () => $"Item {index + 1}");
            });
            list.Selectable = true;
            list.OnValueChanged = e => State.SelectedRow = e.NewIndex;
        }

        /// <summary>A sortable / resizable data grid over generated rows; single select logs the underlying row.</summary>
        private void BuildDataGrid(IUIContainer parent)
        {
            string[] names = { "Parsnip", "Cauliflower", "Potato", "Kale", "Melon", "Blueberry", "Pumpkin", "Cranberries" };
            int count = 40;
            IUIDataGrid grid = api.AddDataGrid(parent, "grid", 40, 5, () => count);
            grid.MarginTop = 8;
            grid.Selectable = true;

            IUIDataGridColumn item = grid.AddColumn("item", () => "Item", "*");
            item.Text = row => $"{names[row % names.Length]} #{row + 1}";
            item.Sortable = true;
            item.Resizable = true;
            item.MinWidth = 120;

            IUIDataGridColumn qty = grid.AddColumn("qty", () => "Qty", "110px");
            qty.Text = row => (((row * 7) % 23) + 1).ToString();
            qty.SortNumber = row => ((row * 7) % 23) + 1;
            qty.Align = UIAlign.End;
            qty.Sortable = true;
            qty.Resizable = true;
            qty.MinWidth = 60;

            IUIDataGridColumn price = grid.AddColumn("price", () => "Price", "140px");
            price.Text = row => $"{((row * 37) % 500) + 25}g";
            price.SortNumber = row => ((row * 37) % 500) + 25;
            price.Align = UIAlign.End;
            price.Sortable = true;
            price.CellTooltip = row => $"Row {row}: {names[row % names.Length]}";

            grid.OnValueChanged = e => monitor.Log($"Grid row selected: {e.NewIndex} (was {e.OldIndex})", LogLevel.Debug);
            grid.OnRowActivated = row => monitor.Log($"Grid row activated: {row}", LogLevel.Debug);
            grid.OnColumnResized = (id, width) => monitor.Log($"Grid column '{id}' resized to {width}px", LogLevel.Debug);
        }

        /// <summary>OK (also the Enter default), About and Close.</summary>
        private void BuildButtons()
        {
            IUIStack buttons = api.AddStack(Menu.Root, "buttons", true, 16);
            buttons.HorizontalAlign = UIAlign.Center;
            buttons.Wrap = true; // a narrow window moves the buttons that do not fit onto another line
            IUIButton ok = api.AddButton(buttons, "ok", () => translations.Get("demo.ok", new { clicks = State.Clicks }).ToString(), _ =>
            {
                DemoState current = State;
                current.Clicks++;
                api.Publish(Menu, "ok"); // contributors (C# Subscribe or a data "On": { "ok": ... }) hear it once each
                monitor.Log($"OK: name={current.Name} day={current.Day} season={current.Season} seeds={current.PayForSeeds} volume={current.Volume} row={current.SelectedRow}", LogLevel.Debug);
                api.ShowToastWithIcon($"OK pressed {current.Clicks} time(s).", Game1.mouseCursors, new Microsoft.Xna.Framework.Rectangle(128, 256, 16, 16), 3000);
            });
            IUIMenu about = BuildAboutMenu();
            api.AddButton(buttons, "about", () => T("demo.about"), _ => about.OpenAsChild(Menu));
            api.AddButton(buttons, "close", () => T("demo.close"), _ => Menu.Close());
            Menu.DefaultButton = ok;
        }

        /// <summary>A second, minimal menu opened from the demo as a child: a line of text and a button that closes it.</summary>
        private IUIMenu BuildAboutMenu()
        {
            IUIMenuOptions options = api.CreateMenuOptions();
            options.Title = () => T("about.title");
            IUIMenu about = api.CreateMenu("about", options);
            api.AddLabel(about.Root, "about.text", () => translations.Get("about.text", new { version = api.ApiVersion }).ToString());
            IUIButton back = api.AddButton(about.Root, "about.back", () => T("about.back"), _ => about.Close());
            back.HorizontalAlign = UIAlign.Center;
            about.DefaultButton = back;
            return about;
        }

        /// <summary>
        /// v1.1 composites: a reusable "money field" (label + number input + "g") defined once under a global name
        /// and instantiated into the form like any element, plus a custom-drawn frame that embeds built-in checkboxes.
        /// </summary>
        private void BuildComposites()
        {
            api.DefineComposite(MoneyFieldName, (host, args) => BuildMoneyField(api, host, args));

            if (Menu.Find("form") is not IUIGrid form)
            {
                return;
            }

            IUICompositeArgs args = api.CreateCompositeArgs();
            args.SetString("label", "Money:");
            args.SetNumberGetter("get", () => State.Money);
            args.SetNumberSetter("set", v => State.Money = v);
            args.SetNumber("max", 99999);
            IUIComposite moneyField = api.AddComposite(form, "money", MoneyFieldName, args);
            moneyField.Row = 6;
            moneyField.ColumnSpan = 2;
            moneyField.Tooltip = () => $"Composite '{moneyField.CompositeName}': value = {moneyField.GetNumber("value"):0}";
            moneyField.Subscribe("changed", () => monitor.Log($"Money → {State.Money:0}g", LogLevel.Debug));

            // a hand-drawn frame (IUICustomComponent) around a column of built-in checkboxes
            IUIElement framed = api.AddCustom(form, "options", new FrameBox(), host =>
            {
                host.SetMargin(16);
                api.AddCheckbox(host, "options.tips", () => State.ShowTips, v => State.ShowTips = v).Label = () => "Show tips";
                api.AddCheckbox(host, "options.sounds", () => State.PlaySounds, v => State.PlaySounds = v).Label = () => "Play sounds";
            });
            framed.Row = 7;
            framed.ColumnSpan = 2;
            framed.Tooltip = () => "AddCustom + build: the frame is drawn by the mod, the checkboxes are built-ins.";
        }

        /// <summary>
        /// A data composite (v1.7) from C#: the "[CP] UI Framework Example" pack defines <see cref="DataMoneyFieldName"/>
        /// in its Composites entry; C# instantiates it with <c>AddComposite</c> like any composite. Its "value" parameter is
        /// two-way: a getter under "value" and its setter under "value.set". Added on the first open where the pack is
        /// loaded (data composites are registered after the game launched); editing the pack's entry and running
        /// <c>patch reload</c> rebuilds this instance in place.
        /// </summary>
        private void AddDataComposite()
        {
            if (Menu.Find("data.money") != null || !api.HasComposite(DataMoneyFieldName))
            {
                return;
            }

            IUICompositeArgs args = api.CreateCompositeArgs();
            args.SetString("label", "Savings (data composite):");
            args.SetNumberGetter("value", () => State.Savings);
            args.SetNumberSetter("value.set", v => State.Savings = v);
            args.SetNumber("max", 50000);
            IUIComposite field = api.AddComposite(Menu.Root, "data.money", DataMoneyFieldName, args);
            field.HorizontalAlign = UIAlign.Center;
            field.Tooltip = () => $"'{field.CompositeName}' is defined in JSON by a content pack; exposed value = {field.GetNumber("value"):0}";
            field.Subscribe("changed", () => monitor.Log($"Savings → {State.Savings:0}g (event from a data composite)", LogLevel.Debug));
        }

        /// <summary>The composite's builder: runs once per instance (and again on <see cref="IUIComposite.Rebuild"/>).</summary>
        private static void BuildMoneyField(IStardewUIApi api, IUICompositeHost host, IUICompositeArgs args)
        {
            Func<double> get = args.GetNumberGetter("get") ?? (() => 0);
            Action<double> set = args.GetNumberSetter("set") ?? (_ => { });
            double max = args.Has("max") ? args.GetNumber("max") : 1000;

            IUIStack row = api.AddStack(host, host.Id + ".row", true, 8);
            row.Alignment = UIAlign.Center;
            api.AddLabel(row, host.Id + ".caption", () => args.GetString("label"));
            IUINumberInput input = api.AddNumberInput(row, host.Id + ".input", get, set, 0, max, 10, true);
            input.Width = 160;
            input.OnValueChanged = _ => host.Publish("changed");
            api.AddLabel(row, host.Id + ".suffix", () => "g");

            host.ExposeNumber("value", get);
            host.ExposeCommand("reset", () => set(0));
        }

        /// <summary>
        /// Extension slots (v2): the demo menu declares a footer slot (the last row) and shares two values and a command;
        /// then this same mod contributes to that slot exactly the way another mod would (a contributor only sees what the
        /// owner exposed through <see cref="IUIScreenContext"/>). Contributions are rebuilt every time the menu opens.
        /// </summary>
        private void BuildSlotDemo()
        {
            IUISlot footer = api.AddSlot(Menu.Root, "demo.footer");
            footer.Horizontal = true;
            // contributions (and the items inside each) flow onto further lines instead of squeezing one row
            footer.Wrap = true;
            footer.MaxContributions = 4;
            api.Expose(Menu, "name", () => State.Name);
            api.ExposeNumber(Menu, "volume", () => State.Volume);
            api.ExposeCommand(Menu, "log", () => monitor.Log($"Command 'log' invoked from the footer slot: name={State.Name} volume={State.Volume}", LogLevel.Debug));

            api.ContributeTo(modId, "demo", "demo.footer", (slot, ctx) =>
            {
                // the footer wraps: the label sits beside the button when it fits, otherwise it moves to a line of its own and wraps there
                api.AddButton(slot, "footer.log", () => "Log", _ => ctx.Invoke("log"));
                IUILabel info = api.AddLabel(slot, "footer.info", () => $"Contributed: name: {ctx.GetString("name")}, volume: {ctx.GetNumber("volume"):0}");
                info.Wrap = true;
                info.VerticalAlign = UIAlign.Center;
                info.Tooltip = () => $"Values exposed by {ctx.OwnerModId}/{ctx.MenuId}: {string.Join(", ", ctx.Keys)}";
            });

            foreach (IUISlotInfo slot in api.ListSlots(modId))
            {
                monitor.Log($"Slot {slot.OwnerModId}/{slot.MenuId}/{slot.SlotId} ({(slot.Horizontal ? "row" : "column")}).", LogLevel.Debug);
            }
        }

        /// <summary>A rich tooltip (title, parsnip item row, money, colored line) on the OK button.</summary>
        private void BuildRichTooltip()
        {
            IUIElement ok = Menu.Find("ok");
            ok.RichTooltip = api.CreateTooltip()
                .Title(() => "Submit")
                .Line(() => "Enter also triggers this button.")
                .Divider()
                .Item("(O)24")
                .Money(() => 35 * (State.Clicks + 1))
                .Line(() => $"Clicked [b]{State.Clicks}[/b] times", Microsoft.Xna.Framework.Color.DarkGreen)
                .MaxWidth(360);
        }
    }
}
