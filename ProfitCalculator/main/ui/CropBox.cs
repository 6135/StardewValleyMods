using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using System;

namespace ProfitCalculator.main.ui
{
    /// <summary>
    /// A box that displays a crop and its information
    /// </summary>
    public class CropBox : BaseOption
    {
        /// <summary> The crop info to display. <see cref="main.CropInfo"/> </summary>
        public CropInfo CropInfo { get; }

        /// <summary> The hover box to display when the mouse is over the box. <see cref="CropHoverBox"/> </summary>
        public CropHoverBox HoverBox { get; }

        private readonly SpriteFont Font = Game1.smallFont;
        private readonly string mainText;

        /// <summary> The x position (from the box's left) the right-aligned profit texts end at. </summary>
        private float ProfitRightEdge => Position.X + (69 * (Game1.tileSize / 8));

        /// <summary>
        /// Creates a new CropBox
        /// </summary>
        /// <param name="x"> The x position of the box</param>
        /// <param name="y"> The y position of the box</param>
        /// <param name="w"> The width of the box</param>
        /// <param name="h"> The height of the box</param>
        /// <param name="crop"> The cropInfo to display. <see cref="main.CropInfo"/> </param>
        public CropBox(int x, int y, int w, int h, CropInfo crop) : base(x, y, w, h, () => crop.Crop.DisplayName, () => crop.Crop.DisplayName, () => crop.Crop.DisplayName)
        {
            mainText = crop.Crop.DisplayName;
            if (mainText.Length < 1)
            {
                mainText = "PlaceHolder";
            }
            CropInfo = crop;
            HoverBox = new CropHoverBox(CropInfo);
        }

        /// <summary>
        /// Called when the left mouse button is pressed. Executes before the action of the button is performed
        /// </summary>
        /// <param name="x"> The x position of the mouse</param>
        /// <param name="y"> The y position of the mouse</param>
        public override void BeforeReceiveLeftClick(int x, int y)
        {
            //no behaviour needed
        }

        /// <inheritdoc/>
        public override void Draw(SpriteBatch b)
        {
            DrawContent(b);
            HoverBox.Draw(b);
        }

        /// <summary>
        /// Draws the box at explicit bounds, without the hover box (used by the UI Framework results screen, which draws the hover box in its overlay pass).
        /// </summary>
        /// <param name="b"> The SpriteBatch to draw to</param>
        /// <param name="drawBounds"> The absolute bounds to draw the box in</param>
        public void Draw(SpriteBatch b, Rectangle drawBounds)
        {
            bounds = drawBounds;
            DrawContent(b);
        }

        private void DrawContent(SpriteBatch b)
        {
            DrawBoxAndSprite(b);
            DrawName(b);
            DrawProfit(b);
        }

        /// <summary> Draws the box background and the crop sprite in the middle of the box, aligned to the left. </summary>
        private void DrawBoxAndSprite(SpriteBatch b)
        {
            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new(0, 256, 60, 60),
                (int)Position.X,// - 16,
                (int)Position.Y,// - 8 - 4,
                bounds.Width,// + 32,
                bounds.Height,// + 16 + 8,
                Color.White,
                1.2f,
                false,
                0.5f
             );
            const int spriteSize = 16;
            const int spriteDisplaySize = (int)(spriteSize * 3.25f);

            b.Draw(
                CropInfo.Crop.Sprite.Item1,
                new Rectangle(
                    (int)Position.X + (3 * Game1.tileSize / 8),
                    (int)Position.Y + (bounds.Height / 2) - (Game1.tileSize / 2) + 6,
                    spriteDisplaySize,
                    spriteDisplaySize
                ),
                CropInfo.Crop.Sprite.Item2,
                Color.White,
                0f,
                Vector2.Zero,
                SpriteEffects.None,
                0.6f
            );
        }

        /// <summary>
        /// Draws the crop name in the middle of the box, aligned to the left with a spacing of 2xtilesize from the left.
        /// If the name is too wide for the space left of the profit texts, the font size is reduced until it fits.
        /// </summary>
        private void DrawName(SpriteBatch b)
        {
            float fontSizeModifier = 1.3f;

            float fontSize = Font.MeasureString(mainText).X * fontSizeModifier;

            float rightSideTextMaxSize = Font.MeasureString(CropInfo.ProfitPerDay.ToString("0.00")).X + Font.MeasureString($" {Helper.Translation.Get("g")}/{Helper.Translation.Get("day")}").X;
            rightSideTextMaxSize *= 1.8f;

            float boxWidth = bounds.Width - (3 * Game1.tileSize / 8) - rightSideTextMaxSize;

            while (fontSize > boxWidth)
            {
                fontSizeModifier -= 0.005f;
                fontSize = Font.MeasureString(mainText).X * fontSizeModifier;
            }

            b.DrawString(
                Font,
                mainText,
                new Vector2(
                    Position.X + (3 * Game1.tileSize / 2),
                    Position.Y + (bounds.Height / 2) - (Font.MeasureString(mainText).Y / 2)
                ),
                Color.Black,
                0f,
                Vector2.Zero,
                fontSizeModifier,
                SpriteEffects.None,
                0.6f
            );
        }

        /// <summary>
        /// Draws the total profit (rounded) above the middle of the box and the profit per day (two decimals) below it, both right aligned
        /// with their unit after them and in red when the crop loses money.
        /// </summary>
        private void DrawProfit(SpriteBatch b)
        {
            Color color = CropInfo.TotalProfit < 0 ? Color.Red : Color.DarkGreen;
            float middle = Position.Y + (bounds.Height / 2) + 3;

            string price = Math.Round(CropInfo.TotalProfit).ToString();
            string g = $" {Helper.Translation.Get("g")}";
            DrawValueWithUnit(b, price, g, middle, true, color);

            string pricePerDay = CropInfo.ProfitPerDay.ToString("0.00");
            string ppd = $" {Helper.Translation.Get("g")}/{Helper.Translation.Get("day")}";
            DrawValueWithUnit(b, pricePerDay, ppd, middle, false, color);
        }

        /// <summary>
        /// Draws <paramref name="unit"/> ending at <see cref="ProfitRightEdge"/> and <paramref name="value"/> right before it,
        /// either sitting on <paramref name="middle"/> (<paramref name="above"/>) or hanging from it.
        /// </summary>
        private void DrawValueWithUnit(SpriteBatch b, string value, string unit, float middle, bool above, Color valueColor)
        {
            float unitWidth = Font.MeasureString(unit).X;
            b.DrawString(
                Font,
                value,
                new Vector2(ProfitRightEdge - Font.MeasureString(value).X - unitWidth, above ? middle - Font.MeasureString(value).Y : middle),
                valueColor,
                0f,
                Vector2.Zero,
                1f,
                SpriteEffects.None,
                0.6f
            );
            b.DrawString(
                Font,
                unit,
                new Vector2(ProfitRightEdge - unitWidth, above ? middle - Font.MeasureString(unit).Y : middle),
                Color.Black,
                0f,
                Vector2.Zero,
                1f,
                SpriteEffects.None,
                0.6f
            );
        }

        /// <summary>
        /// The update event.
        /// </summary>
        public override void Update()
        {
            //No need to update
        }

        ///<inheritdoc/>
        public override void PerformHoverAction(int x, int y)
        {
            base.PerformHoverAction(x, y);
            if (containsPoint(x, y))
            {
                HoverBox.Update();
                HoverBox.Open(true);
            }
            else
            {
                HoverBox.Open(false);
            }
        }
    }
}
