using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using UIFramework.Components.Base;
using UIFramework.Config;
using UIFramework.Layout;

namespace UIFramework.Menus
{
    public class BaseMenu : IClickableMenu
    {
        public string Id { get; protected set; }
        public MenuConfig Config { get; protected set; }
        public List<BaseComponent> Components { get; protected set; } = new List<BaseComponent>();
        public BaseMenu ParentMenu { get; protected set; }
        public List<BaseMenu> SubMenus { get; protected set; } = new List<BaseMenu>();

        protected bool isVisible = false;

        public BaseMenu(string id, MenuConfig config) : base(0, 0, 0, 0)
        {
            Id = id;
            Config = config ?? new MenuConfig();

            // Directly implement initialization instead of calling virtual method
            InitializeMenu();
        }

        // Renamed from Initialize to avoid S1699 warning
        protected virtual void InitializeMenu()
        {
            // Set position
            xPositionOnScreen = (int)Config.Position.X;
            yPositionOnScreen = (int)Config.Position.Y;
            width = Config.Width;
            height = Config.Height;

            // If position is default (0, 0), center the menu on screen
            if (Config.Position == Vector2.Zero)
            {
                xPositionOnScreen = Game1.viewport.Width / 2 - Config.Width / 2;
                yPositionOnScreen = Game1.viewport.Height / 2 - Config.Height / 2;
            }

            // Setup close button if enabled
            if (Config.ShowCloseButton)
            {
                upperRightCloseButton = new ClickableTextureComponent(
                    new Rectangle(
                        xPositionOnScreen + width - 32,
                        yPositionOnScreen + 16,
                        32,
                        32
                    ),
                    Game1.mouseCursors,
                    new Rectangle(337, 494, 12, 12),
                    2.5f
                );
            }
            else
            {
                upperRightCloseButton = null;
            }
        }

        public override void receiveKeyPress(Keys key)
        {
            if (!isVisible)
                return;

            // Check if any submenu is visible and let it handle the key press first
            foreach (var subMenu in SubMenus)
            {
                if (subMenu.IsVisible())
                {
                    subMenu.receiveKeyPress(key);
                    return;
                }
            }

            // Handle ESC key for this menu
            if (key == Keys.Escape)
            {
                ExitMenu();
                return;
            }

            // For all other keys, call the base implementation
            base.receiveKeyPress(key);
        }

        public virtual void AddComponent(BaseComponent component)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            // Check if component with same ID already exists
            if (Components.Any(c => c.Id == component.Id))
                throw new ArgumentException($"Component with ID '{component.Id}' already exists");

            // Add the component to the list
            Components.Add(component);

            // Adjust the component position relative to menu if it's not already positioned
            if (component.Position.X < xPositionOnScreen || component.Position.Y < yPositionOnScreen)
            {
                // Apply menu position offset to components not already placed relative to menu
                // This helps with components created by layouts
                if (!(component.Position.X >= xPositionOnScreen &&
                     component.Position.Y >= yPositionOnScreen &&
                     component.Position.X + component.Size.X <= xPositionOnScreen + width &&
                     component.Position.Y + component.Size.Y <= yPositionOnScreen + height))
                {
                    // Add title offset if title is present
                    int titleOffset = !string.IsNullOrEmpty(Config.Title) ? 50 : 0;
                    component.Position = new Vector2(
                        xPositionOnScreen + component.Position.X,
                        yPositionOnScreen + component.Position.Y + titleOffset
                    );
                }
            }
        }

        public virtual void RemoveComponent(string componentId)
        {
            if (string.IsNullOrEmpty(componentId))
                throw new ArgumentNullException(nameof(componentId));

            BaseComponent component = GetComponent(componentId);
            if (component != null)
            {
                Components.Remove(component);
            }
        }

        public virtual BaseComponent GetComponent(string componentId)
        {
            if (string.IsNullOrEmpty(componentId))
                return null;

            return Components.FirstOrDefault(c => c.Id == componentId);
        }

        public virtual void AddSubMenu(BaseMenu menu)
        {
            if (menu == null)
                throw new ArgumentNullException(nameof(menu));

            // Check if submenu with same ID already exists
            if (SubMenus.Any(m => m.Id == menu.Id))
                throw new ArgumentException($"SubMenu with ID '{menu.Id}' already exists");

            menu.ParentMenu = this;
            SubMenus.Add(menu);
        }

        public virtual void RemoveSubMenu(string menuId)
        {
            if (string.IsNullOrEmpty(menuId))
                throw new ArgumentNullException(nameof(menuId));

            BaseMenu menu = GetSubMenu(menuId);
            if (menu != null)
            {
                SubMenus.Remove(menu);
            }
        }

        public virtual BaseMenu GetSubMenu(string menuId)
        {
            if (string.IsNullOrEmpty(menuId))
                return null;

            return SubMenus.FirstOrDefault(m => m.Id == menuId);
        }

        public virtual void Show()
        {
            // Only play sound if menu wasn't already visible
            if (!isVisible)
            {
                Game1.playSound("bigSelect");
            }
            isVisible = true;
            Game1.activeClickableMenu = this;
        }

        public virtual void Hide()
        {
            // Only play sound if menu was visible
            if (isVisible)
            {
                Game1.playSound("bigDeSelect");
            }

            isVisible = false;

            // If this menu is the active menu, clear it
            if (Game1.activeClickableMenu == this)
            {
                Game1.activeClickableMenu = ParentMenu;
            }
        }

        public virtual bool IsVisible()
        {
            return isVisible;
        }

        public virtual void PositionGridLayout(GridLayout layout)
        {
            if (layout == null)
                return;

            // Set the grid's origin to the menu's position, with optional title offset
            int titleOffset = !string.IsNullOrEmpty(Config.Title) ? 50 : 0;
            layout.SetOrigin(new Vector2(xPositionOnScreen, yPositionOnScreen + titleOffset));

            // Update all components in the grid to reflect their new positions
            foreach (var component in layout.GetComponents())
            {
                // We don't need to update the component positions here as the SetOrigin method
                // in the GridLayout will handle updating all component positions
            }
        }

        public override void draw(SpriteBatch b)
        {
            if (!isVisible)
                return;

            // Draw the menu background
            if (Config.UseGameBackground)
            {
                // Draw standard game background
                drawTextureBox(
                    b,
                    Game1.menuTexture,
                    new Rectangle(0, 256, 60, 60),
                    xPositionOnScreen,
                    yPositionOnScreen,
                    width,
                    height,
                    Color.White
                );
            }
            else if (Config.BackgroundColor.HasValue)
            {
                // Draw custom colored background
                b.Draw(
                    Game1.staminaRect,
                    new Rectangle(xPositionOnScreen, yPositionOnScreen, width, height),
                    Config.BackgroundColor.Value
                );
            }
            else if (!string.IsNullOrEmpty(Config.BackgroundTexture))
            {
                // Draw custom textured background if texture is provided
                try
                {
                    Texture2D texture = Game1.content.Load<Texture2D>(Config.BackgroundTexture);
                    b.Draw(
                        texture,
                        new Rectangle(xPositionOnScreen, yPositionOnScreen, width, height),
                        Color.White
                    );
                }
                catch
                {
                    // Fallback to standard background if texture loading fails
                    drawTextureBox(
                        b,
                        Game1.menuTexture,
                        new Rectangle(0, 256, 60, 60),
                        xPositionOnScreen,
                        yPositionOnScreen,
                        width,
                        height,
                        Color.White
                    );
                }
            }

            // Draw title if present
            if (!string.IsNullOrEmpty(Config.Title))
            {
                Vector2 titleSize = Game1.dialogueFont.MeasureString(Config.Title);
                float titleX = xPositionOnScreen + width / 2 - titleSize.X / 2;
                float titleY = yPositionOnScreen + 20;

                b.DrawString(
                    Game1.dialogueFont,
                    Config.Title,
                    new Vector2(titleX, titleY),
                    Game1.textColor
                );
            }

            // Draw components
            DrawComponents(b);

            // Using LINQ directly for S3267 fix
            SubMenus.Where(subMenu => subMenu.IsVisible()).ToList().ForEach(subMenu => subMenu.draw(b));

            // Draw close button if enabled
            if (Config.ShowCloseButton && upperRightCloseButton != null)
            {
                upperRightCloseButton.draw(b);
            }

            drawMouse(b);
        }

        protected virtual void DrawComponents(SpriteBatch b)
        {
            // Sort components by layer for proper drawing order
            var sortedComponents = Components
                .Where(c => c.Visible)
                .OrderBy(c => c.Layer)
                .ToList();

            // Draw all visible components
            foreach (BaseComponent component in sortedComponents)
            {
                component.Draw(b);
            }

            // Draw tooltips for hovered components
            foreach (BaseComponent component in sortedComponents)
            {
                // Use Contains method instead of accessing _isHovered directly
                if (component.Contains(Game1.getMouseX(), Game1.getMouseY()) &&
                    !string.IsNullOrEmpty(component.Tooltip))
                {
                    component.DrawTooltip(b);
                }
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (!isVisible)
                return;

            // Handle close button if enabled
            if (Config.ShowCloseButton && upperRightCloseButton != null && upperRightCloseButton.containsPoint(x, y))
            {
                if (playSound)
                    Game1.playSound("bigDeSelect");

                // Using our implementation of exitThisMenu
                ExitMenu();
                return;
            }

            // Handle submenu clicks first (they should be on top) - fix S3267
            var visibleSubmenus = SubMenus.Where(sm => sm.IsVisible()).ToList();
            foreach (BaseMenu subMenu in visibleSubmenus)
            {
                // If click is within submenu bounds, let it handle the click
                if (subMenu.IsWithinBounds(x, y))
                {
                    subMenu.receiveLeftClick(x, y, playSound);
                    return;
                }
            }

            // Handle component clicks
            foreach (BaseComponent component in Components.Where(c => c.Visible && c.Enabled))
            {
                if (component is BaseClickableComponent clickable && clickable.Contains(x, y))
                {
                    clickable.OnClick(x, y);
                    return;
                }
            }
        }

        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
            if (!isVisible)
                return;

            // Handle submenu clicks first - fix S3267
            var visibleSubmenus = SubMenus.Where(sm => sm.IsVisible()).ToList();
            foreach (BaseMenu subMenu in visibleSubmenus)
            {
                if (subMenu.IsWithinBounds(x, y))
                {
                    subMenu.receiveRightClick(x, y, playSound);
                    return;
                }
            }

            // Handle component right clicks
            foreach (BaseComponent component in Components.Where(c => c.Visible && c.Enabled))
            {
                if (component is BaseClickableComponent clickable && clickable.Contains(x, y))
                {
                    clickable.OnRightClick(x, y);
                    return;
                }
            }
        }

        public override void performHoverAction(int x, int y)
        {
            if (!isVisible)
                return;

            // Close button hover action
            if (Config.ShowCloseButton && upperRightCloseButton != null)
            {
                upperRightCloseButton.tryHover(x, y);
            }

            // Handle submenu hovers first - fix S3267
            var visibleSubmenus = SubMenus.Where(sm => sm.IsVisible()).ToList();
            foreach (BaseMenu subMenu in visibleSubmenus)
            {
                subMenu.performHoverAction(x, y);
            }

            // Update hover state for components
            foreach (BaseComponent component in Components.Where(c => c.Visible && c.Enabled))
            {
                bool contains = component.Contains(x, y);

                // Instead of directly accessing _isHovered, use Contains() for current state
                // and pass hover state to component's methods
                if (contains)
                {
                    if (component is BaseClickableComponent clickable)
                    {
                        clickable.OnHover(x, y);
                    }
                }
            }
        }

        public override void update(GameTime time)
        {
            base.update(time);

            // Update all submenus - fix S3267
            SubMenus.Where(sm => sm.IsVisible()).ToList().ForEach(sm => sm.update(time));

            // Update all components
            Components.Where(c => c.Visible).ToList().ForEach(c => c.Update(time));
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);

            // Recalculate menu position if it was centered
            if (Config.Position == Vector2.Zero)
            {
                xPositionOnScreen = Game1.viewport.Width / 2 - width / 2;
                yPositionOnScreen = Game1.viewport.Height / 2 - height / 2;
            }

            // Adjust close button position
            if (Config.ShowCloseButton && upperRightCloseButton != null)
            {
                upperRightCloseButton.bounds = new Rectangle(
                    xPositionOnScreen + width - 32,
                    yPositionOnScreen + 16,
                    32,
                    32
                );
            }

            // Notify all components about the resize
            Components.ForEach(component => component.OnResize(oldBounds, newBounds));

            // Notify all submenus about the resize - fix S3267
            SubMenus.ForEach(subMenu => subMenu.gameWindowSizeChanged(oldBounds, newBounds));
        }

        // Renamed method to avoid CS0115 error
        public virtual void ExitMenu()
        {
            Hide();
            if (Game1.activeClickableMenu == this)
            {
                Game1.activeClickableMenu = null;
            }
        }

        // Fix CS0114 by adding 'new' keyword
        public new bool IsWithinBounds(int x, int y)
        {
            return x >= xPositionOnScreen && x < xPositionOnScreen + width &&
                   y >= yPositionOnScreen && y < yPositionOnScreen + height;
        }
    }
}