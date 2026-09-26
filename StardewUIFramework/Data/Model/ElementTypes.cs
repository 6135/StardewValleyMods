using System;
using System.Collections.Generic;
using System.Linq;

namespace UIFramework.Data.Model
{
    /// <summary>
    /// The element types a data tree can use and which <see cref="ElementDefinition"/> members each of them reads.
    /// Shared by the validator (unused-field warnings), the builder and the schema writer.
    /// </summary>
    internal static class ElementTypes
    {
        internal const string Stack = "Stack";
        internal const string Grid = "Grid";
        internal const string Panel = "Panel";
        internal const string Canvas = "Canvas";
        internal const string ScrollView = "ScrollView";
        internal const string Slot = "Slot";
        internal const string Spacer = "Spacer";
        internal const string Label = "Label";
        internal const string Button = "Button";
        internal const string Image = "Image";
        internal const string ItemImage = "ItemImage";
        internal const string Checkbox = "Checkbox";
        internal const string TextInput = "TextInput";
        internal const string NumberInput = "NumberInput";
        internal const string Dropdown = "Dropdown";
        internal const string Slider = "Slider";
        internal const string Switch = "Switch";
        internal const string Repeat = "Repeat";
        internal const string List = "List";
        internal const string DataGrid = "DataGrid";
        internal const string Form = "Form";
        internal const string Composite = "Composite";
        internal const string Template = "Template";
        internal const string Outlet = "Outlet";

        /// <summary>Members every element reads.</summary>
        internal static readonly string[] Common =
        {
            "Type", "Id", "Condition", "Visible", "Enabled", "Tooltip", "TooltipTitle", "Tag", "Sealed", "AccessibleName",
            "Margin", "MarginLeft", "MarginTop", "MarginRight", "MarginBottom", "Width", "Height", "MinWidth", "MaxWidth", "HorizontalAlign", "VerticalAlign",
            "X", "Y", "Row", "Column", "RowSpan", "ColumnSpan", "Cell", "Span", "Style",
            "OnClick", "OnRightClick", "OnHover", "OnHoverEnd", "OnFocus", "OnBlur",
            "If", "Case", "With", "Out", "Class", "Keys", "RichTooltip", "DrawExtra", "DrawOverlay", "Outlet"
        };

        /// <summary>Type-specific members, by canonical type name.</summary>
        private static readonly Dictionary<string, string[]> Specific = new(StringComparer.OrdinalIgnoreCase)
        {
            [Stack] = new[] { "Children", "Horizontal", "Spacing", "Alignment", "Wrap" },
            [Grid] = new[] { "Children", "Columns", "Rows", "ColumnSpacing", "RowSpacing" },
            [Panel] = new[] { "Children", "DrawBox", "Padding" },
            [Canvas] = new[] { "Children" },
            [ScrollView] = new[] { "Children", "ViewportHeight", "ScrollStep", "ShowScrollbar", "OnScroll" },
            [Slot] = new[] { "Horizontal", "MaxHeight", "MaxContributions", "Wrap" },
            [Spacer] = new[] { "Line" },
            [Label] = new[] { "Text", "Label", "Font", "Color", "Shadow", "Wrap", "TextAlign", "Scale", "RichText", "OnLink", "Shrink" },
            [Button] = new[] { "Text", "Button", "Font", "Icon", "IconScale", "ClickSound", "HoverSound", "DrawBox", "RichText", "Shrink" },
            [Image] = new[] { "Sprite", "Image", "Source", "Scale", "Tint" },
            [ItemImage] = new[] { "Item", "Quality", "Count", "Scale", "Stack", "DrawShadow", "Alpha", "Tint" },
            [Checkbox] = new[] { "Label", "Checkbox", "Text", "Value", "Bind", "ClickSound", "OnValueChanged", "Shrink" },
            [TextInput] = new[] { "Value", "Bind", "Placeholder", "MaxLength", "Texture", "Validate", "OnInvalid", "OnValueChanged", "OnSubmit" },
            [NumberInput] = new[] { "Value", "Bind", "Min", "Max", "Step", "Clamp", "Decimals", "Texture", "Validate", "OnInvalid", "OnValueChanged", "OnSubmit" },
            [Dropdown] = new[] { "Value", "Bind", "Choices", "Labels", "MaxVisible", "ChoicesSource", "ChoiceValue", "ChoiceLabel", "OnValueChanged", "OnScroll", "Shrink" },
            [Slider] = new[] { "Value", "Bind", "Min", "Max", "Step", "OnValueChanged" },
            [Switch] = new[] { "Children", "Switch" },
            [Repeat] = new[] { "Children", "Repeat", "As", "Horizontal", "Spacing", "Alignment", "Wrap" },
            [List] = new[] { "Source", "As", "RowTemplate", "RowHeight", "VisibleRows", "Selectable", "BindSelected", "OnValueChanged", "OnScroll" },
            [DataGrid] = new[]
            {
                "Source", "As", "Columns", "RowHeight", "VisibleRows", "Selectable", "MultiSelect", "BindSelected", "BindSelection", "Sort", "SortDescending",
                "Filter", "RowTooltip", "OnRowClick", "OnRowActivated", "OnColumnResized", "OnValueChanged", "OnScroll", "ScrollSound", "SelectSound", "SortSound"
            },
            [Form] = new[] { "Fields", "Model", "ShowButtons", "OnSaved", "OnCancelled", "OnChanged" },
            [Composite] = new[] { "Children", "Composite", "Args", "ContentTarget", "On" },
            [Template] = new[] { "Children", "Template", "Args" },
            [Outlet] = new[] { "Children", "Horizontal", "Spacing", "Alignment", "Wrap" }
        };

        /// <summary>
        /// The style fields each leaf type draws with. Every other type holds children, which inherit the whole style,
        /// so any field can matter there; a leaf ignores the fields it does not list (<c>Padding</c> outside a Panel).
        /// </summary>
        private static readonly Dictionary<string, string[]> LeafStyle = new(StringComparer.OrdinalIgnoreCase)
        {
            [Spacer] = new[] { "TextColor" },
            [Label] = new[] { "Font", "TextColor", "TextShadow" },
            [Button] = new[] { "Font", "TextColor", "HoverColor", "TextShadow", "BoxTexture", "BoxSource", "BoxScale", "ClickSound", "HoverSound" },
            [Image] = Array.Empty<string>(),
            [ItemImage] = Array.Empty<string>(),
            [Checkbox] = new[] { "Font", "TextColor", "HoverColor", "TextShadow", "ClickSound" },
            [TextInput] = new[] { "Font", "TextColor", "TextShadow" },
            [NumberInput] = new[] { "Font", "TextColor", "TextShadow" },
            [Dropdown] = new[] { "Font", "TextColor", "HoverColor", "TextShadow", "ClickSound", "HoverSound" },
            [Slider] = new[] { "HoverColor" }
        };

        /// <summary>True when an element of <paramref name="type"/> (or one of its children) can use style field <paramref name="field"/>.</summary>
        internal static bool UsesStyle(string type, string field) => !LeafStyle.TryGetValue(type, out string[]? fields) || fields.Contains(field, StringComparer.OrdinalIgnoreCase);

        /// <summary>Every type name, in documentation order.</summary>
        internal static IReadOnlyCollection<string> All => Specific.Keys;

        /// <summary>The canonical spelling of <paramref name="type"/>, or null when it is not a known type.</summary>
        internal static string? Canonical(string? type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return null;
            }

            string trimmed = type.Trim();
            return Specific.Keys.FirstOrDefault(k => string.Equals(k, trimmed, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>True when the type holds (data-built) children; a Repeat's children are its row template instead.</summary>
        internal static bool IsContainer(string type) => type != Repeat && Specific.TryGetValue(type, out string[]? members) && members.Contains("Children");

        /// <summary>True for a custom tag: a dotted type name that stands for the composite of that name (v1.6).</summary>
        internal static bool IsCustomTag(string? type) => type != null && type.Trim().Contains('.') && Canonical(type) == null;

        /// <summary>The keys an element's Out map accepts.</summary>
        internal static readonly string[] OutKeys =
        {
            "IsHovered", "IsFocused", "Visible", "ScrollOffset", "MaxScroll", "SelectedIndex", "SelectedValue", "SelectedRow", "Value", "Text", "IsOpen", "X", "Y", "Width", "Height"
        };

        /// <summary>True when <paramref name="type"/> reads <paramref name="member"/>.</summary>
        internal static bool Uses(string type, string member)
        {
            return Common.Contains(member, StringComparer.OrdinalIgnoreCase)
                || (Specific.TryGetValue(type, out string[]? members) && members.Contains(member, StringComparer.OrdinalIgnoreCase));
        }
    }
}
