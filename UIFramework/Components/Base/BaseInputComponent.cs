using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using System;
using UIFramework.Events;

namespace UIFramework.Components.Base
{
    public abstract class BaseInputComponent : BaseClickableComponent, IKeyboardSubscriber
    {
        public bool Selected
        {
            get;
            set;
        }

        public virtual string Value { get; set; } = "";
        public string Placeholder { get; set; } = "";
        public Color TextColor { get; set; } = Game1.textColor;
        public Color PlaceholderColor { get; set; } = Color.Gray;
        public int MaxLength { get; set; } = 32;
        public bool ReadOnly { get; set; } = false;

        protected int _caretPosition;
        protected int _caretBlinkTimer;
        protected bool _showCaret;

        public event Action<InputEventArgs> ValueChanged;

        protected BaseInputComponent(string id, Vector2 position, Vector2 size)
            : base(id, position, size)
        {
        }

        public override void Update(GameTime time)
        {
            base.Update(time);

            if (Selected)
            {
                _caretBlinkTimer += time.ElapsedGameTime.Milliseconds;
                if (_caretBlinkTimer > 500)
                {
                    _showCaret = !_showCaret;
                    _caretBlinkTimer = 0;
                }
            }
            else
            {
                _showCaret = false;
            }
        }

        public override void OnClick(int x, int y)
        {
            base.OnClick(x, y);

            if (Contains(x, y))
            {
                Select();
            }
            else if (Selected)
            {
                Deselect();
            }
        }

        // IKeyboardSubscriber implementation
        public void RecieveTextInput(char inputChar)
        {
            OnTextInput(inputChar);
        }

        public void RecieveTextInput(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            foreach (char c in text)
            {
                OnTextInput(c);
            }
        }

        public void RecieveCommandInput(char command)
        {
            if (command == '\b' && _caretPosition > 0 && Value.Length > 0)
            {
                string oldValue = Value;
                Value = Value.Remove(_caretPosition - 1, 1);
                _caretPosition--;
                Game1.playSound("tinyWhip");

                if (oldValue != Value)
                {
                    ValueChanged?.Invoke(new InputEventArgs(this, oldValue, Value));
                }
            }
        }

        public void RecieveSpecialInput(Keys key)
        {
            OnKeyPressed(key);
        }

        public virtual void OnKeyPressed(Keys key)
        {
            if (!Selected || ReadOnly) return;

            string oldValue = Value;

            switch (key)
            {
                case Keys.Delete:
                    if (_caretPosition < Value.Length)
                    {
                        Value = Value.Remove(_caretPosition, 1);
                        Game1.playSound("tinyWhip");
                    }
                    break;

                case Keys.Left:
                    if (_caretPosition > 0)
                    {
                        _caretPosition--;
                    }
                    break;

                case Keys.Right:
                    if (_caretPosition < Value.Length)
                    {
                        _caretPosition++;
                    }
                    break;

                case Keys.Home:
                    _caretPosition = 0;
                    break;

                case Keys.End:
                    _caretPosition = Value.Length;
                    break;
            }

            if (oldValue != Value)
            {
                ValueChanged?.Invoke(new InputEventArgs(this, oldValue, Value));
            }
        }

        public virtual void OnTextInput(char input)
        {
            if (!Selected || ReadOnly) return;

            if (Value.Length < MaxLength && IsValidInput(input))
            {
                string oldValue = Value;

                if (_caretPosition == Value.Length)
                {
                    Value += input;
                }
                else
                {
                    Value = Value.Insert(_caretPosition, input.ToString());
                }

                _caretPosition++;
                Game1.playSound("cowboy_monsterhit");

                ValueChanged?.Invoke(new InputEventArgs(this, oldValue, Value));
            }
        }

        public virtual void Select()
        {
            if (!Enabled || ReadOnly) return;

            Selected = true;
            _caretPosition = Value.Length;
            _showCaret = true;
            _caretBlinkTimer = 0;

            Game1.keyboardDispatcher.Subscriber = this;
        }

        public virtual void Deselect()
        {
            Selected = false;

            if (Game1.keyboardDispatcher.Subscriber == this)
            {
                Game1.keyboardDispatcher.Subscriber = null;
            }
        }

        protected virtual bool IsValidInput(char input)
        {
            return char.IsLetterOrDigit(input) || char.IsPunctuation(input) || char.IsWhiteSpace(input);
        }
    }
}