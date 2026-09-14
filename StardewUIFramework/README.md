# UI Framework

**UI Framework** (`6135.UIFramework`) is a SMAPI library mod. Other mods use it to build in-game menus out of
reusable parts (labels, buttons, text and number inputs, checkboxes, dropdowns, sliders, scroll areas, virtualized
lists, tooltips) through a code-first C# builder API, and to run their own code when the player interacts with those
parts.

It does nothing on its own: install it because another mod lists it as a requirement.

> **Not the same as StardewUI.** [focustense's StardewUI](https://github.com/focustense/StardewUI) is a separate
> framework built around StarML markup and data binding. UI Framework is a code-first builder API: you call
> `AddButton(...)`, `AddGrid(...)` and so on from C#; there is no markup language. Mods that depend on one do not
> need the other.

Design notes and the implementation plan live in [architecture.md](architecture.md).

## For players

### What it is

A shared library. It provides menus, input handling and layout to mods that depend on it, and adds no gameplay,
items or screens of its own. If no installed mod requires it you can remove it.

### Installation

1. Install [SMAPI](https://smapi.io) 4.x (Stardew Valley 1.6 or later).
2. Optionally install [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) to edit the
   settings in-game.
3. Unzip the mod into `Stardew Valley/Mods` so that `Mods/UIFramework/manifest.json` exists.
4. Run the game through SMAPI.

### Configuration

The mod creates `Mods/UIFramework/config.json` on first launch. All options are also exposed through Generic Mod
Config Menu when it is installed.

| Key              | Default | Description                                                                               |
|------------------|---------|-------------------------------------------------------------------------------------------|
| `TooltipDelayMs` | `400`   | How long (milliseconds) the cursor must rest on an element before its tooltip appears.   |
| `DebugOverlay`   | `false` | Draw the bounds and ids of every element in framework menus (for mod authors).            |
| `LogCallbacks`   | `false` | Write a trace log line every time a consumer mod's callback is invoked (for mod authors). |

A mod that uses the framework can override the tooltip delay for its own menus; the config value is the default.

### Console commands

Type these in the SMAPI console.

| Command    | Effect                                                                                 |
|------------|----------------------------------------------------------------------------------------|
| `ui_debug` | Toggle the debug overlay (element bounds and ids) for the current session.             |
| `ui_list`  | List the framework menus that are currently open, with the mod that owns each of them. |

### Compatibility

- Stardew Valley 1.6+, SMAPI 4.0.0+ (`MinimumApiVersion` in the manifest).
- Works in single player, multiplayer and split-screen.
- No Harmony patches. The mod only reacts to SMAPI events and to the menus it opens itself.
- Textures come from the vanilla content pipeline (`LooseSprites/textBox`, `Game1.mouseCursors`,
  `Game1.menuTexture`) plus a bundled `assets/text_box_small.png`, so Content Patcher packs can retexture them.

## For modders

### Getting started

1. Copy [`Api/Public/IStardewUIApi.cs`](Api/Public/IStardewUIApi.cs) into your project. It is the single file
   consumers need; you may change its namespace. Do **not** add a project or DLL reference to the framework.
2. Declare the dependency in your `manifest.json`:

   ```json
   "Dependencies": [ { "UniqueID": "6135.UIFramework", "MinimumVersion": "1.0.0", "IsRequired": true } ]
   ```

3. Request the API once the game has launched:

   ```csharp
   private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
   {
       IStardewUIApi? ui = Helper.ModRegistry.GetApi<IStardewUIApi>("6135.UIFramework");
       if (ui == null)
       {
           Monitor.Log("UI Framework is not installed.", LogLevel.Warn);
           return;
       }
       // build menus here
   }
   ```

SMAPI generates a proxy (Pintail) that maps your copy of the interface onto the framework's object. Every mod gets
its own API instance, so the ids you pick for menus, elements and hotkeys are private to your mod.

### Minimal example

A menu with a label and a button, toggled with F9. `Root` is a vertical stack, so the two elements appear one above
the other. The label text is a delegate, so it updates on its own every frame.

```csharp
using StardewModdingAPI;
using StardewModdingAPI.Events;
using UIFramework.Api; // the namespace of your copy of IStardewUIApi.cs

public class ModEntry : Mod
{
    private IUIMenu? menu;
    private int clicks;

    public override void Entry(IModHelper helper)
    {
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        IStardewUIApi? ui = Helper.ModRegistry.GetApi<IStardewUIApi>("6135.UIFramework");
        if (ui == null)
        {
            Monitor.Log("UI Framework (6135.UIFramework) is not installed.", LogLevel.Warn);
            return;
        }

        IUIMenuOptions options = ui.CreateMenuOptions();
        options.Title = () => "Hello from UI Framework";
        options.Width = 480;
        menu = ui.CreateMenu("hello", options);

        ui.AddLabel(menu.Root, "text", () => $"The button was clicked {clicks} times.");

        IUIButton button = ui.AddButton(menu.Root, "ok", () => "Click me", _ => clicks++);
        button.HorizontalAlign = UIAlign.Center;
        button.Tooltip = () => "Enter also triggers this button.";
        menu.DefaultButton = button;

        ui.BindToggleHotkey(menu, "F9");
    }
}
```

The [example mod](../UIFrameworkExample/ModEntry.cs) in this repository goes further: a two-column form with one of
every input type, a virtualized list with selection, a custom-drawn component
([`VolumeGauge.cs`](../UIFrameworkExample/VolumeGauge.cs)), OK/Close buttons and a `ui_demo` console command. It
compiles against the copied API file only.

### Concepts

#### Menus and the component tree

A screen is an `IUIMenu` created with `CreateMenu(id, options)`. Every menu owns a `Root` container (a vertical
`IUIStack` with 8 px spacing, stretched to the menu's content area) and you build a tree under it with the `Add*`
methods. Each `Add*` call takes the parent container and an id and returns a typed handle (`IUIButton`,
`IUIGrid`, ...) so you rarely need to look elements up by string; `menu.Find(id)` / `api.Find(menu, id)` exist for
convenience. Ids should be unique within a menu; a duplicate is logged at debug level and `Find` returns the first
match.

`CreateMenu` with an id that already exists replaces the old menu. `DestroyMenu(id)` closes and forgets it.

While open, the framework hosts the tree in a real `IClickableMenu` (`Game1.activeClickableMenu`, or a child menu
when you use `OpenAsChild`). Re-opening reuses the same tree, so keep state that has to survive a close/open cycle in
your own fields and expose it through delegates (see value binding below).

Menu chrome and placement come from `IUIMenuOptions` / the same properties on `IUIMenu`:

- `Title` (drawn on the vanilla scroll banner above the box), `DrawBox` (the vanilla dialogue box), `ShowCloseButton`.
- `Width` / `Height`: fixed size, or `null` to fit the content. The result is clamped to the UI viewport.
- `Anchor` (`Center` by default, or an edge / corner) or `Explicit` with `X` / `Y`; `SetPosition(x, y)` switches to
  `Explicit`.
- `Modal` (default `true`). When it is `false`, a click outside the box closes the menu.
- `DimBackground` (default `true`) darkens the screen, unless the player turned on "Show menu background" in the
  game options (the game then draws its own backdrop).
- `Padding` between the box border and `Root`.
- `CloseOnEscape` (default `true`).

Lifecycle: `Open(force)` only opens when the player is free (`Context.IsPlayerFree`) unless `force` is `true`;
`OpenAsChild(parent)` needs an open parent framework menu; `Close()` fires `OnClose`. All framework menus are closed
when returning to the title screen or loading a save. `OnOpen`, `OnClose`, `OnUpdate` (every tick, with elapsed
milliseconds), `OnKey` and `OnScroll` are menu-level callbacks.

#### Layout containers

Positions are never hard-coded. Every container measures its children and arranges them, and the whole tree is
re-laid out when the window is resized, when a size-affecting property changes, when the tree changes, or when you
call `InvalidateLayout()`.

| Container       | Created with                                                       | Behaviour                                                                                                                                                                                                       |
|-----------------|--------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `IUIStack`      | `AddStack(parent, id, horizontal, spacing)`                        | Row or column with `Spacing` between children; `Alignment` is the default cross-axis alignment.                                                                                                                 |
| `IUIGrid`       | `AddGrid(parent, id, columns, rows)`                               | Rows and columns from track strings; children choose a cell with `Row`, `Column`, `RowSpan`, `ColumnSpan`. `ColumnSpacing` / `RowSpacing` add gaps.                                                             |
| `IUIPanel`      | `AddPanel(parent, id, drawBox, padding)`                           | Optional 9-slice box plus padding; children overlap and fill the padded area.                                                                                                                                   |
| `IUICanvas`     | `AddCanvas(parent, id)`                                            | Pixel-exact escape hatch: children are placed at their own `X` / `Y`.                                                                                                                                           |
| `IUIScrollView` | `AddScrollView(parent, id, viewportHeight)`                        | Clips its content to `ViewportHeight` and scrolls it vertically with a scrollbar (arrows, draggable thumb) and the mouse wheel. `ScrollOffset`, `MaxScroll`, `ScrollStep`, `ScrollTo`, `ScrollBy`, `OnScroll`.   |
| `IUIList`       | `AddList(parent, id, rowHeight, visibleRows, itemCount, buildRow)` | Virtualized list: only `VisibleRows` rows exist as elements; `buildRow(index, container)` is called whenever a row scrolls to a new index. Optional selection (`Selectable`, `SelectedIndex`, `OnValueChanged`). |

Grid track strings are comma separated. Each track is `auto` (size to content), a pixel size (`120px` or `120`), or
a star weight (`*`, `2*`) that shares the remaining space:

```csharp
IUIGrid form = ui.AddGrid(menu.Root, "form", "auto,*,120px", "auto,auto,auto");
IUILabel label = ui.AddLabel(form, "day.label", () => "Day:");   // row 0, column 0 by default
IUINumberInput day = ui.AddNumberInput(form, "day", () => this.day, v => this.day = v, 1, 28, 1, true);
day.Column = 1;
```

Every element has sizing and placement properties that its parent honours: `Width` / `Height` (`null` = size to
content), `MarginLeft/Top/Right/Bottom` (or `SetMargin(...)`), `HorizontalAlign` / `VerticalAlign` (`Start`, `Center`,
`End`, `Stretch`), `X` / `Y` for canvases and `Row` / `Column` / `RowSpan` / `ColumnSpan` for grids. `Bounds` gives
the absolute rectangle in UI pixels once layout has run. Elements can be moved between containers with
`IUIContainer.Add(child)` (it is detached from its previous parent) and removed with `Remove` / `Clear` /
`api.Remove(element)`.

All coordinates are UI pixels, the space `IClickableMenu` already works in (after the game's UI scale).

#### Value binding

Value components take a `Func<T>` getter and an `Action<T>` setter, the same pattern Generic Mod Config Menu uses:

```csharp
ui.AddCheckbox(form, "seeds", () => config.PayForSeeds, v => config.PayForSeeds = v);
ui.AddTextInput(form, "name", () => name, v => name = v);
ui.AddNumberInput(form, "day", () => day, v => day = v, min: 1, max: 28, step: 1, clamp: true);
ui.AddSlider(form, "volume", () => volume, v => volume = v, 0, 100);
ui.AddDropdown(form, "season",
    () => new[] { "spring", "summer", "fall", "winter" },   // values
    () => new[] { "Spring", "Summer", "Fall", "Winter" },   // labels (null = use the values)
    () => season, v => season = v);
```

The getter is called every frame, so a value changed elsewhere shows up immediately; the setter runs when the player
changes the value. Text (`Func<string>`) works the same way for labels, buttons, titles, placeholders and tooltips,
which is what makes `helper.Translation.Get(...)` and live counters work with no extra code. Each value component also
exposes its current value as a plain property (`Value`, `SelectedIndex`, `SelectedValue`) and raises
`OnValueChanged` with an `IUIValueEvent` carrying the old and new value (`OldValue`/`NewValue` as strings,
`OldNumber`/`NewNumber`, `OldBool`/`NewBool`, `OldIndex`/`NewIndex`; only the pair matching the element's type is
meaningful).

`IUITextInput.Validate` (`Func<string, bool>`) and `IUINumberInput.Validate` (`Func<double, bool>`) run with the
prospective value before the setter; return `false` to reject the edit.

#### Events and the `Handled` flag

Every element has delegate properties for its events: `OnClick`, `OnRightClick` (`Action<IUIClickEvent>`), `OnHover`,
`OnHoverEnd`, `OnFocus`, `OnBlur` (`Action<IUIElement>`), `OnKey` (`Func<IUIKeyEvent, bool>`), `OnDrawExtra` and
`OnDrawOverlay` (`Action<SpriteBatch, Rectangle>`). Value components add `OnValueChanged`, inputs add `OnSubmit`
(Enter), scrollable ones add `OnScroll`.

Routing of a click:

1. Overlay elements (an open dropdown list) get the event first. Clicking outside an open dropdown closes it *and*
   swallows the click.
2. The deepest visible, enabled element under the cursor is the target (scroll views only hit inside their
   viewport). A focusable target takes keyboard focus; clicking anything else clears focus.
3. The event is delivered to the target, then bubbles to each ancestor until someone sets `IUIEvent.Handled = true`
   (buttons, checkboxes, dropdowns and custom components that return `true` from `OnClick` mark their own clicks
   handled). This lets a list row react to clicks on any of its children.

Key presses go to the focused element and bubble the same way (`OnKey` returns `true` to mark handled), then to the
menu's `OnKey`, then to the built-in bindings described below. A left-click drag (`leftClickHeld` / `releaseLeftClick`)
stays captured by the element that received the click, which is how scrollbar thumbs and sliders drag.

`OnDrawExtra` runs right after the element drew itself, with its absolute bounds, for decorations. `OnDrawOverlay`
runs in the overlay pass on top of the whole menu, which is enough for a hover box without writing a custom component.

Every consumer callback is invoked inside a `try/catch`. An exception is logged with your mod id, element id and
event name, and that callback is then muted for that element so it cannot spam the log or crash the game loop.

#### Overlay, dropdowns and popups

The menu draws in two passes: the tree in order, then an overlay pass for anything that has to sit above its
siblings (an open dropdown list, `OnDrawOverlay` callbacks, custom components with `WantsOverlay`). Overlay elements
also get first pick at input. Only one dropdown is open at a time; opening another closes the first. Dropdowns show
`MaxVisible` rows and scroll beyond that; the open list is clamped to the screen. Tooltips are suppressed while a
popup is open.

#### Focus, keyboard and gamepad navigation

Buttons, checkboxes, dropdowns, text and number inputs, sliders and custom components with `WantsFocus` are
focusable. The framework owns `Game1.keyboardDispatcher.Subscriber` while a text or number input is focused and
restores the previous subscriber afterwards, so it does not fight other mods for text input.

Built-in keys (after element and menu `OnKey` handlers declined them):

- **Tab / Shift+Tab** cycle focus in layout order; **arrow keys** move to the nearest focusable element in that
  direction, except that Up/Down step a focused number input and Left/Right nudge a focused slider. Text inputs
  keep the caret at the end of the text (vanilla text box behaviour), so arrows still move focus while typing.
- **Enter** activates the focused element (button, checkbox, dropdown) or raises `OnSubmit` on an input; otherwise
  it clicks `IUIMenu.DefaultButton`. **Space** activates buttons, checkboxes and dropdowns. A custom component sees
  Enter in its `OnKey` first.
- **Escape** closes an open popup, else clears focus, else clicks `IUIMenu.CancelButton`, else closes the menu when
  `CloseOnEscape` is set. The game's menu key (`E` by default) also closes the menu unless a text field is taking
  input.
- **Mouse wheel** scrolls the hovered scroll view, list, open dropdown or number input, then falls through to the
  menu's `OnScroll` (positive = up).

With gamepad controls and snappy menus enabled, every focusable element becomes a snap target with automatic
neighbours, so the d-pad / left stick move between controls and `A` activates them.

#### Tooltips

Set `Tooltip` (`Func<string>`) on any element; `TooltipTitle` adds a bold title. The tooltip appears after the
delay from the framework config, or after the value you pass to `SetTooltipDelay(ms)` for your own menus (a negative
value goes back to the config default). It is drawn with the vanilla `IClickableMenu.drawHoverText`.

#### Styles

`CreateStyle()` returns an `IUIStyle` whose members are all optional (`null` = inherit): `Font`, `TextColor`,
`HoverColor`, `BoxTexture` + `BoxSource` + `BoxScale` (the 9-slice used by panels and buttons), `Padding`,
`TextShadow`, `ClickSound`, `HoverSound`. Assign a style to `IUIElement.Style` for one element, or call
`SetDefaultStyle(style)` to apply it to every element your mod creates. Resolution order is theme default, then your
default style, then the element's own style.

Theme defaults are the vanilla look: `Game1.smallFont`, `Game1.textColor`, `Color.Wheat` hover tint, no text shadow,
button click `select`, checkbox `drumkit6`, dropdown open `shwip` / close `drumkit6`, scrolling `shiny4`, typing
`cowboy_monsterhit`, backspace `tinyWhip`, and no hover sound. Buttons and checkboxes also expose `ClickSound` /
`HoverSound` directly; an empty string is silent, `null` means theme default.

#### Custom components

Implement `IUICustomComponent` in your mod and attach it with `AddCustom(parent, id, implementation)`. The framework
wraps it so it takes part in layout, hit-testing, focus, hover, tooltips and event bubbling like a built-in element;
you only measure, draw and react:

| Member                                            | Description                                                                                 |
|---------------------------------------------------|---------------------------------------------------------------------------------------------|
| `Vector2 Measure(Vector2 available)`              | Return the desired size given the available size.                                           |
| `void Draw(SpriteBatch b, Rectangle bounds)`      | Draw with the absolute bounds already resolved (in the overlay pass when `WantsOverlay`).   |
| `void Update(Rectangle bounds, double elapsedMs)` | Called every tick.                                                                          |
| `bool OnClick(int x, int y, bool rightButton)`    | Return `true` if the click was handled (stops bubbling).                                    |
| `void OnHover(int x, int y, bool entered)`        | Cursor entered (`entered` = `true`), moved inside (`true`), or left (`false`).              |
| `bool OnKey(Keys key, bool shift, bool ctrl)`     | Key pressed while focused. Return `true` if handled.                                        |
| `bool WantsFocus`                                 | Whether clicking gives this component keyboard focus.                                       |
| `bool WantsOverlay`                               | Draw in the overlay pass (on top of the whole menu) instead of in tree order.               |

Every call into your implementation crosses the API proxy and is guarded like any other callback. The returned
`IUIElement` handle supports all the common properties (`Tooltip`, margins, alignment, `Visible`, `Enabled`, grid
cell, `OnDrawExtra`, ...). See `UIFrameworkExample/VolumeGauge.cs` for a complete component.

#### Hotkeys

`RegisterHotkey(id, keybindList, onPressed)` runs a delegate when a SMAPI keybind list is pressed;
`UnregisterHotkey(id)` removes it. `BindToggleHotkey(menu, keybindList)` opens or closes a menu with one key; pass an
empty string to unbind. Keybind lists use SMAPI's `KeybindList` string syntax, so you can feed values straight from
your Generic Mod Config Menu settings: `"F9"`, `"LeftControl + F8"`, `"LeftControl + F8, LeftShift + F9"`. An invalid
string is logged as a warning and ignored.

### API reference

Everything below is declared in [`Api/Public/IStardewUIApi.cs`](Api/Public/IStardewUIApi.cs). A browsable version
can be generated with Doxygen (see [Generating the API reference](#generating-the-api-reference)).

#### Enums

| Enum            | Values                                                                                                                             | Meaning                                                                                  |
|-----------------|------------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------|
| `UIAlign`       | `Start`, `Center`, `End`, `Stretch`                                                                                                | Alignment of an element inside the slot its parent gives it (or of text inside a label). |
| `UIFont`        | `Small`, `Dialogue`, `Tiny`                                                                                                        | Vanilla fonts: `Game1.smallFont`, `Game1.dialogueFont`, `Game1.tinyFont`.                |
| `UIAnchor`      | `Center`, `TopLeft`, `TopCenter`, `TopRight`, `MiddleLeft`, `MiddleRight`, `BottomLeft`, `BottomCenter`, `BottomRight`, `Explicit` | Where a menu is placed on screen; `Explicit` uses `IUIMenu.X` / `Y` verbatim.            |
| `UIMouseButton` | `Left`, `Right`                                                                                                                    | Mouse button of a click event.                                                           |
| `UIEventKind`   | `Click`, `RightClick`, `Hover`, `HoverEnd`, `ValueChanged`, `Focus`, `Blur`, `Key`, `Scroll`, `Open`, `Close`                      | Kind of a UI event (informational, mirrors the callback that raised it).                 |

#### Event args

`IUIEvent` (base of every event):

| Member                  | Description                                                             |
|-------------------------|-------------------------------------------------------------------------|
| `UIEventKind Kind`      | What kind of event this is.                                             |
| `IUIElement Element`    | The element the event targets (`null` for menu-level events).           |
| `string ElementId`      | Id of `Element`, or an empty string.                                    |
| `bool Handled { set; }` | Set to `true` to stop the event bubbling to parent elements / the menu. |

`IUIClickEvent : IUIEvent`:

| Member                 | Description            |
|------------------------|------------------------|
| `int X`                | Cursor X in UI pixels. |
| `int Y`                | Cursor Y in UI pixels. |
| `UIMouseButton Button` | Which mouse button.    |

`IUIValueEvent : IUIEvent` (only the accessors matching the element's value type are meaningful):

| Member                          | Description                                            |
|---------------------------------|--------------------------------------------------------|
| `string OldValue`, `NewValue`   | Previous / new value as text (text inputs, dropdowns). |
| `double OldNumber`, `NewNumber` | Previous / new number (number inputs, sliders).        |
| `bool OldBool`, `NewBool`       | Previous / new state (checkboxes).                     |
| `int OldIndex`, `NewIndex`      | Previous / new index (dropdowns, list selection).      |

`IUIKeyEvent : IUIEvent`:

| Member       | Description                   |
|--------------|-------------------------------|
| `Keys Key`   | The XNA key that was pressed. |
| `bool Shift` | Shift was held.               |
| `bool Ctrl`  | Control was held.             |
| `bool Alt`   | Alt was held.                 |

#### `IUIStyle`

Visual overrides. Every member is optional; a `null` / default value means "use the theme default". Create with
`IStardewUIApi.CreateStyle` and assign to `IUIElement.Style` or `IStardewUIApi.SetDefaultStyle`.

| Member                 | Description                                             |
|------------------------|---------------------------------------------------------|
| `UIFont? Font`         | Font for text (labels, buttons, inputs).                |
| `Color? TextColor`     | Text color.                                             |
| `Color? HoverColor`    | Tint applied to boxes / buttons while hovered.          |
| `Texture2D BoxTexture` | Texture used for 9-slice boxes (panels, buttons).       |
| `Rectangle? BoxSource` | Source rectangle inside `BoxTexture`.                   |
| `float? BoxScale`      | Scale applied to the 9-slice border.                    |
| `int? Padding`         | Inner padding for boxes.                                |
| `bool? TextShadow`     | Draw text with a shadow.                                |
| `string ClickSound`    | Sound cue played on click (empty string = none).        |
| `string HoverSound`    | Sound cue played when the cursor enters (empty = none). |

#### `IUIElement`

Common surface of every element in a menu tree.

| Member                                                       | Description                                                                                             |
|--------------------------------------------------------------|---------------------------------------------------------------------------------------------------------|
| `string Id`                                                  | Consumer-chosen id (unique within its menu).                                                            |
| `IUIContainer Parent`                                        | Parent container, or `null` for the menu root.                                                          |
| `IUIMenu Menu`                                               | The menu this element belongs to.                                                                       |
| `Rectangle Bounds`                                           | Absolute bounds in UI pixels, valid after layout.                                                       |
| `bool Visible`                                               | Whether the element is drawn and hit-tested.                                                            |
| `bool Enabled`                                               | Whether the element accepts input (disabled elements draw greyed).                                      |
| `Func<string> Tooltip`                                       | Tooltip body, evaluated when shown. `null` disables the tooltip.                                        |
| `Func<string> TooltipTitle`                                  | Optional bold tooltip title.                                                                            |
| `object Tag`                                                 | Free-form consumer data.                                                                                |
| `IUIStyle Style`                                             | Visual overrides for this element (`null` = inherit).                                                   |
| `int MarginLeft`, `MarginTop`, `MarginRight`, `MarginBottom` | Outer margins in UI pixels.                                                                             |
| `void SetMargin(int all)`                                    | Set all four margins.                                                                                   |
| `void SetMargin(int horizontal, int vertical)`               | Set horizontal (left/right) and vertical (top/bottom) margins.                                          |
| `void SetMargin(int left, int top, int right, int bottom)`   | Set each margin.                                                                                        |
| `int? Width`                                                 | Explicit width in UI pixels, or `null` for "size to content".                                           |
| `int? Height`                                                | Explicit height in UI pixels, or `null` for "size to content".                                          |
| `UIAlign HorizontalAlign`                                    | Horizontal alignment inside the slot given by the parent.                                               |
| `UIAlign VerticalAlign`                                      | Vertical alignment inside the slot given by the parent.                                                 |
| `int X`                                                      | X position, only used when the parent is an `IUICanvas`.                                                |
| `int Y`                                                      | Y position, only used when the parent is an `IUICanvas`.                                                |
| `int Row`                                                    | Grid row, only used when the parent is an `IUIGrid`.                                                    |
| `int Column`                                                 | Grid column, only used when the parent is an `IUIGrid`.                                                 |
| `int RowSpan`                                                | Grid row span (default 1).                                                                              |
| `int ColumnSpan`                                             | Grid column span (default 1).                                                                           |
| `bool IsFocused`                                             | `true` while this element owns keyboard focus.                                                          |
| `bool IsHovered`                                             | `true` while the cursor is over this element.                                                           |
| `void Focus()`                                               | Give this element keyboard focus (no-op if it is not focusable).                                        |
| `void InvalidateLayout()`                                    | Request a layout pass before the next draw.                                                             |
| `Action<IUIClickEvent> OnClick`                              | Left click on the element (or bubbling from a child).                                                   |
| `Action<IUIClickEvent> OnRightClick`                         | Right click on the element (or bubbling from a child).                                                  |
| `Action<IUIElement> OnHover`                                 | Cursor entered the element.                                                                             |
| `Action<IUIElement> OnHoverEnd`                              | Cursor left the element.                                                                                |
| `Action<IUIElement> OnFocus`                                 | Element gained keyboard focus.                                                                          |
| `Action<IUIElement> OnBlur`                                  | Element lost keyboard focus.                                                                            |
| `Func<IUIKeyEvent, bool> OnKey`                              | Raised for key presses while focused (or bubbling from a focused child). Return `true` to mark handled. |
| `Action<SpriteBatch, Rectangle> OnDrawExtra`                 | Called after the element drew itself, with its absolute bounds. Draw custom decorations here.           |
| `Action<SpriteBatch, Rectangle> OnDrawOverlay`               | Called in the overlay pass (on top of everything else in the menu), with the element's absolute bounds. |

#### Containers

`IUIContainer : IUIElement` (an element that holds children):

| Member                           | Description                                                                             |
|----------------------------------|-----------------------------------------------------------------------------------------|
| `int ChildCount`                 | Number of direct children.                                                              |
| `IUIElement GetChild(int index)` | Get the child at `index` (in add order).                                                |
| `void Add(IUIElement child)`     | Re-parent an existing element under this container (it is removed from its old parent). |
| `void Remove(IUIElement child)`  | Remove a direct child.                                                                  |
| `void Clear()`                   | Remove all children.                                                                    |

`IUIPanel : IUIContainer` (a box with padding and an optional 9-slice background; children overlap and fill the
padded area):

| Member         | Description                  |
|----------------|------------------------------|
| `bool DrawBox` | Draw the 9-slice background. |
| `int Padding`  | Inner padding in UI pixels.  |

`IUIStack : IUIContainer` (lays children out in a row or column):

| Member              | Description                                                           |
|---------------------|-----------------------------------------------------------------------|
| `bool Horizontal`   | Row (`true`) or column (`false`).                                     |
| `int Spacing`       | Gap between children in UI pixels.                                    |
| `UIAlign Alignment` | Default cross-axis alignment for children that did not set their own. |

`IUIGrid : IUIContainer` (rows and columns; track definitions are comma separated: `auto`, `120px` or `120`, `*`,
`2*`; children pick a cell through `IUIElement.Row`, `Column` and the span properties):

| Member              | Description                       |
|---------------------|-----------------------------------|
| `string Columns`    | Column track definitions.         |
| `string Rows`       | Row track definitions.            |
| `int ColumnSpacing` | Gap between columns in UI pixels. |
| `int RowSpacing`    | Gap between rows in UI pixels.    |

`IUICanvas : IUIContainer` places children at explicit `IUIElement.X` / `Y` offsets and adds no members of its own.

`IUIScrollView : IUIContainer` (clips and scrolls its content vertically):

| Member                      | Description                                                                       |
|-----------------------------|-----------------------------------------------------------------------------------|
| `int ViewportHeight`        | Visible height in UI pixels.                                                      |
| `int ScrollOffset`          | Current scroll offset in UI pixels (0 = top).                                     |
| `int MaxScroll`             | Largest valid `ScrollOffset`.                                                     |
| `int ScrollStep`            | Pixels moved per wheel notch / arrow click.                                       |
| `bool ShowScrollbar`        | Draw the scrollbar.                                                               |
| `Action<int> OnScroll`      | Raised after the offset changed; argument is the delta in pixels (negative = up). |
| `void ScrollTo(int offset)` | Scroll to an absolute offset.                                                     |
| `void ScrollBy(int delta)`  | Scroll by a relative amount.                                                      |

`IUIList : IUIContainer` (virtualized list: only the visible rows exist as elements; rows are rebuilt through the
`buildRow` delegate given to `AddList` whenever they scroll into a new index):

| Member                                 | Description                                                                  |
|----------------------------------------|------------------------------------------------------------------------------|
| `int RowHeight`                        | Height of one row in UI pixels.                                              |
| `int VisibleRows`                      | Number of rows shown at once.                                                |
| `int FirstVisibleIndex`                | Index of the first visible row.                                              |
| `int ItemCount`                        | Number of items reported by the data source at the last refresh.             |
| `bool Selectable`                      | Whether clicking a row selects it.                                           |
| `int SelectedIndex`                    | Selected item index or -1.                                                   |
| `Action<IUIValueEvent> OnValueChanged` | Raised when `SelectedIndex` changes (`IUIValueEvent.OldIndex` / `NewIndex`). |
| `Action<int> OnScroll`                 | Raised after scrolling; argument is the row delta.                           |
| `void Refresh()`                       | Re-query the item count and rebuild visible rows.                            |
| `void ScrollTo(int firstIndex)`        | Make `firstIndex` the first visible row.                                     |

#### Leaves

`IUILabel : IUIElement` (text):

| Member              | Description                                             |
|---------------------|---------------------------------------------------------|
| `Func<string> Text` | Text, evaluated every frame.                            |
| `UIFont Font`       | Font.                                                   |
| `Color? Color`      | Text color (`null` = style / theme).                    |
| `bool Shadow`       | Draw with a shadow.                                     |
| `bool Wrap`         | Wrap to the available width (or to `IUIElement.Width`). |
| `UIAlign TextAlign` | Horizontal text alignment inside the label's bounds.    |
| `float Scale`       | Text scale.                                             |

`IUIImage : IUIElement` (a texture, or a region of one):

| Member              | Description                                |
|---------------------|--------------------------------------------|
| `Texture2D Texture` | Texture to draw.                           |
| `Rectangle? Source` | Source rectangle (`null` = whole texture). |
| `float Scale`       | Draw scale.                                |
| `Color Tint`        | Tint color.                                |

`IUIButton : IUIElement` (a clickable box with text and/or an icon):

| Member                  | Description                                                    |
|-------------------------|----------------------------------------------------------------|
| `Func<string> Text`     | Button text.                                                   |
| `UIFont Font`           | Font.                                                          |
| `Texture2D Icon`        | Optional icon texture.                                         |
| `Rectangle? IconSource` | Source rectangle of the icon.                                  |
| `float IconScale`       | Icon scale.                                                    |
| `string ClickSound`     | Sound cue on click (`null` = theme default, empty = none).     |
| `string HoverSound`     | Sound cue when hovered (`null` = theme default, empty = none). |
| `bool DrawBox`          | Draw the 9-slice box behind the content.                       |

`IUICheckbox : IUIElement` (a boolean toggle):

| Member                                 | Description                                  |
|----------------------------------------|----------------------------------------------|
| `bool Value`                           | Current state.                               |
| `Func<string> Label`                   | Optional text drawn to the right of the box. |
| `string ClickSound`                    | Sound cue on click.                          |
| `Action<IUIValueEvent> OnValueChanged` | Raised when the state changes.               |

`IUITextInput : IUIElement` (single-line text entry):

| Member                                 | Description                                                                           |
|----------------------------------------|---------------------------------------------------------------------------------------|
| `string Value`                         | Current text.                                                                         |
| `Func<string> Placeholder`             | Text shown while empty.                                                               |
| `int MaxLength`                        | Maximum characters (0 = unlimited).                                                   |
| `Func<string, bool> Validate`          | Called with the prospective new value before it is applied; return `false` to reject. |
| `Texture2D Texture`                    | Custom box texture (`null` = vanilla text box).                                       |
| `Action<IUIValueEvent> OnValueChanged` | Raised when the text changes.                                                         |
| `Action<IUIElement> OnSubmit`          | Raised when Enter is pressed while focused.                                           |

`IUINumberInput : IUIElement` (numeric entry with clamping, stepping (Up/Down keys, wheel) and validation):

| Member                                 | Description                                                                           |
|----------------------------------------|---------------------------------------------------------------------------------------|
| `double Value`                         | Current value.                                                                        |
| `double Min`, `Max`                    | Range.                                                                                |
| `double Step`                          | Increment for Up/Down and the wheel.                                                  |
| `bool Clamp`                           | Keep the value inside `Min` / `Max`.                                                  |
| `int Decimals`                         | Decimal places accepted / displayed (0 = integers only).                              |
| `Func<double, bool> Validate`          | Called with the prospective new value before it is applied; return `false` to reject. |
| `Texture2D Texture`                    | Custom box texture (`null` = vanilla text box).                                       |
| `Action<IUIValueEvent> OnValueChanged` | Raised when the value changes.                                                        |
| `Action<IUIElement> OnSubmit`          | Raised when Enter is pressed while focused.                                           |

`IUIDropdown : IUIElement` (pick one of several string choices):

| Member                                 | Description                                                  |
|----------------------------------------|--------------------------------------------------------------|
| `int SelectedIndex`                    | Index of the selected choice.                                |
| `string SelectedValue`                 | Value of the selected choice.                                |
| `int MaxVisible`                       | Rows shown at once when open (the list scrolls beyond that). |
| `bool IsOpen`                          | Whether the list is open.                                    |
| `void Open()`, `void Close()`          | Open / close the list.                                       |
| `void RefreshChoices()`                | Re-evaluate the choices / labels delegates.                  |
| `int ChoiceCount`                      | Number of choices.                                           |
| `string GetChoice(int index)`          | Value at `index`.                                            |
| `string GetLabel(int index)`           | Label at `index`.                                            |
| `Action<IUIValueEvent> OnValueChanged` | Raised when the selection changes.                           |
| `Action<int> OnScroll`                 | Raised when the open list scrolls.                           |

`IUISlider : IUIElement` (a horizontal slider over a numeric range):

| Member                                 | Description                      |
|----------------------------------------|----------------------------------|
| `double Value`                         | Current value.                   |
| `double Min`, `Max`                    | Range.                           |
| `double Step`                          | Snap increment (0 = continuous). |
| `Action<IUIValueEvent> OnValueChanged` | Raised when the value changes.   |

`IUISpacer : IUIElement` (empty space, optionally drawn as a divider line):

| Member      | Description          |
|-------------|----------------------|
| `bool Line` | Draw a divider line. |

#### `IUICustomComponent`

See [Custom components](#custom-components) above for the member table.

#### `IUIMenuOptions`

Initial settings for `IStardewUIApi.CreateMenu(string, IUIMenuOptions)`. All of these can also be changed later on
the menu.

| Member                 | Description                                                        |
|------------------------|--------------------------------------------------------------------|
| `Func<string> Title`   | Title text (`null` = no title).                                    |
| `int? Width`           | Fixed width, or `null` to fit content.                             |
| `int? Height`          | Fixed height, or `null` to fit content.                            |
| `bool ShowCloseButton` | Show the vanilla close button (default `true`).                    |
| `bool Modal`           | Block clicks outside the menu (default `true`).                    |
| `bool DimBackground`   | Darken the screen behind the menu (default `true`).                |
| `UIAnchor Anchor`      | Placement (default `Center`).                                      |
| `int X`, `int Y`       | Position used with `UIAnchor.Explicit`.                            |
| `bool DrawBox`         | Draw the vanilla dialogue box behind the content (default `true`). |
| `int Padding`          | Inner padding between the box border and the root container.       |
| `bool CloseOnEscape`   | Escape (or the menu key) closes the menu (default `true`).         |

#### `IUIMenu`

A screen. Build its tree under `Root`, then `Open`.

| Member                              | Description                                                                            |
|-------------------------------------|----------------------------------------------------------------------------------------|
| `string Id`                         | The id given to `CreateMenu`.                                                          |
| `IUIStack Root`                     | Root container (a vertical `IUIStack`).                                                |
| `bool IsOpen`                       | Whether the menu is currently hosted by the game.                                      |
| `Func<string> Title`                | Title text.                                                                            |
| `int? Width`, `int? Height`         | Fixed size, or `null` to fit content.                                                  |
| `bool ShowCloseButton`              | Show the vanilla close button.                                                         |
| `bool Modal`                        | Block clicks outside the menu.                                                         |
| `bool DimBackground`                | Darken the screen behind the menu.                                                     |
| `UIAnchor Anchor`, `int X`, `int Y` | Placement.                                                                             |
| `bool DrawBox`                      | Draw the vanilla dialogue box.                                                         |
| `int Padding`                       | Inner padding.                                                                         |
| `bool CloseOnEscape`                | Escape (or the menu key) closes the menu.                                              |
| `Rectangle Bounds`                  | Absolute bounds of the menu box, valid while open.                                     |
| `IUIButton DefaultButton`           | Button clicked when Enter is pressed and no focused element consumed it.               |
| `IUIButton CancelButton`            | Button clicked when Escape is pressed (instead of closing).                            |
| `Action<IUIMenu> OnOpen`            | Raised after the menu opened.                                                          |
| `Action<IUIMenu> OnClose`           | Raised after the menu closed.                                                          |
| `Action<IUIMenu, double> OnUpdate`  | Every tick while open, with elapsed milliseconds.                                      |
| `Func<IUIKeyEvent, bool> OnKey`     | Key presses not handled by any element. Return `true` to mark handled.                 |
| `Action<int> OnScroll`              | Scroll wheel not handled by any element (positive = up).                               |
| `void Open(bool force)`             | Open as the active menu. Does nothing unless the player is free, or `force` is `true`. |
| `void OpenAsChild(IUIMenu parent)`  | Open as a child of another (open) framework menu.                                      |
| `void Close()`                      | Close the menu.                                                                        |
| `void InvalidateLayout()`           | Request a layout pass before the next draw.                                            |
| `IUIElement Find(string id)`        | Find an element by id anywhere in the tree, or `null`.                                 |
| `void SetPosition(int x, int y)`    | Move the menu (sets `Anchor` to `Explicit`).                                           |

#### `IStardewUIApi`

Entry point. One instance per consumer mod; every id you register is private to your mod.

| Member                                                                                                                                                | Description                                                                                           |
|-------------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------|
| `string ApiVersion`                                                                                                                                   | Semantic version of this API (`1.0.0`).                                                               |
| `IUIMenuOptions CreateMenuOptions()`                                                                                                                  | New options bag with the defaults listed under `IUIMenuOptions`.                                      |
| `IUIMenu CreateMenu(string id, IUIMenuOptions options)`                                                                                               | Create (or replace) a menu.                                                                           |
| `IUIMenu CreateMenu(string id)`                                                                                                                       | Create a menu with default options.                                                                   |
| `IUIMenu GetMenu(string id)`                                                                                                                          | Look up one of your menus, or `null`.                                                                 |
| `void DestroyMenu(string id)`                                                                                                                         | Close and forget a menu.                                                                              |
| `void OpenMenu(string id)`                                                                                                                            | `Open(false)` on the menu with that id.                                                               |
| `void CloseMenu(string id)`                                                                                                                           | Close the menu with that id.                                                                          |
| `bool IsOpen(string id)`                                                                                                                              | Whether the menu with that id is open.                                                                |
| `IUIStack AddStack(IUIContainer parent, string id, bool horizontal, int spacing)`                                                                     | Add a row / column container.                                                                         |
| `IUIGrid AddGrid(IUIContainer parent, string id, string columns, string rows)`                                                                        | Add a grid; track strings such as `"auto,*,120px"` (`null` falls back to `"*"` / `"auto"`).           |
| `IUIPanel AddPanel(IUIContainer parent, string id, bool drawBox, int padding)`                                                                        | Add a panel with optional box and padding.                                                            |
| `IUICanvas AddCanvas(IUIContainer parent, string id)`                                                                                                 | Add a canvas (explicit `X` / `Y` children).                                                           |
| `IUIScrollView AddScrollView(IUIContainer parent, string id, int viewportHeight)`                                                                     | Add a vertical scroll view.                                                                           |
| `IUIList AddList(IUIContainer parent, string id, int rowHeight, int visibleRows, Func<int> itemCount, Action<int, IUIContainer> buildRow)`            | Add a virtualized list; `buildRow` fills a row container for an index.                                |
| `IUILabel AddLabel(IUIContainer parent, string id, Func<string> text)`                                                                                | Add text (`null` text = empty).                                                                       |
| `IUIImage AddImage(IUIContainer parent, string id, Texture2D texture, Rectangle? source, float scale)`                                                | Add a texture or a region of one.                                                                     |
| `IUIButton AddButton(IUIContainer parent, string id, Func<string> text, Action<IUIClickEvent> onClick)`                                               | Add a button.                                                                                         |
| `IUICheckbox AddCheckbox(IUIContainer parent, string id, Func<bool> get, Action<bool> set)`                                                           | Add a bound checkbox.                                                                                 |
| `IUITextInput AddTextInput(IUIContainer parent, string id, Func<string> get, Action<string> set)`                                                     | Add a bound text input.                                                                               |
| `IUINumberInput AddNumberInput(IUIContainer parent, string id, Func<double> get, Action<double> set, double min, double max, double step, bool clamp)` | Add a bound number input (`min` / `max` are swapped if reversed).                                     |
| `IUIDropdown AddDropdown(IUIContainer parent, string id, Func<string[]> choices, Func<string[]> labels, Func<string> get, Action<string> set)`         | Add a bound dropdown; `labels` may be `null` to display the values.                                   |
| `IUISlider AddSlider(IUIContainer parent, string id, Func<double> get, Action<double> set, double min, double max)`                                   | Add a bound slider (`min` / `max` are swapped if reversed).                                           |
| `IUISpacer AddSpacer(IUIContainer parent, string id, int width, int height)`                                                                          | Add empty space (negative sizes become 0).                                                            |
| `IUIElement AddCustom(IUIContainer parent, string id, IUICustomComponent implementation)`                                                             | Add a consumer-implemented component.                                                                 |
| `IUIElement Find(IUIMenu menu, string id)`                                                                                                            | Find an element by id anywhere in the menu, or `null`.                                                |
| `void Remove(IUIElement element)`                                                                                                                     | Detach an element from its parent.                                                                    |
| `void InvalidateLayout(IUIMenu menu)`                                                                                                                 | Request a layout pass for the menu.                                                                   |
| `void RegisterHotkey(string id, string keybindList, Action onPressed)`                                                                                | Run `onPressed` when the keybind list (e.g. `"F8"`, `"LeftControl + F8, LeftShift + F9"`) is pressed. |
| `void UnregisterHotkey(string id)`                                                                                                                    | Remove a hotkey.                                                                                      |
| `void BindToggleHotkey(IUIMenu menu, string keybindList)`                                                                                             | Toggle the menu open/closed when the keybind list is pressed (pass an empty string to unbind).        |
| `void SetTooltipDelay(int milliseconds)`                                                                                                              | Tooltip delay for this consumer's menus (pass a negative value to reset to the framework default).    |
| `IUIStyle CreateStyle()`                                                                                                                              | New empty style.                                                                                      |
| `void SetDefaultStyle(IUIStyle style)`                                                                                                                | Default style for every element this consumer creates (`null` = theme).                               |

Argument checks: ids must be non-empty; `parent` must be a container created by the framework and must belong to
one of *your* menus (adding to another mod's menu throws `InvalidOperationException`); `itemCount`, `buildRow`,
`choices`, `onPressed` and custom `implementation` must not be `null`.

### Proxy rules

SMAPI maps your copy of the interfaces onto the framework's objects at runtime, which constrains what can cross the
API:

- Only **interfaces**, **enums**, **delegates**, primitives, `string` and types both sides already share
  (`Microsoft.Xna.Framework.*` such as `Vector2`, `Rectangle`, `Color`, `SpriteBatch`, `Texture2D`, `Keys`; SMAPI and
  game types) appear in the API. No framework class is ever visible to a consumer.
- Delegates (`Action`, `Action<T>`, `Func<T>`) are fine when every `T` is proxyable; event data is passed as
  interfaces (`IUIClickEvent`, `IUIValueEvent`, `IUIKeyEvent`).
- Interfaces you implement (`IUICustomComponent`) are proxied back into the framework.
- Enums are matched by name; copy them verbatim.
- Objects you receive from the API (menus, elements, styles) must be handed back unchanged; the framework rejects
  objects it did not create ("was not created by this framework").
- The API has no optional parameters and no generics, so partial copies of the interface still line up.

### Versioning

`IStardewUIApi.ApiVersion` returns a semantic version string (currently `1.0.0`). Members are only ever **added**,
never renamed or removed: additive changes bump the minor version, and a breaking change would ship as a new
`IStardewUIApi2` interface alongside the old one. A consumer's copy of the interface may be a subset of the
framework's, so you can keep an older copy of `IStardewUIApi.cs` and only update it when you need new members. Set
`MinimumVersion` in your manifest to the framework version that introduced the members you use.

### Generating the API reference

`Doxyfile` in this folder documents the public API file from its XML comments:

```sh
cd StardewUIFramework
doxygen Doxyfile
```

The HTML lands in `StardewUIFramework/docs/api/html` (ignored by git). Paths in the Doxyfile are relative to the
working directory, so run it from `StardewUIFramework/` rather than `doxygen StardewUIFramework/Doxyfile` from the
repository root.

## Building from source

Requirements: .NET 6 SDK and a Stardew Valley 1.6 install with SMAPI.

1. Set the `STARDEW_GAME_DIR` environment variable to the game folder (the one containing `Stardew Valley.dll` and
   `Mods/`). Both projects read it as `<GamePath>`; without it the game and SMAPI references cannot be resolved and
   the build fails (this is what CI needs too).
2. Build:

   ```sh
   dotnet build StardewUIFramework/UIFramework.csproj -c Release
   dotnet build UIFrameworkExample/UIFrameworkExample.csproj -c Release
   ```

   Or open `Stardew Mods.sln` and build the `UIFramework` and `UIFrameworkExample` projects.

[Pathoschild.Stardew.ModBuildConfig](https://github.com/Pathoschild/SMAPI/blob/develop/docs/technical/mod-package.md)
handles the rest: it references the game assemblies, copies the built mod into `<game>/Mods/UIFramework` after every
build so you can test immediately, and drops a release zip (`UIFramework <version>.zip`) into the project's build
output folder. The same happens for the example mod. `UIFramework.csproj` also generates the XML documentation file
that Doxygen and IDE tooltips use.

`UIFrameworkExample` deliberately has **no** `ProjectReference` to the framework: it compiles against its copy of
`Api/IStardewUIApi.cs` only, which is exactly the situation a third-party consumer is in and keeps the proxy path
honest. When the API file changes, copy it to `UIFrameworkExample/Api/IStardewUIApi.cs` again.

## Layout of this folder

| Path                          | Contents                                                                                                                                                               |
|-------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Api/Public/IStardewUIApi.cs` | The public API (copy this).                                                                                                                                            |
| `Api/`                        | Per-consumer facade (`StardewUIApi`) and the menu options bag.                                                                                                         |
| `Core/`                       | Element tree, layout engine, focus manager, overlay layer, event router.                                                                                               |
| `Components/`                 | Built-in elements (Label, Button, TextInput, NumberInput, Checkbox, Dropdown, Slider, Image, Spacer, Stack, Grid, Panel, Canvas, ScrollView, ListView, custom adapter). |
| `Hosting/`                    | `MenuHost : IClickableMenu`, hotkey service, menu registry.                                                                                                            |
| `Rendering/`                  | Drawing helpers and the vanilla theme.                                                                                                                                 |
| `Integrations/`               | Generic Mod Config Menu API copy.                                                                                                                                      |
| `assets/`, `i18n/`            | Bundled texture and translations.                                                                                                                                      |
| `architecture.md`             | Design document and implementation plan.                                                                                                                               |
| `Doxyfile`                    | Doxygen configuration for the API reference.                                                                                                                           |
