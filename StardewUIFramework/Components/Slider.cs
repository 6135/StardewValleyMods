using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    /// <summary>
    /// A horizontal slider with the vanilla options look (<c>OptionsSlider</c>: 9-slice track, 10x6 knob at 4x).
    /// Clicking or dragging moves the knob under the cursor; Left / Right step while focused. The value is snapped
    /// to <see cref="Step"/>, clamped to [<see cref="Min"/>, <see cref="Max"/>] and committed through the bound
    /// setter plus <see cref="OnValueChanged"/> only when it actually changes.
    /// </summary>
    internal sealed class Slider : UIElement, IUISlider
    {
        private const int DefaultWidth = 192;
        private const int DefaultHeight = 24;

        private readonly Func<double>? get;
        private readonly Action<double>? set;
        private double ownValue;

        public Slider(string id, Func<double>? get, Action<double>? set, double min, double max) : base(id)
        {
            this.get = get;
            this.set = set;
            Min = min;
            Max = max;
        }

        /// <summary>
        /// The current value: the bound getter when one was supplied, otherwise the element's own value. Setting it
        /// writes through the bound setter (or stores it) without raising <see cref="OnValueChanged"/>.
        /// </summary>
        public double Value
        {
            get => get != null ? Raise("get", get, ownValue) : ownValue;
            set => Store(value);
        }

        public double Min { get; set; }
        public double Max { get; set; }

        /// <summary>Snap increment (0 = continuous).</summary>
        public double Step { get; set; }

        public Action<IUIValueEvent>? OnValueChanged { get; set; }

        Action<IUIValueEvent> IUISlider.OnValueChanged { get => OnValueChanged!; set => OnValueChanged = value; }

        public override bool Focusable => true;

        private static int KnobWidth => Theme.SliderKnob.Width * Theme.PixelScale;
        private static int KnobHeight => Theme.SliderKnob.Height * Theme.PixelScale;

        /// <summary>Pixels the knob can travel along the track.</summary>
        private int TrackTravel => Math.Max(1, Bounds.Width - KnobWidth);

        private double Low => Math.Min(Min, Max);
        private double High => Math.Max(Min, Max);

        /// <summary>Keyboard increment: <see cref="Step"/>, or a twentieth of the range when continuous.</summary>
        private double KeyStep => Step > 0 ? Step : (High - Low) / 20.0;

        // ---------------------------------------------------------------------------------------------------------
        //  Value pipeline
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Write a value to the bound setter (or the own value) without raising events.</summary>
        private void Store(double value)
        {
            ownValue = value;
            if (set != null)
            {
                Action<double> setter = set;
                Raise("set", () => setter(value));
            }
        }

        /// <summary>Snap to <see cref="Step"/> (relative to <see cref="Min"/>) and clamp to the range.</summary>
        private double Normalize(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                value = Low;
            if (Step > 0)
                value = Min + Math.Round((value - Min) / Step) * Step;
            return Math.Clamp(value, Low, High);
        }

        /// <summary>Normalize, then store and raise <see cref="OnValueChanged"/> if the value changed.</summary>
        private bool TryCommit(double value)
        {
            double newValue = Normalize(value);
            double oldValue = Value;
            if (newValue == oldValue)
                return false;
            Store(newValue);
            if (OnValueChanged != null)
            {
                Action<IUIValueEvent> cb = OnValueChanged;
                UIValueEvent e = UIValueEvent.Number(this, oldValue, newValue);
                Raise("OnValueChanged", () => cb(e));
            }
            return true;
        }

        /// <summary>Fraction (0..1) of the track the current value sits at.</summary>
        private float Fraction
        {
            get
            {
                double range = High - Low;
                if (range <= 0)
                    return 0f;
                return (float)Math.Clamp((Value - Low) / range, 0, 1);
            }
        }

        /// <summary>Set the value so the knob is centered under cursor x (clamped to the track ends).</summary>
        private void SetFromCursor(int px)
        {
            int travel = TrackTravel;
            double t = Math.Clamp((px - Bounds.X - KnobWidth / 2.0) / travel, 0, 1);
            TryCommit(Low + t * (High - Low));
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Layout / draw
        // ---------------------------------------------------------------------------------------------------------

        protected override Vector2 MeasureCore(Vector2 available) => new(DefaultWidth, DefaultHeight);

        protected override void DrawCore(SpriteBatch b)
        {
            if (Bounds.Width <= 0 || Bounds.Height <= 0)
                return;
            ResolvedStyle style = Style;
            bool highlighted = Enabled && (IsHovered || IsFocused);
            Color trackTint = Enabled ? Color.White : Color.Gray;
            Color knobTint = !Enabled ? Color.Gray : highlighted ? style.HoverColor : Color.White;

            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, Theme.SliderTrack, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height, trackTint, Theme.PixelScale, false);

            float knobX = Bounds.X + TrackTravel * Fraction;
            float knobY = Bounds.Y + (Bounds.Height - KnobHeight) / 2f;
            b.Draw(Game1.mouseCursors, new Vector2((int)knobX, (int)knobY), Theme.SliderKnob, knobTint, 0f, Vector2.Zero, Theme.PixelScale, SpriteEffects.None, 0f);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Input
        // ---------------------------------------------------------------------------------------------------------

        protected internal override bool HandleClick(UIClickEvent e)
        {
            if (e.Target == this && e.Button == UIMouseButton.Left && Enabled)
                SetFromCursor(e.X);
            return base.HandleClick(e);
        }

        /// <summary>Drag: the router keeps sending held positions to the element that took the click.</summary>
        protected internal override void HandleClickHeld(int px, int py)
        {
            if (Enabled)
                SetFromCursor(px);
        }

        protected internal override bool HandleKey(UIKeyEvent e)
        {
            if (base.HandleKey(e))
                return true;
            if (!Enabled)
                return false;
            switch (e.Key)
            {
                case Keys.Left:
                    TryCommit(Value - KeyStep);
                    return true;
                case Keys.Right:
                    TryCommit(Value + KeyStep);
                    return true;
                default:
                    return false;
            }
        }
    }
}
