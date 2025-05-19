namespace UIFramework.Components
{
    public class Tooltip
    {
        public string Text { get; set; }
        public Vector2 Position { get; set; }
        public Color BackgroundColor { get; set; } = new Color(0, 0, 0, 200);
        public Color TextColor { get; set; } = Color.White;
        public SpriteFont Font { get; set; } = Game1.smallFont;
        public int Padding { get; set; } = 5;
        public int MaxWidth { get; set; } = 300;
        public int DelayMS { get; set; } = 500;
        public bool IsVisible { get; private set; }

        private int hoverTime;

        public void Update(GameTime time, bool isHovering);
        public void Draw(SpriteBatch b);
        public void Show();
        public void Hide();
        public Vector2 MeasureTooltip();
    }
}
