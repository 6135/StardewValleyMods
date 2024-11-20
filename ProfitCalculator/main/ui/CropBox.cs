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
        /// <summary> The crop info to display. <see cref="CropInfo"/> </summary>
        public readonly CropInfo cropInfo;

        /// <summary> The hover box to display when the mouse is over the box. <see cref="CropHoverBox"/> </summary>
        public readonly CropHoverBox cropHoverBox;

        private readonly SpriteFont Font = Game1.smallFont;
        private readonly string mainText;

        /// <summary>
        /// Creates a new CropBox
        /// </summary>
        /// <param name="x"> The x position of the box</param>
        /// <param name="y"> The y position of the box</param>
        /// <param name="w"> The width of the box</param>
        /// <param name="h"> The height of the box</param>
        /// <param name="crop"> The cropInfo to display. <see cref="CropInfo"/> </param>
        public CropBox(int x, int y, int w, int h, CropInfo crop) : base(x, y, w, h, () => crop.Crop.DisplayName, () => crop.Crop.DisplayName, () => crop.Crop.DisplayName)
        {
            mainText = crop.Crop.DisplayName;
            if (mainText.Length < 1)
            {
                mainText = "PlaceHolder";
            }
            cropInfo = crop;
            cropHoverBox = new CropHoverBox(cropInfo);
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
            //draw crop sprite in the middle of the box aligned to the left
            const int spriteSize = 16;
            int spriteDisplaySize = (int)(spriteSize * 3.25f);

            b.Draw(
                cropInfo.Crop.Sprite.Item1,
                new Rectangle(
                    (int)Position.X + (3 * Game1.tileSize / 8),
                    (int)Position.Y + (bounds.Height / 2) - (Game1.tileSize / 2) + 6,
                    spriteDisplaySize,
                    spriteDisplaySize
                ),
                cropInfo.Crop.Sprite.Item2,
                Color.White,
                0f,
                Vector2.Zero,
                SpriteEffects.None,
                0.6f
            );

            //draw string in middle of box, aligned to the left with a spacing of 2xtilesize from the left
            //But if string size is too big, draw, reduce font size, and draw again
            float fontSizeModifier = 1.3f;

            float fontSize = Font.MeasureString(mainText).X * fontSizeModifier;

            float rightSideTextMaxSize = Font.MeasureString(cropInfo.ProfitPerDay.ToString("0.00")).X + Font.MeasureString($" {Helper.Translation.Get("g")}/{Helper.Translation.Get("day")}").X;
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

            string price = Math.Round(cropInfo.TotalProfit).ToString();
            string g = $" {Helper.Translation.Get("g")}";
            Color color;
            if (cropInfo.TotalProfit < 0)
            {
                color = Color.Red;
            }
            else
            {
                color = Color.DarkGreen;
            }
            b.DrawString(
                Font,
                price,
                new Vector2(
                    Position.X + (69 * (Game1.tileSize / 8)) - Font.MeasureString(price).X - Font.MeasureString(g).X,
                    Position.Y + (bounds.Height / 2) + 3 - Font.MeasureString(price).Y
                ),
                color,
                0f,
                Vector2.Zero,
                1f,
                SpriteEffects.None,
                0.6f
            );
            b.DrawString(
                Font,
                g,
                new Vector2(
                    Position.X + (69 * (Game1.tileSize / 8)) - Font.MeasureString(g).X,
                    Position.Y + (bounds.Height / 2) + 3 - Font.MeasureString(g).Y
                ),
                Color.Black,
                0f,
                Vector2.Zero,
                1f,
                SpriteEffects.None,
                0.6f
            );
            //further left, draw the price per day of the crop in the box, rounded to the nearest two decimal places, with G/Day at the end
            string pricePerDay = cropInfo.ProfitPerDay.ToString("0.00");
            string ppd = $" {Helper.Translation.Get("g")}/{Helper.Translation.Get("day")}";
            b.DrawString(
                Font,
                pricePerDay,
                new Vector2(
                    Position.X + (69 * (Game1.tileSize / 8)) - Font.MeasureString(pricePerDay).X - Font.MeasureString(ppd).X,
                    Position.Y + (bounds.Height / 2) + 3
                ),
                color,
                0f,
                Vector2.Zero,
                1f,
                SpriteEffects.None,
                0.6f
            );
            b.DrawString(
                Font,
                ppd,
                new Vector2(
                    Position.X + (69 * (Game1.tileSize / 8)) - Font.MeasureString(ppd).X,
                    Position.Y + (bounds.Height / 2) + 3
                ),
                Color.Black,
                0f,
                Vector2.Zero,
                1f,
                SpriteEffects.None,
                0.6f
            );

            cropHoverBox.Draw(b);
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
                cropHoverBox.Update();
                cropHoverBox.Open(true);
            }
            else
            {
                cropHoverBox.Open(false);
            }
        }
    }
}