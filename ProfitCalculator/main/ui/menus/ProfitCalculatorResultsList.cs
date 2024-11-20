using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;

namespace ProfitCalculator.main.ui.menus
{
    /// <summary>
    /// A menu that displays the results of the profit calculator.
    /// </summary>
    public class ProfitCalculatorResultsList : IClickableMenu
    {
        private static readonly int widthOnScreen = 632 + (borderWidth * 2);
        private static readonly int heightOnScreen = 600 + (borderWidth * 2) + Game1.tileSize;

        private readonly List<BaseOption> Options = new();
        private readonly List<Vector4> OptionSlots = new();

        private ClickableTextureComponent upArrow;
        private ClickableTextureComponent downArrow;
        private Rectangle scrollBarBounds;
        private ClickableTextureComponent scrollBar;

        private int currentItemIndex;
        private readonly int maxOptions = 6;
        private bool scrolling;

        /// <summary> Tracks whether the menu is open or not. </summary>
        public bool IsResultsListOpen { get; set; }

        /// <summary>
        /// Creates a new instance of the ProfitCalculatorResultsList class.
        /// </summary>
        /// <param name="_cropInfos"> The list of crop infos to display in the menu. </param>
        public ProfitCalculatorResultsList(List<CropInfo> _cropInfos) :
            base(
                (int)GetAppropriateMenuPosition().X,
                (int)GetAppropriateMenuPosition().Y,
                widthOnScreen,
                heightOnScreen
            )
        {
            for (int i = 0; i < maxOptions; i++)
            {
                OptionSlots.Add(
                    new(
                        xPositionOnScreen + spaceToClearSideBorder + borderWidth + 10,
                        yPositionOnScreen + spaceToClearTopBorder + 5 + (Game1.tileSize / 2) - (Game1.tileSize / 4) + ((Game1.tileSize + (Game1.tileSize / 2)) * i),
                        widthOnScreen - ((spaceToClearSideBorder + borderWidth + 10) * 2),
                        Game1.tileSize + (Game1.tileSize / 2)
                   )
                );
            }
            foreach (CropInfo cropInfo in _cropInfos)
            {
                Options.Add(
                    new CropBox(
                        0,
                        0,
                        0,
                        0,
                        cropInfo
                    )
                );
            }

            behaviorBeforeCleanup = delegate
            {
                IsResultsListOpen = false;
            };

            xPositionOnScreen = (int)GetAppropriateMenuPosition().X;
            yPositionOnScreen = (int)GetAppropriateMenuPosition().Y;

            int scrollbar_x = xPositionOnScreen + width;
            upArrow = new ClickableTextureComponent(
                new Rectangle(scrollbar_x, yPositionOnScreen + Game1.tileSize + (Game1.tileSize / 3), 44, 48),
                Game1.mouseCursors,
                new Rectangle(421, 459, 11, 12),
                4f);
            downArrow = new ClickableTextureComponent(
                new Rectangle(scrollbar_x, yPositionOnScreen + height - 64, 44, 48),
                Game1.mouseCursors,
                new Rectangle(421, 472, 11, 12),
                4f);
            scrollBarBounds = default;
            scrollBarBounds.X = upArrow.bounds.X + 12;
            scrollBarBounds.Width = 24;
            scrollBarBounds.Y = upArrow.bounds.Y + upArrow.bounds.Height + 4;
            scrollBarBounds.Height = downArrow.bounds.Y - 4 - scrollBarBounds.Y;
            scrollBar = new ClickableTextureComponent(new Rectangle(scrollBarBounds.X, scrollBarBounds.Y, 24, 40), Game1.mouseCursors, new Rectangle(435, 463, 6, 10), 4f);
        }

        #region Draw Methods

        /// <inheritdoc/>
        public override void draw(SpriteBatch b)
        {
            //draw bottom up

            if (!Game1.options.showMenuBackground)
                b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.4f);

            Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, widthOnScreen, heightOnScreen, speaker: false, drawOnlyBox: true);

            b.End();
            b.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, SamplerState.PointClamp);

            int maxIndex = Math.Min(maxOptions, Options.Count);
            for (int i = 0; i < maxIndex; i++)
            {
                if (currentItemIndex + i >= Options.Count)
                    break;
                Options[currentItemIndex + i].bounds = new(
                    (int)OptionSlots[i].X,
                    (int)OptionSlots[i].Y,
                    (int)OptionSlots[i].Z,
                    (int)OptionSlots[i].W);
                Options[currentItemIndex + i].Draw(b);
            }

            upArrow.draw(b, Color.White, 0.6f);
            downArrow.draw(b, Color.White, 0.6f);

            drawTextureBox(
                b,
                Game1.mouseCursors,

                new Rectangle(403, 383, 6, 6),

                scrollBarBounds.X,
                scrollBarBounds.Y,
                scrollBarBounds.Width,
                scrollBarBounds.Height,
                Color.White,
                4f,
                drawShadow: false,
                draw_layer: 0.6f
                );
            scrollBar.draw(
                b,
                Color.White,
                0.65f
            );

            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            if (shouldDrawCloseButton())

                base.draw(b);
            if (!Game1.options.hardwareCursor)

                b.Draw(Game1.mouseCursors, new Vector2(Game1.getMouseX(), Game1.getMouseY()), Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, Game1.options.gamepadControls ? 44 : 0, 16, 16), Color.White, 0f, Vector2.Zero, 4f + (Game1.dialogueButtonScale / 150f), SpriteEffects.None, 1f);
        }

        #endregion Draw Methods

        #region Event Handling

        private void SetScrollBarToCurrentIndex()
        {
            if (Options.Count > 0)
            {
                //devide the height of the scroll bar by the number of options minus the displayed options, then multiply by the current index to get the position of the scroll bar without going out of bounds. //804 is max y for bar
                int numberOfSteps = Math.Max(1, Options.Count - maxOptions);
                double sizeOfStep = Math.Floor(
                    (scrollBarBounds.Height - (scrollBar.bounds.Height / 2.0)) / numberOfSteps
                    );
                double barPosition = scrollBarBounds.Y + (sizeOfStep * currentItemIndex);
                scrollBar.bounds.Y =
                    (int)Math.Floor(barPosition);
                if (currentItemIndex == Options.Count - maxOptions)
                {
                    scrollBar.bounds.Y = downArrow.bounds.Y - scrollBar.bounds.Height - 7;
                }
            }
            else
            {
                scrollBar.bounds.Y = scrollBarBounds.Y;
            }
        }

        /// <summary>
        /// Handles mouse scroll wheel actions received by the menu. Goes up or down a page depending on the direction of the scroll.
        /// </summary>
        /// <param name="direction"> The direction of the scroll. 1 for down and -1 for up </param>
        public override void receiveScrollWheelAction(int direction)
        {
            base.receiveScrollWheelAction(direction);
            if (direction > 0 && currentItemIndex > 0)
            {
                ArrowPressed(-1);
                Game1.playSound("shiny4");
            }
            else if (direction < 0 && currentItemIndex < Math.Max(0, Options.Count - maxOptions))
            {
                ArrowPressed();
                Game1.playSound("shiny4");
            }
            else
            {
                throw new InvalidOperationException();
            }

            if (Game1.options.SnappyMenus)
            {
                snapCursorToCurrentSnappedComponent();
            }
        }

        /// <summary>
        /// Handles key presses received by the menu.
        /// </summary>
        /// <param name="key"> The key that was pressed. </param>
        public override void receiveKeyPress(Keys key)
        {
            switch (key)
            {
                case Keys.Escape:
                    exitThisMenu();
                    break;

                case Keys.Down:
                    if (currentItemIndex + maxOptions < Options.Count)
                    {
                        ArrowPressed();
                        Game1.playSound("shwip");
                    }
                    break;

                case Keys.Up:
                    if (currentItemIndex - maxOptions >= 0)
                    {
                        ArrowPressed(-1);
                        Game1.playSound("shwip");
                    }
                    break;

                default:
                    base.receiveKeyPress(key);
                    break;
            }
        }

        /// <summary>
        /// Handles mouse hovers received by the menu.
        /// </summary>
        /// <param name="x"> The x position of the mouse. </param>
        /// <param name="y"> The y position of the mouse. </param>
        public override void performHoverAction(int x, int y)
        {
            for (int i = 0; i < OptionSlots.Count; i++)
            {
                if (currentItemIndex >= 0 && currentItemIndex + i < Options.Count && Options[currentItemIndex + i].bounds.Contains(x - OptionSlots[i].X, y - OptionSlots[i].Y))
                {
                    Game1.SetFreeCursorDrag();
                    break;
                }
            }
            if (scrollBarBounds.Contains(x, y))
            {
                Game1.SetFreeCursorDrag();
            }
            if (GameMenu.forcePreventClose)
            {
                return;
            }

            //if hover over any of the optionSlots, print to log "HoveringOver it"
            int maxIndex = Math.Min(OptionSlots.Count, Options.Count);
            for (int i = 0; i < maxIndex; i++)
            {
                BaseOption option = (CropBox)Options[currentItemIndex + i];
                option.PerformHoverAction(x, y);
            }

            upArrow.tryHover(x, y);
            downArrow.tryHover(x, y);
            scrollBar.tryHover(x, y);
            _ = scrolling;
        }

        /// <summary>
        /// Handles scroll bar movement received by the menu according to the mouse y position.
        /// </summary>
        /// <param name="y"> The y position of the mouse. </param>
        public virtual void SetScrollFromY(int y)
        {
            int y2 = scrollBar.bounds.Y;
            float percentage = (y - scrollBarBounds.Y) / (float)scrollBarBounds.Height;
            float currentItemIndexFloat =
                Utility.Lerp(
                    t: Utility.Clamp(percentage, 0f, 1f),
                    a: 0f,
                    b: Options.Count - maxOptions);
            currentItemIndex = (int)Math.Round(currentItemIndexFloat);
            SetScrollBarToCurrentIndex();
            if (y2 != scrollBar.bounds.Y)
            {
                Game1.playSound("shiny4");
            }
        }

        /// <summary>
        /// Handles mouse clicks held received by the menu.
        /// </summary>
        /// <param name="x"> The x position of the mouse. </param>
        /// <param name="y"> The y position of the mouse. </param>
        public override void leftClickHeld(int x, int y)
        {
            if (!GameMenu.forcePreventClose)
            {
                base.leftClickHeld(x, y);
                if (scrolling)
                {
                    SetScrollFromY(y);
                }
            }
        }

        /// <summary>
        /// Handles mouse clicks released received by the menu.
        /// </summary>
        /// <param name="x"> The x position of the mouse. </param>
        /// <param name="y"> The y position of the mouse. </param>
        public override void releaseLeftClick(int x, int y)
        {
            if (!GameMenu.forcePreventClose)
            {
                base.releaseLeftClick(x, y);
                scrolling = false;
            }
        }

        /// <summary>
        /// Handles mouse clicks received by the menu.
        /// </summary>
        /// <param name="x"> The x position of the mouse. </param>
        /// <param name="y"> The y position of the mouse. </param>
        /// <param name="playSound"> Whether to play a sound when the click is received. </param>
        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (GameMenu.forcePreventClose)
            {
                return;
            }
            if (downArrow.containsPoint(x, y) && currentItemIndex < Math.Max(0, Options.Count - maxOptions))
            {
                ArrowPressed();
                Game1.playSound("shwip");
            }
            else if (upArrow.containsPoint(x, y) && currentItemIndex > 0)
            {
                ArrowPressed(-1);
                Game1.playSound("shwip");
            }
            else if (scrollBar.containsPoint(x, y))
            {
                scrolling = true;
            }
            else if (IsWithinScrollArea(x, y))
            {
                scrolling = true;
                leftClickHeld(x, y);
                releaseLeftClick(x, y);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private bool IsWithinScrollArea(int x, int y)
        {
            bool isWithinXBounds = x > xPositionOnScreen + width && x < xPositionOnScreen + width + 128;
            bool isWithinYBounds = y > yPositionOnScreen && y < yPositionOnScreen + height;
            return !downArrow.containsPoint(x, y) && isWithinXBounds && isWithinYBounds;
        }

        /// <summary>
        /// Handles arrow key presses received by the menu. Causing the menu to scroll up or down.
        /// </summary>
        /// <param name="direction"> The direction of the arrow key press. 1 for down and -1 for up </param>
        private void ArrowPressed(int direction = 1)
        {
            if (direction == 1)
            {
                downArrow.scale = downArrow.baseScale;
            }
            else
            {
                upArrow.scale = upArrow.baseScale;
            }
            currentItemIndex += direction;
            SetScrollBarToCurrentIndex();
        }

        /// <summary>
        /// Updates the menu. Refreshes the positions of the buttons and options.
        /// </summary>
        public override void update(GameTime time)
        {
            base.update(time);
            //update all the options and labels and buttons
            foreach (BaseOption option in Options)
            {
                option.Update();
            }
        }

        /// <summary>
        /// Gets the appropriate position for the menu to be in.
        /// </summary>
        /// <returns> The appropriate position for the menu to be in, in Vector2 format </returns>
        public static Vector2 GetAppropriateMenuPosition()
        {
            Vector2 defaultPosition = new(
                (Game1.viewport.Width * Game1.options.zoomLevel * (1 / Game1.options.uiScale) / 2) - (widthOnScreen / 2),
                (Game1.viewport.Height * Game1.options.zoomLevel * (1 / Game1.options.uiScale) / 2) - (heightOnScreen / 2)
            );

            //Force the viewport into a position that it should fit into on the screen???
            if (defaultPosition.X + widthOnScreen > Game1.viewport.Width)
            {
                defaultPosition.X = 0;
            }

            if (defaultPosition.Y + heightOnScreen > Game1.viewport.Height)
            {
                defaultPosition.Y = 0;
            }
            return defaultPosition;
        }

        /// <summary>The method called when the game window changes size.</summary>
        /// <param name="oldBounds">The former viewport.</param>
        /// <param name="newBounds">The new viewport.</param>
        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);

            xPositionOnScreen = (int)GetAppropriateMenuPosition().X;
            yPositionOnScreen = (int)GetAppropriateMenuPosition().Y;
            OptionSlots.Clear();
            for (int i = 0; i < maxOptions; i++)
            {
                OptionSlots.Add(
                    new(
                        xPositionOnScreen + spaceToClearSideBorder + borderWidth + 10,
                        yPositionOnScreen + spaceToClearTopBorder + 5 + (Game1.tileSize / 2) - (Game1.tileSize / 4) + ((Game1.tileSize + (Game1.tileSize / 2)) * i),
                        widthOnScreen - ((spaceToClearSideBorder + borderWidth + 10) * 2),
                        Game1.tileSize + (Game1.tileSize / 2)
                   )
                );
            }

            int scrollbar_x = xPositionOnScreen + width;
            upArrow = new ClickableTextureComponent(
                new Rectangle(scrollbar_x, yPositionOnScreen + Game1.tileSize + (Game1.tileSize / 3), 44, 48),
                Game1.mouseCursors,
                new Rectangle(421, 459, 11, 12),
                4f);
            downArrow = new ClickableTextureComponent(
                new Rectangle(scrollbar_x, yPositionOnScreen + height - 64, 44, 48),
                Game1.mouseCursors,
                new Rectangle(421, 472, 11, 12),
                4f);
            scrollBarBounds = default;
            scrollBarBounds.X = upArrow.bounds.X + 12;
            scrollBarBounds.Width = 24;
            scrollBarBounds.Y = upArrow.bounds.Y + upArrow.bounds.Height + 4;
            scrollBarBounds.Height = downArrow.bounds.Y - 4 - scrollBarBounds.Y;
            scrollBar = new ClickableTextureComponent(new Rectangle(scrollBarBounds.X, scrollBarBounds.Y, 24, 40), Game1.mouseCursors, new Rectangle(435, 463, 6, 10), 4f);
        }

        #endregion Event Handling
    }
}