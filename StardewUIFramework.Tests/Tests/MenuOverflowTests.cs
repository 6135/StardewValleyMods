using StardewUIFramework.Tests.Testing;
using UIFramework.Api;
using UIFramework.Core;
using Xunit;

namespace StardewUIFramework.Tests.Tests
{
    /// <summary>Content taller than the window scrolls inside the menu; overflowing stack children stay inside the box.</summary>
    public class MenuOverflowTests
    {
        private static IUIMenu BuildTallMenu(TestHost host, int lines)
        {
            IUIMenu menu = host.CreateBareMenu("m");
            menu.Width = 300;
            for (int i = 0; i < lines; i++)
            {
                host.Api.AddLabel(menu.Root, "l" + i, () => "Line");
            }

            TestHost.Layout(menu);
            return menu;
        }

        [Fact]
        public void TallContentIsClampedToTheViewportAndScrolls()
        {
            var host = new TestHost { Viewport = new Microsoft.Xna.Framework.Point(800, 200) };
            IUIMenu menu = BuildTallMenu(host, 30);   // 30 x 16 px + spacing, far taller than 200 px
            UIMenu model = TestHost.Unwrap(menu);

            Assert.True(menu.Bounds.Height <= 200);
            Assert.True(model.Viewport.Overflowing);
            Assert.True(model.Viewport.MaxScroll > 0);
            Assert.Null(menu.Root.Parent); // the viewport is a tree element but not the root's API parent
            IUIElement last = menu.Find("l29");
            Assert.True(last.Bounds.Y > menu.Bounds.Bottom);

            InputDriver input = host.Drive(menu);
            input.Hover(menu.Bounds.X + 5, menu.Bounds.Y + 5);
            Assert.True(input.Scroll(-1));            // wheel down scrolls the menu content
            input.Tick(16);
            Assert.True(menu.Find("l0").Bounds.Y < menu.Bounds.Y);

            // elements scrolled out of view are not hit-testable
            input.Hover(last.Bounds.Center.X, last.Bounds.Center.Y);
            Assert.NotEqual(TestHost.Unwrap(menu).Root.FindById("l29"), model.Hovered);

            // focusing an off-screen element scrolls it into view
            IUIButton button = host.Api.AddButton(menu.Root, "b", () => "B", _ => { });
            TestHost.Layout(menu);
            model.Focus.SetFocus(TestHost.Unwrap(menu).Root.FindById("b")!);
            TestHost.Layout(menu);
            Assert.True(button.Bounds.Bottom <= menu.Bounds.Bottom);
        }

        [Fact]
        public void ShortContentDoesNotScroll()
        {
            var host = new TestHost();
            IUIMenu menu = BuildTallMenu(host, 3);
            Assert.False(TestHost.Unwrap(menu).Viewport.Overflowing);
            Assert.False(host.Drive(menu).Scroll(-1));
        }

        [Fact]
        public void HorizontalStackChildrenNeverExtendPastTheStack()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            menu.Width = 200;
            IUIStack row = host.Api.AddStack(menu.Root, "row", true, 8);
            host.Api.AddLabel(row, "a", () => new string('x', 30));   // 240 px in the fake font: wider than the stack
            IUIButton b = host.Api.AddButton(row, "b", () => "Go", _ => { });
            TestHost.Layout(menu);

            Assert.True(menu.Find("a").Bounds.Right <= row.Bounds.Right);
            Assert.True(b.Bounds.Right <= row.Bounds.Right);
        }
    }
}
