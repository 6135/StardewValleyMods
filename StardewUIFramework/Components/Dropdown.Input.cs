using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    internal sealed partial class Dropdown
    {
        // ---------------------------------------------------------------------------------------------------------
        //  Input
        // ---------------------------------------------------------------------------------------------------------

        protected internal override bool HandleClick(UIClickEvent e)
        {
            if (e.Target == this && e.Button == UIMouseButton.Left && Enabled)
            {
                Open(byMouse: true);
                base.HandleClick(e);
                return true;
            }
            return base.HandleClick(e);
        }

        protected internal override bool HandleActivate()
        {
            if (!Enabled || !Visible)
            {
                return false;
            }

            if (IsOpen)
            {
                CommitHighlightAndClose();
            }
            else
            {
                Open();
            }

            return true;
        }

        protected internal override bool HandleKey(UIKeyEvent e)
        {
            // the consumer's OnKey first, as on every other component
            if (base.HandleKey(e))
            {
                return true;
            }

            SyncChoices();
            return Enabled && choices.Length > 0 && HandleNavigationKey(e.Key);
        }

        /// <summary>Up / Down move the selection (closed) or the highlight (open); Enter / Space commit the highlight while open.</summary>
        private bool HandleNavigationKey(Keys key)
        {
            switch (key)
            {
                case Keys.Up:
                case Keys.Down:
                    int delta = key == Keys.Up ? -1 : 1;
                    if (IsOpen)
                    {
                        MoveHighlight(delta);
                    }
                    else
                    {
                        Commit(Math.Clamp(SelectedIndex + delta, 0, choices.Length - 1));
                    }

                    return true;

                case Keys.Enter:
                case Keys.Space:
                    if (!IsOpen)
                    {
                        return false;
                    }

                    CommitHighlightAndClose();
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>Move the open list's highlight by <paramref name="delta"/> rows, scrolling so it stays visible.</summary>
        private void MoveHighlight(int delta)
        {
            int current = highlightIndex >= 0 ? highlightIndex : SelectedIndex;
            highlightIndex = Math.Clamp(current + delta, 0, choices.Length - 1);
            if (highlightIndex < ActivePosition)
            {
                ActivePosition = highlightIndex;
            }
            else if (highlightIndex >= ActivePosition + maxVisible)
            {
                ActivePosition = highlightIndex - maxVisible + 1;
            }
            else
            {
                // highlight already visible: keep the scroll position
            }
        }

        /// <summary>
        /// Gamepad d-pad while the list is open: move the highlight and keep the cursor on it, so A (a click at the cursor)
        /// picks it, like the vanilla <c>OptionsDropDown</c>. The list keeps the d-pad until it closes.
        /// </summary>
        protected internal override bool HandleGamepadDirection(int dx, int dy)
        {
            if (!IsOpen)
            {
                return false;
            }

            if (dy != 0)
            {
                MoveHighlight(dy);
                SnapCursorToHighlight();
            }

            return true;
        }

        /// <summary>Put the cursor on the highlighted row (it is always visible: <see cref="MoveHighlight"/> scrolls to it).</summary>
        private void SnapCursorToHighlight()
        {
            int index = highlightIndex >= 0 ? highlightIndex : SelectedIndex;
            if (index < ActivePosition || index >= ActivePosition + maxVisible)
            {
                return;
            }

            Rectangle list = ListBounds;
            int reserved = HasOverflow ? IndicatorWidth + IndicatorGap : 0;
            Game1.setMousePosition(list.X + ((list.Width - reserved) / 2), list.Y + ((index - ActivePosition) * RowHeight) + (RowHeight / 2));
        }

        /// <summary>Choice index of the list row under the point, or -1.</summary>
        private int RowAt(int px, int py)
        {
            Rectangle list = ListBounds;
            if (!list.Contains(px, py) || IndicatorBounds(list).Contains(px, py))
            {
                return -1;
            }

            int index = ActivePosition + ((py - list.Y) / RowHeight);
            return index >= 0 && index < choices.Length ? index : -1;
        }

        protected internal override void HandlePopupClick(UIClickEvent e)
        {
            if (e.Button == UIMouseButton.Left && HandleIndicatorClick(e.X, e.Y))
            {
                // scrolling through the indicator keeps the list open
                return;
            }

            if (e.Button == UIMouseButton.Left)
            {
                Commit(RowAt(e.X, e.Y));
            }

            Close();
        }

        protected internal override void HandlePopupHover(int px, int py)
        {
            int row = RowAt(px, py);
            if (row >= 0)
            {
                highlightIndex = row;
            }
        }

        protected internal override bool HandlePopupScroll(int direction)
        {
            if (!IsOpen)
            {
                return false;
            }

            ActivePosition -= Math.Sign(direction);
            if (OnScroll != null)
            {
                Action<int> cb = OnScroll;
                Raise("OnScroll", () => cb(direction));
            }
            return true;
        }

        protected internal override void HandleFocusLost()
        {
            Close();
            base.HandleFocusLost();
        }
    }
}
