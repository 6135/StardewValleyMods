// ---------------------------------------------------------------------------------------------------------------------
//  UI Framework (6135.UIFramework) - public API.
//
//  Copy this file into your mod (you may change the namespace), request the API from SMAPI's mod registry with
//  GetApi<IStardewUIApi>("6135.UIFramework") once the game has launched, and list 6135.UIFramework
//  (MinimumVersion 1.0.0, IsRequired true) under Dependencies in your manifest.json.
//
//  Everything in this file is proxy-safe (SMAPI/Pintail): interfaces, enums, delegates, primitives and XNA / game types.
//  Members are only ever added, never renamed or removed; a breaking change would ship as IStardewUIApi2.
// ---------------------------------------------------------------------------------------------------------------------

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

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

        /// <summary>Tooltip delay for this consumer's menus (null-equivalent: pass a negative value to reset to the framework default).</summary>
        void SetTooltipDelay(int milliseconds);
        IUIStyle CreateStyle();
        void SetDefaultStyle(IUIStyle style);
    }
}
