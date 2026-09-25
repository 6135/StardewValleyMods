using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    /// <summary>
    /// Numeric entry with the vanilla text box look (Profit Calculator's <c>UIntOption</c> generalized to doubles).
    /// While focused the user edits a text buffer (digits, a leading '-' when <see cref="Min"/> is negative and one
    /// '.' when <see cref="Decimals"/> &gt; 0); every keystroke parses the buffer, clamps / rounds it and commits the
    /// number through <see cref="ValidateFunc"/>, the bound setter and <see cref="OnValueChanged"/>. Up / Down and the
    /// wheel step by <see cref="Step"/>; losing focus re-clamps; Enter raises <see cref="OnSubmit"/>.
    /// </summary>
    internal sealed class NumberInput : UIElement, IUINumberInput
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        private Func<double>? getter;
        private Action<double>? setter;
        private double ownValue;
        private int decimals;
        private Texture2D? texture;

        /// <summary>Text being edited while focused; null otherwise (the bound value is displayed).</summary>
        private string? buffer;

        /// <summary>True when <see cref="buffer"/> was auto-filled after clearing, so the next digit replaces it.</summary>
        private bool bufferIsPlaceholder;

        internal NumberInput(string id, Func<double>? getter, Action<double>? setter, double min, double max, double step, bool clamp) : base(id)
        {
            this.getter = getter;
            this.setter = setter;
            Min = min;
            Max = max;
            Step = step;
            Clamp = clamp;
        }

        /// <summary>
        /// The current number: the bound getter when one was supplied, otherwise the element's own value. Setting it
        /// writes through the bound setter (or stores it) without validation or <see cref="OnValueChanged"/>.
        /// </summary>
        public double Value
        {
            get => getter != null ? Raise("get", getter, ownValue) : ownValue;
            set => WriteValue(value);
        }

        /// <summary>Store a programmatic value and, while editing, mirror it into the buffer.</summary>
        private void WriteValue(double value)
        {
            Store(value);
            if (buffer != null)
            {
                SetBuffer(Trim(value));
            }
        }

        /// <summary>The value delegates the element currently reads / writes through (null = own value).</summary>
        internal Func<double>? BoundGetter => getter;

        internal Action<double>? BoundSetter => setter;

        /// <summary>Swap the value delegates after construction (signal bindings); the own value is kept as the fallback.</summary>
        internal void Rebind(Func<double>? newGetter, Action<double>? newSetter)
        {
            getter = newGetter;
            setter = newSetter;
        }

        public double Min { get; set; }
        public double Max { get; set; }

        /// <summary>Increment for Up / Down and the wheel (&lt;= 0 steps by 1).</summary>
        public double Step { get; set; }

        /// <summary>Keep typed values inside [<see cref="Min"/>, <see cref="Max"/>].</summary>
        public bool Clamp { get; set; }

        /// <summary>Decimal places accepted / displayed (0 = integers only).</summary>
        public int Decimals
        {
            get => decimals;
            set => decimals = Math.Clamp(value, 0, 15);
        }

        /// <summary>Called with the prospective value before it is applied; false rejects the edit silently.</summary>
        internal Func<double, bool>? ValidateFunc { get; set; }

        Func<double, bool> IUINumberInput.Validate { get => ValidateFunc!; set => ValidateFunc = value; }

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

        Texture2D IUINumberInput.Texture { get => texture!; set => Texture = value; }

        internal Action<IUIValueEvent>? OnValueChanged { get; set; }

        Action<IUIValueEvent> IUINumberInput.OnValueChanged { get => OnValueChanged!; set => OnValueChanged = value; }

        internal Action<IUIElement>? OnSubmit { get; set; }

        Action<IUIElement> IUINumberInput.OnSubmit { get => OnSubmit!; set => OnSubmit = value; }

        internal override bool Focusable => true;
        internal override bool WantsTextInput => true;

        internal override string AccessibleDescription => Accessibility.Compose(
            Accessibility.Text("number-input", "Number input"),
            buffer != null && IsFocused ? buffer : Format(Value),
            Enabled ? null : Accessibility.Text("disabled", "disabled"));

        private double EffectiveStep => Step > 0 ? Step : 1;

        // ---------------------------------------------------------------------------------------------------------
        //  Number helpers
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Round to <see cref="Decimals"/> and, when <see cref="Clamp"/> is setter (or <paramref name="forceClamp"/>), clamp to the range.</summary>
        private double Normalize(double value, bool forceClamp = false)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                value = 0;
            }

            value = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
            if (Clamp || forceClamp)
            {
                value = ClampToRange(value);
            }

            return value;
        }

        private double ClampToRange(double value)
        {
            double lo = Math.Min(Min, Max);
            double hi = Math.Max(Min, Max);
            return Math.Clamp(value, lo, hi);
        }

        /// <summary>Shortest text for a value (no trailing zeros), used for the edit buffer.</summary>
        private string Trim(double value)
        {
            string format = decimals > 0 ? "0." + new string('#', decimals) : "0";
            return value.ToString(format, Culture);
        }

        /// <summary>Display text when not editing: fixed <see cref="Decimals"/> places.</summary>
        private string Format(double value) => value.ToString("F" + decimals, Culture);

        /// <summary>The value a cleared box falls back to: 0, or the nearest bound when 0 is out of range.</summary>
        private double EmptyValue => Math.Clamp(0, Math.Min(Min, Max), Math.Max(Min, Max));

        private static bool TryParse(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Culture, out value);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Value pipeline
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Write a value to the bound setter (or the own value) without raising events.</summary>
        private void Store(double value)
        {
            ownValue = value;
            if (setter != null)
            {
                Action<double> bound = setter;
                Raise("set", () => bound(value));
            }
        }

        /// <summary>Validate, store and raise <see cref="OnValueChanged"/> for an already normalized value. Returns false when rejected or unchanged.</summary>
        private bool TryCommit(double newValue)
        {
            double oldValue = Value;
            if (Numbers.Same(oldValue, newValue))
            {
                return false;
            }

            if (ValidateFunc != null)
            {
                Func<double, bool> validate = ValidateFunc;
                if (!Raise("Validate", () => validate(newValue), false))
                {
                    return false;
                }
            }
            Store(newValue);
            if (OnValueChanged != null)
            {
                Action<IUIValueEvent> cb = OnValueChanged;
                UIValueEvent e = UIValueEvent.Number(this, oldValue, newValue);
                Raise("OnValueChanged", () => cb(e));
            }
            Accessibility.AnnounceValue(this);
            return true;
        }

        private void SetBuffer(string text, bool placeholder = false)
        {
            buffer = text;
            bufferIsPlaceholder = placeholder;
        }

        /// <summary>
        /// Parse the edit buffer and commit the number it represents. Buffers that do not parse yet ("-", ".", "")
        /// leave the value alone; a value changed by clamping / rounding is written back into the buffer (as
        /// <c>UIntOption</c> does); a value rejected by <see cref="ValidateFunc"/> restores <paramref name="previous"/>.
        /// </summary>
        private void ApplyBuffer(string previous)
        {
            if (buffer == null || !TryParse(buffer, out double parsed))
            {
                return;
            }

            double normalized = Normalize(parsed);
            if (Numbers.Same(normalized, Value))
            {
                if (!Numbers.Same(normalized, parsed))
                {
                    SetBuffer(Trim(normalized));
                }

                return;
            }
            if (!TryCommit(normalized))
            {
                SetBuffer(previous);
                return;
            }
            if (!Numbers.Same(normalized, parsed))
            {
                SetBuffer(Trim(normalized));
            }
        }

        /// <summary>Move the value by <paramref name="direction"/> steps (clamped to the range) and commit it.</summary>
        private void StepBy(int direction)
        {
            double current = Value;
            double target = Normalize(current + (direction * EffectiveStep), forceClamp: true);
            if (Numbers.Same(target, current) || !TryCommit(target))
            {
                return;
            }

            if (buffer != null)
            {
                SetBuffer(Trim(target));
            }
        }

        /// <summary>Type one character into the buffer. Returns true if it was accepted.</summary>
        private bool Insert(char c)
        {
            if (buffer == null)
            {
                SetBuffer(Trim(Value));
            }

            string previous = buffer!;
            string? next = NextBuffer(c, previous);
            if (next == null)
            {
                return false;
            }

            SetBuffer(next);
            ApplyBuffer(previous);
            return true;
        }

        /// <summary>The buffer after typing <paramref name="c"/> onto <paramref name="previous"/>, or null when the character is rejected.</summary>
        private string? NextBuffer(char c, string previous)
        {
            if (char.IsDigit(c))
            {
                return InsertDigit(c, previous);
            }

            if (c == '-')
            {
                return InsertMinus(previous);
            }

            if (c == '.')
            {
                return InsertDecimalPoint(previous);
            }

            return null;
        }

        /// <summary>A digit replaces a placeholder / lone zero, otherwise appends.</summary>
        private string InsertDigit(char c, string previous)
        {
            if (bufferIsPlaceholder || previous == "0")
            {
                return c.ToString();
            }

            if (previous == "-0")
            {
                return "-" + c;
            }

            return previous + c;
        }

        /// <summary>A leading minus is only allowed on an empty buffer when the range goes below zero.</summary>
        private string? InsertMinus(string previous)
        {
            if (Min >= 0 || (previous.Length > 0 && !bufferIsPlaceholder))
            {
                return null;
            }

            return "-";
        }

        /// <summary>One decimal point, only when decimals are accepted; an empty / sign-only buffer gets a leading zero.</summary>
        private string? InsertDecimalPoint(string previous)
        {
            if (decimals <= 0 || (previous.Contains('.') && !bufferIsPlaceholder))
            {
                return null;
            }

            if (bufferIsPlaceholder)
            {
                return "0.";
            }

            if (previous.Length == 0 || previous == "-")
            {
                return previous + "0.";
            }

            return previous + ".";
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

            bool focused = Enabled && IsFocused;
            string text = focused && buffer != null ? buffer : Format(Value);
            Color textColor = Enabled ? style.TextColor : style.DisabledTextColor;
            TextBoxDrawing.DrawText(b, Bounds, text, style.Font, textColor, style.TextShadow, focused);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Focus
        // ---------------------------------------------------------------------------------------------------------

        protected internal override void HandleFocusGained()
        {
            SetBuffer(Trim(Value));
            base.HandleFocusGained();
        }

        /// <summary>Leaving the box re-clamps the value (like <c>UIntOption.BeforeReceiveLeftClick</c>) and drops the buffer.</summary>
        protected internal override void HandleFocusLost()
        {
            buffer = null;
            bufferIsPlaceholder = false;
            double current = Value;
            double normalized = Normalize(current);
            if (!Numbers.Same(normalized, current))
            {
                TryCommit(normalized);
            }

            base.HandleFocusLost();
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Keyboard / wheel
        // ---------------------------------------------------------------------------------------------------------

        protected internal override void HandleTextInput(char c)
        {
            if (!Enabled)
            {
                return;
            }

            if (Insert(c))
            {
                UIServices.PlaySound(Theme.TypeSound);
            }
        }

        /// <summary>Paste: feed each character through the same filter, silently.</summary>
        protected internal override void HandleTextInput(string text)
        {
            if (!Enabled || string.IsNullOrEmpty(text))
            {
                return;
            }

            foreach (char c in text)
            {
                Insert(c);
            }
        }

        protected internal override void HandleCommandInput(char command)
        {
            if (!Enabled || command != '\b')
            {
                return;
            }

            if (buffer == null)
            {
                SetBuffer(Trim(Value));
            }

            string previous = buffer!;
            if (previous.Length == 0)
            {
                return;
            }

            UIServices.PlaySound(Theme.BackspaceSound);
            string shorter = bufferIsPlaceholder ? string.Empty : previous.Substring(0, previous.Length - 1);
            if (shorter.Length == 0 || shorter == "-")
            {
                // cleared: fall back to 0 (or the nearest bound) and let the next digit replace it
                double empty = Normalize(EmptyValue);
                SetBuffer(Trim(empty), placeholder: true);
                if (!TryCommit(empty) && !Numbers.Same(Value, empty))
                {
                    SetBuffer(Trim(Value), placeholder: true);
                }

                return;
            }
            SetBuffer(shorter);
            ApplyBuffer(previous);
        }

        protected internal override void HandleSpecialInput(Keys key)
        {
            if (!Enabled)
            {
                return;
            }

            if (key == Keys.Up)
            {
                StepBy(1);
            }
            else if (key == Keys.Down)
            {
                StepBy(-1);
            }
            else
            {
                // other special keys (arrows, Home/End) are handled by the router / ignored
            }
        }

        /// <summary>Wheel over the box (while focused or hovered) steps the value.</summary>
        protected internal override bool HandleScroll(int direction)
        {
            if (!Enabled || direction == 0 || !(IsFocused || IsHovered))
            {
                return false;
            }

            StepBy(direction > 0 ? 1 : -1);
            return true;
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
            // Up / Down arrive through HandleSpecialInput (keyboard subscriber); consume them here so focus does not move
            if (e.Key == Keys.Up || e.Key == Keys.Down)
            {
                return true;
            }

            // Ctrl shortcuts (Ctrl+Z undo in a form) are not typing: let them bubble
            return !e.Ctrl && TextBoxDrawing.IsTypingKey(e.Key);
        }
    }
}
