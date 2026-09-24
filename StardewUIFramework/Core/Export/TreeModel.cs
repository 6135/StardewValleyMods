using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace UIFramework.Core.Export
{
    /// <summary>What kind of value a <see cref="TreeProperty"/> holds (decides how each emitter writes it).</summary>
    internal enum TreeValueKind
    {
        Bool,
        Int,
        Float,
        Double,
        String,

        /// <summary>A framework enum (<c>UIAlign</c>, <c>UIFont</c> …); the value is the enum itself.</summary>
        Enum,
        Color,
        Rect,

        /// <summary>Four margins (<c>int[] { left, top, right, bottom }</c>).</summary>
        Margin,
        StringList,

        /// <summary>A <see cref="TreeStyle"/>.</summary>
        Style,

        /// <summary>A <see cref="TreeTooltip"/>.</summary>
        Tooltip,

        /// <summary>A <see cref="TreeIcon"/> (button icon).</summary>
        Icon,

        /// <summary>No value (delegates that cannot be previewed).</summary>
        None
    }

    /// <summary>Where a <see cref="TreeProperty"/>'s value came from.</summary>
    internal enum TreeValueOrigin
    {
        /// <summary>A plain value, written as is.</summary>
        Literal,

        /// <summary>A consumer delegate; <see cref="TreeProperty.Value"/> is its value right now (a preview).</summary>
        Delegate,

        /// <summary>Something that cannot be serialized (delegate, texture, object); <see cref="TreeProperty.Value"/> is an optional hint.</summary>
        Opaque
    }

    /// <summary>One property of an exported element, in the order the exporter reads it.</summary>
    internal sealed class TreeProperty
    {
        private TreeProperty(string name, TreeValueKind kind, TreeValueOrigin origin, object? value, bool isDefault, string? note)
        {
            Name = name;
            Kind = kind;
            Origin = origin;
            Value = value;
            IsDefault = isDefault;
            Note = note;
        }

        /// <summary>The C# member name (the data field name unless the JSON emitter maps it).</summary>
        internal string Name { get; }

        internal TreeValueKind Kind { get; }

        internal TreeValueOrigin Origin { get; }

        /// <summary>The value (literal), the evaluated preview (delegate) or an optional hint (opaque).</summary>
        internal object? Value { get; }

        /// <summary>True when the value equals the default, so emitters omit it (delegates: not set).</summary>
        internal bool IsDefault { get; }

        /// <summary>Extra information for the emitters (e.g. the composite-args setter name, or why a hint is a guess).</summary>
        internal string? Note { get; }

        internal static TreeProperty Literal(string name, TreeValueKind kind, object? value, bool isDefault)
        {
            return new TreeProperty(name, kind, TreeValueOrigin.Literal, value, isDefault, null);
        }

        internal static TreeProperty Evaluated(string name, TreeValueKind kind, object? preview, bool isSet, string? note = null)
        {
            return new TreeProperty(name, kind, TreeValueOrigin.Delegate, preview, !isSet, note);
        }

        internal static TreeProperty Opaque(string name, bool isSet, object? hint = null, string? note = null)
        {
            return new TreeProperty(name, hint == null ? TreeValueKind.None : TreeValueKind.String, TreeValueOrigin.Opaque, hint, !isSet, note);
        }

        public override string ToString() => $"{Name}={Value} ({Origin}{(IsDefault ? ", default" : string.Empty)})";
    }

    /// <summary>An exported inline style.</summary>
    internal sealed class TreeStyle
    {
        internal List<TreeProperty> Properties { get; } = new();
    }

    /// <summary>A button icon: the texture cannot be exported, only its source, scale and (when known) asset name.</summary>
    internal sealed class TreeIcon
    {
        internal Rectangle? Source { get; init; }
        internal float Scale { get; init; }
        internal string? TextureName { get; init; }
    }

    /// <summary>One block of an exported rich tooltip.</summary>
    internal sealed class TreeTooltipBlock
    {
        internal TooltipBlockKind Kind { get; init; }

        /// <summary>Title / line text right now.</summary>
        internal string? Text { get; init; }

        internal Color? Color { get; init; }
        internal Rectangle? Source { get; init; }
        internal float Scale { get; init; } = 1f;
        internal string? TextureName { get; init; }
        internal string ItemId { get; init; } = string.Empty;

        /// <summary>Money amount right now.</summary>
        internal int Amount { get; init; }

        /// <summary>ItemInstance: qualified id of the item right now (null when unknown).</summary>
        internal string? ItemPreview { get; init; }

        /// <summary>The block had a visibility condition (internal, no public API).</summary>
        internal bool HasWhen { get; init; }

        /// <summary>The block had a dynamic color (internal, no public API).</summary>
        internal bool HasColorFunc { get; init; }
    }

    /// <summary>An exported rich tooltip.</summary>
    internal sealed class TreeTooltip
    {
        internal List<TreeTooltipBlock> Blocks { get; } = new();
        internal int MaxWidth { get; init; }
    }

    /// <summary>One exported data grid column.</summary>
    internal sealed class TreeColumn
    {
        internal TreeColumn(string id, string header)
        {
            Id = id;
            Header = header;
        }

        internal string Id { get; }

        /// <summary>Header text right now.</summary>
        internal string Header { get; }

        /// <summary>Width first (always set), then the other column members in order.</summary>
        internal List<TreeProperty> Properties { get; } = new();
    }

    /// <summary>Element kinds the exporter understands (the data type names where one exists).</summary>
    internal static class TreeKinds
    {
        internal const string Stack = "Stack";
        internal const string Grid = "Grid";
        internal const string Panel = "Panel";
        internal const string Canvas = "Canvas";
        internal const string ScrollView = "ScrollView";
        internal const string List = "List";
        internal const string Slot = "Slot";
        internal const string Composite = "Composite";
        internal const string CustomHost = "CustomHost";
        internal const string Custom = "Custom";
        internal const string DataGrid = "DataGrid";
        internal const string Form = "Form";
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

        /// <summary>An element type the exporter does not know; only its id and CLR type are exported.</summary>
        internal const string Unknown = "Unknown";
    }

    /// <summary>
    /// One element of the exported tree, independent of the output format: its kind, id, an ordered property list
    /// (type-specific members first, then the common ones, each flagged default or not) and its exported children.
    /// </summary>
    internal sealed class TreeNode
    {
        internal TreeNode(string kind, string id, string clrType)
        {
            Kind = kind;
            Id = id;
            ClrType = clrType;
        }

        /// <summary>One of <see cref="TreeKinds"/>.</summary>
        internal string Kind { get; }

        internal string Id { get; }

        /// <summary>The element's CLR type name (used for identifier fallbacks and unknown-type notes).</summary>
        internal string ClrType { get; }

        internal List<TreeProperty> Properties { get; } = new();

        /// <summary>Exported children (for a custom host: the children of its host container).</summary>
        internal List<TreeNode> Children { get; } = new();

        /// <summary>Why the element's real children are not exported (built by a delegate, a composite, a model …), or null.</summary>
        internal string? ChildrenNote { get; set; }

        /// <summary>Slot: mod ids of the contributions that were left out.</summary>
        internal List<string> ExcludedContributors { get; } = new();

        /// <summary>Composite: the composite name.</summary>
        internal string? CompositeName { get; set; }

        /// <summary>Composite: true when the composite is defined in the <c>Composites</c> data asset (v1.7).</summary>
        internal bool IsDataComposite { get; set; }

        /// <summary>Composite: the arguments (<see cref="TreeProperty.Note"/> holds the C# setter name).</summary>
        internal List<TreeProperty> Args { get; } = new();

        /// <summary>Data grid: the columns.</summary>
        internal List<TreeColumn> Columns { get; } = new();

        /// <summary>Form: the model's type name.</summary>
        internal string? ModelType { get; set; }

        /// <summary>The first property called <paramref name="name"/>, or null.</summary>
        internal TreeProperty? Find(string name)
        {
            foreach (TreeProperty p in Properties)
            {
                if (p.Name == name)
                {
                    return p;
                }
            }

            return null;
        }

        /// <summary>The value of property <paramref name="name"/> (which the reader always adds for this kind).</summary>
        internal T Get<T>(string name)
        {
            TreeProperty p = Find(name) ?? throw new InvalidOperationException($"export model: {Kind} '{Id}' has no property '{name}'.");
            return p.Value is T value ? value : default!;
        }
    }

    /// <summary>The exported menu: options, menu-level events, the root stack (with its tree) and the default / cancel buttons.</summary>
    internal sealed class TreeMenu
    {
        internal TreeMenu(string id, string ownerModId, TreeNode root)
        {
            Id = id;
            OwnerModId = ownerModId;
            Root = root;
        }

        internal string Id { get; }
        internal string OwnerModId { get; }

        /// <summary>The <c>IUIMenuOptions</c> members, in order.</summary>
        internal List<TreeProperty> Options { get; } = new();

        /// <summary>Menu delegates (<c>OnOpen</c> …), in order.</summary>
        internal List<TreeProperty> Events { get; } = new();

        /// <summary>The root stack: only its own layout properties plus the children.</summary>
        internal TreeNode Root { get; }

        internal TreeNode? DefaultButton { get; set; }
        internal TreeNode? CancelButton { get; set; }
    }
}
