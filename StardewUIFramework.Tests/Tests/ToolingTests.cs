using Microsoft.Xna.Framework.Input;
using StardewUIFramework.Tests.Testing;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Hosting;
using Xunit;

namespace StardewUIFramework.Tests.Tests
{
    /// <summary>The developer tools: tree dump, builder-code export, perf counters and the inspector's picking / editing.</summary>
    public class ToolingTests
    {
        private static IUIMenu BuildForm(TestHost host)
        {
            IUIMenuOptions options = host.Api.CreateMenuOptions();
            options.Title = () => "Settings";
            options.Width = 400;
            options.DrawBox = false;
            IUIMenu menu = host.Api.CreateMenu("settings", options);
            IUIGrid form = host.Api.AddGrid(menu.Root, "form", "auto,*", "auto");
            form.ColumnSpacing = 12;
            IUILabel label = host.Api.AddLabel(form, "name.label", () => "Name");
            label.SetMargin(4);
            IUITextInput input = host.Api.AddTextInput(form, "name", () => "Farmer", _ => { });
            input.Column = 1;
            input.MaxLength = 12;
            input.HorizontalAlign = UIAlign.Stretch;
            IUIButton ok = host.Api.AddButton(menu.Root, "ok", () => "Ok", _ => { });
            ok.Tooltip = () => "Apply";
            menu.DefaultButton = ok;
            return menu;
        }

        [Fact]
        public void TreeDumpListsEveryElementWithBoundsAndState()
        {
            var host = new TestHost();
            IUIMenu menu = BuildForm(host);
            IUIElement label = menu.Find("name.label");
            label.Enabled = false;
            TestHost.Layout(menu);

            string dump = TreeDump.Render(TestHost.Unwrap(menu).Root, includeState: true);

            string[] lines = dump.Split('\n');
            Assert.Equal(5, lines.Length);
            Assert.StartsWith("Stack 'settings.root' [", lines[0]);
            Assert.StartsWith("  Grid 'form' [", lines[1]);
            Assert.Equal($"    Label 'name.label' [{label.Bounds.X},{label.Bounds.Y} 32x16] visible=true enabled=false", lines[2]);
            Assert.EndsWith("visible=true enabled=true", lines[4]);
            Assert.Equal("Button 'ok' [" + menu.Find("ok").Bounds.X + "," + menu.Find("ok").Bounds.Y + " 64x64] visible=true enabled=true", TreeDump.Line((UIElement)menu.Find("ok"), true));
        }

        [Fact]
        public void ExportWritesBuilderCodeWithNestingPropertiesAndTodos()
        {
            var host = new TestHost();
            IUIMenu menu = BuildForm(host);

            string code = TreeExporter.Export(TestHost.Unwrap(menu));

            Assert.Contains("IUIMenuOptions options = api.CreateMenuOptions();", code);
            Assert.Contains("options.Title = () => \"Settings\"; /* TODO */", code);
            Assert.Contains("options.Width = 400;", code);
            Assert.Contains("options.DrawBox = false;", code);
            Assert.Contains("IUIMenu menu = api.CreateMenu(\"settings\", options);", code);
            Assert.Contains("IUIStack root = menu.Root;", code);
            Assert.Contains("IUIGrid form = api.AddGrid(root, \"form\", \"auto,*\", \"auto\");", code);
            Assert.Contains("form.ColumnSpacing = 12;", code);
            Assert.Contains("IUILabel nameLabel = api.AddLabel(form, \"name.label\", () => \"Name\" /* TODO */);", code);
            Assert.Contains("nameLabel.SetMargin(4);", code);
            Assert.Contains("IUITextInput name = api.AddTextInput(form, \"name\", () => \"Farmer\" /* TODO */, value => { /* TODO */ });", code);
            Assert.Contains("name.MaxLength = 12;", code);
            Assert.Contains("name.HorizontalAlign = UIAlign.Stretch;", code);
            Assert.Contains("name.Column = 1;", code);
            Assert.Contains("IUIButton ok = api.AddButton(root, \"ok\", () => \"Ok\" /* TODO */, e => { /* TODO */ });", code);
            Assert.Contains("ok.Tooltip = () => \"Apply\"; /* TODO */", code);
            Assert.Contains("menu.DefaultButton = ok;", code);
            Assert.DoesNotContain("options.Modal", code);
            Assert.DoesNotContain("ok.OnClick", code);
        }

        [Fact]
        public void ExportKeepsIdentifiersUniqueAndEscapesStrings()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            host.Api.AddLabel(menu.Root, "class", () => "say \"hi\"\n");
            host.Api.AddLabel(menu.Root, "class", () => "again");
            host.Api.AddLabel(menu.Root, "1st", () => "x");
            IUIList list = host.Api.AddList(menu.Root, "list", 20, 2, () => 3, (i, row) => host.Api.AddLabel(row, "row" + i, () => "r"));
            list.Selectable = true;
            host.Drive(menu).Tick(16);

            string code = TreeExporter.Export(TestHost.Unwrap(menu));

            Assert.Contains("IUILabel class2 = api.AddLabel(root, \"class\", () => \"say \\\"hi\\\"\\n\" /* TODO */);", code);
            Assert.Contains("IUILabel class3 = api.AddLabel(root, \"class\", () => \"again\" /* TODO */);", code);
            Assert.Contains("IUILabel label1st = api.AddLabel(root, \"1st\"", code);
            Assert.Contains("IUIList list = api.AddList(root, \"list\", 20, 2, () => 3 /* TODO */, (index, row) => { /* TODO */ });", code);
            Assert.Contains("list.Selectable = true;", code);
            Assert.DoesNotContain("row0", code); // list rows are not exported
        }

        [Fact]
        public void PerfCountersSampleFramesOnlyWhenEnabled()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            host.Api.AddLabel(menu.Root, "l", () => "x");
            UIMenu model = TestHost.Unwrap(menu);
            InputDriver input = host.Drive(menu);

            input.Tick(16);
            Assert.Null(PerfCounters.Describe(model));

            PerfCounters.Enabled = true;
            try
            {
                input.Tick(16);
                Assert.Null(PerfCounters.Describe(model)); // no frame has ended yet (frames end on draw)
                using (PerfCounters.Begin(model, PerfCounters.Phase.Draw, endsFrame: true))
                {
                    host.Consumer.Invoke("l", "Text", () => { });
                }

                string? report = PerfCounters.Describe(model);
                Assert.NotNull(report);
                Assert.StartsWith("1 frame(s) sampled", report);
                Assert.Contains("callback", report);
            }
            finally
            {
                PerfCounters.Enabled = false;
            }
        }

        [Fact]
        public void InspectorPicksLayoutOnlyContainersAndEditsTheSubject()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            IUIStack row = host.Api.AddStack(menu.Root, "row", true, 8);
            row.Width = 200;
            row.Height = 40;
            IUILabel label = host.Api.AddLabel(row, "l", () => "abc");
            UIMenu model = TestHost.Unwrap(menu);
            TestHost.Layout(menu);
            Inspector.SetEnabled(true);
            try
            {
                // the stack's empty area is not hit-testable for the router, but the inspector picks it
                Inspector.HandleHover(model, row.Bounds.Right - 2, row.Bounds.Bottom - 2);
                Assert.Same(row, Inspector.Subject(model));
                Inspector.HandleHover(model, label.Bounds.X + 1, label.Bounds.Y + 1);
                Assert.Same(label, Inspector.Subject(model));

                Inspector.HandleKey(model, Keys.Right, shift: true, ctrl: false);
                Inspector.HandleKey(model, Keys.Down, shift: false, ctrl: false);
                Assert.Equal(8, label.MarginLeft);
                Assert.Equal(1, label.MarginTop);

                Inspector.HandleKey(model, Keys.OemPlus, shift: false, ctrl: true);
                Assert.Equal(24 + 8, label.Width);
                Inspector.HandleKey(model, Keys.OemMinus, shift: true, ctrl: false);
                Assert.Equal(15, label.Height);

                Inspector.HandleClick(model, label.Bounds.X + 1, label.Bounds.Y + 1);
                Assert.Same(label, Inspector.Pinned);
                Inspector.HandleKey(model, Keys.V, shift: false, ctrl: false);
                Assert.False(label.Visible);
                Assert.Same(label, Inspector.Subject(model)); // pinned even though it is hidden now

                string[] info = Inspector.Describe((UIElement)label);
                Assert.StartsWith("Label 'l'  (pinned)", info[0]);
                Assert.Contains("parent: m.viewport > m.root > row", info);
                Assert.Equal(Inspector.KeyHelp, info[^1]);

                Inspector.HandleKey(model, Keys.Escape, shift: false, ctrl: false);
                Assert.False(Inspector.Enabled);
            }
            finally
            {
                Inspector.SetEnabled(false);
            }
        }

        [Fact]
        public void ParseTracksAndNumbersHelpers()
        {
            var tracks = LayoutEngine.ParseTracks(" auto, 120px ,2*, 30 , * ");
            Assert.Equal(new[] { "auto", "120px", "2*", "30px", "1*" }, tracks.ConvertAll(t => t.ToString()));
            Assert.Single(LayoutEngine.ParseTracks(string.Empty));

            float[] sizes = LayoutEngine.ResolveTracks(tracks, new float[] { 10, 0, 0, 0, 0 }, 400);
            Assert.Equal(new float[] { 10, 120, 160, 30, 80 }, sizes);
            Assert.True(Numbers.Same(0.1 + 0.2, 0.3));
        }
    }
}
