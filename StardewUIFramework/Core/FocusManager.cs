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
            public bool Selected { get; set; }

            public KeyboardAdapter(FocusManager owner)
            {
                this.owner = owner;
            }

            public void RecieveTextInput(char inputChar) => owner.Focused?.HandleTextInput(inputChar);
            public void RecieveTextInput(string text) => owner.Focused?.HandleTextInput(text);
            public void RecieveCommandInput(char command) => owner.Focused?.HandleCommandInput(command);
            public void RecieveSpecialInput(Keys key) => owner.Focused?.HandleSpecialInput(key);
        }

        private readonly UIMenu menu;
        private readonly KeyboardAdapter adapter;
        private IKeyboardSubscriber? previousSubscriber;
        private bool subscribed;

        /// <summary>Hook for tests: replaces the <see cref="Game1.keyboardDispatcher"/> access.</summary>
        internal static Func<IKeyboardSubscriber?>? GetSubscriber { get; set; }
        internal static Action<IKeyboardSubscriber?>? SetSubscriber { get; set; }

        public UIElement? Focused { get; private set; }

        public FocusManager(UIMenu menu)
        {
            this.menu = menu;
            adapter = new KeyboardAdapter(this);
        }

        public void SetFocus(UIElement? element)
        {
            if (element != null && (!element.Focusable || !element.Visible || !element.Enabled || element.OwnerMenu != menu))
                return;
            if (Focused == element)
                return;

            UIElement? old = Focused;
            Focused = element;
            old?.HandleFocusLost();
            UpdateSubscription();
            element?.HandleFocusGained();
        }

        public void ClearFocus() => SetFocus(null);

        /// <summary>Re-evaluate whether the game's keyboard subscriber should be ours (call after focus changes or on open/close).</summary>
        public void UpdateSubscription()
        {
            bool wants = Focused != null && Focused.WantsTextInput && menu.IsOpen;
            if (wants && !subscribed)
            {
                previousSubscriber = ReadSubscriber();
                if (previousSubscriber == adapter)
                    previousSubscriber = null;
                WriteSubscriber(adapter);
                subscribed = true;
            }
            else if (!wants && subscribed)
            {
                Release();
            }
        }

        /// <summary>Give the keyboard back (menu closing).</summary>
        public void Release()
        {
            if (!subscribed)
                return;
            if (ReadSubscriber() == adapter)
                WriteSubscriber(previousSubscriber);
            previousSubscriber = null;
            subscribed = false;
        }

        /// <summary>Drop focus if the focused element left the tree or became unusable.</summary>
        public void Validate()
        {
            if (Focused != null && (Focused.OwnerMenu != menu || !Focused.Visible || !Focused.Enabled))
                ClearFocus();
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Traversal
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Focusable elements in layout (tree) order.</summary>
        public List<UIElement> FocusableElements()
        {
            var list = new List<UIElement>();
            foreach (UIElement e in menu.Root.SelfAndDescendants())
            {
                if (e.Focusable && IsUsable(e))
                    list.Add(e);
            }
            return list;
        }

        private static bool IsUsable(UIElement e)
        {
            for (UIElement? cur = e; cur != null; cur = cur.ParentElement)
            {
                if (!cur.Visible || !cur.Enabled)
                    return false;
            }
            return true;
        }

        /// <summary>Tab / Shift+Tab.</summary>
        public bool MoveNext(bool backwards)
        {
            List<UIElement> all = FocusableElements();
            if (all.Count == 0)
                return false;
            int index = Focused == null ? -1 : all.IndexOf(Focused);
            int next = backwards
                ? (index <= 0 ? all.Count - 1 : index - 1)
                : (index < 0 || index >= all.Count - 1 ? 0 : index + 1);
            SetFocus(all[next]);
            return true;
        }

        /// <summary>Arrow keys / d-pad: nearest focusable element in the given direction (dx, dy ∈ {-1,0,1}).</summary>
        public bool MoveDirection(int dx, int dy)
        {
            List<UIElement> all = FocusableElements();
            if (all.Count == 0)
                return false;
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
                    continue;
                Vector2 delta = candidate.Bounds.Center.ToVector2() - origin;
                float along = delta.X * dx + delta.Y * dy;
                if (along <= 0)
                    continue;
                float across = Math.Abs(dx != 0 ? delta.Y : delta.X);
                float score = along + across * 2.5f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            if (best == null)
                return false;
            SetFocus(best);
            return true;
        }

        private static IKeyboardSubscriber? ReadSubscriber()
        {
            if (GetSubscriber != null)
                return GetSubscriber();
            return Game1.keyboardDispatcher?.Subscriber;
        }

        private static void WriteSubscriber(IKeyboardSubscriber? subscriber)
        {
            if (SetSubscriber != null)
            {
                SetSubscriber(subscriber);
                return;
            }
            if (Game1.keyboardDispatcher != null)
                Game1.keyboardDispatcher.Subscriber = subscriber;
        }
    }
}
