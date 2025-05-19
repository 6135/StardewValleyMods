using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UIFramework.Components.Base;
using UIFramework.Config;

namespace UIFramework.Menus
{
    public abstract class BaseMenu : IClickableMenu
    {
        public string Id { get; protected set; }
        public MenuConfig Config { get; protected set; }
        public List<BaseComponent> Components { get; protected set; }
        public BaseMenu ParentMenu { get; protected set; }
        public List<BaseMenu> SubMenus { get; protected set; }

        public virtual void AddComponent(BaseComponent component);

        public virtual void RemoveComponent(string componentId);

        public virtual BaseComponent GetComponent(string componentId);

        public virtual void AddSubMenu(BaseMenu menu);

        public virtual void RemoveSubMenu(string menuId);

        public virtual BaseMenu GetSubMenu(string menuId);

        public virtual void Show();

        public virtual void Hide();

        public virtual bool IsVisible();

        public override void draw(SpriteBatch b);

        public override void receiveLeftClick(int x, int y, bool playSound = true);

        public override void receiveRightClick(int x, int y, bool playSound = true);

        public override void performHoverAction(int x, int y);

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds);
    }
}