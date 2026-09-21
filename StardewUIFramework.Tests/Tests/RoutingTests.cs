using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewUIFramework.Tests.Testing;
using StardewModdingAPI.Utilities;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Hosting;
using Xunit;

namespace StardewUIFramework.Tests.Tests
{
    /// <summary>Event routing: bubbling / Handled, focus traversal, overlay-first hit testing, hover, hotkey parsing.</summary>
    public class RoutingTests
    {
        /// <summary>A consumer component that wants the overlay pass and swallows clicks.</summary>
        private sealed class OverlayBox : IUICustomComponent
        {
            public int Clicks { get; private set; }

            public Vector2 Measure(Vector2 available) => new(40, 40);

            public void Draw(SpriteBatch b, Rectangle bounds)
            {
            }

            public void Update(Rectangle bounds, double elapsedMs)
            {
            }

            public bool OnClick(int x, int y, bool rightButton)
            {
                Clicks++;
                return true;
            }

            public void OnHover(int x, int y, bool entered)
            {
            }

            public bool OnKey(Keys key, bool shift, bool ctrl) => false;

            public bool WantsFocus => false;

            public bool WantsOverlay => true;
        }

        [Fact]
        public void ClicksBubbleToAncestorsUntilHandled()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            var order = new List<string>();
            IUIPanel panel = host.Api.AddPanel(menu.Root, "panel", true, 8);
            panel.OnClick = _ => order.Add("panel");
            IUIButton button = host.Api.AddButton(panel, "button", () => "Ok", _ => order.Add("button"));
            InputDriver input = host.Drive(menu);

            // delivered to both, but nobody marked it handled
            Assert.False(input.Click(button.Bounds.Center.X, button.Bounds.Center.Y));
            Assert.Equal(new[] { "button", "panel" }, order);

            order.Clear();
            button.OnClick = e =>
            {
                order.Add("button");
                e.Handled = true;
            };
            Assert.True(input.Click(button.Bounds.Center.X, button.Bounds.Center.Y));
            Assert.Equal(new[] { "button" }, order);

            // the panel's padding belongs to the panel itself
            order.Clear();
            input.Click(panel.Bounds.X + 2, panel.Bounds.Y + 2);
            Assert.Equal(new[] { "panel" }, order);
            Assert.Null(input.Focused);
        }

        [Fact]
        public void TabTraversalFollowsTreeOrderAndWraps()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            IUIButton b1 = host.Api.AddButton(menu.Root, "b1", () => "One", null!);
            host.Api.AddLabel(menu.Root, "label", () => "not focusable");
            IUITextInput t1 = host.Api.AddTextInput(menu.Root, "t1", null!, null!);
            IUICheckbox c1 = host.Api.AddCheckbox(menu.Root, "c1", null!, null!);
            IUIButton hidden = host.Api.AddButton(menu.Root, "hidden", () => "Hidden", null!);
            hidden.Visible = false;
            var focusLog = new List<string>();
            t1.OnFocus = e => focusLog.Add("focus:" + e.Id);
            t1.OnBlur = e => focusLog.Add("blur:" + e.Id);
            InputDriver input = host.Drive(menu);

            Assert.True(input.Key(Keys.Tab));
            Assert.Same(b1, input.Focused);
            input.Key(Keys.Tab);
            Assert.Same(t1, input.Focused);
            input.Key(Keys.Tab);
            Assert.Same(c1, input.Focused);
            input.Key(Keys.Tab);
            Assert.Same(b1, input.Focused);
            input.Key(Keys.Tab, shift: true);
            Assert.Same(c1, input.Focused);
            Assert.Equal(new[] { "focus:t1", "blur:t1" }, focusLog);

            input.Key(Keys.Escape);
            Assert.Null(input.Focused);
        }

        [Fact]
        public void ArrowKeysMoveFocusGeometrically()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            IUIStack row = host.Api.AddStack(menu.Root, "row", true, 8);
            IUIButton left = host.Api.AddButton(row, "left", () => "L", null!);
            IUIButton right = host.Api.AddButton(row, "right", () => "R", null!);
            IUIButton below = host.Api.AddButton(menu.Root, "below", () => "B", null!);
            InputDriver input = host.Drive(menu);

            left.Focus();
            input.Key(Keys.Right);
            Assert.Same(right, input.Focused);
            input.Key(Keys.Down);
            Assert.Same(below, input.Focused);
            input.Key(Keys.Up);
            Assert.Same(left, input.Focused);
        }

        [Fact]
        public void EnterActivatesTheDefaultButtonAndEscapeClosesThroughCancel()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            int ok = 0, cancel = 0;
            menu.DefaultButton = host.Api.AddButton(menu.Root, "ok", () => "Ok", _ => ok++);
            menu.CancelButton = host.Api.AddButton(menu.Root, "cancel", () => "Cancel", _ => cancel++);
            InputDriver input = host.Drive(menu);

            Assert.True(input.Key(Keys.Enter));
            Assert.Equal(1, ok);
            Assert.True(input.Key(Keys.Escape));
            Assert.Equal(1, cancel);
        }

        [Fact]
        public void HoverRaisesEnterAndLeaveAndTracksTheDeepestElement()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            var log = new List<string>();
            IUIButton button = host.Api.AddButton(menu.Root, "b", () => "Hover me", null!);
            button.OnHover = e => log.Add("enter");
            button.OnHoverEnd = e => log.Add("leave");
            InputDriver input = host.Drive(menu);

            input.Hover(button.Bounds.Center.X, button.Bounds.Center.Y);
            Assert.Same(button, input.Hovered);
            Assert.True(button.IsHovered);
            input.Hover(button.Bounds.Center.X, button.Bounds.Center.Y);
            input.Hover(0, 0);
            Assert.Null(input.Hovered);
            Assert.Equal(new[] { "enter", "leave" }, log);
        }

        [Fact]
        public void OverlayElementsAreHitBeforeTheTree()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            IUICanvas canvas = host.Api.AddCanvas(menu.Root, "canvas");
            var overlay = new OverlayBox();
            IUIElement custom = host.Api.AddCustom(canvas, "custom", overlay);
            int labelClicks = 0;
            IUILabel label = host.Api.AddLabel(canvas, "label", () => "on top in tree order");
            label.OnClick = _ => labelClicks++;
            InputDriver input = host.Drive(menu);
            TestHost.Layout(menu);

            // without a frame the label (later sibling) wins
            input.Click(label.Bounds.X + 2, label.Bounds.Y + 2);
            Assert.Equal(1, labelClicks);
            Assert.Equal(0, overlay.Clicks);

            // simulate the draw pass that registers the custom component in the overlay
            UIMenu model = TestHost.Unwrap(menu);
            model.Overlay.RegisterElement((UIElement)custom, _ => { });
            model.Overlay.Draw(null!);

            input.Click(label.Bounds.X + 2, label.Bounds.Y + 2);
            Assert.Equal(1, labelClicks);
            Assert.Equal(1, overlay.Clicks);
        }

        [Fact]
        public void PopupsGetFirstPickAndSwallowOutsideClicks()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            IUIDropdown dropdown = host.Api.AddDropdown(menu.Root, "d", () => new[] { "a", "b" }, null!, null!, null!);
            host.Api.AddSpacer(menu.Root, "gap", 0, 200);
            int clicks = 0;
            IUIButton button = host.Api.AddButton(menu.Root, "b", () => "Go", _ => clicks++);
            InputDriver input = host.Drive(menu);

            dropdown.Open();
            Assert.True(TestHost.Unwrap(menu).Overlay.HasPopups);
            Assert.True(input.Click(button.Bounds.Center.X, button.Bounds.Center.Y));
            Assert.Equal(0, clicks);
            Assert.False(dropdown.IsOpen);
        }

        [Fact]
        public void FaultingCallbacksAreMutedAfterTheFirstThrow()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            int calls = 0;
            IUIButton button = host.Api.AddButton(menu.Root, "b", () => "Boom", _ =>
            {
                calls++;
                throw new System.InvalidOperationException("consumer bug");
            });
            InputDriver input = host.Drive(menu);

            input.Click(button.Bounds.Center.X, button.Bounds.Center.Y);
            input.Click(button.Bounds.Center.X, button.Bounds.Center.Y);

            Assert.Equal(1, calls);
        }

        [Fact]
        public void HotkeyServiceParsesKeybindListsAndDropsInvalidOnes()
        {
            var host = new TestHost();
            var consumer = new ConsumerContext("hotkeys.test");

            Assert.True(HotkeyService.TryParse("F8", consumer, out KeybindList single));
            Assert.True(single.IsBound);
            Assert.True(HotkeyService.TryParse("LeftControl + F8, LeftShift + F9", consumer, out KeybindList combo));
            Assert.Equal(2, combo.Keybinds.Length);
            Assert.False(HotkeyService.TryParse("NotAKey", consumer, out _));
            Assert.False(HotkeyService.TryParse(string.Empty, consumer, out _));

            host.Hotkeys.Register(consumer, "open", "F8", () => { });
            Assert.Equal(1, host.Hotkeys.Count);
            host.Hotkeys.Register(consumer, "open", "NotAKey", () => { });
            Assert.Equal(0, host.Hotkeys.Count);

            host.Api.RegisterHotkey("a", "F1", () => { });
            host.Api.RegisterHotkey("b", "F2", () => { });
            host.Api.UnregisterHotkey("a");
            Assert.Equal(1, host.Hotkeys.Count);
            host.Hotkeys.UnregisterAll(host.Consumer);
            Assert.Equal(0, host.Hotkeys.Count);
        }
    }
}
