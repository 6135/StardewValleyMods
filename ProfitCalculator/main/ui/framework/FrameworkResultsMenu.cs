using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System.Collections.Generic;
using UIFramework.Api;

#nullable enable

namespace ProfitCalculator.main.ui.framework
{
    /// <summary>
    /// The Profit Calculator results screen built through the UI Framework API; the counterpart of the legacy
    /// <see cref="menus.ProfitCalculatorResultsList"/>. A virtualized list shows one <see cref="CropBox"/> per crop
    /// (same slot height and visible row count as the legacy list); the framework supplies scrolling (wheel, arrows,
    /// scrollbar drag). Each row is drawn by the existing <see cref="CropBox"/> code through <c>OnDrawExtra</c>, and its
    /// <see cref="CropHoverBox"/> is drawn in the menu's overlay pass while the row is hovered.
    /// </summary>
    internal sealed class FrameworkResultsMenu
    {
        /// <summary> Menu id, private to this mod inside the framework. </summary>
        public const string MenuId = "results";

        /// <summary> Rows shown at once (legacy <c>maxOptions</c>). </summary>
        private const int VisibleRows = 6;

        /// <summary> Legacy slot height. </summary>
        private static readonly int RowHeight = Game1.tileSize + (Game1.tileSize / 2);

        /// <summary> Legacy box width; the framework draws the list scrollbar inside the box, so room is added for it. </summary>
        private static readonly int MenuWidth = 632 + (IClickableMenu.borderWidth * 2) + Game1.tileSize;

        private readonly IStardewUIApi api;
        private readonly List<CropBox> boxes = new();
        private readonly IUIMenu menu;
        private readonly IUIList list;
        private readonly IUILabel emptyLabel;

        /// <summary>
        /// Builds the results screen (empty until <see cref="Show"/> is called).
        /// </summary>
        /// <param name="api"> The UI Framework API. </param>
        /// <param name="helper"> The mod helper (translations). </param>
        public FrameworkResultsMenu(IStardewUIApi api, IModHelper helper)
        {
            this.api = api;

            IUIMenuOptions options = api.CreateMenuOptions();
            options.Width = MenuWidth;
            menu = api.CreateMenu(MenuId, options);

            emptyLabel = api.AddLabel(menu.Root, "empty", () => helper.Translation.Get("no-results"));
            emptyLabel.Font = UIFont.Dialogue;
            emptyLabel.SetMargin(Game1.tileSize / 2);
            emptyLabel.Visible = false;

            list = api.AddList(menu.Root, "crops", RowHeight, VisibleRows, () => boxes.Count, BuildRow);
            list.HorizontalAlign = UIAlign.Stretch;
        }

        /// <summary>
        /// Replaces the listed crops and opens the screen as a child of <paramref name="parent"/>.
        /// </summary>
        /// <param name="cropInfos"> The calculation results to list. </param>
        /// <param name="parent"> The (open) main menu. </param>
        public void Show(IReadOnlyList<CropInfo> cropInfos, IUIMenu parent)
        {
            boxes.Clear();
            foreach (CropInfo cropInfo in cropInfos)
            {
                boxes.Add(new CropBox(0, 0, 0, 0, cropInfo));
            }
            emptyLabel.Visible = boxes.Count == 0;
            list.Visible = boxes.Count > 0;
            list.ScrollTo(0);
            list.Refresh();
            menu.OpenAsChild(parent);
        }

        /// <summary>
        /// Fills a list row for item <paramref name="index"/>: a bare panel stretched over the slot whose drawing is
        /// delegated to the legacy <see cref="CropBox"/>, with the hover box opened / closed on hover and drawn in the overlay pass.
        /// </summary>
        private void BuildRow(int index, IUIContainer row)
        {
            if (index < 0 || index >= boxes.Count)
            {
                return;
            }
            CropBox box = boxes[index];
            box.HoverBox.Open(false);
            IUIPanel panel = api.AddPanel(row, $"crop{index}", false, 0);
            panel.HorizontalAlign = UIAlign.Stretch;
            panel.VerticalAlign = UIAlign.Stretch;
            panel.OnDrawExtra = box.Draw;
            panel.OnHover = _ => box.HoverBox.Open(true);
            panel.OnHoverEnd = _ => box.HoverBox.Open(false);
            panel.OnDrawOverlay = (b, _) => DrawHoverBox(b, panel, box.HoverBox);
        }

        /// <summary>Position the hover box next to the cursor and draw it (it handles its own open state and delay) while the row is hovered.</summary>
        private static void DrawHoverBox(SpriteBatch b, IUIElement row, CropHoverBox hoverBox)
        {
            if (!row.IsHovered)
            {
                return;
            }
            hoverBox.Update();
            hoverBox.Draw(b);
        }
    }
}
