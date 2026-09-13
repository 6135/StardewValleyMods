using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    /// <summary>
    /// Vanilla text box look shared by <see cref="TextInput"/> and <see cref="NumberInput"/>: the three-slice draw of
    /// <c>LooseSprites\textBox</c> (16px caps), text at (+16, +12), a 4x32 caret blinking on a one second cycle and
    /// text scrolled in from the left when it no longer fits.
    /// </summary>
    internal static class TextBoxDrawing
    {
        /// <summary>Width of the left / right caps of the box texture.</summary>
        public const int CapWidth = 16;

        /// <summary>Text offset from the box origin (vanilla <c>TextBox</c>).</summary>
        public const int TextOffsetX = 16;
        public const int TextOffsetY = 12;

        /// <summary>Caret rectangle (vanilla <c>TextBox</c>).</summary>
        public const int CaretOffsetY = 8;
        public const int CaretWidth = 4;
        public const int CaretHeight = 32;

        /// <summary>Horizontal room reserved for the caps and caret when clipping text (<c>TextOption</c>'s write bar offset).</summary>
        public const int TextInset = 26;

        /// <summary>Height of the vanilla box; the text / caret offsets above are relative to it.</summary>
        public const int VanillaHeight = 48;

        private static Point vanillaSize = new(192, VanillaHeight);

        /// <summary>
        /// Size of the vanilla text box texture. Assumed to be 192x48 until the texture is first drawn so layout
        /// never needs the game content; <see cref="Resolve"/> corrects it if a retexture changed the size.
        /// </summary>
        public static Point DefaultSize => vanillaSize;

        /// <summary>Natural size of a box using <paramref name="custom"/> (or the vanilla texture when null).</summary>
        public static Vector2 Measure(Texture2D? custom)
        {
            return custom != null ? new Vector2(custom.Width, custom.Height) : vanillaSize.ToVector2();
        }

        /// <summary>The texture to draw: <paramref name="custom"/> or the vanilla one. <paramref name="sizeChanged"/> is true when the vanilla size assumption was wrong (re-layout).</summary>
        public static Texture2D Resolve(Texture2D? custom, out bool sizeChanged)
        {
            sizeChanged = false;
            if (custom != null)
                return custom;
            Texture2D tex = UIServices.TextBoxTexture;
            var size = new Point(tex.Width, tex.Height);
            if (size != vanillaSize)
            {
                vanillaSize = size;
                sizeChanged = true;
            }
            return tex;
        }

        /// <summary>Draw the box as three slices (left cap, stretched middle, right cap) into <paramref name="bounds"/>.</summary>
        public static void DrawBox(SpriteBatch b, Texture2D texture, Rectangle bounds, Color tint)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;
            int width = Math.Max(bounds.Width, 2 * CapWidth);
            int texH = texture.Height;
            b.Draw(texture, new Rectangle(bounds.X, bounds.Y, CapWidth, bounds.Height), new Rectangle(0, 0, CapWidth, texH), tint);
            b.Draw(texture, new Rectangle(bounds.X + CapWidth, bounds.Y, width - 2 * CapWidth, bounds.Height), new Rectangle(CapWidth, 0, 4, texH), tint);
            b.Draw(texture, new Rectangle(bounds.X + width - CapWidth, bounds.Y, CapWidth, bounds.Height), new Rectangle(texture.Width - CapWidth, 0, CapWidth, texH), tint);
        }

        /// <summary>Drop characters from the left until <paramref name="text"/> fits in <paramref name="maxWidth"/> pixels.</summary>
        public static string ClipLeft(UIFont font, string text, int maxWidth)
        {
            while (text.Length > 0 && UIServices.Text.Measure(font, text, 1f).X > maxWidth)
                text = text.Substring(1);
            return text;
        }

        /// <summary>Whether the caret is in the visible half of its blink cycle.</summary>
        public static bool CaretVisible => UIServices.NowMs() % 1000 >= 500;

        /// <summary>Draw the (clipped) text and, when <paramref name="caret"/> is set, the blinking caret after it.</summary>
        public static void DrawText(SpriteBatch b, Rectangle bounds, string text, UIFont font, Color color, bool shadow, bool caret)
        {
            int centerShift = (bounds.Height - VanillaHeight) / 2;
            string visible = ClipLeft(font, text, bounds.Width - TextInset);
            Vector2 size = visible.Length > 0 ? UIServices.Text.Measure(font, visible, 1f) : Vector2.Zero;

            if (caret && CaretVisible)
            {
                var caretRect = new Rectangle(bounds.X + TextOffsetX + (int)size.X + 2, bounds.Y + CaretOffsetY + centerShift, CaretWidth, CaretHeight);
                b.Draw(Game1.staminaRect, caretRect, color);
            }
            if (visible.Length > 0)
                DrawHelper.Text(b, visible, font, new Vector2(bounds.X + TextOffsetX, bounds.Y + TextOffsetY + centerShift), color, shadow, 1f);
        }

        /// <summary>
        /// Keys a text field consumes as typing (letters, digits, punctuation, space, backspace, delete). Returning
        /// true for them from <c>HandleKey</c> keeps the game's menu button from closing the menu mid-sentence.
        /// </summary>
        public static bool IsTypingKey(Keys key)
        {
            if (key >= Keys.A && key <= Keys.Z)
                return true;
            if (key >= Keys.D0 && key <= Keys.D9)
                return true;
            if (key >= Keys.NumPad0 && key <= Keys.Divide)
                return true;
            switch (key)
            {
                case Keys.Space:
                case Keys.Back:
                case Keys.Delete:
                case Keys.OemSemicolon:
                case Keys.OemPlus:
                case Keys.OemComma:
                case Keys.OemMinus:
                case Keys.OemPeriod:
                case Keys.OemQuestion:
                case Keys.OemTilde:
                case Keys.OemOpenBrackets:
                case Keys.OemPipe:
                case Keys.OemCloseBrackets:
                case Keys.OemQuotes:
                case Keys.Oem8:
                case Keys.OemBackslash:
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Single-line text entry with the vanilla text box look. Focusable and takes keyboard input through the menu's
    /// keyboard subscriber while focused; every change goes through <see cref="MaxLength"/> and <see cref="ValidateFunc"/>
    /// before the bound setter and <see cref="OnValueChanged"/>. Enter raises <see cref="OnSubmit"/>.
    /// </summary>
    internal sealed class TextInput : UIElement, IUITextInput
    {
        private readonly Func<string>? get;
        private readonly Action<string>? set;
        private string ownValue = string.Empty;
        private Texture2D? texture;

        public TextInput(string id, Func<string>? get, Action<string>? set) : base(id)
        {
            this.get = get;
            this.set = set;
        }

        /// <summary>
        /// The current text: the bound getter when one was supplied, otherwise the element's own value. Setting it
        /// writes through the bound setter (or stores it) without validation or <see cref="OnValueChanged"/>.
        /// </summary>
        public string Value
        {
            get => get != null ? Raise("get", get, ownValue) ?? string.Empty : ownValue;
            set => Store(value ?? string.Empty);
        }

        /// <summary>Text shown (dimmed) while the value is empty and the box is not focused.</summary>
        public Func<string>? PlaceholderFunc { get; set; }

        Func<string> IUITextInput.Placeholder { get => PlaceholderFunc!; set => PlaceholderFunc = value; }

        /// <summary>Maximum characters (0 = unlimited).</summary>
        public int MaxLength { get; set; }

        /// <summary>Called with the prospective value before it is applied; false rejects the edit silently.</summary>
        public Func<string, bool>? ValidateFunc { get; set; }

        Func<string, bool> IUITextInput.Validate { get => ValidateFunc!; set => ValidateFunc = value; }

        /// <summary>Custom box texture (null = vanilla <c>LooseSprites\textBox</c>). Changes the natural size.</summary>
        public Texture2D? Texture
        {
            get => texture;
            set
            {
                texture = value;
                InvalidateLayout();
            }
        }

        Texture2D IUITextInput.Texture { get => texture!; set => Texture = value; }

        public Action<IUIValueEvent>? OnValueChanged { get; set; }

        Action<IUIValueEvent> IUITextInput.OnValueChanged { get => OnValueChanged!; set => OnValueChanged = value; }

        public Action<IUIElement>? OnSubmit { get; set; }

        Action<IUIElement> IUITextInput.OnSubmit { get => OnSubmit!; set => OnSubmit = value; }

        public override bool Focusable => true;
        public override bool WantsTextInput => true;

        // ---------------------------------------------------------------------------------------------------------
        //  Value pipeline
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Write a value to the bound setter (or the own value) without raising events.</summary>
        private void Store(string value)
        {
            ownValue = value;
            if (set != null)
            {
                Action<string> setter = set;
                Raise("set", () => setter(value));
            }
        }

        /// <summary>
        /// Apply a prospective value: enforce <see cref="MaxLength"/>, run <see cref="ValidateFunc"/>, store and raise
        /// <see cref="OnValueChanged"/>. Returns false when nothing changed (rejected or identical).
        /// </summary>
        private bool TryCommit(string newValue)
        {
            if (MaxLength > 0 && newValue.Length > MaxLength)
                return false;
            string oldValue = Value;
            if (oldValue == newValue)
                return false;
            if (ValidateFunc != null)
            {
                Func<string, bool> validate = ValidateFunc;
                if (!Raise("Validate", () => validate(newValue), false))
                    return false;
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

        protected override Vector2 MeasureCore(Vector2 available) => TextBoxDrawing.Measure(texture);

        protected override void DrawCore(SpriteBatch b)
        {
            ResolvedStyle style = Style;
            Texture2D tex = TextBoxDrawing.Resolve(texture, out bool sizeChanged);
            if (sizeChanged && texture == null)
                InvalidateLayout();

            TextBoxDrawing.DrawBox(b, tex, Bounds, Enabled ? Color.White : Color.Gray);

            string value = Value;
            bool focused = Enabled && IsFocused;
            if (value.Length == 0 && !focused && PlaceholderFunc != null)
            {
                string placeholder = Raise("Placeholder", PlaceholderFunc, string.Empty) ?? string.Empty;
                if (placeholder.Length > 0)
                {
                    TextBoxDrawing.DrawText(b, Bounds, placeholder, style.Font, style.TextColor * 0.5f, false, false);
                    return;
                }
            }

            Color textColor = Enabled ? style.TextColor : style.TextColor * 0.5f;
            TextBoxDrawing.DrawText(b, Bounds, value, style.Font, textColor, style.TextShadow, focused);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Keyboard
        // ---------------------------------------------------------------------------------------------------------

        protected internal override void HandleTextInput(char c)
        {
            if (!Enabled)
                return;
            if (c == '"')
                return; // vanilla: never inserted, no sound
            if (!TryCommit(Value + c))
                return;
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

        /// <summary>Paste: insert the whole string (truncated to <see cref="MaxLength"/>) as one edit, silently.</summary>
        protected internal override void HandleTextInput(string text)
        {
            if (!Enabled || string.IsNullOrEmpty(text))
                return;
            string current = Value;
            string prospective = current + text;
            if (MaxLength > 0 && prospective.Length > MaxLength)
                prospective = prospective.Substring(0, MaxLength);
            TryCommit(prospective);
        }

        protected internal override void HandleCommandInput(char command)
        {
            if (!Enabled || command != '\b')
                return;
            string current = Value;
            if (current.Length == 0)
                return;
            if (TryCommit(current.Substring(0, current.Length - 1)))
                UIServices.PlaySound(Theme.BackspaceSound);
        }

        protected internal override void HandleSpecialInput(Keys key)
        {
            // nothing: the router handles Tab / Escape / arrows through HandleKey
        }

        protected internal override bool HandleKey(UIKeyEvent e)
        {
            if (base.HandleKey(e))
                return true;
            if (e.Key == Keys.Enter)
            {
                // with no OnSubmit, Enter falls through to the menu's DefaultButton
                if (OnSubmit == null)
                    return false;
                Action<IUIElement> cb = OnSubmit;
                Raise("OnSubmit", () => cb(this));
                return true;
            }
            // typing keys are consumed so the game's menu button (E) does not close the menu; Tab / Escape / arrows fall through
            return TextBoxDrawing.IsTypingKey(e.Key);
        }
    }
}
