namespace UIFramework.Components
{
    public class Label : BaseComponent
    {
        public string Text { get; set; }
        public Color TextColor { get; set; } = Game1.textColor;
        public SpriteFont Font { get; set; } = Game1.smallFont;
        public float Scale { get; set; } = 1.0f;
        public bool AutoSize { get; set; } = true;
        public TextAlignment Alignment { get; set; } = TextAlignment.Left;

        public enum TextAlignment { Left, Center, Right }

        public override void Draw(SpriteBatch b);
        public override void Update(GameTime time);

        public Vector2 MeasureText();
        public void SetText(string text, bool autoResize = true);
    }
}
