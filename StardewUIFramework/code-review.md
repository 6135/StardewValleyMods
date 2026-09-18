# UI Framework – code review and polish backlog

Branch `UIFramework` at `29733da` ("Fixes"), reviewed 2026‑09‑15 against `architecture.md`. Read‑only: nothing in the
code was changed; this file is the only output.

**Scope and method.** Every `.cs` file in `StardewUIFramework/` (≈8 200 lines) plus the example mod, README, NEXUS and
i18n were read in full. The core (`Core/*`, `Hosting/*`, `Rendering/*`, `Api/*`) was reviewed directly; the 16
component files and the README were split across three parallel reviewers, and every BUG / LIKELY‑BUG they reported
was re‑checked line by line before it was accepted here. Game‑dependent claims were verified against the decompiled
SDV 1.6 source in `D:\SteamLibrary\steamapps\common\Stardew Valley\Decompiled Code` (references in Appendix A) rather
than from memory; the one engine claim (MonoGame `SpriteBatch` state timing) is from MonoGame's `SpriteBatch.cs` and
should be confirmed in‑game with the repro given.

Severity: **BUG** = wrong behaviour a user will hit · **LIKELY‑BUG** = wrong under a plausible scenario (given) ·
**POLISH** = works but should improve · **DOC** = docs/code disagree · **NIT** = style/tiny. Effort: S (< 1 h), M
(half a day), L (a day or more). Line numbers are from the current tree.

---

## 1. Summary – what to fix first

The architecture is sound and the code follows it closely: one game‑facing class, measure/arrange layout, overlay‑first
routing, a single keyboard subscriber, guarded consumer callbacks, per‑consumer namespacing. The input components in
particular are careful (the Enter/Tab/Backspace double delivery from the keyboard dispatcher is handled correctly,
value binding and `OnValueChanged` are right, parsing is invariant‑culture). The problems are concentrated in four
places: the **measure contract** (fit‑content menus that contain a star Grid or a ListView become screen‑wide; Stacks
never distribute space; the example's divider is invisible), **1.6 rendering details** (the three‑way menu‑background
option, nested scissor clipping, title vs. `DrawBox=false`), **gamepad** (no initial snap, B swallowed while typing,
non‑focusable clickables unreachable, no on‑screen keyboard), and a handful of **component logic slips** (Dropdown
keyboard navigation overwritten by hover every tick, NumberInput cannot take a leading `-`, step finer than `Decimals`
is a no‑op, wheel "handled" when nothing changed).

| # | Item | Sev | Where | Effort |
|---|---|---|---|---|
| 1 | Fit‑content menus with a star Grid (default `columns="*"`) or a ListView measure as the full viewport width; Stack measures every child with the whole available size and never distributes | BUG | `Stack.cs:85,116-121`, `Grid.cs:237-239`, `ListView.cs:273`, `UIMenu.cs:265-270` | M |
| 2 | `AddSpacer(…, 0, h)` + `Line = true` is invisible (explicit `Width = 0` wins over alignment) — the example mod's divider never draws | BUG | `Spacer.cs:16-17,28` | S |
| 3 | Menu background option (Standard / Graphical / None) handled wrong: "Graphical" shows the un‑dimmed world, "None" still dims | BUG | `UIMenu.cs:351`, `MenuHost.cs` (no `showWithoutTransparencyIfOptionIsSet`) | S |
| 4 | Dropdown: keyboard Up/Down inside a mouse‑opened list is overwritten by `HandlePopupHover` every tick; Enter commits the row under the resting cursor | BUG | `Dropdown.cs:446-453,283,405-421` | S |
| 5 | Nested `WithScissor` (ScrollView in ScrollView, reachable via the public API) reads stale `device.RasterizerState` → inner not intersected with outer, outer re‑begun without scissor | LIKELY‑BUG | `Rendering/Draw.cs:100,119-126` | S |
| 6 | `ListView.Clear()` / `Remove()` (public via `IUIContainer`) detach the row panels permanently → blank list | LIKELY‑BUG | `ListView.cs:132-149`, `UIContainer.cs:54-75` | S |
| 7 | NumberInput: `-` rejected on a freshly focused `0`; `Step` finer than `Decimals` never moves; Up/Down with `Clamp=false` snap to the bound; wheel returns handled when nothing changed (stalls an enclosing ScrollView) | LIKELY‑BUG ×3 + POLISH | `NumberInput.cs:313-321,124/243,240-253,481-490` | S–M |
| 8 | Title drawn over the first row when `DrawBox = false`; tall menus lose the banner reserve (root cause of the known banner overlap) | LIKELY‑BUG | `UIMenu.cs:255,366-371,286,269-272` | S |
| 9 | Focus validation ignores hidden/disabled *ancestors*: a TextInput in a collapsed panel keeps focus and the keyboard subscriber; an open dropdown under a hidden ancestor keeps drawing and eating clicks; `DefaultButton` inside a hidden panel still fires on Enter | LIKELY‑BUG | `FocusManager.cs:117,169-172`, `Button.cs:175-185` | S |
| 10 | Gamepad: no snap on open, B (= `Keys.E`) swallowed while a text input is focused, list rows / scroll arrows / clickable images unreachable with snappy menus, no on‑screen keyboard | POLISH (goal §1.7) | `MenuHost.cs:142-145,178-181,184-205`, `UIMenu.cs:447-457` | M |
| 11 | Per‑frame allocations: `UIElement.Style` allocates two `UIStyle` per read (Button reads it 3× per frame, Label 2×), `ConsumerContext.Invoke` concatenates a key string per call, TextInput `ClipLeft` is O(n²) strings per frame | POLISH | `UIElement.cs:290-297`, `ConsumerContext.cs:42,71`, `TextInput.cs:87-95` | M |
| 12 | Overlay pass draws popups *under* `OnDrawOverlay` / `WantsOverlay` content while input still goes to the popup first; contradicts the class doc | POLISH | `OverlayLayer.cs:71-87` | S |

---

## 2. Findings by area

### 2.1 Layout and the measure contract

- **BUG (high)** — `Components/Stack.cs:85` measures every child with the stack's *whole* available size and
  `:116-121` arranges each at its desired extent with no distribution. Any child that answers "fill what you give me"
  takes everything: a `Grid` with a star track (`Grid.cs:237-239` resolves stars against `available`, and the default
  `columns` is `"*"`) in a horizontal stack claims the full width and pushes later siblings past `Bounds.Right` (drawn
  outside the box, not hit‑testable because `UIContainer.HitTest` requires containment); `rows="*,…"` in a vertical
  stack pushes the button row below the box. Combined with `UIMenu.Relayout` (`UIMenu.cs:265-270`), which measures
  the root with `viewport − insets` and sizes a fit‑content menu from the root's desired size, **any menu without an
  explicit `Width` that contains the README's own `"auto,*"` form grid or a `ListView` (`ListView.cs:273`:
  `Math.Max(available.X, …)`) is as wide as the screen.** The example mod masks this with `options.Width = 720`.
  *Fix (one coherent rule):* treat a fit‑content axis as unbounded — measure the root with `float.PositiveInfinity` on
  axes where `Width`/`Height` is null, and have `Stack` measure the main axis with infinity (or the remaining space).
  Both `LayoutEngine.ResolveTracks` (`:137-139`) and `ListView.cs:273` already fall back to content size for an
  infinite available, so they need no change; `Grid` needs the measure/arrange consistency fix below. Then decide
  whether main‑axis `Stretch` children of a Stack should share leftover space (today `Stretch`/`Center`/`End` on the
  main axis are silent no‑ops, `Stack.cs:116-119`) — needed to let a ScrollView/ListView fill a fixed‑height menu.
- **LIKELY‑BUG (medium)** — `Components/Grid.cs:237-239` vs `:346-347`. Star tracks are resolved against `available`
  in Measure (content‑sized when infinite, e.g. inside a ScrollView which passes `+∞` for Y, `ScrollView.cs:147`) and
  against `Bounds` in Arrange (weight‑sized). `rows="*,*"` with 20 px and 100 px children measures 120 px and arranges
  two 60 px rows; the tall child is truncated (`UIElement.Arrange:380` takes `min(desired, avail)`). *Fix:* remember
  in Measure whether stars fell back to content and resolve Arrange the same way (or carry the measured sizes over).
- **LIKELY‑BUG (medium)** — `Components/Grid.cs:291-293`. Children in auto/star cells are measured with the *whole*
  grid size and never re‑measured with their resolved cell. A wrapping `Label` in the `*` column of `"auto,*"` wraps at
  the full grid width (`Label.cs:91-92`) but is arranged into a narrower cell, so the row is too short and the text
  overdraws the cell edge. *Fix:* after `ResolveTracks` in `MeasureCore`, re‑measure children whose cell is narrower
  than the size they were measured with (only star/auto‑span children), or re‑measure in `ArrangeCore` before
  `child.Arrange`.
- **BUG (high)** — `Components/Spacer.cs:16-17` stores `Width`/`Height` verbatim, including `0`; `UIElement.Measure:342-345`
  and `Arrange:379` treat an explicit `0` as a size, so `AddSpacer(root, "divider", 0, 8); divider.Line = true`
  (`UIFrameworkExample/ModEntry.cs:57-58`) gets `Bounds.Width == 0` and `DrawCore:28` bails — the example's divider is
  never drawn and `Stretch` cannot rescue it. *Fix:* map `0` to `null` in the ctor and default that axis to
  `Stretch`; make `MeasureCore` return `LineThickness` on both axes when `Line` is set; `Line` setter should
  `InvalidateLayout()` (`Spacer.cs:20`).
- **POLISH (medium)** — `Components/Grid.cs:203-209` `DistributeSpan` spreads a spanning child's excess evenly over
  every non‑pixel track, inflating auto columns that a star track in the same span would absorb for free. Prefer star
  tracks in the span; touch auto tracks only when there is none.
- **DOC (high)** — `Core/LayoutEngine.cs:50` says unknown tokens are treated as `auto`; `:79-83` makes them `0px`
  (`"foo"`, `"*2"` collapse the track). Pick one (auto is friendlier). `LayoutEngine.cs:81` uses the culture‑sensitive
  `EndsWith("px")` overload → `StringComparison.Ordinal` (NIT).
- **DOC (medium)** — Panel: `Panel.cs:11`, `IStardewUIApi.cs:309`, `README.md:187,469` say children "overlap and
  fill the padded area"; `Panel` does not override `DefaultChild*Align` (`UIContainer.cs:99-101` → `Start`), so
  children sit top‑left at their desired size unless they set `Stretch`. Either override both defaults to `Stretch`
  (would also make ListView row content span the row) or fix the three docs. Same for ScrollView's summary
  (`ScrollView.cs:11-12`, "at the full content width") vs no alignment override — ListView does override it.
- **LIKELY‑BUG (low)** — `Components/Panel.cs:46` `padding > 0 ? padding : Style.Padding ?? 0`: `0` means "unset", so a
  consumer cannot force zero padding over a style, and `ListView` builds rows as `new Panel(…, padding: 0)`
  (`ListView.cs:136`) — a consumer default style with `Padding` set insets every list row on all sides (12 px in a
  44 px row leaves 20 px). Use `int?` so `0` is explicit; have ListView rows opt out of style padding.
- **POLISH (low)** — `Panel.cs:86-93` draws the 20 px 9‑slice border with no minimum inset: `AddPanel(p, id, true, 0)`
  draws children over the frame. Derive a minimum padding from the border when `DrawBox` is on, or document it.
- **NIT** — Stack/ScrollView sum float desired sizes but arrange `Ceiling` per child (`Stack.cs:88/93` vs `:116`,
  `ScrollView.cs:160` vs `:178-180`): the run can be `(n−1)` px longer than measured; the last child pokes out /
  the last pixels cannot be scrolled into view. Ceil per child in Measure. Zero‑size visible children still count for
  `spacing` (`Stack.cs:96-101`, matches WPF; document). Measure/Arrange loops `foreach` over `Children` while consumer
  delegates run inside (`Label.CurrentText`, `IUICustomComponent.Measure`) — a delegate that mutates the tree throws
  `InvalidOperationException` out of `Relayout`; `DrawChildren` already uses an index loop for exactly this reason.
  `Canvas.cs:25,49`: negative `X`/`Y` place a child partly outside `Bounds` where it draws but is not clickable.
- **NIT** — `UIElement.LayoutDirty` (`UIElement.cs:307`) is written but never read; only `UIMenu.LayoutDirty` drives
  relayout. Either delete it or use it for the partial‑relayout optimisation ScrollView drag wants (see §2.8).

### 2.2 Rendering and window chrome

- **BUG (high, verified against 1.6 source)** — `Core/UIMenu.cs:351` dims only when `!Game1.options.showMenuBackground`.
  SDV 1.6 has a three‑way option (`Options.setBackgroundMode`: Standard / Graphical / None →
  `showMenuBackground` / `showClearBackgrounds`). With **Graphical** the game replaces the world with
  `menu.drawBackground()` *only* for menus whose `showWithoutTransparencyIfOptionIsSet()` returns true (default false;
  `MenuHost` does not override it), so the framework draws neither the backdrop nor the dim and the world shows through
  un‑dimmed; with **None** the framework still dims although vanilla menus draw clear. The legacy Profit Calculator
  menus have the same quirk (parity), and `README.md:167-168` documents the wrong assumption. *Fix:*
  `MenuHost.showWithoutTransparencyIfOptionIsSet() => Menu.DimBackground` and dim when
  `DimBackground && !showMenuBackground && !showClearBackgrounds` (the vanilla `GameMenu.draw` condition).
  Consider not dimming a child `MenuHost` whose parent is a `MenuHost` (today both dim, 0.4 twice).
- **LIKELY‑BUG (high, engine)** — `Rendering/Draw.cs:100` detects an outer clip via
  `device.RasterizerState?.ScissorTestEnable`. MonoGame's `SpriteBatch` in Deferred mode applies its
  `RasterizerState` to the device only in `Setup()`, i.e. at `End()` — so after the outer `Begin(…, ScissorState)`
  the device still reports the game's state. A nested call therefore (1) does not intersect its clip with the outer
  one — an inner ScrollView partly scrolled out of the outer viewport draws outside it — and (2) on exit (`:119-126`)
  restores the outer *rectangle* correctly but re‑begins **without** scissor, so every outer child drawn after the
  inner ScrollView is unclipped until the outer `finally`. Only `ScrollView.DrawCore:196` calls `WithScissor`, and
  nesting is reachable through the public API (`AddScrollView(scrollView, …)`, or via Panel/Grid/Stack/ListView row).
  *Fix:* keep the framework's own clip stack in `DrawHelper` (static depth + current effective rect); never sniff
  device state. *Repro:* ScrollView inside ScrollView, scroll the outer so the inner is half hidden.
- **LIKELY‑BUG (high)** — `Core/UIMenu.cs:255` `InsetTop` is only `padding` when `!drawBox`, but `:366-371` still draws
  the title at `Bounds.Y + 8` in the dialogue font → the title overlaps the first row whenever `DrawBox = false` and a
  title is set. *Fix:* add the title height (+8) to `InsetTop` when `title != null && !drawBox`.
- **Known gap, concrete cause** — `UIMenu.cs:286` `minY = min(TitleReserve, max(0, vp.Y − h))`: a menu taller than
  `vp.Y − 72` loses the reserve, and the scroll banner (≈68 px tall, drawn at `Bounds.Y − 56`, clamped to ≥ 8) lands on
  the box's top edge and first row — the "at some sizes" overlap in §0. *Fix:* in `Relayout` (`:269-272`) clamp `h`
  to `vp.Y − TitleReserve` when a title and box are present so the banner always has room.
- **POLISH (medium)** — Texture cache: `UIServices.cs:90` caches `LooseSprites\textBox` in a static forever, so the
  Content‑Patcher retexturing promised in architecture §11 / README only applies to the first load. Subscribe to
  `Content.AssetsInvalidated` (or `AssetReady`) for that name and drop the cache. `UIServices.cs:93`
  `SmallTextBoxTexture` and `assets/text_box_small.png` are shipped but never drawn (README:59-60 advertises them).
- **POLISH (low)** — `MenuHost.draw` (`MenuHost.cs:62-71`) draws the close button after `Menu.Draw`, i.e. over
  tooltips/overlays. Draw it before the overlay pass (or let `UIMenu.Draw` draw it).
- **POLISH (low)** — Disabled look is inconsistent: Button/Dropdown/Image/inputs/Slider draw at 50 % or gray, Label,
  Spacer and Panel do not (`Label.cs:113`), while `README.md:424` says disabled elements draw greyed.
- **NIT** — `ScrollView.cs:196` allocates a closure per frame for `WithScissor`; `UIServices.cs:59-63` `PlaySound`
  lambda is formatted on one line; `Draw.cs:109` sets the scissor in render‑target pixels, which is correct because
  the game draws menus into the UI‑sized `uiScreen` target (`Game1.PushUIMode`) — confirm at 75/100/150 % UI scale as
  §14 already plans.

### 2.3 Input routing, focus and keyboard

- **LIKELY‑BUG (medium)** — `Core/FocusManager.cs:117` `Validate` and `:169-172` `CanFocus` check only the element's own
  `Visible`/`Enabled`; `IsUsable` (`:141-151`, used by traversal) checks ancestors. Consequences: a TextInput inside a
  panel that becomes hidden/disabled keeps focus and `Game1.keyboardDispatcher.Subscriber`; an open Dropdown under a
  hidden ancestor is never closed (`Dropdown.HandleFocusLost` → `Close()` never runs), `OverlayLayer.Draw` keeps
  calling `DrawPopup` at stale bounds and `PopupBounds` keeps routing clicks/wheel to it; `Button.HandleActivate`
  (`Button.cs:175-185`) fires a `DefaultButton` inside a hidden panel on Enter. *Fix:* use `IsUsable` in
  `Validate`/`CanFocus`/`HandleActivate` (and `Dropdown.Update` can close when not effectively usable).
- **POLISH (low)** — `UIMenu.cs:447-457` `AfterOpened` never calls `Focus.UpdateSubscription()`, so
  `element.Focus()` before `Open()` (allowed: `CanFocus` needs only `OwnerMenu == menu`) leaves the keyboard
  unsubscribed until focus changes. One call fixes it.
- **POLISH (low)** — `EventRouter.cs:44-48` `FocusFromClick` runs before bubbling and clears focus on any
  non‑focusable target: clicking a ScrollView/ListView scrollbar or arrow (`ScrollView.cs:219`, `ListView.cs:342`)
  blurs a focused text input. Let elements opt out ("preserves focus") for scrollbar parts, or only clear focus when
  the click landed on nothing.
- **NIT** — `EventRouter.cs:31` resets `Captured` on *any* click, so a right click during a left drag (slider, scroll
  thumb) drops the capture without `HandleClickRelease`. Only reset for left clicks, or deliver a release first.
- **NIT** — `EventRouter.cs:135-141` delivers the wheel only along the hovered chain; verified that ScrollView and
  ListView return themselves over empty space so this works. `MenuHost.receiveKeyPress` (`:142-145`) and
  `UIMenu.Tick` calling `Focus.Validate()` twice per tick (`Relayout` also calls it) are harmless.
- **DOC (medium)** — Keyboard subtleties worth a sentence in the interface docs: an element/menu `OnKey` returning
  `true` for a typing key does not stop the character (insertion arrives through the keyboard subscriber,
  `TextInput.cs:374-377`); Up/Down in NumberInput step via `HandleSpecialInput` before `OnKey` can decline
  (`NumberInput.cs:459-478` vs `:512-515`); `IUITextInput.Value`/`IUINumberInput.Value` setters bypass `MaxLength`,
  `Validate` and `OnValueChanged` (`TextInput.cs:156-163`, `NumberInput.cs:49-63`).

### 2.4 Gamepad (architecture §1 goal 7; §0 "relies on vanilla snapping")

All verified against `Game1.updateActiveMenu` / `Utility.mapGamePadButtonToKey` (Appendix A).

- **POLISH (high)** — No snap on open. The `activeClickableMenu` setter does not snap; vanilla menus call
  `populateClickableComponentList(); snapToDefaultClickableComponent()` in their constructors when
  `Game1.options.SnappyMenus`. `UIMenu.Open`/`OpenAsChild` never do, so a controller user opens a menu with the cursor
  wherever it was. *Fix:* after the first `Relayout` in `AfterOpened` (bounds must be valid first — note `Open`
  assigns `Game1.activeClickableMenu` before layout, `UIMenu.cs:423-425`), call both when `SnappyMenus` is on.
- **POLISH (high)** — B / Start / Y arrive as `receiveKeyPress(Keys.E)` (first key of `menuButton`, default `[E, Escape]`).
  `MenuHost.receiveKeyPress:142-145` returns early while a text input is focused, so a gamepad user cannot leave a
  focused TextInput/NumberInput with B (only A on another element). *Fix:* override `receiveGamePadButton`: B with a
  focused text input → `Focus.ClearFocus()` (Escape semantics), else fall through.
- **POLISH (high)** — `populateClickableComponentList` (`MenuHost.cs:184-205`) lists only `Focusable` elements
  (Button, Checkbox, Dropdown, NumberInput, Slider, TextInput, custom `WantsFocus`). ListView rows, ScrollView arrows /
  thumb, Image/Panel/Label with `OnClick` and non‑focus custom components are unreachable with snappy menus, and the
  thumbstick cursor is banned unless `overrideSnappyMenuCursorMovementBan()` returns true. *Fix:* the one‑liner
  `overrideSnappyMenuCursorMovementBan() => true` restores free cursor movement; better, add elements with
  `HasPointerHandlers`, list rows and scroll arrows as snap targets, and filter out targets clipped by a ScrollView
  (today the cursor can snap to an invisible button whose click is rejected, `ScrollView.cs:222`).
- **POLISH (medium)** — After a relayout `SyncBounds` nulls `allClickableComponents` (safe: `applyMovementKey`
  repopulates) but `currentlySnappedComponent` keeps pointing at the old `ClickableComponent` with stale bounds;
  `gameWindowSizeChanged` (`:225-230`) does not re‑resolve it. Re‑populate and re‑select by name
  (`ClickableComponent.name == element.Id`) after `SyncBounds`.
- **POLISH (medium)** — No on‑screen keyboard: vanilla `TextBox.Selected` calls `Game1.showTextEntry(this)` when
  `gamepadControls && !lastCursorMotionWasMouse` (`TextBox.cs:141-143`). Framework text inputs never do, so
  controller users cannot type. Either bridge to a `TextEntryMenu` or document the limitation in README "gamepad".
- **DOC** — Two navigation systems coexist: keyboard arrows drive `FocusManager.MoveDirection`, while the d‑pad /
  stick arrive as `W/A/S/D` (`moveUp…Button` first keys) and drive vanilla `applyMovementKey` cursor snapping; they
  only converge on A → `receiveLeftClick`. Document, or map the `move*Button` keys to `MoveDirection` when snappy menus
  are off.

### 2.5 Overlay, popups and Dropdown

- **BUG (high)** — `Components/Dropdown.cs:446-453` `HandlePopupHover` sets `highlightIndex = RowAt(px, py)`
  unconditionally, and the game calls `performHoverAction` every tick (`Game1.cs:4501`) → `EventRouter.Hover` →
  `OverlayLayer.TryHandleHover`. The open list is placed at `Bounds.Y` (`:283`) so it always covers the closed box; after
  a mouse‑open the cursor rests inside it, Up/Down (`MoveHighlight`, `:405-421`) move the highlight for zero frames and
  Enter commits the row under the cursor. *Fix:* only take the hover row when the cursor actually moved (remember the
  last hover point), or let keyboard navigation own the highlight until the mouse moves.
- **LIKELY‑BUG (medium)** — `Dropdown.cs:131-149,152-161` + `:225`. For an unbound dropdown `RefreshChoices()` computes
  `previous = GetChoice(ownIndex)` = `""` when nothing is selected and `RestoreOwnIndex("")` returns `0`; `Open()`
  calls `RefreshChoices()` every time, so `SelectedIndex = -1` (or a `SelectedValue` not in the choices) silently
  becomes the first choice on open, with no setter call and no `OnValueChanged`. Only fall back to 0 when a previously
  selected value disappeared.
- **POLISH (high)** — `Dropdown.cs:363-368` runs built‑in navigation before `base.HandleKey`, so the consumer's `OnKey`
  cannot intercept Up/Down/Enter/Space (every other input calls `base.HandleKey` first). `:375-387` Up/Down on a
  *closed* focused dropdown commit a new value and return `true`, so vertical focus traversal cannot leave a dropdown
  (vanilla `OptionsDropDown` uses Left/Right). README:280-282 omits this exception.
- **POLISH (medium)** — `OverlayLayer.cs:71-87` draws popups first, then frame draws (`OnDrawOverlay` callbacks,
  `WantsOverlay` elements), so those paint *over* an open dropdown list while `TryHandleClick` still gives the popup
  first pick — draw/input mismatch, and the class doc says popups "draw above everything". Draw popups last.
- **POLISH (medium)** — Dropdown polish set: close sound only from `Close()` (`:243-252`), not on click‑outside /
  Escape (`OverlayLayer.cs:140`, `EventRouter.cs:239-243`), and `Style.ClickSound` overrides only the open cue;
  fixed 300×44 measure (`:271`) with unclipped labels (`:297-298,325`); no scroll indicator when
  `choices.Length > maxVisible` and `ListBounds` (`:281-284`) can push rows off‑screen when `MaxVisible × 44` exceeds
  the viewport; `HandlePopupScroll` (`:455-469`) raises `OnScroll` on every notch even at the ends and passes the raw
  wheel value (ListView passes a row delta); `Open()` (`:218-239`) checks `OwnerMenu != null` but not `IsOpen`, so a
  programmatic open before the menu shows plays a sound with nothing on screen; the ctor calls `RefreshChoices()` while
  detached so a throwing delegate is logged/muted under the process‑global `ConsumerContext.None` (`:46-53`); opening a
  second dropdown takes two clicks (the first is swallowed closing the first) while README:268 reads as one.
- **NIT** — `Dropdown.cs:126-128` dead per‑index label fallback (`labels` is always resized to `choices`);
  `SelectedIndex`/`SelectedValue` setters are silent and behave differently bound vs unbound (`:76-91`) — document.
- **LIKELY‑BUG (low)** — `UIMenu.cs:223-226,487` set `Hovered = null` directly instead of `Router.SetHovered(null)`, so
  `HandleHoverLeave` / `OnHoverEnd` / `IUICustomComponent.OnHover(false)` never fire on close or detach;
  `VolumeGauge.hovered` stays `true` across close/re‑open (the tree is reused) and draws highlighted until the cursor
  passes again. Route both through `SetHovered(null)`.
- **POLISH (medium)** — `ScrollView.cs:196` + `UIElement.Draw:406-421`: overlay‑drawn descendants (`WantsOverlay`,
  `OnDrawOverlay`) register inside the scissored pass but draw unclipped and are hit‑tested by their own bounds before
  the tree (`OverlayLayer.ElementAt`), bypassing the viewport check — a `WantsOverlay` component scrolled out of view is
  still drawn and clickable. Skip drawing children outside the viewport (also a perf win, §2.8) and/or intersect
  overlay hit‑tests with ancestor viewports.

### 2.6 Components

**TextInput** (`Components/TextInput.cs`)
- POLISH (high) `:87-95` `ClipLeft` strips one char and re‑measures per iteration, every frame: a 200‑char value in a
  192 px box allocates ~180 substrings and O(n²) `MeasureString` per frame (default `MaxLength` is unlimited). Find the
  cut once (binary search on prefix widths) and cache by (text, width).
- POLISH (medium) `:331-346` paste appends the clipboard verbatim: control chars, newlines and `"` (which the
  per‑char path rejects at `:297-300`) are committed; a multi‑line clipboard renders outside the box. Filter
  `char.IsControl` and `"`.
- POLISH (medium) `:235-242` (and `NumberInput.cs:182-189`) `Raise("Validate", …, false)`: once a validator throws and
  is muted, every later edit is rejected forever with one log line. Fail open (`true`) or say so in the mute message.
- NIT `:263` (`NumberInput.cs:354`) `sizeChanged && texture == null` — the second test is redundant; `:104` clips very
  long unfocused text from the left so the integer part of a wide number disappears.

**NumberInput** (`Components/NumberInput.cs`)
- LIKELY‑BUG (high) `:313-321` with `:373`/`:299`. On focus the buffer is `Trim(Value)` with the placeholder flag off,
  so a zero value gives `"0"`; `InsertMinus` rejects `-` (`previous.Length > 0 && !bufferIsPlaceholder`) while
  `InsertDigit` treats the same `"0"` as replaceable: `Min=-10, Value=0`, type `-5` → **5** (same for paste). Accept
  `-` when `previous == "0"`.
- LIKELY‑BUG (high) `:124` + `:243` `StepBy` normalises `current + step` with `Math.Round(value, decimals)` before the
  `Same` check, so `step: 0.25` with the default `Decimals = 0` never changes the value (and `HandleScroll` still
  returns handled); `0.5` steps by 1. Round to `max(decimals, digitsOf(Step))` or warn once.
- LIKELY‑BUG (medium) `:240-253` `StepBy` passes `forceClamp: true` even when `Clamp == false`: an out‑of‑range value
  (allowed with clamping off) jumps to the bound on the first Up/Down/wheel in either direction. Clamp only when the
  step would cross the bound.
- POLISH (high) `:481-490` `HandleScroll` returns `true` whenever enabled and hovered, even when `StepBy` changed
  nothing → an enclosing ScrollView stalls when the cursor passes a box at its bound (`ScrollView.HandleScroll` returns
  `false` at the ends on purpose), and `menu.OnScroll` never fires. Hover‑only wheel stepping (the `IsFocused ||`
  clause is inert; the router only delivers to the hovered chain) also edits values while scrolling a form. Return
  whether the value changed; consider requiring focus.
- POLISH (medium) `:410-421` paste commits per character (`"12.5"` → setter + `OnValueChanged` ×3); build the buffer
  then apply once, as TextInput does.
- Smaller: `:315` `InsertMinus` tests `Min >= 0` instead of `Math.Min(Min, Max)`; `:278` `char.IsDigit` accepts
  non‑ASCII digits the invariant parser rejects; `:288` only `.` is a decimal separator (numpad `,` on comma locales);
  `:148/:143` format strings rebuilt per frame/call; `:396-407` the type sound plays even when the validator rejected;
  `:49-63` `Value` setter stores raw (no rounding/clamp/validate) and the blur re‑normalisation then raises
  `OnValueChanged` without a keystroke; `:119-124` NaN → 0 instead of `EmptyValue`, and `Math.Round` keeps `-0`
  (formats as `"-0"`); `:230` restoring a negative placeholder clears the placeholder flag.

**Checkbox / Slider / Button**
- DOC (high) `README.md:311-312` "Buttons and checkboxes also expose `ClickSound`/`HoverSound`": `IUICheckbox`
  (`IStardewUIApi.cs:446-455`) has no `HoverSound` and `Checkbox` never plays one (no `HoverSoundCue` override).
- DOC (medium) `README.md:250-252` "buttons, checkboxes, dropdowns … mark their own clicks handled": Button
  (`Button.cs:165-173`), Checkbox (`:148-156`) and Slider (`:172-180`) return the consumer's `Handled` flag (false by
  default) and bubble — ListView row selection depends on it (`ListView.cs:347-351`); only Dropdown marks handled.
  Fix the sentence, not the code.
- POLISH (high) `Button.cs:122,144,161` resolve `Style` three times per frame via the `Font` getter (6 `UIStyle`
  allocations per button per frame); use the already‑resolved style inside `DrawCore`/`MeasureCore`.
- POLISH (low) Button: fixed `Width`/`Height` smaller than icon+text overflows both sides (`:147-161`); `DrawBox=false`
  buttons give no hover/focus feedback (`:123-129`); Checkbox label overflows a narrow width (`Checkbox.cs:138-141`);
  Slider snapping produces binary noise (`0.30000000000000004`) in the setter and `IUIValueEvent.NewValue`
  (`Slider.cs:95`) — round to the step's digits; `Fraction` does not guard NaN from the getter (`:131,163-165`).
- LIKELY‑BUG (medium, all four value components) — a *mixed* pair from the API (getter non‑null, setter null; the
  facade passes both unchecked, `StardewUIApi.cs:134-173`): `Store` (`TextInput.cs:208-216`, `NumberInput.cs:163-171`,
  `Checkbox.cs:75-83`, `Slider.cs:75-83`) updates only `ownValue` while `Value` keeps returning the getter, so every
  keystroke/click/drag looks like a change and raises `OnValueChanged` (and sounds) although the displayed value never
  moves. Throw `ArgumentException` in `Add*` when exactly one of get/set is null, or treat getter‑without‑setter as
  read‑only.

**Label / Image / CustomElementAdapter**
- POLISH (high) `Label.cs:39-41,112-113` resolves `Style` twice per frame (4 allocations; 6 when the text changed) and
  `TextInRect` (`Draw.cs:66-68`) measures the whole (possibly wrapped) text every frame even for `Start`; block‑aligns
  wrapped text by its widest line so `Center`/`End` leave inner lines left‑aligned. Cache the content size from
  `MeasureCore`; align per line.
- POLISH (medium) `Label.cs:102-111` a text delegate returning a new string every frame (timers) triggers a full‑tree
  `Relayout` every frame; only invalidate when the measured size actually changed.
- POLISH (medium) `Image.cs:14` has no `IsHitTestVisible` override, unlike Label/Stack/Grid/Canvas/bare Panel: a plain
  image blocks its parent's tooltip and clears focus on click (the example's list rows put an Image first,
  `ModEntry.cs:126`). `IsHitTestVisible => HasPointerHandlers`. `:85-106` does not guard `texture.IsDisposed`
  (throws inside draw, outside the guard); `:24` vs `:55` inconsistent `Scale` clamping, neither rejects NaN;
  explicit `Width`/`Height` stretch and ignore `Scale` (class XML only).
- LIKELY‑BUG (medium) `CustomElementAdapter.cs:32-36` clamps negatives but passes NaN/Infinity into layout;
  `ScrollView` hands children `available.Y = +∞` (`ScrollView.cs:147`), so a "fill me" implementation returns ∞ →
  `ScrollView.contentHeight = (int)Math.Ceiling(∞)` (int.MinValue on .NET 6 x64) and Stack/Grid sums go non‑finite.
  Replace non‑finite components with 0 and document that `available` can be infinite.
- POLISH (low) adapter: `OnHover(px, py, true)` is forwarded every tick while the cursor rests (`:59-64`; proxied call
  + closure per tick) — forward only on movement; `Update` runs for invisible components with stale bounds
  (`:44-48`); one exception in `Draw`/`Update` mutes the component for the rest of the session (`ResetMutes` is never
  called); component `OnKey` runs before the element `OnKey` and the latter still runs when handled (`:73-80`).
  `IUICustomComponent.OnClick/OnHover` receive no bounds, which is why `VolumeGauge` caches `lastBounds` — candidate for
  an additive V2 member.

**ScrollView / ScrollbarGadget / ListView**
- LIKELY‑BUG (medium) `ListView.cs:132-149` `EnsureRowContainers` only compares `rows.Count` to `visibleRows`, and
  `UIContainer.Clear/Remove` are public and non‑virtual (`UIContainer.cs:54-75`, exposed through
  `IUIList : IUIContainer`): `list.Clear()` detaches the row panels, `rows` still references them, `ArrangeCore`/`BuildRow`
  keep working on detached panels and nothing is ever drawn again. Make Clear/Remove virtual (ListView: clear row
  contents) or re‑attach rows whose `ParentElement != this`.
- POLISH (medium) `ScrollView.cs:113` `MaxScroll` is 0 until the first Measure, so `ScrollTo`/`ScrollBy`/`ScrollOffset`
  before `Open()` clamp to 0 and are lost (ListView's `EnsureFresh` avoids this). Clamp in `ArrangeCore` instead.
- POLISH (medium) `ScrollView.cs:196` draws every child regardless of intersection with the viewport (all consumer
  text getters and `Style` resolutions run for clipped content); cull by `Bounds ∩ ViewportRect`.
- POLISH (medium) `ListView.cs:152-163` from `Update:304-307` rebuilds **all** rows on any count change, so a
  TextInput/Dropdown inside a row loses focus/popup whenever an item is appended elsewhere; `SetFirstVisible:206-213`
  already has "rebuild only rows whose item changed" — reuse it.
- LIKELY‑BUG (low) `ListView.cs:152-163,178` `Refresh()` is synchronous: called from a row child's `OnClick`
  (a "remove" button) it detaches the button mid‑bubble, so `EventRouter.Bubble:70` stops and `ListView.HandleClick`
  (selection, consumer `OnClick`) never runs. Defer to `Update` via `needsRefresh`.
- POLISH (low) ListView: one throwing `buildRow` mutes the delegate for the whole list (`:190`; per‑row key or
  per‑item log); first programmatic `ScrollTo` builds every row twice (`:195-198`); scrollbar column always reserved
  with no `ShowScrollbar` (`:256`); `Selectable=false` also hides a programmatic `SelectedIndex` highlight (`:314`);
  stale `selectedIndex` when the count shrinks; `:246` `int.ToString()` is culture‑sensitive (`"−1"` under ICU
  cultures) — use `InvariantCulture` like `UIValueEvent.Number`.
- POLISH (low) ScrollView: every offset change → `InvalidateLayout()` → full‑tree relayout once per tick while
  dragging (`:121`); thumb drag jumps the thumb centre to the cursor (no grab offset, `:263`, `ScrollbarGadget.cs:85`);
  no PageUp/PageDown/Home/End and no scroll‑into‑view on focus (Tab can focus a clipped child; gamepad can snap to it);
  gadget arrows overlap below `2×ArrowHeight + 2×TrackGap` (`ScrollbarGadget.cs:56-66`).
- DOC (medium) `ListView.cs:92` `ItemCount` re‑invokes the consumer delegate on every access (and every tick from
  `Update:304`) while `IStardewUIApi.cs:379-380` / `README.md:518` say "at the last refresh" (`lastCount`); the
  auto‑refresh on count change is undocumented; `SelectedIndex` setter does not raise `OnValueChanged` although the
  API doc reads as if it does (`:388`).

### 2.7 Lifecycle, hosting and registry

- **POLISH (medium)** — Parent menu under a child: the game updates and hover‑tests only the deepest child
  (`Game1.cs:4425-4429,4501,4505`) but draws every menu in the chain (`DrawMenu`). The parent therefore keeps its last
  `Hovered` (highlighted button, and its tooltip once the delay passes, drawn under the child), and `OnUpdate` /
  caret blink / `Root.Update` stop while the child is open although `IUIMenu.OnUpdate` promises "every tick while
  open". Clear the parent's hover in `OpenAsChild` (`UIMenu.cs:430-445`) and document the update pause.
- **POLISH (low)** — `UIMenu.Open(force: true)` over another framework menu: the `activeClickableMenu` setter does not
  call `cleanupBeforeExit`, so the old host is dropped silently and its `OnClose` fires on the next `UpdateTicked`
  through `MenuRegistry.ValidateOpenMenus`. Close an existing `MenuHost` explicitly first.
- **DOC (low)** — `IUIMenuOptions.Modal` "Block clicks outside the menu (default true)" (`IStardewUIApi.cs:595-596`,
  README:643/663): the implementation is "when false, a click outside *closes* the menu"
  (`MenuHost.cs:101-104`); README:166 says so, the API doc does not. Name or document it as click‑outside‑to‑close.
- **DOC (low)** — Split screen: `MenuRegistry.menusByConsumer` is global while `open` is `PerScreen`, and a `UIMenu`
  has one `Host`, so a menu can be open on only one screen at a time (`IsOpen` blocks the other). Document.
- **POLISH (low)** — `HotkeyService` fires while a framework TextInput owns the keyboard (a letter‑bound toggle fires
  while typing). Skip when `Game1.keyboardDispatcher.Subscriber != null` (trade‑off: toggle‑to‑close while typing);
  `README.md:340-341` claims invalid keybinds are logged, but empty/unbound strings are ignored silently and
  `Register` removes an existing binding in that case (`HotkeyService.cs:35-38,46-55`).

### 2.8 Performance and allocations (per frame)

- **POLISH (high)** — `UIElement.cs:290-297` `Style` = `Theme.Default.Merge(Consumer.DefaultStyle).Merge(StyleObject)`:
  two `UIStyle` heap objects (10‑field copies) per read. Reads per frame: Button 3 (+1 per layout), Label 2 (4 when
  text changed), Dropdown 1 (2 open), Checkbox/TextInput/NumberInput/Slider/Panel(boxed)/Spacer(line) 1, Panel 2 per
  layout when unpadded (×VisibleRows for a ListView), plus one per click/hover‑enter in Button/Checkbox. A 20‑control
  form allocates ~50 objects per frame from styles alone. *Fix:* resolve field‑by‑field without intermediates
  (`StyleObject?.Font ?? Consumer.DefaultStyle?.Font ?? …`) or cache a `ResolvedStyle` per element invalidated by a
  version stamp on `UIStyle` setters and `SetDefaultStyle`; keep `Theme.TextColor` lazy (`Theme.cs:127`).
- **POLISH (medium)** — `ConsumerContext.cs:42,71` build `elementId + "|" + eventName` on every guarded call; per‑frame
  callers: every bound getter, `Label.Text`, `Button.Text`, `Checkbox.Label`, `Placeholder`, `Title`, `Tooltip`,
  `OnDrawExtra`, `OnUpdate`, all `Custom.*`, `ListView.ItemCount` every tick. Fast‑path `muted.Count == 0`, or key on
  a `(string, string)` tuple. `Invoke<T>` also ignores `LogCallbacks` while the `Action` overload honours it.
- **POLISH (medium)** — `TextInput.ClipLeft` O(n²) (§2.6); Label re‑measure per frame; `NumberInput.Format` string
  building per frame; `Grid` allocates iterators/arrays several times per pass (`Grid.cs:243-252,259,287,302,317`);
  ScrollView draws clipped children; ScrollView drag relayouts the whole menu per tick; `UIElement.Draw` allocates a
  delegate per frame for overlay elements (`:409`) and a closure for `OnDrawOverlay` (`:420`); `Stack`/`Grid`/`Panel`
  `foreach` over `IReadOnlyList` boxes the enumerator per pass.

### 2.9 API surface, Pintail and argument validation

- **Verify in‑game** — `IUIStyle.Font` is `UIFont?` (a `Nullable<>` of a consumer‑side enum). The example mod never
  touches `IUIStyle`, so Pintail's mapping of `Nullable<enum>` is unproven; if unsupported, `CreateStyle()` fails at
  first use. Add `var s = api.CreateStyle(); s.Font = UIFont.Dialogue;` to the example (§14 "test every member").
  `int?`/`Color?`/`Rectangle?`/`object`/`Keys` are shared types and fine.
- **POLISH (low)** — Silent `as` casts: `StardewUIApi.SetDefaultStyle` (`:264`), `IUIElement.Style` (`UIElement.cs:151`),
  `DefaultButton`/`CancelButton` (`UIMenu.cs:187-188`) store `null` for a foreign object while `Attach`/`Find`/`Remove`
  throw "was not created by this framework" (README:741-742 promises the throw). Use `Unwrap`‑style exceptions.
- **DOC (medium)** — Three different `OnScroll` semantics under one name: `IUIMenu.OnScroll` raw direction (+ = up),
  `IUIScrollView.OnScroll` pixel delta (− = up), `IUIList.OnScroll` row delta, `IUIDropdown.OnScroll` raw direction
  every notch. Document side by side (or unify on "delta in the component's unit").
- **DOC (medium)** — `IUIStyle.ClickSound`/`HoverSound` docs read as universal; only Button (both), Checkbox
  (click) and Dropdown (open = click, hover) honour them; inputs use fixed typing cues, Slider/Label/Image/Panel ignore
  them. `IUICheckbox.ClickSound` lacks the `null = default, "" = none` doc that `IUIButton` has.
- **DOC (low)** — `IUIEvent.Element` / every `Func`‑typed interface property returns `null` when unset although the
  interface types are non‑nullable (consistent pattern; one sentence in the file header would do). `IUISpacer` gets
  `Tooltip`/`OnClick` via `IUIElement` but is never hit‑testable (`Spacer.cs:22`).
- **NIT** — `StardewUIApi.Attach` (`:294`) runs a full‑tree `FindById` per `Add*` for the duplicate‑id warning; a
  `buildRow` adding k elements does k tree scans per rebuilt row. Keep a per‑menu id set instead.

### 2.10 Dead code and leftovers

`HotkeyService.UnregisterAll` (`:62`), `MenuRegistry.MenusOf` (`:44`), `ConsumerContext.ResetMutes` (`:95`),
`UIServices.Translation` (`:84`, set in `ModEntry.cs:25`), `UIServices.SmallTextBoxTexture` (`:93`) + `assets/text_box_small.png`,
`OverlayLayer.Popups` (`:27`), `i18n/default.json` key `menu.close`, `UIElement.LayoutDirty` (§2.1). Either wire a
per‑consumer teardown (`UnregisterAll` + `MenusOf` + `ResetMutes` on menu rebuild — architecture §2 promises "all of
that consumer's menus/hotkeys can be torn down together" but no API exposes it) or delete them.

### 2.11 Documentation drift

- **architecture.md** — §4 diagram / §12 list `Input/`, `Themes/`, `Registry/`, `Config/`, `DefaultTheme.cs`; the tree
  is `Hosting/`, `Rendering/Theme.cs`, `ModConfig.cs`. §8's sketch differs from the shipped API in details
  (`CreateMenu(id)` overload, `IUIMenuOptions`, `Find` on the menu, no `IUIDivider` — it is `IUISpacer.Line`). §0
  known gaps should add: fit‑content width (§2.1), 1.6 background modes (§2.2), gamepad items (§2.4), the
  child‑menu update pause (§2.7). §5 "Units: UI pixels" and §9 "Clipping … restores exactly the outer parameters" —
  the restore is hard‑coded to the game's menu batch parameters (fine, but say so). §6.3 already notes
  `ButtonsChanged` vs `ButtonPressed`.
- **README.md** (both `IStardewUIApi.cs` copies are byte‑identical; every sample compiles against the interface) —
  behavioural over‑claims: 167‑168 backdrop assumption (§2.2); 250‑252 handled clicks; 268 "opening another closes the
  first" (two clicks); 280‑282 arrow‑key exceptions omit Dropdown/custom; 292‑293 gamepad reach (§2.4); 297/239
  tooltip/`OnClick` "any element" vs Spacer; 311‑312 checkbox `HoverSound`; 310 close cue; 340‑341 hotkey logging; 424
  greyed disabled elements; 469/187 Panel fill; 518 `ItemCount`; 521 `SelectedIndex`/`OnValueChanged`; 544‑546 Image
  null/stretch; 601‑602 dropdown setter semantics; 606/711/222 `RefreshChoices` timing and label‑length rule; 611
  dropdown `OnScroll`; 324/326 custom `Update`/`OnHover` per‑tick; 322 infinite `available`; 741‑742 foreign objects;
  226‑227 "getter every frame" does not hold for a focused NumberInput; 154 `CreateMenu` replace also closes; 59‑60
  unused small text box.
- **NEXUS.md:35** and root README rows link `blob/master/StardewUIFramework/README.md`, which does not exist on
  `master` yet (branch not merged) — reminder for the pending upload.
- **i18n** — all six `config.*` keys used; `menu.close` unused.

---

## 3. Suggested work packages (in order)

| # | Package | Files | Effort |
|---|---|---|---|
| A | **Measure contract**: unbounded fit‑content axes in `UIMenu.Relayout`; Stack main axis unbounded/remaining + optional leftover distribution for `Stretch`; Grid star measure/arrange consistency + re‑measure of star/auto‑span children; ListView width = content; Spacer 0 → null + Stretch; `LayoutEngine` unknown‑token rule; ceil‑per‑child sums | `UIMenu.cs`, `Stack.cs`, `Grid.cs`, `ListView.cs`, `Spacer.cs`, `LayoutEngine.cs` | M–L |
| B | **1.6 chrome**: background modes + `showWithoutTransparencyIfOptionIsSet`; no double dim for child hosts; title inset when `DrawBox=false`; banner reserve in the height clamp; clip stack in `DrawHelper.WithScissor`; texture cache invalidation | `UIMenu.cs`, `MenuHost.cs`, `Draw.cs`, `UIServices.cs` | S–M |
| C | **Focus/overlay correctness**: `IsUsable` in `Validate`/`CanFocus`/`HandleActivate`; `UpdateSubscription` on open; `SetHovered(null)` on close/detach and on `OpenAsChild`; popups drawn last; preserve focus on scrollbar clicks; capture reset only for left clicks | `FocusManager.cs`, `UIMenu.cs`, `OverlayLayer.cs`, `EventRouter.cs`, `Button.cs` | S |
| D | **Dropdown**: hover‑vs‑keyboard highlight; `RestoreOwnIndex` only on vanished value; `base.HandleKey` first; Left/Right (or open‑only arrows); close cue on all close paths; content‑sized width + clipping; scroll indicator and viewport cap; `OnScroll` only on change | `Dropdown.cs` | M |
| E | **NumberInput/TextInput**: leading `-`; step vs decimals; `Clamp=false` stepping; wheel returns changed; paste as one edit; paste filtering; `ClipLeft`; validator fail‑open; mixed get/set validation in `StardewUIApi.Add*` | `NumberInput.cs`, `TextInput.cs`, `StardewUIApi.cs` | M |
| F | **ListView/ScrollView**: virtual `Clear`/`Remove`; deferred `Refresh`; partial rebuild on count change; early `ScrollTo`; viewport culling; scroll‑into‑view on focus; `ShowScrollbar` parity; style padding opt‑out for rows; invariant `ToString` | `ListView.cs`, `ScrollView.cs`, `UIContainer.cs`, `Panel.cs` | M |
| G | **Gamepad**: snap on open; B clears text focus; `overrideSnappyMenuCursorMovementBan` or richer snap targets filtered by visibility; re‑resolve snapped component after relayout; on‑screen keyboard or documented limitation | `MenuHost.cs`, `UIMenu.cs`, `ScrollView.cs` | M |
| H | **Perf**: cached/allocation‑free style resolution; tuple mute keys; Label/Button single style read; NumberInput format cache; Grid array reuse; overlay delegate caching | `UIElement.cs`, `Theme.cs`, `ConsumerContext.cs`, components | M |
| I | **Small polish**: Image hit‑test + disposed guard; adapter NaN/∞ and hover‑on‑move; Slider rounding; Panel min inset with box; Canvas negative offsets; disabled look for Label; close‑button draw order; `PlaySound` formatting | various | S |
| J | **Docs**: README corrections (list above), interface XML additions (setter semantics, `OnScroll` units, sound applicability, keyboard subtleties, split‑screen), architecture §0/§4/§12 refresh, delete or wire dead members, example: style member + divider fix + validator sample | `README.md`, `IStardewUIApi.cs` (both copies), `architecture.md`, `ModEntry.cs` | S–M |
| K | **Tests** (§14): the measure‑contract package is the ideal first target for `UIFramework.Tests` (`ITextMeasurer` fake, `UIServices` hooks already exist): fit‑content sizing, Stack distribution, Grid star consistency, Spacer, ScrollView clamps, NumberInput stepping, `ParseTracks` | new project | M |

---

## 4. Verified OK (no action needed)

- Keyboard double delivery: Enter (`\r` ignored by `HandleCommandInput`, `OnSubmit` once from `HandleKey`, falls
  through to `DefaultButton`, never closes the menu while typing), Tab (`\t` ignored, one focus move), Backspace
  (deleted only via `\b`, `Keys.Back` consumed), Escape (clears focus first, closes second), Space, the menu key `E`.
- Value pipeline in all value components: bound getter read each frame through the guard; `OnValueChanged` from one
  commit path, only after a real change, after the setter, never on external changes or validator rejection;
  `Validate` receives the prospective value; `MaxLength` on typing and paste; invariant‑culture number parsing and
  formatting; `Min`/`Max` swap handled (except `InsertMinus`); `Decimals` clamped 0..15.
- Every consumer delegate in every component goes through `Raise`/`Consumer.Invoke`; the overlay probe click (Target
  == null) never reaches a consumer; `null` `get`/`set`/`text`/`labels`/`texture` are all tolerated (own‑value mode).
- Routing: hit‑test order overlay elements → tree; ScrollView/ListView hit‑test clips children to the viewport and
  returns themselves over empty space (wheel works there); wheel sign, fall‑through at the ends, `OnScroll` units as
  documented per component; capture‑based drags; `OnElementDetached` clears focus/hover/capture/popups/default buttons
  for whole subtrees; dropdown open/close state and overlay registration stay consistent in every path except the
  hidden‑ancestor case.
- Layout: `ParseTracks` parsed once (ctor/setters), tolerant of odd input, implicit tracks for out‑of‑range cells,
  spacing in spans/offsets, exact cell tiling; invisible children take no space or spacing; explicit `Width`/`Height`
  honoured everywhere; scrollbar width reserved consistently in measure/arrange/draw/hit‑test; ListView
  virtualization cleans up rows and clamps indices; `ScrollbarGadget` geometry matches the vanilla sprites.
- Hosting: `Close()` ↔ `cleanupBeforeExit` ↔ `OnHostClosed` idempotent; child hosts closed before the parent;
  dropped hosts detected each tick; return‑to‑title / save‑loaded close all; keyboard subscriber restored on close;
  `applyMovementKey` repopulates a nulled `allClickableComponents`; both `IStardewUIApi.cs` copies identical; every
  README sample compiles against the interface; all `config.*` i18n keys present.

---

## Appendix A – game and engine facts used (decompiled SDV 1.6, `Decompiled Code/`)

| Fact | Source |
|---|---|
| Only the deepest child menu receives `update`, `performHoverAction` and input; every menu in the chain is drawn | `StardewValley/Game1.cs:4425-4429, 4501, 4505`; `DrawMenu` `:13433-13446` |
| `receiveKeyPress(k)` fires for every newly pressed key even while a keyboard subscriber is active | `Game1.cs:4576-4580` |
| Keyboard dispatcher sends `\b`/`\r`/`\t` as command input and every key as special input | `StardewValley/KeyboardDispatcher.cs:84-101, 103-125, 195-240` |
| D‑pad / stick → `receiveKeyPress(first key of moveUp/Right/Down/LeftButton)` (W/D/S/A); A → `receiveLeftClick`; X → right click; B/Start/Y → first key of `menuButton` (= `E`) | `Game1.cs:4615-4633, 4636-4672`; `StardewValley/Utility.cs:6815-6835`; `Options.cs:757-761` |
| Thumbstick moves the cursor only when `!snappyMenus \|\| overrideSnappyMenuCursorMovementBan()` | `Game1.cs:4461-4465`; `IClickableMenu.cs:742-745` |
| `activeClickableMenu` setter neither calls `cleanupBeforeExit` on the old menu nor snaps; snapping happens in menu constructors / on gamepad toggle | `Game1.cs:1632-1671, 3244-3250` |
| `applyMovementKey` repopulates `allClickableComponents` when null; `moveCursorInDirection` uses `currentlySnappedComponent` | `IClickableMenu.cs:309-316, 355-368` |
| Base `receiveKeyPress`: menu key + `readyToClose()` → `exitThisMenu`; else snappy `applyMovementKey` | `IClickableMenu.cs:751-764` |
| `exitThisMenu` → `cleanupBeforeExit` → `exitActiveMenu` / `parent.SetChildMenu(null)` | `IClickableMenu.cs:827-848` |
| Base `gameWindowSizeChanged` rescales position proportionally (framework override is right not to call it) | `IClickableMenu.cs:707-711` |
| Menu background: world replaced by `drawBackground()` only when `showMenuBackground && showWithoutTransparencyIfOptionIsSet()`; vanilla dim condition `!showMenuBackground && !showClearBackgrounds`; three‑way option | `Game1.cs:13302-13313`; `IClickableMenu.cs:694-698`; `GameMenu.cs:478`; `Options.cs:1035-1051` |
| UI is drawn into the `uiScreen` render target sized to the UI viewport (scissor in UI pixels is correct) | `Game1.cs:11322-11348` |
| Vanilla `TextBox.Selected` opens the on‑screen keyboard for controllers | `StardewValley.Menus/TextBox.cs:141-143` |
| MonoGame `SpriteBatch` (Deferred) applies Blend/Sampler/Rasterizer state in `Setup()`, called from `End()`; `Begin` only stores them | MonoGame `SpriteBatch.cs` (`Begin`/`End`/`Setup`) — confirm in‑game with the nested ScrollView repro |
