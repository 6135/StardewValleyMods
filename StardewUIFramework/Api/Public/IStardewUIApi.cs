// ---------------------------------------------------------------------------------------------------------------------
//  UI Framework (6135.UIFramework) - public API.
//
//  Copy this file into your mod (you may change the namespace), request the API from SMAPI's mod registry with
//  GetApi<IStardewUIApi>("6135.UIFramework") once the game has launched, and list 6135.UIFramework
//  (MinimumVersion 1.0.0, IsRequired true) under Dependencies in your manifest.json.
//
//  Everything in this file is proxy-safe (SMAPI/Pintail): interfaces, enums, delegates, primitives and XNA / game types.
//  Members are only ever added, never renamed or removed; a breaking change would ship as IStardewUIApi2.
//  Overloads must differ by parameter count, never only by interface type: Pintail resolves overloads by trying to
//  proxy each interface parameter and throws (instead of moving on) when two interfaces do not match.
// ---------------------------------------------------------------------------------------------------------------------

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace UIFramework.Api
{
    // =================================================================================================================
    //  Enums
    // =================================================================================================================

    /// <summary>Alignment of an element inside the slot its parent gives it (or of text inside a label).</summary>
    public enum UIAlign
    {
        /// <summary>Left / top.</summary>
        Start,
        /// <summary>Centered.</summary>
        Center,
        /// <summary>Right / bottom.</summary>
        End,
        /// <summary>Fill the whole slot.</summary>
        Stretch
    }

    /// <summary>Vanilla fonts.</summary>
    public enum UIFont
    {
        /// <summary><c>Game1.smallFont</c>.</summary>
        Small,
        /// <summary><c>Game1.dialogueFont</c>.</summary>
        Dialogue,
        /// <summary><c>Game1.tinyFont</c>.</summary>
        Tiny
    }

    /// <summary>Where a menu is placed on screen.</summary>
    public enum UIAnchor
    {
        Center,
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight,
        /// <summary>Use <see cref="IUIMenu.X"/> / <see cref="IUIMenu.Y"/> verbatim.</summary>
        Explicit
    }

    /// <summary>Mouse button of a click event.</summary>
    public enum UIMouseButton
    {
        Left,
        Right
    }

    /// <summary>Kind of a UI event (informational, mirrors the callback that raised it).</summary>
    public enum UIEventKind
    {
        Click,
        RightClick,
        Hover,
        HoverEnd,
        ValueChanged,
        Focus,
        Blur,
        Key,
        Scroll,
        Open,
        Close
    }

    // =================================================================================================================
    //  Event args
    // =================================================================================================================

    /// <summary>Base event data.</summary>
    public interface IUIEvent
    {
        /// <summary>What kind of event this is.</summary>
        UIEventKind Kind { get; }

        /// <summary>The element the event targets (null for menu-level events).</summary>
        IUIElement Element { get; }

        /// <summary>Id of <see cref="Element"/>, or an empty string.</summary>
        string ElementId { get; }

        /// <summary>Set to true to stop the event bubbling to parent elements / the menu.</summary>
        bool Handled { get; set; }
    }

    /// <summary>Data for click events.</summary>
    public interface IUIClickEvent : IUIEvent
    {
        /// <summary>Cursor X in UI pixels.</summary>
        int X { get; }

        /// <summary>Cursor Y in UI pixels.</summary>
        int Y { get; }

        /// <summary>Which mouse button.</summary>
        UIMouseButton Button { get; }
    }

    /// <summary>Data for value-change events. Only the accessors matching the element's value type are meaningful.</summary>
    public interface IUIValueEvent : IUIEvent
    {
        string OldValue { get; }
        string NewValue { get; }
        double OldNumber { get; }
        double NewNumber { get; }
        bool OldBool { get; }
        bool NewBool { get; }
        int OldIndex { get; }
        int NewIndex { get; }
    }

    /// <summary>Data for key events.</summary>
    public interface IUIKeyEvent : IUIEvent
    {
        Keys Key { get; }
        bool Shift { get; }
        bool Ctrl { get; }
        bool Alt { get; }
    }

    // =================================================================================================================
    //  Styling
    // =================================================================================================================

    /// <summary>
    /// Visual overrides. Every member is optional; a null / default value means "use the theme default".
    /// Create with <see cref="IStardewUIApi.CreateStyle"/> and assign to <see cref="IUIElement.Style"/>.
    /// </summary>
    public interface IUIStyle
    {
        /// <summary>Font for text (labels, buttons, inputs).</summary>
        UIFont? Font { get; set; }

        /// <summary>Text color.</summary>
        Color? TextColor { get; set; }

        /// <summary>Tint applied to boxes / buttons while hovered.</summary>
        Color? HoverColor { get; set; }

        /// <summary>Texture used for 9-slice boxes (panels, buttons).</summary>
        Texture2D BoxTexture { get; set; }

        /// <summary>Source rectangle inside <see cref="BoxTexture"/>.</summary>
        Rectangle? BoxSource { get; set; }

        /// <summary>Scale applied to the 9-slice border.</summary>
        float? BoxScale { get; set; }

        /// <summary>Inner padding for boxes.</summary>
        int? Padding { get; set; }

        /// <summary>Draw text with a shadow.</summary>
        bool? TextShadow { get; set; }

        /// <summary>Sound cue played on click (empty string = none).</summary>
        string ClickSound { get; set; }

        /// <summary>Sound cue played when the cursor enters (empty string = none).</summary>
        string HoverSound { get; set; }
    }

    // =================================================================================================================
    //  Elements
    // =================================================================================================================

    /// <summary>Common surface of every element in a menu tree.</summary>
    public interface IUIElement
    {
        /// <summary>Consumer-chosen id (unique within its menu).</summary>
        string Id { get; }

        /// <summary>Parent container, or null for the menu root.</summary>
        IUIContainer Parent { get; }

        /// <summary>The menu this element belongs to.</summary>
        IUIMenu Menu { get; }

        /// <summary>Absolute bounds in UI pixels, valid after layout.</summary>
        Rectangle Bounds { get; }

        bool Visible { get; set; }
        bool Enabled { get; set; }

        /// <summary>Tooltip body, evaluated when shown. Null disables the tooltip.</summary>
        Func<string> Tooltip { get; set; }

        /// <summary>Optional bold tooltip title.</summary>
        Func<string> TooltipTitle { get; set; }

        /// <summary>Free-form consumer data.</summary>
        object Tag { get; set; }

        /// <summary>Visual overrides for this element (null = inherit).</summary>
        IUIStyle Style { get; set; }

        int MarginLeft { get; set; }
        int MarginTop { get; set; }
        int MarginRight { get; set; }
        int MarginBottom { get; set; }

        /// <summary>Set all four margins.</summary>
        void SetMargin(int all);

        /// <summary>Set horizontal (left/right) and vertical (top/bottom) margins.</summary>
        void SetMargin(int horizontal, int vertical);

        /// <summary>Set each margin.</summary>
        void SetMargin(int left, int top, int right, int bottom);

        /// <summary>Explicit width in UI pixels, or null for "size to content".</summary>
        int? Width { get; set; }

        /// <summary>Explicit height in UI pixels, or null for "size to content".</summary>
        int? Height { get; set; }

        /// <summary>Horizontal alignment inside the slot given by the parent.</summary>
        UIAlign HorizontalAlign { get; set; }

        /// <summary>Vertical alignment inside the slot given by the parent.</summary>
        UIAlign VerticalAlign { get; set; }

        /// <summary>X position, only used when the parent is a <see cref="IUICanvas"/>.</summary>
        int X { get; set; }

        /// <summary>Y position, only used when the parent is a <see cref="IUICanvas"/>.</summary>
        int Y { get; set; }

        /// <summary>Grid row, only used when the parent is a <see cref="IUIGrid"/>.</summary>
        int Row { get; set; }

        /// <summary>Grid column, only used when the parent is a <see cref="IUIGrid"/>.</summary>
        int Column { get; set; }

        /// <summary>Grid row span (default 1).</summary>
        int RowSpan { get; set; }

        /// <summary>Grid column span (default 1).</summary>
        int ColumnSpan { get; set; }

        /// <summary>True while this element owns keyboard focus.</summary>
        bool IsFocused { get; }

        /// <summary>True while the cursor is over this element.</summary>
        bool IsHovered { get; }

        /// <summary>Give this element keyboard focus (no-op if it is not focusable).</summary>
        void Focus();

        /// <summary>Request a layout pass before the next draw.</summary>
        void InvalidateLayout();

        // ---- events ----

        Action<IUIClickEvent> OnClick { get; set; }
        Action<IUIClickEvent> OnRightClick { get; set; }
        Action<IUIElement> OnHover { get; set; }
        Action<IUIElement> OnHoverEnd { get; set; }
        Action<IUIElement> OnFocus { get; set; }
        Action<IUIElement> OnBlur { get; set; }

        /// <summary>Raised for key presses while focused (or bubbling from a focused child). Return true to mark handled.</summary>
        Func<IUIKeyEvent, bool> OnKey { get; set; }

        /// <summary>Called after the element drew itself, with its absolute bounds. Draw custom decorations here.</summary>
        Action<SpriteBatch, Rectangle> OnDrawExtra { get; set; }

        /// <summary>Called in the overlay pass (on top of everything else in the menu), with the element's absolute bounds.</summary>
        Action<SpriteBatch, Rectangle> OnDrawOverlay { get; set; }

        // SLOTS
        /// <summary>
        /// Opt this element (and its descendants) out of modification by other mods (see architecture.md §16.1).
        /// The framework cannot attribute a property setter to a caller, so sealing is enforced on lookup and tree
        /// edits instead: <see cref="IUIMenu.Find"/> while a slot contribution or <see cref="IStardewUIApi.OnScreenBuilt"/>
        /// decorator of another mod is running, and <see cref="IStardewUIApi.Find"/> from another mod's API instance,
        /// return null for a sealed element and everything below it; <see cref="IStardewUIApi.Remove"/> of, and any
        /// <c>Add*</c> into, a sealed subtree from another mod's API instance throw <see cref="InvalidOperationException"/>.
        /// A slot contributor keeps full access to the container it was handed, even under a sealed ancestor. The owner
        /// of the menu is never restricted.
        /// </summary>
        bool Sealed { get; set; }
        // RICHTEXT
        /// <summary>
        /// Rich tooltip (title, lines, icons, items, money) built with <see cref="IStardewUIApi.CreateTooltip"/>.
        /// When set it replaces <see cref="Tooltip"/> / <see cref="TooltipTitle"/>; null shows the plain tooltip.
        /// </summary>
        IUITooltip RichTooltip { get; set; }
        // THEME
        /// <summary>Screen reader description override (null = the framework's "&lt;type&gt;: &lt;label / text / value&gt;" description).</summary>
        Func<string> AccessibleName { get; set; }
    }

    /// <summary>An element that holds children.</summary>
    public interface IUIContainer : IUIElement
    {
        int ChildCount { get; }

        /// <summary>Get the child at <paramref name="index"/> (in add order).</summary>
        IUIElement GetChild(int index);

        /// <summary>Re-parent an existing element under this container (it is removed from its old parent).</summary>
        void Add(IUIElement child);

        /// <summary>Remove a direct child.</summary>
        void Remove(IUIElement child);

        /// <summary>Remove all children.</summary>
        void Clear();
    }

    /// <summary>A box with padding and an optional 9-slice background. Children overlap and fill the padded area.</summary>
    public interface IUIPanel : IUIContainer
    {
        bool DrawBox { get; set; }
        int Padding { get; set; }
    }

    /// <summary>Lays children out in a row or column.</summary>
    public interface IUIStack : IUIContainer
    {
        bool Horizontal { get; set; }
        int Spacing { get; set; }

        /// <summary>Default cross-axis alignment for children that did not set their own.</summary>
        UIAlign Alignment { get; set; }
    }

    /// <summary>
    /// Rows and columns. Track definitions are comma separated: <c>auto</c>, <c>120px</c> (or <c>120</c>), <c>*</c>, <c>2*</c>.
    /// Children pick a cell through <see cref="IUIElement.Row"/>, <see cref="IUIElement.Column"/> and the span properties.
    /// </summary>
    public interface IUIGrid : IUIContainer
    {
        string Columns { get; set; }
        string Rows { get; set; }
        int ColumnSpacing { get; set; }
        int RowSpacing { get; set; }
    }

    /// <summary>Places children at explicit <see cref="IUIElement.X"/> / <see cref="IUIElement.Y"/> offsets.</summary>
    public interface IUICanvas : IUIContainer
    {
    }

    /// <summary>Clips and scrolls its content vertically.</summary>
    public interface IUIScrollView : IUIContainer
    {
        /// <summary>Visible height in UI pixels.</summary>
        int ViewportHeight { get; set; }

        /// <summary>Current scroll offset in UI pixels (0 = top).</summary>
        int ScrollOffset { get; set; }

        /// <summary>Largest valid <see cref="ScrollOffset"/>.</summary>
        int MaxScroll { get; }

        /// <summary>Pixels moved per wheel notch / arrow click.</summary>
        int ScrollStep { get; set; }

        bool ShowScrollbar { get; set; }

        /// <summary>Raised after the offset changed; argument is the delta in pixels (negative = up).</summary>
        Action<int> OnScroll { get; set; }

        void ScrollTo(int offset);
        void ScrollBy(int delta);
    }

    /// <summary>
    /// Virtualized list: only the visible rows exist as elements. Rows are rebuilt through the
    /// <c>buildRow</c> delegate given to <see cref="IStardewUIApi.AddList"/> whenever they scroll into a new index.
    /// </summary>
    public interface IUIList : IUIContainer
    {
        int RowHeight { get; set; }
        int VisibleRows { get; set; }

        /// <summary>Index of the first visible row.</summary>
        int FirstVisibleIndex { get; set; }

        /// <summary>Number of items reported by the data source at the last refresh.</summary>
        int ItemCount { get; }

        /// <summary>Whether clicking a row selects it.</summary>
        bool Selectable { get; set; }

        /// <summary>Selected item index or -1.</summary>
        int SelectedIndex { get; set; }

        /// <summary>Raised when <see cref="SelectedIndex"/> changes (<see cref="IUIValueEvent.OldIndex"/> / <see cref="IUIValueEvent.NewIndex"/>).</summary>
        Action<IUIValueEvent> OnValueChanged { get; set; }

        /// <summary>Raised after scrolling; argument is the row delta.</summary>
        Action<int> OnScroll { get; set; }

        /// <summary>Re-query the item count and rebuild visible rows.</summary>
        void Refresh();

        void ScrollTo(int firstIndex);
    }

    /// <summary>Text.</summary>
    public interface IUILabel : IUIElement
    {
        Func<string> Text { get; set; }
        UIFont Font { get; set; }
        Color? Color { get; set; }
        bool Shadow { get; set; }

        /// <summary>Wrap to the available width (or to <see cref="IUIElement.Width"/>).</summary>
        bool Wrap { get; set; }

        /// <summary>Horizontal text alignment inside the label's bounds.</summary>
        UIAlign TextAlign { get; set; }

        float Scale { get; set; }

        // RICHTEXT
        /// <summary>
        /// Parse markup in <see cref="Text"/>: <c>[color=#RRGGBB]…[/color]</c> (or a name: <c>red</c>, <c>green</c>, <c>blue</c>, <c>gray</c>/<c>grey</c>, <c>white</c>, <c>black</c>, <c>yellow</c>, <c>orange</c>, <c>purple</c>),
        /// <c>[b]…[/b]</c>, <c>[icon=(O)24]</c>, <c>[link=name]…[/link]</c>; <c>[[</c> / <c>]]</c> are literal brackets. Default false.
        /// </summary>
        bool RichText { get; set; }

        /// <summary>Raised with the link name when a <c>[link=name]</c> span is clicked (<see cref="RichText"/> only).</summary>
        Action<string> OnLink { get; set; }
    }

    /// <summary>A texture (or a region of one).</summary>
    public interface IUIImage : IUIElement
    {
        Texture2D Texture { get; set; }
        Rectangle? Source { get; set; }
        float Scale { get; set; }
        Color Tint { get; set; }
    }

    /// <summary>A clickable box with text and/or an icon.</summary>
    public interface IUIButton : IUIElement
    {
        Func<string> Text { get; set; }
        UIFont Font { get; set; }
        Texture2D Icon { get; set; }
        Rectangle? IconSource { get; set; }
        float IconScale { get; set; }

        /// <summary>Sound cue on click (null = theme default, empty = none).</summary>
        string ClickSound { get; set; }

        /// <summary>Sound cue when hovered (null = theme default, empty = none).</summary>
        string HoverSound { get; set; }

        /// <summary>Draw the 9-slice box behind the content.</summary>
        bool DrawBox { get; set; }

        // RICHTEXT
        /// <summary>Parse markup in <see cref="Text"/> (same syntax as <see cref="IUILabel.RichText"/>; links are not clickable on buttons). Default false.</summary>
        bool RichText { get; set; }
    }

    /// <summary>A boolean toggle.</summary>
    public interface IUICheckbox : IUIElement
    {
        bool Value { get; set; }

        /// <summary>Optional text drawn to the right of the box.</summary>
        Func<string> Label { get; set; }

        string ClickSound { get; set; }
        Action<IUIValueEvent> OnValueChanged { get; set; }
    }

    /// <summary>Single-line text entry.</summary>
    public interface IUITextInput : IUIElement
    {
        string Value { get; set; }
        Func<string> Placeholder { get; set; }

        /// <summary>Maximum characters (0 = unlimited).</summary>
        int MaxLength { get; set; }

        /// <summary>Called with the prospective new value before it is applied; return false to reject.</summary>
        Func<string, bool> Validate { get; set; }

        /// <summary>Custom box texture (null = vanilla text box).</summary>
        Texture2D Texture { get; set; }

        Action<IUIValueEvent> OnValueChanged { get; set; }

        /// <summary>Raised when Enter is pressed while focused.</summary>
        Action<IUIElement> OnSubmit { get; set; }
    }

    /// <summary>Numeric entry with clamping, stepping (Up/Down keys, wheel) and validation.</summary>
    public interface IUINumberInput : IUIElement
    {
        double Value { get; set; }
        double Min { get; set; }
        double Max { get; set; }
        double Step { get; set; }
        bool Clamp { get; set; }

        /// <summary>Decimal places accepted / displayed (0 = integers only).</summary>
        int Decimals { get; set; }

        /// <summary>Called with the prospective new value before it is applied; return false to reject.</summary>
        Func<double, bool> Validate { get; set; }

        /// <summary>Custom box texture (null = vanilla text box).</summary>
        Texture2D Texture { get; set; }

        Action<IUIValueEvent> OnValueChanged { get; set; }
        Action<IUIElement> OnSubmit { get; set; }
    }

    /// <summary>Pick one of several string choices.</summary>
    public interface IUIDropdown : IUIElement
    {
        int SelectedIndex { get; set; }
        string SelectedValue { get; set; }

        /// <summary>Rows shown at once when open (the list scrolls beyond that).</summary>
        int MaxVisible { get; set; }

        bool IsOpen { get; }
        void Open();
        void Close();

        /// <summary>Re-evaluate the choices / labels delegates.</summary>
        void RefreshChoices();

        int ChoiceCount { get; }
        string GetChoice(int index);
        string GetLabel(int index);

        Action<IUIValueEvent> OnValueChanged { get; set; }
        Action<int> OnScroll { get; set; }
    }

    /// <summary>A horizontal slider over a numeric range.</summary>
    public interface IUISlider : IUIElement
    {
        double Value { get; set; }
        double Min { get; set; }
        double Max { get; set; }

        /// <summary>Snap increment (0 = continuous).</summary>
        double Step { get; set; }

        Action<IUIValueEvent> OnValueChanged { get; set; }
    }

    /// <summary>Empty space, optionally drawn as a divider line.</summary>
    public interface IUISpacer : IUIElement
    {
        bool Line { get; set; }
    }

    // =================================================================================================================
    //  Custom components
    // =================================================================================================================

    /// <summary>
    /// Implement this in your mod to supply a fully custom element. The framework wraps it so it takes part in
    /// layout, hit-testing, focus, tooltips and event bubbling like a built-in element.
    /// </summary>
    public interface IUICustomComponent
    {
        /// <summary>Return the desired size given the available size.</summary>
        Vector2 Measure(Vector2 available);

        /// <summary>Draw with the absolute bounds already resolved. Called in the overlay pass instead when <see cref="WantsOverlay"/> is true.</summary>
        void Draw(SpriteBatch b, Rectangle bounds);

        /// <summary>Called every tick.</summary>
        void Update(Rectangle bounds, double elapsedMs);

        /// <summary>Return true if the click was handled (stops bubbling).</summary>
        bool OnClick(int x, int y, bool rightButton);

        /// <summary>Cursor entered (<paramref name="entered"/> = true), moved inside (true), or left (false).</summary>
        void OnHover(int x, int y, bool entered);

        /// <summary>Key pressed while focused. Return true if handled.</summary>
        bool OnKey(Keys key, bool shift, bool ctrl);

        /// <summary>Whether clicking gives this component keyboard focus.</summary>
        bool WantsFocus { get; }

        /// <summary>Draw in the overlay pass (on top of the whole menu) instead of in tree order; the component is then also hit-tested before the tree.</summary>
        bool WantsOverlay { get; }
    }

    // =================================================================================================================
    //  Menus
    // =================================================================================================================

    /// <summary>Initial settings for <see cref="IStardewUIApi.CreateMenu(string, IUIMenuOptions)"/>. All of these can also be changed later on the menu.</summary>
    public interface IUIMenuOptions
    {
        Func<string> Title { get; set; }

        /// <summary>Fixed width, or null to fit content.</summary>
        int? Width { get; set; }

        /// <summary>Fixed height, or null to fit content.</summary>
        int? Height { get; set; }

        bool ShowCloseButton { get; set; }

        /// <summary>Block clicks outside the menu (default true).</summary>
        bool Modal { get; set; }

        /// <summary>Darken the screen behind the menu (default true).</summary>
        bool DimBackground { get; set; }

        UIAnchor Anchor { get; set; }
        int X { get; set; }
        int Y { get; set; }

        /// <summary>Draw the vanilla dialogue box behind the content (default true).</summary>
        bool DrawBox { get; set; }

        /// <summary>Inner padding between the box border and the root container.</summary>
        int Padding { get; set; }

        /// <summary>Escape (or the menu key) closes the menu (default true).</summary>
        bool CloseOnEscape { get; set; }

        // HUD
        /// <summary>Let the player move, resize and collapse the window; the result persists per save (default true).</summary>
        bool PlayerLayout { get; set; }
    }

    /// <summary>A screen. Build its tree under <see cref="Root"/>, then <see cref="Open"/>.</summary>
    public interface IUIMenu
    {
        string Id { get; }

        /// <summary>Root container (a vertical <see cref="IUIStack"/>).</summary>
        IUIStack Root { get; }

        bool IsOpen { get; }

        Func<string> Title { get; set; }
        int? Width { get; set; }
        int? Height { get; set; }
        bool ShowCloseButton { get; set; }
        bool Modal { get; set; }
        bool DimBackground { get; set; }
        UIAnchor Anchor { get; set; }
        int X { get; set; }
        int Y { get; set; }
        bool DrawBox { get; set; }
        int Padding { get; set; }
        bool CloseOnEscape { get; set; }

        /// <summary>Absolute bounds of the menu box, valid while open.</summary>
        Rectangle Bounds { get; }

        /// <summary>Button clicked when Enter is pressed and no focused element consumed it.</summary>
        IUIButton DefaultButton { get; set; }

        /// <summary>Button clicked when Escape is pressed (instead of closing).</summary>
        IUIButton CancelButton { get; set; }

        Action<IUIMenu> OnOpen { get; set; }
        Action<IUIMenu> OnClose { get; set; }

        /// <summary>Every tick while open, with elapsed milliseconds.</summary>
        Action<IUIMenu, double> OnUpdate { get; set; }

        /// <summary>Key presses not handled by any element. Return true to mark handled.</summary>
        Func<IUIKeyEvent, bool> OnKey { get; set; }

        /// <summary>Scroll wheel not handled by any element (positive = up).</summary>
        Action<int> OnScroll { get; set; }

        /// <summary>Open as the active menu. Does nothing unless the player is free, or <paramref name="force"/> is true.</summary>
        void Open(bool force);

        /// <summary>Open as a child of another (open) framework menu.</summary>
        void OpenAsChild(IUIMenu parent);

        void Close();

        void InvalidateLayout();

        /// <summary>Find an element by id anywhere in the tree, or null.</summary>
        IUIElement Find(string id);

        /// <summary>Move the menu (sets <see cref="Anchor"/> to Explicit).</summary>
        void SetPosition(int x, int y);

        // HUD
        /// <summary>
        /// Let the player drag the window by its title strip, collapse it with the button next to the close button and
        /// (when <see cref="Width"/> and <see cref="Height"/> are fixed) resize it from the bottom-right corner. The
        /// result is saved per save file and re-applied whenever the menu opens (default true; needs <see cref="DrawBox"/>).
        /// </summary>
        bool PlayerLayout { get; set; }
    }

    // =================================================================================================================
    //  The API
    // =================================================================================================================

    /// <summary>Entry point. One instance per consumer mod; every id you register is private to your mod.</summary>
    public interface IStardewUIApi
    {
        /// <summary>Semantic version of this API.</summary>
        string ApiVersion { get; }

        // ---- Menus ----

        IUIMenuOptions CreateMenuOptions();
        IUIMenu CreateMenu(string id, IUIMenuOptions options);
        IUIMenu CreateMenu(string id);
        IUIMenu GetMenu(string id);
        void DestroyMenu(string id);
        void OpenMenu(string id);
        void CloseMenu(string id);
        bool IsOpen(string id);

        // ---- Containers ----

        IUIStack AddStack(IUIContainer parent, string id, bool horizontal, int spacing);
        IUIGrid AddGrid(IUIContainer parent, string id, string columns, string rows);
        IUIPanel AddPanel(IUIContainer parent, string id, bool drawBox, int padding);
        IUICanvas AddCanvas(IUIContainer parent, string id);
        IUIScrollView AddScrollView(IUIContainer parent, string id, int viewportHeight);
        IUIList AddList(IUIContainer parent, string id, int rowHeight, int visibleRows, Func<int> itemCount, Action<int, IUIContainer> buildRow);

        // ---- Leaves ----

        IUILabel AddLabel(IUIContainer parent, string id, Func<string> text);
        IUIImage AddImage(IUIContainer parent, string id, Texture2D texture, Rectangle? source, float scale);
        IUIButton AddButton(IUIContainer parent, string id, Func<string> text, Action<IUIClickEvent> onClick);
        IUICheckbox AddCheckbox(IUIContainer parent, string id, Func<bool> get, Action<bool> set);
        IUITextInput AddTextInput(IUIContainer parent, string id, Func<string> get, Action<string> set);
        IUINumberInput AddNumberInput(IUIContainer parent, string id, Func<double> get, Action<double> set, double min, double max, double step, bool clamp);
        IUIDropdown AddDropdown(IUIContainer parent, string id, Func<string[]> choices, Func<string[]> labels, Func<string> get, Action<string> set);
        IUISlider AddSlider(IUIContainer parent, string id, Func<double> get, Action<double> set, double min, double max);
        IUISpacer AddSpacer(IUIContainer parent, string id, int width, int height);
        IUIElement AddCustom(IUIContainer parent, string id, IUICustomComponent implementation);

        // ---- Lookup / tree ----

        IUIElement Find(IUIMenu menu, string id);
        void Remove(IUIElement element);
        void InvalidateLayout(IUIMenu menu);

        // ---- Input ----

        /// <summary>Run <paramref name="onPressed"/> when the keybind list (e.g. <c>"F8"</c>, <c>"LeftControl + F8, LeftShift + F9"</c>) is pressed.</summary>
        void RegisterHotkey(string id, string keybindList, Action onPressed);
        void UnregisterHotkey(string id);

        /// <summary>Toggle the menu open/closed when the keybind list is pressed (pass an empty string to unbind).</summary>
        void BindToggleHotkey(IUIMenu menu, string keybindList);

        // ---- Style / config ----

        /// <summary>Tooltip delay for this consumer's menus (pass a negative value to reset to the framework default).</summary>
        void SetTooltipDelay(int milliseconds);
        IUIStyle CreateStyle();
        void SetDefaultStyle(IUIStyle style);

        // ---- v1.1 additions (additive; consumers may copy a subset) ----

        // BEGIN SLOTS members

        // ---- Extension slots (architecture.md §16.1) ----

        /// <summary>
        /// Declare an extension slot in one of your menus: a container other mods can contribute elements to through
        /// <see cref="ContributeTo(string, string, string, Action{IUIContainer, IUIScreenContext})"/>. Slot ids are
        /// unique per menu; other mods address the slot as (your mod id, menu id, slot id). The slot is emptied and
        /// rebuilt from the registered contributions every time the menu opens, before layout.
        /// </summary>
        IUISlot AddSlot(IUIContainer parent, string id);

        /// <summary>Every slot declared by <paramref name="ownerModId"/>'s menus, with their layout hints (empty array if none).</summary>
        IUISlotInfo[] ListSlots(string ownerModId);

        /// <summary>
        /// Contribute elements to another mod's slot (or one of your own) with priority 0. <paramref name="build"/> runs
        /// every time the owning menu opens: it receives a container of its own inside the slot (id
        /// <c>"&lt;slotId&gt;.&lt;yourModId&gt;"</c>) to add elements to through this API instance, and the owner's
        /// <see cref="IUIScreenContext"/>. One contribution per (mod, slot): calling again replaces it. The registration
        /// is kept even if the owner's menu does not exist yet. A build callback that throws is logged and muted.
        /// </summary>
        void ContributeTo(string ownerModId, string menuId, string slotId, Action<IUIContainer, IUIScreenContext> build);

        /// <summary>Like <see cref="ContributeTo(string, string, string, Action{IUIContainer, IUIScreenContext})"/>; contributions are ordered by ascending <paramref name="priority"/>, then by mod id.</summary>
        void ContributeTo(string ownerModId, string menuId, string slotId, int priority, Action<IUIContainer, IUIScreenContext> build);

        /// <summary>Remove your contribution to a slot (takes effect the next time the menu opens).</summary>
        void RemoveContribution(string ownerModId, string menuId, string slotId);

        /// <summary>Expose a string value of one of your menus to contributors (<see cref="IUIScreenContext.GetString"/>). Calling again replaces it; null removes it.</summary>
        void Expose(IUIMenu menu, string key, Func<string> value);

        /// <summary>Expose a numeric value of one of your menus to contributors (<see cref="IUIScreenContext.GetNumber"/>).</summary>
        void ExposeNumber(IUIMenu menu, string key, Func<double> value);

        /// <summary>Expose a boolean value of one of your menus to contributors (<see cref="IUIScreenContext.GetBool"/>).</summary>
        void ExposeBool(IUIMenu menu, string key, Func<bool> value);

        /// <summary>Expose a command of one of your menus to contributors (<see cref="IUIScreenContext.Invoke"/>). Calling again replaces it; null removes it.</summary>
        void ExposeCommand(IUIMenu menu, string key, Action command);

        /// <summary>Raise an event of one of your menus to every contributor that subscribed to it (<see cref="IUIScreenContext.Subscribe"/>); each handler is guarded.</summary>
        void Publish(IUIMenu menu, string eventName);

        /// <summary>
        /// Inspect and adjust another mod's menu (or one of your own) after it is built: <paramref name="decorate"/> runs
        /// every time the menu opens, after all slot contributions and before layout, so it sees the final tree. Use
        /// <see cref="IUIMenu.Find"/> to reach elements; sealed elements (<see cref="IUIElement.Sealed"/>) are hidden from
        /// it. Registering makes your API instance a decorator of that menu, allowed to add elements outside sealed
        /// subtrees. One decorator per (mod, menu): calling again replaces it; null removes it.
        /// </summary>
        void OnScreenBuilt(string ownerModId, string menuId, Action<IUIMenu> decorate);

        // END SLOTS members

        // BEGIN COMPOSITES members

        /// <summary>Create an empty argument bag for <see cref="AddComposite"/>.</summary>
        IUICompositeArgs CreateCompositeArgs();

        /// <summary>
        /// Register a reusable composite under a global name (convention: <c>"&lt;ModId&gt;.&lt;Name&gt;"</c>). Any mod can
        /// then instantiate it with <see cref="AddComposite"/>. Defining an existing name replaces it.
        /// </summary>
        void DefineComposite(string name, Action<IUICompositeHost, IUICompositeArgs> build);

        /// <summary>Whether a composite with that name is defined (by any mod).</summary>
        bool HasComposite(string name);

        /// <summary>Names of every defined composite.</summary>
        string[] ListComposites();

        /// <summary>Remove a composite this mod defined (definitions of other mods are left alone).</summary>
        void UndefineComposite(string name);

        /// <summary>
        /// Instantiate a composite: a host container is added under <paramref name="parent"/> and the composite's
        /// builder fills it. When no composite has that name yet the host stays empty (logged) until
        /// <see cref="IUIComposite.Rebuild"/> is called after it was defined.
        /// </summary>
        IUIComposite AddComposite(IUIContainer parent, string id, string compositeName, IUICompositeArgs args);

        /// <summary>
        /// A custom component that embeds built-in elements: <paramref name="build"/> is called once with a container
        /// (<c>"&lt;id&gt;.host"</c>) the component owns. The implementation draws first (its chrome), then the host's
        /// children; the element measures as the larger of the two (the host alone when the implementation reports no size).
        /// </summary>
        IUIElement AddCustom(IUIContainer parent, string id, IUICustomComponent implementation, Action<IUIContainer> build);

        // END COMPOSITES members

        // BEGIN RICHTEXT members
        /// <summary>Create an empty rich tooltip builder; assign it to <see cref="IUIElement.RichTooltip"/>.</summary>
        IUITooltip CreateTooltip();
        // END RICHTEXT members

        // BEGIN THEME members

        /// <summary>Names of the themes defined in the <c>Mods/6135.UIFramework/Themes</c> asset (Content Patcher packs can add to it).</summary>
        string[] ListThemes();

        /// <summary>Name of the theme every framework menu currently uses.</summary>
        string ActiveTheme { get; }

        /// <summary>Switch every framework menu to <paramref name="name"/> (one of <see cref="ListThemes"/>) and save it in the framework's config.</summary>
        void SetTheme(string name);

        /// <summary>A color of the active theme: <c>text</c>, <c>disabled-text</c>, <c>hover</c>, <c>scrollbar</c>, <c>border</c> (unknown keys return the text color).</summary>
        Color ThemeColor(string key);

        /// <summary>True when the player asked for reduced motion; custom components should skip their own animations then.</summary>
        bool ReducedMotion { get; }

        /// <summary>Speak <paramref name="text"/> through the screen reader (Stardew Access) when one is installed; otherwise nothing happens.</summary>
        void Announce(string text);

        // END THEME members

        // BEGIN DATAGRID members

        /// <summary>
        /// A virtualized table: add columns with <see cref="IUIDataGrid.AddColumn"/>, then each column reads its cell
        /// text for a row index through <see cref="IUIDataGridColumn.Text"/>. <paramref name="rowCount"/> is re-queried
        /// every tick and on <see cref="IUIDataGrid.Refresh"/>.
        /// </summary>
        IUIDataGrid AddDataGrid(IUIContainer parent, string id, int rowHeight, int visibleRows, Func<int> rowCount);

        // END DATAGRID members

        // BEGIN SIGNALS members

        // ---- Signals (architecture.md §16.2): reactive values with automatic dependency tracking ----

        /// <summary>Create a signal holding a string (it can also be read / written as a number or a flag).</summary>
        IUISignal Signal(string initial);

        /// <summary>Create a signal holding a number.</summary>
        IUISignal SignalNumber(double initial);

        /// <summary>Create a signal holding a flag.</summary>
        IUISignal SignalBool(bool initial);

        /// <summary>
        /// Create a lazy, cached value derived from other signals / computeds. Every signal read while
        /// <paramref name="compute"/> runs becomes a dependency; the computed is invalidated when any of them changes.
        /// </summary>
        IUIComputed Computed(Func<string> compute);

        /// <summary>Numeric variant of <see cref="Computed"/>.</summary>
        IUIComputed ComputedNumber(Func<double> compute);

        /// <summary>Boolean variant of <see cref="Computed"/>.</summary>
        IUIComputed ComputedBool(Func<bool> compute);

        /// <summary>Show <paramref name="source"/> in the label; the label only re-flows when the computed's version changes.</summary>
        void BindText(IUILabel label, IUIComputed source);

        /// <summary>Show <paramref name="source"/> in the label; the label only re-flows when the signal's version changes.</summary>
        void BindTextToSignal(IUILabel label, IUISignal source);

        /// <summary>Drive <see cref="IUIElement.Visible"/> from the computed's <see cref="IUIComputed.Flag"/>.</summary>
        void BindVisible(IUIElement element, IUIComputed source);

        /// <summary>Drive <see cref="IUIElement.Enabled"/> from the computed's <see cref="IUIComputed.Flag"/>.</summary>
        void BindEnabled(IUIElement element, IUIComputed source);

        /// <summary>Two-way: the input reads the signal instead of its original getter and writes it when edited (the setter it was created with is still called).</summary>
        void BindTextInput(IUITextInput input, IUISignal signal);

        /// <summary>Two-way: the input shows the signal's <see cref="IUISignal.Number"/> and writes it when edited.</summary>
        void BindNumberInput(IUINumberInput input, IUISignal signal);

        /// <summary>Two-way: the checkbox shows the signal's <see cref="IUISignal.Flag"/> and writes it when toggled.</summary>
        void BindCheckbox(IUICheckbox input, IUISignal signal);

        /// <summary>Two-way: the slider shows the signal's <see cref="IUISignal.Number"/> and writes it when moved.</summary>
        void BindSlider(IUISlider input, IUISignal signal);

        /// <summary>Two-way: the dropdown selects the choice equal to the signal's <see cref="IUISignal.Value"/> and writes it when changed.</summary>
        void BindDropdown(IUIDropdown input, IUISignal signal);

        /// <summary>Drop every binding on <paramref name="element"/> (restoring the original value delegates). Bindings are also dropped when the element leaves its menu.</summary>
        void Unbind(IUIElement element);

        // ---- Auto-forms (architecture.md §16.2): a Save / Cancel / Undo / Redo form generated from a plain object ----

        /// <summary>
        /// Generate a two-column form from the public read / write properties of <paramref name="model"/> (in declaration
        /// order): <c>bool</c> → checkbox, numbers → number input, <c>string</c> → text input (or dropdown with a
        /// <c>[Choices]</c> attribute), enums → dropdown; other types are skipped. Attributes are matched by name so you
        /// can declare your own: <c>Range(min, max)</c>, <c>Choices(string[] Values / string Csv)</c>, <c>Section(title)</c>,
        /// <c>Tooltip(text)</c>, <c>DisplayName</c> / <c>Display(Name)</c>, <c>ReadOnly</c>. A method <c>Validate&lt;Property&gt;()</c>
        /// returning <c>bool</c> or an error <c>string</c> (optionally taking the prospective value) runs before each write.
        /// </summary>
        IUIForm AddForm(IUIContainer parent, string id, object model);

        // END SIGNALS members

        // BEGIN HUD members

        // ---- HUD widgets and toasts ----

        /// <summary>Create (or replace) a HUD widget: a non-modal overlay drawn during gameplay. Build its tree under <see cref="IUIHud.Root"/>.</summary>
        IUIHud CreateHud(string id);

        /// <summary>Look up one of your HUD widgets, or null.</summary>
        IUIHud GetHud(string id);

        /// <summary>Remove a HUD widget.</summary>
        void DestroyHud(string id);

        /// <summary>Show a short notification in the bottom-left corner for 3.5 seconds.</summary>
        void ShowToast(string text);

        /// <summary>Show a short notification in the bottom-left corner for <paramref name="durationMs"/> milliseconds.</summary>
        void ShowToast(string text, int durationMs);

        /// <summary>Show a notification with an icon (<paramref name="source"/> null = whole texture).</summary>
        void ShowToastWithIcon(string text, Texture2D icon, Rectangle? source, int durationMs);

        /// <summary>Forget the player's saved position / size / collapsed state for <paramref name="menu"/> and restore the values you set.</summary>
        void ResetPlayerLayout(IUIMenu menu);

        // END HUD members

        // BEGIN ITEMIMAGE members (v1.2)

        /// <summary>
        /// Add an element that draws an item instance with the game's own <c>drawInMenu</c>, so flavored goods (wine,
        /// jelly, pickles...) keep their color tint. <paramref name="item"/> is read every frame; null draws nothing.
        /// Measures 16 x 16 UI pixels at <paramref name="scale"/> 1 (4 = a vanilla 64 px slot).
        /// </summary>
        IUIItemImage AddItemImage(IUIContainer parent, string id, Func<Item> item, float scale);

        // END ITEMIMAGE members

    }

    // =================================================================================================================
    //  v1.1 types (one region per feature; see architecture.md §16)
    // =================================================================================================================

    // BEGIN SLOTS types

    /// <summary>
    /// An extension slot (<see cref="IStardewUIApi.AddSlot"/>): a stack-like container the framework fills with one
    /// child container per contributing mod whenever the menu opens. The framework manages
    /// <see cref="IUIElement.Visible"/>: the slot is shown while it has contributions and <see cref="VisiblePredicate"/>
    /// (if any) returns true.
    /// </summary>
    public interface IUISlot : IUIContainer
    {
        /// <summary>Layout hint: lay contributions out in a row instead of a column (default false).</summary>
        bool Horizontal { get; set; }

        /// <summary>Layout hint: cap the slot's measured height in UI pixels (null = unlimited).</summary>
        int? MaxHeight { get; set; }

        /// <summary>Evaluated every tick; false hides the slot (null = always visible).</summary>
        Func<bool> VisiblePredicate { get; set; }

        /// <summary>Most contributions accepted, in priority order (0 = unlimited).</summary>
        int MaxContributions { get; set; }

        /// <summary>Mod ids whose contributions are skipped (null = none).</summary>
        string[] VetoedContributors { get; set; }
    }

    /// <summary>A declared slot as reported by <see cref="IStardewUIApi.ListSlots"/>.</summary>
    public interface IUISlotInfo
    {
        string OwnerModId { get; }
        string MenuId { get; }
        string SlotId { get; }
        bool Horizontal { get; }
        int? MaxHeight { get; }
    }

    /// <summary>
    /// What a menu owner chose to share with contributors: values, commands and events published through the
    /// <c>Expose*</c> / <c>Publish</c> members of <see cref="IStardewUIApi"/>. Nothing else of the owner is reachable.
    /// Values are read live (the owner's delegate runs on every call).
    /// </summary>
    public interface IUIScreenContext
    {
        /// <summary>Mod that owns the menu.</summary>
        string OwnerModId { get; }

        /// <summary>Id of the menu (unique within the owner).</summary>
        string MenuId { get; }

        /// <summary>Keys of every exposed value (string, number and bool).</summary>
        string[] Keys { get; }

        /// <summary>Whether a value (of any kind) is exposed under <paramref name="key"/>.</summary>
        bool HasValue(string key);

        /// <summary>Exposed string value; numbers and bools are converted (invariant culture). Empty string when unknown.</summary>
        string GetString(string key);

        /// <summary>Exposed number; strings are parsed (invariant culture) and bools map to 1 / 0. 0 when unknown.</summary>
        double GetNumber(string key);

        /// <summary>Exposed bool; strings are parsed and numbers are true when non-zero. False when unknown.</summary>
        bool GetBool(string key);

        /// <summary>Whether the owner exposed <paramref name="command"/>.</summary>
        bool HasCommand(string command);

        /// <summary>Run an exposed command (no-op when unknown; a faulting command is logged against the owner and muted).</summary>
        void Invoke(string command);

        /// <summary>Run <paramref name="handler"/> whenever the owner publishes <paramref name="eventName"/>.</summary>
        void Subscribe(string eventName, Action handler);

        /// <summary>Stop a handler registered through <see cref="Subscribe"/>.</summary>
        void Unsubscribe(string eventName, Action handler);
    }

    // END SLOTS types

    // BEGIN COMPOSITES types

    /// <summary>
    /// String-keyed bag of proxy-safe values handed to a composite's builder. Getters return a default
    /// (<c>""</c>, 0, false, null) when the key is missing or holds a value of another type.
    /// </summary>
    public interface IUICompositeArgs
    {
        void SetString(string key, string value);
        void SetNumber(string key, double value);
        void SetBool(string key, bool value);
        void SetAction(string key, Action value);
        void SetGetter(string key, Func<string> value);
        void SetSetter(string key, Action<string> value);
        void SetNumberGetter(string key, Func<double> value);
        void SetNumberSetter(string key, Action<double> value);

        /// <summary>Store any object; it crosses the API unproxied, so only share types both mods know.</summary>
        void SetObject(string key, object value);

        string GetString(string key);
        double GetNumber(string key);
        bool GetBool(string key);
        Action GetAction(string key);
        Func<string> GetGetter(string key);
        Action<string> GetSetter(string key);
        Func<double> GetNumberGetter(string key);
        Action<double> GetNumberSetter(string key);
        object GetObject(string key);

        /// <summary>Whether any value is stored under <paramref name="key"/>.</summary>
        bool Has(string key);

        /// <summary>Every stored key.</summary>
        string[] Keys { get; }
    }

    /// <summary>
    /// The container a composite builder fills (see <see cref="IStardewUIApi.DefineComposite"/>). Children are laid
    /// out in a column. What the builder exposes here is what the composite's user reaches through <see cref="IUIComposite"/>.
    /// </summary>
    public interface IUICompositeHost : IUIContainer
    {
        /// <summary>Publish a read-only string value under <paramref name="key"/>.</summary>
        void Expose(string key, Func<string> value);

        /// <summary>Publish a read-only number under <paramref name="key"/>.</summary>
        void ExposeNumber(string key, Func<double> value);

        /// <summary>Publish a read-only boolean under <paramref name="key"/>.</summary>
        void ExposeBool(string key, Func<bool> value);

        /// <summary>Publish a command the user can run with <see cref="IUIComposite.Invoke"/>.</summary>
        void ExposeCommand(string key, Action command);

        /// <summary>Notify every <see cref="IUIComposite.Subscribe"/> handler of <paramref name="eventName"/>.</summary>
        void Publish(string eventName);
    }

    /// <summary>
    /// An instance of a composite (see <see cref="IStardewUIApi.AddComposite"/>). It is the same container the
    /// builder filled, plus the values, commands and events the builder exposed.
    /// </summary>
    public interface IUIComposite : IUIContainer
    {
        /// <summary>The global name it was created from.</summary>
        string CompositeName { get; }

        /// <summary>The arguments it was created with (edit them and <see cref="Rebuild"/> to apply).</summary>
        IUICompositeArgs Args { get; }

        /// <summary>Clear the children and run the builder again.</summary>
        void Rebuild();

        /// <summary>Read an exposed string value (empty when not exposed).</summary>
        string GetValue(string key);

        /// <summary>Read an exposed number (0 when not exposed).</summary>
        double GetNumber(string key);

        /// <summary>Read an exposed boolean (false when not exposed).</summary>
        bool GetBool(string key);

        /// <summary>Run an exposed command (no-op when not exposed).</summary>
        void Invoke(string command);

        /// <summary>Whether the builder exposed <paramref name="command"/>.</summary>
        bool HasCommand(string command);

        /// <summary>Run <paramref name="handler"/> whenever the composite publishes <paramref name="eventName"/>.</summary>
        void Subscribe(string eventName, Action handler);
    }

    // END COMPOSITES types

    // BEGIN RICHTEXT types

    /// <summary>
    /// A tooltip built from blocks, drawn in a vanilla box sized to its content. Every method returns the builder so
    /// calls chain; line text supports the <see cref="IUILabel.RichText"/> markup. Delegates are evaluated each frame the
    /// tooltip is visible.
    /// </summary>
    public interface IUITooltip
    {
        /// <summary>Bold title drawn in the dialogue font.</summary>
        IUITooltip Title(Func<string> title);

        /// <summary>A line of (rich) text in the default text color.</summary>
        IUITooltip Line(Func<string> text);

        /// <summary>A line of (rich) text in <paramref name="color"/>.</summary>
        IUITooltip Line(Func<string> text, Color color);

        /// <summary>A block icon drawn on its own row.</summary>
        IUITooltip Icon(Texture2D texture, Rectangle? source, float scale);

        /// <summary>A vanilla item's sprite and display name on one row (qualified id, e.g. <c>(O)24</c>).</summary>
        IUITooltip Item(string qualifiedItemId);

        /// <summary>
        /// (v1.2) An item instance's sprite, drawn with <c>drawInMenu</c> so tints are kept, and its display name on one
        /// row (e.g. "Starfruit Wine"). <paramref name="item"/> is read each frame; null skips the row.
        /// </summary>
        IUITooltip ItemInstance(Func<Item> item);

        /// <summary>A horizontal rule.</summary>
        IUITooltip Divider();

        /// <summary>A coin icon followed by the amount, like vanilla shop tooltips.</summary>
        IUITooltip Money(Func<int> amount);

        /// <summary>Wrap lines wider than this many UI pixels (0 = only the screen limits the width).</summary>
        IUITooltip MaxWidth(int px);

        /// <summary>Remove every block.</summary>
        IUITooltip Clear();
    }
    // END RICHTEXT types

    // BEGIN DATAGRID types

    /// <summary>Click on a data grid row (see <see cref="IUIDataGrid.OnRowClick"/>).</summary>
    public interface IUIRowEvent : IUIClickEvent
    {
        /// <summary>Underlying row index (the index handed to the column delegates), not the display position.</summary>
        int Row { get; }
    }

    /// <summary>One column of an <see cref="IUIDataGrid"/>. Every row delegate receives the underlying row index.</summary>
    public interface IUIDataGridColumn
    {
        /// <summary>Id given to <see cref="IUIDataGrid.AddColumn"/>.</summary>
        string Id { get; }

        /// <summary>Header text, evaluated every frame.</summary>
        Func<string> Header { get; set; }

        /// <summary>Track width: <c>auto</c>, <c>120px</c>, <c>*</c> or <c>2*</c> (star columns share the leftover width). A drag resize turns it into pixels.</summary>
        string Width { get; set; }

        /// <summary>Clicking the header sorts by this column (toggles ascending / descending).</summary>
        bool Sortable { get; set; }

        /// <summary>The divider right of the header can be dragged to resize the column.</summary>
        bool Resizable { get; set; }

        /// <summary>Smallest width in UI pixels (resize floor, also applied to the resolved track).</summary>
        int MinWidth { get; set; }

        /// <summary>Horizontal alignment of the header and of the default text cells.</summary>
        UIAlign Align { get; set; }

        /// <summary>Cell text for a row index (used when <see cref="BuildCell"/> is null; also the default sort key).</summary>
        Func<int, string> Text { get; set; }

        /// <summary>String sort key for a row index (null = sort by <see cref="Text"/>). Ignored when <see cref="SortNumber"/> is set.</summary>
        Func<int, string> SortKey { get; set; }

        /// <summary>Numeric sort key for a row index; when set the column sorts numerically.</summary>
        Func<int, double> SortNumber { get; set; }

        /// <summary>Custom cell renderer: build elements into the cell container instead of a text label.</summary>
        Action<int, IUIContainer> BuildCell { get; set; }

        /// <summary>Tooltip for a cell (null = none; an empty string hides the tooltip for that row).</summary>
        Func<int, string> CellTooltip { get; set; }
    }

    /// <summary>
    /// Virtualized table with a header row, sortable / resizable columns, filtering, row selection and keyboard
    /// navigation. Rows are addressed by their <b>underlying</b> index (0 .. rowCount - 1, as the data source knows
    /// them); sorting and filtering only change the display order. Only the visible rows exist as elements.
    /// </summary>
    public interface IUIDataGrid : IUIContainer
    {
        int RowHeight { get; set; }
        int VisibleRows { get; set; }

        /// <summary>Display position (after filter / sort) of the first visible row.</summary>
        int FirstVisibleIndex { get; set; }

        /// <summary>Number of rows shown after filtering (at the last refresh).</summary>
        int RowCount { get; }

        // ---- columns ----

        /// <summary>Append a column; <paramref name="width"/> uses the track syntax (<c>auto</c>, <c>120px</c>, <c>*</c>, <c>2*</c>).</summary>
        IUIDataGridColumn AddColumn(string id, Func<string> header, string width);

        /// <summary>Remove a column by id (no-op when unknown).</summary>
        void RemoveColumn(string columnId);

        int ColumnCount { get; }
        IUIDataGridColumn GetColumn(int index);

        /// <summary>Find a column by id, or null.</summary>
        IUIDataGridColumn FindColumn(string columnId);

        /// <summary>Raised after a drag resize with the column id and its new width in pixels.</summary>
        Action<string, int> OnColumnResized { get; set; }

        // ---- sorting / filtering ----

        /// <summary>Id of the column the rows are sorted by, or an empty string.</summary>
        string SortColumn { get; }

        bool SortDescending { get; }

        /// <summary>Sort by a column (an unknown id clears the sort).</summary>
        void Sort(string columnId, bool descending);

        void ClearSort();

        /// <summary>Row predicate (underlying index); null shows every row. Call <see cref="Refresh"/> after changing what it returns.</summary>
        Func<int, bool> Filter { get; set; }

        /// <summary>Re-query the row count, re-run the filter and sort, rebuild the visible rows. Selection is kept by underlying index.</summary>
        void Refresh();

        // ---- selection ----

        bool Selectable { get; set; }

        /// <summary>Ctrl-click toggles a row, Shift-click selects a range.</summary>
        bool MultiSelect { get; set; }

        /// <summary>Primary selected row (underlying index) or -1. Setting it replaces the selection without raising events.</summary>
        int SelectedRow { get; set; }

        /// <summary>Every selected row (underlying indices) in display order. Setting it replaces the selection without raising events.</summary>
        int[] SelectedRows { get; set; }

        void ClearSelection();

        /// <summary>Raised when the user changes the selection (<see cref="IUIValueEvent.OldIndex"/> / <see cref="IUIValueEvent.NewIndex"/> are underlying indices of the primary row).</summary>
        Action<IUIValueEvent> OnValueChanged { get; set; }

        /// <summary>Raised for every click on a row (left or right), after selection was applied.</summary>
        Action<IUIRowEvent> OnRowClick { get; set; }

        /// <summary>Raised on a double-click on a row or Enter with a selected row; the argument is the underlying index.</summary>
        Action<int> OnRowActivated { get; set; }

        /// <summary>Rich tooltip for a whole row (underlying index), built with <see cref="IStardewUIApi.CreateTooltip"/>; null = none. Evaluated when the row is (re)built.</summary>
        Func<int, IUITooltip> RowTooltip { get; set; }

        /// <summary>Raised after scrolling; argument is the row delta.</summary>
        Action<int> OnScroll { get; set; }

        /// <summary>Scroll so the underlying row is visible (no-op when it is filtered out).</summary>
        void ScrollToRow(int row);

        // ---- sounds (null = default, empty = silent) ----

        string ScrollSound { get; set; }
        string SelectSound { get; set; }
        string SortSound { get; set; }
    }

    // END DATAGRID types

    // BEGIN SIGNALS types

    /// <summary>
    /// A reactive value. One stored value is exposed through three typed views; the setter used last decides the
    /// "kind" and the other views convert from it (a number reads as its invariant text and as <c>true</c> when
    /// non-zero, text reads as a number when it parses, a flag reads as <c>True</c> / <c>False</c> and 1 / 0).
    /// Reading a signal while a <see cref="IUIComputed"/> is computing registers it as a dependency.
    /// </summary>
    public interface IUISignal
    {
        string Value { get; set; }
        double Number { get; set; }
        bool Flag { get; set; }

        /// <summary>Incremented on every change; bound elements re-render only when it moves.</summary>
        int Version { get; }

        /// <summary>Run <paramref name="handler"/> after the value changed (once per change, after the write completed).</summary>
        void Subscribe(Action handler);

        void Unsubscribe(Action handler);
    }

    /// <summary>
    /// A value derived from signals (and other computeds) with automatic dependency tracking. Lazy and cached: it is
    /// recomputed on the next read after a dependency changed. A dependency cycle is logged and the stale value kept.
    /// </summary>
    public interface IUIComputed
    {
        string Value { get; }
        double Number { get; }
        bool Flag { get; }

        /// <summary>Incremented whenever the computed is invalidated by a dependency.</summary>
        int Version { get; }

        /// <summary>Run <paramref name="handler"/> after the computed was invalidated (once per batch of changes).</summary>
        void Subscribe(Action handler);

        void Unsubscribe(Action handler);
    }

    /// <summary>
    /// A form generated by <see cref="IStardewUIApi.AddForm"/>. Edits write straight into the model; the form keeps a
    /// snapshot for <see cref="Cancel"/> and an undo / redo history of every committed change. Ctrl+Z / Ctrl+Y while a
    /// field is focused undo / redo.
    /// </summary>
    public interface IUIForm : IUIContainer
    {
        /// <summary>True while any property differs from the last <see cref="Save"/> (or the initial snapshot).</summary>
        bool IsDirty { get; }

        bool CanUndo { get; }
        bool CanRedo { get; }
        void Undo();
        void Redo();

        /// <summary>Accept the current values: clears the dirty state and the history, raises <see cref="OnSaved"/>.</summary>
        void Save();

        /// <summary>Restore the snapshot into the model, refresh the controls, raise <see cref="OnCancelled"/>.</summary>
        void Cancel();

        /// <summary>Re-read the model into the controls (after changing it from code) and clear validation messages.</summary>
        void Refresh();

        Action<IUIForm> OnSaved { get; set; }
        Action<IUIForm> OnCancelled { get; set; }

        /// <summary>Raised after every committed edit, undo, redo or cancel.</summary>
        Action<IUIForm> OnChanged { get; set; }

        /// <summary>Show the Save / Cancel / Undo / Redo button row (default true).</summary>
        bool ShowButtons { get; set; }

        /// <summary>The input element generated for a property, or null.</summary>
        IUIElement FieldFor(string propertyName);
    }

    // END SIGNALS types

    // BEGIN HUD types

    /// <summary>
    /// A HUD widget: a tree of ordinary elements drawn over the world during gameplay (from <c>Display.RenderedHud</c>),
    /// never as a menu. It hides itself while any menu is open, during events / cutscenes and while the vanilla HUD is
    /// hidden. Add elements to <see cref="Root"/> with the usual <c>Add*</c> calls.
    /// </summary>
    public interface IUIHud
    {
        /// <summary>The id given to <see cref="IStardewUIApi.CreateHud"/>.</summary>
        string Id { get; }

        /// <summary>Root container (a vertical <see cref="IUIStack"/>).</summary>
        IUIStack Root { get; }

        /// <summary>Consumer switch; the widget is also hidden when <see cref="ShowWhen"/> returns false.</summary>
        bool Visible { get; set; }

        /// <summary>Screen corner / edge the widget hangs from (<see cref="UIAnchor.Explicit"/> uses <see cref="X"/> / <see cref="Y"/> verbatim).</summary>
        UIAnchor Anchor { get; set; }

        /// <summary>Horizontal offset in UI pixels added to the anchor position (positive = right).</summary>
        int X { get; set; }

        /// <summary>Vertical offset in UI pixels added to the anchor position (positive = down).</summary>
        int Y { get; set; }

        /// <summary>Fixed width, or null to fit content.</summary>
        int? Width { get; set; }

        /// <summary>Fixed height, or null to fit content.</summary>
        int? Height { get; set; }

        /// <summary>Draw a vanilla 9-slice panel box (with padding) behind the content (default true).</summary>
        bool DrawBox { get; set; }

        /// <summary>Opacity of the background box, 0..1 (default 1). Element content is always drawn opaque.</summary>
        float Opacity { get; set; }

        /// <summary>
        /// When true the widget receives hover and clicks while no menu is open (the game only loses the click when an
        /// element handled it) and the player can drag it to a new position, which persists per save.
        /// </summary>
        bool Interactive { get; set; }

        /// <summary>Evaluated every frame; return false to hide the widget (null = always shown).</summary>
        Func<bool> ShowWhen { get; set; }

        /// <summary>Every tick while shown, with elapsed milliseconds.</summary>
        Action<IUIHud, double> OnUpdate { get; set; }

        /// <summary>Absolute bounds of the widget box, valid after it was drawn once.</summary>
        Rectangle Bounds { get; }

        /// <summary>Find an element by id anywhere in the tree, or null.</summary>
        IUIElement Find(string id);

        /// <summary>Request a layout pass before the next draw.</summary>
        void InvalidateLayout();
    }

    // END HUD types

    // BEGIN ITEMIMAGE types (v1.2)

    /// <summary>What an <see cref="IUIItemImage"/> draws on top of the item sprite.</summary>
    public enum UIItemStack
    {
        /// <summary>Only the sprite.</summary>
        Hide,
        /// <summary>The quality star, but no stack number.</summary>
        Quality,
        /// <summary>The stack number (when above 1) and the quality star, like an inventory slot.</summary>
        NumberAndQuality
    }

    /// <summary>An item instance drawn with the game's <c>drawInMenu</c>. Created by <see cref="IStardewUIApi.AddItemImage"/>.</summary>
    public interface IUIItemImage : IUIElement
    {
        /// <summary>The item to draw, read every frame; null draws nothing.</summary>
        Func<Item> Item { get; set; }

        /// <summary>Size multiplier: 1 = 16 UI pixels, 4 = a vanilla 64 px slot. Ignored when an explicit width / height is set (the item then fits the bounds).</summary>
        float Scale { get; set; }

        /// <summary>Stack number / quality star overlay. Default <see cref="UIItemStack.Hide"/>. Overlays are hidden while the item is drawn smaller than 32 UI pixels (scale 2).</summary>
        UIItemStack Stack { get; set; }

        /// <summary>Draw the vanilla drop shadow under the item. Default false.</summary>
        bool DrawShadow { get; set; }

        /// <summary>Opacity from 0 to 1 (default 1); halved while the element is disabled.</summary>
        float Alpha { get; set; }

        /// <summary>Color multiplied into the sprite (default white).</summary>
        Color Tint { get; set; }
    }

    // END ITEMIMAGE types

}
