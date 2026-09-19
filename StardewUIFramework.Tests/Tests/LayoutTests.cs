using Microsoft.Xna.Framework;
using StardewUIFramework.Tests.Testing;
using UIFramework.Api;
using Xunit;

namespace StardewUIFramework.Tests.Tests
{
    /// <summary>Measure / arrange of the built-in containers (viewport 1280x720, 8 px per character, 16 px lines).</summary>
    public class LayoutTests
    {
        [Fact]
        public void StackOfLabelsFitsContentAndIsCentered()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            host.Api.AddLabel(menu.Root, "a", () => "Hello");
            host.Api.AddLabel(menu.Root, "b", () => "World!!");

            string snapshot = TreeSnapshot.Render(menu);

            // widest label 56, heights 16 + 8 spacing + 16 = 40, centered in 1280x720
            Assert.Equal(
                "Stack 'm.root' [612,340 56x40]\n" +
                "  Label 'a' [612,340 40x16]\n" +
                "  Label 'b' [612,364 56x16]",
                snapshot);
            Assert.Equal(new Rectangle(612, 340, 56, 40), menu.Bounds);
        }

        [Fact]
        public void HorizontalStackRespectsSpacingAndCrossAlignment()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            IUIStack row = host.Api.AddStack(menu.Root, "row", true, 4);
            row.Alignment = UIAlign.Center;
            host.Api.AddSpacer(row, "tall", 10, 40);
            IUILabel label = host.Api.AddLabel(row, "text", () => "ab");

            TestHost.Layout(menu);

            Assert.Equal(new Rectangle(row.Bounds.X + 14, row.Bounds.Y + 12, 16, 16), label.Bounds);
            Assert.Equal(30, row.Bounds.Width);
            Assert.Equal(40, row.Bounds.Height);
        }

        [Fact]
        public void GridDistributesAutoAndStarTracksAndHonoursSpans()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            menu.Width = 400;
            IUIGrid grid = host.Api.AddGrid(menu.Root, "form", "auto,*", "auto,auto");
            IUILabel name = host.Api.AddLabel(grid, "name", () => "Name");
            IUILabel value = host.Api.AddLabel(grid, "value", () => "value");
            value.Column = 1;
            value.HorizontalAlign = UIAlign.Stretch;
            IUILabel span = host.Api.AddLabel(grid, "span", () => "spanning");
            span.Row = 1;
            span.ColumnSpan = 2;

            TestHost.Layout(menu);

            Assert.Equal(new Rectangle(440, 344, 400, 32), grid.Bounds);
            Assert.Equal(new Rectangle(440, 344, 32, 16), name.Bounds);
            Assert.Equal(new Rectangle(472, 344, 368, 16), value.Bounds);
            Assert.Equal(new Rectangle(440, 360, 64, 16), span.Bounds);
        }

        [Fact]
        public void GridPixelTracksAndSpacingAreExact()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            IUIGrid grid = host.Api.AddGrid(menu.Root, "g", "100px,50", "auto");
            grid.ColumnSpacing = 10;
            IUILabel a = host.Api.AddLabel(grid, "a", () => "a");
            IUILabel b = host.Api.AddLabel(grid, "b", () => "b");
            b.Column = 1;

            TestHost.Layout(menu);

            Assert.Equal(160, grid.Bounds.Width);
            Assert.Equal(grid.Bounds.X, a.Bounds.X);
            Assert.Equal(grid.Bounds.X + 110, b.Bounds.X);
        }

        [Fact]
        public void SpacerTakesItsSizeAndDividerGetsLineHeight()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            host.Api.AddLabel(menu.Root, "top", () => "Hi");
            IUISpacer gap = host.Api.AddSpacer(menu.Root, "gap", 0, 24);
            IUILabel bottom = host.Api.AddLabel(menu.Root, "bottom", () => "Yo");
            IUISpacer divider = host.Api.AddSpacer(menu.Root, "divider", 120, 6);
            divider.Line = true;

            TestHost.Layout(menu);

            Assert.Equal(new Point(0, 24), new Point(gap.Bounds.Width, gap.Bounds.Height));
            Assert.Equal(menu.Bounds.Y + 16 + 8 + 24 + 8, bottom.Bounds.Y);
            Assert.Equal(new Point(120, 6), new Point(divider.Bounds.Width, divider.Bounds.Height));
            Assert.Equal(16 + 8 + 24 + 8 + 16 + 8 + 6, menu.Bounds.Height);
        }

        [Fact]
        public void ExplicitSizeMarginsAndAlignmentAreApplied()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            menu.Width = 200;
            IUILabel label = host.Api.AddLabel(menu.Root, "l", () => "x");
            label.Width = 50;
            label.Height = 30;
            label.SetMargin(10, 4);
            label.HorizontalAlign = UIAlign.End;

            TestHost.Layout(menu);

            // slot is the root's full width; End alignment pushes the 50 px box to the right, inside the 10 px margin
            Assert.Equal(new Rectangle(menu.Bounds.Right - 10 - 50, menu.Bounds.Y + 4, 50, 30), label.Bounds);
            Assert.Equal(38, menu.Bounds.Height);
        }

        [Fact]
        public void HiddenElementsTakeNoSpaceAndWrappedLabelsGrowDown()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            menu.Width = 80;
            IUILabel hidden = host.Api.AddLabel(menu.Root, "hidden", () => "invisible");
            hidden.Visible = false;
            IUILabel wrapped = host.Api.AddLabel(menu.Root, "wrapped", () => "one two three");
            wrapped.Wrap = true;

            TestHost.Layout(menu);

            Assert.Equal(0, hidden.Bounds.Height);
            // 80 px = 10 chars per line → "one two" / "three"
            Assert.Equal(32, wrapped.Bounds.Height);
            Assert.Equal(32, menu.Bounds.Height);
        }

        [Fact]
        public void CanvasPlacesChildrenAtOffsets()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            IUICanvas canvas = host.Api.AddCanvas(menu.Root, "c");
            IUILabel label = host.Api.AddLabel(canvas, "l", () => "abc");
            label.X = 30;
            label.Y = 12;

            TestHost.Layout(menu);

            Assert.Equal(new Rectangle(canvas.Bounds.X + 30, canvas.Bounds.Y + 12, 24, 16), label.Bounds);
            Assert.Equal(new Point(54, 28), new Point(canvas.Bounds.Width, canvas.Bounds.Height));
        }
    }
}
