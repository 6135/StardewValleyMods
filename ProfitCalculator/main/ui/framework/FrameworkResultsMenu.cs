using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Globalization;
using UIFramework.Api;

#nullable enable

namespace ProfitCalculator.main.ui.framework
{
    /// <summary>
    /// The Profit Calculator results screen built through the UI Framework API (v1.1): a sortable data grid with one
    /// row per crop (sprite + name, profit, profit per day, seed / fertilizer loss, harvests, duration) and a rich
    /// tooltip per row with the details the legacy <see cref="CropHoverBox"/> showed. Replaces the legacy
    /// <see cref="menus.ProfitCalculatorResultsList"/>; nothing from <c>main/ui</c> is used.
    /// </summary>
    internal sealed class FrameworkResultsMenu
    {
        /// <summary> Menu id, private to this mod inside the framework. </summary>
        public const string MenuId = "results";

        private const int VisibleRows = 8;
        private const int RowHeight = 56;
        private const int SpriteSize = 16;
        private const int MenuWidth = 900;

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
        public FrameworkResultsMenu(IStardewUIApi api, IModHelper helper)
        {
            this.api = api;
            this.helper = helper;

            IUIMenuOptions options = api.CreateMenuOptions();
            options.Title = () => helper.Translation.Get("app-name");
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
        /// <param name="cropInfos"> The calculation results to list. </param>
        /// <param name="parent"> The (open) main menu. </param>
        public void Show(IReadOnlyList<CropInfo> cropInfos, IUIMenu parent)
        {
            crops.Clear();
            crops.AddRange(cropInfos);
            emptyLabel.Visible = crops.Count == 0;
            grid.Visible = crops.Count > 0;
            grid.ClearSelection();
            grid.Refresh();
            grid.ScrollToRow(0);
            menu.OpenAsChild(parent);
        }

        #region Columns

        /// <summary>Crop (sprite + name, sorted by name) and the numeric columns, sorted by value, profit per day descending first.</summary>
        private void AddColumns()
        {
            IUIDataGridColumn name = grid.AddColumn("crop", () => helper.Translation.Get("produce-type-sold"), "*");
            name.Text = row => crops[row].Crop.DisplayName;
            name.BuildCell = BuildCropCell;
            name.Sortable = true;
            name.MinWidth = 160;

            AddNumberColumn("profit", "total-p", row => crops[row].TotalProfit, true);
            AddNumberColumn("profit-day", "total-p-day", row => crops[row].ProfitPerDay, true);
            AddNumberColumn("seed-loss", "total-s-loss", row => crops[row].TotalSeedLoss, true);
            AddNumberColumn("fert-loss", "total-f-loss", row => crops[row].TotalFertilizerLoss, true);
            AddNumberColumn("harvests", "harvest-count", row => crops[row].TotalHarvests, false);
            AddNumberColumn("duration", "duration", row => crops[row].Duration, false);

            grid.Sort("profit-day", true);
        }

        private void AddNumberColumn(string id, string headerKey, Func<int, double> value, bool money)
        {
            IUIDataGridColumn column = grid.AddColumn(id, () => helper.Translation.Get(headerKey), money ? "130px" : "100px");
            column.Text = row => money ? Money(value(row)) : value(row).ToString("0", CultureInfo.CurrentCulture);
            column.SortNumber = value;
            column.Sortable = true;
            column.Align = UIAlign.End;
        }

        /// <summary>The crop sprite followed by its name.</summary>
        private void BuildCropCell(int row, IUIContainer cell)
        {
            CropInfo info = crops[row];
            IUIStack line = api.AddStack(cell, $"crop{row}", true, 8);
            line.VerticalAlign = UIAlign.Center;
            IUIImage sprite = api.AddImage(line, $"crop{row}.sprite", info.Crop.Sprite.Item1, info.Crop.Sprite.Item2, 2f);
            sprite.VerticalAlign = UIAlign.Center;
            IUILabel label = api.AddLabel(line, $"crop{row}.name", () => info.Crop.DisplayName);
            label.VerticalAlign = UIAlign.Center;
        }

        #endregion Columns

        #region Tooltip

        /// <summary>What the legacy hover box showed: the item, the money figures, timing and quality chances.</summary>
        private IUITooltip BuildTooltip(int row)
        {
            CropInfo info = crops[row];
            IUITooltip tip = api.CreateTooltip()
                .Title(() => info.Crop.DisplayName)
                .Item(info.Crop.Seed.QualifiedItemId)
                .Line(() => $"{helper.Translation.Get("total-p")}: {Money(info.TotalProfit)}   {helper.Translation.Get("total-p-day")}: {Money(info.ProfitPerDay)}")
                .Line(() => $"{helper.Translation.Get("total-s-loss")}: {Money(info.TotalSeedLoss)}   {helper.Translation.Get("total-s-loss-day")}: {Money(info.SeedLossPerDay)}")
                .Line(() => $"{helper.Translation.Get("total-f-loss")}: {Money(info.TotalFertilizerLoss)}   {helper.Translation.Get("total-f-loss-day")}: {Money(info.FertilizerLossPerDay)}")
                .Divider()
                .Line(() => $"{helper.Translation.Get("grow-time")}: {info.GrowthTime} {helper.Translation.Get("days")}   {helper.Translation.Get("regrow-time")}: {info.RegrowthTime}   {helper.Translation.Get("harvest-count")}: {info.TotalHarvests}")
                .Line(() => $"{helper.Translation.Get("duration")}: {info.Duration} {helper.Translation.Get("days")}   {helper.Translation.Get("produce-count")}: {info.ProductCount}   {helper.Translation.Get("extra-harvest-chance")}: {info.ChanceOfExtraProduct:P0}")
                .Divider()
                .Line(() => $"{helper.Translation.Get("quality")}: {info.ChanceOfNormalQuality:P0} / {info.ChanceOfSilverQuality:P0} / {info.ChanceOfGoldQuality:P0} / {info.ChanceOfIridiumQuality:P0}", Color.DarkSlateGray)
                .MaxWidth(520);
            return tip;
        }

        #endregion Tooltip

        private string Money(double value) => $"{value.ToString("0", CultureInfo.CurrentCulture)}{helper.Translation.Get("g")}";
    }
}
