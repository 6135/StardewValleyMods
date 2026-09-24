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
        private readonly PlayerLayoutController layout; // HUD: player-owned layout
        private bool closed;

        internal UIMenu Menu { get; }

        internal MenuHost(UIMenu menu) : base(0, 0, 100, 100, showUpperRightCloseButton: false)
        {
            Menu = menu;
            layout = new PlayerLayoutController(menu);
            SyncCloseButton();
        }

        /// <summary>Copy the model's bounds onto the IClickableMenu fields (after layout).</summary>
        internal void SyncBounds()
        {
            Rectangle r = Menu.Bounds;
            xPositionOnScreen = r.X;
            yPositionOnScreen = r.Y;
            width = r.Width;
            height = r.Height;
            SyncCloseButton();
            layout.SyncButtons(upperRightCloseButton);
            allClickableComponents = null; // rebuilt lazily for gamepad snapping
        }

        internal void SyncCloseButton()
        {
            if (Menu.ShowCloseButton && Menu.DrawBox)
            {
                initializeUpperRightCloseButton();
            }
            else
            {
                upperRightCloseButton = null;
            }
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
            {
                base.draw(b);
            }

            layout.Draw(b);
            drawMouse(b);
        }

        public override bool shouldDrawCloseButton() => Menu.ShowCloseButton && Menu.DrawBox && upperRightCloseButton != null;

        public override bool readyToClose() => true;

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            if (Inspector.Enabled)
            {
                Inspector.HandleHover(Menu, x, y);
                return;
            }

            layout.Hover(x, y);
            if (!Menu.Collapsed)
            {
                Menu.Router.Hover(x, y);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Mouse
        // ---------------------------------------------------------------------------------------------------------

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (Inspector.Enabled)
            {
                Inspector.HandleClick(Menu, x, y);
                return;
            }

            if (upperRightCloseButton != null && shouldDrawCloseButton() && upperRightCloseButton.containsPoint(x, y))
            {
                if (playSound)
                {
                    Game1.playSound(closeSound);
                }

                Menu.Close();
                return;
            }

            bool handled = !Menu.Collapsed && Menu.Router.Click(x, y, UIMouseButton.Left);
            if (!handled && layout.TryBegin(x, y))
            {
                return;
            }

            if (!handled && !Menu.Modal && !Menu.Bounds.Contains(x, y) && !Menu.Overlay.HasPopups)
            {
                Menu.Close();
            }
        }

        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
            if (Inspector.Enabled)
            {
                return; // the inspector owns the mouse
            }

            if (!Menu.Collapsed)
            {
                Menu.Router.Click(x, y, UIMouseButton.Right);
            }
        }

        public override void leftClickHeld(int x, int y)
        {
            base.leftClickHeld(x, y);
            if (layout.IsInteracting)
            {
                layout.Held(x, y);
            }
            else
            {
                Menu.Router.ClickHeld(x, y);
            }
        }

        public override void releaseLeftClick(int x, int y)
        {
            base.releaseLeftClick(x, y);
            if (layout.IsInteracting)
            {
                layout.Released();
            }
            else
            {
                Menu.Router.ClickReleased(x, y);
            }
        }

        public override void receiveScrollWheelAction(int direction)
        {
            if (!Menu.Collapsed)
            {
                Menu.Router.Scroll(direction);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Keyboard / gamepad
        // ---------------------------------------------------------------------------------------------------------

        public override void receiveKeyPress(Keys key)
        {
            (bool shift, bool ctrl, bool alt) = ReadModifiers();
            if (Inspector.Enabled)
            {
                Inspector.HandleKey(Menu, key, shift, ctrl);
                return;
            }

            if (!Menu.Collapsed && Menu.Router.KeyPress(key, shift, ctrl, alt))
            {
                return;
            }

            // vanilla behaviour: the menu button (E / Escape) closes, unless a text field is taking input
            if (Menu.Focus.Focused?.WantsTextInput == true)
            {
                return;
            }

            if (Game1.options.doesInputListContain(Game1.options.menuButton, key))
            {
                CloseIfAllowed();
            }
            else if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            {
                applyMovementKey(key);
            }
            else
            {
                // unbound key: nothing to do
            }
        }

        private static (bool shift, bool ctrl, bool alt) ReadModifiers()
        {
            KeyboardState kb = Game1.GetKeyboardState();
            bool shift = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);
            bool ctrl = kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);
            bool alt = kb.IsKeyDown(Keys.LeftAlt) || kb.IsKeyDown(Keys.RightAlt);
            return (shift, ctrl, alt);
        }

        private void CloseIfAllowed()
        {
            if (Menu.CloseOnEscape)
            {
                Menu.Close();
            }
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
            if (layout.CollapseButton != null)
            {
                list.Add(layout.CollapseButton);
            }

            if (upperRightCloseButton != null)
            {
                list.Add(upperRightCloseButton);
            }

            allClickableComponents = list;
        }

        public override void snapToDefaultClickableComponent()
        {
            if (allClickableComponents == null)
            {
                populateClickableComponentList();
            }

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
            Menu.ResetPosition(); // re-place from the anchor in the new viewport
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
            {
                return;
            }

            closed = true;
            if (_childMenu is MenuHost child)
            {
                child.Menu.Close();
            }

            Menu.OnHostClosed(this);
        }

        /// <summary>Whether this host is the game's active menu (as opposed to a child menu).</summary>
        internal bool IsActiveMenu => Game1.activeClickableMenu == this;

        /// <summary>Whether this host is still reachable from the game's active menu chain.</summary>
        internal bool IsStillActive()
        {
            for (IClickableMenu? m = Game1.activeClickableMenu; m != null; m = m.GetChildMenu())
            {
                if (m == this)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Mark closed without going through the game (the game already dropped this host).</summary>
        internal void NotifyDropped() => HandleClosed();
    }
}
