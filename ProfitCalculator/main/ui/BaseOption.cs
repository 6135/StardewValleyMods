using CoreUtils.management.memory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System;

namespace ProfitCalculator.main.ui
{
    /// <summary>
    ///  Base class for all options in the options menu. This might be usefull for other mods. Might clean this up later and make it a seperate mod or framework.
    /// </summary>
    public abstract class BaseOption : ClickableComponent
    {
        /// <summary>
        /// The helper for the mod
        /// </summary>
        protected readonly IModHelper Helper = Container.Instance.GetInstance<IModHelper>(ModEntry.UniqueID);

        /// <summary> The sound to play when the option is clicked, or <c>null</c> to play no sound. </summary>
        public virtual string ClickedSound => null;

        /// <summary> Whether the option was clicked by the cursor. </summary>
        protected bool Clicked;

        /// <summary> The sound to play when the cursor hovers on the option, or <c>null</c> to play no sound. </summary>
        public virtual string HoveredSound => null;

        /// <summary> Whether the option is currently hovered by the cursor. </summary>
        public bool Hover { get; }

        /// <summary>
        /// If the option was clicked by a left click
        /// </summary>
        public bool ClickGestured { get; }

        /// <summary>The tooltip text shown when the cursor hovers on the field, or <c>null</c> to disable the tooltip.</summary>
        public Func<string> Tooltip { get; }

        /// <summary>The DisplayName to show in the form.</summary>
        public Func<string> Name { get; set; }

        /// <summary>The Label to show in the form.</summary>
        public Func<string> Label { get; set; }

        /// <summary> The position of the clickable component in Vector2 format for easy access</summary>
        public Vector2 Position
        {
            get => new(bounds.X, bounds.Y);
            set
            {
                bounds.X = (int)value.X;
                bounds.Y = (int)value.Y;
            }
        }

        /// <summary>
        /// Creates a new BaseOption
        /// </summary>
        /// <param name="x"> The x position of the clickable component</param>
        /// <param name="y"> The y position of the clickable component</param>
        /// <param name="w"> The width of the clickable component</param>
        /// <param name="h"> The height of the clickable component</param>
        /// <param name="name"> The name of the clickable component</param>
        /// <param name="label"> The label of the clickable component</param>
        /// <param name="tooltip"> The tooltip of the clickable component</param>
        protected BaseOption(int x, int y, int w, int h, Func<string> name, Func<string> label, Func<string> tooltip) :
            base(new Rectangle(x, y, w, h), name(), label())
        {
            Tooltip = tooltip;
            Name = name;
            Label = label;
        }

        /// <summary>
        /// Draws the option to the screen. Abstract so it can be overriden by subclasses
        /// </summary>
        /// <param name="b"> The SpriteBatch to draw to</param>
        public abstract void Draw(SpriteBatch b);

        /// <summary>
        /// Behaviour before executing the left click action itself. Abstract so it can be overriden by subclasses
        /// </summary>
        /// <param name="x"> The x position of the mouse</param>
        /// <param name="y"> The y position of the mouse</param>
        public abstract void BeforeReceiveLeftClick(int x, int y);

        /// <summary>
        /// Behaviour for the left click action. Implemented here so it can be used by subclasses.
        /// </summary>
        /// <param name="x"> The x position of the mouse</param>
        /// <param name="y"> The y position of the mouse</param>
        /// <param name="stopSpread"> The action to stop the spread of the left click to Children or sibling components</param>
        public virtual void ReceiveLeftClick(int x, int y, Action stopSpread)
        {
            BeforeReceiveLeftClick(x, y);
            //check if x and y are within the bounds of the checkbox
            if (containsPoint(x, y))
                ExecuteClick();
        }

        /// <summary>
        /// Execution og left click action. Implemented here so it can be used by subclasses.
        /// </summary>
        public virtual void ExecuteClick()
        {
            Clicked = true;
            if (ClickedSound != null)
                Game1.playSound(ClickedSound);
        }

        /// <summary>
        /// Update event for the option. Abstract so it can be overriden by subclasses
        /// </summary>
        public abstract void Update();

        /// <summary>
        /// Called when the option the there's an hover action. Implemented here so it can be used by subclasses.
        /// </summary>
        /// <param name="x"> The x position of the mouse</param>
        /// <param name="y"> The y position of the mouse</param>
        public virtual void PerformHoverAction(int x, int y)
        {
        }
    }
}