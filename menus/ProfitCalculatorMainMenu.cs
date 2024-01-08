﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProfitCalculator.helper;
using ProfitCalculator.UI;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using static ProfitCalculator.helper.Helpers;

using StardewValley.Menus;

namespace ProfitCalculator.menus
{
    public class ProfitCalculatorMainMenu : IClickableMenu
    {
        StardewValley.Menus.OptionsDropDown asd;
        private readonly IModHelper helper;

        private readonly IMonitor monitor;
        private readonly ModConfig config;

        //aceessors
        public uint Day { get; set; } = 1;

        public uint MaxDay { get; set; } = 28;
        public uint MinDay { get; set; } = 1;
        public Season Season { get; set; } = Helpers.Season.Spring;

        public void setSeason(string season)
        {
            Season = (Season)Season.Parse(typeof(Season), season, false);
        }

        public ProduceType ProduceType { get; set; } = Helpers.ProduceType.Raw;
        public FertilizerQuality FertilizerQuality { get; set; } = Helpers.FertilizerQuality.None;
        public bool PayForSeeds { get; set; } = true;
        public bool PayForFertilizer { get; set; } = false;
        public uint MaxMoney { get; set; } = 0;
        public bool UseBaseStats { get; set; } = false;

        public string exampleString { get; set; } = "example";
        private static int widthOnScreen = 632 + borderWidth * 2;
        private static int heightOnScreen = 600 + borderWidth * 2 + Game1.tileSize;

        private readonly List<ClickableComponent> Labels = new List<ClickableComponent>();

        private readonly List<BaseOption> Options = new List<BaseOption>();

        private ClickableComponent calculateButton;
        private ClickableComponent resetButton;
        public bool isProfitCalculatorOpen { get; set; } = false;

        public ProfitCalculatorMainMenu(IModHelper _helper, IMonitor _monitor, ModConfig _modConfig) :
            base(
                (int)getAppropriateMenuPosition().X,
                (int)getAppropriateMenuPosition().Y,
                widthOnScreen,
                heightOnScreen)
        {
            helper = _helper;
            monitor = _monitor;
            config = _modConfig;

            behaviorBeforeCleanup = delegate
            {
                isProfitCalculatorOpen = false;
            };

            this.xPositionOnScreen = (int)getAppropriateMenuPosition().X;
            this.yPositionOnScreen = (int)getAppropriateMenuPosition().Y;
        }

        public void updateMenu()
        {
            Labels.Clear();
            Options.Clear();
            this.setUpPositions();
        }

        public override void update(GameTime time)
        {
            base.update(time);
            //Helpers.Monitor.Log("Updating Profit Calculator Menu", LogLevel.Debug);
            //update all the options and labels and buttons
            foreach (BaseOption option in Options)
            {
                // Helpers.Monitor.Log($"Updating {option.Name()}", LogLevel.Debug);
                option.Update();
            }
        }

        public static Vector2 getAppropriateMenuPosition()
        {
            Vector2 defaultPosition = new Vector2(Game1.viewport.Width / 2 - widthOnScreen / 2, (Game1.viewport.Height / 2 - heightOnScreen / 2));

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
            this.xPositionOnScreen = (int)getAppropriateMenuPosition().X;
            this.yPositionOnScreen = (int)getAppropriateMenuPosition().Y;

            this.updateMenu();
        }

        private void setUpPositions()
        {
            this.setUpButtonPositions();
            //option order:
            //Day int

            this.setUpDayOptionPositions();
            //Season dropdown
            this.setUpSeasonOptionPositions();
            //Produce Type dropdown
            this.setUpProduceTypeOptionPositions();
            //Fertilizer Quality dropdown
            /*this.setUpFertilizerQualityPositions();
            //Pay for Seeds checkbox
            this.setUpSeedsOptionPositions();
            //Pay for Fertilizer checkbox
            this.setUpFertilizerOptionPositions();
            //Max Money int
            this.setUpMoneyOptionPositions();
            //Use Base Stats checkbox
            this.setUpBaseStatsOptionPositions();*/
        }

        private void setUpButtonPositions()
        {
            calculateButton = new ClickableComponent(
                new Rectangle(
                    this.xPositionOnScreen + spaceToClearSideBorder + borderWidth,
                    this.yPositionOnScreen + borderWidth * 2 + spaceToClearTopBorder + Game1.tileSize * 7,
                    Game1.tileSize * 2,
                    Game1.tileSize
                ),
                "calculate",
                helper.Translation.Get("calculate")
            );

            resetButton = new ClickableComponent(
                new Rectangle(
                    this.xPositionOnScreen + spaceToClearSideBorder + borderWidth + Game1.tileSize * 2 + Game1.tileSize / 4,
                    this.yPositionOnScreen + borderWidth * 2 + spaceToClearTopBorder + Game1.tileSize * 7,
                    Game1.tileSize * 2,
                    Game1.tileSize
                ),
                "reset",
                helper.Translation.Get("reset")
                );
        }

        private void setUpDayOptionPositions()
        {
            Labels.Add(
                new ClickableComponent(
                    new Rectangle(
                        this.xPositionOnScreen + spaceToClearSideBorder + borderWidth,
                        this.yPositionOnScreen + spaceToClearTopBorder,
                        Game1.tileSize * 2,
                        Game1.tileSize
                    ),
                    "day",
                    helper.Translation.Get("day") + ": "
                )
            );
            /*Options.Add(
                new ClickableComponent(
                    new Rectangle(
                        this.xPositionOnScreen + spaceToClearSideBorder + borderWidth + Game1.tileSize * 7 + Game1.tileSize / 4,
                        this.yPositionOnScreen + spaceToClearTopBorder,
                        Game1.tileSize * 2,
                        Game1.tileSize
                    ),
                    "day",
                    day.ToString()
                )
            );*/
            /*Options.Add(
               new TextOption(
                     this.xPositionOnScreen + spaceToClearSideBorder + borderWidth + Game1.tileSize * 7 + Game1.tileSize / 4,
                     this.yPositionOnScreen + spaceToClearTopBorder + Game1.tileSize / 4,
                     () => "day",
                     () => helper.Translation.Get("day"),
                     valueGetter: () => this.exampleString,
                     valueSetter: (string value) => exampleString = value
                 )
             );*/
            UIntOption dayOption =
               new UIntOption(
                   this.xPositionOnScreen + spaceToClearSideBorder + borderWidth + Game1.tileSize * 5 - Game1.tileSize / 8,
                   this.yPositionOnScreen + spaceToClearTopBorder + Game1.tileSize / 4,
                   () => "day",
                   () => helper.Translation.Get("day"),
                   valueGetter: () => this.Day,
                   max: () => this.MaxDay,
                   min: () => this.MinDay,
                   valueSetter: (string value) => this.Day = uint.Parse(value),
                   enableClamping: true
               );
            dayOption.setTexture(Helper.ModContent.Load<Texture2D>(Path.Combine("assets", "text_box_small.png")));
            Options.Add(dayOption);
            /*Options.Add(
              new CheckboxOption(
                    this.xPositionOnScreen + spaceToClearSideBorder + borderWidth + Game1.tileSize * 7 + Game1.tileSize / 4,
                    this.yPositionOnScreen + spaceToClearTopBorder + Game1.tileSize / 4,
                    () => "day",
                    () => helper.Translation.Get("day"),
                    () => this.PayForFertilizer,
                    (bool value) => this.PayForFertilizer = value

                )
            );*/
        }

        private void setUpSeasonOptionPositions()
        {
            Labels.Add(
                new ClickableComponent(
                    new Rectangle(
                        this.xPositionOnScreen + spaceToClearSideBorder + borderWidth,
                        this.yPositionOnScreen + spaceToClearTopBorder + Game1.tileSize,
                        Game1.tileSize * 2,
                        Game1.tileSize
                    ),
                    "season",
                    helper.Translation.Get("season") + ": "
                )
            );

            DropdownOption seasonOption = new(
                this.xPositionOnScreen + spaceToClearSideBorder + borderWidth + Game1.tileSize * 5,
                this.yPositionOnScreen + spaceToClearTopBorder + Game1.tileSize + Game1.tileSize / 4,
                name: () => "season",
                label: () => helper.Translation.Get("season"),
                choices: () => Helpers.Season.GetNames(typeof(Helpers.Season)),
                labels: () => Helpers.GetAllTranslatedSeasons(),
                valueGetter: this.Season.ToString,
                valueSetter:
                    (string value) => this.Season = (Season)Helpers.Season.Parse(typeof(Season), value, true)
            );
            seasonOption.MaxValuesAtOnce = Enum.GetValues(typeof(Season)).Length;//size of enum

            Options.Add(seasonOption);
        }

        private void setUpProduceTypeOptionPositions()
        {
            Labels.Add(
                new ClickableComponent(
                    new Rectangle(
                        this.xPositionOnScreen + spaceToClearSideBorder + borderWidth,
                        this.yPositionOnScreen + spaceToClearTopBorder + Game1.tileSize * 2,
                        Game1.tileSize * 2,
                        Game1.tileSize
                    ),
                    "produceType",
                    helper.Translation.Get("produce-type") + ": "
                )
            );

            DropdownOption produceTypeOption = new(
                this.xPositionOnScreen + spaceToClearSideBorder + borderWidth + Game1.tileSize * 5,
                this.yPositionOnScreen + spaceToClearTopBorder + Game1.tileSize * 2 + Game1.tileSize / 4,
                name: () => "produceType",
                label: () => helper.Translation.Get("produce-type"),
                choices: () => Helpers.ProduceType.GetNames(typeof(Helpers.ProduceType)),
                labels: () => Helpers.GetAllTranslatedProduceTypes(),
                valueGetter: this.ProduceType.ToString,
                valueSetter: (string value) => this.ProduceType = (ProduceType)Helpers.ProduceType.Parse(typeof(ProduceType), value, true)
            );
            produceTypeOption.MaxValuesAtOnce = Enum.GetValues(typeof(ProduceType)).Length;//size of enum
            Options.Add(produceTypeOption);
        }

        /*private void setUpFertilizerQualityPositions()
        {
            Labels.Add(
                new ClickableComponent(
                    new Rectangle(
                        this.xPositionOnScreen + spaceToClearSideBorder + borderWidth,
                        this.yPositionOnScreen + spaceToClearTopBorder + Game1.tileSize * 3,
                        Game1.tileSize * 2,
                        Game1.tileSize
                    ),
                    "fertilizerQuality",
                    helper.Translation.Get("fertilizer-type") + ": "
                )
            );
            Options.Add(
                new ClickableComponent(
                    new Rectangle(
                        this.xPositionOnScreen + spaceToClearSideBorder + borderWidth + Game1.tileSize * 7 + Game1.tileSize / 4,
                        this.yPositionOnScreen + spaceToClearTopBorder + Game1.tileSize * 3,
                        Game1.tileSize * 2,
                        Game1.tileSize
                    ),
                    "fertilizerQuality",
                    fertilizerQuality.ToString()
                )
            );
        }

        private void setUpSeedsOptionPositions()
        {
            Labels.Add(
                new ClickableComponent(
                    new Rectangle(
                        this.xPositionOnScreen + spaceToClearSideBorder + borderWidth,
                        this.yPositionOnScreen + spaceToClearTopBorder + Game1.tileSize * 4,
                        Game1.tileSize * 2,
                        Game1.tileSize
                    ),
                    "payForSeeds",
                    helper.Translation.Get("pay-for-seeds") + ": "
                )
            );
            Options.Add(
                new ClickableComponent(
                    new Rectangle(
                        this.xPositionOnScreen + spaceToClearSideBorder + borderWidth + Game1.tileSize * 7 + Game1.tileSize / 4,
                        this.yPositionOnScreen + spaceToClearTopBorder + Game1.tileSize * 4,
                        Game1.tileSize * 2,
                        Game1.tileSize
                        ),
                    "payForSeeds",
                    payForSeeds.ToString()
                    )
            );
        }

        private void setUpFertilizerOptionPositions()
        {
            Labels.Add(
                                              new ClickableComponent(
                                                                                                       new Rectangle(
                                                                                                                                                                                           this.xPositionOnScreen + spaceToClearSideBorder + borderWidth,
                                                                                                                                                                                                                                                                                                  this.yPositionOnScreen + spaceToClearTopBorder + Game1.tileSize * 5,
                                                                                                                                                                                                                                                                                                                                                                                                                                Game1.tileSize * 2,
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     Game1.tileSize
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             ),
                                                                                                                                                                               "payForFertilizer",
                                                                                                                                                                                                                                                                          helper.Translation.Get("pay-for-fertilizer") + ": "
                                                                                                                                                                                                                                                                                                                                                                                    )
                                                                                       );
            Options.Add(
                                              new ClickableComponent(
                                                                                                       new Rectangle(
                                                                                                                                                                                           this.xPositionOnScreen + spaceToClearSideBorder + borderWidth + Game1.tileSize * 7 + Game1.tileSize / 4,
                                                                                                                                                                                                                                                                                                  this.yPositionOnScreen + spaceToClearTopBorder + Game1.tileSize * 5,
                                                                                                                                                                                                                                                                                                                                                                                                                                Game1.tileSize * 2,
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     Game1.tileSize
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             ),
                                                                                                                                                                               "payForFertilizer",
                                                                                                                                                                                                                                                                          payForFertilizer.ToString()
                                                                                                                                                                                                                                                                                                                                                                                    )
                                                                                       );
        }

        private void setUpMoneyOptionPositions()
        {
            Labels.Add(
                new ClickableComponent(
                    new Rectangle(
                        this.xPositionOnScreen + spaceToClearSideBorder + borderWidth,
                        this.yPositionOnScreen + spaceToClearTopBorder + Game1.tileSize * 6,
                        Game1.tileSize * 2,
                        Game1.tileSize
                    ),
                    "maxMoney",
                    helper.Translation.Get("max-money") + ": "
                )
            );
            Options.Add(
                new ClickableComponent(
                    new Rectangle(
                        this.xPositionOnScreen + spaceToClearSideBorder + borderWidth + Game1.tileSize * 7 + Game1.tileSize / 4,
                        this.yPositionOnScreen + spaceToClearTopBorder + Game1.tileSize * 6,
                        Game1.tileSize * 2,
                        Game1.tileSize
                        ),
                        "maxMoney",
                        maxMoney.ToString()
                    )
                );
        }

        private void setUpBaseStatsOptionPositions()
        {
            Labels.Add(
                               new ClickableComponent(
                                                      new Rectangle(
                                                                                 this.xPositionOnScreen + spaceToClearSideBorder + borderWidth,
                                                                                                        this.yPositionOnScreen + spaceToClearTopBorder + Game1.tileSize * 7,
                                                                                                                               Game1.tileSize * 2,
                                                                                                                                                      Game1.tileSize
                                                                                                                                                                         ),
                                                                         "useBaseStats",
                                                                         helper.Translation.Get("base-stats") + ": "
                                                                                                           )
                                          );
            Options.Add(
                               new ClickableComponent(
                                                      new Rectangle(
                                                                                 this.xPositionOnScreen + spaceToClearSideBorder + borderWidth + Game1.tileSize * 7 + Game1.tileSize / 4,
                                                                                                        this.yPositionOnScreen + spaceToClearTopBorder + Game1.tileSize * 7,
                                                                                                                               Game1.tileSize * 2,
                                                                                                                                                      Game1.tileSize
                                                                                                                                                                         ),
                                                                         "useBaseStats",
                                                                                            useBaseStats.ToString()
                                                                                                           )
                                          );
        }
        */

        public override void draw(SpriteBatch b)
        {
            if (!Game1.options.showMenuBackground)
                b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.4f);
            Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, widthOnScreen, heightOnScreen, speaker: false, drawOnlyBox: true);

            // Draw Labels and Options and buttons
            this.drawActions(b);
            this.drawLabels(b);
            this.drawOptions(b);

            if (shouldDrawCloseButton()) base.draw(b);
            if (!Game1.options.hardwareCursor) b.Draw(Game1.mouseCursors, new Vector2(Game1.getMouseX(), Game1.getMouseY()), Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, Game1.options.gamepadControls ? 44 : 0, 16, 16), Color.White, 0f, Vector2.Zero, 4f + Game1.dialogueButtonScale / 150f, SpriteEffects.None, 1f);
        }

        private void drawActions(SpriteBatch b)
        {
            // Draw the calculate button.
            IClickableMenu.drawTextureBox
            (
                b,
                Game1.mouseCursors,
                new Rectangle(432, 439, 9, 9),
                calculateButton.bounds.X,
                calculateButton.bounds.Y,
                calculateButton.bounds.Width,
                calculateButton.bounds.Height,
                (calculateButton.scale != 1.0001f) ? Color.Wheat : Color.White,
                4f,
                false
            );
            b.DrawString
            (
                Game1.smallFont,
                calculateButton.label,
                new Vector2
                (
                    (float)calculateButton.bounds.X
                        + (calculateButton.bounds.Width / 2)
                        - (Game1.smallFont.MeasureString(calculateButton.label).X / 2),
                    (float)calculateButton.bounds.Y
                        + (calculateButton.bounds.Height / 2)
                        - (Game1.smallFont.MeasureString(calculateButton.name).Y / 2)
                ),
                Game1.textColor
            );

            // Draw the reset button.
            IClickableMenu.drawTextureBox
            (
                b,
                Game1.mouseCursors,
                new Rectangle(432, 439, 9, 9),
                resetButton.bounds.X,
                resetButton.bounds.Y,
                resetButton.bounds.Width,
                resetButton.bounds.Height,
                (resetButton.scale != 1.0001f) ? Color.Wheat : Color.White,
                4f,
                false
            );
            b.DrawString
            (
                Game1.smallFont,
                resetButton.label,
                new Vector2
                (
                    (float)resetButton.bounds.X
                        + (resetButton.bounds.Width / 2)
                        - (Game1.smallFont.MeasureString(resetButton.label).X / 2),
                    (float)resetButton.bounds.Y
                        + (resetButton.bounds.Height / 2)
                        - (Game1.smallFont.MeasureString(resetButton.name).Y / 2)
                ),
                Game1.textColor
            );
        }

        private void drawLabels(SpriteBatch b)
        {
            foreach (ClickableComponent label in Labels)
            {
                /*               IClickableMenu.drawTextureBox(
                                   b,
                                   Game1.mouseCursors,
                                   new Rectangle(432, 439, 9, 9),
                                   label.bounds.X,
                                   label.bounds.Y,
                                   label.bounds.Width,
                                   label.bounds.Height,
                                   (label.scale != 1.0001f) ? Color.Wheat : Color.White,
                                   4f,
                                   false
                               );*/
                b.DrawString(
                    Game1.dialogueFont,
                    label.label,
                    new Vector2(
                        (float)label.bounds.X,
                        (float)label.bounds.Y + (label.bounds.Height / 2) - (Game1.smallFont.MeasureString(label.name).Y / 2)
                    ),
                    Game1.textColor
                );
            }
        }

        private void drawOptions(SpriteBatch b)
        {
            foreach (BaseOption option in Options)
            {
                option.Draw(b);
            }
        }

        public override void receiveKeyPress(Keys key)
        {
            switch (key)
            {
                case Keys.Enter:
                    DoCalculation();
                    Game1.playSound("select");
                    break;

                case Keys.Escape:
                    exitThisMenu();
                    break;
            }
        }

        public override void performHoverAction(int x, int y)
        {
            //TODO: add hover actions for buttons
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            /*            foreach (ClickableComponent button in numpad)
                        {
                            if (button.containsPoint(x, y))
                            {
                                if (currentInput)
                                    inputB += button.name;
                                else
                                    inputA += button.name;
                                Game1.playSound("smallSelect");
                            }
                        }
                        foreach (ClickableComponent button in opButtons)
                        {
                            if (button.containsPoint(x, y))
                            {
                                switch (button.name)
                                {
                                    case "+":
                                        if (!currentInput)
                                            currentInput = true;
                                        op = Operation.Add;
                                        break;

                                    case "-":
                                        if (!currentInput)
                                            currentInput = true;
                                        op = Operation.Subtract;
                                        break;

                                    case "X":
                                        if (!currentInput)
                                            currentInput = true;
                                        op = Operation.Multiply;
                                        break;

                                    case "/":
                                        if (!currentInput)
                                            currentInput = true;
                                        op = Operation.Divide;
                                        break;

                                    case "EQ":
                                        DoCalculation();
                                        break;

                                    case ".":
                                        if (!currentInput)
                                            inputA += ".";
                                        else
                                            inputB += ".";
                                        break;
                                }
                                Game1.playSound("smallSelect");
                            }
                        }
                        if (zeroButton.containsPoint(x, y))
                        {
                            if (!currentInput)
                                inputA += "0";
                            else
                                inputB += "0";
                            Game1.playSound("smallSelect");
                        }*/
            if (calculateButton.containsPoint(x, y))
            {
                this.DoCalculation();
                if (playSound) Game1.playSound("select");
            }
            if (resetButton.containsPoint(x, y))
            {
                this.resetMenu();
                if (playSound) Game1.playSound("dialogueCharacterClose");
            }
            //for each option, check if it was clicked
            foreach (BaseOption option in Options)
            {
                option.ReceiveLeftClick(x, y);
            }
        }

        private void resetMenu()
        {
            //set all the options to default values
            //get day from game
            Day = (uint)Game1.dayOfMonth;
            Season = (Season)Season.Parse(typeof(Season), Game1.currentSeason, true);
            ProduceType = Helpers.ProduceType.Raw;
            FertilizerQuality = Helpers.FertilizerQuality.None;
            PayForSeeds = true;
            PayForFertilizer = false;
            MaxMoney = (uint)Game1.player.team.money.Value;
            UseBaseStats = false;
            this.updateMenu();
        }

        private void DoCalculation()
        {
            monitor.Log("Doing Calculation", LogLevel.Debug);
        }
    }
}