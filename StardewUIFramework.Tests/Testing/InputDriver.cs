using Microsoft.Xna.Framework.Input;
using UIFramework.Api;
using UIFramework.Core;

namespace StardewUIFramework.Tests.Testing
{
    /// <summary>
    /// Scripts input against a menu the way <c>MenuHost</c> feeds it from the game, without an <c>IClickableMenu</c>:
    /// a layout pass runs before every action (so bounds are valid), the action goes through the menu's
    /// <see cref="EventRouter"/>, and any layout the action dirtied is re-run afterwards. Keyboard input reproduces
    /// both game paths: <c>receiveKeyPress</c> (the router) and the keyboard dispatcher's text / command / special
    /// input to the focused element.
    /// </summary>
    public sealed class InputDriver
    {
        private readonly TestHost host;
        private readonly UIMenu menu;

        internal InputDriver(TestHost host, UIMenu menu)
        {
            this.host = host;
            this.menu = menu;
        }

        /// <summary>The element that owns keyboard focus, or null.</summary>
        public IUIElement? Focused => menu.Focus.Focused;

        /// <summary>The element under the cursor after the last hover, or null.</summary>
        public IUIElement? Hovered => menu.Hovered;

        /// <summary>Move the cursor (raises hover enter / leave).</summary>
        public void Hover(int x, int y)
        {
            EnsureLayout();
            menu.Router.Hover(x, y);
            Settle();
        }

        /// <summary>Left click: hover, press, release. Returns true if an element (or popup) handled it.</summary>
        public bool Click(int x, int y) => Click(x, y, UIMouseButton.Left);

        public bool RightClick(int x, int y) => Click(x, y, UIMouseButton.Right);

        private bool Click(int x, int y, UIMouseButton button)
        {
            EnsureLayout();
            menu.Router.Hover(x, y);
            bool handled = menu.Router.Click(x, y, button);
            if (button == UIMouseButton.Left)
            {
                menu.Router.ClickReleased(x, y);
            }

            Settle();
            return handled;
        }

        /// <summary>Press at (x1, y1), drag through (x2, y2) and release there.</summary>
        public void Drag(int x1, int y1, int x2, int y2)
        {
            EnsureLayout();
            menu.Router.Hover(x1, y1);
            menu.Router.Click(x1, y1, UIMouseButton.Left);
            menu.Router.ClickHeld(x2, y2);
            menu.Router.ClickReleased(x2, y2);
            Settle();
        }

        /// <summary>Scroll wheel over the current cursor position (positive = up). Returns true if handled.</summary>
        public bool Scroll(int direction)
        {
            EnsureLayout();
            bool handled = menu.Router.Scroll(direction);
            Settle();
            return handled;
        }

        /// <summary>
        /// A key press: routed like <c>receiveKeyPress</c> and, when the focused element takes text input, also
        /// delivered the way the game's keyboard dispatcher would (Back → backspace command, Enter / Tab → command
        /// characters, arrows and editing keys → special input). Returns true if the router consumed it.
        /// </summary>
        public bool Key(Keys key, bool shift = false, bool ctrl = false)
        {
            EnsureLayout();
            bool handled = menu.Router.KeyPress(key, shift, ctrl, false);
            UIElement? focused = menu.Focus.Focused;
            if (focused != null && focused.WantsTextInput)
            {
                DispatchToSubscriber(focused, key);
            }

            Settle();
            return handled;
        }

        private static void DispatchToSubscriber(UIElement focused, Keys key)
        {
            switch (key)
            {
                case Keys.Back:
                    focused.HandleCommandInput('\b');
                    break;
                case Keys.Enter:
                    focused.HandleCommandInput('\r');
                    break;
                case Keys.Tab:
                    focused.HandleCommandInput('\t');
                    break;
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                case Keys.Home:
                case Keys.End:
                case Keys.Delete:
                    focused.HandleSpecialInput(key);
                    break;
                default:
                    break;
            }
        }

        /// <summary>Type characters into the focused element one at a time ('\b' is a backspace). Ignored without focus.</summary>
        public void Type(string text)
        {
            EnsureLayout();
            foreach (char c in text)
            {
                UIElement? focused = menu.Focus.Focused;
                if (focused == null)
                {
                    break;
                }

                if (c == '\b')
                {
                    focused.HandleCommandInput('\b');
                }
                else
                {
                    focused.HandleTextInput(c);
                }
            }
            Settle();
        }

        /// <summary>Paste a string into the focused element as one edit.</summary>
        public void Paste(string text)
        {
            EnsureLayout();
            menu.Focus.Focused?.HandleTextInput(text);
            Settle();
        }

        /// <summary>One game tick: advance the clock and run the menu's update (layout if dirty, focus validation, element updates, OnUpdate).</summary>
        public void Tick(double elapsedMs)
        {
            host.Advance(elapsedMs);
            menu.Tick(elapsedMs);
        }

        /// <summary>Run a layout pass if anything is dirty (before an action so bounds are valid, after it so the tree reflects it).</summary>
        private void EnsureLayout() => Settle();

        private void Settle()
        {
            if (menu.LayoutDirty || menu.Root.LayoutDirty)
            {
                menu.Relayout();
            }
        }
    }
}
