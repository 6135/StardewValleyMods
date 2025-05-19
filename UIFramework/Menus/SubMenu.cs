using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UIFramework.Components.Base;

namespace UIFramework.Menus
{
    public class SubMenu : BaseMenu
    {
        public bool AutoClose { get; set; } = true;
        public bool CloseOnOutsideClick { get; set; } = true;
        public BaseComponent Anchor { get; set; }
        public MenuPosition Position { get; set; } = MenuPosition.Below;

        public enum MenuPosition
        { Above, Below, Left, Right }

        public override void draw(SpriteBatch b);

        public override void receiveLeftClick(int x, int y, bool playSound = true);

        public void PositionRelativeTo(BaseComponent component, MenuPosition position = MenuPosition.Below);

        public void PositionAt(Vector2 position);
    }
}