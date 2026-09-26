using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    /// <summary>
    /// Single-line text entry with the vanilla text box look. Focusable and takes keyboard input through the menu's
    /// keyboard subscriber while focused; every change goes through <see cref="MaxLength"/> and <see cref="ValidateFunc"/>
    /// before the bound setter and <see cref="OnValueChanged"/>. Enter raises <see cref="OnSubmit"/>.
    /// </summary>
    internal sealed class TextInput : UIElement, IUITextInput
    {
        private Func<string>? getter;
        private Action<string>? setter;
        private string ownValue = string.Empty;
        private Texture2D? texture;

        internal TextInput(string id, Func<string>? getter, Action<string>? setter) : base(id)
        {
            this.getter = getter;
            this.setter = setter;
        }

        /// <summary>
        /// The current text: the bound getter when one was supplied, otherwise the element's own value. Setting it
        /// writes through the bound setter (or stores it) without validation or <see cref="OnValueChanged"/>.
        /// </summary>
        public string Value
        {
            get => getter != null ? Raise("get", getter, ownValue) ?? string.Empty : ownValue;
            set => Store(value ?? string.Empty);
        }

        /// <summary>The value delegates the element currently reads / writes through (null = own value).</summary>
        internal Func<string>? BoundGetter => getter;

        internal Action<string>? BoundSetter => setter;

        /// <summary>Swap the value delegates after construction (signal bindings); the own value is kept as the fallback.</summary>
        internal void Rebind(Func<string>? newGetter, Action<string>? newSetter)
        {
            getter = newGetter;
            setter = newSetter;
        }

        /// <summary>Text shown (dimmed) while the value is empty and the box is not focused.</summary>
        internal Func<string>? PlaceholderFunc { get; set; }

        Func<string> IUITextInput.Placeholder { get => PlaceholderFunc!; set => PlaceholderFunc = value; }

        /// <summary>Maximum characters (0 = unlimited).</summary>
        public int MaxLength { get; set; }

        /// <summary>Called with the prospective value before it is applied; false rejects the edit silently.</summary>
        internal Func<string, bool>? ValidateFunc { get; set; }

        Func<string, bool> IUITextInput.Validate { get => ValidateFunc!; set => ValidateFunc = value; }

        /// <summary>Custom box texture (null = vanilla <c>LooseSprites\textBox</c>). Changes the natural size.</summary>
        internal Texture2D? Texture
        {
            get => texture;
            set
            {
                texture = value;
                InvalidateLayout();
            }
        }

        Texture2D IUITextInput.Texture { get => texture!; set => Texture = value; }

        internal Action<IUIValueEvent>? OnValueChanged { get; set; }

        Action<IUIValueEvent> IUITextInput.OnValueChanged { get => OnValueChanged!; set => OnValueChanged = value; }

        internal Action<IUIElement>? OnSubmit { get; set; }

        Action<IUIElement> IUITextInput.OnSubmit { get => OnSubmit!; set => OnSubmit = value; }

        internal override bool Focusable => true;
        internal override bool WantsTextInput => true;

        internal override string AccessibleDescription
        {
            get
            {
                string value = Value;
                string content = value.Length > 0 ? value : CurrentPlaceholder;
                return Accessibility.Compose(
                    Accessibility.Text("text-input", "Text input"),
                    content.Length > 0 ? content : Accessibility.Text("empty", "empty"),
                    Enabled ? null : Accessibility.Text("disabled", "disabled"));
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Value pipeline
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Write a value to the bound setter (or the own value) without raising events.</summary>
        private void Store(string value)
        {
            ownValue = value;
            if (setter != null)
            {
                Action<string> bound = setter;
                Raise("set", () => bound(value));
            }
        }

        /// <summary>
        /// Apply a prospective value: enforce <see cref="MaxLength"/>, run <see cref="ValidateFunc"/>, store and raise
        /// <see cref="OnValueChanged"/>. Returns false when nothing changed (rejected or identical).
        /// </summary>
        private bool TryCommit(string newValue)
        {
            if (MaxLength > 0 && newValue.Length > MaxLength)
            {
                return false;
            }

            string oldValue = Value;
            if (oldValue == newValue)
            {
                return false;
            }

            if (ValidateFunc != null)
            {
                Func<string, bool> validate = ValidateFunc;
                if (!Raise("Validate", () => validate(newValue), false))
                {
                    return false;
                }
            }
            Store(newValue);
            if (OnValueChanged != null)
            {
                Action<IUIValueEvent> cb = OnValueChanged;
                UIValueEvent e = UIValueEvent.Text(this, oldValue, newValue);
                Raise("OnValueChanged", () => cb(e));
            }
            return true;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Layout / draw
        // ---------------------------------------------------------------------------------------------------------

        protected override Vector2 MeasureCore(Vector2 available) => TextBoxDrawing.Measure(texture, Style.Font);

        // the box is 3-slice and the text clips from the left, so it narrows to the caps plus a few characters
        protected override float MinWidthCore() => TextBoxDrawing.MinWidth(Style.Font);

        protected override void DrawCore(SpriteBatch b)
        {
            ResolvedStyle style = Style;
            Texture2D tex = TextBoxDrawing.Resolve(texture, out bool sizeChanged);
            if (sizeChanged && texture == null)
            {
                InvalidateLayout();
            }

            TextBoxDrawing.DrawBox(b, tex, Bounds, Enabled ? Color.White : Color.Gray);

            string value = Value;
            bool focused = Enabled && IsFocused;
            string placeholder = value.Length == 0 && !focused ? CurrentPlaceholder : string.Empty;
            if (placeholder.Length > 0)
            {
                TextBoxDrawing.DrawText(b, Bounds, placeholder, style.Font, style.DisabledTextColor, false, false);
                return;
            }

            Color textColor = Enabled ? style.TextColor : style.DisabledTextColor;
            TextBoxDrawing.DrawText(b, Bounds, value, style.Font, textColor, style.TextShadow, focused);
        }

        /// <summary>The placeholder text right now (empty when none is setter).</summary>
        private string CurrentPlaceholder => PlaceholderFunc == null ? string.Empty : Pseudo.Transform(Raise("Placeholder", PlaceholderFunc, string.Empty));

        // ---------------------------------------------------------------------------------------------------------
        //  Keyboard
        // ---------------------------------------------------------------------------------------------------------

        protected internal override void HandleTextInput(char c)
        {
            if (!Enabled)
            {
                return;
            }

            if (c == '"')
            {
                return; // vanilla: never inserted, no sound
            }

            if (!TryCommit(Value + c))
            {
                return;
            }

            // screen readers echo the typed character rather than the whole value
            Accessibility.Announce(c.ToString());
            switch (c)
            {
                case '$':
                    UIServices.PlaySound("money");
                    break;
                case '*':
                    UIServices.PlaySound("hammer");
                    break;
                case '+':
                    UIServices.PlaySound("slimeHit");
                    break;
                case '<':
                    UIServices.PlaySound("crystal");
                    break;
                case '=':
                    UIServices.PlaySound("coin");
                    break;
                default:
                    UIServices.PlaySound(Theme.TypeSound);
                    break;
            }
        }

        /// <summary>Paste: insert the whole string (without quotes and line breaks, truncated to <see cref="MaxLength"/>) as one edit, silently.</summary>
        protected internal override void HandleTextInput(string text)
        {
            if (!Enabled || string.IsNullOrEmpty(text))
            {
                return;
            }

            // typing never inserts quotes or line breaks (single-line box): a paste gets the same filter
            text = text.Replace("\"", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
            if (text.Length == 0)
            {
                return;
            }

            string current = Value;
            string prospective = current + text;
            if (MaxLength > 0 && prospective.Length > MaxLength)
            {
                prospective = prospective.Substring(0, MaxLength);
            }

            if (TryCommit(prospective))
            {
                Accessibility.AnnounceValue(this);
            }
        }

        protected internal override void HandleCommandInput(char command)
        {
            if (!Enabled || command != '\b')
            {
                return;
            }

            string current = Value;
            if (current.Length == 0)
            {
                return;
            }

            if (TryCommit(current.Substring(0, current.Length - 1)))
            {
                UIServices.PlaySound(Theme.BackspaceSound);
                Accessibility.AnnounceValue(this);
            }
        }

        protected internal override void HandleSpecialInput(Keys key)
        {
            // nothing: the router handles Tab / Escape / arrows through HandleKey
        }

        protected internal override bool HandleKey(UIKeyEvent e)
        {
            if (base.HandleKey(e))
            {
                return true;
            }

            if (e.Key == Keys.Enter)
            {
                // with no OnSubmit, Enter falls through to the menu's DefaultButton
                if (OnSubmit == null)
                {
                    return false;
                }

                Action<IUIElement> cb = OnSubmit;
                Raise("OnSubmit", () => cb(this));
                return true;
            }
            // typing keys are consumed so the game's menu button (E) does not close the menu; Tab / Escape / arrows and
            // Ctrl shortcuts (Ctrl+Z undo in a form) fall through
            return !e.Ctrl && TextBoxDrawing.IsTypingKey(e.Key);
        }
    }
}
