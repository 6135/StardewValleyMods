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
            "Margin", "MarginLeft", "MarginTop", "MarginRight", "MarginBottom", "Width", "Height", "HorizontalAlign", "VerticalAlign",
            "X", "Y", "Row", "Column", "RowSpan", "ColumnSpan", "Cell", "Span", "Style",
            "OnClick", "OnRightClick", "OnHover", "OnHoverEnd", "OnFocus", "OnBlur",
            "If", "Case", "With", "Out", "Class", "Keys", "RichTooltip", "DrawExtra", "DrawOverlay", "Outlet"
        };

        /// <summary>Type-specific members, by canonical type name.</summary>
        private static readonly Dictionary<string, string[]> Specific = new(StringComparer.OrdinalIgnoreCase)
        {
            [Stack] = new[] { "Children", "Horizontal", "Spacing", "Alignment" },
            [Grid] = new[] { "Children", "Columns", "Rows", "ColumnSpacing", "RowSpacing" },
            [Panel] = new[] { "Children", "DrawBox", "Padding" },
            [Canvas] = new[] { "Children" },
            [ScrollView] = new[] { "Children", "ViewportHeight", "ScrollStep", "ShowScrollbar", "OnScroll" },
            [Slot] = new[] { "Horizontal", "MaxHeight", "MaxContributions" },
            [Spacer] = new[] { "Line" },
            [Label] = new[] { "Text", "Label", "Font", "Color", "Shadow", "Wrap", "TextAlign", "Scale", "RichText", "OnLink" },
            [Button] = new[] { "Text", "Button", "Font", "Icon", "IconScale", "ClickSound", "HoverSound", "DrawBox", "RichText" },
            [Image] = new[] { "Sprite", "Image", "Source", "Scale", "Tint" },
            [ItemImage] = new[] { "Item", "Quality", "Count", "Scale", "Stack", "DrawShadow", "Alpha", "Tint" },
            [Checkbox] = new[] { "Label", "Checkbox", "Text", "Value", "Bind", "ClickSound", "OnValueChanged" },
            [TextInput] = new[] { "Value", "Bind", "Placeholder", "MaxLength", "Texture", "Validate", "OnInvalid", "OnValueChanged", "OnSubmit" },
            [NumberInput] = new[] { "Value", "Bind", "Min", "Max", "Step", "Clamp", "Decimals", "Texture", "Validate", "OnInvalid", "OnValueChanged", "OnSubmit" },
            [Dropdown] = new[] { "Value", "Bind", "Choices", "Labels", "MaxVisible", "ChoicesSource", "ChoiceValue", "ChoiceLabel", "OnValueChanged", "OnScroll" },
            [Slider] = new[] { "Value", "Bind", "Min", "Max", "Step", "OnValueChanged" },
            [Switch] = new[] { "Children", "Switch" },
            [Repeat] = new[] { "Children", "Repeat", "As", "Horizontal", "Spacing", "Alignment" },
            [List] = new[] { "Source", "As", "RowTemplate", "RowHeight", "VisibleRows", "Selectable", "BindSelected", "OnValueChanged", "OnScroll" },
            [DataGrid] = new[]
            {
                "Source", "As", "Columns", "RowHeight", "VisibleRows", "Selectable", "MultiSelect", "BindSelected", "BindSelection", "Sort", "SortDescending",
                "Filter", "RowTooltip", "OnRowClick", "OnRowActivated", "OnColumnResized", "OnValueChanged", "OnScroll", "ScrollSound", "SelectSound", "SortSound"
            },
            [Form] = new[] { "Fields", "Model", "ShowButtons", "OnSaved", "OnCancelled", "OnChanged" },
            [Composite] = new[] { "Children", "Composite", "Args", "ContentTarget", "On" },
            [Template] = new[] { "Children", "Template", "Args" },
            [Outlet] = new[] { "Children", "Horizontal", "Spacing", "Alignment" }
        };

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

        /// <summary>True for the collection elements whose children / row templates are built per row.</summary>
        internal static bool IsCollection(string type) => type is Repeat or List or DataGrid;

        /// <summary>True for inputs that hold a value (bound to state through Bind, default menu.&lt;Id&gt;).</summary>
        internal static bool IsInput(string type) => type is Checkbox or TextInput or NumberInput or Dropdown or Slider;

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
