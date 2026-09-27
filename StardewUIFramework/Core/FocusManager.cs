using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace UIFramework.Core
{
    /// <summary>
    /// Single owner of keyboard focus for one menu. Installs exactly one <see cref="IKeyboardSubscriber"/> on
    /// <see cref="Game1.keyboardDispatcher"/> (only while the focused element wants text input) and restores the
    /// previous subscriber when focus is released. A gamepad player gets the game's on-screen keyboard for it, as with
    /// vanilla text boxes.
    /// </summary>
    internal sealed class FocusManager
    {
        /// <summary>
        /// The keyboard subscriber: a vanilla <see cref="TextBox"/> that forwards what it receives to the focused element.
        /// Being the game's own class, it is what the game's on-screen keyboard (<see cref="TextEntryMenu"/>) types into and
        /// draws, and selecting / deselecting it opens and closes the Steam keyboard like any vanilla text box.
        /// </summary>
        private sealed class SubscriberTextBox : TextBox, IKeyboardSubscriber
        {
            private readonly FocusManager owner;

            internal SubscriberTextBox(FocusManager owner)
                : base(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Game1.textColor)
            {
                this.owner = owner;
                limitWidth = false; // the element enforces its own limits
            }

            public override void RecieveTextInput(char inputChar) => owner.Focused?.HandleTextInput(inputChar);

            public override void RecieveTextInput(string text) => owner.Focused?.HandleTextInput(text);

            public override void RecieveCommandInput(char command) => owner.Focused?.HandleCommandInput(command);

            // RecieveSpecialInput is not virtual in TextBox: re-implement the interface member instead
            void IKeyboardSubscriber.RecieveSpecialInput(Keys key) => owner.Focused?.HandleSpecialInput(key);

            /// <summary>Drawn by the on-screen keyboard: show the focused element's current text.</summary>
            public override void Draw(SpriteBatch spriteBatch, bool drawShadow = true)
            {
                Text = owner.Focused?.TextEntryText ?? string.Empty;
                base.Draw(spriteBatch, drawShadow);
            }
        }

        private readonly UIMenu menu;
        private SubscriberTextBox? textBox;
        private IKeyboardSubscriber? previousSubscriber;
        private bool subscribed;

        /// <summary>Whether the on-screen keyboard was opened for the current subscription.</summary>
        private bool onScreenKeyboardShown;

        internal UIElement? Focused { get; private set; }

        internal FocusManager(UIMenu menu)
        {
            this.menu = menu;
        }

        internal void SetFocus(UIElement? element)
        {
            if (element != null && !CanFocus(element))
            {
                return;
            }

            if (Focused == element)
            {
                return;
            }

            UIElement? old = Focused;
            Focused = element;
            old?.HandleFocusLost();
            UpdateSubscription();
            element?.HandleFocusGained();
            ScrollIntoView(element);
            Accessibility.AnnounceElement(element);
        }

        internal void ClearFocus() => SetFocus(null);

        /// <summary>Ask every enclosing scroll view (innermost first) to bring the element into view.</summary>
        internal static void ScrollIntoView(UIElement? element)
        {
            for (UIElement? cur = element?.ParentElement; cur != null; cur = cur.ParentElement)
            {
                if (cur is Components.ScrollView view)
                {
                    view.ScrollIntoView(element!.Bounds);
                }
            }
        }

        /// <summary>Re-evaluate whether the game's keyboard subscriber should be ours (call after focus changes or on open/close).</summary>
        internal void UpdateSubscription()
        {
            bool wants = Focused != null && Focused.WantsTextInput && menu.IsOpen;
            if (wants && !subscribed)
            {
                textBox ??= new SubscriberTextBox(this);
                previousSubscriber = ReadSubscriber();
                if (previousSubscriber == textBox)
                {
                    previousSubscriber = null;
                }

                textBox.SelectMe(); // becomes the game's keyboard subscriber
                subscribed = true;
                ShowOnScreenKeyboard();
            }
            else if (!wants && subscribed)
            {
                Release();
            }
            else
            {
                // subscription already matches the focused element
            }
        }

        /// <summary>
        /// Open the game's on-screen keyboard for the subscription when the player uses a gamepad, with the same condition as
        /// a vanilla <see cref="TextBox"/> (<c>TextBox.Update</c>).
        /// </summary>
        private void ShowOnScreenKeyboard()
        {
            if (textBox != null && Game1.options.gamepadControls && !Game1.lastCursorMotionWasMouse)
            {
                Game1.showTextEntry(textBox);
                onScreenKeyboardShown = true;
            }
        }

        /// <summary>
        /// Once per tick: when the on-screen keyboard was closed (OK or B), end the edit by dropping focus, so the gamepad
        /// moves between elements again.
        /// </summary>
        internal void Tick()
        {
            if (onScreenKeyboardShown && Game1.textEntry == null)
            {
                onScreenKeyboardShown = false;
                if (Focused?.WantsTextInput == true)
                {
                    ClearFocus();
                }
            }
        }

        /// <summary>Give the keyboard back (menu closing). Deselecting the text box also closes the on-screen keyboard.</summary>
        internal void Release()
        {
            if (!subscribed)
            {
                return;
            }

            bool ours = ReadSubscriber() == textBox;
            textBox!.Selected = false; // clears the game's subscriber when it is ours
            if (ours)
            {
                WriteSubscriber(previousSubscriber);
            }

            previousSubscriber = null;
            subscribed = false;
            onScreenKeyboardShown = false;
        }

        /// <summary>Drop focus if the focused element left the tree or became unusable.</summary>
        internal void Validate()
        {
            if (Focused != null && (Focused.OwnerMenu != menu || !Focused.Visible || !Focused.Enabled))
            {
                ClearFocus();
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Traversal
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Focusable elements in layout (tree) order.</summary>
        internal List<UIElement> FocusableElements()
        {
            var list = new List<UIElement>();
            foreach (UIElement e in menu.Root.SelfAndDescendants())
            {
                if (e.Focusable && IsUsable(e))
                {
                    list.Add(e);
                }
            }
            return list;
        }

        private static bool IsUsable(UIElement e)
        {
            for (UIElement? cur = e; cur != null; cur = cur.ParentElement)
            {
                if (!cur.Visible || !cur.Enabled)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Tab / Shift+Tab.</summary>
        internal bool MoveNext(bool backwards)
        {
            List<UIElement> all = FocusableElements();
            if (all.Count == 0)
            {
                return false;
            }

            int index = Focused == null ? -1 : all.IndexOf(Focused);
            SetFocus(all[NextIndex(index, all.Count, backwards)]);
            return true;
        }

        /// <summary>Whether <paramref name="element"/> can take focus in this menu right now.</summary>
        private bool CanFocus(UIElement element)
        {
            bool usable = element.Visible && element.Enabled;
            return element.Focusable && usable && element.OwnerMenu == menu;
        }

        /// <summary>The next index in a cyclic list of <paramref name="count"/> items (-1 = nothing focused yet).</summary>
        private static int NextIndex(int index, int count, bool backwards)
        {
            if (backwards)
            {
                return index <= 0 ? count - 1 : index - 1;
            }

            bool atEnd = index < 0 || index >= count - 1;
            return atEnd ? 0 : index + 1;
        }

        /// <summary>Arrow keys / d-pad: nearest focusable element in the given direction (dx, dy âˆˆ {-1,0,1}).</summary>
        internal bool MoveDirection(int dx, int dy)
        {
            List<UIElement> all = FocusableElements();
            if (all.Count == 0)
            {
                return false;
            }

            if (Focused == null)
            {
                SetFocus(all[0]);
                return true;
            }

            Rectangle from = Focused.Bounds;
            Vector2 origin = from.Center.ToVector2();
            UIElement? best = null;
            float bestScore = float.MaxValue;
            foreach (UIElement candidate in all)
            {
                if (candidate == Focused)
                {
                    continue;
                }

                Vector2 delta = candidate.Bounds.Center.ToVector2() - origin;
                float along = (delta.X * dx) + (delta.Y * dy);
                if (along <= 0)
                {
                    continue;
                }

                float across = Math.Abs(dx != 0 ? delta.Y : delta.X);
                float score = along + (across * 2.5f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            if (best == null)
            {
                return false;
            }

            SetFocus(best);
            return true;
        }

        private static IKeyboardSubscriber? ReadSubscriber()
        {
            return Game1.keyboardDispatcher?.Subscriber;
        }

        private static void WriteSubscriber(IKeyboardSubscriber? subscriber)
        {
            if (Game1.keyboardDispatcher != null)
            {
                Game1.keyboardDispatcher.Subscriber = subscriber;
            }
        }
    }
}
