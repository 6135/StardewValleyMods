using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace UIFramework.Core
{
    /// <summary>
    /// Single owner of keyboard focus for one menu. Installs exactly one <see cref="IKeyboardSubscriber"/> on
    /// <see cref="Game1.keyboardDispatcher"/> (only while the focused element wants text input) and restores the
    /// previous subscriber when focus is released.
    /// </summary>
    internal sealed class FocusManager
    {
        /// <summary>Forwards the game's text input to the focused element.</summary>
        private sealed class KeyboardAdapter : IKeyboardSubscriber
        {
            private readonly FocusManager owner;

            internal KeyboardAdapter(FocusManager owner)
            {
                this.owner = owner;
            }

            bool IKeyboardSubscriber.Selected { get; set; }

            void IKeyboardSubscriber.RecieveTextInput(char inputChar) => owner.Focused?.HandleTextInput(inputChar);
            void IKeyboardSubscriber.RecieveTextInput(string text) => owner.Focused?.HandleTextInput(text);
            void IKeyboardSubscriber.RecieveCommandInput(char command) => owner.Focused?.HandleCommandInput(command);
            void IKeyboardSubscriber.RecieveSpecialInput(Keys key) => owner.Focused?.HandleSpecialInput(key);
        }

        private readonly UIMenu menu;
        private readonly KeyboardAdapter adapter;
        private IKeyboardSubscriber? previousSubscriber;
        private bool subscribed;

        internal UIElement? Focused { get; private set; }


        internal FocusManager(UIMenu menu)
        {
            this.menu = menu;
            adapter = new KeyboardAdapter(this);
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
        private static void ScrollIntoView(UIElement? element)
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
                previousSubscriber = ReadSubscriber();
                if (previousSubscriber == adapter)
                {
                    previousSubscriber = null;
                }

                WriteSubscriber(adapter);
                subscribed = true;
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

        /// <summary>Give the keyboard back (menu closing).</summary>
        internal void Release()
        {
            if (!subscribed)
            {
                return;
            }

            if (ReadSubscriber() == adapter)
            {
                WriteSubscriber(previousSubscriber);
            }

            previousSubscriber = null;
            subscribed = false;
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
