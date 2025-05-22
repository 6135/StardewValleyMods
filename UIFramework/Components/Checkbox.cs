//using Microsoft.Xna.Framework;
//using Microsoft.Xna.Framework.Graphics;
//using StardewValley;
//using System;
//using UIFramework.Components.Base;

//namespace UIFramework.Components
//{
//    public class Checkbox : BaseClickableComponent
//    {
//        public bool Checked { get; set; }
//        public string Text { get; set; }
//        public Color TextColor { get; set; } = Game1.textColor;
//        public Color CheckedColor { get; set; } = Color.Green;
//        public Color UncheckedColor { get; set; } = Color.White;
//        public Color BorderColor { get; set; } = Color.Black;
//        public Texture2D CheckmarkTexture { get; set; }
//        public Rectangle CheckmarkSourceRect { get; set; }

//        public event Action<bool> CheckedChanged;

//        public override void Draw(SpriteBatch b);

//        public override void Update(GameTime time);

//        public override void OnClick(int x, int y);

//        public void Toggle();
//    }
//}