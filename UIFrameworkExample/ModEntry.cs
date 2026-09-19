using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using UIFramework.Api;

namespace UIFrameworkExample
{
    /// <summary>
    /// Example consumer of the UI Framework. It compiles against the copied <c>Api/IStardewUIApi.cs</c> only
    /// (no reference to the framework assembly) and builds a small settings-style screen: press F9 in-game.
    /// </summary>
    public class ModEntry : Mod
    {
        private IUIMenu? menu;
        private IUIHud? hud;

        // state edited by the form (values live here, the framework reads/writes them through delegates)
        private string name = "Farmer";
        private double day = 1;
        private string season = "spring";
        private bool payForSeeds = true;
        private double volume = 50;
        private int clicks;
        private int selectedRow = -1;

        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.ConsoleCommands.Add("ui_demo", "Open the UI Framework example menu.", (_, _) => menu?.Open(true));
            helper.ConsoleCommands.Add("ui_hud", "Toggle the UI Framework example HUD widget.", (_, _) =>
            {
                if (hud != null)
                {
                    hud.Visible = !hud.Visible;
                }
            });
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            IStardewUIApi? ui = Helper.ModRegistry.GetApi<IStardewUIApi>("6135.UIFramework");
            if (ui == null)
            {
                Monitor.Log("UI Framework (6135.UIFramework) is not installed; the example menu is unavailable.", LogLevel.Warn);
                return;
            }
            Monitor.Log($"UI Framework API version {ui.ApiVersion}.", LogLevel.Info);

            menu = BuildMenu(ui);
            ui.BindToggleHotkey(menu, "F9");
        }

        private IUIMenu BuildMenu(IStardewUIApi api)
        {
            IUIMenuOptions options = api.CreateMenuOptions();
            options.Title = () => "UI Framework demo";
            options.Width = 720;
            IUIMenu demo = api.CreateMenu("demo", options);
            demo.OnOpen = _ => Monitor.Log("Demo menu opened.", LogLevel.Debug);
            demo.OnClose = _ => Monitor.Log("Demo menu closed.", LogLevel.Debug);

            BuildForm(api, demo.Root);

            IUISpacer divider = api.AddSpacer(demo.Root, "divider", 0, 8);
            divider.Line = true;

            BuildList(api, demo.Root);
            BuildButtons(api, demo);
            BuildHud(api);
            return demo;
        }

        /// <summary>
        /// A HUD widget in the top-right corner (hidden until <c>ui_hud</c>): the OK click counter and the current
        /// day. It is interactive, so it can be dragged (the position is saved with the game) and its button clicked.
        /// </summary>
        private void BuildHud(IStardewUIApi api)
        {
            hud = api.CreateHud("demo-hud");
            hud.Anchor = UIAnchor.TopRight;
            hud.X = -16;
            hud.Y = 16;
            hud.Visible = false;
            hud.Interactive = true;
            hud.Opacity = 0.85f;
            hud.ShowWhen = () => Context.IsWorldReady;

            IUIStack lines = api.AddStack(hud.Root, "hud.lines", false, 4);
            api.AddLabel(lines, "hud.clicks", () => $"Clicks: {clicks}");
            api.AddLabel(lines, "hud.day", () => $"{Game1.currentSeason} {Game1.dayOfMonth}, year {Game1.year}");
            api.AddButton(lines, "hud.open", () => "Open demo", _ => menu?.Open(false));
        }

        /// <summary>Label / control rows in a two-column grid, one of every input type.</summary>
        private void BuildForm(IStardewUIApi api, IUIContainer parent)
        {
            IUIGrid form = api.AddGrid(parent, "form", "auto,*", "auto,auto,auto,auto,auto,auto");
            form.ColumnSpacing = 24;
            form.RowSpacing = 12;

            IUITextInput nameInput = api.AddTextInput(form, "name", () => name, v => name = v);
            nameInput.MaxLength = 20;
            nameInput.Tooltip = () => "Letters only (validated).";
            nameInput.Validate = v => v.Length == 0 || char.IsLetter(v[^1]) || v[^1] == ' ';
            AddFormRow(api, form, 0, "Name:", nameInput);

            IUINumberInput dayInput = api.AddNumberInput(form, "day", () => day, v => day = v, 1, 28, 1, true);
            dayInput.Width = 120;
            dayInput.Tooltip = () => "1-28, Up/Down or wheel to step.";
            AddFormRow(api, form, 1, "Day:", dayInput);

            IUIDropdown seasonDropdown = api.AddDropdown(form, "season",
                () => new[] { "spring", "summer", "fall", "winter" },
                () => new[] { "Spring", "Summer", "Fall", "Winter" },
                () => season, v => season = v);
            seasonDropdown.OnValueChanged = e => Monitor.Log($"Season → {e.NewValue}", LogLevel.Debug);
            AddFormRow(api, form, 2, "Season:", seasonDropdown);

            IUICheckbox seeds = api.AddCheckbox(form, "seeds", () => payForSeeds, v => payForSeeds = v);
            AddFormRow(api, form, 3, "Pay for seeds:", seeds);

            IUISlider slider = api.AddSlider(form, "volume", () => volume, v => volume = v, 0, 100);
            slider.Step = 5;
            slider.VerticalAlign = UIAlign.Center;
            AddFormRow(api, form, 4, () => $"Volume ({volume:0}):", slider);

            // a consumer-drawn component: shares the slider's value, click or Left/Right to change it
            IUIElement gauge = api.AddCustom(form, "gauge", new VolumeGauge(() => volume, v => volume = v));
            gauge.Tooltip = () => "Custom component (IUICustomComponent): click to set, Left/Right to nudge.";
            AddFormRow(api, form, 5, "Gauge:", gauge);
        }

        private static void AddFormRow(IStardewUIApi api, IUIGrid form, int row, string label, IUIElement control)
        {
            AddFormRow(api, form, row, () => label, control);
        }

        /// <summary>Put a label in column 0 and <paramref name="control"/> in column 1 of <paramref name="row"/>.</summary>
        private static void AddFormRow(IStardewUIApi api, IUIGrid form, int row, Func<string> label, IUIElement control)
        {
            IUILabel rowLabel = api.AddLabel(form, control.Id + ".label", label);
            rowLabel.Row = row;
            rowLabel.VerticalAlign = UIAlign.Center;
            control.Row = row;
            control.Column = 1;
        }

        /// <summary>A scrollable list of virtualized rows (only the visible rows exist as elements).</summary>
        private void BuildList(IStardewUIApi api, IUIContainer parent)
        {
            IUIList list = api.AddList(parent, "list", 44, 4, () => 30, (index, container) =>
            {
                IUIStack line = api.AddStack(container, $"row{index}", true, 12);
                line.VerticalAlign = UIAlign.Center;
                api.AddImage(line, $"row{index}.icon", Game1.mouseCursors, new Microsoft.Xna.Framework.Rectangle(128 + ((index % 4) * 16), 256, 16, 16), 2f);
                api.AddLabel(line, $"row{index}.text", () => $"Item {index + 1}");
            });
            list.Selectable = true;
            list.OnValueChanged = e => selectedRow = e.NewIndex;
        }

        /// <summary>OK (also the Enter default) and Close.</summary>
        private void BuildButtons(IStardewUIApi api, IUIMenu demo)
        {
            IUIStack buttons = api.AddStack(demo.Root, "buttons", true, 16);
            buttons.HorizontalAlign = UIAlign.Center;
            IUIButton ok = api.AddButton(buttons, "ok", () => $"OK ({clicks})", _ =>
            {
                clicks++;
                Monitor.Log($"OK: name={name} day={day} season={season} seeds={payForSeeds} volume={volume} row={selectedRow}", LogLevel.Info);
                api.ShowToastWithIcon($"OK pressed {clicks} time(s).", Game1.mouseCursors, new Microsoft.Xna.Framework.Rectangle(128, 256, 16, 16), 3000);
            });
            ok.Tooltip = () => "Enter also triggers this button.";
            api.AddButton(buttons, "close", () => "Close", _ => demo.Close());
            demo.DefaultButton = ok;
        }
    }
}
