using Microsoft.Xna.Framework.Graphics;
using ProfitCalculator.main.memory;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.IO;
using UIFramework.Api;
using static ProfitCalculator.Utils;

#nullable enable

namespace ProfitCalculator.main.ui
{
    /// <summary>
    /// The main menu for the profit calculator, built through the UI Framework. It is opened with the configured
    /// hotkey ("F8" by default) and edits a <see cref="ProfitCalculatorSettings"/> through a two-column form (label /
    /// control per row). The Calculate button (also bound to Enter) runs the calculation and opens the
    /// <see cref="ProfitCalculatorResultsMenu"/> as a child; Reset restores the defaults.
    /// </summary>
    public sealed class ProfitCalculatorMainMenu
    {
        /// <summary> Menu id, private to this mod inside the framework. </summary>
        public const string MenuId = "main";

        private static readonly int MenuWidth = 632 + (IClickableMenu.borderWidth * 2);
        private static readonly int ButtonHeight = Game1.tileSize;
        private static readonly int ButtonSpacing = Game1.tileSize / 4;

        private readonly IStardewUIApi api;
        private readonly IModHelper helper;
        private readonly IMonitor monitor;
        private readonly ProfitCalculatorSettings settings;
        private readonly ProfitCalculatorResultsMenu results;
        private readonly Texture2D inputTexture;

        /// <summary> The framework menu handle (open / close / hotkey binding). </summary>
        public IUIMenu Menu { get; }

        /// <summary>
        /// Builds the main screen.
        /// </summary>
        /// <param name="api"> The UI Framework API. </param>
        /// <param name="helper"> The mod helper (translations, content). </param>
        /// <param name="monitor"> The mod monitor. </param>
        /// <param name="settings"> The values the form edits. </param>
        /// <param name="results"> The results screen opened by Calculate. </param>
        public ProfitCalculatorMainMenu(IStardewUIApi api, IModHelper helper, IMonitor monitor, ProfitCalculatorSettings settings, ProfitCalculatorResultsMenu results)
        {
            this.api = api;
            this.helper = helper;
            this.monitor = monitor;
            this.settings = settings;
            this.results = results;
            inputTexture = helper.ModContent.Load<Texture2D>(Path.Combine("assets", "text_box_small.png"));

            IUIMenuOptions options = api.CreateMenuOptions();
            options.Title = () => helper.Translation.Get("app-name");
            options.Width = MenuWidth;
            Menu = api.CreateMenu(MenuId, options);
            BuildForm(Menu.Root);
            BuildButtons(Menu);
        }

        #region Form

        /// <summary>One label / control row per setting.</summary>
        private void BuildForm(IUIContainer parent)
        {
            IUIGrid form = api.AddGrid(parent, "form", "auto,*", "auto,auto,auto,auto,auto,auto,auto,auto");
            form.ColumnSpacing = Game1.tileSize / 2;
            form.RowSpacing = Game1.tileSize / 4;

            AddFormRow(form, 0, "day", BuildDayInput(form));
            AddFormRow(form, 1, "Season", BuildSeasonDropdown(form));
            AddFormRow(form, 2, "produce-type", BuildProduceTypeDropdown(form));
            AddFormRow(form, 3, "fertilizer-type", BuildFertilizerDropdown(form));
            AddFormRow(form, 4, "pay-for-seeds", api.AddCheckbox(form, "payForSeeds", () => settings.PayForSeeds, v => settings.PayForSeeds = v));
            AddFormRow(form, 5, "pay-for-fertilizer", api.AddCheckbox(form, "payForFertilizer", () => settings.PayForFertilizer, v => settings.PayForFertilizer = v));
            AddFormRow(form, 6, "max-money", BuildMaxMoneyInput(form));
            AddFormRow(form, 7, "base-stats", api.AddCheckbox(form, "useBaseStats", () => settings.UseBaseStats, v => settings.UseBaseStats = v));
        }

        /// <summary>Put a translated label in column 0 and <paramref name="control"/> in column 1 of <paramref name="row"/>.</summary>
        private void AddFormRow(IUIGrid form, int row, string translationKey, IUIElement control)
        {
            IUILabel label = api.AddLabel(form, control.Id + ".label", () => helper.Translation.Get(translationKey) + ": ");
            label.Font = UIFont.Dialogue;
            label.Row = row;
            label.VerticalAlign = UIAlign.Center;
            control.Row = row;
            control.Column = 1;
            control.VerticalAlign = UIAlign.Center;
        }

        private IUINumberInput BuildDayInput(IUIContainer parent)
        {
            IUINumberInput day = api.AddNumberInput(parent, "day", () => settings.Day, v => settings.Day = (uint)v, settings.MinDay, settings.MaxDay, 1, true);
            day.Decimals = 0;
            day.Texture = inputTexture;
            return day;
        }

        private IUINumberInput BuildMaxMoneyInput(IUIContainer parent)
        {
            IUINumberInput money = api.AddNumberInput(parent, "maxMoney", () => settings.MaxMoney, v => settings.MaxMoney = (uint)v, 0, 99999999, 1, true);
            money.Decimals = 0;
            money.Texture = inputTexture;
            // room for eight digits without the text shrinking
            money.Width = Game1.tileSize * 3;
            return money;
        }

        private IUIDropdown BuildSeasonDropdown(IUIContainer parent)
        {
            return AddEnumDropdown<UtilsSeason>(parent, "season", GetAllTranslatedSeasons,
                () => settings.Season.ToString(), v => settings.Season = ParseEnum<UtilsSeason>(v));
        }

        /// <summary>Only raw produce is implemented, so every produce type is still labelled "not implemented".</summary>
        private IUIDropdown BuildProduceTypeDropdown(IUIContainer parent)
        {
            return AddEnumDropdown<ProduceType>(parent, "produceType", () => NotImplemented(Enum.GetNames(typeof(ProduceType)).Length),
                () => settings.ProduceType.ToString(), v => settings.ProduceType = ParseEnum<ProduceType>(v));
        }

        private IUIDropdown BuildFertilizerDropdown(IUIContainer parent)
        {
            return AddEnumDropdown<FertilizerQuality>(parent, "fertilizerQuality", GetAllTranslatedFertilizerQualities,
                () => settings.FertilizerQuality.ToString(), v => settings.FertilizerQuality = ParseEnum<FertilizerQuality>(v));
        }

        /// <summary>A dropdown whose choices are the names of <typeparamref name="TEnum"/>, showing every choice at once.</summary>
        private IUIDropdown AddEnumDropdown<TEnum>(IUIContainer parent, string id, Func<string[]> labels, Func<string> get, Action<string> set) where TEnum : struct, Enum
        {
            string[] names = Enum.GetNames(typeof(TEnum));
            IUIDropdown dropdown = api.AddDropdown(parent, id, () => names, labels, get, set);
            dropdown.MaxVisible = names.Length;
            return dropdown;
        }

        private static TEnum ParseEnum<TEnum>(string value) where TEnum : struct, Enum
        {
            return Enum.TryParse(value, true, out TEnum parsed) ? parsed : default;
        }

        private string[] NotImplemented(int count)
        {
            string[] labels = new string[count];
            for (int i = 0; i < count; i++)
            {
                labels[i] = helper.Translation.Get("not-implemented");
            }
            return labels;
        }

        #endregion Form

        #region Buttons

        /// <summary>Calculate (Enter default) and Reset.</summary>
        private void BuildButtons(IUIMenu menu)
        {
            IUIStack buttons = api.AddStack(menu.Root, "buttons", true, ButtonSpacing);
            buttons.MarginTop = Game1.tileSize / 2;

            IUIButton calculate = api.AddButton(buttons, "calculate", () => helper.Translation.Get("calculate"), _ => Calculate());
            calculate.ClickSound = "select";
            calculate.Height = ButtonHeight;

            IUIButton reset = api.AddButton(buttons, "reset", () => helper.Translation.Get("reset"), _ => settings.Reset());
            reset.ClickSound = "dialogueCharacterClose";
            reset.Height = ButtonHeight;

            menu.DefaultButton = calculate;
        }

        /// <summary>Push the settings to the calculator, retrieve the crop infos and open the results as a child menu.</summary>
        private void Calculate()
        {
            Calculator? calculator = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID);
            if (calculator is null)
            {
                monitor.Log("Calculator is null", LogLevel.Error);
                return;
            }
            settings.ApplyTo(calculator);
            monitor.Log("Doing Calculation", LogLevel.Debug);
            List<CropInfo> cropInfos = calculator.RetrieveCropInfos();
            results.Show(cropInfos, Menu);
        }

        #endregion Buttons
    }
}
