using System;
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
    /// <remarks>
    /// The host is closed only when the game closes it: <c>exitThisMenu</c> / <c>cleanupBeforeExit</c>, an emergency
    /// shutdown, or <see cref="Dispose"/>, which the <c>Game1.activeClickableMenu</c> setter calls on the menu it replaces.
    /// A host that is merely not the active menu (another mod showed its own menu on top by setting the game's field
    /// directly, like Lookup Anything's search) stays open and resumes when that mod puts it back.
    /// <para>
    /// Gamepad input uses the game's snappy-menu machinery, as vanilla menus do: the focusable elements are the
    /// <see cref="IClickableMenu.allClickableComponents"/>, the d-pad / stick snap the cursor between them through
    /// <see cref="IClickableMenu.applyMovementKey(Keys)"/>, and the game turns the A button into a click at the cursor.
    /// </para>
    /// </remarks>
    internal sealed class MenuHost : IClickableMenu, IDisposable
    {
        private readonly PlayerLayoutController layout; // HUD: player-owned layout
        private bool closed;

        /// <summary>Snap target id of the collapse button (element targets are numbered from 0).</summary>
        private const int CollapseButtonId = 10000;

        /// <summary>Snap target id of the close button.</summary>
        private const int CloseButtonId = 10001;

        /// <summary>No neighbour in that direction (the game then keeps the cursor where it is).</summary>
        private const int NoNeighbor = -1;

        /// <summary>The element behind each snap target of <see cref="IClickableMenu.allClickableComponents"/>.</summary>
        private readonly Dictionary<ClickableComponent, UIElement> snapTargets = new();

        /// <summary>The focusable element that supplied each snap target through <see cref="UIElement.GamepadTargets"/> (a list for its rows).</summary>
        private readonly Dictionary<UIElement, UIElement> targetOwners = new();

        /// <summary>Snap the gamepad cursor on the next update, once the menu's first tick built its content (data rows).</summary>
        private bool snapPending;

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
            if (snapPending)
            {
                snapPending = false;
                if (Game1.options.SnappyMenus)
                {
                    populateClickableComponentList();
                    snapToDefaultClickableComponent();
                }
            }
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
                RequestClose(playSound);
                return;
            }

            bool handled = !Menu.Collapsed && Menu.Router.Click(x, y, UIMouseButton.Left);
            if (!handled && layout.TryBegin(x, y))
            {
                return;
            }

            if (!handled && !Menu.Modal && !Menu.Bounds.Contains(x, y) && !Menu.Overlay.HasPopups)
            {
                RequestClose(playSound);
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

            if (TryGamepadDirection(key))
            {
                return;
            }

            // gamepad B / Y / Start while a text field is focused (it was focused with the mouse, so no on-screen keyboard):
            // back out of it instead of letting the field swallow the menu button as a typing key
            bool back = key == Keys.Escape || Game1.options.doesInputListContain(Game1.options.menuButton, key);
            if (back && Menu.Focus.Focused?.WantsTextInput == true && IsGamepadBackHeld())
            {
                Menu.Router.BackOut();
                return;
            }

            if (!Menu.Collapsed && Menu.Router.KeyPress(key, shift, ctrl, alt))
            {
                return;
            }

            // vanilla behaviour: the menu button (E / Escape, gamepad B) closes, unless a text field is taking input;
            // first it backs out of an open popup, the focused element or the cancel button
            if (Menu.Focus.Focused?.WantsTextInput == true)
            {
                return;
            }

            if (back && (Menu.Collapsed || !Menu.Router.BackOut()))
            {
                CloseIfAllowed();
            }
        }

        /// <summary>
        /// Gamepad in the game's snappy menus: the d-pad / stick arrive as the movement keys (<c>Utility.mapGamePadButtonToKey</c>).
        /// An open popup takes them first (the vanilla <c>OptionsDropDown</c>), then the element under the snapped cursor
        /// (left / right on the vanilla <c>OptionsSlider</c>); otherwise the game snaps the cursor to the next element.
        /// </summary>
        private bool TryGamepadDirection(Keys key)
        {
            if (!Game1.options.SnappyMenus || !TryGetDirection(key, out int dx, out int dy))
            {
                return false;
            }

            // a focused text field types W / A / S / D; only a real d-pad / stick press leaves it (the field was focused
            // with the mouse, so no on-screen keyboard is open)
            if (Menu.Focus.Focused?.WantsTextInput == true)
            {
                if (!Game1.isDPadPressed() && !Game1.isGamePadThumbstickInMotion(0.2))
                {
                    return false;
                }

                Menu.Focus.ClearFocus();
            }

            UIElement? popup = Menu.Collapsed ? null : Menu.Overlay.TopPopup;
            if (popup != null)
            {
                popup.HandleGamepadDirection(dx, dy); // the popup keeps the d-pad while it is open
                return true;
            }

            UIElement? snapped = SnappedElement();
            if (snapped != null && snapped.HandleGamepadDirection(dx, dy))
            {
                return true;
            }

            if (snapped != null && targetOwners.TryGetValue(snapped, out UIElement? owner) && owner.HandleGamepadTargetDirection(snapped, dx, dy))
            {
                ResnapAtCursor(); // a list scrolled: snap to the row now under the cursor
                return true;
            }

            applyMovementKey(key);
            return true;
        }

        /// <summary>After the content under the cursor changed (a list scrolled), snap to the target now at the cursor.</summary>
        private void ResnapAtCursor()
        {
            if (Menu.LayoutDirty)
            {
                Menu.Relayout();
            }

            Point cursor = new(Game1.getMouseX(), Game1.getMouseY());
            populateClickableComponentList();
            ClickableComponent? atCursor = allClickableComponents.Find(c => c.containsPoint(cursor.X, cursor.Y));
            if (atCursor != null)
            {
                currentlySnappedComponent = atCursor;
            }
        }

        /// <summary>Whether a gamepad button the game maps to the menu button (B, Y, Start) is held.</summary>
        private static bool IsGamepadBackHeld()
        {
            GamePadState pad = Game1.input.GetGamePadState();
            return pad.IsButtonDown(Buttons.B) || pad.IsButtonDown(Buttons.Y) || pad.IsButtonDown(Buttons.Start);
        }

        private static bool TryGetDirection(Keys key, out int dx, out int dy)
        {
            Options options = Game1.options;
            dx = options.doesInputListContain(options.moveLeftButton, key) ? -1 : options.doesInputListContain(options.moveRightButton, key) ? 1 : 0;
            dy = options.doesInputListContain(options.moveUpButton, key) ? -1 : options.doesInputListContain(options.moveDownButton, key) ? 1 : 0;
            return dx != 0 || dy != 0;
        }

        /// <summary>The element behind the snapped cursor target, or null (nothing snapped, or a window button).</summary>
        private UIElement? SnappedElement()
        {
            return currentlySnappedComponent != null && snapTargets.TryGetValue(currentlySnappedComponent, out UIElement? element) ? element : null;
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
                RequestClose(playSound: true);
            }
        }

        /// <summary>
        /// Close this host because the player asked to (with the vanilla close sound when <paramref name="playSound"/>):
        /// through its menu when it is the menu's current host, otherwise (the menu was closed or reopened elsewhere while
        /// this host was hidden) just take this host off the screen.
        /// </summary>
        private void RequestClose(bool playSound)
        {
            if (playSound)
            {
                PlayCloseSound();
            }

            if (Menu.Host == this)
            {
                Menu.Close();
            }
            else
            {
                exitThisMenu(playSound: false);
            }
        }

        /// <summary>
        /// Build the snap targets from the focusable elements (a list supplies its rows, see <see cref="UIElement.GamepadTargets"/>;
        /// none while collapsed) plus the window buttons, and wire their neighbours like a vanilla menu does. Layout changes
        /// drop the list (<see cref="SyncBounds"/>) and the game rebuilds it on the next move; the snapped element stays
        /// snapped across rebuilds.
        /// </summary>
        public override void populateClickableComponentList()
        {
            UIElement? snappedElement = SnappedElement();
            int snappedId = currentlySnappedComponent?.myID ?? NoNeighbor;
            snapTargets.Clear();
            targetOwners.Clear();

            var list = new List<ClickableComponent>();
            if (!Menu.Collapsed)
            {
                foreach (UIElement element in Menu.Focus.FocusableElements())
                {
                    if (element.GamepadTargets is { } targets)
                    {
                        foreach (UIElement target in targets)
                        {
                            AddTarget(list, target);
                            targetOwners[target] = element;
                        }
                    }
                    else
                    {
                        AddTarget(list, element);
                    }
                }
            }

            if (layout.CollapseButton != null)
            {
                layout.CollapseButton.myID = CollapseButtonId;
                list.Add(layout.CollapseButton);
            }

            if (upperRightCloseButton != null)
            {
                upperRightCloseButton.myID = CloseButtonId;
                list.Add(upperRightCloseButton);
            }

            AssignNeighbors(list);
            allClickableComponents = list;
            if (currentlySnappedComponent != null)
            {
                currentlySnappedComponent = snappedElement != null
                    ? list.Find(c => snapTargets.TryGetValue(c, out UIElement? e) && e == snappedElement)
                    : list.Find(c => !snapTargets.ContainsKey(c) && c.myID == snappedId);
            }
        }

        private void AddTarget(List<ClickableComponent> list, UIElement element)
        {
            var component = new ClickableComponent(element.Bounds, element.Id) { myID = snapTargets.Count };
            list.Add(component);
            snapTargets[component] = element;
        }

        /// <summary>
        /// Set each target's up / down / left / right neighbour, as vanilla menus do by hand. The neighbour is the nearest
        /// target ahead that overlaps it across the direction of travel (the next row of a form column, whatever the width
        /// of its controls); up / down fall back to the nearest target ahead when nothing overlaps (a button row off to one
        /// side), left / right do not.
        /// </summary>
        private static void AssignNeighbors(List<ClickableComponent> list)
        {
            foreach (ClickableComponent from in list)
            {
                from.upNeighborID = FindNeighbor(list, from, 0, -1);
                from.downNeighborID = FindNeighbor(list, from, 0, 1);
                from.leftNeighborID = FindNeighbor(list, from, -1, 0);
                from.rightNeighborID = FindNeighbor(list, from, 1, 0);
            }
        }

        private static int FindNeighbor(List<ClickableComponent> list, ClickableComponent from, int dx, int dy)
        {
            Rectangle a = from.bounds;
            bool vertical = dy != 0;
            ClickableComponent? best = null;
            float bestScore = float.MaxValue;
            bool bestOverlaps = false;
            foreach (ClickableComponent c in list)
            {
                if (c == from)
                {
                    continue;
                }

                Rectangle b = c.bounds;
                float along = vertical ? (b.Center.Y - a.Center.Y) * dy : (b.Center.X - a.Center.X) * dx;
                if (along <= 0)
                {
                    continue;
                }

                float across = Math.Abs(vertical ? b.Center.X - a.Center.X : b.Center.Y - a.Center.Y);
                int overlap = vertical
                    ? Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left)
                    : Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top);
                bool overlaps = overlap > 0;
                if (!overlaps && (!vertical || bestOverlaps))
                {
                    continue;
                }

                // overlapping: the smallest gap between the edges (ties: the most aligned); otherwise the closest by centers
                float gap = vertical ? (dy > 0 ? b.Top - a.Bottom : a.Top - b.Bottom) : (dx > 0 ? b.Left - a.Right : a.Left - b.Right);
                float score = overlaps ? (Math.Max(0, gap) * 1000) + across : along + (2 * across);
                if ((overlaps && !bestOverlaps) || score < bestScore)
                {
                    best = c;
                    bestScore = score;
                    bestOverlaps = overlaps;
                }
            }

            return best?.myID ?? NoNeighbor;
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

        /// <summary>Scroll the snapped element into view (it may be outside a scroll view's viewport), then move the cursor onto it.</summary>
        public override void snapCursorToCurrentSnappedComponent()
        {
            if (currentlySnappedComponent != null && snapTargets.TryGetValue(currentlySnappedComponent, out UIElement? element))
            {
                FocusManager.ScrollIntoView(element);
                if (Menu.LayoutDirty)
                {
                    Menu.Relayout();
                }

                currentlySnappedComponent.bounds = element.Bounds;
            }

            base.snapCursorToCurrentSnappedComponent();
        }

        /// <summary>The vanilla menu close sound (<see cref="IClickableMenu.closeSound"/>).</summary>
        internal void PlayCloseSound() => Game1.playSound(closeSound);

        /// <summary>
        /// When the menu opens with a gamepad, snap the cursor to the first element (vanilla menus do it in their constructor).
        /// It happens on the first update, after the menu's first tick filled data-driven content such as a grid's rows.
        /// </summary>
        internal void SnapForGamepad() => snapPending = true;

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

        /// <summary>Called by the <c>Game1.activeClickableMenu</c> setter when another menu replaces this one.</summary>
        public void Dispose() => HandleClosed();

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
    }
}
