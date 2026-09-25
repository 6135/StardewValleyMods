using System;
using Microsoft.Xna.Framework.Input;
using UIFramework.Api;

namespace UIFramework.Core
{
    /// <summary>
    /// Turns the raw callbacks the game gives <see cref="Hosting.MenuHost"/> into routed UI events:
    /// overlay first → hit-test → deliver to the target → bubble to ancestors while not handled → menu-level fallback.
    /// Also owns mouse capture (drag) and the hover / tooltip bookkeeping.
    /// </summary>
    internal sealed class EventRouter
    {
        private readonly UIMenu menu;

        /// <summary>Element that received the last left click; gets held / release callbacks.</summary>
        internal UIElement? Captured { get; private set; }

        internal EventRouter(UIMenu menu)
        {
            this.menu = menu;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Mouse
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Route a click. Returns true if any element (or popup) handled it.</summary>
        internal bool Click(int x, int y, UIMouseButton button)
        {
            Captured = null;

            // 1. overlays get first pick; clicking outside an open popup closes it and swallows the click
            var probe = new UIClickEvent(null, x, y, button);
            if (menu.Overlay.TryHandleClick(probe))
            {
                return true;
            }

            // 2. hit-test (elements drawn in the overlay pass sit above the tree)
            UIElement? target = HitTest(x, y);

            // 3. focus + capture (left button only)
            if (button == UIMouseButton.Left)
            {
                FocusFromClick(target);
                Captured = target;
            }

            // 4. bubble
            return target != null && Bubble(new UIClickEvent(target, x, y, button));
        }

        /// <summary>
        /// A target that keeps taking input takes focus; clicking empty space, a non-focusable element or a click-once
        /// control (button, checkbox, dropdown, slider: <see cref="UIElement.FocusOnClick"/> false) clears it.
        /// </summary>
        private void FocusFromClick(UIElement? target)
        {
            if (target != null && target.Focusable && target.FocusOnClick)
            {
                menu.Focus.SetFocus(target);
            }
            else
            {
                menu.Focus.ClearFocus();
            }
        }

        /// <summary>Deliver a click to its target and then to each ancestor until one marks it handled.</summary>
        internal static bool Bubble(UIClickEvent e)
        {
            for (UIElement? cur = e.Target; cur != null && !e.Handled; cur = cur.ParentElement)
            {
                if (cur.HandleClick(e))
                {
                    e.Handled = true;
                }
            }

            return e.Handled;
        }

        internal void ClickHeld(int x, int y)
        {
            Captured?.HandleClickHeld(x, y);
        }

        /// <summary>Forget the capture of <paramref name="element"/> without a release callback (it left the tree).</summary>
        internal void DropCapture(UIElement element)
        {
            if (Captured == element)
            {
                Captured = null;
            }
        }

        internal void ClickReleased(int x, int y)
        {
            UIElement? captured = Captured;
            Captured = null;
            captured?.HandleClickRelease(x, y);
        }

        /// <summary>Hover pass: raise enter / leave, track the hovered element for tooltips.</summary>
        internal void Hover(int x, int y)
        {
            menu.CursorX = x;
            menu.CursorY = y;

            if (menu.Overlay.TryHandleHover(x, y))
            {
                SetHovered(null);
                return;
            }

            UIElement? target = HitTest(x, y);
            SetHovered(target);
            target?.HandleHoverMove(x, y);
        }

        internal void SetHovered(UIElement? target)
        {
            if (menu.Hovered == target)
            {
                return;
            }

            UIElement? old = menu.Hovered;
            menu.Hovered = target;

            // the tooltip delay restarts only when the tooltip changes, not when the cursor moves between the parts of
            // an element that shares its tooltip (the cells of a data grid row)
            UIElement? owner = UIMenu.TooltipOwner(target);
            if (owner == null || owner != UIMenu.TooltipOwner(old))
            {
                menu.HoverStartMs = UIServices.NowMs();
            }
            old?.HandleHoverLeave();
            target?.HandleHoverEnter();
        }

        /// <summary>Deepest element under the point: elements drawn in the overlay pass first, then the tree.</summary>
        private UIElement? HitTest(int x, int y)
        {
            UIElement? overlay = menu.Overlay.ElementAt(x, y);
            if (overlay != null)
            {
                return overlay;
            }

            return menu.Viewport.HitTest(x, y);
        }

        /// <summary>Scroll wheel: popup → hovered element chain → menu.</summary>
        internal bool Scroll(int direction)
        {
            if (menu.Overlay.TryHandleScroll(direction, menu.CursorX, menu.CursorY))
            {
                return true;
            }

            for (UIElement? cur = menu.Hovered; cur != null; cur = cur.ParentElement)
            {
                if (cur.HandleScroll(direction))
                {
                    return true;
                }
            }

            if (menu.OnScroll != null)
            {
                Action<int> cb = menu.OnScroll;
                menu.Consumer.Invoke(menu.Id, menu.Id, "OnScroll", () => cb(direction));
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Keyboard
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Key press: focused element first, bubbling up; then menu <c>OnKey</c>; then built-in bindings
        /// (Tab traversal, arrows, Enter → default button, Escape → cancel / close). Returns true if consumed.
        /// </summary>
        internal bool KeyPress(Keys key, bool shift, bool ctrl, bool alt)
        {
            UIElement? focused = menu.Focus.Focused;
            var e = new UIKeyEvent(focused, key, shift, ctrl, alt);

            for (UIElement? cur = focused; cur != null && !e.Handled; cur = cur.ParentElement)
            {
                if (cur.HandleKey(e))
                {
                    e.Handled = true;
                }
            }
            if (e.Handled)
            {
                return true;
            }

            if (menu.OnKey != null)
            {
                Func<IUIKeyEvent, bool> cb = menu.OnKey;
                if (menu.Consumer.Invoke(menu.Id, menu.Id, "OnKey", () => cb(e), false))
                {
                    return true;
                }
            }

            return HandleBuiltInKey(key, shift, focused);
        }

        /// <summary>Menu-level bindings: Tab / arrows move focus, Enter / Space activate, Escape closes popups, focus, cancel button or the menu.</summary>
        private bool HandleBuiltInKey(Keys key, bool shift, UIElement? focused)
        {
            switch (key)
            {
                case Keys.Tab:
                    return menu.Focus.MoveNext(shift);
                case Keys.Up:
                    return menu.Focus.MoveDirection(0, -1);
                case Keys.Down:
                    return menu.Focus.MoveDirection(0, 1);
                case Keys.Left:
                    return menu.Focus.MoveDirection(-1, 0);
                case Keys.Right:
                    return menu.Focus.MoveDirection(1, 0);
                case Keys.Enter:
                case Keys.Space:
                    return Activate(key, focused);
                case Keys.Escape:
                    return Escape(focused);
                default:
                    return false;
            }
        }

        /// <summary>Enter / Space: the focused element first, then the menu's default button (Space only activates buttons).</summary>
        private bool Activate(Keys key, UIElement? focused)
        {
            if (key == Keys.Space && (focused == null || !focused.ActivateOnEnter))
            {
                return false;
            }

            if (focused != null && focused.HandleActivate())
            {
                return true;
            }

            if (menu.DefaultButtonElement != null && menu.DefaultButtonElement.OwnerMenu == menu)
            {
                menu.DefaultButtonElement.HandleActivate();
                return true;
            }

            return false;
        }

        /// <summary>Escape: close an open popup, else drop focus, else the cancel button, else the menu.</summary>
        private bool Escape(UIElement? focused)
        {
            if (menu.Overlay.HasPopups)
            {
                menu.Overlay.CloseAll();
                return true;
            }

            if (focused != null)
            {
                menu.Focus.ClearFocus();
                return true;
            }

            if (menu.CancelButtonElement != null && menu.CancelButtonElement.OwnerMenu == menu)
            {
                menu.CancelButtonElement.HandleActivate();
                return true;
            }

            if (menu.CloseOnEscape)
            {
                menu.Close();
                return true;
            }

            return false;
        }
    }
}
