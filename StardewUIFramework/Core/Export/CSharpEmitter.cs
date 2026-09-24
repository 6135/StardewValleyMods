using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace UIFramework.Core.Export
{
    /// <summary>
    /// Writes the C# builder code that recreates a <see cref="TreeMenu"/> through the public API: one <c>Add*</c>
    /// call per element with the correct nesting and ids, an assignment for every non-default property, and a
    /// <c>// TODO</c> comment wherever a delegate or texture cannot be serialized (the current value of text
    /// delegates is exported as a lambda so the screen looks the same).
    /// <para>
    /// The constructor arguments of each kind are written from named properties; the remaining non-default
    /// properties follow in the reader's order. The output for the kinds the original single-class exporter
    /// supported is kept byte for byte (the tooling tests assert literal lines).
    /// </para>
    /// </summary>
    internal sealed class CSharpEmitter
    {
        private const string Todo = "/* TODO */";

        private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
        {
            "api", "menu", "options", "root",
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue",
            "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
            "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected",
            "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
            "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
            "using", "virtual", "void", "volatile", "while"
        };

        /// <summary>Properties each kind writes as constructor arguments (or not at all), so they are skipped in the setter pass.</summary>
        private static readonly Dictionary<string, string[]> Consumed = new(StringComparer.Ordinal)
        {
            [TreeKinds.Stack] = new[] { "Horizontal", "Spacing" },
            [TreeKinds.Grid] = new[] { "Columns", "Rows" },
            [TreeKinds.Panel] = new[] { "DrawBox", "Padding" },
            [TreeKinds.ScrollView] = new[] { "ViewportHeight" },
            [TreeKinds.List] = new[] { "RowHeight", "VisibleRows", "ItemCount" },
            [TreeKinds.DataGrid] = new[] { "RowHeight", "VisibleRows", "RowCount", "SortColumn", "SortDescending" },
            [TreeKinds.Label] = new[] { "Text" },
            [TreeKinds.Image] = new[] { "Texture", "Source", "Scale" },
            [TreeKinds.ItemImage] = new[] { "Item", "Quality", "Count", "Scale" },
            [TreeKinds.Button] = new[] { "Text", "OnClick" },
            [TreeKinds.Checkbox] = new[] { "Value" },
            [TreeKinds.TextInput] = new[] { "Value" },
            [TreeKinds.NumberInput] = new[] { "Value", "Min", "Max", "Step", "Clamp" },
            [TreeKinds.Dropdown] = new[] { "Choices", "Labels", "Value" },
            [TreeKinds.Slider] = new[] { "Value", "Min", "Max" },
            [TreeKinds.Spacer] = new[] { "Width", "Height" }
        };

        private readonly StringBuilder sb = new();
        private readonly HashSet<string> usedNames = new(StringComparer.Ordinal);
        private readonly Dictionary<TreeNode, string> names = new();

        /// <summary>Variables declared inside a build lambda (not reachable from the menu-level statements).</summary>
        private readonly HashSet<TreeNode> scoped = new();

        private readonly TreeMenu menu;
        private int indent;

        private CSharpEmitter(TreeMenu menu)
        {
            this.menu = menu;
            usedNames.UnionWith(Reserved);
        }

        /// <summary>The builder code for <paramref name="menu"/>.</summary>
        internal static string Emit(TreeMenu menu) => new CSharpEmitter(menu).Run();

        private string Run()
        {
            Line($"// Exported by the UI Framework inspector: menu '{menu.Id}' of {menu.OwnerModId}. 'api' is your IStardewUIApi.");
            DescribeMenu();
            DescribeRoot(menu.Root);
            VisitChildren(menu.Root, "root");
            DescribeMenuButtons();
            return sb.ToString();
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Menu
        // ---------------------------------------------------------------------------------------------------------

        private void DescribeMenu()
        {
            Line("IUIMenuOptions options = api.CreateMenuOptions();");
            EmitAll("options", menu.Options);
            Line($"IUIMenu menu = api.CreateMenu({Str(menu.Id)}, options);");
            EmitAll("menu", menu.Events);
        }

        /// <summary>The root stack is created by the menu; only its deviations from the defaults are emitted.</summary>
        private void DescribeRoot(TreeNode root)
        {
            Line("IUIStack root = menu.Root;");
            names[root] = "root";
            EmitAll("root", root.Properties);
        }

        private void DescribeMenuButtons()
        {
            MenuButton("DefaultButton", menu.DefaultButton);
            MenuButton("CancelButton", menu.CancelButton);
        }

        private void MenuButton(string property, TreeNode? button)
        {
            if (button == null || !names.TryGetValue(button, out string? name))
            {
                return;
            }

            Line(scoped.Contains(button)
                ? $"// TODO: menu.{property} = {name}; ({name} is declared inside a build callback)"
                : $"menu.{property} = {name};");
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Visitor
        // ---------------------------------------------------------------------------------------------------------

        private void VisitChildren(TreeNode parent, string parentVar)
        {
            foreach (TreeNode child in parent.Children)
            {
                Visit(child, parentVar);
            }
        }

        private void Visit(TreeNode n, string parentVar)
        {
            string v = NameFor(n);
            if (indent > 0)
            {
                scoped.Add(n);
            }

            if (n.Kind == TreeKinds.Unknown)
            {
                Line($"// TODO: element '{n.Id}' is a {n.ClrType}, which this exporter does not know; its subtree was skipped.");
                return;
            }

            Describe(n, v, parentVar);
            EmitRest(n, v);
            DescribeStructure(n, v);
        }

        /// <summary>Emit the creation line (constructor arguments) for <paramref name="n"/>.</summary>
        private void Describe(TreeNode n, string v, string p)
        {
            string id = Str(n.Id);
            switch (n.Kind)
            {
                case TreeKinds.Stack:
                    Line($"IUIStack {v} = api.AddStack({p}, {id}, {Bool(n.Get<bool>("Horizontal"))}, {n.Get<int>("Spacing")});");
                    break;
                case TreeKinds.Grid:
                    Line($"IUIGrid {v} = api.AddGrid({p}, {id}, {Str(n.Get<string>("Columns"))}, {Str(n.Get<string>("Rows"))});");
                    break;
                case TreeKinds.Panel:
                    Line($"IUIPanel {v} = api.AddPanel({p}, {id}, {Bool(n.Get<bool>("DrawBox"))}, {n.Get<int>("Padding")});");
                    break;
                case TreeKinds.Canvas:
                    Line($"IUICanvas {v} = api.AddCanvas({p}, {id});");
                    break;
                case TreeKinds.ScrollView:
                    Line($"IUIScrollView {v} = api.AddScrollView({p}, {id}, {n.Get<int>("ViewportHeight")});");
                    break;
                case TreeKinds.List:
                    Line($"IUIList {v} = api.AddList({p}, {id}, {n.Get<int>("RowHeight")}, {n.Get<int>("VisibleRows")}, () => {n.Get<int>("ItemCount")} {Todo}, (index, row) => {{ {Todo} }});");
                    break;
                case TreeKinds.Slot:
                    Line($"IUISlot {v} = api.AddSlot({p}, {id});");
                    break;
                case TreeKinds.Composite:
                    DescribeComposite(n, v, p);
                    break;
                case TreeKinds.CustomHost:
                    DescribeCustomHost(n, v, p);
                    break;
                case TreeKinds.DataGrid:
                    DescribeDataGrid(n, v, p);
                    break;
                case TreeKinds.Form:
                    Line($"IUIForm {v} = api.AddForm({p}, {id}, null {TodoComment($"your model (a {n.ModelType})")});");
                    break;
                case TreeKinds.Custom:
                    Line($"IUIElement {v} = api.AddCustom({p}, {id}, null {TodoComment("your IUICustomComponent")});");
                    break;
                default:
                    DescribeLeaf(n, v, p, id);
                    break;
            }
        }

        private void DescribeLeaf(TreeNode n, string v, string p, string id)
        {
            switch (n.Kind)
            {
                case TreeKinds.Label:
                    Line($"IUILabel {v} = api.AddLabel({p}, {id}, () => {Str(n.Get<string>("Text"))} {Todo});");
                    break;
                case TreeKinds.Image:
                    Rectangle? src = n.Get<Rectangle?>("Source");
                    string source = src.HasValue ? RectLiteral(src.Value) : "null";
                    Line($"IUIImage {v} = api.AddImage({p}, {id}, null {TodoComment("texture")}, {source}, {Float(n.Get<float>("Scale"))});");
                    break;
                case TreeKinds.ItemImage:
                    Line($"IUIItemImage {v} = api.AddItemImage({p}, {id}, () => null {TodoComment("item")}, {Float(n.Get<float>("Scale"))});");
                    break;
                case TreeKinds.Button:
                    Line($"IUIButton {v} = api.AddButton({p}, {id}, () => {Str(n.Get<string>("Text"))} {Todo}, e => {{ {Todo} }});");
                    break;
                case TreeKinds.Checkbox:
                    Line($"IUICheckbox {v} = api.AddCheckbox({p}, {id}, () => {Bool(n.Get<bool>("Value"))} {Todo}, value => {{ {Todo} }});");
                    break;
                case TreeKinds.TextInput:
                    Line($"IUITextInput {v} = api.AddTextInput({p}, {id}, () => {Str(n.Get<string>("Value"))} {Todo}, value => {{ {Todo} }});");
                    break;
                case TreeKinds.NumberInput:
                    Line($"IUINumberInput {v} = api.AddNumberInput({p}, {id}, () => {Double(n.Get<double>("Value"))} {Todo}, value => {{ {Todo} }}, {Double(n.Get<double>("Min"))}, {Double(n.Get<double>("Max"))}, {Double(n.Get<double>("Step"))}, {Bool(n.Get<bool>("Clamp"))});");
                    break;
                case TreeKinds.Dropdown:
                    string choices = string.Join(", ", n.Get<string[]>("Choices").Select(Str));
                    string labels = string.Join(", ", n.Get<string[]>("Labels").Select(Str));
                    Line($"IUIDropdown {v} = api.AddDropdown({p}, {id}, () => new[] {{ {choices} }} {Todo}, () => new[] {{ {labels} }} {Todo}, () => {Str(n.Get<string>("Value"))} {Todo}, value => {{ {Todo} }});");
                    break;
                case TreeKinds.Slider:
                    Line($"IUISlider {v} = api.AddSlider({p}, {id}, () => {Double(n.Get<double>("Value"))} {Todo}, value => {{ {Todo} }}, {Double(n.Get<double>("Min"))}, {Double(n.Get<double>("Max"))});");
                    break;
                case TreeKinds.Spacer:
                    Line($"IUISpacer {v} = api.AddSpacer({p}, {id}, {n.Get<int?>("Width") ?? 0}, {n.Get<int?>("Height") ?? 0});");
                    break;
                default:
                    Line($"// TODO: element '{n.Id}' is a {n.ClrType}, which this exporter does not know.");
                    break;
            }
        }

        private void DescribeComposite(TreeNode n, string v, string p)
        {
            string args = "api.CreateCompositeArgs()";
            if (n.Args.Count > 0)
            {
                args = Unique(v + "Args");
                Line($"IUICompositeArgs {args} = api.CreateCompositeArgs();");
                foreach (TreeProperty a in n.Args)
                {
                    string setter = a.Origin == TreeValueOrigin.Literal ? LiteralSetter(a.Kind) : a.Note ?? "SetObject";
                    switch (a.Origin)
                    {
                        case TreeValueOrigin.Literal:
                            Line($"{args}.{setter}({Str(a.Name)}, {Literal(a.Kind, a.Value)});");
                            break;
                        case TreeValueOrigin.Delegate:
                            Line($"{args}.{setter}({Str(a.Name)}, () => {Literal(a.Kind, a.Value)} {Todo});");
                            break;
                        default:
                            Line($"// TODO: {args}.{setter}({Str(a.Name)}, ...); (was set)");
                            break;
                    }
                }
            }

            Line($"IUIComposite {v} = api.AddComposite({p}, {Str(n.Id)}, {Str(n.CompositeName)}, {args});");
        }

        private static string LiteralSetter(TreeValueKind kind)
        {
            return kind switch
            {
                TreeValueKind.Bool => "SetBool",
                TreeValueKind.Double => "SetNumber",
                _ => "SetString"
            };
        }

        /// <summary>A custom component with a host: its host children are written inside the build lambda.</summary>
        private void DescribeCustomHost(TreeNode n, string v, string p)
        {
            string host = Unique(v + "Host");
            Line($"IUIElement {v} = api.AddCustom({p}, {Str(n.Id)}, null {TodoComment("your IUICustomComponent")}, {host} =>");
            Line("{");
            indent++;
            VisitChildren(n, host);
            indent--;
            Line("});");
        }

        private void DescribeDataGrid(TreeNode n, string v, string p)
        {
            Line($"IUIDataGrid {v} = api.AddDataGrid({p}, {Str(n.Id)}, {n.Get<int>("RowHeight")}, {n.Get<int>("VisibleRows")}, () => {n.Get<int>("RowCount")} {Todo});");
            foreach (TreeColumn c in n.Columns)
            {
                string cv = Unique(v + Pascal(c.Id));
                string width = c.Properties[0].Value as string ?? "*";
                Line($"IUIDataGridColumn {cv} = {v}.AddColumn({Str(c.Id)}, () => {Str(c.Header)} {Todo}, {Str(width)});");
                foreach (TreeProperty cp in c.Properties.Skip(1))
                {
                    EmitProperty(cv, cp);
                }
            }

            TreeProperty? sort = n.Find("SortColumn");
            if (sort is { IsDefault: false })
            {
                Line($"{v}.Sort({Str(sort.Value as string)}, {Bool(n.Get<bool>("SortDescending"))});");
            }
        }

        /// <summary>The non-default properties that are not constructor arguments, in the reader's order.</summary>
        private void EmitRest(TreeNode n, string v)
        {
            Consumed.TryGetValue(n.Kind, out string[]? consumed);
            foreach (TreeProperty p in n.Properties)
            {
                if (consumed == null || Array.IndexOf(consumed, p.Name) < 0)
                {
                    EmitProperty(v, p);
                }
            }
        }

        /// <summary>Children, or a comment saying why they are not exported.</summary>
        private void DescribeStructure(TreeNode n, string v)
        {
            if (n.Kind == TreeKinds.List)
            {
                Line($"// rows of {v} are built by its buildRow delegate");
                return;
            }

            if (n.ChildrenNote != null)
            {
                Line($"// children of {v} are {n.ChildrenNote}");
                return;
            }

            if (n.Kind == TreeKinds.CustomHost)
            {
                return; // written inside the build lambda
            }

            if (n.ExcludedContributors.Count > 0)
            {
                Line($"// {v}: {n.ExcludedContributors.Count} contribution(s) from other mods ({string.Join(", ", n.ExcludedContributors)}) are not exported; they are rebuilt by their ContributeTo when the menu opens.");
            }

            VisitChildren(n, v);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Properties
        // ---------------------------------------------------------------------------------------------------------

        private void EmitAll(string v, IEnumerable<TreeProperty> properties)
        {
            foreach (TreeProperty p in properties)
            {
                EmitProperty(v, p);
            }
        }

        /// <summary>One non-default property as a C# statement (or a TODO comment).</summary>
        private void EmitProperty(string v, TreeProperty p)
        {
            if (p.IsDefault)
            {
                return;
            }

            switch (p.Origin)
            {
                case TreeValueOrigin.Opaque:
                    Line($"// TODO: {v}.{p.Name} = ...; (was set)");
                    return;
                case TreeValueOrigin.Delegate:
                    Line($"{v}.{p.Name} = () => {Literal(p.Kind, p.Value)}; {Todo}");
                    return;
            }

            switch (p.Kind)
            {
                case TreeValueKind.Margin:
                    EmitMargin(v, (int[])p.Value!);
                    break;
                case TreeValueKind.Style:
                    EmitStyle(v, (TreeStyle)p.Value!);
                    break;
                case TreeValueKind.Tooltip:
                    EmitTooltip(v, (TreeTooltip)p.Value!);
                    break;
                case TreeValueKind.Icon:
                    var icon = (TreeIcon)p.Value!;
                    Line($"// TODO: {v}.Icon = <texture>; {v}.IconSource = {(icon.Source.HasValue ? RectLiteral(icon.Source.Value) : "null")}; {v}.IconScale = {Float(icon.Scale)};");
                    break;
                default:
                    Line($"{v}.{p.Name} = {Literal(p.Kind, p.Value)};");
                    break;
            }
        }

        private void EmitMargin(string v, int[] m)
        {
            int l = m[0], t = m[1], r = m[2], b = m[3];
            if (l == t && t == r && r == b)
            {
                Line($"{v}.SetMargin({l});");
            }
            else if (l == r && t == b)
            {
                Line($"{v}.SetMargin({l}, {t});");
            }
            else
            {
                Line($"{v}.SetMargin({l}, {t}, {r}, {b});");
            }
        }

        private void EmitStyle(string v, TreeStyle style)
        {
            string sv = v + "Style";
            Line($"IUIStyle {sv} = api.CreateStyle();");
            EmitAll(sv, style.Properties);
            Line($"{v}.Style = {sv};");
        }

        private void EmitTooltip(string v, TreeTooltip tooltip)
        {
            string tv = Unique(v + "Tooltip");
            Line($"IUITooltip {tv} = api.CreateTooltip();");
            foreach (TreeTooltipBlock b in tooltip.Blocks)
            {
                switch (b.Kind)
                {
                    case TooltipBlockKind.Title:
                        Line($"{tv}.Title(() => {Str(b.Text)} {Todo});");
                        break;
                    case TooltipBlockKind.Line:
                        Line(b.Color.HasValue
                            ? $"{tv}.Line(() => {Str(b.Text)} {Todo}, {ColorLiteral(b.Color.Value)});"
                            : $"{tv}.Line(() => {Str(b.Text)} {Todo});");
                        break;
                    case TooltipBlockKind.Icon:
                        Line($"// TODO: {tv}.Icon(<texture>, {(b.Source.HasValue ? RectLiteral(b.Source.Value) : "null")}, {Float(b.Scale)});");
                        break;
                    case TooltipBlockKind.Item:
                        Line($"{tv}.Item({Str(b.ItemId)});");
                        break;
                    case TooltipBlockKind.ItemInstance:
                        Line($"// TODO: {tv}.ItemInstance(() => ...); (was {b.ItemPreview ?? "set"})");
                        break;
                    case TooltipBlockKind.Divider:
                        Line($"{tv}.Divider();");
                        break;
                    case TooltipBlockKind.Money:
                        Line($"{tv}.Money(() => {b.Amount.ToString(CultureInfo.InvariantCulture)} {Todo});");
                        break;
                }

                if (b.HasWhen || b.HasColorFunc)
                {
                    Line($"// TODO: the block above had {(b.HasWhen ? "a visibility condition" : string.Empty)}{(b.HasWhen && b.HasColorFunc ? " and " : string.Empty)}{(b.HasColorFunc ? "a dynamic color" : string.Empty)} (data-built tooltips only; no public API)");
                }
            }

            if (tooltip.MaxWidth > 0)
            {
                Line($"{tv}.MaxWidth({tooltip.MaxWidth.ToString(CultureInfo.InvariantCulture)});");
            }

            Line($"{v}.RichTooltip = {tv};");
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------------------------------------------------

        private void Line(string text)
        {
            if (indent > 0)
            {
                sb.Append(' ', indent * 4);
            }

            sb.Append(text).Append('\n');
        }

        private static string Literal(TreeValueKind kind, object? value)
        {
            return kind switch
            {
                TreeValueKind.Bool => Bool((bool)value!),
                TreeValueKind.String => Str(value as string),
                TreeValueKind.Float => Float((float)value!),
                TreeValueKind.Double => Double((double)value!),
                TreeValueKind.Enum => value!.GetType().Name + "." + value,
                TreeValueKind.Color => ColorLiteral((Color)value!),
                TreeValueKind.Rect => RectLiteral((Rectangle)value!),
                TreeValueKind.StringList => "new[] { " + string.Join(", ", ((string[])value!).Select(Str)) + " }",
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            };
        }

        private static string Bool(bool value) => value ? "true" : "false";

        private static string TodoComment(string what) => $"/* TODO: {what} */";

        private static string Float(float value) => value.ToString("0.0###", CultureInfo.InvariantCulture) + "f";

        private static string Double(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string ColorLiteral(Color c) => $"new Color({c.R}, {c.G}, {c.B}, {c.A})";

        private static string RectLiteral(Rectangle r) => $"new Rectangle({r.X}, {r.Y}, {r.Width}, {r.Height})";

        /// <summary>A C# string literal for <paramref name="value"/>.</summary>
        internal static string Str(string? value)
        {
            var result = new StringBuilder("\"");
            foreach (char c in value ?? string.Empty)
            {
                result.Append(c switch
                {
                    '\\' => "\\\\",
                    '"' => "\\\"",
                    '\n' => "\\n",
                    '\r' => "\\r",
                    '\t' => "\\t",
                    _ => c < ' ' ? "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture) : c.ToString()
                });
            }
            return result.Append('"').ToString();
        }

        /// <summary>A unique C# identifier derived from the element's id.</summary>
        private string NameFor(TreeNode n)
        {
            string camel = Camel(n.Id);
            string baseName = camel.Length == 0 || char.IsDigit(camel[0]) ? n.ClrType.ToLowerInvariant() + camel : camel;
            baseName = char.ToLowerInvariant(baseName[0]) + baseName[1..];
            string candidate = Unique(baseName);
            names[n] = candidate;
            return candidate;
        }

        /// <summary><paramref name="baseName"/>, or it with the first free number appended; reserves the result.</summary>
        private string Unique(string baseName)
        {
            string candidate = baseName;
            for (int i = 2; !usedNames.Add(candidate); i++)
            {
                candidate = baseName + i.ToString(CultureInfo.InvariantCulture);
            }

            return candidate;
        }

        /// <summary>Letters and digits of <paramref name="raw"/>, each run after a separator starting upper-case.</summary>
        private static string Camel(string raw)
        {
            var name = new StringBuilder();
            bool upper = false;
            foreach (char c in raw)
            {
                if (char.IsLetterOrDigit(c))
                {
                    name.Append(upper ? char.ToUpperInvariant(c) : c);
                    upper = false;
                }
                else
                {
                    upper = name.Length > 0;
                }
            }

            return name.ToString();
        }

        /// <summary><see cref="Camel"/> with an upper-case first letter (a suffix for a derived variable name).</summary>
        private static string Pascal(string raw)
        {
            string camel = Camel(raw);
            return camel.Length == 0 ? "Column" : char.ToUpperInvariant(camel[0]) + camel[1..];
        }
    }
}
