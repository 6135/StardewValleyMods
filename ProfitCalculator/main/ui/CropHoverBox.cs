using ProfitCalculator.main.memory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System;

namespace ProfitCalculator.main.ui
{
    /// <summary>
    ///   Hover details for each crop in the profit calculator.
    /// </summary>
    public class CropHoverBox : IDisposable, IDrawable
    {
        private bool isOpen;
        private readonly int windowWidth;
        private readonly int windowHeight;
        private int x;
        private int y;
        private int hoverDelay;
        private readonly int hoverDelayDefault;
        private Rectangle drawBox;
        private readonly CropInfo cropInfo;
        private readonly SpriteFont font;
        private readonly IModHelper Helper = Container.Instance.Resolve<IModHelper>(ModEntry.UniqueID);

        /// <summary> Extra space left between two groups of rows. </summary>
        private const int GroupSpacing = 16;

        /// <summary> Where the next row of a panel is drawn: X is the left edge of the labels, Y the top of the row, Z the right edge the values end at. </summary>
        private Vector3 currentTextPosition;

        /// <summary>
        /// Creates a new CropHoverBox.
        /// </summary>
        /// <param name="cropInfo"></param>
        public CropHoverBox(CropInfo cropInfo)
        {
            font = Game1.smallFont;
            isOpen = false;
            windowWidth = 400;
            windowHeight = 600;
            x = 0;
            y = 0;
            drawBox = new(x, y, windowWidth, windowHeight);
            this.cropInfo = cropInfo;
            ModConfig config = Helper.ReadConfig<ModConfig>();
            hoverDelay = config?.ToolTipDelay ?? 30;
            hoverDelayDefault = config?.ToolTipDelay ?? 30;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        protected virtual void Dispose(bool disposing)
        {
            isOpen = false;
        }

        /// <inheritdoc/>
        public void Draw(SpriteBatch b)
        {
            if (isOpen && hoverDelay <= 0)
            {
                //Top Panel
                DrawMainBox(b);
                //Bottom Panel
                DrawSecondaryBox(b);
            }
            else if (isOpen)
            {
                hoverDelay--;
            }
            else
            {
                // closed: nothing to draw
            }
        }

        /// <summary>
        /// Draws a panel background of the box and sets <see cref="currentTextPosition"/> to its first row.
        /// </summary>
        /// <param name="b"> The SpriteBatch to draw to</param>
        /// <param name="top"> The y position of the panel</param>
        /// <param name="height"> The height of the panel</param>
        /// <param name="drawLayer"> The layer depth the panel is drawn at</param>
        private void DrawPanel(SpriteBatch b, int top, int height, float drawLayer)
        {
            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                drawBox.X,
                top,
                windowWidth,
                height,
                Color.White,
                1f,
                draw_layer: drawLayer
            );
            currentTextPosition = new(
                drawBox.X + ((float)Game1.tileSize / 4),
                top + ((float)Game1.tileSize / 4),
                drawBox.X + drawBox.Width - (Game1.tileSize / 4));
        }

        /// <summary>
        /// Draws a row at <see cref="currentTextPosition"/>: the translated label on the left (with a colon after it) and the value right aligned, then moves the position to the next row.
        /// </summary>
        /// <param name="b"> The SpriteBatch to draw to</param>
        /// <param name="key"> The translation key of the label</param>
        /// <param name="value"> The text on the right</param>
        private void DrawRow(SpriteBatch b, string key, string value)
        {
            string label = $"{Helper.Translation.Get(key)}:";
            b.DrawString(
                font,
                label,
                new Vector2(
                    currentTextPosition.X,
                    currentTextPosition.Y
                ),
                Color.Black,
                0f,
                Vector2.Zero,
                1f,
                SpriteEffects.None,
                0.75f
            );
            b.DrawString(
                font,
                value,
                new Vector2(
                    currentTextPosition.Z - font.MeasureString(value).X,
                    currentTextPosition.Y),
                Color.Black,
                0f,
                Vector2.Zero,
                1f,
                SpriteEffects.None,
                0.75f
            );
            currentTextPosition.Y += font.MeasureString(label).Y;
        }

        /// <summary> Draws a row whose value is a number of days. </summary>
        private void DrawDaysRow(SpriteBatch b, string key, int days) => DrawRow(b, key, $"{days} {Helper.Translation.Get("days")}");

        /// <summary> Draws a row whose value is an amount of gold. </summary>
        private void DrawGoldRow(SpriteBatch b, string key, double gold) => DrawRow(b, key, $"{Math.Round(gold)} {Helper.Translation.Get("g")}");

        /// <summary> Draws a row whose value is an amount of gold per day. </summary>
        private void DrawGoldPerDayRow(SpriteBatch b, string key, double goldPerDay) => DrawRow(b, key, $"{goldPerDay:0.00} {Helper.Translation.Get("g")}/{Helper.Translation.Get("day")}");

        /// <summary> Draws a row whose value is a count. </summary>
        private void DrawCountRow(SpriteBatch b, string key, object count) => DrawRow(b, key, $"#{count}");

        /// <summary> Draws a row whose value is a chance, as a percentage with the given number format. </summary>
        private void DrawChanceRow(SpriteBatch b, string key, double chance, string format = "0.00") => DrawRow(b, key, $"{(chance * 100).ToString(format)}%");

        /// <summary> Top panel: profit, seed loss and growth details. </summary>
        private void DrawMainBox(SpriteBatch b)
        {
            DrawPanel(b, drawBox.Y, windowHeight / 2, 0.7f);

            //Total profit: Total Profit
            //Total Profit Per Day: P/D
            DrawGoldRow(b, "total-p", cropInfo.TotalProfit);
            DrawGoldPerDayRow(b, "total-p-day", cropInfo.ProfitPerDay);

            currentTextPosition.Y += GroupSpacing;
            DrawGoldRow(b, "total-s-loss", cropInfo.TotalSeedLoss);
            DrawGoldPerDayRow(b, "total-s-loss-day", cropInfo.SeedLossPerDay);

            currentTextPosition.Y += GroupSpacing;
            DrawDaysRow(b, "grow-time", cropInfo.GrowthTime);
            if (cropInfo.RegrowthTime <= 0)
            {
                DrawRow(b, "regrow-time", Helper.Translation.Get("no"));
            }
            else
            {
                DrawDaysRow(b, "regrow-time", cropInfo.RegrowthTime);
            }
            DrawCountRow(b, "harvest-count", cropInfo.TotalHarvests);
        }

        /// <summary> Bottom panel: harvest amounts and quality chances. </summary>
        private void DrawSecondaryBox(SpriteBatch b)
        {
            DrawPanel(b, drawBox.Y + (windowHeight / 2) - (Game1.tileSize / 4), windowHeight - (windowHeight / 2), 0.71f);

            DrawCountRow(b, "min-harvests", cropInfo.Crop.MinHarvests);
            DrawCountRow(b, "max-harvests", cropInfo.Crop.MaxHarvests);
            if (cropInfo.Crop.MaxHarvestIncreasePerFarmingLevel != 0)
            {
                DrawCountRow(b, "max-harvests-level", cropInfo.Crop.MaxHarvestIncreasePerFarmingLevel);
            }
            if (cropInfo.Crop.ChanceForExtraCrops != 0)
            {
                DrawRow(b, "extra-harvest-chance", $"{cropInfo.Crop.ChanceForExtraCrops * 100}%");
            }

            currentTextPosition.Y += 8;
            if (cropInfo.ChanceOfNormalQuality != 0)
            {
                DrawChanceRow(b, "value-normal", cropInfo.ChanceOfNormalQuality);
            }
            DrawChanceRow(b, "value-silver", cropInfo.ChanceOfSilverQuality);
            DrawChanceRow(b, "value-gold", cropInfo.ChanceOfGoldQuality);
            if (cropInfo.ChanceOfIridiumQuality != 0)
            {
                DrawChanceRow(b, "value-iridium", cropInfo.ChanceOfIridiumQuality, "0.0");
            }
        }

        /// <inheritdoc/>
        public void Reset()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void Update()
        {
            //x and y set to near the mouse
            //if mouse is near the edge of the screen, move the box to the other side of the mouse
            Rectangle safeArea = Utility.getSafeArea();

            int mouseX = Game1.getMouseX() + Game1.tileSize;
            int mouseY = Game1.getMouseY();

            if (mouseX + windowWidth > safeArea.Right)
            {
                x = mouseX - windowWidth;
            }
            else
            {
                x = mouseX;
            }

            if (mouseY + windowHeight > safeArea.Bottom)
            {
                y = mouseY - windowHeight;
            }
            else
            {
                y = mouseY;
            }

            //if the box is off the screen, move it back on
            if (x < safeArea.Left)
            {
                x = safeArea.Left + (Game1.tileSize / 4);
            }
            if (y < safeArea.Top)
            {
                y = safeArea.Top + (Game1.tileSize / 4);
            }

            drawBox = new(
                x,
                y,
                windowWidth,
                windowHeight
            );
        }

        /// <inheritdoc/>
        public void GameWindowSizeChanged()
        {
            //No behavior needed
        }

        /// <summary>
        ///  Opens or closes the hover box.
        /// </summary>
        /// <param name="_open"> Whether to open or close the box.</param>
        public void Open(bool _open)
        {
            isOpen = _open;
            if (!_open)
            {
                hoverDelay = hoverDelayDefault;
            }
        }

        /// <summary>
        ///  Opens or closes the hover box.
        /// </summary>
        public void Open() => Open(false);
    }
}
