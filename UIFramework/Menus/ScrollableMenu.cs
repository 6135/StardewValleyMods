//using Microsoft.Xna.Framework;
//using Microsoft.Xna.Framework.Graphics;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using UIFramework.Components.Base;

//namespace UIFramework.Menus
//{
//    public class ScrollableMenu : BaseMenu
//    {
//        public int ContentHeight { get; protected set; }
//        public int ViewportHeight { get; protected set; }
//        public int ScrollPosition { get; set; }
//        public int ScrollStep { get; set; } = 20;
//        public bool ShowScrollbar { get; set; } = true;
//        public Color ScrollbarColor { get; set; } = Color.Gray;
//        public Color ScrollbarBackgroundColor { get; set; } = new Color(0, 0, 0, 100);
//        public int ScrollbarWidth { get; set; } = 20;

//        public event Action<int> ScrollChanged;

//        public override void draw(SpriteBatch b);

//        public override void receiveScrollWheelAction(int direction);

//        public void ScrollToTop();

//        public void ScrollToBottom();

//        public void ScrollTo(int position);

//        public void RecalculateContentHeight();

//        // Handle components that are outside the visible area
//        protected bool IsComponentVisible(BaseComponent component);

//        protected override void DrawComponents(SpriteBatch b);
//    }
//}