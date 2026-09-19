using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    /// <summary>
    /// A vanilla checkbox (<c>OptionsCheckbox</c> sprites at 4x) with an optional label to its right. Focusable;
    /// a click, Enter, Space or gamepad A toggles the bound value and raises <see cref="OnValueChanged"/>.
    /// </summary>
    internal sealed class Checkbox : UIElement, IUICheckbox
    {
        private const int LabelGap = 8;

        private Func<bool>? getter;
        private Action<bool>? setter;
        private bool ownValue;
        private Func<string>? label;
        private string measuredLabel = string.Empty;

        internal Checkbox(string id, Func<bool>? getter, Action<bool>? setter) : base(id)
        {
            this.getter = getter;
            this.setter = setter;
        }

        /// <summary>Size of the box in UI pixels (9px sprite at the theme scale).</summary>
        private static int BoxSize => Theme.CheckboxChecked.Width * Theme.PixelScale;

        /// <summary>
        /// The current value: the bound getter when one was supplied, otherwise the element's own value. Setting it
        /// writes through the bound setter (or stores it) without raising <see cref="OnValueChanged"/>.
        /// </summary>
        public bool Value
        {
            get => getter != null ? Raise("get", getter, ownValue) : ownValue;
            set => Store(value);
        }

        /// <summary>The value delegates the element currently reads / writes through (null = own value).</summary>
        internal Func<bool>? BoundGetter => getter;

        internal Action<bool>? BoundSetter => setter;

        /// <summary>Swap the value delegates after construction (signal bindings); the own value is kept as the fallback.</summary>
        internal void Rebind(Func<bool>? newGetter, Action<bool>? newSetter)
        {
            getter = newGetter;
            setter = newSetter;
        }

        internal Func<string>? LabelFunc
        {
            get => label;
            set
            {
                label = value;
                InvalidateLayout();
            }
        }

        Func<string> IUICheckbox.Label { get => label!; set => LabelFunc = value; }

        /// <summary>null = theme default, empty = silent.</summary>
        internal string? ClickSound { get; set; }

        string IUICheckbox.ClickSound { get => ClickSound!; set => ClickSound = value; }

        internal Action<IUIValueEvent>? OnValueChanged { get; set; }

        Action<IUIValueEvent> IUICheckbox.OnValueChanged { get => OnValueChanged!; set => OnValueChanged = value; }

        internal override bool Focusable => true;
        internal override bool ActivateOnEnter => true;

        internal string CurrentLabel => Pseudo.Transform(Raise("Label", label, string.Empty) ?? string.Empty);

        internal override string AccessibleDescription => Accessibility.Compose(
            Accessibility.Text("checkbox", "Checkbox"),
            CurrentLabel,
            Value ? Accessibility.Text("checked", "checked") : Accessibility.Text("unchecked", "unchecked"),
            Enabled ? null : Accessibility.Text("disabled", "disabled"));

        // ---------------------------------------------------------------------------------------------------------
        //  Value pipeline
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Write a value to the bound setter (or the own value) without raising events.</summary>
        private void Store(bool value)
        {
            ownValue = value;
            if (setter != null)
            {
                Action<bool> bound = setter;
                Raise("set", () => bound(value));
            }
        }

        /// <summary>Flip the value: store, play the click sound, raise <see cref="OnValueChanged"/>.</summary>
        private void Toggle()
        {
            bool oldValue = Value;
            bool newValue = !oldValue;
            Store(newValue);
            UIServices.PlaySound(ClickSound ?? Style.ClickSound ?? Theme.CheckboxClickSound);
            if (OnValueChanged != null)
            {
                Action<IUIValueEvent> cb = OnValueChanged;
                UIValueEvent e = UIValueEvent.Bool(this, oldValue, newValue);
                Raise("OnValueChanged", () => cb(e));
            }
            Accessibility.AnnounceValue(this);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Layout / draw
        // ---------------------------------------------------------------------------------------------------------

        protected override Vector2 MeasureCore(Vector2 available)
        {
            measuredLabel = CurrentLabel;
            int box = BoxSize;
            if (measuredLabel.Length == 0)
            {
                return new Vector2(box, box);
            }

            Vector2 textSize = UIServices.Text.Measure(Style.Font, measuredLabel, 1f);
            return new Vector2(box + Theme.Space(LabelGap) + textSize.X, Math.Max(box, textSize.Y));
        }

        protected override void DrawCore(SpriteBatch b)
        {
            ResolvedStyle style = Style;
            bool highlighted = Enabled && (IsHovered || IsFocused);
            Color tint = Theme.StateTint(Enabled, highlighted, style.HoverColor);

            int box = BoxSize;
            var boxPos = new Vector2(Bounds.X, Bounds.Y + ((Bounds.Height - box) / 2));
            b.Draw(Game1.mouseCursors, boxPos, Value ? Theme.CheckboxChecked : Theme.CheckboxUnchecked, tint, 0f, Vector2.Zero, Theme.PixelScale, SpriteEffects.None, 0f);

            string current = CurrentLabel;
            if (current != measuredLabel)
            {
                measuredLabel = current;
                InvalidateLayout();
            }
            if (current.Length == 0)
            {
                return;
            }

            Vector2 textSize = UIServices.Text.Measure(style.Font, current, 1f);
            Color textColor = Enabled ? style.TextColor : style.DisabledTextColor;
            int textX = Bounds.X + box + Theme.Space(LabelGap);
            var textRect = new Rectangle(textX, (int)(Bounds.Y + ((Bounds.Height - textSize.Y) / 2f)), Math.Max(0, Bounds.Right - textX), (int)textSize.Y);
            DrawHelper.FitText(b, current, style.Font, textRect, textColor, style.TextShadow, 1f, UIAlign.Start);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Input
        // ---------------------------------------------------------------------------------------------------------

        protected internal override bool HandleClick(UIClickEvent e)
        {
            if (e.Target == this && e.Button == UIMouseButton.Left && Enabled)
            {
                Toggle();
            }

            return base.HandleClick(e);
        }

        protected internal override bool HandleActivate()
        {
            if (!Enabled || !Visible)
            {
                return false;
            }
            // synthesize a click at the center so the toggle, bubbling and callbacks behave exactly like a mouse click
            var e = new UIClickEvent(this, Bounds.Center.X, Bounds.Center.Y, UIMouseButton.Left);
            EventRouter.Bubble(e);
            return true;
        }
    }
}
