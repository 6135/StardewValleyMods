using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Globalization;
using UIFramework.Api;

#nullable enable

namespace ProfitCalculator.main.ui
{
    /// <summary>
    /// The results screen of the profit calculator, built through the UI Framework: a sortable data grid with one row
    /// per crop (sprite + name, total profit, profit per day, harvests) and a rich tooltip per row with the full details
    /// (seed / fertilizer loss, timing, drop counts, quality chances). Opened as a child of the <see cref="ProfitCalculatorMainMenu"/>.
    /// </summary>
    public sealed class ProfitCalculatorResultsMenu
    {
        /// <summary> Menu id, private to this mod inside the framework. </summary>
        public const string MenuId = "results";

        private const int VisibleRows = 8;
        private const int RowHeight = 56;
        /// <summary> Wide enough for the fixed columns (below) plus a 250+ px crop column, the scrollbar and the box insets. </summary>
        private const int MenuWidth = 900;
        /// <summary> Column widths generous enough that no cell text has to shrink to fit. </summary>
        private const string MoneyColumnWidth = "150px";
        private const string MoneyPerDayColumnWidth = "190px";
        private const string NumberColumnWidth = "100px";
        private const float SpriteScale = 3f;

        private static readonly Color ProfitColor = Color.DarkGreen;
        private static readonly Color LossColor = Color.Red;

        private readonly IStardewUIApi api;
        private readonly IModHelper helper;
        private readonly List<CropInfo> crops = new();
        private readonly IUIMenu menu;
        private readonly IUIDataGrid grid;
        private readonly IUILabel emptyLabel;

        /// <summary>
        /// Builds the results screen (empty until <see cref="Show"/> is called).
        /// </summary>
        /// <param name="api"> The UI Framework API. </param>
        /// <param name="helper"> The mod helper (translations). </param>
        public ProfitCalculatorResultsMenu(IStardewUIApi api, IModHelper helper)
        {
            this.api = api;
            this.helper = helper;

            // no title banner: the screen opens on top of the main menu, which already carries it
            IUIMenuOptions options = api.CreateMenuOptions();
            options.Width = MenuWidth;
            menu = api.CreateMenu(MenuId, options);

            emptyLabel = api.AddLabel(menu.Root, "empty", () => helper.Translation.Get("no-results"));
            emptyLabel.Font = UIFont.Dialogue;
            emptyLabel.SetMargin(Game1.tileSize / 2);
            emptyLabel.Visible = false;

            grid = api.AddDataGrid(menu.Root, "crops", RowHeight, VisibleRows, () => crops.Count);
            grid.HorizontalAlign = UIAlign.Stretch;
            grid.Selectable = true;
            grid.RowTooltip = BuildTooltip;
            AddColumns();
        }

        /// <summary>
        /// Replaces the listed crops and opens the screen as a child of <paramref name="parent"/>.
        /// </summary>
        /// <param name="cropInfos"> The calculation results to list (already ordered by profit per day). </param>
        /// <param name="parent"> The (open) main menu. </param>
        public void Show(IReadOnlyList<CropInfo> cropInfos, IUIMenu parent)
        {
            crops.Clear();
            crops.AddRange(cropInfos);
            emptyLabel.Visible = crops.Count == 0;
            grid.Visible = crops.Count > 0;
            grid.ClearSelection();
            grid.Sort("profit-day", true);
            grid.Refresh();
            grid.ScrollToRow(0);
            menu.OpenAsChild(parent);
        }

        #region Columns

        private void AddColumns()
        {
            IUIDataGridColumn name = grid.AddColumn("crop", () => helper.Translation.Get("crop"), "*");
            name.Text = row => crops[row].Crop.DisplayName;
            name.BuildCell = BuildCropCell;
            name.Sortable = true;
            name.MinWidth = 180;

            AddMoneyColumn("profit", "total-p", row => crops[row].TotalProfit, false);
            AddMoneyColumn("profit-day", "total-p-day", row => crops[row].ProfitPerDay, true);
            AddNumberColumn("harvests", "harvest-count", row => crops[row].TotalHarvests, "#{0}");
        }

        /// <summary>A right-aligned money column, drawn green for a gain and red for a loss.</summary>
        private void AddMoneyColumn(string id, string headerKey, Func<int, double> value, bool perDay)
        {
            IUIDataGridColumn column = grid.AddColumn(id, () => helper.Translation.Get(headerKey), perDay ? MoneyPerDayColumnWidth : MoneyColumnWidth);
            column.Text = row => perDay ? MoneyPerDay(value(row)) : Money(value(row));
            column.SortNumber = value;
            column.Sortable = true;
            column.Align = UIAlign.End;
            column.BuildCell = (row, cell) =>
            {
                double amount = value(row);
                // ids hang off the slot cell, not the item: slots are rebuilt one by one on scroll, so item-based ids would collide transiently
                IUILabel label = api.AddLabel(cell, $"{cell.Id}.text", () => perDay ? MoneyPerDay(amount) : Money(amount));
                label.HorizontalAlign = UIAlign.Stretch;
                label.TextAlign = UIAlign.End;
                label.Color = amount < 0 ? LossColor : ProfitColor;
            };
        }

        private void AddNumberColumn(string id, string headerKey, Func<int, double> value, string format)
        {
            IUIDataGridColumn column = grid.AddColumn(id, () => helper.Translation.Get(headerKey), NumberColumnWidth);
            column.Text = row => string.Format(CultureInfo.CurrentCulture, format, value(row));
            column.SortNumber = value;
            column.Sortable = true;
            column.Align = UIAlign.End;
        }

        /// <summary>The crop sprite followed by its name; the sprite carries the row tooltip so hovering it still shows the details.</summary>
        private void BuildCropCell(int row, IUIContainer cell)
        {
            CropInfo info = crops[row];
            IUIStack line = api.AddStack(cell, $"{cell.Id}.line", true, 8);
            line.VerticalAlign = UIAlign.Center;
            IUIImage sprite = api.AddImage(line, $"{cell.Id}.sprite", info.Crop.Sprite.Item1, info.Crop.Sprite.Item2, SpriteScale);
            sprite.VerticalAlign = UIAlign.Center;
            sprite.RichTooltip = BuildTooltip(row);
            IUILabel label = api.AddLabel(line, $"{cell.Id}.name", () => info.Crop.DisplayName);
            label.VerticalAlign = UIAlign.Center;
        }

        #endregion Columns

        #region Tooltip

        /// <summary>The crop's money figures, timing, drop counts and quality chances.</summary>
        private IUITooltip BuildTooltip(int row)
        {
            CropInfo info = crops[row];
            IUITooltip tip = api.CreateTooltip().Title(() => info.Crop.DisplayName);
            if (info.Crop.Seed != null)
            {
                tip.Item(info.Crop.Seed.QualifiedItemId);
            }

            tip.Line(() => Detail("total-p", Money(info.TotalProfit)), info.TotalProfit < 0 ? LossColor : ProfitColor)
               .Line(() => Detail("total-p-day", MoneyPerDay(info.ProfitPerDay)), info.ProfitPerDay < 0 ? LossColor : ProfitColor)
               .Line(() => Detail("total-s-loss", Money(info.TotalSeedLoss)))
               .Line(() => Detail("total-s-loss-day", MoneyPerDay(info.SeedLossPerDay)));
            if (info.TotalFertilizerLoss != 0)
            {
                tip.Line(() => Detail("total-f-loss", Money(info.TotalFertilizerLoss)))
                   .Line(() => Detail("total-f-loss-day", MoneyPerDay(info.FertilizerLossPerDay)));
            }

            tip.Divider()
               .Line(() => Detail("grow-time", $"{info.GrowthTime} {helper.Translation.Get("days")}"))
               .Line(() => Detail("regrow-time", info.RegrowthTime > 0 ? $"{info.RegrowthTime} {helper.Translation.Get("days")}" : helper.Translation.Get("no")))
               .Line(() => Detail("harvest-count", $"#{info.TotalHarvests}"))
               .Line(() => Detail("duration", $"{info.Duration} {helper.Translation.Get("days")}"));

            tip.Divider()
               .Line(() => Detail("min-harvests", $"#{info.Crop.MinHarvests}"))
               .Line(() => Detail("max-harvests", $"#{info.Crop.MaxHarvests}"));
            if (info.Crop.MaxHarvestIncreasePerFarmingLevel != 0)
            {
                tip.Line(() => Detail("max-harvests-level", $"#{info.Crop.MaxHarvestIncreasePerFarmingLevel}"));
            }
            if (info.Crop.ChanceForExtraCrops != 0)
            {
                tip.Line(() => Detail("extra-harvest-chance", Percent(info.Crop.ChanceForExtraCrops)));
            }

            tip.Divider();
            if (info.ChanceOfNormalQuality != 0)
            {
                tip.Line(() => Detail("value-normal", Percent(info.ChanceOfNormalQuality)));
            }
            tip.Line(() => Detail("value-silver", Percent(info.ChanceOfSilverQuality)))
               .Line(() => Detail("value-gold", Percent(info.ChanceOfGoldQuality)));
            if (info.ChanceOfIridiumQuality != 0)
            {
                tip.Line(() => Detail("value-iridium", Percent(info.ChanceOfIridiumQuality)));
            }
            return tip.MaxWidth(420);
        }

        private string Detail(string key, string value) => $"{helper.Translation.Get(key)}: {value}";

        #endregion Tooltip

        private string Money(double value) => $"{Math.Round(value).ToString("0", CultureInfo.CurrentCulture)} {helper.Translation.Get("g")}";

        private string MoneyPerDay(double value) => $"{value.ToString("0.00", CultureInfo.CurrentCulture)} {helper.Translation.Get("g")}/{helper.Translation.Get("day")}";

        private static string Percent(double value) => $"{(value * 100).ToString("0.00", CultureInfo.CurrentCulture)}%";
    }
}
