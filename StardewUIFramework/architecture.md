# UI Framework Mod – Architecture

This document describes what has to be built to ship a **UI Framework** SMAPI mod (`6135.UIFramework`) that other mods use to build in‑game screens (menus) out of reusable parts — labels, buttons, text/number inputs, checkboxes, dropdowns, lists, scroll areas, tooltips — and to run their own code when the player interacts with those parts.

The reference implementation for "what a consumer needs" is the **Profit Calculator** mod in this repository (`ProfitCalculator/main/ui/**`). Its two screens (`ProfitCalculatorMainMenu`, `ProfitCalculatorResultsList`) are the acceptance test: when the framework is done, both screens must be re‑creatable through the public API with no `IClickableMenu` subclassing on the consumer side.

Background reading:

- Framework mods (data/delegate style): https://stardewmodding.wiki.gg/wiki/Tutorial:_C_Sharp_-_Making_Framework_Mods
- Custom C# mods index: https://stardewmodding.wiki.gg/wiki/Making_Mods:_Custom_C_Sharp
- SMAPI mod‑provided APIs (`Mod.GetApi`, Pintail proxying): https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Integrations#Mod-provided_APIs

---

## 1. Goals and non‑goals

### Goals

1. **Screens from parts.** A consumer describes a screen as a tree of components and the framework hosts it as a real `IClickableMenu` (`Game1.activeClickableMenu` or a child menu).
2. **Every interaction is hookable.** Click, hover, value change, focus, key press, scroll, open/close — each raises a callback the consumer supplies as a plain delegate.
3. **Custom actions.** Beyond built‑in behaviors, consumers can attach arbitrary `Action`/`Func` delegates and can supply their own component implementations (custom draw + custom input handling) through an interface.
4. **Works across the SMAPI API boundary.** Everything a consumer touches must be legal for SMAPI's interface proxy (Pintail): interfaces, primitives, enums, delegates, XNA/game types. No framework classes leak into the API.
5. **Looks native.** Uses the vanilla textures (`Game1.mouseCursors`, `Game1.menuTexture`, `LooseSprites\textBox`), fonts and sounds by default, with per‑component overrides.
6. **Layout that survives resizes.** Positions are computed by layout containers, not hard‑coded, so window resize / UI scale changes re‑flow the screen.
7. **Controller + keyboard friendly.** Tab/arrow focus traversal, gamepad snapping via `ClickableComponent.myID`/neighbor ids, Enter/Escape defaults.

### Non‑goals (v1)

- Declarative UI from JSON/StarML (that is StardewUI's niche). v1 is code‑first through the API.
- Data binding framework. Values are read/written through getter/setter delegates, like Profit Calculator does today.
- Replacing vanilla menus (inventory, shops). Only new screens.
- Draggable/resizable windows (nice‑to‑have, later).

---

## 2. How a SMAPI framework mod exposes an API (constraints that shape the design)

SMAPI lets a mod expose an API object by overriding `Mod.GetApi()` (or `GetApi(IModInfo)` for per‑consumer instances). Consumers copy the **interface** into their own project and call `helper.ModRegistry.GetApi<IStardewUIApi>("6135.UIFramework")`. SMAPI generates a runtime proxy (Pintail) that maps the consumer's copy of the interface onto the framework's real object.

Rules that follow from that mechanism:

| Rule | Consequence for this design |
|---|---|
| Only **interfaces** are proxied. Framework **classes/structs** passed through the API are not visible to the consumer (they only see `object`). | All public API types are interfaces (`IUIComponent`, `IUIMenu`, `IUIEventArgs` …) or types both sides already share: primitives, `string`, enums, `Microsoft.Xna.Framework.*` (`Vector2`, `Rectangle`, `Color`, `SpriteBatch`, `Texture2D`), `StardewModdingAPI.SButton`, `StardewValley.*`. |
| Delegates (`Action`, `Action<T>`, `Func<T>`) are supported when `T` is a proxyable type. | Custom actions are plain delegates. Event‑args parameters are interfaces. |
| Enums are matched by name/value; consumers copy them too. | Keep enums small and stable (`UIAnchor`, `UIFont`, `UIAlign`, `UIEventKind`). |
| Consumer‑implemented interfaces are proxied **back** into the framework. | This is the custom‑component mechanism: a consumer implements `IUICustomComponent` (draw/update/input methods) and hands it to the framework. |
| The consumer's copy of the interface may be a **subset**; extra members on the framework side are fine. | Version the API additively. Never rename/remove members; add `V2` interfaces if a break is needed. |
| Optional parameters work only if the consumer copied them; defaults are compile‑time. | Prefer explicit overloads or option‑interface objects over long optional lists. |
| `GetApi(IModInfo mod)` gives one instance per consumer. | Use it: every id the consumer registers is namespaced by the consumer's `UniqueID`, and all of that consumer's menus/hotkeys can be torn down together. |

Consumers declare the dependency in `manifest.json`:

```json
"Dependencies": [ { "UniqueID": "6135.UIFramework", "MinimumVersion": "1.0.0", "IsRequired": true } ]
```

The framework ships a ready‑to‑copy `IStardewUIApi.cs` (plus the small set of interfaces/enums it references) in an `Api/Public/` folder, and an **example mod** that consumes it.

---

## 3. What the existing code teaches

### 3.1 Profit Calculator (patterns to keep)

- `BaseOption : ClickableComponent` — option = bounds + `Draw`, `Update`, `ReceiveLeftClick(x, y, stopSpread)`, `BeforeReceiveLeftClick`, `PerformHoverAction`, sounds on click/hover. Good minimal component contract.
- Values are accessed through **`Func<T>` getters / `Action<T>` setters** (`() => Day`, `v => Day = uint.Parse(v)`), and labels/names are `Func<string>` so they re‑translate at draw time. Keep this: it maps 1:1 onto proxy‑safe delegates.
- `TextOption : IKeyboardSubscriber` — takes keyboard focus via `Game1.keyboardDispatcher.Subscriber`, handles `RecieveTextInput/RecieveCommandInput/RecieveSpecialInput`, draws blinking caret. `UIntOption` builds on it with clamping and Up/Down increment.
- `DropdownOption` — the hard one: needs to draw **above** siblings (menu switches `SpriteSortMode` to `FrontToBack` with explicit layer depths), needs to be the only open dropdown (global `ActiveDropdown` in the DI container), and needs to **swallow the click** that closes it (`stopSpread`).
- `ProfitCalculatorResultsList` — scrollable list with up/down arrows, scrollbar drag (`leftClickHeld`/`releaseLeftClick`), scroll wheel, fixed slots and a hover box (`CropHoverBox`) that acts as a rich tooltip.
- Menus recompute positions from `GetAppropriateMenuPosition()` on `gameWindowSizeChanged` and rebuild all options (`UpdateMenu()`); the child menu is opened with `SetChildMenu`.
- Opening is done from `Input.ButtonPressed` with a configurable hotkey exposed through Generic Mod Config Menu.

### 3.2 First‑attempt framework (deleted in commit `766032a`) — what to avoid

- **Flat, string‑id everything API** (`CreateButton(menuId, id, text, x, y, …)` + `RegisterClickHandler(componentId, …)`) with `Dictionary<string, object> GetMenus()` / `ReplaceMenu(string, object)` escape hatches. The `object` escapes cannot work across Pintail and the string plumbing makes hierarchies (a button inside a grid inside a scroll panel) awkward.
- Components stored absolute screen positions and were re‑scaled on resize with ratios; layouts were separate objects that *wrote* positions into components. Layout must instead be **owned by container components** and re‑run on demand.
- Tooltip timing, hover state and the keyboard subscriber were handled per component instead of by the host, so they fought each other.
- The old README promised `SubMenu`, `ScrollableMenu`, `DialogMenu`, layouts, styling; roughly half was stubbed. This document scopes v1 so that everything listed is actually finished.
- Note: `Stardew Mods.sln` still references `UIFramework\UIFramework.csproj` and `UIFrameworkExample\UIFrameworkExample.csproj`; those entries must be repointed/recreated when the new projects are added.

---

## 4. High‑level architecture

```
┌───────────────────────────────────────────────────────────────────────┐
│ Consumer mod (e.g. ProfitCalculator)                                  │
│   copies Api/Public/*.cs  →  helper.ModRegistry.GetApi<IStardewUIApi> │
└───────────────┬───────────────────────────────────────────────────────┘
                │ Pintail proxy (interfaces, delegates, enums, XNA types)
┌───────────────▼───────────────────────────────────────────────────────┐
│ UIFramework mod                                                       │
│                                                                       │
│  Api/          StardewUIApi (per-consumer instance)  ── facade         │
│  Core/         Component tree, layout, focus, event dispatch          │
│  Components/   Label, Button, TextInput, NumberInput, Checkbox,       │
│                Dropdown, Image, Panel, Stack, Grid, Canvas,           │
│                ScrollView, List, Slider, Spacer, Custom adapter       │
│  Hosting/      MenuHost : IClickableMenu (bridges game callbacks)     │
│  Input/        Mouse/keyboard/gamepad translation, focus manager      │
│  Rendering/    SpriteBatch helpers, 9-slice boxes, text measuring,    │
│                overlay (popup) layer                                  │
│  Themes/       Default vanilla theme + per-component style overrides  │
│  Registry/     Per-consumer registries: menus, hotkeys, ids           │
│  Config/       ModConfig (global tooltip delay, debug overlay, …)     │
└───────────────────────────────────────────────────────────────────────┘
```

### 4.1 Core model

**Component tree.** A screen is a tree. Every node derives from the internal `UIElement` base class (framework‑side) and is exposed to consumers only through interfaces:

```
IUIElement              Id, Parent, Bounds, Visible, Enabled, Tooltip, Tag, Style, Margin, Width/Height, Align
 ├─ IUIContainer        Children, Add/Remove/Clear, InvalidateLayout
 │    ├─ IUIPanel       background box (9-slice), padding, single child
 │    ├─ IUIStack       vertical/horizontal flow, spacing, alignment
 │    ├─ IUIGrid        rows/cols (fixed px, auto, star), spans
 │    ├─ IUICanvas      explicit X/Y per child (pixel-exact escape hatch)
 │    ├─ IUIScrollView  clips & scrolls a child, scrollbar, wheel
 │    └─ IUIList        virtualized rows from a data source (+ selection)
 ├─ IUILabel            text, font, color, wrap, align
 ├─ IUIImage            texture, source rect, scale, tint
 ├─ IUIButton           label/icon, click
 ├─ IUICheckbox         bool value
 ├─ IUITextInput        string value, placeholder, max length, validator
 ├─ IUINumberInput      numeric value, min/max/step, clamp
 ├─ IUIDropdown         choices + labels, selected index/value
 ├─ IUISlider           numeric range
 ├─ IUISpacer / IUIDivider
 └─ IUIElement (custom) consumer-implemented via IUICustomComponent (see §7)
```

**Menu.** `IUIMenu` wraps a root container plus window chrome (dialogue box, title, close button, modal dimming), position policy (centered / anchored / explicit), size policy (fixed / fit‑content), lifecycle callbacks and a toggle hotkey.

**Host.** `MenuHost : IClickableMenu` is the only game‑facing class. One host instance per open menu. It forwards `draw`, `update`, `receiveLeftClick`, `receiveRightClick`, `leftClickHeld`, `releaseLeftClick`, `performHoverAction`, `receiveScrollWheelAction`, `receiveKeyPress`, `receiveGamePadButton`, `gameWindowSizeChanged`, `emergencyShutDown` and `cleanupBeforeExit` into the tree.

### 4.2 Per‑frame pipeline

```
update(GameTime)
  ├─ Layout pass if dirty (measure → arrange, top-down)
  ├─ Hover pass (hit-test deepest visible+enabled element under cursor; raise Enter/Leave)
  ├─ Tooltip timer
  └─ element.Update() (caret blink, animations, dropdown open state)

draw(SpriteBatch)
  ├─ optional dim rectangle (modal)
  ├─ window chrome (Game1.drawDialogueBox / drawTextureBox)
  ├─ tree draw in Deferred sort mode, depth-first, with scissor rects for ScrollView
  ├─ OVERLAY pass: elements that requested overlay (open dropdown list, hover boxes, tooltips)
  └─ mouse cursor (unless hardware cursor)
```

The overlay pass removes the need for the `SpriteSortMode.FrontToBack` juggling Profit Calculator does today: a dropdown draws its closed box in the normal pass and registers its open list with the `OverlayLayer` for the second pass. Overlay elements also get **first pick at input** (see §6), which is how "clicking outside closes the dropdown and swallows the click" is implemented once, centrally.

---

## 5. Layout system

Two‑phase, WPF‑style but minimal:

1. **Measure**: each element returns its desired size given an available size. Leaves measure their content (text via `SpriteFont.MeasureString`, textures via source rect × scale). Containers measure children.
2. **Arrange**: parent assigns each child a final `Rectangle` (relative to the parent), applying `Margin`, `HorizontalAlign`/`VerticalAlign` and `Anchor`.

Absolute screen `Bounds` are derived (parent absolute origin + local rect) and cached; a `LayoutDirty` flag bubbles up when any property affecting size changes, when the tree changes, or when the window resizes.

Containers to implement in v1:

- **Stack** (vertical/horizontal, `Spacing`, `Alignment`) — covers Profit Calculator's option rows.
- **Grid** (`ColumnDefinitions`/`RowDefinitions` as `Auto`, `Px(n)`, `Star(w)`; `Row/Column/RowSpan/ColumnSpan` attached values) — covers "label : control" forms.
- **Panel** (single child + padding + optional 9‑slice background).
- **Canvas** (explicit `X/Y` per child) — escape hatch for pixel‑exact placement, like today's code.
- **ScrollView** (child larger than viewport; vertical scrollbar with arrows and draggable thumb, wheel support; uses `GraphicsDevice.ScissorRectangle` + `RasterizerState { ScissorTestEnable = true }` around the child's draw).
- **List** (virtualized: renders only rows that fit, given `ItemCount`, `RowHeight`, `BuildRow(index, container)`); covers `ProfitCalculatorResultsList`.

Units: work in **UI pixels** (the coordinate space `IClickableMenu` already uses, i.e. after `uiScale`). The host centers the menu with the same formula Profit Calculator uses (`Game1.uiViewport` based) and clamps to the screen.

---

## 6. Input, focus and event model

### 6.1 Routing

Every game input callback becomes a **UI event** routed through the tree:

1. **Overlay elements first** (open dropdown, popup). If the overlay handles or *swallows* the event, routing stops. Clicking outside an open dropdown closes it *and* swallows the click.
2. **Hit‑test**: deepest visible + enabled element whose absolute bounds contain the point (ScrollView also requires the point to be inside its viewport).
3. **Bubble**: the event is delivered to the target; if `Handled` is false it bubbles to the parent, up to the menu. This lets a container (e.g. a list row) react to clicks on any of its children.
4. **Focus**: clicking a focusable element (`TextInput`, `NumberInput`, `Dropdown`, `Button`) gives it focus; the **FocusManager** is the single owner of `Game1.keyboardDispatcher.Subscriber` (it installs one `IKeyboardSubscriber` adapter that forwards to the focused element). Clicking empty space or pressing Escape clears focus. Tab / Shift+Tab / arrow keys / gamepad d‑pad move focus in layout order.
5. **Keyboard**: `receiveKeyPress` goes to the focused element first; unhandled keys fall through to menu‑level bindings (Enter → `DefaultButton`, Escape → close or `CancelButton`).
6. **Mouse capture**: `leftClickHeld`/`releaseLeftClick` go to the element that received the original click (scrollbar thumb drag, slider drag).

### 6.2 Events exposed to consumers ("custom actions")

Each component exposes typed callbacks the consumer sets via the API. All are proxy‑safe delegates taking either primitives or event‑args **interfaces**:

| Event | Signature | Raised on |
|---|---|---|
| `OnClick` | `Action<IUIClickEvent>` (element id, x, y, button, `Handled` setter) | Button, Image, Panel, Label, Custom |
| `OnRightClick` | `Action<IUIClickEvent>` | any |
| `OnHover` / `OnHoverEnd` | `Action<IUIElement>` | any |
| `OnValueChanged` | `Action<IUIValueEvent>` (element id, old, new as string + typed accessors) | Checkbox, TextInput, NumberInput, Dropdown, Slider, List selection |
| `OnFocus` / `OnBlur` | `Action<IUIElement>` | focusable |
| `OnKey` | `Func<IUIKeyEvent, bool>` (return true = handled) | focused element / menu |
| `OnScroll` | `Action<int>` (direction) | ScrollView, Dropdown, menu |
| `OnOpen` / `OnClose` | `Action<IUIMenu>` | menu |
| `OnUpdate` | `Action<IUIMenu, double>` (elapsed ms) | menu, every tick |
| `OnDrawExtra` | `Action<SpriteBatch, Rectangle>` | any element, after its own draw (custom overlays without a full custom component) |

Design rules:

- Callbacks are invoked inside `try/catch`; exceptions are logged with the consumer's mod id and never crash the game loop.
- A `Validate` delegate (`Func<string, bool>` / `Func<double, bool>`) on inputs runs *before* the setter so consumers can reject input (digits only, ranges) — this replaces `UIntOption.ReceiveInput`'s hand‑rolled filtering.
- **Value binding**: every value component takes `Func<T> get` + `Action<T> set` (Profit Calculator style) *or* holds its own value; when bound, the getter is called each frame so external changes show immediately.
- Sounds: components have `ClickSound`/`HoverSound` (string cue names) defaulting to vanilla (`"drumkit6"`, `"shwip"`, `"select"`, `"tinyWhip"`, `"cowboy_monsterhit"` …), overridable/nullable.

### 6.3 Hotkeys and opening menus

- `RegisterHotkey(id, keybindList, Action onPressed)` and `BindToggleHotkey(menu, keybindList)` — the framework listens to `Input.ButtonPressed` once and dispatches. Modifier combos are supported via SMAPI's `KeybindList` (string form, e.g. `"LeftControl + F8"`), so consumers can feed values straight from their GMCM config.
- `menu.Open()` sets `Game1.activeClickableMenu` (only when `Context.IsPlayerFree` or `force: true`); `menu.OpenAsChild(parent)` uses `IClickableMenu.SetChildMenu`; `menu.Close()` calls `exitThisMenu` and fires `OnClose`.

---

## 7. Extensibility: custom components and custom menus

Three tiers so most consumers never subclass anything:

1. **Delegate tier** (no new types): `OnDrawExtra`, `OnUpdate`, `OnKey`, validators, styles. Enough for a hover box or a custom badge on a button.
2. **Custom component tier**: the consumer implements the copied interface

   ```csharp
   public interface IUICustomComponent
   {
       Vector2 Measure(Vector2 available);            // desired size
       void Draw(SpriteBatch b, Rectangle bounds);     // absolute bounds already resolved
       void Update(Rectangle bounds, double elapsedMs);
       bool OnClick(int x, int y, bool rightButton);   // true = handled
       void OnHover(int x, int y, bool entered);
       bool OnKey(int key /* Keys */, bool shift, bool ctrl);
       bool WantsFocus { get; }
       bool WantsOverlay { get; }                      // draw in overlay pass (popups)
   }
   ```

   and registers it with `api.AddCustom(parent, id, impl)`. The framework wraps it in an internal `CustomElementAdapter : UIElement` so it participates in layout, focus, tooltips and event bubbling like any built‑in. `CropBox`/`CropHoverBox` from Profit Calculator port cleanly onto this.

3. **In‑assembly tier**: mods that reference `UIFramework.dll` directly (same solution) may subclass `UIElement`/`MenuHost`. Supported but not the primary path; documented as "no compatibility promise".

---

## 8. Public API surface (`Api/Public/IStardewUIApi.cs`)

Builder/handle style. Every `Add*` returns the created element's interface so the consumer can keep a typed reference and never has to look things up by string, while `Find(menu, id)` remains for convenience.

```csharp
public interface IStardewUIApi
{
    string ApiVersion { get; }

    // ---- Menus ----
    IUIMenu CreateMenu(string id, IUIMenuOptions options);   // title, width/height or FitContent, ShowCloseButton, Modal, DimBackground, Position/Anchor, PauseGame
    IUIMenuOptions CreateMenuOptions();
    IUIMenu GetMenu(string id);
    void DestroyMenu(string id);
    void OpenMenu(string id);  void CloseMenu(string id);  bool IsOpen(string id);

    // ---- Containers ----
    IUIStack      AddStack(IUIContainer parent, string id, bool horizontal, int spacing);
    IUIGrid       AddGrid(IUIContainer parent, string id, string columns, string rows);   // "auto,*,120px"
    IUIPanel      AddPanel(IUIContainer parent, string id, bool drawBox, int padding);
    IUICanvas     AddCanvas(IUIContainer parent, string id);
    IUIScrollView AddScrollView(IUIContainer parent, string id, int viewportHeight);
    IUIList       AddList(IUIContainer parent, string id, int rowHeight, int visibleRows,
                          Func<int> itemCount, Action<int, IUIContainer> buildRow);

    // ---- Leaves ----
    IUILabel       AddLabel(IUIContainer parent, string id, Func<string> text);
    IUIImage       AddImage(IUIContainer parent, string id, Texture2D texture, Rectangle? source, float scale);
    IUIButton      AddButton(IUIContainer parent, string id, Func<string> text, Action<IUIClickEvent> onClick);
    IUICheckbox    AddCheckbox(IUIContainer parent, string id, Func<bool> get, Action<bool> set);
    IUITextInput   AddTextInput(IUIContainer parent, string id, Func<string> get, Action<string> set);
    IUINumberInput AddNumberInput(IUIContainer parent, string id, Func<double> get, Action<double> set,
                                  double min, double max, double step, bool clamp);
    IUIDropdown    AddDropdown(IUIContainer parent, string id, Func<string[]> choices, Func<string[]> labels,
                               Func<string> get, Action<string> set);
    IUISlider      AddSlider(IUIContainer parent, string id, Func<double> get, Action<double> set, double min, double max);
    IUISpacer      AddSpacer(IUIContainer parent, string id, int width, int height);
    IUIElement     AddCustom(IUIContainer parent, string id, IUICustomComponent implementation);

    // ---- Lookup / tree ----
    IUIElement Find(IUIMenu menu, string id);
    void Remove(IUIElement element);
    void InvalidateLayout(IUIMenu menu);

    // ---- Input ----
    void RegisterHotkey(string id, string keybindList, Action onPressed);
    void UnregisterHotkey(string id);
    void BindToggleHotkey(IUIMenu menu, string keybindList);

    // ---- Style / config ----
    void SetTooltipDelay(int milliseconds);            // per consumer
    IUIStyle CreateStyle();                            // font, colors, box texture/source, padding
    void SetDefaultStyle(IUIStyle style);              // per consumer
}
```

Element interfaces expose settable properties (`Visible`, `Enabled`, `Tooltip` as `Func<string>`, `Margin`, `Width`/`Height` (nullable = auto), `HorizontalAlign`, `VerticalAlign`, `Style`, `Tag`) and their event delegates as properties (`OnClick { get; set; }`). Everything the consumer copies lives in **one file** (`IStardewUIApi.cs`, roughly 300 lines) so adoption is copy‑paste. No optional parameters in the interface (see §2), so the example above uses explicit arguments.

Versioning: `ApiVersion` returns a semver string; additive changes bump minor; any breaking change goes in `IStardewUIApi2`.

---

## 9. Rendering details

- **Boxes**: `IClickableMenu.drawTextureBox` with `Game1.menuTexture (0,256,60,60)` for panels, `Game1.mouseCursors (432,439,9,9)` for buttons (hover tint `Color.Wheat`), text box texture `LooseSprites\textBox` (and a bundled small variant, as Profit Calculator ships `assets/text_box_small.png`).
- **Text**: `Game1.smallFont` default, `Game1.dialogueFont` for titles; `Game1.textColor`. Label wrapping via `Game1.parseText`. Optional shadow via `Utility.drawTextWithShadow`.
- **Checkbox**: `OptionsCheckbox.sourceRectChecked/Unchecked` at scale 4.
- **Dropdown**: `OptionsDropDown.dropDownBGSource` / `dropDownButtonSource`; open list drawn in overlay pass, clamped to `Game1.uiViewport.Height`, scrollable when choices exceed `MaxVisible`.
- **Scrollbar**: arrows `(421,459,11,12)`/`(421,472,11,12)`, track `(403,383,6,6)`, thumb `(435,463,6,10)` — same as `ProfitCalculatorResultsList`.
- **Tooltips**: `IClickableMenu.drawHoverText` after the delay elapses; rich tooltips via a `TooltipBuilder` (title, lines, icon) or a custom component in overlay.
- **Clipping**: ScrollView ends the current batch, begins one with `ScissorTestEnable`, draws child, restores. Must save/restore the outer batch's parameters (use Deferred everywhere and never rely on FrontToBack).
- **Cursor**: host draws the software cursor last unless `Game1.options.hardwareCursor`.
- **Layer depths**: not used for ordering (draw order is tree order + overlay pass); components pass `layerDepth` only where a vanilla helper requires it.

---

## 10. Hosting, lifecycle and safety

- `MenuHost` constructor takes the `UIMenu` model; `Open()` builds the host, runs layout, sets `Game1.activeClickableMenu`. Re‑opening reuses the model but creates a fresh host, so state that should persist (input values) lives in the consumer's getters/setters, not in the host.
- `gameWindowSizeChanged` → recompute menu position + `InvalidateLayout`; child menus are forwarded (`_childMenu?.gameWindowSizeChanged`).
- `cleanupBeforeExit` → FocusManager releases the keyboard subscriber, overlays are cleared, `OnClose` fires.
- `emergencyShutDown` → same as close, with callbacks guarded.
- `ReturnedToTitle` / `SaveLoaded` events: close all menus of all consumers.
- All consumer callbacks are wrapped; a faulting callback is logged once per (consumer, element, event) with the stack, then muted for that element to avoid log spam.
- Multiplayer/split‑screen: per‑screen state via `PerScreen<T>` for "active menu" and "hotkey listener" tables.

---

## 11. Configuration and integrations

- **ModConfig** (framework's own `config.json`): `TooltipDelayMs` (default 400), `DebugOverlay` (draw element bounds/ids), `LogCallbacks`.
- **Generic Mod Config Menu**: register the framework's own settings; consumers keep using GMCM for *their* options (hotkey etc.) and feed values into the framework (`BindToggleHotkey(menu, config.HotKey)`).
- **i18n**: the framework only has a handful of strings (close, scroll hints); consumers pass `Func<string>` so their `helper.Translation.Get` is evaluated lazily.
- **Texture overrides**: box/button textures are loaded through `helper.GameContent` so Content Patcher packs can retexture them.

---

## 12. Project layout and repository changes

```
UIFramework/
  UIFramework.csproj              net6.0, Pathoschild.Stardew.ModBuildConfig 4.3.2, GenerateDocumentationFile
  manifest.json                   UniqueID 6135.UIFramework, MinimumApiVersion 4.0.0 (SDV 1.6)
  ModEntry.cs                     GetApi(IModInfo) → new StardewUIApi(modInfo, services)
  ModConfig.cs
  Api/
    StardewUIApi.cs               facade; validates args, namespaces ids, wraps callbacks
    Public/IStardewUIApi.cs       ← the single file consumers copy (interfaces + enums)
  Core/
    UIElement.cs  UIContainer.cs  UIMenu.cs  LayoutEngine.cs  FocusManager.cs  OverlayLayer.cs  EventRouter.cs
  Components/
    Label.cs Image.cs Button.cs Checkbox.cs TextInput.cs NumberInput.cs Dropdown.cs Slider.cs Spacer.cs
    Stack.cs Grid.cs Panel.cs Canvas.cs ScrollView.cs ListView.cs CustomElementAdapter.cs
  Hosting/
    MenuHost.cs  HotkeyService.cs  MenuRegistry.cs
  Rendering/
    Draw.cs (9-slice, text, scissor helpers)  Theme.cs  DefaultTheme.cs
  assets/
    text_box_small.png
  i18n/default.json
UIFrameworkExample/
  UIFrameworkExample.csproj, manifest.json (depends on 6135.UIFramework)
  Api/IStardewUIApi.cs            verbatim copy
  ModEntry.cs                     builds a form + a scrollable list; F9 toggles it
UIFramework.Tests/  (optional)    xUnit; layout engine + event routing are pure C# and testable without the game
```

Repository tasks:

- Update `Stardew Mods.sln`: replace the two stale project entries with the new projects (and the test project if added).
- Root `README.md`: add the framework to the mod list; link `architecture.md` and the API file.
- CI (CodeFactor is already wired): ensure the new projects build with `STARDEW_GAME_DIR` documented in the README.

---

## 13. Implementation plan

Each phase ends in a buildable, demoable state.

### Phase 0 — Skeleton
- Create both projects, manifests, sln entries, `ModEntry.GetApi(IModInfo)`.
- Empty `IStardewUIApi` with `ApiVersion`; example mod logs the version. Verifies the proxy path end to end.

### Phase 1 — Core tree + host + layout
- `UIElement`, `UIContainer`, `UIMenu`, `MenuHost` with draw/update/resize.
- `LayoutEngine` measure/arrange; `Stack`, `Panel`, `Canvas`, `Spacer`, `Label`, `Image`.
- Window chrome (dialogue box, title, close button, dim). Centering/clamping.
- Example: a titled menu with two labels opens on F9 and survives window resize.

### Phase 2 — Input, focus, events
- `EventRouter` (hit‑test, bubble, capture), `FocusManager` + single keyboard subscriber adapter, hotkey service with `KeybindList`.
- `Button` (click/hover/sounds), `Checkbox`, event‑args interfaces, callback try/catch + logging.
- Tab/arrow focus traversal; Enter/Escape defaults; gamepad `myID` neighbor wiring generated from layout.

### Phase 3 — Text and numbers
- `TextInput` (caret, placeholder, max length, validator, paste), `NumberInput` (clamp, step, up/down, wheel), `Slider`.
- Port Profit Calculator's `TextOption`/`UIntOption` behaviors as the spec; per‑character sounds kept.

### Phase 4 — Overlay, dropdown, tooltips
- `OverlayLayer`; `Dropdown` with open list, scroll, click‑outside close + swallow; only one open at a time.
- Tooltip delay + `drawHoverText`; rich tooltip builder.

### Phase 5 — Grid, ScrollView, List
- `Grid` with `auto/px/*` tracks and spans; `ScrollView` with scissor clipping, arrows, thumb drag, wheel; virtualized `ListView` with selection + `OnValueChanged`.

### Phase 6 — Custom components + polish
- `IUICustomComponent` + `CustomElementAdapter`; `OnDrawExtra`; styles/theme overrides; debug overlay; `PerScreen` state; close‑all on return‑to‑title.

### Phase 7 — Acceptance: port Profit Calculator
- Rebuild `ProfitCalculatorMainMenu` as: Panel → Grid(2 cols) with rows Day/NumberInput, Season/Dropdown, Produce/Dropdown, Fertilizer/Dropdown, PayForSeeds/Checkbox, PayForFertilizer/Checkbox, MaxMoney/NumberInput, BaseStats/Checkbox; a horizontal Stack with Calculate/Reset buttons; Enter → Calculate.
- Rebuild `ProfitCalculatorResultsList` as a ListView of custom `CropBox` rows with `CropHoverBox` in overlay, opened as a child menu.
- Ship the port behind a config flag until parity is confirmed, then delete `ProfitCalculator/main/ui/**`.

### Phase 8 — Docs and release
- `UIFramework/README.md` rewritten from what actually exists; API reference generated from XML docs (a Doxyfile is already in the repo); Nexus page; example mod zipped alongside.

---

## 14. Testing strategy

- **Unit (no game)**: layout engine (measure/arrange for Stack/Grid/ScrollView), event routing (hit‑test, bubble, capture, overlay‑first), focus traversal order, number clamping/validation, `KeybindList` parsing. Abstract font measurement behind `ITextMeasurer` so tests don't need `SpriteFont`.
- **In‑game smoke (example mod)**: a "kitchen sink" screen exercising every component; console command `ui_demo` opens it; `ui_debug` toggles the bounds overlay.
- **Acceptance**: Profit Calculator port matches current behavior (Phase 7) at UI scales 75 % / 100 % / 150 % and after window resize; gamepad navigation reaches every control.
- **Proxy compatibility**: the example mod must compile against *only* the copied `IStardewUIApi.cs` (no project reference to UIFramework) — enforce by keeping it a separate project without a `ProjectReference`.

---

## 15. Risks and decisions

| Risk | Mitigation |
|---|---|
| Pintail cannot proxy a member (e.g. generic `IUIList<T>`, `ref`/`out`, nested generics of interfaces). | Keep the API non‑generic (`IUIList` with `int` indices + `Func<int, …>` builders); test every member from the example mod before release. |
| Consumers implementing `IUICustomComponent` get proxied on every call (per‑frame `Draw`). | Proxy overhead is a virtual call + argument mapping; XNA types pass through untouched. Measure with the debug overlay; if needed, offer `Draw` via a cached `Action<SpriteBatch, Rectangle>` delegate instead. |
| `Game1.keyboardDispatcher.Subscriber` fought over by multiple mods. | Only `FocusManager` sets it, only while a framework menu is active, and it restores the previous subscriber on close. |
| SpriteBatch state corruption when nesting `End/Begin` for scissor clipping. | Single helper `Draw.WithScissor(b, rect, action)` that restores exactly the outer parameters; no raw `b.End()` elsewhere (enforced in code review). |
| SDV 1.6 changed `IClickableMenu` signatures and UI viewport handling. | Target `MinimumApiVersion 4.0.0`, use `Game1.uiViewport` everywhere, and reuse the Profit Calculator position formula which already works on 1.6. |
| Naming collision with the existing StardewUI framework (focustense). | Keep the mod name **UI Framework** / id `6135.UIFramework`; call out the difference (code‑first builder API vs StarML markup) in the README. |

Decisions taken in this document:

- Code‑first, builder API returning typed interface handles (not string‑id everything, not JSON).
- Layout owned by containers with a measure/arrange pass (not absolute positions with ratio rescaling).
- Overlay pass + centralized focus manager instead of per‑component global state.
- Per‑consumer API instances so ids, hotkeys and styles are namespaced and cleaned up per mod.

---

## 16. Roadmap beyond v1

v1 (§1–§15) ships a code‑first framework that one mod uses to build its own screens. v2 opens those screens to *other* mods. v3 is a list of optional differentiators, each independent, picked up as time allows.

### 16.1 v2 — Modders hook their own components into any framework screen

Goal: a modder can contribute UI to a screen they do not own, and can build that contribution out of the framework's out‑of‑the‑box components rather than drawing pixels themselves.

**Extension slots**

- A screen owner declares named slots anywhere in its tree: `api.AddSlot(parent, "results.footer")`. A slot is an `IUIContainer` with an optional layout hint (stack direction, max height) and an optional `Func<bool>` visibility predicate.
- Any other mod registers a contribution: `api.ContributeTo("6135.ProfitCalculator", "results.footer", (IUIContainer slot, IUIScreenContext ctx) => { … })`. The builder callback receives the slot container and adds normal components to it (labels, checkboxes, a grid, a custom component). Contributions run every time the screen is built, so they see fresh state.
- `IUIScreenContext` exposes what the owner chooses to share: read‑only values (`ctx.GetString("season")`, `ctx.GetNumber("day")`), commands the owner exports (`ctx.Invoke("recalculate")`) and an event bus (`ctx.Subscribe("results.changed", Action)`). Owners publish values and commands explicitly through `api.Expose(menu, key, Func<string>)` / `api.ExposeCommand(menu, key, Action)`. Nothing else is reachable, which keeps owners in control and keeps the surface proxy‑safe (strings, numbers, delegates only).
- Ordering and conflicts: contributions are sorted by an optional `priority` then by mod id; a slot can declare `MaxContributions`; the owner can veto by contributor id. Contributions from a mod that throws are muted and logged, as in §10.
- Discovery: `api.ListSlots(ownerModId)` returns declared slots with their hints, so contributing mods can adapt at runtime and a debug command can print the slot map of every open screen.

**Reusable composite components**

- Consumers can package a subtree as a **composite**: `api.DefineComposite("6135.Shared.MoneyField", (IUIContainer host, IUICompositeArgs args) => { label + number input + currency icon })`. Composites are registered by one mod but resolvable by any mod through `api.AddComposite(parent, id, "6135.Shared.MoneyField", args)`. Args are a string‑keyed bag of primitives and delegates (proxy‑safe).
- Composites expose their own values and commands via the same `IUIScreenContext` mechanism, so a composite behaves like a first‑class component to whoever uses it.
- This is how mods share widgets without sharing assemblies: the framework is the only DLL anyone references.

**Custom components that embed built‑ins**

- `IUICustomComponent` (§7) gains an optional `Build(IUIContainer host)` step: the framework calls it once with a container the custom component owns, so a custom component can be "a hand‑drawn frame around a Stack of built‑in inputs". Layout, focus and events of the embedded built‑ins are handled by the framework; the custom component only draws its own chrome and handles its own clicks.
- The adapter forwards `Measure` to the embedded container when the custom component returns `null` from its own `Measure`.

**Screen decoration and wrapping**

- `api.OnScreenBuilt(ownerModId, menuId, Action<IUIMenu>)` lets a mod inspect and adjust an existing screen after it is built (hide an element, change a tooltip, add a button next to an existing one via `Find`). Runs after all slot contributions, so decorators see the final tree.
- Owners can mark elements `Sealed = true` to opt out of external modification.

**Deliverables**

- API additions: `AddSlot`, `ContributeTo`, `ListSlots`, `Expose`, `ExposeCommand`, `IUIScreenContext`, `DefineComposite`, `AddComposite`, `IUICompositeArgs`, `OnScreenBuilt`, `Sealed`, `IUICustomComponent.Build`.
- Profit Calculator declares slots (`settings.extra`, `results.header`, `results.footer`, `crop.hoverbox.extra`) and exposes its settings and the `recalculate` command; the example mod contributes a row to each to prove the path.
- Docs: a "Contributing UI to another mod" guide plus a "Publishing a composite" guide.

### 16.2 v3 — Optional differentiators

Each item stands alone; none is required by another unless noted.

| Feature | What it is | Builds on | Value |
|---|---|---|---|
| **In‑game inspector and editor** | Devtools‑style overlay: hover any element to see id, bounds, margins, grid tracks; drag to nudge, edit properties live; **export the resulting C# builder code** to the clipboard/log. | Tree + debug overlay (§11), layout engine | Cuts the compile‑launch‑look loop to seconds; no other Stardew UI framework has it. |
| **Player‑owned layout** | Every framework window is movable/resizable/collapsible by the player; positions and sizes persist per save via `helper.Data`. Zero consumer code. | `MenuHost`, `MenuRegistry` | Consistent player experience across all consumer mods. |
| **Theme assets** | Theme = Content Patcher‑patchable asset (fonts, colors, box sprites, spacing scale) with dark, high‑contrast and colorblind variants. One theme applies to every consumer. | `Theme`/`DefaultTheme` (§9) | Players restyle all mod UIs at once; modders get it free. |
| **Data grid** | Virtualized table with sortable/filterable columns, column resize, cell renderers, row selection and multi‑select events. | `ListView`, `ScrollView`, `Grid` | Replaces the Profit Calculator results list; nothing comparable exists. |
| **Signals** | `api.Signal(get)`, `api.Computed(() => …)` with automatic dependency tracking; bound labels/inputs re‑render only when inputs change. | Value binding (§6.2) | Reactive UI without StarML or view models. |
| **Auto‑forms from POCOs** | Generate a complete form from an object using attributes (`[Range]`, `[Choices]`, `[Section]`, `[Tooltip]`), with validation, dirty tracking, undo/redo, Save/Cancel. | Grid, inputs, validators | GMCM‑style forms for any object on any screen. |
| **HUD widgets and toasts** | Non‑modal overlays drawn during gameplay (counters, timers, notifications) using the same components, drawn from `Display.RenderedHud`. | Components, player‑owned layout | Extends the framework beyond menus. |
| **Accessibility** | Focused‑element announcements via the Stardew Access API, text scaling, reduced motion, high‑contrast theme. | FocusManager, themes | First accessible UI framework for SDV. |
| **Rich inline text** | Markup in labels/tooltips: colored spans, item icons, links, bold. | Label, tooltip builder | Better tooltips and result rows. |
| **Headless test harness** | Public `UIFramework.Testing` package: fake `ITextMeasurer`, scripted input driver, tree snapshot assertions; consumers test screens in CI without the game. | Tests (§14) | Unique developer‑experience win. |
| **Pseudo‑localization mode** | Config flag that stretches and accents all strings to catch overflow before translation. | Label | Cheap, useful. |
| **Debug console** | `ui_list`, `ui_dump <menu>`, `ui_perf`, `ui_slots` console commands. | Registry, inspector | Support and diagnostics. |

Suggested order if all are pursued: inspector → theme assets → data grid → player‑owned layout → signals → auto‑forms → HUD widgets → accessibility → rich text → test harness → pseudo‑localization → debug console.
