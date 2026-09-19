using System.Collections.Generic;
using StardewUIFramework.Tests.Testing;
using UIFramework.Api;
using Xunit;

namespace StardewUIFramework.Tests.Tests
{
    /// <summary>ScrollView offset clamping / wheel and ListView virtualization.</summary>
    public class ScrollAndListTests
    {
        private static IUIScrollView BuildScrollView(TestHost host, out IUIMenu menu)
        {
            menu = host.CreateBareMenu("m");
            menu.Width = 300;
            IUIScrollView view = host.Api.AddScrollView(menu.Root, "view", 100);
            IUIStack content = host.Api.AddStack(view, "content", false, 0);
            for (int i = 0; i < 10; i++)
            {
                host.Api.AddLabel(content, "l" + i, () => "Line");
            }

            TestHost.Layout(menu);
            return view;
        }

        [Fact]
        public void ScrollViewClampsOffsetToContent()
        {
            var host = new TestHost();
            IUIScrollView view = BuildScrollView(host, out _);

            Assert.Equal(100, view.Bounds.Height);
            Assert.Equal(60, view.MaxScroll); // 10 x 16 px content in a 100 px viewport

            view.ScrollTo(1000);
            Assert.Equal(60, view.ScrollOffset);
            view.ScrollBy(-1000);
            Assert.Equal(0, view.ScrollOffset);
        }

        [Fact]
        public void ScrollViewWheelMovesByStepAndFallsThroughAtTheEnd()
        {
            var host = new TestHost();
            IUIScrollView view = BuildScrollView(host, out IUIMenu menu);
            InputDriver input = host.Drive(menu);
            var deltas = new List<int>();
            view.OnScroll = d => deltas.Add(d);

            input.Hover(view.Bounds.X + 5, view.Bounds.Y + 5);
            Assert.True(input.Scroll(-1));   // down: 64 px step clamped to 60
            Assert.Equal(60, view.ScrollOffset);
            Assert.False(input.Scroll(-1));  // already at the end: not handled, no event
            Assert.True(input.Scroll(1));    // up
            Assert.Equal(0, view.ScrollOffset);
            Assert.Equal(new[] { 60, -60 }, deltas);
            Assert.Contains("shiny4", host.Sounds);

            // the content moved with the offset
            IUIElement first = menu.Find("l0");
            view.ScrollTo(60);
            TestHost.Layout(menu);
            Assert.Equal(view.Bounds.Y - 60, first.Bounds.Y);
        }

        [Fact]
        public void ListViewOnlyBuildsVisibleRowsAndRebuildsOnScroll()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            menu.Width = 300;
            var built = new List<int>();
            IUIList list = host.Api.AddList(menu.Root, "list", 20, 3, () => 10, (index, row) =>
            {
                built.Add(index);
                host.Api.AddLabel(row, "item" + index, () => "Item " + index);
            });
            InputDriver input = host.Drive(menu);

            input.Tick(16);

            Assert.Equal(3, list.ChildCount);
            Assert.Equal(new[] { 0, 1, 2 }, built);
            Assert.Equal(10, list.ItemCount);
            Assert.Equal(60, list.Bounds.Height);
            Assert.Equal("item0", ((IUIContainer)list.GetChild(0)).GetChild(0).Id);
            Assert.Equal(list.Bounds.Y + 40, list.GetChild(2).Bounds.Y);

            input.Hover(list.Bounds.X + 5, list.Bounds.Y + 5);
            Assert.True(input.Scroll(-1));
            Assert.Equal(1, list.FirstVisibleIndex);
            Assert.Equal(new[] { 0, 1, 2, 1, 2, 3 }, built);
            Assert.Equal("item1", ((IUIContainer)list.GetChild(0)).GetChild(0).Id);

            list.ScrollTo(100);
            Assert.Equal(7, list.FirstVisibleIndex);
        }

        [Fact]
        public void ListViewSelectsRowOnClick()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            menu.Width = 300;
            IUIList list = host.Api.AddList(menu.Root, "list", 20, 3, () => 5, (index, row) => host.Api.AddLabel(row, "item" + index, () => "Item"));
            list.Selectable = true;
            int? selected = null;
            list.OnValueChanged = e => selected = e.NewIndex;
            InputDriver input = host.Drive(menu);
            input.Tick(16);

            input.Click(list.Bounds.X + 5, list.Bounds.Y + 45);

            Assert.Equal(2, list.SelectedIndex);
            Assert.Equal(2, selected);
        }
    }
}
