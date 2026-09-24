using ProfitCalculator.main.accessors;
using ProfitCalculator.main.memory;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UIFramework.Api;
using static ProfitCalculator.Utils;

#nullable enable

namespace ProfitCalculator.main.ui
{
    /// <summary>
    /// The data-driven form of the calculator's screens: <c>assets/ui.json</c> holds the main menu and both results
    /// menus as UI Framework data, and this class gives that data what only C# can compute. It exposes the settings
    /// (<c>model.settings</c>), the produce types offered (<c>hook:produceOptions</c>) and the last calculation's rows
    /// (<c>hook:crops</c>), registers the <c>@Calculate</c> / <c>@Reset</c> / <c>@ValidateSettings</c> commands and the
    /// <c>@t</c> / <c>@money</c> / <c>@moneyDay</c> / <c>@percent</c> / <c>@format</c> functions, then imports the file.
    /// </summary>
    public sealed class ProfitCalculatorDataUI
    {
        /// <summary> Menu id of the main screen (an entry of <c>assets/ui.json</c>). </summary>
        public const string MainMenuId = "main";

        /// <summary> Menu id of the raw results screen. </summary>
        public const string ResultsMenuId = "results";

        /// <summary> Menu id of the machine-product results screen. </summary>
        public const string MachineResultsMenuId = "results-machine";

        /// <summary> The definitions, relative to the mod folder. </summary>
        private const string DataFile = "assets/ui.json";

        private readonly IStardewUIApi api;
        private readonly IModHelper helper;
        private readonly IMonitor monitor;
        private readonly ProfitCalculatorSettings settings;

        /// <summary> The last calculation's results, read by the results screens through <c>hook:crops</c>. </summary>
        private readonly List<CropInfo> crops = new();

        /// <summary> A produce type choice as data reads it (<c>row.Id</c>, <c>row.Label</c>). </summary>
        /// <param name="Id"> The produce type id. </param>
        /// <param name="Label"> The translated name shown in the dropdown. </param>
        public sealed record ProduceOption(string Id, string Label);

        /// <summary>
        /// Registers the hooks and imports <c>assets/ui.json</c> (in DEBUG builds the project's source copy, watched, so
        /// saving it updates open menus without rebuilding the mod).
        /// </summary>
        /// <param name="api"> The UI Framework API. </param>
        /// <param name="helper"> The mod helper (translations). </param>
        /// <param name="monitor"> The mod monitor. </param>
        /// <param name="settings"> The values the main screen edits. </param>
        public ProfitCalculatorDataUI(IStardewUIApi api, IModHelper helper, IMonitor monitor, ProfitCalculatorSettings settings)
        {
            this.api = api;
            this.helper = helper;
            this.monitor = monitor;
            this.settings = settings;

            RegisterFunctions();
            api.ExposeModel("settings", settings);
            api.ExposeRows("produceOptions", () => ProduceOptions().Select(option => (object)new ProduceOption(option.Id, option.Label)).ToArray());
            api.ExposeRows("crops", () => crops.Cast<object>().ToArray());
            api.RegisterCommand("Calculate", Calculate);
            api.RegisterCommand("Reset", _ => settings.Reset());
            api.RegisterCommand("ValidateSettings", _ => ValidProduceType());
            ImportDefinitions();
        }

        /// <summary>
        /// Registers the text functions. Also called when the game language changes: registering again refreshes every
        /// data value, so open screens re-translate.
        /// </summary>
        public void RegisterFunctions()
        {
            api.RegisterFunction("t", Translate);
            api.RegisterFunction("money", args => Money(Number(args, 0)));
            api.RegisterFunction("moneyDay", args => MoneyPerDay(Number(args, 0)));
            api.RegisterFunction("percent", args => Percent(Number(args, 0)));
            api.RegisterFunction("format", args => Number(args, 0).ToString(args.Length > 1 ? args[1] : "0.##", CultureInfo.CurrentCulture));
        }

        /// <summary>Toggle the main screen with <paramref name="keybindList"/> (the configured hotkey).</summary>
        /// <param name="keybindList"> The keys, e.g. <c>"F8"</c>. </param>
        public void BindHotkey(string keybindList)
        {
            // the import builds the menu before it returns, and reloads rebuild it in place, so the binding stays valid
            IUIMenu? menu = api.GetMenu(MainMenuId);
            if (menu is null)
            {
                monitor.Log($"The data menu '{MainMenuId}' is not built; check the UI Framework's 'ui_validate' output.", LogLevel.Warn);
                return;
            }
            api.BindToggleHotkey(menu, keybindList);
        }

        #region Definitions

        private void ImportDefinitions()
        {
#if DEBUG
            string source = SourceFile();
            if (File.Exists(source))
            {
                monitor.Log($"Loading the UI from the source copy {source} (watched).", LogLevel.Debug);
                api.ImportDataFile(source, true);
                return;
            }
#endif
            api.ImportDataFile(Path.Combine(helper.DirectoryPath, DataFile), false);
        }

#if DEBUG
        /// <summary>The project's <c>assets/ui.json</c>, located from this source file's compile-time path.</summary>
        private static string SourceFile([CallerFilePath] string path = "")
        {
            string? directory = Path.GetDirectoryName(path);
            return directory is null ? string.Empty : Path.GetFullPath(Path.Combine(directory, "..", "..", "assets", "ui.json"));
        }
#endif

        #endregion Definitions

        #region Commands

        /// <summary>Push the settings to the calculator, retrieve the crop infos and open the matching results screen as a child.</summary>
        private void Calculate(IUIDataCall call)
        {
            Calculator? calculator = Container.Instance.GetInstance<Calculator>(ModEntry.UniqueID);
            if (calculator is null)
            {
                monitor.Log("Calculator is null", LogLevel.Error);
                return;
            }
            ValidProduceType();
            settings.ApplyTo(calculator);
            monitor.Log($"Doing Calculation: day {calculator.Day} {calculator.Season}, produce {calculator.ProduceType}, fertilizer {calculator.FertilizerQuality}, cross season {calculator.CrossSeason}, years {calculator.Years}, heavy tapper {calculator.HeavyTapper}, tree fertilizer {calculator.TreeFertilizer}, base stats {calculator.UseBaseStats}, farming level {calculator.FarmingLevel}, tiller {Game1.player.professions.Contains(Farmer.tiller)}, agriculturist {Game1.player.professions.Contains(Farmer.agriculturist)}", LogLevel.Debug);
            crops.Clear();
            crops.AddRange(calculator.RetrieveCropInfos());

            // Raw and the tree views sell the harvest as is and share the raw screen; the screens' OnOpen resets sort, selection and scroll
            string resultsId = IsSoldRaw(calculator.ProduceType) ? ResultsMenuId : MachineResultsMenuId;
            IUIMenu? results = api.GetMenu(resultsId);
            IUIMenu? parent = call.Menu ?? api.GetMenu(MainMenuId);
            if (results is null || parent is null)
            {
                monitor.Log($"The data menu '{(results is null ? resultsId : MainMenuId)}' is not built; check the UI Framework's 'ui_validate' output.", LogLevel.Warn);
                return;
            }
            results.OpenAsChild(parent);
        }

        private static IReadOnlyList<(string Id, string Label)> ProduceOptions()
        {
            return Container.Instance.GetInstance<MachineAccessor>(ModEntry.UniqueID)?.GetProduceOptions()
                ?? new List<(string Id, string Label)> { (RawProduceType, RawProduceType) };
        }

        /// <summary>Fall back to raw when the selected produce type is no longer offered (for example after loading another save).</summary>
        private void ValidProduceType()
        {
            if (!ProduceOptions().Any(option => option.Id == settings.ProduceType))
            {
                settings.ProduceType = RawProduceType;
            }
        }

        #endregion Commands

        #region Functions

        /// <summary><c>@t(key)</c> or <c>@t(key, token1, value1, ...)</c>: the translation of <c>key</c>.</summary>
        private string Translate(string[] args)
        {
            if (args.Length == 0)
            {
                return string.Empty;
            }
            if (args.Length < 3)
            {
                return helper.Translation.Get(args[0]).ToString();
            }
            Dictionary<string, object> tokens = new();
            for (int i = 1; i + 1 < args.Length; i += 2)
            {
                tokens[args[i]] = args[i + 1];
            }
            return helper.Translation.Get(args[0], tokens).ToString();
        }

        /// <summary>Argument <paramref name="index"/> as a number (data passes numbers as invariant text); 0 when missing or not a number.</summary>
        private static double Number(string[] args, int index)
        {
            return index < args.Length && double.TryParse(args[index], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0;
        }

        private string Money(double value) => $"{Math.Round(value).ToString("0", CultureInfo.CurrentCulture)} {helper.Translation.Get("g")}";

        private string MoneyPerDay(double value) => $"{value.ToString("0.00", CultureInfo.CurrentCulture)} {helper.Translation.Get("g")}/{helper.Translation.Get("day")}";

        private static string Percent(double value) => $"{(value * 100).ToString("0.00", CultureInfo.CurrentCulture)}%";

        #endregion Functions
    }
}
