using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using System;
using System.Text.RegularExpressions;
using UIFramework.Components.Base;
using UIFramework.Events;

namespace UIFramework.Components
{
    public class TextInput : BaseInputComponent
    {
        public string Placeholder { get; set; } = "";
        public Color TextColor { get; set; } = Game1.textColor;
        public Color PlaceholderColor { get; set; } = Color.Gray;
        public Color BackgroundColor { get; set; } = Color.White;
        public Color BorderColor { get; set; } = Color.Black;
        public int BorderWidth { get; set; } = 2;
        public int MaxLength { get; set; } = 32;
        public bool Password { get; set; } = false;
        public char PasswordChar { get; set; } = '*';
        public TextInputType InputType { get; set; } = TextInputType.Any;
        public SpriteFont Font { get; set; } = Game1.smallFont;
        public float Scale { get; set; } = 1.0f;
        public int Padding { get; set; } = 8;

        private int cursorPosition = 0;
        private int cursorBlinkTimer = 0;
        private const int cursorBlinkInterval = 500; // milliseconds
        private bool cursorVisible = true;
        private Texture2D backgroundTexture;

        public event Action<string> TextChanged;

        public override string Value { get; set; } = "";

        public enum TextInputType
        { Any, Alphanumeric, Numeric, Letters }

        public TextInput(string id, Vector2 position, Vector2 size, string initialValue = "") : base(id, position, size)
        {
            Id = id;
            Position = position;
            Size = size;
            Value = initialValue;
            cursorPosition = initialValue.Length;

            // Load background texture or create one
            backgroundTexture = Game1.content.Load<Texture2D>("LooseSprites\\textBox");
        }

        public override void Draw(SpriteBatch b)
        {
            if (!Visible)
                return;

            // Draw background
            Rectangle bounds = new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y);

            // Use background texture if available, otherwise draw a rectangle
            if (backgroundTexture != null)
            {
                b.Draw(
                    backgroundTexture,
                    bounds,
                    new Rectangle(0, 0, backgroundTexture.Width, backgroundTexture.Height),
                    BackgroundColor
                );
            }
            else
            {
                // Draw background
                b.Draw(Game1.staminaRect, bounds, BackgroundColor);

                // Draw border
                if (BorderWidth > 0)
                {
                    DrawBorder(b, bounds, BorderWidth, BorderColor);
                }
            }

            // Determine text to display (original or password-masked)
            string displayText = Password ? new string(PasswordChar, Value.Length) : Value;

            // Calculate text position with padding
            Vector2 textPosition = new Vector2(Position.X + Padding, Position.Y + ((Size.Y - (Font.MeasureString(displayText).Y * Scale)) / 2));

            // Determine if text needs to be truncated to fit the input field
            Vector2 textSize = Font.MeasureString(displayText) * Scale;
            int maxWidth = (int)Size.X - (Padding * 2);

            // Truncate text if necessary
            if (textSize.X > maxWidth)
            {
                // Find where to truncate from start
                string visibleText = displayText;
                while (Font.MeasureString(visibleText).X * Scale > maxWidth && visibleText.Length > 0)
                {
                    visibleText = visibleText.Substring(1);
                }

                displayText = visibleText;
            }

            // Draw text or placeholder
            if (string.IsNullOrEmpty(Value) && !string.IsNullOrEmpty(Placeholder) && !Selected)
            {
                b.DrawString(
                    Font,
                    Placeholder,
                    textPosition,
                    PlaceholderColor,
                    0f,
                    Vector2.Zero,
                    Scale,
                    SpriteEffects.None,
                    0.9f
                );
            }
            else
            {
                b.DrawString(
                    Font,
                    displayText,
                    textPosition,
                    Enabled ? TextColor : Color.DarkGray,
                    0f,
                    Vector2.Zero,
                    Scale,
                    SpriteEffects.None,
                    0.9f
                );
            }

            // Draw cursor if selected
            if (Selected && cursorVisible)
            {
                string textBeforeCursor = Password ? new string(PasswordChar, cursorPosition) : Value.Substring(0, cursorPosition);
                float cursorX = textPosition.X + (Font.MeasureString(textBeforeCursor).X * Scale);

                // Ensure cursor is visible within the text input bounds
                if (cursorX >= Position.X + Padding && cursorX <= Position.X + Size.X - Padding)
                {
                    b.Draw(
                        Game1.staminaRect,
                        new Rectangle(
                            (int)cursorX,
                            (int)(Position.Y + ((Size.Y - (Font.LineSpacing * Scale)) / 2)),
                            2,
                            (int)(Font.LineSpacing * Scale)
                        ),
                        TextColor
                    );
                }
            }
        }

        private void DrawBorder(SpriteBatch b, Rectangle rect, int width, Color color)
        {
            // Top
            b.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Y, rect.Width, width), color);
            // Bottom
            b.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Y + rect.Height - width, rect.Width, width), color);
            // Left
            b.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Y + width, width, rect.Height - (width * 2)), color);
            // Right
            b.Draw(Game1.staminaRect, new Rectangle(rect.X + rect.Width - width, rect.Y + width, width, rect.Height - (width * 2)), color);
        }

        public override void Update(GameTime time)
        {
            // Update cursor blink effect
            cursorBlinkTimer += time.ElapsedGameTime.Milliseconds;
            if (cursorBlinkTimer >= cursorBlinkInterval)
            {
                cursorBlinkTimer = 0;
                cursorVisible = !cursorVisible;
            }
        }

        public override void OnKeyPressed(Keys key)
        {
            if (!Selected || !Enabled)
                return;

            switch (key)
            {
                case Keys.Back:
                    if (cursorPosition > 0)
                    {
                        string oldValue = Value;
                        Value = Value.Remove(cursorPosition - 1, 1);
                        cursorPosition--;
                        OnTextChanged(oldValue, Value);
                    }
                    break;

                case Keys.Delete:
                    if (cursorPosition < Value.Length)
                    {
                        string oldValue = Value;
                        Value = Value.Remove(cursorPosition, 1);
                        OnTextChanged(oldValue, Value);
                    }
                    break;

                case Keys.Left:
                    if (cursorPosition > 0)
                    {
                        cursorPosition--;
                        cursorVisible = true;
                        cursorBlinkTimer = 0;
                    }
                    break;

                case Keys.Right:
                    if (cursorPosition < Value.Length)
                    {
                        cursorPosition++;
                        cursorVisible = true;
                        cursorBlinkTimer = 0;
                    }
                    break;

                case Keys.Home:
                    cursorPosition = 0;
                    cursorVisible = true;
                    cursorBlinkTimer = 0;
                    break;

                case Keys.End:
                    cursorPosition = Value.Length;
                    cursorVisible = true;
                    cursorBlinkTimer = 0;
                    break;

                case Keys.Enter:
                    // Trigger enter key handler if needed
                    Deselect();
                    break;

                case Keys.Tab:
                    // Handle tab to move to next control if needed
                    Deselect();
                    break;

                case Keys.Escape:
                    Deselect();
                    break;
            }
        }

        public override void OnTextInput(char input)
        {
            if (!Selected || !Enabled)
                return;

            // Validate input based on input type
            if (!ValidateInput(input))
                return;

            // Check max length
            if (Value.Length >= MaxLength)
                return;

            // Insert character at cursor position
            string oldValue = Value;
            Value = Value.Insert(cursorPosition, input.ToString());
            cursorPosition++;

            // Notify about text change
            OnTextChanged(oldValue, Value);

            // Reset cursor blink
            cursorVisible = true;
            cursorBlinkTimer = 0;
        }

        public bool ValidateInput(char input)
        {
            switch (InputType)
            {
                case TextInputType.Numeric:
                    return char.IsDigit(input) || input == '.' || input == '-';

                case TextInputType.Letters:
                    return char.IsLetter(input) || char.IsWhiteSpace(input);

                case TextInputType.Alphanumeric:
                    return char.IsLetterOrDigit(input) || char.IsWhiteSpace(input);

                default:
                    return true;
            }
        }

        public override void Select()
        {
            Selected = true;
            cursorPosition = Value.Length; // Put cursor at the end
            cursorVisible = true;
            cursorBlinkTimer = 0;

            // Register for keyboard input
            Game1.keyboardDispatcher.Subscriber = this;
        }

        public override void Deselect()
        {
            Selected = false;

            // Unregister from keyboard input
            if (Game1.keyboardDispatcher.Subscriber == this)
            {
                Game1.keyboardDispatcher.Subscriber = null;
            }
        }

        private void OnTextChanged(string oldValue, string newValue)
        {
            TextChanged?.Invoke(newValue);
        }

        public void Clear()
        {
            string oldValue = Value;
            Value = "";
            cursorPosition = 0;

            if (oldValue != Value)
            {
                OnTextChanged(oldValue, Value);
            }
        }

        public override bool Contains(int x, int y)
        {
            return x >= Position.X && x <= Position.X + Size.X &&
                   y >= Position.Y && y <= Position.Y + Size.Y;
        }

        public override void OnClick(int x, int y)
        {
            if (Contains(x, y))
            {
                if (!Selected)
                {
                    Select();
                }

                // Try to position cursor based on click position
                // This is an approximate calculation
                float textX = Position.X + Padding;
                float clickOffset = x - textX;

                string displayText = Password ? new string(PasswordChar, Value.Length) : Value;
                int newPos = 0;

                // Find the closest character position to the click
                for (int i = 0; i <= displayText.Length; i++)
                {
                    float charWidth = i > 0
                        ? Font.MeasureString(displayText.Substring(0, i)).X * Scale
                        : 0;

                    if (charWidth > clickOffset)
                    {
                        newPos = i - 1;
                        break;
                    }

                    newPos = i;
                }

                cursorPosition = Math.Max(0, Math.Min(Value.Length, newPos));
                cursorVisible = true;
                cursorBlinkTimer = 0;
            }
            else
            {
                Deselect();
            }
        }
    }
}