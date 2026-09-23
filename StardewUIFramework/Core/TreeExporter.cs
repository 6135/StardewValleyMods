using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Xna.Framework;
using UIFramework.Api;
using UIFramework.Components;
using UIFramework.Rendering;

namespace UIFramework.Core
{
    /// <summary>
    /// Writes the C# builder code that would recreate a menu tree through the public API (<c>api.CreateMenu</c>,
    /// <c>api.AddStack(...)</c> …): one <c>Add*</c> call per element with the correct nesting and ids, an assignment
    /// for every non-default property, and a <c>// TODO</c> comment wherever a delegate or texture cannot be
    /// serialized (the current value of text delegates is exported as a lambda so the screen looks the same).
    /// A visitor over <see cref="UIElement"/> with one <c>Describe</c> per component type.
    /// </summary>
    internal sealed class TreeExporter
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

        private readonly StringBuilder sb = new();
        private readonly HashSet<string> usedNames = new(StringComparer.Ordinal);
        private readonly Dictionary<UIElement, string> names = new();
        private readonly UIMenu menu;

        private TreeExporter(UIMenu menu)
        {
            this.menu = menu;
            usedNames.UnionWith(Reserved);
        }

        /// <summary>The builder code for <paramref name="menu"/>.</summary>
        internal static string Export(UIMenu menu) => new TreeExporter(menu).Run();

        private string Run()
        {
            Line($"// Exported by the UI Framework inspector: menu '{menu.Id}' of {menu.Consumer.ModId}. 'api' is your IStardewUIApi.");
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
            if (menu.TitleFunc != null)
            {
                Line($"options.Title = () => {Str(Eval(menu.Id, "Export.Title", menu.TitleFunc))}; {Todo}");
            }

            SetIf("options", "Width", menu.Width);
            SetIf("options", "Height", menu.Height);
            SetIf("options", "ShowCloseButton", menu.ShowCloseButton, true);
            SetIf("options", "Modal", menu.Modal, true);
            SetIf("options", "DimBackground", menu.DimBackground, true);
            if (menu.Anchor != UIAnchor.Center)
            {
                Line($"options.Anchor = UIAnchor.{menu.Anchor};");
            }

            SetIf("options", "X", menu.X, 0);
            SetIf("options", "Y", menu.Y, 0);
            SetIf("options", "DrawBox", menu.DrawBox, true);
            SetIf("options", "Padding", menu.Padding, 0);
            SetIf("options", "CloseOnEscape", menu.CloseOnEscape, true);
            Line($"IUIMenu menu = api.CreateMenu({Str(menu.Id)}, options);");
            TodoIf(menu.OnOpen, "menu.OnOpen");
            TodoIf(menu.OnClose, "menu.OnClose");
            TodoIf(menu.OnUpdate, "menu.OnUpdate");
            TodoIf(menu.OnKey, "menu.OnKey");
            TodoIf(menu.OnScroll, "menu.OnScroll");
        }

        /// <summary>The root stack is created by the menu; only its deviations from the defaults are emitted.</summary>
        private void DescribeRoot(Stack root)
        {
            Line("IUIStack root = menu.Root;");
            names[root] = "root";
            SetIf("root", "Horizontal", root.Horizontal, false);
            SetIf("root", "Spacing", root.Spacing, 8);
            if (root.Alignment != UIAlign.Start)
            {
                Line($"root.Alignment = UIAlign.{root.Alignment};");
            }

            DescribeMargins("root", root);
            SetIf("root", "Width", root.Width);
            SetIf("root", "Height", root.Height);
        }

        private void DescribeMenuButtons()
        {
            if (menu.DefaultButtonElement != null && names.TryGetValue(menu.DefaultButtonElement, out string? def))
            {
                Line($"menu.DefaultButton = {def};");
            }

            if (menu.CancelButtonElement != null && names.TryGetValue(menu.CancelButtonElement, out string? cancel))
            {
                Line($"menu.CancelButton = {cancel};");
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Visitor
        // ---------------------------------------------------------------------------------------------------------

        private void VisitChildren(UIContainer parent, string parentVar)
        {
            foreach (UIElement child in parent.Children)
            {
                Visit(child, parentVar);
            }
        }

        private void Visit(UIElement e, string parentVar)
        {
            string v = NameFor(e);
            if (!Describe(e, v, parentVar))
            {
                Line($"// TODO: element '{e.Id}' is a {e.GetType().Name}, which this exporter does not know; its subtree was skipped.");
                return;
            }

            DescribeCommon(v, e);
            if (e is ListView)
            {
                Line($"// rows of {v} are built by its buildRow delegate");
            }
            else if (e is UIContainer container)
            {
                VisitChildren(container, v);
            }
            else
            {
                // leaf
            }
        }

        /// <summary>Emit the creation line (and type-specific properties) for <paramref name="e"/>. False for unknown types.</summary>
        private bool Describe(UIElement e, string v, string p)
        {
            switch (e)
            {
                case Stack stack:
                    DescribeStack(stack, v, p);
                    return true;
                case Grid grid:
                    DescribeGrid(grid, v, p);
                    return true;
                case Panel panel:
                    Line($"IUIPanel {v} = api.AddPanel({p}, {Str(e.Id)}, {Bool(panel.DrawBox)}, {panel.Padding});");
                    return true;
                case Canvas:
                    Line($"IUICanvas {v} = api.AddCanvas({p}, {Str(e.Id)});");
                    return true;
                case ScrollView scroll:
                    DescribeScrollView(scroll, v, p);
                    return true;
                case ListView list:
                    DescribeList(list, v, p);
                    return true;
                default:
                    return DescribeLeaf(e, v, p);
            }
        }

        private bool DescribeLeaf(UIElement e, string v, string p)
        {
            switch (e)
            {
                case Label label:
                    DescribeLabel(label, v, p);
                    return true;
                case Image image:
                    DescribeImage(image, v, p);
                    return true;
                case ItemImage itemImage:
                    DescribeItemImage(itemImage, v, p);
                    return true;
                case Button button:
                    DescribeButton(button, v, p);
                    return true;
                case Checkbox checkbox:
                    DescribeCheckbox(checkbox, v, p);
                    return true;
                case TextInput text:
                    DescribeTextInput(text, v, p);
                    return true;
                case NumberInput number:
                    DescribeNumberInput(number, v, p);
                    return true;
                case Dropdown dropdown:
                    DescribeDropdown(dropdown, v, p);
                    return true;
                case Slider slider:
                    DescribeSlider(slider, v, p);
                    return true;
                case Spacer spacer:
                    DescribeSpacer(spacer, v, p);
                    return true;
                case CustomElementAdapter:
                    Line($"IUIElement {v} = api.AddCustom({p}, {Str(e.Id)}, null {TodoComment("your IUICustomComponent")});");
                    return true;
                default:
                    return false;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Containers
        // ---------------------------------------------------------------------------------------------------------

        private void DescribeStack(Stack s, string v, string p)
        {
            Line($"IUIStack {v} = api.AddStack({p}, {Str(s.Id)}, {Bool(s.Horizontal)}, {s.Spacing});");
            if (s.Alignment != UIAlign.Start)
            {
                Line($"{v}.Alignment = UIAlign.{s.Alignment};");
            }
        }

        private void DescribeGrid(Grid g, string v, string p)
        {
            Line($"IUIGrid {v} = api.AddGrid({p}, {Str(g.Id)}, {Str(g.Columns)}, {Str(g.Rows)});");
            SetIf(v, "ColumnSpacing", g.ColumnSpacing, 0);
            SetIf(v, "RowSpacing", g.RowSpacing, 0);
        }

        private void DescribeScrollView(ScrollView s, string v, string p)
        {
            Line($"IUIScrollView {v} = api.AddScrollView({p}, {Str(s.Id)}, {s.ViewportHeight});");
            SetIf(v, "ScrollStep", s.ScrollStep, 64);
            SetIf(v, "ShowScrollbar", s.ShowScrollbar, true);
            TodoIf(s.OnScroll, v + ".OnScroll");
        }

        private void DescribeList(ListView l, string v, string p)
        {
            Line($"IUIList {v} = api.AddList({p}, {Str(l.Id)}, {l.RowHeight}, {l.VisibleRows}, () => {l.ItemCount} {Todo}, (index, row) => {{ {Todo} }});");
            SetIf(v, "Selectable", l.Selectable, false);
            TodoIf(l.OnValueChanged, v + ".OnValueChanged");
            TodoIf(l.OnScroll, v + ".OnScroll");
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Leaves
        // ---------------------------------------------------------------------------------------------------------

        private void DescribeLabel(Label l, string v, string p)
        {
            Line($"IUILabel {v} = api.AddLabel({p}, {Str(l.Id)}, () => {Str(Eval(l.Id, "Export.Text", l.TextFunc))} {Todo});");
            if (l.Font != UIFont.Small)
            {
                Line($"{v}.Font = UIFont.{l.Font};");
            }

            if (l.Color.HasValue)
            {
                Line($"{v}.Color = {ColorLiteral(l.Color.Value)};");
            }

            SetIf(v, "Shadow", l.Shadow, false);
            SetIf(v, "Wrap", l.Wrap, false);
            if (l.TextAlign != UIAlign.Start)
            {
                Line($"{v}.TextAlign = UIAlign.{l.TextAlign};");
            }

            if (!Numbers.Same(l.Scale, 1f))
            {
                Line($"{v}.Scale = {Float(l.Scale)};");
            }
        }

        private void DescribeImage(Image i, string v, string p)
        {
            string source = i.Source.HasValue ? RectLiteral(i.Source.Value) : "null";
            Line($"IUIImage {v} = api.AddImage({p}, {Str(i.Id)}, null {TodoComment("texture")}, {source}, {Float(i.Scale)});");
            if (i.Tint != Color.White)
            {
                Line($"{v}.Tint = {ColorLiteral(i.Tint)};");
            }
        }

        private void DescribeItemImage(ItemImage i, string v, string p)
        {
            Line($"IUIItemImage {v} = api.AddItemImage({p}, {Str(i.Id)}, () => null {TodoComment("item")}, {Float(i.Scale)});");
            if (i.Stack != UIItemStack.Hide)
            {
                Line($"{v}.Stack = UIItemStack.{i.Stack};");
            }

            if (i.DrawShadow)
            {
                Line($"{v}.DrawShadow = true;");
            }

            if (i.Alpha != 1f)
            {
                Line($"{v}.Alpha = {Float(i.Alpha)};");
            }

            if (i.Tint != Color.White)
            {
                Line($"{v}.Tint = {ColorLiteral(i.Tint)};");
            }
        }

        private void DescribeButton(Button b, string v, string p)
        {
            Line($"IUIButton {v} = api.AddButton({p}, {Str(b.Id)}, () => {Str(Eval(b.Id, "Export.Text", b.TextFunc))} {Todo}, e => {{ {Todo} }});");
            if (b.Font != UIFont.Small)
            {
                Line($"{v}.Font = UIFont.{b.Font};");
            }

            if (b.Icon != null)
            {
                Line($"// TODO: {v}.Icon = <texture>; {v}.IconSource = {(b.IconSource.HasValue ? RectLiteral(b.IconSource.Value) : "null")}; {v}.IconScale = {Float(b.IconScale)};");
            }

            SetIf(v, "DrawBox", b.DrawBox, true);
            SoundIf(v, "ClickSound", b.ClickSound);
            SoundIf(v, "HoverSound", b.HoverSound);
        }

        private void DescribeCheckbox(Checkbox c, string v, string p)
        {
            Line($"IUICheckbox {v} = api.AddCheckbox({p}, {Str(c.Id)}, () => {Bool(c.Value)} {Todo}, value => {{ {Todo} }});");
            if (c.LabelFunc != null)
            {
                Line($"{v}.Label = () => {Str(Eval(c.Id, "Export.Label", c.LabelFunc))}; {Todo}");
            }

            SoundIf(v, "ClickSound", c.ClickSound);
            TodoIf(c.OnValueChanged, v + ".OnValueChanged");
        }

        private void DescribeTextInput(TextInput t, string v, string p)
        {
            Line($"IUITextInput {v} = api.AddTextInput({p}, {Str(t.Id)}, () => {Str(t.Value)} {Todo}, value => {{ {Todo} }});");
            if (t.PlaceholderFunc != null)
            {
                Line($"{v}.Placeholder = () => {Str(Eval(t.Id, "Export.Placeholder", t.PlaceholderFunc))}; {Todo}");
            }

            SetIf(v, "MaxLength", t.MaxLength, 0);
            TodoIf(t.ValidateFunc, v + ".Validate");
            TodoIf(t.Texture, v + ".Texture");
            TodoIf(t.OnValueChanged, v + ".OnValueChanged");
            TodoIf(t.OnSubmit, v + ".OnSubmit");
        }

        private void DescribeNumberInput(NumberInput n, string v, string p)
        {
            Line($"IUINumberInput {v} = api.AddNumberInput({p}, {Str(n.Id)}, () => {Double(n.Value)} {Todo}, value => {{ {Todo} }}, {Double(n.Min)}, {Double(n.Max)}, {Double(n.Step)}, {Bool(n.Clamp)});");
            SetIf(v, "Decimals", n.Decimals, 0);
            TodoIf(n.ValidateFunc, v + ".Validate");
            TodoIf(n.Texture, v + ".Texture");
            TodoIf(n.OnValueChanged, v + ".OnValueChanged");
            TodoIf(n.OnSubmit, v + ".OnSubmit");
        }

        private void DescribeDropdown(Dropdown d, string v, string p)
        {
            var choices = new List<string>();
            var labels = new List<string>();
            for (int i = 0; i < d.ChoiceCount; i++)
            {
                choices.Add(Str(d.GetChoice(i)));
                labels.Add(Str(d.GetLabel(i)));
            }

            Line($"IUIDropdown {v} = api.AddDropdown({p}, {Str(d.Id)}, () => new[] {{ {string.Join(", ", choices)} }} {Todo}, () => new[] {{ {string.Join(", ", labels)} }} {Todo}, () => {Str(d.SelectedValue)} {Todo}, value => {{ {Todo} }});");
            SetIf(v, "MaxVisible", d.MaxVisible, 5);
            TodoIf(d.OnValueChanged, v + ".OnValueChanged");
            TodoIf(d.OnScroll, v + ".OnScroll");
        }

        private void DescribeSlider(Slider s, string v, string p)
        {
            Line($"IUISlider {v} = api.AddSlider({p}, {Str(s.Id)}, () => {Double(s.Value)} {Todo}, value => {{ {Todo} }}, {Double(s.Min)}, {Double(s.Max)});");
            if (s.Step > 0)
            {
                Line($"{v}.Step = {Double(s.Step)};");
            }

            TodoIf(s.OnValueChanged, v + ".OnValueChanged");
        }

        private void DescribeSpacer(Spacer s, string v, string p)
        {
            Line($"IUISpacer {v} = api.AddSpacer({p}, {Str(s.Id)}, {s.Width ?? 0}, {s.Height ?? 0});");
            SetIf(v, "Line", s.Line, false);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Common properties
        // ---------------------------------------------------------------------------------------------------------

        private void DescribeCommon(string v, UIElement e)
        {
            SetIf(v, "Visible", e.Visible, true);
            SetIf(v, "Enabled", e.Enabled, true);
            DescribeMargins(v, e);
            if (e is not Spacer)
            {
                SetIf(v, "Width", e.Width);
                SetIf(v, "Height", e.Height);
            }

            if (e.HorizontalAlignSet)
            {
                Line($"{v}.HorizontalAlign = UIAlign.{e.HorizontalAlign};");
            }

            if (e.VerticalAlignSet)
            {
                Line($"{v}.VerticalAlign = UIAlign.{e.VerticalAlign};");
            }

            DescribePlacement(v, e);
            DescribeStyle(v, e);
            DescribeCallbacks(v, e);
        }

        private void DescribeMargins(string v, UIElement e)
        {
            int l = e.MarginLeft, t = e.MarginTop, r = e.MarginRight, b = e.MarginBottom;
            if (l == 0 && t == 0 && r == 0 && b == 0)
            {
                return;
            }

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

        /// <summary>Canvas offsets and grid cells, only where the parent uses them.</summary>
        private void DescribePlacement(string v, UIElement e)
        {
            if (e.ParentElement is Canvas)
            {
                SetIf(v, "X", e.X, 0);
                SetIf(v, "Y", e.Y, 0);
            }

            if (e.ParentElement is Grid)
            {
                SetIf(v, "Row", e.Row, 0);
                SetIf(v, "Column", e.Column, 0);
                SetIf(v, "RowSpan", e.RowSpan, 1);
                SetIf(v, "ColumnSpan", e.ColumnSpan, 1);
            }
        }

        private void DescribeStyle(string v, UIElement e)
        {
            UIStyle? s = e.StyleObject;
            if (s == null)
            {
                return;
            }

            string sv = v + "Style";
            Line($"IUIStyle {sv} = api.CreateStyle();");
            if (s.Font.HasValue)
            {
                Line($"{sv}.Font = UIFont.{s.Font.Value};");
            }

            if (s.TextColor.HasValue)
            {
                Line($"{sv}.TextColor = {ColorLiteral(s.TextColor.Value)};");
            }

            if (s.HoverColor.HasValue)
            {
                Line($"{sv}.HoverColor = {ColorLiteral(s.HoverColor.Value)};");
            }

            TodoIf(s.BoxTexture, sv + ".BoxTexture");
            if (s.BoxSource.HasValue)
            {
                Line($"{sv}.BoxSource = {RectLiteral(s.BoxSource.Value)};");
            }

            if (s.BoxScale.HasValue)
            {
                Line($"{sv}.BoxScale = {Float(s.BoxScale.Value)};");
            }

            SetIf(sv, "Padding", s.Padding);
            if (s.TextShadow.HasValue)
            {
                Line($"{sv}.TextShadow = {Bool(s.TextShadow.Value)};");
            }

            SoundIf(sv, "ClickSound", s.ClickSound);
            SoundIf(sv, "HoverSound", s.HoverSound);
            Line($"{v}.Style = {sv};");
        }

        private void DescribeCallbacks(string v, UIElement e)
        {
            if (e.Tooltip != null)
            {
                Line($"{v}.Tooltip = () => {Str(Eval(e.Id, "Export.Tooltip", e.Tooltip))}; {Todo}");
            }

            if (e.TooltipTitle != null)
            {
                Line($"{v}.TooltipTitle = () => {Str(Eval(e.Id, "Export.TooltipTitle", e.TooltipTitle))}; {Todo}");
            }

            if (e is not Button)
            {
                TodoIf(e.OnClick, v + ".OnClick");
            }

            TodoIf(e.OnRightClick, v + ".OnRightClick");
            TodoIf(e.OnHover, v + ".OnHover");
            TodoIf(e.OnHoverEnd, v + ".OnHoverEnd");
            TodoIf(e.OnFocus, v + ".OnFocus");
            TodoIf(e.OnBlur, v + ".OnBlur");
            TodoIf(e.OnKey, v + ".OnKey");
            TodoIf(e.OnDrawExtra, v + ".OnDrawExtra");
            TodoIf(e.OnDrawOverlay, v + ".OnDrawOverlay");
            TodoIf(e.Tag, v + ".Tag");
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------------------------------------------------

        private void Line(string text) => sb.Append(text).Append('\n');

        private void SetIf<T>(string v, string property, T value, T defaultValue) where T : IEquatable<T>
        {
            if (!value.Equals(defaultValue))
            {
                Line($"{v}.{property} = {Literal(value)};");
            }
        }

        private void SetIf(string v, string property, int? value)
        {
            if (value.HasValue)
            {
                Line($"{v}.{property} = {value.Value.ToString(CultureInfo.InvariantCulture)};");
            }
        }

        private void SoundIf(string v, string property, string? cue)
        {
            if (cue != null)
            {
                Line($"{v}.{property} = {Str(cue)};");
            }
        }

        /// <summary>Emit a TODO comment for a member that cannot be serialized (delegate, texture, tag) when it is set.</summary>
        private void TodoIf(object? value, string member)
        {
            if (value != null)
            {
                Line($"// TODO: {member} = ...; (was set)");
            }
        }

        /// <summary>The current value of a consumer text delegate, through the callback guard.</summary>
        private string Eval(string elementId, string eventName, Func<string>? func)
        {
            return menu.Consumer.Invoke(elementId, eventName, func, string.Empty) ?? string.Empty;
        }

        private static string Literal<T>(T value)
        {
            return value switch
            {
                bool b => Bool(b),
                string s => Str(s),
                float f => Float(f),
                double d => Double(d),
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
        private string NameFor(UIElement e)
        {
            var name = new StringBuilder();
            bool upper = false;
            foreach (char c in e.Id)
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

            string baseName = name.Length == 0 || char.IsDigit(name[0]) ? e.GetType().Name.ToLowerInvariant() + name : name.ToString();
            baseName = char.ToLowerInvariant(baseName[0]) + baseName[1..];
            string candidate = baseName;
            for (int i = 2; !usedNames.Add(candidate); i++)
            {
                candidate = baseName + i.ToString(CultureInfo.InvariantCulture);
            }

            names[e] = candidate;
            return candidate;
        }
    }
}
