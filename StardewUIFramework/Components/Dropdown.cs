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
    /// A vanilla-looking dropdown (<c>OptionsDropDown</c> sprites): a closed box showing the selected label plus an arrow
    /// button; clicking it opens a scrollable list of <see cref="MaxVisible"/> rows as an overlay popup that gets first
    /// pick at input and is closed (with the click swallowed) when the user clicks anywhere else.
    /// <para>
    /// The selection is bound through the <c>get</c> / <c>set</c> delegates when given (the getter is read every frame so
    /// external changes show immediately); otherwise the element holds its own selected index. Keyboard: Up / Down change
    /// the selection while closed and move the highlight while open, Enter / Space open or commit, Escape closes
    /// (handled by the router).
    /// </para>
    /// </summary>
    internal sealed class Dropdown : UIElement, IUIDropdown
    {
        /// <summary>Height of the closed box and of every row in the open list.</summary>
        internal const int DropdownRowHeight = 44;

        private const int DefaultWidth = 300;
        private const int ButtonWidth = 48;
        private const int TextPadX = 4;
        private const int TextPadY = 8;
        private const int HighlightInset = 4;
        private const float SpriteScale = 4f;

        private readonly Func<string[]>? choicesFunc;
        private readonly Func<string[]>? labelsFunc;
        private readonly Func<string>? getter;
        private readonly Action<string>? setter;
        private string[] choices = Array.Empty<string>();
        private string[] labels = Array.Empty<string>();
        private int ownIndex = -1;
        private int maxVisible = 5;
        private int highlightIndex = -1;
        private int activePosition;

        internal Dropdown(string id, Func<string[]>? choices, Func<string[]>? labels, Func<string>? getter, Action<string>? setter) : base(id)
        {
            choicesFunc = choices;
            labelsFunc = labels;
            this.getter = getter;
            this.setter = setter;
            RefreshChoices();
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Selection
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Index of the selected choice: derived from the bound getter when there is one (-1 when its value is not a
        /// choice), otherwise the element's own state. Setting it writes through the setter without raising
        /// <see cref="OnValueChanged"/> (that is reserved for user interaction).
        /// </summary>
        public int SelectedIndex
        {
            get
            {
                if (getter == null)
                {
                    return ownIndex;
                }

                string? value = Raise("get", getter, string.Empty);
                return value == null ? -1 : Array.IndexOf(choices, value);
            }
            set
            {
                if (value >= 0 && value < choices.Length)
                {
                    ownIndex = value;
                    if (setter != null)
                    {
                        string choice = choices[value];
                        Raise("set", () => setter(choice));
                    }
                }
                else
                {
                    ownIndex = -1;
                }
            }
        }

        public string SelectedValue
        {
            get => GetChoice(SelectedIndex);
            set => SelectedIndex = Array.IndexOf(choices, value);
        }

        public int MaxVisible
        {
            get => maxVisible;
            set
            {
                maxVisible = Math.Max(1, value);
                ClampActivePosition();
            }
        }

        /// <summary>Index of the first visible row of the open list.</summary>
        internal int ActivePosition
        {
            get => activePosition;
            set => activePosition = Math.Clamp(value, 0, MaxPosition);
        }

        private int MaxPosition => Math.Max(0, choices.Length - maxVisible);

        /// <summary>Re-clamp the scroll position after the choice count or <see cref="MaxVisible"/> changed.</summary>
        private void ClampActivePosition() => activePosition = Math.Clamp(activePosition, 0, MaxPosition);

        public bool IsOpen { get; private set; }

        public int ChoiceCount => choices.Length;

        public string GetChoice(int index) => index >= 0 && index < choices.Length ? choices[index] : string.Empty;

        public string GetLabel(int index) => index >= 0 && index < labels.Length ? labels[index] : GetChoice(index);

        /// <summary>Re-evaluate the choices / labels delegates; labels fall back to the choices when missing or mismatched.</summary>
        public void RefreshChoices()
        {
            string previous = getter == null ? GetChoice(ownIndex) : string.Empty;

            choices = Raise("choices", choicesFunc, Array.Empty<string>()) ?? Array.Empty<string>();
            string[]? newLabels = labelsFunc == null ? null : Raise("labels", labelsFunc, choices);
            labels = newLabels != null && newLabels.Length == choices.Length ? newLabels : choices;

            if (getter == null)
            {
                ownIndex = RestoreOwnIndex(previous);
            }

            ClampActivePosition();
            if (highlightIndex >= choices.Length)
            {
                highlightIndex = -1;
            }
        }

        /// <summary>After the choices changed: keep the previously selected value if it still exists, else the first choice (or none).</summary>
        private int RestoreOwnIndex(string previous)
        {
            int found = previous.Length > 0 ? Array.IndexOf(choices, previous) : -1;
            if (found >= 0)
            {
                return found;
            }

            return choices.Length > 0 ? 0 : -1;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Consumer callbacks
        // ---------------------------------------------------------------------------------------------------------

        internal Action<IUIValueEvent>? OnValueChanged { get; set; }
        Action<IUIValueEvent> IUIDropdown.OnValueChanged { get => OnValueChanged!; set => OnValueChanged = value; }

        internal Action<int>? OnScroll { get; set; }
        Action<int> IUIDropdown.OnScroll { get => OnScroll!; set => OnScroll = value; }

        internal override bool Focusable => true;
        internal override bool ActivateOnEnter => true;

        protected override string? HoverSoundCue => Style.HoverSound ?? Theme.HoverSound;

        private string OpenSoundCue => Style.ClickSound ?? Theme.DropdownOpenSound;

        /// <summary>
        /// Select <paramref name="newIndex"/> on behalf of the user: write it through the setter (or own state) and raise
        /// <see cref="OnValueChanged"/> when the selection actually changed.
        /// </summary>
        private void Commit(int newIndex)
        {
            if (newIndex < 0 || newIndex >= choices.Length)
            {
                return;
            }

            int oldIndex = SelectedIndex;
            if (oldIndex == newIndex)
            {
                return;
            }

            string oldChoice = GetChoice(oldIndex);
            string newChoice = choices[newIndex];
            ownIndex = newIndex;
            if (setter != null)
            {
                Raise("set", () => setter(newChoice));
            }

            if (OnValueChanged != null)
            {
                Action<IUIValueEvent> cb = OnValueChanged;
                UIValueEvent e = UIValueEvent.Index(this, oldIndex, newIndex, oldChoice, newChoice);
                Raise("OnValueChanged", () => cb(e));
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Open / close
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Open the list (no-op while detached, disabled, hidden, already open or without choices).</summary>
        public void Open()
        {
            if (IsOpen || OwnerMenu == null || !Enabled || !Visible)
            {
                return;
            }

            RefreshChoices();
            if (choices.Length == 0)
            {
                return;
            }

            UIServices.PlaySound(OpenSoundCue);
            Focus();
            OwnerMenu.Overlay.OpenPopup(this);
            IsOpen = true;

            int selected = SelectedIndex;
            highlightIndex = selected;
            ActivePosition = selected;
        }

        /// <summary>Close the list (plays the close sound when it was open).</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            UIServices.PlaySound(Theme.DropdownCloseSound);
            ClosePopup();
            OwnerMenu?.Overlay.RemovePopup(this);
        }

        /// <summary>Commit the highlighted row (if any) and close.</summary>
        private void CommitHighlightAndClose()
        {
            Commit(highlightIndex);
            Close();
        }

        protected internal override void ClosePopup()
        {
            IsOpen = false;
            highlightIndex = -1;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Layout / draw
        // ---------------------------------------------------------------------------------------------------------

        protected override Vector2 MeasureCore(Vector2 available) => new(DefaultWidth, DropdownRowHeight);

        /// <summary>Width of the text box part (the arrow button takes the rest).</summary>
        private int BoxWidth => Math.Max(0, Bounds.Width - ButtonWidth);

        /// <summary>Absolute bounds of the open list, clamped so it never leaves the screen.</summary>
        private Rectangle ListBounds
        {
            get
            {
                int rows = Math.Min(maxVisible, choices.Length);
                int height = rows * DropdownRowHeight;
                int y = Math.Min(Bounds.Y, UIServices.ViewportSize().Y - height);
                return new Rectangle(Bounds.X, Math.Max(0, y), BoxWidth, height);
            }
        }

        protected internal override Rectangle PopupBounds => IsOpen ? ListBounds : Rectangle.Empty;

        protected override void DrawCore(SpriteBatch b)
        {
            ResolvedStyle style = Style;
            bool highlighted = Enabled && (IsHovered || IsFocused || IsOpen);
            Color tint = Theme.StateTint(Enabled, highlighted, style.HoverColor);
            Color textColor = Enabled ? style.TextColor : style.TextColor * 0.5f;

            DrawHelper.Box(b, Game1.mouseCursors, Theme.DropdownBoxSource, new Rectangle(Bounds.X, Bounds.Y, BoxWidth, Bounds.Height), tint, SpriteScale);
            DrawHelper.Text(b, Pseudo.Transform(GetLabel(SelectedIndex)), style.Font, new Vector2(Bounds.X + TextPadX, Bounds.Y + TextPadY), textColor, style.TextShadow, 1f);
            b.Draw(Game1.mouseCursors, new Vector2(Bounds.X + Bounds.Width - ButtonWidth, Bounds.Y), Theme.DropdownButtonSource, tint, 0f, Vector2.Zero, SpriteScale, SpriteEffects.None, 0f);
        }

        protected internal override void DrawPopup(SpriteBatch b)
        {
            if (!IsOpen || choices.Length == 0)
            {
                return;
            }

            ResolvedStyle style = Style;
            Rectangle list = ListBounds;
            DrawHelper.Box(b, Game1.mouseCursors, Theme.DropdownBoxSource, list, Color.White, SpriteScale);

            var interior = new Rectangle(list.X + HighlightInset, list.Y + HighlightInset, list.Width - (2 * HighlightInset), list.Height - (2 * HighlightInset));
            int start = ActivePosition;
            int end = Math.Min(choices.Length, start + maxVisible);
            int highlight = highlightIndex >= 0 ? highlightIndex : SelectedIndex;
            for (int i = start; i < end; i++)
            {
                int rowY = list.Y + ((i - start) * DropdownRowHeight);
                if (i == highlight)
                {
                    var row = new Rectangle(list.X + HighlightInset, rowY, list.Width - (2 * HighlightInset), DropdownRowHeight);
                    DrawHelper.Fill(b, Rectangle.Intersect(row, interior), Color.Wheat);
                }
                DrawHelper.Text(b, Pseudo.Transform(GetLabel(i)), style.Font, new Vector2(list.X + TextPadX, rowY + TextPadY), style.TextColor, style.TextShadow, 1f);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Input
        // ---------------------------------------------------------------------------------------------------------

        protected internal override bool HandleClick(UIClickEvent e)
        {
            if (e.Target == this && e.Button == UIMouseButton.Left && Enabled)
            {
                Open();
                base.HandleClick(e);
                return true;
            }
            return base.HandleClick(e);
        }

        protected internal override bool HandleActivate()
        {
            if (!Enabled || !Visible)
            {
                return false;
            }

            if (IsOpen)
            {
                CommitHighlightAndClose();
            }
            else
            {
                Open();
            }

            return true;
        }

        protected internal override bool HandleKey(UIKeyEvent e)
        {
            bool handled = Enabled && choices.Length > 0 && HandleNavigationKey(e.Key);
            base.HandleKey(e);
            return handled || e.Handled;
        }

        /// <summary>Up / Down move the selection (closed) or the highlight (open); Enter / Space commit the highlight while open.</summary>
        private bool HandleNavigationKey(Keys key)
        {
            switch (key)
            {
                case Keys.Up:
                case Keys.Down:
                    int delta = key == Keys.Up ? -1 : 1;
                    if (IsOpen)
                    {
                        MoveHighlight(delta);
                    }
                    else
                    {
                        Commit(Math.Clamp(SelectedIndex + delta, 0, choices.Length - 1));
                    }

                    return true;

                case Keys.Enter:
                case Keys.Space:
                    if (!IsOpen)
                    {
                        return false;
                    }

                    CommitHighlightAndClose();
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>Move the open list's highlight by <paramref name="delta"/> rows, scrolling so it stays visible.</summary>
        private void MoveHighlight(int delta)
        {
            int current = highlightIndex >= 0 ? highlightIndex : SelectedIndex;
            highlightIndex = Math.Clamp(current + delta, 0, choices.Length - 1);
            if (highlightIndex < ActivePosition)
            {
                ActivePosition = highlightIndex;
            }
            else if (highlightIndex >= ActivePosition + maxVisible)
            {
                ActivePosition = highlightIndex - maxVisible + 1;
            }
            else
            {
                // highlight already visible: keep the scroll position
            }
        }

        /// <summary>Choice index of the list row under the point, or -1.</summary>
        private int RowAt(int px, int py)
        {
            Rectangle list = ListBounds;
            if (!list.Contains(px, py))
            {
                return -1;
            }

            int index = ActivePosition + ((py - list.Y) / DropdownRowHeight);
            return index >= 0 && index < choices.Length ? index : -1;
        }

        protected internal override void HandlePopupClick(UIClickEvent e)
        {
            if (e.Button == UIMouseButton.Left)
            {
                Commit(RowAt(e.X, e.Y));
            }

            Close();
        }

        protected internal override void HandlePopupHover(int px, int py)
        {
            int row = RowAt(px, py);
            if (row >= 0)
            {
                highlightIndex = row;
            }
        }

        protected internal override bool HandlePopupScroll(int direction)
        {
            if (!IsOpen)
            {
                return false;
            }

            ActivePosition -= Math.Sign(direction);
            if (OnScroll != null)
            {
                Action<int> cb = OnScroll;
                Raise("OnScroll", () => cb(direction));
            }
            return true;
        }

        protected internal override void HandleFocusLost()
        {
            Close();
            base.HandleFocusLost();
        }
    }
}
