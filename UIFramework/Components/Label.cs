using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using System;
using UIFramework.Components.Base;

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
        public bool WordWrap { get; set; } = false;
        public int MaxWidth { get; set; } = 0;
        public Color BackgroundColor { get; set; } = Color.Transparent;
        public int Padding { get; set; } = 0;

        public enum TextAlignment
        { Left, Center, Right }

        public Label(string id, Vector2 position, string text)
            : base(id, position, Vector2.Zero) // Pass required parameters to the base constructor
        {
            Text = text;

            if (AutoSize)
            {
                Size = MeasureText();
            }
        }

        public override void Draw(SpriteBatch b)
        {
            if (!Visible)
                return;

            // Draw background if not transparent
            if (BackgroundColor != Color.Transparent)
            {
                b.Draw(
                    Game1.staminaRect,
                    new Rectangle(
                        (int)Position.X - Padding,
                        (int)Position.Y - Padding,
                        (int)Size.X + (Padding * 2),
                        (int)Size.Y + (Padding * 2)
                    ),
                    BackgroundColor
                );
            }

            // Handle word wrap if enabled
            if (WordWrap && MaxWidth > 0)
            {
                DrawWrappedText(b);
            }
            else
            {
                // Calculate position based on alignment
                float xPos = Position.X;
                if (Alignment == TextAlignment.Center)
                {
                    xPos = Position.X - (MeasureText().X / 2);
                }
                else if (Alignment == TextAlignment.Right)
                {
                    xPos = Position.X - MeasureText().X;
                }

                b.DrawString(
                    Font,
                    Text,
                    new Vector2(xPos, Position.Y),
                    TextColor,
                    0f,
                    Vector2.Zero,
                    Scale,
                    SpriteEffects.None,
                    0.9f
                );
            }
        }

        private void DrawWrappedText(SpriteBatch b)
        {
            string[] words = Text.Split(' ');
            string currentLine = "";
            float yOffset = 0;

            foreach (string word in words)
            {
                string testLine = currentLine.Length > 0 ? currentLine + " " + word : word;
                Vector2 size = Font.MeasureString(testLine) * Scale;

                if (size.X > MaxWidth)
                {
                    // Draw current line and start a new one
                    float xPos = Position.X;
                    if (Alignment == TextAlignment.Center)
                    {
                        xPos = Position.X + (MaxWidth / 2) - (Font.MeasureString(currentLine).X * Scale / 2);
                    }
                    else if (Alignment == TextAlignment.Right)
                    {
                        xPos = Position.X + MaxWidth - (Font.MeasureString(currentLine).X * Scale);
                    }

                    b.DrawString(
                        Font,
                        currentLine,
                        new Vector2(xPos, Position.Y + yOffset),
                        TextColor,
                        0f,
                        Vector2.Zero,
                        Scale,
                        SpriteEffects.None,
                        0.9f
                    );

                    currentLine = word;
                    yOffset += Font.LineSpacing * Scale;
                }
                else
                {
                    currentLine = testLine;
                }
            }

            // Draw final line
            if (!string.IsNullOrEmpty(currentLine))
            {
                float xPos = Position.X;
                if (Alignment == TextAlignment.Center)
                {
                    xPos = Position.X + (MaxWidth / 2) - (Font.MeasureString(currentLine).X * Scale / 2);
                }
                else if (Alignment == TextAlignment.Right)
                {
                    xPos = Position.X + MaxWidth - (Font.MeasureString(currentLine).X * Scale);
                }

                b.DrawString(
                    Font,
                    currentLine,
                    new Vector2(xPos, Position.Y + yOffset),
                    TextColor,
                    0f,
                    Vector2.Zero,
                    Scale,
                    SpriteEffects.None,
                    0.9f
                );
            }
        }

        public override void Update(GameTime time)
        {
            // No specific update logic needed for a label
        }

        public Vector2 MeasureText()
        {
            if (string.IsNullOrEmpty(Text))
                return Vector2.Zero;

            if (WordWrap && MaxWidth > 0)
            {
                return MeasureWrappedText();
            }

            return Font.MeasureString(Text) * Scale;
        }

        private Vector2 MeasureWrappedText()
        {
            string[] words = Text.Split(' ');
            string currentLine = "";
            float height = 0;
            float maxWidth = 0;

            foreach (string word in words)
            {
                string testLine = currentLine.Length > 0 ? currentLine + " " + word : word;
                Vector2 size = Font.MeasureString(testLine) * Scale;

                if (size.X > MaxWidth)
                {
                    // Measure current line and start a new one
                    height += Font.LineSpacing * Scale;
                    maxWidth = Math.Max(maxWidth, Font.MeasureString(currentLine).X * Scale);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }

            // Measure final line
            if (!string.IsNullOrEmpty(currentLine))
            {
                height += Font.LineSpacing * Scale;
                maxWidth = Math.Max(maxWidth, Font.MeasureString(currentLine).X * Scale);
            }

            return new Vector2(Math.Min(maxWidth, MaxWidth), height);
        }

        public void SetText(string text, bool autoResize = true)
        {
            Text = text;

            if (AutoSize && autoResize)
            {
                Size = MeasureText();
            }
        }

        public override bool Contains(int x, int y)
        {
            return x >= Position.X && x <= Position.X + Size.X &&
                   y >= Position.Y && y <= Position.Y + Size.Y;
        }
    }
}