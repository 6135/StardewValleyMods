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
        private IStardewUIApi? ui;
        private IUIMenu? menu;

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
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            ui = Helper.ModRegistry.GetApi<IStardewUIApi>("6135.UIFramework");
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

            // ---- form: label / control rows in a two-column grid ----
            IUIGrid form = api.AddGrid(demo.Root, "form", "auto,*", "auto,auto,auto,auto,auto");
            form.ColumnSpacing = 24;
            form.RowSpacing = 12;

            int row = 0;
            IUILabel nameLabel = api.AddLabel(form, "name.label", () => "Name:");
            nameLabel.Row = row;
            nameLabel.VerticalAlign = UIAlign.Center;
            IUITextInput nameInput = api.AddTextInput(form, "name", () => name, v => name = v);
            nameInput.Row = row;
            nameInput.Column = 1;
            nameInput.MaxLength = 20;
            nameInput.Tooltip = () => "Letters only (validated).";
            nameInput.Validate = v => v.Length == 0 || char.IsLetter(v[^1]) || v[^1] == ' ';
            row++;

            IUILabel dayLabel = api.AddLabel(form, "day.label", () => "Day:");
            dayLabel.Row = row;
            dayLabel.VerticalAlign = UIAlign.Center;
            IUINumberInput dayInput = api.AddNumberInput(form, "day", () => day, v => day = v, 1, 28, 1, true);
            dayInput.Row = row;
            dayInput.Column = 1;
            dayInput.Width = 120;
            dayInput.Tooltip = () => "1-28, Up/Down or wheel to step.";
            row++;

            IUILabel seasonLabel = api.AddLabel(form, "season.label", () => "Season:");
            seasonLabel.Row = row;
            seasonLabel.VerticalAlign = UIAlign.Center;
            IUIDropdown seasonDropdown = api.AddDropdown(form, "season",
                () => new[] { "spring", "summer", "fall", "winter" },
                () => new[] { "Spring", "Summer", "Fall", "Winter" },
                () => season, v => season = v);
            seasonDropdown.Row = row;
            seasonDropdown.Column = 1;
            seasonDropdown.OnValueChanged = e => Monitor.Log($"Season → {e.NewValue}", LogLevel.Debug);
            row++;

            IUILabel seedsLabel = api.AddLabel(form, "seeds.label", () => "Pay for seeds:");
            seedsLabel.Row = row;
            seedsLabel.VerticalAlign = UIAlign.Center;
            IUICheckbox seeds = api.AddCheckbox(form, "seeds", () => payForSeeds, v => payForSeeds = v);
            seeds.Row = row;
            seeds.Column = 1;
            row++;

            IUILabel volumeLabel = api.AddLabel(form, "volume.label", () => $"Volume ({volume:0}):");
            volumeLabel.Row = row;
            volumeLabel.VerticalAlign = UIAlign.Center;
            IUISlider slider = api.AddSlider(form, "volume", () => volume, v => volume = v, 0, 100);
            slider.Row = row;
            slider.Column = 1;
            slider.Step = 5;
            slider.VerticalAlign = UIAlign.Center;

            // ---- a scrollable list of virtualized rows ----
            IUISpacer divider = api.AddSpacer(demo.Root, "divider", 0, 8);
            divider.Line = true;

            IUIList list = api.AddList(demo.Root, "list", 44, 4, () => 30, (index, container) =>
            {
                IUIStack line = api.AddStack(container, $"row{index}", true, 12);
                line.VerticalAlign = UIAlign.Center;
                api.AddImage(line, $"row{index}.icon", Game1.mouseCursors, new Microsoft.Xna.Framework.Rectangle(128 + (index % 4) * 16, 256, 16, 16), 2f);
                api.AddLabel(line, $"row{index}.text", () => $"Item {index + 1}");
            });
            list.Selectable = true;
            list.OnValueChanged = e => selectedRow = e.NewIndex;

            // ---- buttons ----
            IUIStack buttons = api.AddStack(demo.Root, "buttons", true, 16);
            buttons.HorizontalAlign = UIAlign.Center;
            IUIButton ok = api.AddButton(buttons, "ok", () => $"OK ({clicks})", e =>
            {
                clicks++;
                Monitor.Log($"OK: name={name} day={day} season={season} seeds={payForSeeds} volume={volume} row={selectedRow}", LogLevel.Info);
            });
            ok.Tooltip = () => "Enter also triggers this button.";
            IUIButton close = api.AddButton(buttons, "close", () => "Close", _ => demo.Close());
            demo.DefaultButton = ok;

            return demo;
        }
    }
}
