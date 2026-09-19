# UI Framework

**UI Framework** (`6135.UIFramework`) is a SMAPI library mod. Other mods use it to build in-game menus out of
reusable parts (labels, buttons, text and number inputs, checkboxes, dropdowns, sliders, scroll areas, virtualized
lists, data grids, auto-generated forms, rich tooltips) through a code-first C# builder API, and to run their own
code when the player interacts with those parts. It also gives every mod that uses it HUD widgets and toasts,
player-adjustable windows, themes, screen reader support, extension slots other mods can contribute to, and an
in-game inspector.

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

| Key               | Default     | Description                                                                                                                                                                                 |
|-------------------|-------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `TooltipDelayMs`  | `400`       | How long (milliseconds) the cursor must rest on an element before its tooltip appears.                                                                                                      |
| `DebugOverlay`    | `false`     | Draw the bounds and ids of every element in framework menus (for mod authors).                                                                                                              |
| `LogCallbacks`    | `false`     | Write a trace log line every time a consumer mod's callback is invoked (for mod authors).                                                                                                   |
| `PseudoLocalize`  | `false`     | Accent and pad every string framework menus draw (`Calculate` becomes `[Çálçúláté~~~]`) so mod authors can spot text that overflows before it is translated.                                 |
| `Theme`           | `"default"` | Name of the theme every framework menu uses (see [Themes](#themes)).                                                                                                                        |
| `TextScale`       | `1.0`       | Multiplier (0.75-2.0) for all text in framework menus; layout grows with it.                                                                                                                |
| `ReducedMotion`   | `false`     | Disable animation: the text caret stays solid instead of blinking. Hover tints and dropdowns are already instant; custom components can read `ReducedMotion` from the API to honour it too. |
| `InspectorHotkey` | `"F10"`     | SMAPI keybind list that toggles the in-game inspector while a framework menu is open (for mod authors; empty = none). See [Inspector and debug console](#inspector-and-debug-console).      |

A mod that uses the framework can override the tooltip delay for its own menus; the config value is the default.

### Themes

A theme restyles every menu built with the framework at once: text / hover / disabled colors, the box textures (or a
solid fill with a thick border), spacing scale, font scale, text shadow, scrollbar tint and the click / hover / open /
close sounds. Four themes ship with the mod: `default` (vanilla), `dark`, `high-contrast` (black boxes, yellow text,
thick borders) and `colorblind` (blue / orange cues instead of the wheat hover tint). Pick one in Generic Mod Config
Menu, with `ui_theme <name>` in the console, or by editing `Theme` in `config.json`.

Themes live in the data asset `Mods/6135.UIFramework/Themes` (a dictionary of theme name → theme data, defaults in
`assets/themes.json`), so a Content Patcher pack can add or edit themes:

```json
{
  "Format": "2.0.0",
  "Changes": [
    {
      "Action": "EditData",
      "Target": "Mods/6135.UIFramework/Themes",
      "Entries": {
        "midnight": { "TextColor": "#DDE6FF", "BoxTint": "#404860", "HoverColor": "#8FB4FF", "ScrollbarTint": "#8FB4FF" }
      }
    }
  ]
}
```

Every field is optional (unset = vanilla): `TextColor`, `DisabledTextColor`, `HoverColor`, `BoxTint`, `BoxFill`,
`BorderColor`, `ScrollbarTint` (colors as `#RRGGBB`, `#RRGGBBAA`, `R,G,B[,A]` or an XNA color name such as `Wheat`),
`BorderThickness` (pixels, used with `BoxFill`), `BoxTexture` / `BoxSource`, `ButtonTexture` / `ButtonSource`,
`TextBoxTexture` (game asset names plus an optional `x,y,w,h` 3x3 tile region), `SpacingScale`, `FontScale`,
`ShadowEnabled`, `ClickSound`, `HoverSound`, `OpenSound`, `CloseSound` (cue names; an empty string is silent).
Edits apply live when the asset is invalidated. Styles a mod sets on its own elements always win over the theme.

### Accessibility

- **Screen reader**: when [Stardew Access](https://www.nexusmods.com/stardewvalley/mods/16205) (1.6.2+) is installed,
  framework menus announce the menu title on open, the focused element when focus moves ("Button: OK",
  "Checkbox: Pay for seeds, checked", "Dropdown: Spring", ...), value changes on the focused element, selected list
  rows, and the element under the cursor once it rested there for the tooltip delay. Mods can override the spoken
  text per element with `AccessibleName` and speak their own text with `Announce`.
- **Text scaling**: `TextScale` (or a theme's `FontScale`) scales every label, button, input and dropdown; text boxes
  and dropdown rows grow to fit.
- **Reduced motion** and the **high-contrast** theme: see above.

### Console commands

Type these in the SMAPI console. The ones marked *(mod authors)* are diagnostics described under
[Inspector and debug console](#inspector-and-debug-console).

| Command                                | Effect                                                                                                                                              |
|----------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------|
| `ui_theme`                             | List the available themes and the active one.                                                                                                       |
| `ui_theme <name>`                      | Switch every framework menu to that theme and save it in `config.json`.                                                                             |
| `ui_toast [text]`                      | Show a test notification in the bottom-left corner (default text when omitted).                                                                     |
| `ui_layout_reset`                      | Forget every window position / size / collapsed state you changed in the current save.                                                              |
| `ui_debug`                             | Toggle the debug overlay (element bounds and ids) for the current session. *(mod authors)*                                                          |
| `ui_pseudoloc`                         | Toggle pseudo-localization (accented, padded strings in every framework menu) for the current session. *(mod authors)*                              |
| `ui_inspect`                           | Toggle the in-game inspector (hover elements to see their layout; the info panel lists the editing keys). *(mod authors)*                           |
| `ui_list`                              | List the open framework menus: consumer, menu id, host type (`active` / `child`) and element count. *(mod authors)*                                 |
| `ui_dump [<consumerId> <menuId>]`      | Print the element tree of a menu (the most recently opened one without arguments). *(mod authors)*                                                  |
| `ui_find <text>`                       | List every element of the open menus whose id contains the text. *(mod authors)*                                                                    |
| `ui_perf on\|off\|show`                | Per-menu timing of the last 60 frames. *(mod authors)*                                                                                              |
| `ui_export [<consumerId> <menuId>]`    | Export a menu as C# builder code to the log, `Mods/UIFramework/export` and the clipboard. *(mod authors)*                                            |
| `ui_slots`                             | Print the extension slots of every open framework menu (hints and contributors). *(mod authors)*                                                    |
| `ui_composites`                        | List the composite components defined through the framework. *(mod authors)*                                                                        |

The bundled example mod adds `ui_demo` (open its demo menu) and `ui_hud` (toggle its HUD widget) when it is
installed.

### Moving, collapsing and resizing windows

Every framework window can be adjusted by the player (unless the mod that owns it opted out):

- **Move** it by dragging its title banner or the top border of the box.
- **Collapse** it to its title strip with the arrow button next to the close button; click again to expand.
- **Resize** windows that have a fixed size by dragging the dotted grip in the bottom-right corner (it never gets
  smaller than its content).
- Interactive HUD widgets (small overlays some mods draw during play) can be dragged the same way.

Positions, sizes and collapsed states are stored in the save file (host player only; farmhands keep them for the
session) and restored the next time the window opens. `ui_layout_reset` clears them.

### Notifications

Mods that use the framework can show short notifications ("toasts") in the bottom-left corner of the screen, with
or without an icon. At most five are shown at once; each fades in, stays for a few seconds and fades out, and older
ones slide up when a newer one arrives. They stay visible on top of menus.

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
   "Dependencies": [ { "UniqueID": "6135.UIFramework", "MinimumVersion": "1.1.0", "IsRequired": true } ]
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
([`VolumeGauge.cs`](../UIFrameworkExample/VolumeGauge.cs)), OK/Close buttons and a `ui_demo` console command, plus
one demo per v1.1 feature: a composite "money field" and a hand-drawn frame around built-in checkboxes
([`FrameBox.cs`](../UIFrameworkExample/FrameBox.cs)), an extension slot the mod contributes to itself, a sortable
data grid, a rich-text header and a rich tooltip, signals bound to the form inputs, a Save / Cancel / Undo / Redo
form generated from [`DemoSettings.cs`](../UIFrameworkExample/DemoSettings.cs), a draggable HUD widget (`ui_hud`)
and a theme dropdown. It compiles against the copied API file only. The samples in the sections below are adapted
from it.

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
value goes back to the config default). It is drawn with the vanilla `IClickableMenu.drawHoverText`. For tooltips
with icons, item rows, money or colored lines, set `RichTooltip` instead (see
[Rich text and rich tooltips](#rich-text-and-rich-tooltips)).

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

The player's active theme (see [Themes](#themes)) replaces those defaults for every mod; your styles still override
it. `ListThemes()`, `ActiveTheme` and `SetTheme(name)` let a mod offer the theme choice in its own screen, and
`ThemeColor("text" | "disabled-text" | "hover" | "scrollbar" | "border")` returns the active colors so custom
components can match. `ReducedMotion` tells custom components to skip animation.

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
| `bool WantsOverlay`                               | Draw in the overlay pass (on top of the whole menu) instead of in tree order; hit-tested before the tree. |

Every call into your implementation crosses the API proxy and is guarded like any other callback. The returned
`IUIElement` handle supports all the common properties (`Tooltip`, margins, alignment, `Visible`, `Enabled`, grid
cell, `OnDrawExtra`, ...). See `UIFrameworkExample/VolumeGauge.cs` for a complete component.

A custom component can also wrap built-in elements: the `AddCustom(parent, id, implementation, build)` overload is
described under [Composites](#composites).

#### Hotkeys

`RegisterHotkey(id, keybindList, onPressed)` runs a delegate when a SMAPI keybind list is pressed;
`UnregisterHotkey(id)` removes it. `BindToggleHotkey(menu, keybindList)` opens or closes a menu with one key; pass an
empty string to unbind. Keybind lists use SMAPI's `KeybindList` string syntax, so you can feed values straight from
your Generic Mod Config Menu settings: `"F9"`, `"LeftControl + F8"`, `"LeftControl + F8, LeftShift + F9"`. An invalid
string is logged as a warning and ignored.

#### Extension slots and screen context

A slot lets *other* mods add elements to one of your menus without touching the rest of it. The owner declares a
slot with `AddSlot(parent, id)`: an `IUISlot` is a stack-like container that the framework empties and rebuilds from
the registered contributions every time the menu opens, before layout. Slot ids are unique per menu and other mods
address the slot as (your mod id, menu id, slot id). The slot manages its own `Visible`: it is shown while it has
contributions and its `VisiblePredicate` (if any) returns true. The owner sets layout hints and limits on it:
`Horizontal` (row instead of column), `MaxHeight`, `MaxContributions` (0 = unlimited) and `VetoedContributors` (mod
ids to skip).

What contributors can see of the owner's menu is only what the owner shares through the `Expose*` members:
`Expose(menu, key, Func<string>)`, `ExposeNumber`, `ExposeBool` (values, read live), `ExposeCommand(menu, key,
Action)` (commands) and `Publish(menu, eventName)` (an event bus). Calling one of them again replaces the entry;
`null` removes it.

```csharp
// owner (mod id "6135.UIFrameworkExample", menu "demo")
IUISlot footer = ui.AddSlot(menu.Root, "demo.footer");
footer.Horizontal = true;
footer.MaxContributions = 4;
ui.Expose(menu, "name", () => name);
ui.ExposeNumber(menu, "volume", () => volume);
ui.ExposeCommand(menu, "log", () => Monitor.Log($"name={name} volume={volume}", LogLevel.Info));
menu.OnOpen = _ => ui.Publish(menu, "opened");
```

A contributor registers a build callback with `ContributeTo(ownerModId, menuId, slotId, build)` (or the overload
with a `priority`; contributions are ordered by ascending priority, then by mod id). `build` runs every time the
owning menu opens: it receives a container of its own inside the slot (id `"<slotId>.<yourModId>"`) to add elements
to through *your* API instance, and the owner's `IUIScreenContext`:

```csharp
// contributor (any mod, including the owner itself)
ui.ContributeTo("6135.UIFrameworkExample", "demo", "demo.footer", (slot, ctx) =>
{
    IUILabel info = ui.AddLabel(slot, "footer.info",
        () => $"Contributed: name={ctx.GetString("name")} volume={ctx.GetNumber("volume"):0}");
    info.VerticalAlign = UIAlign.Center;
    ui.AddButton(slot, "footer.log", () => "Log", _ => ctx.Invoke("log"));
    ctx.Subscribe("opened", () => Monitor.Log($"{ctx.OwnerModId}/{ctx.MenuId} opened", LogLevel.Trace));
});

foreach (IUISlotInfo slot in ui.ListSlots("6135.UIFrameworkExample"))
{
    Monitor.Log($"{slot.MenuId}/{slot.SlotId} ({(slot.Horizontal ? "row" : "column")})", LogLevel.Debug);
}
```

`IUIScreenContext` reads values with `GetString` / `GetNumber` / `GetBool` (conversions between the three kinds are
automatic; unknown keys give `""` / `0` / `false`), lists them with `Keys` / `HasValue`, runs commands with
`Invoke` / `HasCommand` (a faulting command is logged against the owner and muted) and listens with `Subscribe` /
`Unsubscribe`. One contribution per (mod, slot): calling `ContributeTo` again replaces it, `RemoveContribution`
removes it the next time the menu opens, and the registration is kept even if the owner's menu does not exist yet.
`ListSlots(ownerModId)` returns every slot declared by that mod's menus with their hints, so a contributor can adapt
at runtime; `ui_slots` prints the slot map of every open menu.

`OnScreenBuilt(ownerModId, menuId, decorate)` goes further than a slot: `decorate` runs every time the menu opens,
after all slot contributions and before layout, with the owner's `IUIMenu`, so it sees the final tree and can hide
an element, change a tooltip or add a button next to an existing one through `IUIMenu.Find`. Registering makes your
API instance a *decorator* of that menu, allowed to add elements outside sealed subtrees. One decorator per (mod,
menu); calling again replaces it, `null` removes it.

Owners opt an element and its descendants out of all of this with `IUIElement.Sealed = true`. The framework cannot
attribute a property setter to a caller, so sealing is enforced on lookup and tree edits instead: `IUIMenu.Find`
while a slot contribution or `OnScreenBuilt` decorator of another mod is running, and `IStardewUIApi.Find` from
another mod's API instance, return `null` for a sealed element and everything below it; `IStardewUIApi.Remove` of,
and any `Add*` into, a sealed subtree from another mod's API instance throw `InvalidOperationException`. A slot
contributor keeps full access to the container it was handed, even under a sealed ancestor. The owner of the menu is
never restricted. Contribution and decorator callbacks run under the guard of the mod that registered them: one that
throws is logged and muted.

#### Composites

A composite is a subtree packaged under a global name so that *any* mod can instantiate it without sharing an
assembly; the framework is the only DLL anyone references. Define it once with `DefineComposite(name, build)`
(convention: `"<ModId>.<Name>"`; defining an existing name replaces it, `UndefineComposite` removes one of yours,
`HasComposite` / `ListComposites` query the registry, `ui_composites` prints it). The builder receives an
`IUICompositeHost` (the container it fills, laid out as a column) and an `IUICompositeArgs` bag:

```csharp
private const string MoneyFieldName = "6135.UIFrameworkExample.MoneyField";

ui.DefineComposite(MoneyFieldName, (host, args) =>
{
    Func<double> get = args.GetNumberGetter("get") ?? (() => 0);
    Action<double> set = args.GetNumberSetter("set") ?? (_ => { });
    double max = args.Has("max") ? args.GetNumber("max") : 1000;

    IUIStack row = ui.AddStack(host, host.Id + ".row", true, 8);
    row.Alignment = UIAlign.Center;
    ui.AddLabel(row, host.Id + ".caption", () => args.GetString("label"));
    IUINumberInput input = ui.AddNumberInput(row, host.Id + ".input", get, set, 0, max, 10, true);
    input.Width = 160;
    input.OnValueChanged = _ => host.Publish("changed");
    ui.AddLabel(row, host.Id + ".suffix", () => "g");

    host.ExposeNumber("value", get);
    host.ExposeCommand("reset", () => set(0));
});
```

`IUICompositeArgs` (from `CreateCompositeArgs()`) is a string-keyed bag of proxy-safe values: `SetString` /
`SetNumber` / `SetBool`, `SetAction`, `SetGetter` / `SetSetter` (`Func<string>` / `Action<string>`),
`SetNumberGetter` / `SetNumberSetter`, plus `SetObject` for anything else (it crosses the API unproxied, so only share
types both mods know). The matching `Get*` return a default (`""`, `0`, `false`, `null`) when the key is missing or
holds another type; `Has(key)` and `Keys` inspect the bag.

Whoever uses the composite adds it like any element with `AddComposite(parent, id, name, args)` and gets an
`IUIComposite`: the same container the builder filled, plus the values (`GetValue` / `GetNumber` / `GetBool`),
commands (`Invoke` / `HasCommand`) and events (`Subscribe`) the builder exposed through the host. Edit `Args` and
call `Rebuild()` to apply new arguments. When no composite has that name yet the host stays empty (logged) until
`Rebuild()` is called after it was defined.

```csharp
IUICompositeArgs args = ui.CreateCompositeArgs();
args.SetString("label", "Money:");
args.SetNumberGetter("get", () => money);
args.SetNumberSetter("set", v => money = v);
args.SetNumber("max", 99999);
IUIComposite moneyField = ui.AddComposite(form, "money", MoneyFieldName, args);
moneyField.Row = 6;
moneyField.ColumnSpan = 2;
moneyField.Tooltip = () => $"Composite '{moneyField.CompositeName}': value = {moneyField.GetNumber("value"):0}";
moneyField.Subscribe("changed", () => Monitor.Log($"Money -> {money:0}g", LogLevel.Debug));
```

The builder runs under the defining mod's guard, and the defining mod's API instance may add children to the host
even though the menu belongs to another mod.

A custom component can embed built-in elements the same way. The `AddCustom(parent, id, implementation, build)`
overload calls `build` once with a container (`"<id>.host"`, a column) the component owns; the framework lays out,
draws, focuses and hit-tests those children like any others. The implementation draws first (its chrome), then the
host's children; the element measures as the larger of the two, or as the host alone when the implementation
returns `Vector2.Zero` from `Measure`:

```csharp
// FrameBox : IUICustomComponent draws a vanilla 9-slice frame and returns Vector2.Zero from Measure
IUIElement framed = ui.AddCustom(form, "options", new FrameBox(), host =>
{
    host.SetMargin(16);
    ui.AddCheckbox(host, "options.tips", () => showTips, v => showTips = v).Label = () => "Show tips";
    ui.AddCheckbox(host, "options.sounds", () => playSounds, v => playSounds = v).Label = () => "Play sounds";
});
framed.Row = 7;
framed.ColumnSpan = 2;
```

#### Rich text and rich tooltips

Labels and buttons parse a small markup language when `RichText = true` (default `false`):

| Markup                                   | Effect                                                                                                    |
|------------------------------------------|-----------------------------------------------------------------------------------------------------------|
| `[b]...[/b]`                             | Bold (drawn with `Utility.drawBoldText`).                                                                 |
| `[color=#RRGGBB]...[/color]`             | Colored span; `#RRGGBBAA` is accepted too.                                                                |
| `[color=red]...[/color]`                 | Named colors: `red`, `green`, `blue`, `gray` / `grey`, `white`, `black`, `yellow`, `orange`, `purple`.    |
| `[icon=(O)24]`                           | Inline sprite of a vanilla item, by qualified item id.                                                    |
| `[link=name]...[/link]`                  | Clickable span (blue, orange while hovered, underlined); labels raise `OnLink` with `name`.                |
| `[[` / `]]`                              | Literal `[` / `]`.                                                                                        |

Tags nest, are case-insensitive and unknown tags are drawn verbatim. Links are not clickable on buttons. Rich text
wraps like plain text (`Wrap`) and honours `Font`, `Scale`, `TextAlign` and the style / theme colors for unstyled
runs.

```csharp
IUILabel header = ui.AddLabel(menu.Root, "list.header",
    () => "[b]Items[/b] - [color=green]30[/color] rows, [link=help]help[/link]");
header.RichText = true;
header.OnLink = link => Monitor.Log($"Link clicked: {link}", LogLevel.Info);
```

A rich tooltip is built from blocks with the `CreateTooltip()` builder and assigned to `IUIElement.RichTooltip`;
when set it replaces `Tooltip` / `TooltipTitle` (`null` goes back to the plain tooltip). Every method returns the
builder so calls chain, line text supports the markup above, and delegates are evaluated each frame the tooltip is
visible. It is drawn in a vanilla box sized to its content after the usual tooltip delay.

| Builder method                                    | Block                                                                  |
|---------------------------------------------------|------------------------------------------------------------------------|
| `Title(Func<string>)`                             | Bold title in the dialogue font.                                       |
| `Line(Func<string>)`, `Line(Func<string>, Color)` | A line of rich text in the default text color, or in `color`.          |
| `Icon(Texture2D, Rectangle?, float)`              | A block icon on its own row.                                           |
| `Item(string qualifiedItemId)`                    | A vanilla item's sprite and display name on one row (e.g. `"(O)24"`).  |
| `Divider()`                                       | A horizontal rule.                                                     |
| `Money(Func<int>)`                                | A coin icon followed by the amount, like vanilla shop tooltips.        |
| `MaxWidth(int px)`                                | Wrap lines wider than this (0 = only the screen limits the width).     |
| `Clear()`                                         | Remove every block.                                                    |

```csharp
IUIElement ok = menu.Find("ok");
ok.RichTooltip = ui.CreateTooltip()
    .Title(() => "Submit")
    .Line(() => "Enter also triggers this button.")
    .Divider()
    .Item("(O)24")
    .Money(() => 35 * (clicks + 1))
    .Line(() => $"Clicked [b]{clicks}[/b] times", Color.DarkGreen)
    .MaxWidth(360);
```

#### Data grid

`AddDataGrid(parent, id, rowHeight, visibleRows, rowCount)` adds an `IUIDataGrid`: a virtualized table with a header
row, sortable / resizable columns, filtering, row selection and keyboard navigation. Like `IUIList`, only the visible
rows exist as elements; `rowCount` is re-queried every tick and on `Refresh()`. Rows are always addressed by their
**underlying** index (0 .. rowCount - 1, as your data source knows them); sorting and filtering only change the
display order.

Columns are appended with `AddColumn(id, header, width)` (`width` uses the grid track syntax: `auto`, `120px`, `*`,
`2*`; star columns share the leftover width) and configured through the returned `IUIDataGridColumn`: `Text`
(`Func<int, string>`, the cell text for a row and the default sort key), `SortKey` (string) or `SortNumber` (numeric
sort), `Sortable` (clicking the header toggles ascending / descending), `Resizable` (drag the divider right of the
header; the width becomes pixels and `OnColumnResized` fires), `MinWidth`, `Align`, `BuildCell` (`Action<int,
IUIContainer>`, a custom renderer instead of a text label) and `CellTooltip`. `RemoveColumn`, `ColumnCount`,
`GetColumn` and `FindColumn` manage them afterwards.

```csharp
string[] names = { "Parsnip", "Cauliflower", "Potato", "Kale", "Melon", "Blueberry", "Pumpkin", "Cranberries" };
int count = 40;
IUIDataGrid grid = ui.AddDataGrid(menu.Root, "grid", 40, 5, () => count);
grid.Selectable = true;

IUIDataGridColumn item = grid.AddColumn("item", () => "Item", "*");
item.Text = row => $"{names[row % names.Length]} #{row + 1}";
item.Sortable = true;
item.Resizable = true;
item.MinWidth = 120;

IUIDataGridColumn price = grid.AddColumn("price", () => "Price", "140px");
price.Text = row => $"{(row * 37) % 500 + 25}g";
price.SortNumber = row => (row * 37) % 500 + 25;
price.Align = UIAlign.End;
price.Sortable = true;
price.CellTooltip = row => $"Row {row}: {names[row % names.Length]}";

grid.Filter = row => row % 2 == 0;      // underlying index; call grid.Refresh() when what it returns changes
grid.Sort("price", descending: true);   // or let the player click the header
grid.OnValueChanged = e => Monitor.Log($"Selected {e.NewIndex} (was {e.OldIndex})", LogLevel.Info);
grid.OnRowActivated = row => Monitor.Log($"Activated {row}", LogLevel.Info);
grid.OnRowClick = e =>
{
    if (e.Button == UIMouseButton.Right)
    {
        Monitor.Log($"Right-clicked row {e.Row} at {e.X},{e.Y}", LogLevel.Debug);
    }
};
```

Sorting: `Sort(columnId, descending)` / `ClearSort()` from code, `SortColumn` / `SortDescending` to read the state.
Filtering: `Filter` is a row predicate over underlying indices (`null` shows every row); `RowCount` is the number of
rows shown after filtering at the last refresh. `Refresh()` re-queries the row count, re-runs the filter and sort and
rebuilds the visible rows; the selection is kept by underlying index.

Selection (`Selectable`): a click selects a row; with `MultiSelect`, Ctrl-click toggles a row and Shift-click selects
a range. `SelectedRow` is the primary row (or -1) and `SelectedRows` every selected row in display order; setting
either replaces the selection without raising events, `ClearSelection()` empties it. Events: `OnValueChanged` when
the user changes the selection (`OldIndex` / `NewIndex` are underlying indices of the primary row), `OnRowClick`
for every left or right click on a row after selection was applied (an `IUIRowEvent`: a click event plus the
underlying `Row`), `OnRowActivated` on a double-click or Enter with a selected row, `OnScroll` after scrolling
(row delta). The grid is focusable: while focused, Up / Down / PageUp / PageDown / Home / End move the selection
(Shift extends it in multi-select) and Enter activates the primary row; the wheel scrolls one row per notch.
`ScrollToRow(row)` brings an underlying row into view (no-op when filtered out); `FirstVisibleIndex` is a display
position. `ScrollSound`, `SelectSound` and `SortSound` follow the usual rule (`null` = default, empty = silent).

#### Signals and bindings

Signals are reactive values with automatic dependency tracking, for screens whose text or visibility depend on
several inputs without recomputing everything every frame. `Signal(initial)`, `SignalNumber(initial)` and
`SignalBool(initial)` create an `IUISignal`: one stored value exposed through three typed views (`Value`, `Number`,
`Flag`); the setter used last decides the kind and the other views convert from it (a number reads as its invariant
text and as `true` when non-zero, text reads as a number when it parses, a flag reads as `True` / `False` and
1 / 0). `Version` increments on every change and `Subscribe` / `Unsubscribe` run a handler after each change.

`Computed(Func<string>)`, `ComputedNumber(Func<double>)` and `ComputedBool(Func<bool>)` create an `IUIComputed`:
every signal (or computed) read while the delegate runs becomes a dependency, the value is lazy and cached, and it is
recomputed on the next read after a dependency changed. A dependency cycle is logged and the stale value kept.
`Subscribe` handlers run once per batch of changes, after the computed was invalidated.

Bindings connect them to elements: `BindText(label, computed)` / `BindText(label, signal)` show the value in a
label that only re-flows when the version changes; `BindVisible(element, computed)` and `BindEnabled(element,
computed)` drive `Visible` / `Enabled` from the computed's `Flag`; `BindValue(input, signal)` is two-way for text
inputs, number inputs, checkboxes, sliders and dropdowns (the input reads the signal instead of its getter and writes
it when edited; the setter it was created with is still called afterwards, so your own fields stay in sync).
`Unbind(element)` drops every
binding on an element and restores its original delegates; bindings are also dropped when the element leaves its
menu.

```csharp
IUISignal daySignal = ui.SignalNumber(day);
IUISignal seasonSignal = ui.Signal(season);
ui.BindValue(dayInput, daySignal);            // an IUINumberInput created earlier
ui.BindValue(seasonDropdown, seasonSignal);   // an IUIDropdown created earlier

IUIComputed summary = ui.Computed(() => $"Day {daySignal.Number:0} of {seasonSignal.Value}");
IUILabel summaryLabel = ui.AddLabel(menu.Root, "signals.summary", () => string.Empty);
ui.BindText(summaryLabel, summary);
ui.BindVisible(summaryLabel, ui.ComputedBool(() => daySignal.Number > 0));
summary.Subscribe(() => Monitor.Log($"Summary changed (v{summary.Version}): {summary.Value}", LogLevel.Trace));
```

#### Auto-forms

`AddForm(parent, id, model)` generates a complete settings form from a plain object: a two-column grid of caption /
input rows (with optional section headers and per-row validation messages) above a Save / Cancel / Undo / Redo
button row, returned as an `IUIForm`. The public read / write instance properties of `model` are used in declaration
order (base class first): `bool` becomes a checkbox, `int` / `uint` / `long` / `float` / `double` / `decimal` a number
input (integral types step by 1 with no decimals), `string` a text input (or a dropdown with a `Choices` attribute),
enums a dropdown of their names; other types are skipped with a debug log line. Captions come from the property
name split on camel case (`FarmName` becomes `Farm Name`).

Attributes are matched **by type name** (`Range` or `RangeAttribute`, and so on), so you can declare your own in your
mod or use the BCL ones from `System.ComponentModel` / `System.ComponentModel.DataAnnotations` without any shared
assembly:

| Attribute                  | Read from                                                                          | Effect                                                     |
|----------------------------|------------------------------------------------------------------------------------|------------------------------------------------------------|
| `Range(min, max)`          | `Minimum` / `Maximum` (or `Min` / `Max`) members; numbers or numeric strings.       | Number input range (otherwise the type's limits).          |
| `Choices(...)`             | A `string[] Values` property, else a `Csv` (or string `Values`) property.          | Turns a `string` property into a dropdown.                 |
| `Section(title)`           | `Title`, `Name` or `Text` (else the first string property).                        | A dialogue-font section title above the row.               |
| `Tooltip(text)`            | `Text`, `Tooltip` or `Description`; `Display(Description = ...)` also works.       | Tooltip on the generated input.                            |
| `DisplayName` / `Display`  | `DisplayName` or `Name`.                                                           | Caption instead of the split property name.                |
| `ReadOnly`                 | `IsReadOnly` when present (else read-only).                                        | The input is disabled.                                     |

A method named `Validate<Property>` on the model (public or not) runs before each write: it either takes the
prospective value as its single parameter, or takes no parameter and sees the value through the property (which is
restored afterwards). It returns `bool` (`false` rejects with the generic "Invalid value" message) or a `string`
(an error message; `null` / empty accepts). The message is shown under the row and the edit is rejected.

```csharp
public sealed class DemoSettings
{
    [Section("Farm")]
    public string FarmName { get; set; } = "Stardew";

    [Range(1, 28)]
    public int Day { get; set; } = 1;

    [Choices("spring,summer,fall,winter")]
    public string Season { get; set; } = "spring";

    public bool Pets { get; set; } = true;

    [Section("Game")]
    [Tooltip("0-100")]
    public double Volume { get; set; } = 50;

    public Difficulty Difficulty { get; set; } = Difficulty.Normal;

    public string? ValidateFarmName(string value) =>
        string.IsNullOrWhiteSpace(value) ? "The farm needs a name." : null;
}

IUIForm form = ui.AddForm(menu.Root, "settings", settings);
form.OnSaved = _ => Helper.WriteConfig(settings);
form.OnCancelled = _ => Monitor.Log("Settings cancelled.", LogLevel.Info);
form.OnChanged = f => Monitor.Log($"dirty={f.IsDirty} undo={f.CanUndo} redo={f.CanRedo}", LogLevel.Trace);
```

Edits write straight into the model; the form keeps a snapshot for `Cancel()` and an undo / redo history of every
committed change (`CanUndo` / `CanRedo` / `Undo()` / `Redo()`; Ctrl+Z and Ctrl+Y or Ctrl+Shift+Z while a field is
focused). `IsDirty` is true while any property differs from the last `Save()` (or the initial snapshot); `Save()`
clears the dirty state and the history and raises `OnSaved`; `Cancel()` restores the snapshot, refreshes the
controls and raises `OnCancelled`; `OnChanged` fires after every committed edit, undo, redo or cancel. Call
`Refresh()` after changing the model from code. `ShowButtons = false` hides the button row if you provide your own,
and `FieldFor(propertyName)` returns the generated input for a property so you can style it. The button captions
come from the framework's `i18n` (`form.save`, `form.cancel`, `form.undo`, `form.redo`, `form.invalid`).

#### HUD widgets and toasts

`CreateHud(id)` returns an `IUIHud`: a tree of ordinary elements (build it under `hud.Root` with the same `Add*`
calls) drawn over the world from SMAPI's `Display.RenderedHud`, never as a menu. It hangs from a screen corner or
edge (`Anchor` plus the `X` / `Y` offsets), sizes to its content unless `Width` / `Height` are set, and draws a panel
box behind the content (`DrawBox`, `Opacity`). It hides itself while any menu is open, during events and while the
vanilla HUD is hidden, plus whenever `Visible` is false or `ShowWhen` returns false. With `Interactive = true` it
receives hover and clicks while no menu is open (the game only loses a click that an element handled) and the player
can drag it; the offset is saved with the game. HUD widgets never take keyboard focus, so text inputs in them are
display-only. `OnUpdate` runs every tick while shown.

`ShowToast(text)`, `ShowToast(text, durationMs)` and `ShowToastWithIcon(text, icon, source, durationMs)` queue
notifications in the bottom-left corner: at most five are shown, each fades in and out over 200 ms and older ones
slide up when a newer one arrives. Toasts are drawn from `Display.RenderedHud` while no menu is open and from
`Display.RenderedActiveMenu` while one is, so they stay visible on top of menus.

#### Player-owned layout

Windows are movable, collapsible and (when fixed-size) resizable by the player, and the result is saved per save
file under the key `"<yourModId>/<menuId>"`. This needs no code; set `PlayerLayout = false` on the menu or its
options to opt out, and call `ResetPlayerLayout(menu)` to drop the saved layout and restore the anchor / position /
size you set. A saved position is applied when the menu opens (before `OnOpen`), so values you set in `OnOpen`
override it. Interactive HUD widgets save their dragged anchor offset under `"hud:<yourModId>/<hudId>"` the same way.

#### Themes and accessibility

The player's theme (see [Themes](#themes)) is the bottom layer of every style resolution, so a mod gets it for free.
The theme asset is `Mods/6135.UIFramework/Themes` (a `Dictionary<string, ThemeData>` whose defaults come from
`assets/themes.json`; four themes ship: `default`, `dark`, `high-contrast`, `colorblind`) and Content Patcher packs
can add entries to it. A mod that wants to offer the choice in its own screen uses `ListThemes()`, `ActiveTheme` and
`SetTheme(name)` (which also saves the choice in the framework config); `ThemeColor(key)` returns the active
`text`, `disabled-text`, `hover`, `scrollbar` or `border` color (unknown keys return the text color) so custom
components match the theme:

```csharp
IUIStack row = ui.AddStack(menu.Root, "theme-row", true, 24);
ui.AddLabel(row, "theme.label", () => "Theme:");
IUIDropdown theme = ui.AddDropdown(row, "theme", ui.ListThemes, ui.ListThemes, () => ui.ActiveTheme, ui.SetTheme);
theme.AccessibleName = () => $"Theme dropdown: {ui.ActiveTheme}";
theme.OnValueChanged = e => ui.Announce($"Theme changed to {e.NewValue}");
```

Text scaling needs no code: the theme's `FontScale` times the player's `TextScale` is applied inside text
measurement and drawing, so labels, buttons, inputs and dropdowns grow together with their layout.

Screen reader support goes through [Stardew Access](https://www.nexusmods.com/stardewvalley/mods/16205) (mod id
`shoaib.stardewaccess`, 1.6.2+): when it is installed the framework speaks the menu title on open, the focused
element when focus moves, value changes on the focused element, selected list rows and the element under the cursor
once it rested there for the tooltip delay. The spoken text is `"<type>: <label / text / value>"` built from the
framework's `i18n` (`a11y.*` keys, e.g. "Button: OK", "Checkbox: Pay for seeds, checked", "Row 3 of 30"); set
`IUIElement.AccessibleName` (`Func<string>`) to replace it for one element, and call `Announce(text)` to speak your
own text (a no-op without a screen reader). `ReducedMotion` mirrors the player's config: the framework keeps the
text caret solid, and custom components should skip their own animations when it is `true`.

#### Pseudo-localization

With `PseudoLocalize` on (config, GMCM or the `ui_pseudoloc` console command) every string that reaches the
framework's text path is accented, padded with `~` (30 % of its length) and bracketed: `Calculate` draws as
`[Çálçúláté~~~]`. Labels, buttons, checkbox labels, dropdown rows, placeholders, menu titles and plain tooltips are
transformed; text the player types, ids, link names and markup tags are not (rich text accents its text runs and
pads the whole document). Turn it on while developing to find labels that overflow their column or wrap badly
before a translation exists. Nothing in your code changes: the transformation happens at draw / measure time.

#### Inspector and debug console

The inspector is a devtools-style overlay for framework menus. Toggle it with the `ui_inspect` console command or
the `InspectorHotkey` keybind (`F10` by default, configurable in GMCM) while a framework menu is open. While it is
on, the menu stops routing input and instead:

- every element's bounds are outlined (containers blue, leaves green, the inspected element orange with a
  translucent fill), margins are drawn as tinted bands and grid tracks as dotted lines;
- an info panel (placed away from the cursor) describes the element under the cursor: type and id, bounds and
  desired size, margins and explicit size, resolved alignment (`*` = parent default), grid cell or canvas position,
  style overrides, state (visible / enabled / focusable / focused) and the parent chain;
- keys edit the element live: **arrows** nudge `MarginLeft` / `MarginTop` (Shift x8), **+** / **-** change `Width`
  (Shift: `Height`; Ctrl x8; an element without an explicit size starts from its arranged size), **V** toggles
  `Visible`, **P** or a click pins the element so the panel stays on it while the cursor moves, **E** exports the
  menu, **Esc** turns the inspector off. Layout-only containers (stacks, grids, spacers) can be inspected even though
  they are not hit-testable.

Export (**E** or `ui_export [<consumerId> <menuId>]`) writes the C# builder code that would recreate the menu
through the public API (`api.CreateMenu`, one `Add*` call per element with the right nesting and ids, an assignment
for every non-default property) to the SMAPI log, to `Mods/UIFramework/export/<consumerId>-<menuId>.cs` and to the
clipboard when the platform allows it. Delegates and textures cannot be serialized: the current value of text
delegates is exported as a lambda so the screen looks the same, and every such spot carries a `/* TODO */` marker.
Nudge a layout in-game, export it, paste the numbers back into your code.

The other console commands are diagnostics: `ui_list` (open menus with consumer, id, host type and element count),
`ui_dump [<consumerId> <menuId>]` (the element tree with bounds and state; the most recently opened menu without
arguments), `ui_find <text>` (elements of the open menus whose id contains the text), `ui_perf on|off|show`
(per-menu timing of the last 60 frames, off by default), `ui_slots` (slot map of every open menu with hints and
contributors), `ui_composites` (defined composite names), `ui_debug` (the simpler bounds-and-ids overlay) and
`ui_pseudoloc`. The `LogCallbacks` config flag traces every callback the framework invokes with your mod id,
element id and event name.

#### Headless testing

`StardewUIFramework.Tests` (xUnit) runs the framework without the game through the harness in its `Testing/`
folder, and shows how a consumer can test its own screens in CI:

| Class               | Role                                                                                                                                                                                                                     |
|---------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `TestHost`          | Installs fakes into the framework's services (monospace text measurement, a sound recorder, a fixed 1280x720 `Viewport`, a manual clock `NowMs` / `Advance`, an in-memory keyboard `Subscriber`) and creates a consumer context plus a `StardewUIApi` as `ModEntry` would (`Api`, consumer id `test.consumer`). `CreateBareMenu(id)` makes a chrome-less menu so bounds are easy to predict, `Layout(menu)` runs a layout pass, `Drive(menu)` returns an input driver, `Sounds` / `ClearSounds` inspect the cues played, `Config` is the `ModConfig` in effect. |
| `InputDriver`       | Scripts input the way `MenuHost` feeds it from the game: `Hover`, `Click` / `RightClick` (return whether something handled it), `Drag`, `Scroll`, `Key(key, shift, ctrl)` (routed like `receiveKeyPress` and, for text inputs, also as the keyboard dispatcher would), `Type` (`'\b'` is a backspace), `Paste`, `Tick(elapsedMs)`; `Focused` / `Hovered` read the state. A layout pass runs before every action and again afterwards when the action dirtied it. |
| `TreeSnapshot`      | `Render(menu)` lays the menu out and returns a deterministic text dump (type, id and bounds per line, indented by depth) for snapshot assertions; `Render(element)` dumps a subtree without a layout pass.                     |
| `FakeTextMeasurer`  | The monospace `ITextMeasurer`: every character is 8 px wide (times the scale) and every line 16 px tall whatever the font; wrapping is greedy on spaces.                                                                    |
| `GameAssemblies`    | Resolves the game, SMAPI and MonoGame assemblies from the game folder at run time (from the `GamePath` assembly metadata the project bakes in).                                                                            |

Menus are never opened as `IClickableMenu`s in tests (`Game1.activeClickableMenu`'s setter needs `Game1.player`);
the harness lays them out and drives them directly. The fakes live in the framework's static services, so the test
assembly disables xUnit parallelization.

```csharp
[Fact]
public void NumberInputStepsAndClamps()
{
    var host = new TestHost();
    IUIMenu menu = host.CreateBareMenu("m");
    double value = 5;
    IUINumberInput number = host.Api.AddNumberInput(menu.Root, "n", () => value, v => value = v, 1, 28, 1, true);
    InputDriver input = host.Drive(menu);

    input.Click(number.Bounds.Center.X, number.Bounds.Center.Y);
    Assert.True(number.IsFocused);
    input.Key(Keys.Up);
    Assert.Equal(6, value);
    input.Type("9");                 // "69" clamps to 28
    Assert.Equal(28, value);

    Assert.Contains("NumberInput 'n'", TreeSnapshot.Render(menu));
}
```

Run the suite from the repository root (the project references the game assemblies through ModBuildConfig, so
`STARDEW_GAME_DIR` must point at the game folder exactly as for a build; the two properties stop ModBuildConfig from
deploying or zipping the test assembly as a mod):

```sh
dotnet test StardewUIFramework.Tests -p:EnableModDeploy=false -p:EnableModZip=false
```

The harness is compiled into the test project and uses the framework's internals (`UIFramework.csproj` declares
`InternalsVisibleTo("StardewUIFramework.Tests")`), so it is not yet a package a consumer can reference. To test your
own screens the same way today, add your test classes to `StardewUIFramework.Tests` (they drive menus through
`host.Api`, the same `IStardewUIApi` surface your mod uses), or copy `Testing/*.cs` into a test project of your own
named `StardewUIFramework.Tests` with a project reference to `UIFramework.csproj`.

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

`IUIRowEvent : IUIClickEvent` (a click on a data grid row, see `IUIDataGrid.OnRowClick`):

| Member    | Description                                                                                       |
|-----------|---------------------------------------------------------------------------------------------------|
| `int Row` | Underlying row index (the index handed to the column delegates), not the display position.        |

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
| `bool Sealed`                                                | Opt this element (and its descendants) out of modification by other mods: hidden from their `Find`, and their `Add*` / `Remove` on the subtree throw (see [Extension slots and screen context](#extension-slots-and-screen-context)). |
| `IUITooltip RichTooltip`                                     | Rich tooltip built with `CreateTooltip`; when set it replaces `Tooltip` / `TooltipTitle`, `null` shows the plain tooltip. |
| `Func<string> AccessibleName`                                | Screen reader description override (`null` = the framework's "`<type>: <label / text / value>`" description). |

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
| `bool RichText`     | Parse markup in `Text`: `[color=#RRGGBB]...[/color]` (or `red`, `green`, `blue`, `gray`), `[b]...[/b]`, `[icon=(O)24]`, `[link=name]...[/link]`; `[[` / `]]` are literal brackets. Default `false`. |
| `Action<string> OnLink` | Raised with the link name when a `[link=name]` span is clicked (`RichText` only).             |

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
| `bool RichText`         | Parse markup in `Text` (same syntax as `IUILabel.RichText`; links are not clickable on buttons). Default `false`. |

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
| `bool PlayerLayout`    | Let the player move / collapse / resize the window (default `true`). |

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
| `bool PlayerLayout`                 | Let the player move / collapse / resize the window; persists per save (default `true`, needs `DrawBox`). |

#### Slot interfaces

`IUISlot : IUIContainer` (an extension slot from `AddSlot`: a stack-like container the framework fills with one
child container per contributing mod whenever the menu opens; the framework manages `Visible`: shown while it has
contributions and `VisiblePredicate`, if any, returns true):

| Member                         | Description                                                                    |
|--------------------------------|--------------------------------------------------------------------------------|
| `bool Horizontal`              | Layout hint: lay contributions out in a row instead of a column (default `false`). |
| `int? MaxHeight`               | Layout hint: cap the slot's measured height in UI pixels (`null` = unlimited).  |
| `Func<bool> VisiblePredicate`  | Evaluated every tick; `false` hides the slot (`null` = always visible).         |
| `int MaxContributions`         | Most contributions accepted, in priority order (0 = unlimited).                 |
| `string[] VetoedContributors`  | Mod ids whose contributions are skipped (`null` = none).                        |

`IUISlotInfo` (a declared slot as reported by `ListSlots`):

| Member               | Description                            |
|----------------------|----------------------------------------|
| `string OwnerModId`  | Mod that owns the menu.                |
| `string MenuId`      | Id of the menu.                        |
| `string SlotId`      | Id of the slot.                        |
| `bool Horizontal`    | The slot's row / column hint.          |
| `int? MaxHeight`     | The slot's height cap.                 |

`IUIScreenContext` (what a menu owner chose to share with contributors through the `Expose*` / `Publish` members;
nothing else of the owner is reachable; values are read live):

| Member                                                | Description                                                                                        |
|-------------------------------------------------------|----------------------------------------------------------------------------------------------------|
| `string OwnerModId`                                   | Mod that owns the menu.                                                                            |
| `string MenuId`                                       | Id of the menu (unique within the owner).                                                          |
| `string[] Keys`                                       | Keys of every exposed value (string, number and bool).                                             |
| `bool HasValue(string key)`                           | Whether a value (of any kind) is exposed under `key`.                                              |
| `string GetString(string key)`                        | Exposed string value; numbers and bools are converted (invariant culture). Empty string when unknown. |
| `double GetNumber(string key)`                        | Exposed number; strings are parsed (invariant culture) and bools map to 1 / 0. 0 when unknown.     |
| `bool GetBool(string key)`                            | Exposed bool; strings are parsed and numbers are true when non-zero. False when unknown.           |
| `bool HasCommand(string command)`                     | Whether the owner exposed `command`.                                                               |
| `void Invoke(string command)`                         | Run an exposed command (no-op when unknown; a faulting command is logged against the owner and muted). |
| `void Subscribe(string eventName, Action handler)`    | Run `handler` whenever the owner publishes `eventName`.                                            |
| `void Unsubscribe(string eventName, Action handler)`  | Stop a handler registered through `Subscribe`.                                                     |

#### Composite interfaces

`IUICompositeArgs` (string-keyed bag of proxy-safe values handed to a composite's builder; getters return a default
(`""`, 0, `false`, `null`) when the key is missing or holds a value of another type):

| Member                                                    | Description                                                                             |
|-----------------------------------------------------------|-----------------------------------------------------------------------------------------|
| `void SetString(string key, string value)`                | Store a string.                                                                         |
| `void SetNumber(string key, double value)`                | Store a number.                                                                         |
| `void SetBool(string key, bool value)`                    | Store a flag.                                                                           |
| `void SetAction(string key, Action value)`                | Store a command.                                                                        |
| `void SetGetter(string key, Func<string> value)`          | Store a string getter.                                                                  |
| `void SetSetter(string key, Action<string> value)`        | Store a string setter.                                                                  |
| `void SetNumberGetter(string key, Func<double> value)`    | Store a number getter.                                                                  |
| `void SetNumberSetter(string key, Action<double> value)`  | Store a number setter.                                                                  |
| `void SetObject(string key, object value)`                | Store any object; it crosses the API unproxied, so only share types both mods know.     |
| `string GetString(string key)`                            | Read a string.                                                                          |
| `double GetNumber(string key)`                            | Read a number.                                                                          |
| `bool GetBool(string key)`                                | Read a flag.                                                                            |
| `Action GetAction(string key)`                            | Read a command.                                                                         |
| `Func<string> GetGetter(string key)`                      | Read a string getter.                                                                   |
| `Action<string> GetSetter(string key)`                    | Read a string setter.                                                                   |
| `Func<double> GetNumberGetter(string key)`                | Read a number getter.                                                                   |
| `Action<double> GetNumberSetter(string key)`              | Read a number setter.                                                                   |
| `object GetObject(string key)`                            | Read an object.                                                                         |
| `bool Has(string key)`                                    | Whether any value is stored under `key`.                                                |
| `string[] Keys`                                           | Every stored key.                                                                       |

`IUICompositeHost : IUIContainer` (the container a composite builder fills; children are laid out in a column; what
the builder exposes here is what the composite's user reaches through `IUIComposite`):

| Member                                              | Description                                                    |
|-----------------------------------------------------|----------------------------------------------------------------|
| `void Expose(string key, Func<string> value)`       | Publish a read-only string value under `key`.                  |
| `void ExposeNumber(string key, Func<double> value)` | Publish a read-only number under `key`.                        |
| `void ExposeBool(string key, Func<bool> value)`     | Publish a read-only boolean under `key`.                       |
| `void ExposeCommand(string key, Action command)`    | Publish a command the user can run with `IUIComposite.Invoke`. |
| `void Publish(string eventName)`                    | Notify every `IUIComposite.Subscribe` handler of `eventName`.  |

`IUIComposite : IUIContainer` (an instance of a composite from `AddComposite`: the same container the builder
filled, plus the values, commands and events the builder exposed):

| Member                                              | Description                                                             |
|-----------------------------------------------------|-------------------------------------------------------------------------|
| `string CompositeName`                              | The global name it was created from.                                    |
| `IUICompositeArgs Args`                             | The arguments it was created with (edit them and `Rebuild` to apply).   |
| `void Rebuild()`                                    | Clear the children and run the builder again.                           |
| `string GetValue(string key)`                       | Read an exposed string value (empty when not exposed).                  |
| `double GetNumber(string key)`                      | Read an exposed number (0 when not exposed).                            |
| `bool GetBool(string key)`                          | Read an exposed boolean (`false` when not exposed).                     |
| `void Invoke(string command)`                       | Run an exposed command (no-op when not exposed).                        |
| `bool HasCommand(string command)`                   | Whether the builder exposed `command`.                                  |
| `void Subscribe(string eventName, Action handler)`  | Run `handler` whenever the composite publishes `eventName`.             |

#### `IUITooltip`

A tooltip built from blocks, drawn in a vanilla box sized to its content. Every method returns the builder so calls
chain; line text supports the `IUILabel.RichText` markup; delegates are evaluated each frame the tooltip is visible.
Create with `CreateTooltip()` and assign to `IUIElement.RichTooltip`.

| Member                                                      | Description                                                               |
|-------------------------------------------------------------|---------------------------------------------------------------------------|
| `IUITooltip Title(Func<string> title)`                      | Bold title drawn in the dialogue font.                                    |
| `IUITooltip Line(Func<string> text)`                        | A line of (rich) text in the default text color.                          |
| `IUITooltip Line(Func<string> text, Color color)`           | A line of (rich) text in `color`.                                         |
| `IUITooltip Icon(Texture2D texture, Rectangle? source, float scale)` | A block icon drawn on its own row.                               |
| `IUITooltip Item(string qualifiedItemId)`                   | A vanilla item's sprite and display name on one row (qualified id, e.g. `(O)24`). |
| `IUITooltip Divider()`                                      | A horizontal rule.                                                        |
| `IUITooltip Money(Func<int> amount)`                        | A coin icon followed by the amount, like vanilla shop tooltips.           |
| `IUITooltip MaxWidth(int px)`                               | Wrap lines wider than this many UI pixels (0 = only the screen limits the width). |
| `IUITooltip Clear()`                                        | Remove every block.                                                       |

#### Data grid interfaces

`IUIDataGridColumn` (one column of an `IUIDataGrid`; every row delegate receives the underlying row index):

| Member                                  | Description                                                                                                      |
|-----------------------------------------|------------------------------------------------------------------------------------------------------------------|
| `string Id`                             | Id given to `IUIDataGrid.AddColumn`.                                                                             |
| `Func<string> Header`                   | Header text, evaluated every frame.                                                                              |
| `string Width`                          | Track width: `auto`, `120px`, `*` or `2*` (star columns share the leftover width). A drag resize turns it into pixels. |
| `bool Sortable`                         | Clicking the header sorts by this column (toggles ascending / descending).                                       |
| `bool Resizable`                        | The divider right of the header can be dragged to resize the column.                                             |
| `int MinWidth`                          | Smallest width in UI pixels (resize floor, also applied to the resolved track).                                  |
| `UIAlign Align`                         | Horizontal alignment of the header and of the default text cells.                                                |
| `Func<int, string> Text`                | Cell text for a row index (used when `BuildCell` is `null`; also the default sort key).                          |
| `Func<int, string> SortKey`             | String sort key for a row index (`null` = sort by `Text`). Ignored when `SortNumber` is set.                     |
| `Func<int, double> SortNumber`          | Numeric sort key for a row index; when set the column sorts numerically.                                         |
| `Action<int, IUIContainer> BuildCell`   | Custom cell renderer: build elements into the cell container instead of a text label.                            |
| `Func<int, string> CellTooltip`         | Tooltip for a cell (`null` = none; an empty string hides the tooltip for that row).                              |

`IUIDataGrid : IUIContainer` (virtualized table with a header row, sortable / resizable columns, filtering, row
selection and keyboard navigation; rows are addressed by their **underlying** index, sorting and filtering only
change the display order; only the visible rows exist as elements):

| Member                                                    | Description                                                                                                                   |
|-----------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------|
| `int RowHeight`                                           | Height of one row in UI pixels.                                                                                               |
| `int VisibleRows`                                         | Number of rows shown at once.                                                                                                 |
| `int FirstVisibleIndex`                                   | Display position (after filter / sort) of the first visible row.                                                              |
| `int RowCount`                                            | Number of rows shown after filtering (at the last refresh).                                                                   |
| `IUIDataGridColumn AddColumn(string id, Func<string> header, string width)` | Append a column; `width` uses the track syntax (`auto`, `120px`, `*`, `2*`).                                |
| `void RemoveColumn(string columnId)`                      | Remove a column by id (no-op when unknown).                                                                                   |
| `int ColumnCount`                                         | Number of columns.                                                                                                            |
| `IUIDataGridColumn GetColumn(int index)`                  | Column at `index`.                                                                                                            |
| `IUIDataGridColumn FindColumn(string columnId)`           | Find a column by id, or `null`.                                                                                               |
| `Action<string, int> OnColumnResized`                     | Raised after a drag resize with the column id and its new width in pixels.                                                    |
| `string SortColumn`                                       | Id of the column the rows are sorted by, or an empty string.                                                                  |
| `bool SortDescending`                                     | Whether the sort is descending.                                                                                               |
| `void Sort(string columnId, bool descending)`             | Sort by a column (an unknown id clears the sort).                                                                             |
| `void ClearSort()`                                        | Remove the sort.                                                                                                              |
| `Func<int, bool> Filter`                                  | Row predicate (underlying index); `null` shows every row. Call `Refresh` after changing what it returns.                      |
| `void Refresh()`                                          | Re-query the row count, re-run the filter and sort, rebuild the visible rows. Selection is kept by underlying index.          |
| `bool Selectable`                                         | Whether clicking a row selects it.                                                                                            |
| `bool MultiSelect`                                        | Ctrl-click toggles a row, Shift-click selects a range.                                                                        |
| `int SelectedRow`                                         | Primary selected row (underlying index) or -1. Setting it replaces the selection without raising events.                      |
| `int[] SelectedRows`                                      | Every selected row (underlying indices) in display order. Setting it replaces the selection without raising events.           |
| `void ClearSelection()`                                   | Deselect every row.                                                                                                           |
| `Action<IUIValueEvent> OnValueChanged`                    | Raised when the user changes the selection (`OldIndex` / `NewIndex` are underlying indices of the primary row).               |
| `Action<IUIRowEvent> OnRowClick`                          | Raised for every click on a row (left or right), after selection was applied.                                                 |
| `Action<int> OnRowActivated`                              | Raised on a double-click on a row or Enter with a selected row; the argument is the underlying index.                         |
| `Action<int> OnScroll`                                    | Raised after scrolling; argument is the row delta.                                                                            |
| `void ScrollToRow(int row)`                               | Scroll so the underlying row is visible (no-op when it is filtered out).                                                      |
| `string ScrollSound`                                      | Scroll sound cue (`null` = default, empty = silent).                                                                          |
| `string SelectSound`                                      | Selection sound cue (`null` = default, empty = silent).                                                                       |
| `string SortSound`                                        | Sort sound cue (`null` = default, empty = silent).                                                                            |

#### Signal and form interfaces

`IUISignal` (a reactive value; one stored value exposed through three typed views, the setter used last decides the
kind and the other views convert from it; reading a signal while an `IUIComputed` is computing registers it as a
dependency):

| Member                            | Description                                                                              |
|-----------------------------------|------------------------------------------------------------------------------------------|
| `string Value`                    | The value as text.                                                                       |
| `double Number`                   | The value as a number.                                                                   |
| `bool Flag`                       | The value as a flag.                                                                     |
| `int Version`                     | Incremented on every change; bound elements re-render only when it moves.                |
| `void Subscribe(Action handler)`  | Run `handler` after the value changed (once per change, after the write completed).      |
| `void Unsubscribe(Action handler)` | Stop a handler.                                                                         |

`IUIComputed` (a value derived from signals and other computeds with automatic dependency tracking; lazy and cached,
recomputed on the next read after a dependency changed; a dependency cycle is logged and the stale value kept):

| Member                            | Description                                                                              |
|-----------------------------------|------------------------------------------------------------------------------------------|
| `string Value`                    | The value as text.                                                                       |
| `double Number`                   | The value as a number.                                                                   |
| `bool Flag`                       | The value as a flag.                                                                     |
| `int Version`                     | Incremented whenever the computed is invalidated by a dependency.                        |
| `void Subscribe(Action handler)`  | Run `handler` after the computed was invalidated (once per batch of changes).            |
| `void Unsubscribe(Action handler)` | Stop a handler.                                                                         |

`IUIForm : IUIContainer` (a form generated by `AddForm`; edits write straight into the model, the form keeps a
snapshot for `Cancel` and an undo / redo history of every committed change; Ctrl+Z / Ctrl+Y while a field is
focused undo / redo):

| Member                                     | Description                                                                                   |
|--------------------------------------------|-----------------------------------------------------------------------------------------------|
| `bool IsDirty`                             | `true` while any property differs from the last `Save` (or the initial snapshot).             |
| `bool CanUndo`                             | Whether there is a change to undo.                                                            |
| `bool CanRedo`                             | Whether there is a change to redo.                                                            |
| `void Undo()`                              | Undo the last committed change.                                                               |
| `void Redo()`                              | Redo the last undone change.                                                                  |
| `void Save()`                              | Accept the current values: clears the dirty state and the history, raises `OnSaved`.          |
| `void Cancel()`                            | Restore the snapshot into the model, refresh the controls, raise `OnCancelled`.               |
| `void Refresh()`                           | Re-read the model into the controls (after changing it from code) and clear validation messages. |
| `Action<IUIForm> OnSaved`                  | Raised by `Save`.                                                                             |
| `Action<IUIForm> OnCancelled`              | Raised by `Cancel`.                                                                           |
| `Action<IUIForm> OnChanged`                | Raised after every committed edit, undo, redo or cancel.                                      |
| `bool ShowButtons`                         | Show the Save / Cancel / Undo / Redo button row (default `true`).                             |
| `IUIElement FieldFor(string propertyName)` | The input element generated for a property, or `null`.                                        |

#### `IUIHud`

A HUD widget (see [HUD widgets and toasts](#hud-widgets-and-toasts)).

| Member                            | Description                                                                          |
|-----------------------------------|--------------------------------------------------------------------------------------|
| `string Id`                       | The id given to `CreateHud`.                                                         |
| `IUIStack Root`                   | Root container (a vertical `IUIStack`).                                              |
| `bool Visible`                    | Consumer switch (default `true`).                                                    |
| `UIAnchor Anchor`                 | Screen corner / edge the widget hangs from (default `TopLeft`).                      |
| `int X`, `int Y`                  | Offset added to the anchor position (positive = right / down); verbatim position for `Explicit`. |
| `int? Width`, `int? Height`       | Fixed size, or `null` to fit content.                                                |
| `bool DrawBox`                    | Draw a vanilla panel box with padding behind the content (default `true`).           |
| `float Opacity`                   | Opacity of the box, 0..1 (default 1); content is always opaque.                      |
| `bool Interactive`                | Receive hover / clicks while no menu is open and let the player drag the widget.     |
| `Func<bool> ShowWhen`             | Evaluated every tick; `false` hides the widget (`null` = always).                    |
| `Action<IUIHud, double> OnUpdate` | Every tick while shown, with elapsed milliseconds.                                   |
| `Rectangle Bounds`                | Absolute bounds of the box, valid after the first draw.                              |
| `IUIElement Find(string id)`      | Find an element by id anywhere in the tree, or `null`.                               |
| `void InvalidateLayout()`         | Request a layout pass before the next draw.                                          |

#### `IStardewUIApi`

Entry point. One instance per consumer mod; every id you register is private to your mod.

| Member                                                                                                                                                | Description                                                                                           |
|-------------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------|
| `string ApiVersion`                                                                                                                                   | Semantic version of this API (`1.1.0`).                                                               |
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

v1.1 additions, in the order they appear in the file (a consumer's copy may include a subset):

| Member                                                                                                                                                | Description                                                                                           |
|-------------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------|
| `IUISlot AddSlot(IUIContainer parent, string id)`                                                                                                     | Declare an extension slot other mods can contribute to; emptied and rebuilt from the contributions every time the menu opens, before layout. |
| `IUISlotInfo[] ListSlots(string ownerModId)`                                                                                                          | Every slot declared by that mod's menus, with their layout hints (empty array if none).               |
| `void ContributeTo(string ownerModId, string menuId, string slotId, Action<IUIContainer, IUIScreenContext> build)`                                    | Contribute elements to a slot with priority 0; `build` runs every time the owning menu opens with a container of its own (`"<slotId>.<yourModId>"`) and the owner's screen context. One contribution per (mod, slot). |
| `void ContributeTo(string ownerModId, string menuId, string slotId, int priority, Action<IUIContainer, IUIScreenContext> build)`                      | Same, ordered by ascending `priority`, then by mod id.                                                |
| `void RemoveContribution(string ownerModId, string menuId, string slotId)`                                                                            | Remove your contribution to a slot (takes effect the next time the menu opens).                      |
| `void Expose(IUIMenu menu, string key, Func<string> value)`                                                                                           | Expose a string value of one of your menus to contributors (`GetString`); calling again replaces it, `null` removes it. |
| `void ExposeNumber(IUIMenu menu, string key, Func<double> value)`                                                                                     | Expose a numeric value (`GetNumber`).                                                                 |
| `void ExposeBool(IUIMenu menu, string key, Func<bool> value)`                                                                                         | Expose a boolean value (`GetBool`).                                                                   |
| `void ExposeCommand(IUIMenu menu, string key, Action command)`                                                                                        | Expose a command (`Invoke`); calling again replaces it, `null` removes it.                            |
| `void Publish(IUIMenu menu, string eventName)`                                                                                                        | Raise an event of one of your menus to every contributor that subscribed to it; each handler is guarded. |
| `void OnScreenBuilt(string ownerModId, string menuId, Action<IUIMenu> decorate)`                                                                      | Inspect and adjust a menu after it is built: runs every time it opens, after all slot contributions and before layout; sealed elements are hidden from it. One decorator per (mod, menu); `null` removes it. |
| `IUICompositeArgs CreateCompositeArgs()`                                                                                                              | Create an empty argument bag for `AddComposite`.                                                      |
| `void DefineComposite(string name, Action<IUICompositeHost, IUICompositeArgs> build)`                                                                 | Register a reusable composite under a global name (convention `"<ModId>.<Name>"`); defining an existing name replaces it. |
| `bool HasComposite(string name)`                                                                                                                      | Whether a composite with that name is defined (by any mod).                                           |
| `string[] ListComposites()`                                                                                                                           | Names of every defined composite.                                                                     |
| `void UndefineComposite(string name)`                                                                                                                 | Remove a composite this mod defined (definitions of other mods are left alone).                       |
| `IUIComposite AddComposite(IUIContainer parent, string id, string compositeName, IUICompositeArgs args)`                                              | Instantiate a composite: a host container is added and the builder fills it; an unknown name leaves it empty (logged) until `Rebuild` after it was defined. |
| `IUIElement AddCustom(IUIContainer parent, string id, IUICustomComponent implementation, Action<IUIContainer> build)`                                 | A custom component that embeds built-in elements: `build` runs once with a container (`"<id>.host"`) the component owns; the implementation draws first, the element measures as the larger of the two. |
| `IUITooltip CreateTooltip()`                                                                                                                          | Create an empty rich tooltip builder; assign it to `IUIElement.RichTooltip`.                          |
| `string[] ListThemes()`                                                                                                                               | Names of the themes defined in the `Mods/6135.UIFramework/Themes` asset (Content Patcher packs can add to it). |
| `string ActiveTheme { get; }`                                                                                                                         | Name of the theme every framework menu currently uses.                                                |
| `void SetTheme(string name)`                                                                                                                          | Switch every framework menu to `name` (one of `ListThemes`) and save it in the framework's config.     |
| `Color ThemeColor(string key)`                                                                                                                        | A color of the active theme: `text`, `disabled-text`, `hover`, `scrollbar`, `border` (unknown keys return the text color). |
| `bool ReducedMotion { get; }`                                                                                                                         | True when the player asked for reduced motion; custom components should skip their own animations then. |
| `void Announce(string text)`                                                                                                                          | Speak `text` through the screen reader (Stardew Access) when one is installed; otherwise nothing happens. |
| `IUIDataGrid AddDataGrid(IUIContainer parent, string id, int rowHeight, int visibleRows, Func<int> rowCount)`                                         | A virtualized table: add columns with `AddColumn`, each reads its cell text for a row index through `Text`; `rowCount` is re-queried every tick and on `Refresh`. |
| `IUISignal Signal(string initial)`                                                                                                                    | Create a signal holding a string (it can also be read / written as a number or a flag).               |
| `IUISignal SignalNumber(double initial)`                                                                                                              | Create a signal holding a number.                                                                     |
| `IUISignal SignalBool(bool initial)`                                                                                                                  | Create a signal holding a flag.                                                                       |
| `IUIComputed Computed(Func<string> compute)`                                                                                                          | Create a lazy, cached value derived from other signals / computeds; every signal read while `compute` runs becomes a dependency. |
| `IUIComputed ComputedNumber(Func<double> compute)`                                                                                                    | Numeric variant of `Computed`.                                                                        |
| `IUIComputed ComputedBool(Func<bool> compute)`                                                                                                        | Boolean variant of `Computed`.                                                                        |
| `void BindText(IUILabel label, IUIComputed source)`                                                                                                   | Show `source` in the label; the label only re-flows when the computed's version changes.              |
| `void BindText(IUILabel label, IUISignal source)`                                                                                                     | Show `source` in the label; the label only re-flows when the signal's version changes.                |
| `void BindVisible(IUIElement element, IUIComputed source)`                                                                                            | Drive `Visible` from the computed's `Flag`.                                                           |
| `void BindEnabled(IUIElement element, IUIComputed source)`                                                                                            | Drive `Enabled` from the computed's `Flag`.                                                           |
| `void BindValue(IUITextInput input, IUISignal signal)`                                                                                                | Two-way: the input shows the signal and writes it when edited (the setter it was created with is still called). |
| `void BindValue(IUINumberInput input, IUISignal signal)`                                                                                              | Two-way: the input shows the signal's `Number` and writes it when edited.                             |
| `void BindValue(IUICheckbox input, IUISignal signal)`                                                                                                 | Two-way: the checkbox shows the signal's `Flag` and writes it when toggled.                           |
| `void BindValue(IUISlider input, IUISignal signal)`                                                                                                   | Two-way: the slider shows the signal's `Number` and writes it when moved.                             |
| `void BindValue(IUIDropdown input, IUISignal signal)`                                                                                                 | Two-way: the dropdown selects the choice equal to the signal's `Value` and writes it when changed.    |
| `void Unbind(IUIElement element)`                                                                                                                     | Drop every binding on `element` (restoring the original value delegates). Bindings are also dropped when the element leaves its menu. |
| `IUIForm AddForm(IUIContainer parent, string id, object model)`                                                                                       | Generate a two-column form from the public read / write properties of `model` (see [Auto-forms](#auto-forms)). |
| `IUIHud CreateHud(string id)`                                                                                                                         | Create (or replace) a HUD widget: a non-modal overlay drawn during gameplay. Build its tree under `IUIHud.Root`. |
| `IUIHud GetHud(string id)`                                                                                                                            | Look up one of your HUD widgets, or `null`.                                                           |
| `void DestroyHud(string id)`                                                                                                                          | Remove a HUD widget.                                                                                  |
| `void ShowToast(string text)`                                                                                                                         | Show a short notification in the bottom-left corner for 3.5 seconds.                                  |
| `void ShowToast(string text, int durationMs)`                                                                                                         | Show a short notification in the bottom-left corner for `durationMs` milliseconds (non-positive = default). |
| `void ShowToastWithIcon(string text, Texture2D icon, Rectangle? source, int durationMs)`                                                              | Show a notification with an icon (`source` `null` = whole texture).                                   |
| `void ResetPlayerLayout(IUIMenu menu)`                                                                                                                | Forget the player's saved position / size / collapsed state for `menu` and restore the values you set. |

Argument checks: ids must be non-empty; `parent` must be a container created by the framework and must belong to
one of *your* menus (adding to another mod's menu throws `InvalidOperationException`, unless you are a slot
contributor adding to the container you were handed, a registered decorator adding outside a sealed subtree, or the
defining mod of a composite adding to its host); `itemCount`, `buildRow`, `choices`, `onPressed`, `rowCount`,
`model`, `compute`, the `build` callbacks of `ContributeTo` / `DefineComposite` / `AddCustom`, the bound elements and
sources of `Bind*` and custom `implementation` must not be `null` (`OnScreenBuilt` accepts `null` to remove a
decorator).

### Proxy rules

SMAPI maps your copy of the interfaces onto the framework's objects at runtime, which constrains what can cross the
API:

- Only **interfaces**, **enums**, **delegates**, primitives, `string` and types both sides already share
  (`Microsoft.Xna.Framework.*` such as `Vector2`, `Rectangle`, `Color`, `SpriteBatch`, `Texture2D`, `Keys`; SMAPI and
  game types) appear in the API. No framework class is ever visible to a consumer.
- Delegates (`Action`, `Action<T>`, `Func<T>`) are fine when every `T` is proxyable; event data is passed as
  interfaces (`IUIClickEvent`, `IUIValueEvent`, `IUIKeyEvent`).
- Interfaces you implement (`IUICustomComponent`) are proxied back into the framework.
- Cross-mod data (`IUIScreenContext`, `IUICompositeArgs`) is limited to strings, numbers, bools and delegates over
  them for the same reason; `IUICompositeArgs.SetObject` is the one escape hatch and crosses unproxied.
- Enums are matched by name; copy them verbatim.
- Objects you receive from the API (menus, elements, styles) must be handed back unchanged; the framework rejects
  objects it did not create ("was not created by this framework").
- The API has no optional parameters and no generics, so partial copies of the interface still line up.

### Versioning

`IStardewUIApi.ApiVersion` returns a semantic version string (currently `1.1.0`). Members are only ever **added**,
never renamed or removed: additive changes bump the minor version, and a breaking change would ship as a new
`IStardewUIApi2` interface alongside the old one. A consumer's copy of the interface may be a subset of the
framework's, so you can keep an older copy of `IStardewUIApi.cs` and only update it when you need new members. Set
`MinimumVersion` in your manifest to the framework version that introduced the members you use: everything under
"v1.1 additions" in the API file (the `// BEGIN <FEATURE> members` / `types` regions and the `// SLOTS`,
`// RICHTEXT`, `// THEME`, `// HUD` tagged members at the end of `IUIElement`, `IUILabel`, `IUIButton`,
`IUIMenuOptions` and `IUIMenu`) needs `1.1.0`; a 1.0 copy of the file still works against 1.1.

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

3. Optionally run the headless test suite (see [Headless testing](#headless-testing)):

   ```sh
   dotnet test StardewUIFramework.Tests -p:EnableModDeploy=false -p:EnableModZip=false
   ```

[Pathoschild.Stardew.ModBuildConfig](https://github.com/Pathoschild/SMAPI/blob/develop/docs/technical/mod-package.md)
handles the rest: it references the game assemblies, copies the built mod into `<game>/Mods/UIFramework` after every
build so you can test immediately, and drops a release zip (`UIFramework <version>.zip`) into the project's build
output folder. The same happens for the example mod. `UIFramework.csproj` also generates the XML documentation file
that Doxygen and IDE tooltips use.

`UIFrameworkExample` deliberately has **no** `ProjectReference` to the framework: it compiles against its copy of
`Api/IStardewUIApi.cs` only, which is exactly the situation a third-party consumer is in and keeps the proxy path
honest. When the API file changes, copy it to `UIFrameworkExample/Api/IStardewUIApi.cs` again.

## Layout of this folder

| Path                              | Contents                                                                                                                                                                                                                                                                          |
|-----------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Api/Public/IStardewUIApi.cs`     | The public API (copy this).                                                                                                                                                                                                                                                       |
| `Api/`                            | Per-consumer facade (`StardewUIApi`) and the menu options bag.                                                                                                                                                                                                                    |
| `Core/`                           | Element tree, layout engine, focus manager, overlay layer, event router; v1.1: sealing rules, signals and bindings, auto-form generation and reflection, rich tooltip model, HUD model, accessibility announcements, tree dump / exporter, perf counters.                          |
| `Components/`                     | Built-in elements (Label, Button, TextInput, NumberInput, Checkbox, Dropdown, Slider, Image, Spacer, Stack, Grid, Panel, Canvas, ScrollView, ListView, custom adapter); v1.1: Slot, Composite, DataGrid (+ columns / rows), the custom-host adapter for `AddCustom(..., build)`.  |
| `Hosting/`                        | `MenuHost : IClickableMenu`, hotkey service, menu registry; v1.1: extension registry (slots, exposures, decorators), composite registry, HUD service, toast layer, player layout controller and window layout store, inspector, debug console.                                     |
| `Rendering/`                      | Drawing helpers, the theme (asset data, resolution, switcher), rich text parser / layout, tooltip renderer, pseudo-localizer, inspector renderer.                                                                                                                                  |
| `Integrations/`                   | Generic Mod Config Menu and Stardew Access API copies.                                                                                                                                                                                                                            |
| `assets/`                         | Bundled texture (`text_box_small.png`) and the default themes (`themes.json`).                                                                                                                                                                                                    |
| `i18n/`                           | Translations: config labels, form buttons, screen reader phrases.                                                                                                                                                                                                                 |
| `architecture.md`                 | Design document, implementation plan and the v1.1 roadmap (§16).                                                                                                                                                                                                                  |
| `code-review.md`                  | Review notes and polish backlog.                                                                                                                                                                                                                                                  |
| `Doxyfile`                        | Doxygen configuration for the API reference.                                                                                                                                                                                                                                      |
| `../StardewUIFramework.Tests/`    | xUnit test project: `Testing/` is the headless harness (`TestHost`, `InputDriver`, `TreeSnapshot`, `FakeTextMeasurer`, `GameAssemblies`), `Tests/` covers layout, routing, inputs, scrolling / lists and the developer tools.                                                     |
| `../UIFrameworkExample/`          | The example consumer mod (`ModEntry.cs`, `DemoSettings.cs`, `FrameBox.cs`, `VolumeGauge.cs`) with its own copy of the API file.                                                                                                                                                   |
