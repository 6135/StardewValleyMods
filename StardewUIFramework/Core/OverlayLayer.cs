using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace UIFramework.Core
{
    /// <summary>
    /// The second draw pass of a menu and the first stop for input.
    /// <list type="bullet">
    /// <item><b>Popups</b> (an open dropdown list) are persistent: they draw above everything, get first pick at
    /// clicks / hover / wheel, and a click outside closes them and is swallowed. Only one popup is open at a time.</item>
    /// <item><b>Frame draws</b> are registered during the normal pass (tooltips, <c>OnDrawOverlay</c>, custom
    /// components that want overlay) and run once, after the tree, in registration order.</item>
    /// </list>
    /// </summary>
    internal sealed class OverlayLayer
    {
        private readonly List<UIElement> popups = new();
        private readonly List<Action<SpriteBatch>> frameDraws = new();

        internal bool HasPopups => popups.Count > 0;

        internal IReadOnlyList<UIElement> Popups => popups;

        /// <summary>Open <paramref name="element"/>'s popup, closing any other.</summary>
        internal void OpenPopup(UIElement element)
        {
            foreach (UIElement other in popups.ToArray())
            {
                if (other != element)
                {
                    popups.Remove(other);
                    other.ClosePopup();
                }
            }
            if (!popups.Contains(element))
            {
                popups.Add(element);
            }
        }

        /// <summary>Forget the popup (the element already knows it is closed).</summary>
        internal void RemovePopup(UIElement element) => popups.Remove(element);

        internal void CloseAll()
        {
            foreach (UIElement popup in popups.ToArray())
            {
                popups.Remove(popup);
                popup.ClosePopup();
            }
        }

        /// <summary>Queue a draw callback for the overlay pass of the current frame.</summary>
        internal void RegisterDraw(Action<SpriteBatch> draw) => frameDraws.Add(draw);

        internal void Draw(SpriteBatch b)
        {
            for (int i = 0; i < popups.Count; i++)
            {
                popups[i].DrawPopup(b);
            }

            for (int i = 0; i < frameDraws.Count; i++)
            {
                frameDraws[i](b);
            }

            frameDraws.Clear();
        }

        /// <summary>Drop queued frame draws without drawing (menu closed mid-frame).</summary>
        internal void DiscardFrame() => frameDraws.Clear();

        /// <summary>The popup under the point, if any (top-most first).</summary>
        internal UIElement? PopupAt(int x, int y)
        {
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                if (popups[i].PopupBounds.Contains(x, y))
                {
                    return popups[i];
                }
            }
            return null;
        }

        /// <summary>Route a click. Returns true if the overlay consumed it (hit a popup, or closed one).</summary>
        internal bool TryHandleClick(UIClickEvent e)
        {
            if (popups.Count == 0)
            {
                return false;
            }

            UIElement? popup = PopupAt(e.X, e.Y);
            if (popup != null)
            {
                popup.HandlePopupClick(e);
                e.Handled = true;
                return true;
            }
            // clicking outside closes and swallows
            CloseAll();
            e.Handled = true;
            return true;
        }

        internal bool TryHandleHover(int x, int y)
        {
            if (popups.Count == 0)
            {
                return false;
            }

            UIElement? popup = PopupAt(x, y);
            if (popup == null)
            {
                return false;
            }

            popup.HandlePopupHover(x, y);
            return true;
        }

        internal bool TryHandleScroll(int direction, int x, int y)
        {
            if (popups.Count == 0)
            {
                return false;
            }
            // an open popup owns the wheel wherever the cursor is
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                if (popups[i].HandlePopupScroll(direction))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
