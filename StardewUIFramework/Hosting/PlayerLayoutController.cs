using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Hosting
{
    /// <summary>
    /// The player-owned layout of one hosted window: dragging by the title strip, the collapse button next to the
    /// close button and the resize grip in the bottom-right corner (fixed-size menus only). Changes are recorded in
    /// <see cref="UIServices.Layouts"/> when the mouse is released, and the stored layout is applied on construction.
    /// </summary>
    internal sealed class PlayerLayoutController
    {
        private const int GripSize = 44;
        private const int ButtonWidth = 44;
        private const int ButtonHeight = 48;
        private const int ButtonGap = 8;

        private enum Mode
        {
            None,
            Drag,
            Resize
        }

        private readonly UIMenu menu;
        private ClickableTextureComponent? collapseButton;
        private Mode mode;
        private Point start;
        private Point origin;
        private Point originSize;

        internal PlayerLayoutController(UIMenu menu)
        {
            this.menu = menu;
            UIServices.Layouts?.Apply(menu);
        }

        /// <summary>Whether the player may adjust this window at all (opted in and has window chrome).</summary>
        internal bool Enabled => menu.PlayerLayout && menu.DrawBox;

        /// <summary>True while a drag / resize is in progress (held / release go here instead of the tree).</summary>
        internal bool IsInteracting => mode != Mode.None;

        /// <summary>The collapse button for gamepad snapping, or null.</summary>
        internal ClickableComponent? CollapseButton => Enabled ? collapseButton : null;

        private Rectangle GripBounds => new(menu.Bounds.Right - GripSize, menu.Bounds.Bottom - GripSize, GripSize, GripSize);

        private bool CanResize => Enabled && menu.IsResizable && !menu.Collapsed;

        /// <summary>Place the collapse button left of the close button (or in its place when there is none).</summary>
        internal void SyncButtons(ClickableComponent? closeButton)
        {
            if (!Enabled)
            {
                collapseButton = null;
                return;
            }

            int right = closeButton != null ? closeButton.bounds.X - ButtonGap : menu.Bounds.Right - 36 + ButtonWidth;
            var bounds = new Rectangle(right - ButtonWidth, menu.Bounds.Y - 8, ButtonWidth, ButtonHeight);
            Rectangle source = menu.Collapsed ? Theme.ScrollDownArrow : Theme.ScrollUpArrow;
            if (collapseButton == null)
            {
                collapseButton = new ClickableTextureComponent(bounds, Game1.mouseCursors, source, Theme.PixelScale);
            }
            else
            {
                collapseButton.bounds = bounds;
                collapseButton.sourceRect = source;
            }
        }

        internal void Hover(int x, int y) => collapseButton?.tryHover(x, y, 0.5f);

        internal void Draw(SpriteBatch b)
        {
            if (!Enabled)
            {
                return;
            }

            collapseButton?.draw(b);
            if (CanResize)
            {
                DrawGrip(b);
            }
        }

        /// <summary>Three rows of dots in the corner, inside the box border.</summary>
        private void DrawGrip(SpriteBatch b)
        {
            Color color = Theme.TextColor * 0.6f;
            Rectangle grip = GripBounds;
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col <= row; col++)
                {
                    DrawHelper.Fill(b, new Rectangle(grip.Right - 20 - (col * 8), grip.Bottom - 36 + (row * 8), 4, 4), color);
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Mouse
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A left click the tree did not handle: collapse button, resize grip or title strip. Returns true if consumed.</summary>
        internal bool TryBegin(int x, int y)
        {
            if (!Enabled)
            {
                return false;
            }

            if (collapseButton != null && collapseButton.containsPoint(x, y))
            {
                ToggleCollapsed();
                return true;
            }

            if (CanResize && GripBounds.Contains(x, y))
            {
                Begin(Mode.Resize, x, y);
                return true;
            }

            if (menu.TitleStrip.Contains(x, y))
            {
                Begin(Mode.Drag, x, y);
                return true;
            }

            return false;
        }

        private void ToggleCollapsed()
        {
            menu.Collapsed = !menu.Collapsed;
            UIServices.PlaySound(Theme.DropdownCloseSound);
            UIServices.Layouts?.Remember(menu);
        }

        private void Begin(Mode newMode, int x, int y)
        {
            mode = newMode;
            start = new Point(x, y);
            origin = new Point(menu.Bounds.X, menu.Bounds.Y);
            originSize = new Point(menu.Bounds.Width, menu.Bounds.Height);
            // pin the top-left corner so anchored menus do not jump while dragging / resizing
            menu.SetPosition(origin.X, origin.Y);
        }

        internal void Held(int x, int y)
        {
            int dx = x - start.X;
            int dy = y - start.Y;
            if (mode == Mode.Drag)
            {
                menu.SetPosition(origin.X + dx, origin.Y + dy);
            }
            else if (mode == Mode.Resize)
            {
                Point min = menu.MinimumSize;
                menu.Width = Math.Max(min.X, originSize.X + dx);
                menu.Height = Math.Max(min.Y, originSize.Y + dy);
            }
            else
            {
                // not interacting
            }
        }

        internal void Released()
        {
            if (mode == Mode.None)
            {
                return;
            }

            mode = Mode.None;
            UIServices.Layouts?.Remember(menu);
        }
    }
}
