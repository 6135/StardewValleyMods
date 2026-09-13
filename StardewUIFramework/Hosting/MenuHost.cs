using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Hosting
{
    /// <summary>
    /// The only game-facing class: an <see cref="IClickableMenu"/> that forwards every game callback into a
    /// <see cref="UIMenu"/>. One instance per open menu.
    /// </summary>
    internal sealed class MenuHost : IClickableMenu
    {
        private bool closed;

        public UIMenu Menu { get; }

        public MenuHost(UIMenu menu) : base(0, 0, 100, 100, showUpperRightCloseButton: false)
        {
            Menu = menu;
            SyncCloseButton();
        }

        /// <summary>Copy the model's bounds onto the IClickableMenu fields (after layout).</summary>
        public void SyncBounds()
        {
            Rectangle r = Menu.Bounds;
            xPositionOnScreen = r.X;
            yPositionOnScreen = r.Y;
            width = r.Width;
            height = r.Height;
            SyncCloseButton();
            allClickableComponents = null; // rebuilt lazily for gamepad snapping
        }

        public void SyncCloseButton()
        {
            if (Menu.ShowCloseButton && Menu.DrawBox)
                initializeUpperRightCloseButton();
            else
                upperRightCloseButton = null;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Frame
        // ---------------------------------------------------------------------------------------------------------

        public override void update(GameTime time)
        {
            base.update(time);
            Menu.Tick(time.ElapsedGameTime.TotalMilliseconds);
        }

        public override void draw(SpriteBatch b)
        {
            Menu.Draw(b);
            if (shouldDrawCloseButton())
                base.draw(b);
            drawMouse(b);
        }

        public override bool shouldDrawCloseButton() => Menu.ShowCloseButton && Menu.DrawBox && upperRightCloseButton != null;

        public override bool readyToClose() => true;

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            Menu.Router.Hover(x, y);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Mouse
        // ---------------------------------------------------------------------------------------------------------

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (upperRightCloseButton != null && shouldDrawCloseButton() && upperRightCloseButton.containsPoint(x, y))
            {
                if (playSound)
                    Game1.playSound(closeSound);
                Menu.Close();
                return;
            }

            bool handled = Menu.Router.Click(x, y, UIMouseButton.Left);
            if (!handled && !Menu.Modal && !Menu.Bounds.Contains(x, y) && !Menu.Overlay.HasPopups)
                Menu.Close();
        }

        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
            Menu.Router.Click(x, y, UIMouseButton.Right);
        }

        public override void leftClickHeld(int x, int y)
        {
            base.leftClickHeld(x, y);
            Menu.Router.ClickHeld(x, y);
        }

        public override void releaseLeftClick(int x, int y)
        {
            base.releaseLeftClick(x, y);
            Menu.Router.ClickReleased(x, y);
        }

        public override void receiveScrollWheelAction(int direction)
        {
            Menu.Router.Scroll(direction);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Keyboard / gamepad
        // ---------------------------------------------------------------------------------------------------------

        public override void receiveKeyPress(Keys key)
        {
            KeyboardState kb = Game1.GetKeyboardState();
            bool shift = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);
            bool ctrl = kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);
            bool alt = kb.IsKeyDown(Keys.LeftAlt) || kb.IsKeyDown(Keys.RightAlt);

            if (Menu.Router.Key(key, shift, ctrl, alt))
                return;

            // vanilla behaviour: the menu button (E / Escape) closes, unless a text field is taking input
            bool textFocused = Menu.Focus.Focused?.WantsTextInput == true;
            if (!textFocused && Game1.options.doesInputListContain(Game1.options.menuButton, key))
            {
                if (Menu.CloseOnEscape)
                    Menu.Close();
                return;
            }

            if (!textFocused && Game1.options.snappyMenus && Game1.options.gamepadControls)
                applyMovementKey(key);
        }

        public override void receiveGamePadButton(Buttons b)
        {
            base.receiveGamePadButton(b);
        }

        /// <summary>Build the snap targets from the focusable elements; the game snaps between them geometrically.</summary>
        public override void populateClickableComponentList()
        {
            var list = new List<ClickableComponent>();
            int id = 0;
            foreach (UIElement element in Menu.Focus.FocusableElements())
            {
                list.Add(new ClickableComponent(element.Bounds, element.Id)
                {
                    myID = id++,
                    upNeighborID = ClickableComponent.SNAP_AUTOMATIC,
                    downNeighborID = ClickableComponent.SNAP_AUTOMATIC,
                    leftNeighborID = ClickableComponent.SNAP_AUTOMATIC,
                    rightNeighborID = ClickableComponent.SNAP_AUTOMATIC
                });
            }
            if (upperRightCloseButton != null)
                list.Add(upperRightCloseButton);
            allClickableComponents = list;
        }

        public override void snapToDefaultClickableComponent()
        {
            if (allClickableComponents == null)
                populateClickableComponentList();
            if (allClickableComponents != null && allClickableComponents.Count > 0)
            {
                currentlySnappedComponent = allClickableComponents[0];
                snapCursorToCurrentSnappedComponent();
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------------------------------------------------

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            Menu.InvalidateLayout();
            Menu.Relayout();
            _childMenu?.gameWindowSizeChanged(oldBounds, newBounds);
        }

        protected override void cleanupBeforeExit()
        {
            base.cleanupBeforeExit();
            HandleClosed();
        }

        public override void emergencyShutDown()
        {
            base.emergencyShutDown();
            HandleClosed();
        }

        private void HandleClosed()
        {
            if (closed)
                return;
            closed = true;
            if (_childMenu is MenuHost child)
                child.Menu.Close();
            Menu.OnHostClosed(this);
        }

        /// <summary>Whether this host is still reachable from the game's active menu chain.</summary>
        public bool IsStillActive()
        {
            for (IClickableMenu? m = Game1.activeClickableMenu; m != null; m = m.GetChildMenu())
            {
                if (m == this)
                    return true;
            }
            return false;
        }

        /// <summary>Mark closed without going through the game (the game already dropped this host).</summary>
        public void NotifyDropped() => HandleClosed();
    }
}
