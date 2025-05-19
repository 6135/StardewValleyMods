using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using UIFramework.Components.Base;

namespace UIFramework.Components
{
    public class Button : BaseClickableComponent
    {
        public string Text { get; set; }
        public Color TextColor { get; set; } = Game1.textColor;
        public Color BackgroundColor { get; set; } = Color.White;
        public Color HoverColor { get; set; } = Color.LightGray;
        public Color PressedColor { get; set; } = Color.Gray;

        public override void Draw(SpriteBatch b);

        public override void OnClick(int x, int y);
    }
}