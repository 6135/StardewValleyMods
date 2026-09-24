using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using UIFramework.Api;
using UIFramework.Components;
using UIFramework.Rendering;

namespace UIFramework.Core.Export
{
    /// <summary>
    /// Walks a menu tree once into the format-neutral <see cref="TreeMenu"/> model. Every consumer delegate that
    /// yields a displayable value is evaluated through the menu's callback guard (the preview the emitters write);
    /// the others are recorded as set / not set. Properties are added in a fixed order per element kind
    /// (type-specific members, then the common ones), which is the order the C# emitter writes them in.
    /// <para>
    /// Defaults are those of the data format (<c>DataBuilder</c>) where they differ from the C# constructors' free
    /// arguments; the C# emitter always writes constructor arguments, so this only affects the JSON output.
    /// </para>
    /// </summary>
    internal sealed class TreeModelReader
    {
        // data-format defaults (see Data/Building/DataBuilder.cs)
        private const int DefaultSpacing = 8;
        private const int DefaultPanelPadding = 16;
        private const int DefaultViewportHeight = 300;
        private const float DefaultImageScale = 4f;
        private const double DefaultNumberMax = 999999;
        private const double DefaultSliderMax = 100;

        private readonly UIMenu menu;
        private readonly Dictionary<UIElement, TreeNode> nodes = new();

        private TreeModelReader(UIMenu menu)
        {
            this.menu = menu;
        }

        /// <summary>The model of <paramref name="menu"/>.</summary>
        internal static TreeMenu Read(UIMenu menu) => new TreeModelReader(menu).ReadMenu();

        // ---------------------------------------------------------------------------------------------------------
        //  Menu
        // ---------------------------------------------------------------------------------------------------------

        private TreeMenu ReadMenu()
        {
            Stack root = menu.Root;
            var rootNode = new TreeNode(TreeKinds.Stack, root.Id, root.GetType().Name);
            nodes[root] = rootNode;
            rootNode.Properties.Add(Bool("Horizontal", root.Horizontal, false));
            rootNode.Properties.Add(Int("Spacing", root.Spacing, DefaultSpacing));
            rootNode.Properties.Add(Enum("Alignment", root.Alignment, root.Alignment == UIAlign.Start));
            rootNode.Properties.Add(Margin(root));
            rootNode.Properties.Add(OptionalInt("Width", root.Width));
            rootNode.Properties.Add(OptionalInt("Height", root.Height));

            var model = new TreeMenu(menu.Id, menu.Consumer.ModId, rootNode);
            List<TreeProperty> o = model.Options;
            o.Add(Text("Title", menu.Id, "Export.Title", menu.TitleFunc));
            o.Add(OptionalInt("Width", menu.Width));
            o.Add(OptionalInt("Height", menu.Height));
            o.Add(Bool("ShowCloseButton", menu.ShowCloseButton, true));
            o.Add(Bool("Modal", menu.Modal, true));
            o.Add(Bool("DimBackground", menu.DimBackground, true));
            o.Add(Enum("Anchor", menu.Anchor, menu.Anchor == UIAnchor.Center));
            o.Add(Int("X", menu.X, 0));
            o.Add(Int("Y", menu.Y, 0));
            o.Add(Bool("DrawBox", menu.DrawBox, true));
            o.Add(Int("Padding", menu.Padding, 0));
            o.Add(Bool("CloseOnEscape", menu.CloseOnEscape, true));
            o.Add(Bool("PlayerLayout", menu.PlayerLayout, true));

            List<TreeProperty> ev = model.Events;
            ev.Add(Todo("OnOpen", menu.OnOpen));
            ev.Add(Todo("OnClose", menu.OnClose));
            ev.Add(Todo("OnUpdate", menu.OnUpdate));
            ev.Add(Todo("OnKey", menu.OnKey));
            ev.Add(Todo("OnScroll", menu.OnScroll));

            ReadChildren(root.Children, rootNode);

            if (menu.DefaultButtonElement != null && nodes.TryGetValue(menu.DefaultButtonElement, out TreeNode? def))
            {
                model.DefaultButton = def;
            }

            if (menu.CancelButtonElement != null && nodes.TryGetValue(menu.CancelButtonElement, out TreeNode? cancel))
            {
                model.CancelButton = cancel;
            }

            return model;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Elements
        // ---------------------------------------------------------------------------------------------------------

        private void ReadChildren(IEnumerable<UIElement> children, TreeNode parent)
        {
            foreach (UIElement child in children)
            {
                parent.Children.Add(ReadElement(child));
            }
        }

        private TreeNode ReadElement(UIElement e)
        {
            string kind = KindOf(e);
            var node = new TreeNode(kind, e.Id, e.GetType().Name);
            nodes[e] = node;
            if (kind == TreeKinds.Unknown)
            {
                return node;
            }

            ReadTypeMembers(e, node);
            ReadCommon(e, node);
            ReadStructure(e, node);
            return node;
        }

        private static string KindOf(UIElement e)
        {
            return e switch
            {
                Slot => TreeKinds.Slot,
                Stack => TreeKinds.Stack,
                Grid => TreeKinds.Grid,
                Panel => TreeKinds.Panel,
                Canvas => TreeKinds.Canvas,
                ScrollView => TreeKinds.ScrollView,
                ListView => TreeKinds.List,
                Composite => TreeKinds.Composite,
                CustomHostAdapter => TreeKinds.CustomHost,
                DataGrid => TreeKinds.DataGrid,
                AutoForm => TreeKinds.Form,
                Label => TreeKinds.Label,
                Image => TreeKinds.Image,
                ItemImage => TreeKinds.ItemImage,
                Button => TreeKinds.Button,
                Checkbox => TreeKinds.Checkbox,
                TextInput => TreeKinds.TextInput,
                NumberInput => TreeKinds.NumberInput,
                Dropdown => TreeKinds.Dropdown,
                Slider => TreeKinds.Slider,
                Spacer => TreeKinds.Spacer,
                CustomElementAdapter => TreeKinds.Custom,
                _ => TreeKinds.Unknown
            };
        }

        /// <summary>Which children are exported, and why the others are not.</summary>
        private void ReadStructure(UIElement e, TreeNode node)
        {
            switch (e)
            {
                case Slot slot:
                    foreach (UIElement child in slot.Children)
                    {
                        if (child.Contributor != null)
                        {
                            node.ExcludedContributors.Add(child.Contributor.ModId);
                        }
                        else
                        {
                            node.Children.Add(ReadElement(child));
                        }
                    }
                    break;
                case ListView:
                    node.ChildrenNote = "built by its buildRow delegate";
                    break;
                case Composite composite:
                    node.ChildrenNote = $"built by composite '{composite.CompositeName}'";
                    break;
                case CustomHostAdapter host:
                    ReadChildren(host.Host.Children, node);
                    break;
                case DataGrid:
                    node.ChildrenNote = "built from its columns";
                    break;
                case AutoForm:
                    node.ChildrenNote = "generated from its model";
                    break;
                case UIContainer container:
                    ReadChildren(container.Children, node);
                    break;
                default:
                    // leaf
                    break;
            }
        }

        private void ReadTypeMembers(UIElement e, TreeNode node)
        {
            List<TreeProperty> p = node.Properties;
            switch (e)
            {
                case Slot slot:
                    p.Add(Bool("Horizontal", slot.Horizontal, false));
                    p.Add(OptionalInt("MaxHeight", slot.MaxHeight));
                    p.Add(Int("MaxContributions", slot.MaxContributions, 0));
                    p.Add(TreeProperty.Literal("VetoedContributors", TreeValueKind.StringList, slot.VetoedContributors, slot.VetoedContributors.Length == 0));
                    p.Add(Todo("VisiblePredicate", slot.VisiblePredicate));
                    break;
                case Stack stack:
                    p.Add(Bool("Horizontal", stack.Horizontal, false));
                    p.Add(Int("Spacing", stack.Spacing, DefaultSpacing));
                    p.Add(Enum("Alignment", stack.Alignment, stack.Alignment == UIAlign.Start));
                    break;
                case Grid grid:
                    p.Add(TreeProperty.Literal("Columns", TreeValueKind.String, grid.Columns, grid.Columns == "*"));
                    p.Add(TreeProperty.Literal("Rows", TreeValueKind.String, grid.Rows, grid.Rows == "auto"));
                    p.Add(Int("ColumnSpacing", grid.ColumnSpacing, 0));
                    p.Add(Int("RowSpacing", grid.RowSpacing, 0));
                    break;
                case Panel panel:
                    p.Add(Bool("DrawBox", panel.DrawBox, true));
                    p.Add(Int("Padding", panel.Padding, DefaultPanelPadding));
                    break;
                case ScrollView scroll:
                    p.Add(Int("ViewportHeight", scroll.ViewportHeight, DefaultViewportHeight));
                    p.Add(Int("ScrollStep", scroll.ScrollStep, 64));
                    p.Add(Bool("ShowScrollbar", scroll.ShowScrollbar, true));
                    p.Add(Todo("OnScroll", scroll.OnScroll));
                    break;
                case ListView list:
                    p.Add(TreeProperty.Literal("RowHeight", TreeValueKind.Int, list.RowHeight, false));
                    p.Add(TreeProperty.Literal("VisibleRows", TreeValueKind.Int, list.VisibleRows, false));
                    p.Add(TreeProperty.Evaluated("ItemCount", TreeValueKind.Int, list.ItemCount, true));
                    p.Add(Bool("Selectable", list.Selectable, false));
                    p.Add(Todo("OnValueChanged", list.OnValueChanged));
                    p.Add(Todo("OnScroll", list.OnScroll));
                    break;
                case Composite composite:
                    node.CompositeName = composite.CompositeName;
                    node.IsDataComposite = composite.IsDataComposite;
                    ReadArgs(composite, node);
                    break;
                case DataGrid grid:
                    ReadDataGrid(grid, node);
                    break;
                case AutoForm form:
                    node.ModelType = form.Model.GetType().FullName ?? form.Model.GetType().Name;
                    p.Add(Bool("ShowButtons", form.ShowButtons, true));
                    p.Add(Todo("OnSaved", form.OnSaved));
                    p.Add(Todo("OnCancelled", form.OnCancelled));
                    p.Add(Todo("OnChanged", form.OnChanged));
                    break;
                default:
                    ReadLeaf(e, p);
                    break;
            }
        }

        private void ReadLeaf(UIElement e, List<TreeProperty> p)
        {
            switch (e)
            {
                case Label l:
                    p.Add(TreeProperty.Evaluated("Text", TreeValueKind.String, Eval(l.Id, "Export.Text", l.TextFunc), true));
                    p.Add(Enum("Font", l.Font, l.Font == UIFont.Small));
                    p.Add(OptionalColor("Color", l.Color));
                    p.Add(Bool("Shadow", l.Shadow, false));
                    p.Add(Bool("Wrap", l.Wrap, false));
                    p.Add(Enum("TextAlign", l.TextAlign, l.TextAlign == UIAlign.Start));
                    p.Add(Float("Scale", l.Scale, 1f));
                    p.Add(Bool("RichText", l.RichText, false));
                    p.Add(Todo("OnLink", l.OnLink));
                    break;
                case Image i:
                    p.Add(Todo("Texture", i.Texture, TextureName(i.Texture), "guessed from the texture's asset name"));
                    p.Add(TreeProperty.Literal("Source", TreeValueKind.Rect, i.Source, !i.Source.HasValue));
                    p.Add(Float("Scale", i.Scale, DefaultImageScale));
                    p.Add(Color("Tint", i.Tint, Microsoft.Xna.Framework.Color.White));
                    break;
                case ItemImage i:
                    ReadItemImage(i, p);
                    break;
                case Button b:
                    p.Add(TreeProperty.Evaluated("Text", TreeValueKind.String, Eval(b.Id, "Export.Text", b.TextFunc), true));
                    p.Add(Enum("Font", b.Font, b.Font == UIFont.Small));
                    TreeIcon? icon = b.Icon == null ? null : new TreeIcon { Source = b.IconSource, Scale = b.IconScale, TextureName = TextureName(b.Icon) };
                    p.Add(TreeProperty.Literal("Icon", TreeValueKind.Icon, icon, icon == null));
                    p.Add(Bool("DrawBox", b.DrawBox, true));
                    p.Add(Sound("ClickSound", b.ClickSound));
                    p.Add(Sound("HoverSound", b.HoverSound));
                    p.Add(Bool("RichText", b.RichText, false));
                    break;
                case Checkbox c:
                    p.Add(TreeProperty.Evaluated("Value", TreeValueKind.Bool, c.Value, true));
                    p.Add(Text("Label", c.Id, "Export.Label", c.LabelFunc));
                    p.Add(Sound("ClickSound", c.ClickSound));
                    p.Add(Todo("OnValueChanged", c.OnValueChanged));
                    break;
                case TextInput t:
                    p.Add(TreeProperty.Evaluated("Value", TreeValueKind.String, t.Value, true));
                    p.Add(Text("Placeholder", t.Id, "Export.Placeholder", t.PlaceholderFunc));
                    p.Add(Int("MaxLength", t.MaxLength, 0));
                    p.Add(Todo("Validate", t.ValidateFunc));
                    p.Add(Todo("Texture", t.Texture, TextureName(t.Texture), "guessed from the texture's asset name"));
                    p.Add(Todo("OnValueChanged", t.OnValueChanged));
                    p.Add(Todo("OnSubmit", t.OnSubmit));
                    break;
                case NumberInput n:
                    p.Add(TreeProperty.Evaluated("Value", TreeValueKind.Double, n.Value, true));
                    p.Add(Double("Min", n.Min, 0));
                    p.Add(Double("Max", n.Max, DefaultNumberMax));
                    p.Add(Double("Step", n.Step, 1));
                    p.Add(Bool("Clamp", n.Clamp, true));
                    p.Add(Int("Decimals", n.Decimals, 0));
                    p.Add(Todo("Validate", n.ValidateFunc));
                    p.Add(Todo("Texture", n.Texture, TextureName(n.Texture), "guessed from the texture's asset name"));
                    p.Add(Todo("OnValueChanged", n.OnValueChanged));
                    p.Add(Todo("OnSubmit", n.OnSubmit));
                    break;
                case Dropdown d:
                    ReadDropdown(d, p);
                    break;
                case Slider s:
                    p.Add(TreeProperty.Evaluated("Value", TreeValueKind.Double, s.Value, true));
                    p.Add(Double("Min", s.Min, 0));
                    p.Add(Double("Max", s.Max, DefaultSliderMax));
                    p.Add(TreeProperty.Literal("Step", TreeValueKind.Double, s.Step, !(s.Step > 0)));
                    p.Add(Todo("OnValueChanged", s.OnValueChanged));
                    break;
                case Spacer s:
                    p.Add(TreeProperty.Literal("Width", TreeValueKind.Int, s.Width, (s.Width ?? 0) == 0));
                    p.Add(TreeProperty.Literal("Height", TreeValueKind.Int, s.Height, (s.Height ?? 0) == 0));
                    p.Add(Bool("Line", s.Line, false));
                    break;
                default:
                    // CustomElementAdapter: nothing beyond the common members
                    break;
            }
        }

        private void ReadItemImage(ItemImage i, List<TreeProperty> p)
        {
            Item? item = menu.Consumer.Invoke<Item?>(i.Id, "Export.Item", i.Item, null);
            p.Add(TreeProperty.Evaluated("Item", TreeValueKind.String, item?.QualifiedItemId, true));
            p.Add(TreeProperty.Evaluated("Quality", TreeValueKind.Int, item?.Quality ?? 0, (item?.Quality ?? 0) != 0));
            p.Add(TreeProperty.Evaluated("Count", TreeValueKind.Int, item?.Stack ?? 1, (item?.Stack ?? 1) > 1));
            p.Add(Float("Scale", i.Scale, DefaultImageScale));
            p.Add(Enum("Stack", i.Stack, i.Stack == UIItemStack.Hide));
            p.Add(Bool("DrawShadow", i.DrawShadow, false));
            p.Add(Float("Alpha", i.Alpha, 1f));
            p.Add(Color("Tint", i.Tint, Microsoft.Xna.Framework.Color.White));
        }

        private static void ReadDropdown(Dropdown d, List<TreeProperty> p)
        {
            var choices = new string[d.ChoiceCount];
            var labels = new string[d.ChoiceCount];
            bool sameLabels = true;
            for (int i = 0; i < d.ChoiceCount; i++)
            {
                choices[i] = d.GetChoice(i) ?? string.Empty;
                labels[i] = d.GetLabel(i) ?? string.Empty;
                sameLabels &= choices[i] == labels[i];
            }

            p.Add(TreeProperty.Evaluated("Choices", TreeValueKind.StringList, choices, true));
            p.Add(TreeProperty.Evaluated("Labels", TreeValueKind.StringList, labels, !sameLabels));
            p.Add(TreeProperty.Evaluated("Value", TreeValueKind.String, d.SelectedValue, true));
            p.Add(Int("MaxVisible", d.MaxVisible, 5));
            p.Add(Todo("OnValueChanged", d.OnValueChanged));
            p.Add(Todo("OnScroll", d.OnScroll));
        }

        private void ReadArgs(Composite composite, TreeNode node)
        {
            IUICompositeArgs args = ((IUIComposite)composite).Args;
            foreach (string key in args.Keys)
            {
                node.Args.Add(args.GetObject(key) switch
                {
                    string s => TreeProperty.Literal(key, TreeValueKind.String, s, false),
                    double d => TreeProperty.Literal(key, TreeValueKind.Double, d, false),
                    bool b => TreeProperty.Literal(key, TreeValueKind.Bool, b, false),
                    Func<string> getter => TreeProperty.Evaluated(key, TreeValueKind.String, Eval(composite.Id, "Export.Arg:" + key, getter), true, "SetGetter"),
                    Func<double> number => TreeProperty.Evaluated(key, TreeValueKind.Double, menu.Consumer.Invoke(composite.Id, "Export.Arg:" + key, number, 0d), true, "SetNumberGetter"),
                    Action<string> => TreeProperty.Opaque(key, true, null, "SetSetter"),
                    Action<double> => TreeProperty.Opaque(key, true, null, "SetNumberSetter"),
                    Action => TreeProperty.Opaque(key, true, null, "SetAction"),
                    _ => TreeProperty.Opaque(key, true, null, "SetObject")
                });
            }
        }

        private static void ReadDataGrid(DataGrid grid, TreeNode node)
        {
            List<TreeProperty> p = node.Properties;
            p.Add(TreeProperty.Literal("RowHeight", TreeValueKind.Int, grid.RowHeight, false));
            p.Add(TreeProperty.Literal("VisibleRows", TreeValueKind.Int, grid.VisibleRows, false));
            p.Add(TreeProperty.Evaluated("RowCount", TreeValueKind.Int, grid.RowCount, true));
            p.Add(Bool("Selectable", grid.Selectable, false));
            p.Add(Bool("MultiSelect", grid.MultiSelect, false));
            p.Add(TreeProperty.Literal("SortColumn", TreeValueKind.String, grid.SortColumn, grid.SortColumn.Length == 0));
            p.Add(TreeProperty.Literal("SortDescending", TreeValueKind.Bool, grid.SortDescending, grid.SortColumn.Length == 0 || !grid.SortDescending));
            p.Add(Todo("Filter", grid.FilterFunc));
            p.Add(Todo("OnColumnResized", grid.OnColumnResized));
            p.Add(Todo("OnValueChanged", grid.OnValueChanged));
            p.Add(Todo("OnRowClick", grid.OnRowClick));
            p.Add(Todo("OnRowActivated", grid.OnRowActivated));
            p.Add(Todo("RowTooltip", grid.RowTooltip));
            p.Add(Todo("OnScroll", grid.OnScroll));
            p.Add(Sound("ScrollSound", grid.ScrollSound));
            p.Add(Sound("SelectSound", grid.SelectSound));
            p.Add(Sound("SortSound", grid.SortSound));

            for (int i = 0; i < grid.ColumnCount; i++)
            {
                var c = (DataGridColumn)grid.GetColumn(i);
                var column = new TreeColumn(c.Id, c.HeaderText);
                List<TreeProperty> cp = column.Properties;
                cp.Add(TreeProperty.Literal("Width", TreeValueKind.String, c.Width, c.Width == "*"));
                cp.Add(Bool("Sortable", c.Sortable, false));
                cp.Add(Bool("Resizable", c.Resizable, false));
                cp.Add(Int("MinWidth", c.MinWidth, 24));
                cp.Add(Enum("Align", c.Align, c.Align == UIAlign.Start));
                cp.Add(Todo("Text", c.TextFunc));
                cp.Add(Todo("SortKey", c.SortKeyFunc));
                cp.Add(Todo("SortNumber", c.SortNumberFunc));
                cp.Add(Todo("BuildCell", c.BuildCellFunc));
                cp.Add(Todo("CellTooltip", c.CellTooltipFunc));
                node.Columns.Add(column);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Common members
        // ---------------------------------------------------------------------------------------------------------

        private void ReadCommon(UIElement e, TreeNode node)
        {
            List<TreeProperty> p = node.Properties;
            if (e is not Slot)
            {
                // a slot's visibility is managed by the framework (contributions present + predicate)
                p.Add(Bool("Visible", e.Visible, true));
            }

            p.Add(Bool("Enabled", e.Enabled, true));
            p.Add(Bool("Sealed", e.Sealed, false));
            p.Add(Margin(e));
            if (e is not Spacer)
            {
                p.Add(OptionalInt("Width", e.Width));
                p.Add(OptionalInt("Height", e.Height));
            }

            p.Add(Enum("HorizontalAlign", e.HorizontalAlign, !e.HorizontalAlignSet));
            p.Add(Enum("VerticalAlign", e.VerticalAlign, !e.VerticalAlignSet));

            // canvas offsets and grid cells, only where the parent uses them
            if (e.ParentElement is Canvas)
            {
                p.Add(Int("X", e.X, 0));
                p.Add(Int("Y", e.Y, 0));
            }

            if (e.ParentElement is Grid)
            {
                p.Add(Int("Row", e.Row, 0));
                p.Add(Int("Column", e.Column, 0));
                p.Add(Int("RowSpan", e.RowSpan, 1));
                p.Add(Int("ColumnSpan", e.ColumnSpan, 1));
            }

            TreeStyle? style = ReadStyle(e.StyleObject);
            p.Add(TreeProperty.Literal("Style", TreeValueKind.Style, style, style == null));
            p.Add(Text("Tooltip", e.Id, "Export.Tooltip", e.Tooltip));
            p.Add(Text("TooltipTitle", e.Id, "Export.TooltipTitle", e.TooltipTitle));
            TreeTooltip? tooltip = ReadTooltip(e);
            p.Add(TreeProperty.Literal("RichTooltip", TreeValueKind.Tooltip, tooltip, tooltip == null));
            p.Add(Text("AccessibleName", e.Id, "Export.AccessibleName", e.AccessibleName));
            p.Add(Todo("OnClick", e.OnClick));
            p.Add(Todo("OnRightClick", e.OnRightClick));
            p.Add(Todo("OnHover", e.OnHover));
            p.Add(Todo("OnHoverEnd", e.OnHoverEnd));
            p.Add(Todo("OnFocus", e.OnFocus));
            p.Add(Todo("OnBlur", e.OnBlur));
            p.Add(Todo("OnKey", e.OnKey));
            p.Add(Todo("OnDrawExtra", e.OnDrawExtra));
            p.Add(Todo("OnDrawOverlay", e.OnDrawOverlay));
            p.Add(Todo("Tag", e.Tag, e.Tag as string));
        }

        private static TreeStyle? ReadStyle(UIStyle? s)
        {
            if (s == null)
            {
                return null;
            }

            var style = new TreeStyle();
            List<TreeProperty> p = style.Properties;
            p.Add(TreeProperty.Literal("Font", TreeValueKind.Enum, s.Font, !s.Font.HasValue));
            p.Add(OptionalColor("TextColor", s.TextColor));
            p.Add(OptionalColor("HoverColor", s.HoverColor));
            p.Add(Todo("BoxTexture", s.BoxTexture, TextureName(s.BoxTexture), "guessed from the texture's asset name"));
            p.Add(TreeProperty.Literal("BoxSource", TreeValueKind.Rect, s.BoxSource, !s.BoxSource.HasValue));
            p.Add(TreeProperty.Literal("BoxScale", TreeValueKind.Float, s.BoxScale, !s.BoxScale.HasValue));
            p.Add(OptionalInt("Padding", s.Padding));
            p.Add(TreeProperty.Literal("TextShadow", TreeValueKind.Bool, s.TextShadow, !s.TextShadow.HasValue));
            p.Add(Sound("ClickSound", s.ClickSound));
            p.Add(Sound("HoverSound", s.HoverSound));
            return style;
        }

        private TreeTooltip? ReadTooltip(UIElement e)
        {
            RichTooltip? rich = e.RichTooltip;
            if (rich == null)
            {
                return null;
            }

            var tooltip = new TreeTooltip { MaxWidth = rich.MaxWidthPx };
            foreach (TooltipBlock b in rich.Blocks)
            {
                Item? item = b.ItemGetter == null ? null : menu.Consumer.Invoke<Item?>(e.Id, "Export.TooltipItem", b.ItemGetter, null);
                tooltip.Blocks.Add(new TreeTooltipBlock
                {
                    Kind = b.Kind,
                    Text = b.Text == null ? null : Eval(e.Id, "Export.TooltipLine", b.Text),
                    Color = b.Color,
                    Source = b.Source,
                    Scale = b.Scale,
                    TextureName = TextureName(b.Texture),
                    ItemId = b.ItemId,
                    Amount = b.Amount == null ? 0 : menu.Consumer.Invoke(e.Id, "Export.TooltipMoney", b.Amount, 0),
                    ItemPreview = item?.QualifiedItemId,
                    HasWhen = b.When != null,
                    HasColorFunc = b.ColorFunc != null
                });
            }

            return tooltip;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Property helpers
        // ---------------------------------------------------------------------------------------------------------

        private static TreeProperty Bool(string name, bool value, bool defaultValue) => TreeProperty.Literal(name, TreeValueKind.Bool, value, value == defaultValue);

        private static TreeProperty Int(string name, int value, int defaultValue) => TreeProperty.Literal(name, TreeValueKind.Int, value, value == defaultValue);

        private static TreeProperty OptionalInt(string name, int? value) => TreeProperty.Literal(name, TreeValueKind.Int, value, !value.HasValue);

        private static TreeProperty Float(string name, float value, float defaultValue) => TreeProperty.Literal(name, TreeValueKind.Float, value, Numbers.Same(value, defaultValue));

        private static TreeProperty Double(string name, double value, double defaultValue) => TreeProperty.Literal(name, TreeValueKind.Double, value, Numbers.Same(value, defaultValue));

        private static TreeProperty Enum(string name, System.Enum value, bool isDefault) => TreeProperty.Literal(name, TreeValueKind.Enum, value, isDefault);

        private static TreeProperty Color(string name, Color value, Color defaultValue) => TreeProperty.Literal(name, TreeValueKind.Color, value, value == defaultValue);

        private static TreeProperty OptionalColor(string name, Color? value) => TreeProperty.Literal(name, TreeValueKind.Color, value, !value.HasValue);

        /// <summary>A sound cue: null = default (omitted), empty = silent.</summary>
        private static TreeProperty Sound(string name, string? cue) => TreeProperty.Literal(name, TreeValueKind.String, cue, cue == null);

        private static TreeProperty Todo(string name, object? value, string? hint = null, string? note = null) => TreeProperty.Opaque(name, value != null, hint, hint == null ? null : note);

        private static TreeProperty Margin(UIElement e)
        {
            int[] m = { e.MarginLeft, e.MarginTop, e.MarginRight, e.MarginBottom };
            return TreeProperty.Literal("Margin", TreeValueKind.Margin, m, m[0] == 0 && m[1] == 0 && m[2] == 0 && m[3] == 0);
        }

        /// <summary>A consumer text delegate, evaluated now (not set = default).</summary>
        private TreeProperty Text(string name, string elementId, string eventName, Func<string>? func)
        {
            return TreeProperty.Evaluated(name, TreeValueKind.String, func == null ? null : Eval(elementId, eventName, func), func != null);
        }

        /// <summary>The current value of a consumer text delegate, through the callback guard.</summary>
        private string Eval(string elementId, string eventName, Func<string>? func)
        {
            return menu.Consumer.Invoke(elementId, eventName, func, string.Empty) ?? string.Empty;
        }

        /// <summary>The asset name a texture was loaded from, when the content pipeline recorded one.</summary>
        private static string? TextureName(Texture2D? texture)
        {
            string? name = texture?.Name;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
    }
}
