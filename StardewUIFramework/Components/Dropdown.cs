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
    internal sealed partial class Dropdown : UIElement, IUIDropdown
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

        /// <summary>
        /// The list was opened by a click: it takes focus while open (so the arrows move the highlight) and gives it back
        /// when it closes, as a click-once control must (<see cref="FocusOnClick"/>).
        /// </summary>
        private bool openedByMouse;

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

        /// <summary>
        /// While closed: re-read the choices when the delegate hands back a different array than the one shown, so a
        /// changing choice list never shows a stale label or lets Up / Down commit a choice that is gone. The layout is
        /// invalidated only when the choices or labels actually differ (the minimum width depends on them).
        /// </summary>
        private void SyncChoices()
        {
            if (IsOpen || choicesFunc == null)
            {
                return;
            }

            string[]? current = Raise("choices", choicesFunc, choices);
            if (ReferenceEquals(current, choices))
            {
                return;
            }

            string[] oldChoices = choices;
            string[] oldLabels = labels;
            RefreshChoices();
            if (!choices.AsSpan().SequenceEqual(oldChoices) || !labels.AsSpan().SequenceEqual(oldLabels))
            {
                InvalidateLayout();
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
        public void Open() => Open(byMouse: false);

        private void Open(bool byMouse)
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
            openedByMouse = byMouse;

            int selected = SelectedIndex;
            highlightIndex = selected;
            ActivePosition = selected;
            if (Game1.options.SnappyMenus)
            {
                SnapCursorToHighlight(); // gamepad: A picks the row under the cursor, so start on the selected one
            }
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
            if (Game1.options.SnappyMenus)
            {
                OwnerMenu?.Host?.snapCursorToCurrentSnappedComponent(); // gamepad: back onto the dropdown box
            }

            if (openedByMouse)
            {
                openedByMouse = false;
                if (IsFocused)
                {
                    OwnerMenu?.Focus.ClearFocus();
                }
            }
        }
    }
}
