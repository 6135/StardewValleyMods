namespace UIFramework.Components
{
    public class TextInput : BaseInputComponent
    {
        public string Placeholder { get; set; } = "";
        public Color TextColor { get; set; } = Game1.textColor;
        public Color PlaceholderColor { get; set; } = Color.Gray;
        public Color BackgroundColor { get; set; } = Color.White;
        public Color BorderColor { get; set; } = Color.Black;
        public int MaxLength { get; set; } = 32;
        public bool Password { get; set; } = false;
        public char PasswordChar { get; set; } = '*';
        public TextInputType InputType { get; set; } = TextInputType.Any;

        public enum TextInputType { Any, Alphanumeric, Numeric, Letters }

        public event Action<string> TextChanged;

        public override string Value { get; set; }

        public override void Draw(SpriteBatch b);
        public override void Update(GameTime time);
        public override void OnKeyPressed(Keys key);
        public override void OnTextInput(char input);

        public bool ValidateInput(char input);
        public void Clear();
    }
}
