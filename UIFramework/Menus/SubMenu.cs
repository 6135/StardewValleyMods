using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using System;
using UIFramework.Components.Base;

namespace UIFramework.Menus
{
    public class SubMenu : BaseMenu
    {
        public bool AutoClose { get; set; } = true;
        public bool CloseOnOutsideClick { get; set; } = true;
        public BaseComponent Anchor { get; set; }
        public MenuPosition Position { get; set; }

        public enum MenuPosition
        { Above, Below, Left, Right }

        public SubMenu(string id, Config.MenuConfig config)
            : base(id, config)
        {
            Position = MenuPosition.Below;
            // Set default configuration for submenus
            if (config.ShowCloseButton)
            {
                // For submenus, override close button to use a smaller one
                upperRightCloseButton = new StardewValley.Menus.ClickableTextureComponent(
                    new Rectangle(
                        xPositionOnScreen + width - 24,
                        yPositionOnScreen + 8,
                        16,
                        16
                    ),
                    Game1.mouseCursors,
                    new Rectangle(337, 494, 12, 12),
                    1.5f
                );
            }
        }

        public override void draw(SpriteBatch b)
        {
            if (!isVisible)
                return;

            // Draw a drop shadow for the submenu to make it visually stand out
            b.Draw(
                Game1.staminaRect,
                new Rectangle(
                    xPositionOnScreen + 4,
                    yPositionOnScreen + 4,
                    width,
                    height
                ),
                new Color(0, 0, 0, 100)
            );

            // Draw the submenu with the rest of the standard drawing routine
            base.draw(b);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (!isVisible)
                return;

            // Check if click is outside submenu and should close it
            if (CloseOnOutsideClick && !isWithinBounds(x, y))
            {
                Hide();
                return;
            }

            // Use the standard click handling for components inside the menu
            base.receiveLeftClick(x, y, playSound);
        }

        public void PositionRelativeTo(BaseComponent component, MenuPosition position = MenuPosition.Below)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            Anchor = component;
            Position = position;

            // Calculate position based on anchor and desired relative position
            switch (position)
            {
                case MenuPosition.Above:
                    xPositionOnScreen = (int)component.Position.X;
                    yPositionOnScreen = (int)component.Position.Y - height - 4;
                    break;

                case MenuPosition.Below:
                    xPositionOnScreen = (int)component.Position.X;
                    yPositionOnScreen = (int)(component.Position.Y + component.Size.Y + 4);
                    break;

                case MenuPosition.Left:
                    xPositionOnScreen = (int)component.Position.X - width - 4;
                    yPositionOnScreen = (int)component.Position.Y;
                    break;

                case MenuPosition.Right:
                    xPositionOnScreen = (int)(component.Position.X + component.Size.X + 4);
                    yPositionOnScreen = (int)component.Position.Y;
                    break;
            }

            // Ensure the menu stays within the game window bounds
            ConstrainToViewport();

            // Update close button position
            if (upperRightCloseButton != null)
            {
                upperRightCloseButton.bounds = new Rectangle(
                    xPositionOnScreen + width - 24,
                    yPositionOnScreen + 8,
                    16,
                    16
                );
            }
        }

        public void PositionAt(Vector2 position)
        {
            xPositionOnScreen = (int)position.X;
            yPositionOnScreen = (int)position.Y;

            // Ensure the menu stays within the game window bounds
            ConstrainToViewport();

            // Update close button position
            if (upperRightCloseButton != null)
            {
                upperRightCloseButton.bounds = new Rectangle(
                    xPositionOnScreen + width - 24,
                    yPositionOnScreen + 8,
                    16,
                    16
                );
            }
        }

        private void ConstrainToViewport()
        {
            // Get viewport bounds
            xTile.Dimensions.Rectangle viewport = Game1.viewport;

            // Ensure menu stays within horizontal bounds
            if (xPositionOnScreen < 0)
            {
                xPositionOnScreen = 0;
            }
            else if (xPositionOnScreen + width > viewport.Width)
            {
                xPositionOnScreen = viewport.Width - width;
            }

            // Ensure menu stays within vertical bounds
            if (yPositionOnScreen < 0)
            {
                yPositionOnScreen = 0;
            }
            else if (yPositionOnScreen + height > viewport.Height)
            {
                yPositionOnScreen = viewport.Height - height;
            }
        }

        public override void update(GameTime time)
        {
            base.update(time);

            // If we have an anchor and it's no longer visible, hide this submenu
            if (Anchor != null && !Anchor.Visible)
            {
                Hide();
            }
        }

        // Update position when anchor component moves
        public void UpdateAnchorPosition()
        {
            if (Anchor != null)
            {
                PositionRelativeTo(Anchor, Position);
            }
        }

        public override void Show()
        {
            base.Show();

            // Automatically update position relative to anchor if set
            if (Anchor != null)
            {
                PositionRelativeTo(Anchor, Position);
            }
        }
    }
}