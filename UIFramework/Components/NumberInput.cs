//namespace UIFramework.Components
//{
//    public class NumberInput : BaseInputComponent
//    {
//        public int MinValue { get; set; } = int.MinValue;
//        public int MaxValue { get; set; } = int.MaxValue;
//        public int Step { get; set; } = 1;
//        public bool AllowDecimal { get; set; } = false;
//        public int DecimalPlaces { get; set; } = 2;
//        public Color TextColor { get; set; } = Game1.textColor;
//        public Color BackgroundColor { get; set; } = Color.White;
//        public Color BorderColor { get; set; } = Color.Black;
//        public bool ShowUpDownButtons { get; set; } = true;

//        public event Action<int> ValueChanged;
//        public event Action<double> DecimalValueChanged;

//        public int IntValue { get; set; }
//        public double DecimalValue { get; set; }

//        public override void Draw(SpriteBatch b);
//        public override void Update(GameTime time);
//        public override void OnKeyPressed(Keys key);
//        public override void OnTextInput(char input);

//        public void Increment();
//        public void Decrement();
//        public bool ValidateInput(char input);
//    }
//}