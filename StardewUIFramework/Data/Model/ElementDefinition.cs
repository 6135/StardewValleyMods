using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework.Data.Model
{
    /// <summary>
    /// One element of a data menu tree. A single flat shape for every element type: <see cref="Type"/> selects the
    /// element and each type reads the members that apply to it (the others are reported by the validator). Value
    /// members are strings so numbers, bools and (from v1.4) expressions share one field; parsing is case-insensitive.
    /// <para>
    /// Shorthand: <c>{ "Label": "Hello" }</c>, <c>{ "Button": "OK" }</c>, <c>{ "Checkbox": "Enabled" }</c> and
    /// <c>{ "Image": "sprite:Owner/name" }</c> set both the type and its main value; a definition with only
    /// <see cref="Children"/> is a vertical stack.
    /// </para>
    /// </summary>
    internal sealed class ElementDefinition
    {
        // ---------------------------------------------------------------------------------------------------------
        //  Identity and structure
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Element type: Stack, Grid, Panel, Canvas, ScrollView, Slot, Spacer, Label, Button, Image, ItemImage, Checkbox, TextInput, NumberInput, Dropdown, Slider, Switch, Repeat, List, DataGrid, Form, Composite, Template or Outlet. A dotted name ("6135.Example.Gauge") is a custom tag: the composite of that name (C# or data), with the element's other fields as its arguments; the name of a template (the menu's Templates, else the owner's) expands that template the same way.</summary>
        public string? Type { get; set; }

        /// <summary>Element id, unique within the menu. Content Patcher TargetField reaches nested elements by it; missing ids are generated as parent.index.</summary>
        public string? Id { get; set; }

        /// <summary>A game state query evaluated when the menu opens; the element is hidden while it does not match.</summary>
        public string? Condition { get; set; }

        /// <summary>Child elements (containers only; the pages of a Switch).</summary>
        public List<ElementDefinition>? Children { get; set; }

        // ---------------------------------------------------------------------------------------------------------
        //  Structure (v1.4)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>An expression; the element is only built while it is true (rebuilt when it changes), unlike Visible which keeps it built but hidden.</summary>
        public string? If { get; set; }

        /// <summary>Switch: an expression whose value selects the child page with the matching Case (pages are built the first time they are shown). Also the shorthand for a Switch element.</summary>
        public string? Switch { get; set; }

        /// <summary>A page of a Switch: the value(s) that select it ("general" or "a, b"); "*" or no Case is the default page.</summary>
        public string? Case { get; set; }

        /// <summary>Narrows the scope: inside this element, ".day" means "&lt;With&gt;.day" (e.g. "menu.settings").</summary>
        public string? With { get; set; }

        /// <summary>Output bindings: runtime values written into state when they change, e.g. { "IsHovered": "menu.hoverOk", "ScrollOffset": "menu.scroll" }. Keys: IsHovered, IsFocused, Visible, ScrollOffset, MaxScroll, SelectedIndex, SelectedValue, SelectedRow, Value, Text, IsOpen, X, Y, Width, Height.</summary>
        public Dictionary<string, string>? Out { get; set; }

        /// <summary>Style classes from the owner's Owners entry ("header big"), merged in order before the inline Style.</summary>
        public string? Class { get; set; }

        /// <summary>Key handlers: [{ Key, Shift, Ctrl, Alt, Actions }]; the key counts as handled when an entry matches.</summary>
        public List<KeyBindingDefinition>? Keys { get; set; }

        // ---------------------------------------------------------------------------------------------------------
        //  Shorthand
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Shorthand for a Label element with this text; on a Checkbox, the checkbox label.</summary>
        public string? Label { get; set; }

        /// <summary>Shorthand for a Button element with this text.</summary>
        public string? Button { get; set; }

        /// <summary>Shorthand for a Checkbox element with this label.</summary>
        public string? Checkbox { get; set; }

        /// <summary>Shorthand for an Image element showing this image reference (sprite:Owner/name, item:(O)24, asset:Path@x,y,w,h).</summary>
        public string? Image { get; set; }

        // ---------------------------------------------------------------------------------------------------------
        //  Common members
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Whether the element is shown (hidden elements take no space in stacks).</summary>
        public string? Visible { get; set; }

        /// <summary>Whether the element takes input.</summary>
        public string? Enabled { get; set; }

        /// <summary>Tooltip text shown after the hover delay.</summary>
        public string? Tooltip { get; set; }

        /// <summary>Bold title line of the tooltip.</summary>
        public string? TooltipTitle { get; set; }

        /// <summary>Free-form tag (a string) for other mods.</summary>
        public string? Tag { get; set; }

        /// <summary>Opt out of modification by other mods (slot contributors / decorators).</summary>
        public string? Sealed { get; set; }

        /// <summary>Name read by screen readers instead of the visible text.</summary>
        public string? AccessibleName { get; set; }

        /// <summary>Margin shorthand: "all", "horizontal,vertical" or "left,top,right,bottom".</summary>
        public string? Margin { get; set; }

        /// <summary>Left margin in pixels.</summary>
        public string? MarginLeft { get; set; }

        /// <summary>Top margin in pixels.</summary>
        public string? MarginTop { get; set; }

        /// <summary>Right margin in pixels.</summary>
        public string? MarginRight { get; set; }

        /// <summary>Bottom margin in pixels.</summary>
        public string? MarginBottom { get; set; }

        /// <summary>Fixed width in pixels ("auto" or empty = measured). On a Spacer, its width.</summary>
        public string? Width { get; set; }

        /// <summary>Fixed height in pixels ("auto" or empty = measured). On a Spacer, its height.</summary>
        public string? Height { get; set; }

        /// <summary>Horizontal alignment in the parent's slot: Start, Center, End or Stretch.</summary>
        public string? HorizontalAlign { get; set; }

        /// <summary>Vertical alignment in the parent's slot: Start, Center, End or Stretch.</summary>
        public string? VerticalAlign { get; set; }

        /// <summary>X position inside a Canvas.</summary>
        public string? X { get; set; }

        /// <summary>Y position inside a Canvas.</summary>
        public string? Y { get; set; }

        /// <summary>Grid row (0-based).</summary>
        public string? Row { get; set; }

        /// <summary>Grid column (0-based).</summary>
        public string? Column { get; set; }

        /// <summary>Number of grid rows spanned.</summary>
        public string? RowSpan { get; set; }

        /// <summary>Number of grid columns spanned.</summary>
        public string? ColumnSpan { get; set; }

        /// <summary>Grid cell shorthand: "row,column".</summary>
        public string? Cell { get; set; }

        /// <summary>Grid span shorthand: "rows,columns".</summary>
        public string? Span { get; set; }

        /// <summary>Inline style overrides for this element.</summary>
        public StyleDefinition? Style { get; set; }

        // ---------------------------------------------------------------------------------------------------------
        //  Events (action lists)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Actions run on a left click.</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnClick { get; set; }

        /// <summary>Actions run on a right click.</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnRightClick { get; set; }

        /// <summary>Actions run when the cursor enters the element.</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnHover { get; set; }

        /// <summary>Actions run when the cursor leaves the element.</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnHoverEnd { get; set; }

        /// <summary>Actions run when the element gains keyboard / gamepad focus.</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnFocus { get; set; }

        /// <summary>Actions run when the element loses focus.</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnBlur { get; set; }

        /// <summary>Actions run when an input's value changes (List / DataGrid: the selection; event.index, event.row).</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnValueChanged { get; set; }

        /// <summary>Actions run when Enter is pressed in a text / number input.</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnSubmit { get; set; }

        /// <summary>Actions run when a [link=...] span of a rich text label is clicked.</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnLink { get; set; }

        /// <summary>Actions run when a scroll view / dropdown / list / grid scrolls (event.delta).</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnScroll { get; set; }

        // ---------------------------------------------------------------------------------------------------------
        //  Containers
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Stack / Slot: lay children out in a row instead of a column.</summary>
        public string? Horizontal { get; set; }

        /// <summary>Stack: pixels between children.</summary>
        public string? Spacing { get; set; }

        /// <summary>Stack: default cross-axis alignment of the children.</summary>
        public string? Alignment { get; set; }

        /// <summary>Grid: column tracks, e.g. "auto, *, 2*, 120" (or [{ "Width": "auto" }, ...]). DataGrid: the columns, [{ "Id", "Header", "Width", "Text", "SortNumber", "Cell", ... }].</summary>
        [JsonConverter(typeof(ColumnListConverter))]
        public List<ColumnDefinition>? Columns { get; set; }

        /// <summary>Grid: row tracks, e.g. "auto, auto".</summary>
        public string? Rows { get; set; }

        /// <summary>Grid: pixels between columns.</summary>
        public string? ColumnSpacing { get; set; }

        /// <summary>Grid: pixels between rows.</summary>
        public string? RowSpacing { get; set; }

        /// <summary>Panel / Button: draw the 9-slice box.</summary>
        public string? DrawBox { get; set; }

        /// <summary>Panel: padding inside the box.</summary>
        public string? Padding { get; set; }

        /// <summary>ScrollView: height of the visible area in pixels.</summary>
        public string? ViewportHeight { get; set; }

        /// <summary>ScrollView: pixels scrolled per wheel notch.</summary>
        public string? ScrollStep { get; set; }

        /// <summary>ScrollView: show the scrollbar when the content overflows.</summary>
        public string? ShowScrollbar { get; set; }

        /// <summary>Slot: maximum height before the slot scrolls.</summary>
        public string? MaxHeight { get; set; }

        /// <summary>Slot: maximum number of contributions accepted (0 = unlimited).</summary>
        public string? MaxContributions { get; set; }

        // ---------------------------------------------------------------------------------------------------------
        //  Text
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Label / Button text.</summary>
        public string? Text { get; set; }

        /// <summary>Font: small, dialogue or tiny.</summary>
        public string? Font { get; set; }

        /// <summary>Label text color (#RRGGBB, #RRGGBBAA, R,G,B[,A] or a color name).</summary>
        public string? Color { get; set; }

        /// <summary>Label: draw the text shadow.</summary>
        public string? Shadow { get; set; }

        /// <summary>Label: wrap to the available width.</summary>
        public string? Wrap { get; set; }

        /// <summary>Label: text alignment inside the label (Start, Center, End).</summary>
        public string? TextAlign { get; set; }

        /// <summary>Label text scale, or Image / ItemImage scale.</summary>
        public string? Scale { get; set; }

        /// <summary>Label / Button: parse [b], [color=...], [link=...] and [[ markup.</summary>
        public string? RichText { get; set; }

        // ---------------------------------------------------------------------------------------------------------
        //  Buttons and images
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Button icon: an image reference (sprite:Owner/name, item:(O)24, asset:Path@x,y,w,h).</summary>
        public string? Icon { get; set; }

        /// <summary>Button icon scale.</summary>
        public string? IconScale { get; set; }

        /// <summary>Sound cue played on click (empty = silent).</summary>
        public string? ClickSound { get; set; }

        /// <summary>Sound cue played when the cursor enters (empty = silent).</summary>
        public string? HoverSound { get; set; }

        /// <summary>Image: an image reference (sprite:Owner/name, item:(O)24, asset:Path@x,y,w,h, or a texture asset name).</summary>
        public string? Sprite { get; set; }


        /// <summary>Image / ItemImage tint color.</summary>
        public string? Tint { get; set; }

        /// <summary>ItemImage: a qualified item id ((O)24), an item query (FLAVORED_ITEM Wine (O)398) or an expression giving an item (${row.item}); resolved items are cached by text.</summary>
        public string? Item { get; set; }

        /// <summary>ItemImage: item quality (0 normal, 1 silver, 2 gold, 4 iridium).</summary>
        public string? Quality { get; set; }

        /// <summary>ItemImage: stack size.</summary>
        public string? Count { get; set; }

        /// <summary>ItemImage: what to draw over the item: Hide, Quality or NumberAndQuality.</summary>
        public string? Stack { get; set; }

        /// <summary>ItemImage: draw the item's shadow.</summary>
        public string? DrawShadow { get; set; }

        /// <summary>ItemImage: opacity (0-1).</summary>
        public string? Alpha { get; set; }

        /// <summary>Spacer: draw a horizontal divider line.</summary>
        public string? Line { get; set; }

        // ---------------------------------------------------------------------------------------------------------
        //  Inputs
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Default value of an input (text, number, true/false or the selected choice); only used when its state value does not exist yet.</summary>
        public string? Value { get; set; }

        /// <summary>The state value an input reads and writes (two-way), e.g. "menu.name", "config.volume", "player.nickname" or ".day" inside a With. Default: menu.&lt;Id&gt;.</summary>
        public string? Bind { get; set; }

        /// <summary>TextInput / NumberInput: an expression checked for every new value (event.value); false or a message rejects it, and OnInvalid runs with event.error.</summary>
        public string? Validate { get; set; }

        /// <summary>Actions run when Validate rejects a value (event.value, event.error).</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnInvalid { get; set; }

        /// <summary>TextInput: text shown while empty.</summary>
        public string? Placeholder { get; set; }

        /// <summary>TextInput: maximum number of characters (0 = unlimited).</summary>
        public string? MaxLength { get; set; }

        /// <summary>TextInput / NumberInput: text box texture (an image reference or asset name, e.g. Mods/6135.UIFramework/TextBoxSmall).</summary>
        public string? Texture { get; set; }

        /// <summary>NumberInput / Slider minimum.</summary>
        public string? Min { get; set; }

        /// <summary>NumberInput / Slider maximum.</summary>
        public string? Max { get; set; }

        /// <summary>NumberInput / Slider step.</summary>
        public string? Step { get; set; }

        /// <summary>NumberInput: clamp typed values to Min / Max.</summary>
        public string? Clamp { get; set; }

        /// <summary>NumberInput: decimals shown.</summary>
        public string? Decimals { get; set; }

        /// <summary>Dropdown: choice values (array, or one comma-separated string).</summary>
        [JsonConverter(typeof(StringListConverter))]
        public List<string>? Choices { get; set; }

        /// <summary>Dropdown: display labels matching Choices (defaults to the values).</summary>
        [JsonConverter(typeof(StringListConverter))]
        public List<string>? Labels { get; set; }

        /// <summary>Dropdown: rows visible in the open list before it scrolls.</summary>
        public string? MaxVisible { get; set; }

        /// <summary>Dropdown: take the choices from a source instead of Choices (e.g. "themes" or { "Query": "ALL_ITEMS (O)" }).</summary>
        public SourceDefinition? ChoicesSource { get; set; }

        /// <summary>Dropdown with ChoicesSource: an expression giving a row's value (default: row.value, row.id, row.key, else the row itself).</summary>
        public string? ChoiceValue { get; set; }

        /// <summary>Dropdown with ChoicesSource: an expression giving a row's label (default: row.label, row.displayName, row.name, else the value).</summary>
        public string? ChoiceLabel { get; set; }

        // ---------------------------------------------------------------------------------------------------------
        //  Collections (v1.5)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>List / DataGrid: where the rows come from ("menu.items", "themes", "range:1..10", { "Query": "ALL_ITEMS (O)" }, { "Rows": [...] }...). Image: the source rectangle "x,y,w,h" (overrides the sprite's own).</summary>
        public SourceDefinition? Source { get; set; }

        /// <summary>Repeat: the source to repeat the children over (a non-virtualized copy per row); also the shorthand for a Repeat element.</summary>
        public SourceDefinition? Repeat { get; set; }

        /// <summary>List / DataGrid / Repeat: the name of the row variable besides "row" (nested repeats reach outer rows by it), e.g. "fruit".</summary>
        public string? As { get; set; }

        /// <summary>List: the elements of one row (an element or an array); ids are prefixed with the row container's id.</summary>
        [JsonConverter(typeof(ElementListConverter))]
        public List<ElementDefinition>? RowTemplate { get; set; }

        /// <summary>List / DataGrid: row height in pixels (default 48).</summary>
        public string? RowHeight { get; set; }

        /// <summary>List / DataGrid: number of rows shown (default 6).</summary>
        public string? VisibleRows { get; set; }

        /// <summary>List / DataGrid: clicking a row selects it.</summary>
        public string? Selectable { get; set; }

        /// <summary>DataGrid: Ctrl-click toggles rows, Shift-click selects a range.</summary>
        public string? MultiSelect { get; set; }

        /// <summary>List / DataGrid: a state value holding the selected row index (two-way; -1 = none).</summary>
        public string? BindSelected { get; set; }

        /// <summary>DataGrid: a state value holding every selected row index as comma-separated text (two-way).</summary>
        public string? BindSelection { get; set; }

        /// <summary>DataGrid: the column sorted by when the grid is built ("profit" or "profit desc").</summary>
        public string? Sort { get; set; }

        /// <summary>DataGrid: sort the initial Sort column descending.</summary>
        public string? SortDescending { get; set; }

        /// <summary>DataGrid: an expression over the row; rows where it is false are hidden (row indices stay stable). Re-run when state changes.</summary>
        public string? Filter { get; set; }

        /// <summary>DataGrid: a rich tooltip for each row (blocks read row.x).</summary>
        public TooltipDefinition? RowTooltip { get; set; }

        /// <summary>DataGrid: actions run on every click on a row (event.row, event.index, event.button).</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnRowClick { get; set; }

        /// <summary>DataGrid: actions run on a double-click or Enter on a row (event.row, event.index).</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnRowActivated { get; set; }

        /// <summary>DataGrid: actions run after a column was resized by dragging (event.column, event.width).</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnColumnResized { get; set; }

        /// <summary>DataGrid: sound cue of scrolling (empty = silent).</summary>
        public string? ScrollSound { get; set; }

        /// <summary>DataGrid: sound cue of selecting (empty = silent).</summary>
        public string? SelectSound { get; set; }

        /// <summary>DataGrid: sound cue of sorting (empty = silent).</summary>
        public string? SortSound { get; set; }

        // ---------------------------------------------------------------------------------------------------------
        //  Rich tooltips and forms (v1.5)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A rich tooltip: { "MaxWidth": 400, "Blocks": [ { "Type": "Title" | "Line" | "Icon" | "Item" | "Divider" | "Money", ..., "When": "expr" } ] } (or the block array alone).</summary>
        public TooltipDefinition? RichTooltip { get; set; }

        /// <summary>Form: the fields, each bound to a state value: [{ "Id", "Bind", "Kind", "Label", "Tooltip", "Section", "Min", "Max", "Choices", "ReadOnly", "Validate" }].</summary>
        public List<FormFieldDefinition>? Fields { get; set; }

        /// <summary>Form: show the Save / Cancel / Undo / Redo buttons (default true).</summary>
        public string? ShowButtons { get; set; }

        /// <summary>Form: actions run after Save.</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnSaved { get; set; }

        /// <summary>Form: actions run after Cancel restored the saved values.</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnCancelled { get; set; }

        /// <summary>Form: actions run after every edit, undo, redo or cancel.</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? OnChanged { get; set; }

        // ---------------------------------------------------------------------------------------------------------
        //  C# bridge (v1.6)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Composite: the global name of the composite to instantiate (defined in C# with DefineComposite), e.g. "6135.Example.Gauge".</summary>
        public string? Composite { get; set; }

        /// <summary>Composite: its arguments by name. Text that is a state / model path ("menu.volume", "model.settings.Day") is a reference (read and written by the composite); "${...}" is live; action lists work for command arguments.</summary>
        public Dictionary<string, JToken?>? Args { get; set; }

        /// <summary>Composite: the id of the element inside the composite that receives this element's Children (default: the host of its first custom component, "&lt;id&gt;.host", else the composite itself).</summary>
        public string? ContentTarget { get; set; }

        /// <summary>Composite instance: actions run when the composite publishes an event ({ "changed": [ ... ] }; C# host.Publish or the body's 6135.UIFramework_Publish).</summary>
        [JsonProperty(ItemConverterType = typeof(ActionListConverter))]
        public Dictionary<string, List<ActionDefinition>>? On { get; set; }

        // ---------------------------------------------------------------------------------------------------------
        //  Templates (v1.7)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>An instance of a template (the menu's Templates, else the owner's): its body is expanded here; the other fields are its arguments (args.*). Writing the template's name as the Type does the same.</summary>
        public string? Template { get; set; }

        /// <summary>On a child of a template / composite instance: the Outlet placeholder it goes into (default: the unnamed one). In a template / composite body, an element with only an Outlet name is that placeholder.</summary>
        public string? Outlet { get; set; }

        /// <summary>A draw hook registered in C# (RegisterDrawHook), "ModId/name" (or the owner's short name): drawn after the element's own content.</summary>
        public string? DrawExtra { get; set; }

        /// <summary>A draw hook registered in C# (RegisterDrawHook), "ModId/name": drawn above the whole menu, over the element's bounds.</summary>
        public string? DrawOverlay { get; set; }

        /// <summary>Form: a model exposed from C# (ExposeModel) whose public members become the fields: "settings", "model.settings.Sub" or "model[ModId/settings]".</summary>
        public string? Model { get; set; }

        /// <summary>Fields that match no member (typos); reported by the validator with a suggestion.</summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? Unknown { get; set; }
    }
}
