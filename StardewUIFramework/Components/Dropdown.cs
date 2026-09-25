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
    /// A vanilla-looking dropdown (<c>OptionsDropDown</c> sprites): a closed box showing the selected label plus an arrow
    /// button; clicking it opens a scrollable list of <see cref="MaxVisible"/> rows as an overlay popup that gets first
    /// pick at input and is closed (with the click swallowed) when the user clicks anywhere else.
    /// <para>
    /// The selection is bound through the <c>get</c> / <c>set</c> delegates when given (the getter is read every frame so
    /// external changes show immediately); otherwise the element holds its own selected index. Keyboard: Up / Down change
    /// the selection while closed and move the highlight while open, Enter / Space open or commit, Escape closes
    /// (handled by the router).
    /// </para>
    /// <para>
    /// When there are more choices than <see cref="MaxVisible"/>, the open list shows a compact scroll indicator on its
    /// right edge (the vanilla scrollbar sprites at half size): up / down arrows, dimmed at either end, and a thumb sized
    /// to the visible share of the list. Clicking an arrow scrolls one row, clicking the track jumps there.
    /// </para>
    /// </summary>
    internal sealed class Dropdown : UIElement, IUIDropdown
    {
        /// <summary>Height of the closed box and of every row in the open list at text scale 1.</summary>
        internal const int DropdownRowHeight = 44;

        /// <summary>Preferred (natural) width; the dropdown narrows below it when its slot is smaller, down to <see cref="MinWidthCore"/>.</summary>
        private const int DefaultWidth = 300;

        /// <summary>
        /// Smallest text box width (inside the padding) the minimum width allows: room for the box's 3-slice corners and
        /// a couple of characters plus "..." at the smallest fit scale, whatever the options are.
        /// </summary>
        private const int MinTextWidth = 40;

        private const int ButtonWidth = 48;
        private const int TextPadX = 4;
        private const int TextPadY = 8;
        private const int HighlightInset = 4;
        private const float SpriteScale = 4f;

        // compact scroll indicator: the vanilla scrollbar sprites (arrows 11x12, track / thumb 6 wide) at half size
        private const float IndicatorScale = 2f;
        private const int IndicatorWidth = 22;
        private const int IndicatorArrowHeight = 24;
        private const int IndicatorTrackWidth = 12;
        private const int IndicatorGap = 2;
        private const int IndicatorMinThumb = 12;

        /// <summary>Opacity of an end arrow when the list cannot scroll further that way.</summary>
        private const float DisabledArrowAlpha = 0.35f;

        private readonly Func<string[]>? choicesFunc;
        private readonly Func<string[]>? labelsFunc;
        private Func<string>? getter;
        private Action<string>? setter;
        private string[] choices = Array.Empty<string>();
        private string[] labels = Array.Empty<string>();
        private int ownIndex = -1;
        private int maxVisible = 5;
        private int highlightIndex = -1;
        private int activePosition;
        private bool shrink;

        /// <summary>Label-driven width of the open list, measured when it opens (see <see cref="NaturalListWidth"/>).</summary>
        private int openListWidth;

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

        /// <summary>The value delegates the element currently reads / writes through (null = own value).</summary>
        internal Func<string>? BoundGetter => getter;

        internal Action<string>? BoundSetter => setter;

        /// <summary>Swap the value delegates after construction (signal bindings); the own value is kept as the fallback.</summary>
        internal void Rebind(Func<string>? newGetter, Action<string>? newSetter)
        {
            getter = newGetter;
            setter = newSetter;
        }

        public string SelectedValue
        {
            get => GetChoice(SelectedIndex);
            set => SelectedIndex = Array.IndexOf(choices, value);
        }

        /// <summary>Let the minimum width drop to the fitted form of the widest option (<see cref="DrawHelper.FitTextMinWidth"/>).</summary>
        public bool Shrink
        {
            get => shrink;
            set
            {
                if (shrink == value)
                {
                    return;
                }

                shrink = value;
                InvalidateLayout();
            }
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

        // done with the mouse (pick / drag): a click does not leave it holding focus (Tab / arrows / gamepad still reach it)
        internal override bool FocusOnClick => false;
        internal override bool ActivateOnEnter => true;

        internal override string AccessibleDescription => Accessibility.Compose(
            Accessibility.Text("dropdown", "Dropdown"),
            GetLabel(SelectedIndex),
            Enabled ? null : Accessibility.Text("disabled", "disabled"));

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
            Accessibility.AnnounceValue(this);
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

            openListWidth = NaturalListWidth();
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

        // DefaultWidth is a preference: a narrower slot gets a narrower dropdown, and Stretch (handled by Arrange) fills it
        protected override Vector2 MeasureCore(Vector2 available) => new(Math.Max(0, Math.Min(available.X, DefaultWidth)), RowHeight);

        // the arrow button plus a text box wide enough for every option whole, capped at DefaultWidth (the natural width
        // MeasureCore reports when there is room, so the minimum never exceeds it: options too long for that are fitted
        // even then). With Shrink, the text box only needs the narrowest fitted form (FitText) of the widest-reaching
        // option. Either way never below MinTextWidth, so the 3-slice box keeps its corners and some room for "..."
        protected override float MinWidthCore()
        {
            UIFont font = Style.Font;
            float text = MinTextWidth;
            for (int i = 0; i < choices.Length; i++)
            {
                string label = Pseudo.Transform(GetLabel(i));
                text = Math.Max(text, shrink ? DrawHelper.FitTextMinWidth(label, font, 1f) : (float)Math.Ceiling(UIServices.Text.Measure(font, label, 1f).X));
            }

            float width = ButtonWidth + (2 * TextPadX) + text;
            return shrink ? width : Math.Min(width, DefaultWidth);
        }

        /// <summary>Row height including the extra room scaled text needs.</summary>
        private int RowHeight => DropdownRowHeight + Theme.ExtraTextHeight(Style.Font);

        /// <summary>Width of the text box part (the arrow button takes the rest).</summary>
        private int BoxWidth => Math.Max(0, Bounds.Width - ButtonWidth);

        /// <summary>
        /// Width the open list needs to show every label unshrunk (text + padding + the scroll indicator's strip when
        /// it overflows), capped at the text box width of a <see cref="DefaultWidth"/> dropdown so an unsqueezed dropdown
        /// opens exactly as before. Measured once per <see cref="Open"/>, so a squeezed dropdown still opens a readable list.
        /// </summary>
        private int NaturalListWidth()
        {
            UIFont font = Style.Font;
            float widest = 0;
            for (int i = 0; i < choices.Length; i++)
            {
                widest = Math.Max(widest, UIServices.Text.Measure(font, Pseudo.Transform(GetLabel(i)), 1f).X);
            }

            int reserved = HasOverflow ? IndicatorWidth + IndicatorGap : 0;
            int natural = (int)Math.Ceiling(widest) + (2 * TextPadX) + reserved;
            return Math.Min(natural, DefaultWidth - ButtonWidth);
        }

        /// <summary>
        /// Absolute bounds of the open list: as wide as the text box, or wider when a squeezed box is narrower than the
        /// labels need (the popup is an overlay, so it can outgrow the control), clamped so it never leaves the screen.
        /// </summary>
        private Rectangle ListBounds
        {
            get
            {
                Point viewport = UIServices.ViewportSize();
                int rows = Math.Min(maxVisible, choices.Length);
                int height = rows * RowHeight;
                int width = Math.Min(Math.Max(BoxWidth, openListWidth), viewport.X);
                int x = Math.Min(Bounds.X, viewport.X - width);
                int y = Math.Min(Bounds.Y, viewport.Y - height);
                return new Rectangle(Math.Max(0, x), Math.Max(0, y), width, height);
            }
        }

        protected internal override Rectangle PopupBounds => IsOpen ? ListBounds : Rectangle.Empty;

        protected override void DrawCore(SpriteBatch b)
        {
            ResolvedStyle style = Style;
            bool highlighted = Enabled && (IsHovered || IsFocused || IsOpen);
            Color tint = Theme.StateTint(Enabled, highlighted, style.HoverColor);
            Color textColor = Enabled ? style.TextColor : style.DisabledTextColor;

            DrawHelper.ThemedBox(b, Game1.mouseCursors, Theme.DropdownBoxSource, new Rectangle(Bounds.X, Bounds.Y, BoxWidth, Bounds.Height), tint, SpriteScale);
            var textRect = new Rectangle(Bounds.X + TextPadX, Bounds.Y + TextPadY, Math.Max(0, BoxWidth - (2 * TextPadX)), Bounds.Height);
            DrawHelper.FitText(b, Pseudo.Transform(GetLabel(SelectedIndex)), style.Font, textRect, textColor, style.TextShadow, 1f, UIAlign.Start);
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
            DrawHelper.ThemedBox(b, Game1.mouseCursors, Theme.DropdownBoxSource, list, Color.White, SpriteScale);

            var interior = new Rectangle(list.X + HighlightInset, list.Y + HighlightInset, list.Width - (2 * HighlightInset), list.Height - (2 * HighlightInset));
            int start = ActivePosition;
            int end = Math.Min(choices.Length, start + maxVisible);
            int highlight = highlightIndex >= 0 ? highlightIndex : SelectedIndex;
            int rowHeight = RowHeight;
            // rows give up the indicator's strip when the list overflows
            int reserved = HasOverflow ? IndicatorWidth + IndicatorGap : 0;
            for (int i = start; i < end; i++)
            {
                int rowY = list.Y + ((i - start) * rowHeight);
                if (i == highlight)
                {
                    var row = new Rectangle(list.X + HighlightInset, rowY, list.Width - (2 * HighlightInset) - reserved, rowHeight);
                    DrawHelper.Fill(b, Rectangle.Intersect(row, interior), style.HoverColor);
                }
                var rowText = new Rectangle(list.X + TextPadX, rowY + TextPadY, Math.Max(0, list.Width - (2 * TextPadX) - reserved), rowHeight);
                DrawHelper.FitText(b, Pseudo.Transform(GetLabel(i)), style.Font, rowText, style.TextColor, style.TextShadow, 1f, UIAlign.Start);
            }

            if (HasOverflow)
            {
                DrawIndicator(b, list);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Scroll indicator
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Whether some choices do not fit in the open list (the scroll indicator is shown).</summary>
        private bool HasOverflow => choices.Length > maxVisible;

        /// <summary>The indicator's strip along the right edge of <paramref name="list"/>, or empty when nothing overflows.</summary>
        private Rectangle IndicatorBounds(Rectangle list)
        {
            if (!HasOverflow)
            {
                return Rectangle.Empty;
            }

            return new Rectangle(list.Right - HighlightInset - IndicatorWidth, list.Y + HighlightInset, IndicatorWidth, Math.Max(0, list.Height - (2 * HighlightInset)));
        }

        /// <summary>Whether the strip is tall enough for both arrows plus some track; otherwise only the track is drawn.</summary>
        private static bool HasArrows(Rectangle strip) => strip.Height >= (2 * IndicatorArrowHeight) + IndicatorMinThumb;

        private static Rectangle UpArrowBounds(Rectangle strip) => new(strip.X, strip.Y, IndicatorWidth, IndicatorArrowHeight);

        private static Rectangle DownArrowBounds(Rectangle strip) => new(strip.X, strip.Bottom - IndicatorArrowHeight, IndicatorWidth, IndicatorArrowHeight);

        /// <summary>The track between the arrows (the whole strip when there is no room for them).</summary>
        private static Rectangle TrackBounds(Rectangle strip)
        {
            int x = strip.X + ((IndicatorWidth - IndicatorTrackWidth) / 2);
            if (!HasArrows(strip))
            {
                return new Rectangle(x, strip.Y, IndicatorTrackWidth, strip.Height);
            }

            int top = strip.Y + IndicatorArrowHeight + IndicatorGap;
            int bottom = strip.Bottom - IndicatorArrowHeight - IndicatorGap;
            return new Rectangle(x, top, IndicatorTrackWidth, Math.Max(0, bottom - top));
        }

        /// <summary>The thumb: as tall as the visible share of the list, placed by the scroll position.</summary>
        private Rectangle ThumbBounds(Rectangle track)
        {
            int height = Math.Clamp((int)Math.Round(track.Height * (maxVisible / (float)choices.Length)), Math.Min(IndicatorMinThumb, track.Height), track.Height);
            int range = track.Height - height;
            int max = MaxPosition;
            int y = track.Y + (max > 0 ? (int)Math.Round(range * (ActivePosition / (float)max)) : 0);
            return new Rectangle(track.X, y, track.Width, height);
        }

        private void DrawIndicator(SpriteBatch b, Rectangle list)
        {
            Rectangle strip = IndicatorBounds(list);
            Texture2D texture = Game1.mouseCursors;
            Color tint = Theme.ScrollbarTint;
            Rectangle track = TrackBounds(strip);

            if (HasArrows(strip))
            {
                Rectangle up = UpArrowBounds(strip);
                Rectangle down = DownArrowBounds(strip);
                Color upTint = ActivePosition > 0 ? tint : tint * DisabledArrowAlpha;
                Color downTint = ActivePosition < MaxPosition ? tint : tint * DisabledArrowAlpha;
                b.Draw(texture, new Vector2(up.X, up.Y), Theme.ScrollUpArrow, upTint, 0f, Vector2.Zero, IndicatorScale, SpriteEffects.None, 0f);
                b.Draw(texture, new Vector2(down.X, down.Y), Theme.ScrollDownArrow, downTint, 0f, Vector2.Zero, IndicatorScale, SpriteEffects.None, 0f);
            }

            if (track.Height <= 0)
            {
                return;
            }

            IClickableMenu.drawTextureBox(b, texture, Theme.ScrollTrack, track.X, track.Y, track.Width, track.Height, tint, IndicatorScale, false);
            Rectangle thumb = ThumbBounds(track);
            IClickableMenu.drawTextureBox(b, texture, Theme.ScrollThumb, thumb.X, thumb.Y, thumb.Width, thumb.Height, tint, IndicatorScale, false);
        }

        /// <summary>
        /// Clicks on the indicator: an arrow scrolls one row, the track moves the thumb's center to the click. Returns
        /// false when the point is not on the indicator.
        /// </summary>
        private bool HandleIndicatorClick(int px, int py)
        {
            Rectangle strip = IndicatorBounds(ListBounds);
            if (!strip.Contains(px, py))
            {
                return false;
            }

            int before = ActivePosition;
            Rectangle track = TrackBounds(strip);
            if (HasArrows(strip) && UpArrowBounds(strip).Contains(px, py))
            {
                ActivePosition--;
            }
            else if (HasArrows(strip) && DownArrowBounds(strip).Contains(px, py))
            {
                ActivePosition++;
            }
            else if (track.Height > 0)
            {
                Rectangle thumb = ThumbBounds(track);
                int range = track.Height - thumb.Height;
                float fraction = range > 0 ? Math.Clamp((py - track.Y - (thumb.Height / 2f)) / range, 0f, 1f) : 0f;
                ActivePosition = (int)Math.Round(fraction * MaxPosition);
            }
            else
            {
                // degenerate strip without a track: nothing to scroll
            }

            if (ActivePosition != before)
            {
                UIServices.PlaySound(Theme.ScrollSound);
            }

            return true;
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
            if (!list.Contains(px, py) || IndicatorBounds(list).Contains(px, py))
            {
                return -1;
            }

            int index = ActivePosition + ((py - list.Y) / RowHeight);
            return index >= 0 && index < choices.Length ? index : -1;
        }

        protected internal override void HandlePopupClick(UIClickEvent e)
        {
            if (e.Button == UIMouseButton.Left && HandleIndicatorClick(e.X, e.Y))
            {
                // scrolling through the indicator keeps the list open
                return;
            }

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
