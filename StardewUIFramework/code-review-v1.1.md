# UI Framework – v1.1 code review and polish backlog

Branch `UIFramework-ProfitCalculatorPort` at `7b5ef23` ("Profit Calc Port"), reviewed 2026‑09‑20 against
`architecture.md`. Read‑only: nothing in the code was changed; this file is the only output. It supersedes
`code-review.md` (written at `29733da`, ~8 200 lines) — Appendix A carries the status of every item from that review,
so the old file can be deleted once this one is adopted.

**Scope and method.** The tree is now ~19 200 lines in `StardewUIFramework/` plus the example mod, the Profit
Calculator port and `StardewUIFramework.Tests`. The work was split five ways and every claim below was re‑checked
against the current tree before being accepted: (1) the status of every finding in `code-review.md`, verified through
`git diff 29733da..HEAD` and the current code; (2) new `Core/` code (signals, bindings, auto‑forms, sealing,
accessibility, perf counters, exporter, tooltips, HUD model); (3) new components (DataGrid, Composite,
CustomHostAdapter/Bridge, Slot); (4) new `Hosting/` + `Rendering/` code (extension/composite registries, screen
context, HUD service, toasts, player layout, inspector, debug console, rich text, tooltip renderer, themes,
pseudo‑loc, `ModEntry`); (5) the public API file, its three copies, both consumers and documentation drift.
Game‑dependent claims were verified against the decompiled SDV 1.6 source (`D:\SteamLibrary\...\Decompiled Code`)
and Pintail claims against the shipped `smapi-internal/Pintail.dll` (2.9.1). Nothing was built or tested.

Severity: **BUG** = wrong behaviour a user will hit · **LIKELY‑BUG** = wrong under a plausible scenario (given) ·
**POLISH** = works but should improve · **DOC** = docs/code disagree · **NIT** = style/tiny. Effort: S (< 1 h),
M (half a day), L (a day or more). Line numbers are from the current tree; paths are relative to
`StardewUIFramework/` unless another project is named.

---

## 1. Summary – what to fix first

The v1.1 features are well‑structured and follow the architecture (every consumer delegate is guarded, the signal
scheduler and cycle detection are correct, the data grid's index model is consistent, the slot ordering/veto logic
matches §16.1, the clip stack fixed the nested‑scissor problem, HUD drawing uses the right batch state). The problems
are of four kinds:

1. **The v1.0 bug list was not worked through.** Of the twelve headline items in `code-review.md`, one is fixed
   (nested scissor), two are partial (Stack measure, banner reserve), nine are open (Appendix A). The measure‑contract
   bug now has a *different* symptom: a fill‑what‑you‑get child in a Stack zeroes out its later siblings instead of
   pushing them outside the box.
2. **One advertised feature is dead**: Stardew Access announcements. `UIServices.Announcer` is never assigned (the
   integration file was deleted in `87ff9f7`), so every `Announce*` path is a no‑op while README, NEXUS and
   architecture §0/§11 promise it.
3. **Lifecycle leaks around "rebuilt on every open"**: contributor signal bindings, screen‑context subscriptions and
   HUD bindings are never dropped; re‑binding an element undoes the new binding; a muted contribution stays muted for
   the session.
4. **Sealing is enforced only in the facade**: `IUIContainer.Add/Remove/Clear`, `GetChild`, `Parent` and a
   post‑callback `Find` all bypass it.

| # | Item | Sev | Where | Effort |
|---|---|---|---|---|
| 1 | Stardew Access integration not wired: `UIServices.Announcer` never assigned; `Integrations/IStardewAccessApi.cs` does not exist; all announcements + `IStardewUIApi.Announce` are no‑ops | BUG | `Core/UIServices.cs:99`, `Core/Accessibility.cs:19`, `ModEntry.cs:130-191` | S |
| 2 | Re‑binding an element disposes the *old* binding **after** the new one installed itself: labels freeze on the old text, inputs end up unbound, later `Unbind` re‑binds to the first signal | BUG | `Core/SignalBindings.cs:22-35`, `Api/StardewUIApi.cs:473-526` | S |
| 3 | `Sealed` bypassed by `IUIContainer.Add/Remove/Clear`, `GetChild`, `Parent` walks, `IUIComposite.Rebuild`; `IUIMenu.Find` filters sealed elements only *during* the synchronous decorator call | BUG | `Core/UIContainer.cs:20-23,66`, `Core/UIElement.cs:60`, `Core/UIMenu.cs:283`, `Hosting/ExtensionRegistry.cs:215-227` | M |
| 4 | Fit‑content menus: root still measured with finite viewport width (star Grid / ListView / DataGrid become screen‑wide); Stack now clamps, so a `rows="*"` Grid or ScrollView that is not last gives every later sibling **0 extent** (silently invisible) | BUG | `Core/UIMenu.cs:338-341`, `Components/Stack.cs:92-97,131-136`, `Components/Grid.cs:251-252`, `Components/ListView.cs:294-295`, `Components/DataGrid.cs:543` | M |
| 5 | `Composite` re‑implements the *pre‑fix* Stack layout (whole‑available measure, no cut): a star Grid/ScrollView/ListView inside a composite pushes later children outside its bounds | LIKELY‑BUG | `Components/Composite.cs:166-199` (cf. `Slot.cs:191` which derives from `Stack`) | M |
| 6 | Contributor signal bindings and HUD bindings never dropped: `OnElementDetached` looks in the **menu owner's** table, contributions rebuild every open; `DestroyHud` drops nothing | LIKELY‑BUG | `Core/UIMenu.cs:313`, `Hosting/MenuRegistry.cs:42`, `Hosting/HudService.cs:76-92`, `Hosting/ExtensionRegistry.cs:181` | S |
| 7 | Screen‑context subscriptions accumulate one handler per open and fire stale handlers (README `:477-484` shows the leaking pattern) | LIKELY‑BUG | `Hosting/ScreenExposures.cs:21,141-149,193-207` | S |
| 8 | `DestroyMenu` / same‑id `CreateMenu` leave the toggle hotkey pointing at the dead `UIMenu`; `UIMenu.Open` never checks registration | LIKELY‑BUG | `Api/StardewUIApi.cs:250-260`, `Hosting/MenuRegistry.cs:37-44`, `Core/UIMenu.cs:554-566` | S |
| 9 | `AddForm` accepts a `struct` model and edits the boxed copy: every edit silently lost, `IsDirty` true, `Save` "succeeds" | LIKELY‑BUG | `Api/StardewUIApi.cs:530-535`, `Core/AutoFormField.cs:71` | S |
| 10 | HUD service iterates `huds.Values` while consumer `OnUpdate`/draw callbacks may `Create/DestroyHud` → `InvalidOperationException` escapes to SMAPI every tick | LIKELY‑BUG | `Hosting/HudService.cs:97,123,150` | S |
| 11 | A plain click (no drag) on a menu's title strip converts an anchored menu to explicit pixels and persists it per save; window no longer re‑centres on resize / UI scale | LIKELY‑BUG | `Hosting/PlayerLayoutController.cs:149-157,179-188`, `Hosting/WindowLayoutStore.cs:150-159` | S |
| 12 | DataGrid: `EnsureFresh` ignores count changes (stale `order`/`position` for `ScrollToRow`/keyboard/`SelectedRows` in the same callback); column resize has no upper clamp and rows are never scissored (columns overflow under the scrollbar / outside the box) | LIKELY‑BUG ×2 | `Components/DataGrid.cs:399-405,543,562`, `Components/DataGrid.Input.cs:444`, `Core/LayoutEngine.cs:137-140` | S + M |
| 13 | Muted contributions stay muted for the session even after `ContributeTo` is called again (`ResetMutes` has no caller) | LIKELY‑BUG | `Hosting/ExtensionRegistry.cs:48-58,211`, `Core/ConsumerContext.cs:45-49,108` | S |
| 14 | Computed subscribers fire only once while the computed is never read (`dirty` short‑circuits `Enqueue`) | LIKELY‑BUG | `Core/Signals.cs:371-380` | S |
| 15 | New per‑frame costs on top of the still‑open style allocations: `UIElement.Consumer` walks to the root on every `Raise`; `DrawHelper.FitText` does 3–4 `MeasureString` per label/button/checkbox/dropdown per frame; rich tooltips rebuild every row per frame | POLISH | `Core/UIElement.cs:52`, `Core/Sealing.cs:19-29`, `Rendering/Draw.cs:136-165`, `Rendering/TooltipRenderer.cs:47-73` | M |
| 16 | Architecture §0/§13 phase 7 describe a port (`main/ui/framework/`, `UseUIFramework` flag, legacy fallback, slots + `recalculate`) that does not exist; §16 "everything implemented" is wrong for the PC slots, the two guides, `IUICustomComponent.Build` and the Testing package; §4/§12 layout still stale from the prior review | DOC | `architecture.md:28,130-142,394-415,454-460,494,542` | M |

---

## 2. Findings by area

### 2.1 Layout and the measure contract (carried over, partly regressed)

- **BUG (high)** — Root measured with a finite width. `Core/UIMenu.cs:338-341` measures `Viewport` with
  `vp.X − insets`; `Grid.cs:251-252` resolves stars against `available`, `ListView.cs:294-295` and
  `DataGrid.cs:543` claim `Math.Max(available.X, …)`. Any menu without `Width` that contains the README's `"auto,*"`
  form grid, a ListView or a DataGrid is viewport‑wide. Both consumers mask it (`UIFrameworkExample/ModEntry.cs:69`
  `Width = 720`; `ProfitCalculator/main/ui/ProfitCalculatorResultsMenu.cs:55` `Width = 900`). *Fix:* measure
  fit‑content axes with `float.PositiveInfinity`; `ResolveTracks`, `ListView` and `DataGrid` already fall back to
  content size for an infinite available.
- **BUG (medium, new symptom)** — `Components/Stack.cs:92-97,131-136`: with the new remaining‑space measure a
  fill‑what‑you‑get child (`rows="*"` Grid, ScrollView, `Stretch` custom component) that is *not last* consumes the
  whole remaining main axis and every later sibling is arranged with `extent = 0`: not drawn, not hit‑testable, no
  warning. Scenario: `Grid(rows="*,auto")` followed by a button Stack in `Root` → the OK/Cancel row vanishes (before
  `87ff9f7` it was drawn outside the box). Needs real distribution (measure star children last / share leftover),
  not just clamping. Main‑axis `Stretch`/`Center`/`End` remain no‑ops.
- **LIKELY‑BUG (medium)** — `Components/Composite.cs:166-199` is a copy of the pre‑fix Stack (`:166-182` whole
  `available` per child, `:184-199` no subtraction, no cut). Scenario: composite builder adds
  `AddGrid(host, …, rows: "*,auto")` then a button row → the button is arranged below `Bounds.Bottom`, not clickable.
  `Slot` derives from `Stack` (`Slot.cs:191`); do the same and delete the duplicate.
- **Still open from the prior review** (see Appendix A §2.1): Grid star measure/arrange inconsistency
  (`Grid.cs:251-252` vs `:360-361`); auto/star cell children never re‑measured (`:302-308`); `DistributeSpan` inflates
  auto tracks (`:194-215`); Spacer `0` stored verbatim so the example's divider is invisible (`Spacer.cs:14-18`,
  `UIFrameworkExample/ModEntry.cs:76-77,97-98`); `LayoutEngine.cs:50` doc "auto" vs `:79-82` `0px` for unknown tokens
  (also the documented behaviour of `IUIDataGridColumn.Width`); Panel `padding == 0` means unset (`Panel.cs:45`);
  float sums vs ceil‑per‑child (`Stack.cs:99-107` vs `:132`, `ScrollView.cs:216` vs `:234`); `foreach` over
  `Children` while consumer delegates run inside Measure/Arrange (`Stack.cs:83,122`, `Grid.cs:259,365`,
  `ScrollView.cs:206,227`, now also `CustomHostAdapter.cs:49-54`, `Composite.cs:169`, `DataGridRow.cs:84,95`);
  `UIElement.LayoutDirty` write‑only (`UIElement.cs:343-351`).
- **POLISH (low)** — `Components/Slot.cs:244-258` `MaxHeight` caps the measure but does not clip; overflowing
  contributions are cut by the Stack end‑clamp with no scrolling. Scissor or document "wrap in a ScrollView".
- **POLISH (low)** — `DataGrid.cs:611-618` consumer‑added non‑row children are arranged over `RowsRect` but never
  measured (`:538-541` only measures `rows`); they work only because of the `Stretch` default.

### 2.2 Rendering and window chrome

- **FIXED and verified** — nested `WithScissor` now keeps its own clip stack (`Rendering/Draw.cs:210-249`): push before
  `draw()`, pop in `finally`, inner rect intersected with `Peek()`, outer batch re‑begun with `ScissorState` when
  nested. The `drawDialogueBox(X, Y−64, W, H+64)` offset with `BoxInsetTop = 56` (`Core/UIMenu.cs:492`) matches the
  decompiled `Game1.drawDialogueBox` (`drawOnlyBox` frame spans `[y+64, y+height]`, `ignoreTitleSafe` defaults true).
  The banner reserve is fixed (`UIMenu.cs:338-346` caps `maxH = vp.Y − TitleReserve`, so `minY == TitleReserve`).
- **BUG (high, still open)** — 1.6 three‑way menu background option: `Core/UIMenu.cs:442` dims only when
  `!showMenuBackground`; no `showWithoutTransparencyIfOptionIsSet` override on `MenuHost`. "Graphical" shows the
  un‑dimmed world, "None" still dims. README `:258` documents the wrong assumption. Fix as in the prior review
  (`MenuHost.showWithoutTransparencyIfOptionIsSet() => Menu.DimBackground`; dim when
  `DimBackground && !showMenuBackground && !showClearBackgrounds`); child hosts still double‑dim.
- **LIKELY‑BUG (medium, still open)** — Title with `DrawBox = false`: `UIMenu.cs:326` `InsetTop` is just `padding`,
  title drawn at `Bounds.Y + 8` (`:462`) over the first row; `:338` reserves the banner only when `drawBox`, so now
  the fit‑content `Viewport` scrolls *under* the title.
- **LIKELY‑BUG (low, verify in‑game)** — `Rendering/PseudoLocalizer.cs:285,293` emits `ý`/`Ý`. MonoGame `DrawString`
  throws for a glyph absent from a font with no `DefaultCharacter` (the game never sets one); no supported SDV
  language uses `ý`, and the throw would be in `Label.DrawCore` (framework code, not a muted callback). Map `y → ÿ`
  (Latin‑1, French) or confirm the glyph table in game.
- **POLISH (medium)** — Texture cache: `Core/UIServices.cs:101-105` still caches `LooseSprites\textBox` in a static
  forever (`ModEntry.cs:76-77` only invalidates the theme asset), so Content Patcher retextures apply to the first
  load only; `SmallTextBoxTexture` (`:108`) and `assets/text_box_small.png` still unused.
- **POLISH (low)** — `Hosting/MenuHost.cs:65-75` draws the close button after `Menu.Draw` (over tooltips/overlays);
  `Core/OverlayLayer.cs:71-87` draws popups *before* `OnDrawOverlay`/`WantsOverlay` frame draws while input goes to
  the popup first; Label ignores `Enabled` (`Label.cs:159-171`) while Button/Checkbox/inputs now use the theme's
  `DisabledTextColor`; `ScrollView.cs:252` closure per frame; `UIServices.cs:60-64` `PlaySound` brace formatting.
- **POLISH (low)** — `Core/UIMenu.cs:547` plain tooltips go through vanilla `drawHoverText` (unscaled) while rich
  tooltips use the scaled measurer → two text sizes in one menu at `TextScale ≠ 1`.
- **NIT** — `Rendering/Draw.cs:109` scissor in render‑target pixels is correct because menus/HUD draw inside
  `PushUIMode` (verified `Game1.cs:13348-13368`, `:13435-13450`); the 75/100/150 % UI‑scale check in §14 is still
  pending.

### 2.3 Input routing, focus, keyboard and gamepad (all still open)

- **LIKELY‑BUG (medium)** — `Core/FocusManager.cs:129-135` `Validate` and `:182-186` `CanFocus` check only the
  element's own `Visible`/`Enabled`; `IsUsable` (`:155-165`) checks ancestors but is used only by traversal. A
  TextInput in a collapsed panel keeps the keyboard subscriber; an open Dropdown under a hidden ancestor keeps
  drawing/eating clicks; `Button.HandleActivate` (`Components/Button.cs:228-233`) fires a `DefaultButton` inside a
  hidden panel on Enter.
- **LIKELY‑BUG (low, new)** — `Core/FocusManager.cs:68,76-85` `ScrollIntoView` runs on every `SetFocus` with
  bounds from the *last* layout: nested ScrollViews scroll the outer with the element's pre‑scroll bounds; `SetOffset`
  raises the consumer's `OnScroll` from inside a focus change (re‑entrancy).
- **POLISH** — `UIMenu.cs:590-607` `AfterOpened` never calls `Focus.UpdateSubscription()`; `EventRouter.cs:55-65`
  `FocusFromClick` clears focus on scrollbar/arrow clicks (and now on the hit‑test‑visible `Viewport`, `ScrollView.cs:279`);
  `EventRouter.cs:31` resets `Captured` on any button; `UIMenu.cs:294,637` set `Hovered = null` directly so
  `OnHoverEnd`/`IUICustomComponent.OnHover(false)` never fire on close/detach (`Router.SetHovered` exists at `:110`).
- **POLISH (high) — gamepad, architecture goal §1.7, unchanged**: no snap on open (`UIMenu.cs:590-607`;
  `snapToDefaultClickableComponent` exists at `MenuHost.cs:268-280` but nothing calls it); B (= `Keys.E`) swallowed
  while a text input is focused (`MenuHost.cs:198-201`; `receiveGamePadButton` is base‑only `:234-237`); only
  focusable elements are snap targets and no `overrideSnappyMenuCursorMovementBan` (`:240-266`; list/grid rows, scroll
  arrows, clickable images unreachable); `currentlySnappedComponent` stale after relayout (`:31-41,286-291`); no
  on‑screen keyboard (no `showTextEntry` anywhere). **New**: a `Collapsed` HUD window still lists all its focusable
  elements as snap targets (`MenuHost.cs:240-266` + `UIMenu.cs:203-218`).
- **DOC** — keyboard subtleties (typing keys vs `OnKey`, `Value` setters bypassing `MaxLength`/`Validate`/
  `OnValueChanged`) still undocumented in the interface.

### 2.4 Overlay, Dropdown, TextInput, NumberInput, ListView, ScrollView (all still open — see Appendix A)

Unchanged since the prior review except where noted; the fixes proposed there still apply:

- **Dropdown** `Components/Dropdown.cs`: hover overwrites keyboard highlight every tick (`:469-476`);
  `RestoreOwnIndex("")` → index 0 on every `Open()` (`:143-173,243`); built‑in navigation before `base.HandleKey`,
  closed Up/Down commit and swallow (`:386-425`); close cue only from `Close()`; fixed 300 px; no scroll indicator;
  `OnScroll` on every notch; `Open()` before the menu shows; ctor `RefreshChoices()` while detached. Closed‑box label
  overflow is now clipped via `FitText` (`:318-319`); open‑list rows still use plain `Text` (`:348`).
- **NumberInput** `Components/NumberInput.cs`: `-` rejected on a freshly focused `"0"` (`:331-339`; `Min=-10`, type
  `-5` → **5**); `Step` finer than `Decimals` never moves (`:134-142,258-265`); `StepBy` clamps with `Clamp=false`;
  `HandleScroll` returns `true` when nothing changed (`:499-508`, stalls an enclosing ScrollView); paste commits per
  character (`:428-439`); validator fail‑closed after a mute (`:202`).
- **TextInput** `Components/TextInput.cs`: `ClipLeft` O(n²) per frame (`:102-110`); paste unfiltered (`:374-392`);
  validator fail‑closed (`:279`).
- **Mixed getter/setter pairs** (`Api/StardewUIApi.cs:144-178`, no pairing check): a getter without a setter makes
  every edit look like a change and raise `OnValueChanged`; the new `Rebind` paths (`TextInput.cs:188` etc.) can also
  produce a mixed pair from a signal binding.
- **ListView / ScrollView**: `Clear()`/`Remove()` detach row panels permanently (`Core/UIContainer.cs:54,66`
  non‑virtual; `ListView.cs:150-168`) — **the same hole now bricks `DataGrid`** (`DataGrid.cs:41,381-396`);
  synchronous `Refresh()` from a row child's `OnClick` drops the row click (`ListView.cs:170`; same in
  `DataGrid.cs:267-280`, `:425`); full rebuild on any count change (`ListView.cs:323-329`; `DataGrid.cs:274-277`,
  also on every column content setter `DataGridColumn.cs:180-240`); early `ScrollTo` lost (`ScrollView.cs:98-100,
  122-124`); no viewport culling (`:247-253`); thumb drag has no grab offset (`ScrollView.cs:158-170`,
  `ScrollbarGadget.cs:77-86`, also `DataGrid.Input.cs:351-390`) — fix once in the gadget; scrollbar column always
  reserved (`ListView.cs:282`, `DataGrid.cs:515`); culture‑sensitive `int.ToString()` (`ListView.cs:122,130,264`,
  `DataGrid.Input.cs:177`). Scroll‑into‑view on focus is **fixed** (`FocusManager.cs:68`, `ScrollView.ScrollIntoView:142-157`).
- **Image / adapters**: `Image.cs:14` no `IsHitTestVisible` override; no `IsDisposed` guard (`:85-106`); NaN/∞ from a
  custom `Measure` passes into layout (`Components/CustomComponentBridge.cs:33-37`, `Math.Max(0, NaN)` is NaN);
  `OnHover` forwarded every tick while the cursor rests (`:154-158`); `WantsFocus`/`WantsOverlay` are proxied calls
  with a closure each read (`:132-134`; `DrawsInOverlay` is read every frame in `UIElement.Draw:442`).

### 2.5 Signals, bindings and auto‑forms (new)

- **BUG (high)** — Re‑binding. `Api/StardewUIApi.cs:476` is `consumer.Bindings.Add(target, TextKind, new TextBinding(…))`:
  the constructor (`Core/SignalBindings.cs:83-91`) installs `label.TextFunc = () => cached` and subscribes; then `Add`
  (`:29-32`) disposes the *previous* binding, whose `Dispose` (`:111-116`) sets `label.TextFunc = () => frozen`.
  Scenario: `BindTextToSignal(lbl, a); BindTextToSignal(lbl, b); b.Value = "x"` → label shows `a`'s text forever.
  Value bindings (`:495-523`): binding 2 captures binding 1's `read`/`write` as "originals", `Add` disposes binding 1
  → `rebind(consumerGetter, consumerSetter)` → bound to **neither**; a later `Unbind` restores binding 1's delegates
  → bound to signal 1 *after* Unbind. *Fix:* dispose the previous binding before constructing the new one (factory
  argument), or remove‑then‑add in the facade.
- **LIKELY‑BUG (medium)** — Bindings leak for contributors (`Core/UIMenu.cs:313` drops from the owner's table; the
  contributor's `BindText*` stored them in *its* `ConsumerContext.Bindings`) and for HUDs (`Hosting/HudService.cs:76-92`
  never calls `DropMenu`; the HUD inner `UIMenu` is never registered). Every open of a slot‑bearing menu / every
  `CreateHud` replace leaks one live binding per bound element. *Fix:* `element.Consumer.Bindings.Drop(element)` (the
  contributor context via `Sealing.ContributorOf`) and `hud.Consumer.Bindings.DropMenu(hud.Inner)` in `Destroy`.
- **LIKELY‑BUG (medium)** — `Core/Signals.cs:371-380` `Computed.Invalidate` returns early when already `dirty`; only
  a read clears `dirty`. `c.Subscribe(() => ShowToast(…)); a.Number = 1; a.Number = 2; a.Number = 3` → one toast.
  Either document ("until it is read again") or bump `Version`/`Enqueue` on every upstream change.
- **LIKELY‑BUG (medium)** — Struct form models: `Api/StardewUIApi.cs:530-535` only null‑checks; `FormField.Write`
  (`Core/AutoFormField.cs:71`) writes the boxed copy. Throw `ArgumentException` for `IsValueType`.
- **LIKELY‑BUG (low)** — `Core/AutoFormReflection.cs:160-170,289-314` `StringOf(display, "DisplayName", "Name")` falls
  back to the *first public string property* of the attribute: `[Display(Description = "Help")]` (no `Name`) becomes
  the caption **and** the tooltip. No fallback loop for `Display`.
- **POLISH (medium)** — `Core/AutoFormField.cs:69-71,141-165` every generated control reads the model through
  `PropertyInfo.GetValue` inside a fresh closure + boxing once per field per frame; `Read()` also concatenates the
  mute key. Compile a typed getter once, or cache and re‑read only in `Refresh`/after `Commit`.
- **POLISH (low)** — `Core/Signals.cs:175-180` all `Subscribe` handlers of one reactive share the mute key
  `"<id>|Subscribe"` (one throwing handler mutes all); `:400-408` a `compute` that throws once is muted forever and
  returns the stale value; `:162` `Changed()` copies `dependents` into a new list on every write (per keystroke).
- **DOC** — `Unbind` "restoring the original value delegates" (`IStardewUIApi.cs:975`): `TextBinding.Dispose` freezes
  the last text, does not restore the delegate (`SignalBindings.cs:111-116`); `Cancel` also clears undo/redo and
  raises `OnChanged` before `OnCancelled` (`Core/AutoForm.cs:207-218` vs `IStardewUIApi.cs:1441`); "numbers → number
  input" (`:979-987`) covers only `int/uint/long/float/double/decimal` — `short/byte/ulong/sbyte/ushort` and every
  `Nullable<>` are skipped with a Debug log (`AutoFormReflection.cs:69-70`); `init`‑only setters are treated as
  writable (`:99`); `GetCustomAttributes(inherit: true)` misses attributes on overridden base properties (`:274-286`);
  `new`‑hidden properties collide by name (`:94-111`); `OverflowException` from `Convert.ChangeType` is rejected
  silently with no error label (`AutoForm.cs:227-241`); `FromText("NaN")` parses and makes `Flag` true (`Signals.cs:250-251`).

### 2.6 Extension slots, screen context, sealing, composites (new)

- **BUG (medium)** — Sealing enforcement lives only in `Api/StardewUIApi.cs:208-213` (`Remove`) and `:621-628`
  (`Attach`). `IUIContainer.Add/Remove` (`Core/UIContainer.cs:22-23`), `Clear` (`:66`), `GetChild` (`:20`),
  `IUIElement.Parent` (`Core/UIElement.cs:60`) and `IUIComposite.Rebuild` (`Components/Composite.cs:66`) have no
  `Sealing.RequireWriteAccess`. Scenario: decorator walks `menu.Root.GetChild(i)` to the sealed panel and calls
  `((IUIContainer)menu.Root).Remove(panel)` — succeeds, contradicting `IStardewUIApi.cs:294-302`. Also
  `Core/UIMenu.cs:283` `Find` filters by `ExternalConsumer`, which `RunExternal` (`Hosting/ExtensionRegistry.cs:215-227`)
  sets only for the synchronous call; a decorator that keeps the `IUIMenu` and calls `Find` from its own button
  handler gets the unfiltered tree. *Fix:* route interface‑level tree edits through an ambient caller (the
  `ExternalConsumer` mechanism, or per‑consumer proxies), or document sealing as advisory outside the callbacks.
- **LIKELY‑BUG (medium)** — `Hosting/ScreenExposures.cs:193-201` `Subscribe` appends; nothing clears on close /
  slot rebuild / `DestroyMenu` (table kept per (owner, menu id), `ExtensionRegistry.cs:91-100`). A contribution that
  subscribes in its build (the documented pattern, `IStardewUIApi.cs:1094`, README `:483`) registers a new lambda
  each open; `Publish` runs N handlers, N−1 mutating elements from earlier opens, all under the same guard key
  (`:121`) so one stale throwing handler mutes the live one. *Fix:* tag subscriptions with the contribution instance
  and clear them in `RebuildSlot`.
- **LIKELY‑BUG (low)** — Muted contributions: `ExtensionRegistry.Contribute` (`:48-58`) replaces the record but the
  mute key (`"<owner>.<menu>.<slot>|Contribute"`, `:211`) is never cleared. A build that throws once (e.g. exposure
  empty before `SaveLoaded`) is skipped for the session; re‑registering does not help. Reset the mute on re‑register.
- **LIKELY‑BUG (low)** — `Components/Composite.cs:41` sets `ComponentOwner` (write access) but never `Contributor`,
  so elements the definer's builder creates raise callbacks through `UIElement.Consumer` = the **menu owner**
  (`UIElement.cs:52`): a throwing `OnValueChanged` on the composite's `NumberInput` is logged as the owner, muted in
  the owner's table, styled with the owner's `DefaultStyle`, and mute keys `Id|event` collide across composites of
  different definers. Set `Contributor = definition.Owner`.
- **POLISH (low)** — Contributions run *before* the owner's `OnOpen` (`Core/UIMenu.cs:590-606`: `NotifyOpening` →
  `Relayout` → `OnOpen`), so owners that `Expose*` fresh values in `OnOpen` hand contributors stale data on the very
  open they were built for. Document or reorder.
- **POLISH (low)** — `CustomHostAdapter.cs:44` + `OverlayLayer.cs:98-108`: an overlay host draws embedded children
  in the overlay pass, but `ElementAt` returns the element only when `e.HitTest(x, y) == e`; over an embedded child
  the lookup yields null and a tree element under the popup wins the click. Return the non‑null hit.
- **DOC** — `IUIScreenContext.GetString` returns `""` for a false bool (`ScreenExposures.cs:147`) while signals
  return `"False"` (`Signals.cs:262`) and the doc says bools "are converted"; `ListSlots` returns only slots of menus
  already registered (`ExtensionRegistry.cs:108-113`) whereas `ContributeTo` is kept for menus that do not exist yet —
  say so; `IUIComposite` has `Subscribe` but no `Unsubscribe`, and `Rebuild` keeps subscribers (`Composite.cs:66-73`);
  `IUISlot.VetoedContributors`/`MaxContributions` take effect at the next open only (`Slot.cs:214-221`); veto match is
  ordinal case‑sensitive (`:48`); `architecture.md:532` says the adapter forwards `Measure` when the custom component
  returns `null` — `Measure` returns `Vector2`, the real rule is "the larger of both" (`CustomHostAdapter.cs:46-56`);
  `CustomHostAdapter.cs:78-85,95-102` a handled click/key still runs the element's own `OnClick`/`OnKey` while
  `IStardewUIApi.cs:600-601` says "stops bubbling" (true) — clarify it does not stop the element's callbacks.

### 2.7 DataGrid (new)

- **LIKELY‑BUG (medium)** — `Components/DataGrid.cs:399-405` `EnsureFresh` only tests `needsRefresh`; a count change
  is detected only in `Update` (`:631`). `ScrollToRow` (`:503`), `SetFirstVisible` (`:457`), `MoveSelectionTo`
  (`DataGrid.Input.cs:533`) run against the previous tick's `order`/`position`. Scenario: a handler does
  `items.Add(x); grid.SelectedRow = items.Count − 1; grid.ScrollToRow(items.Count − 1)` → `position.Length` is the old
  count, `ScrollToRow` is a no‑op. The Profit Calculator avoids it by calling `Refresh()` by hand
  (`ProfitCalculatorResultsMenu.cs:83-84`). *Fix:* `if (needsRefresh || SourceCount != lastCount) Refresh();`.
- **LIKELY‑BUG (medium)** — No horizontal clamp/clip. `DataGrid.Input.cs:444` clamps drag width only from below;
  `DataGrid.cs:562` raises resolved widths to `MinWidth` *after* `ResolveTracks` distributed the space;
  `LayoutEngine.cs:137-140` makes star columns fall back to content width when `remaining <= 0`. `total` exceeds
  `contentWidth`, cells are placed beyond `Bounds.Right` (`:592-598`), never scissored (`:639-654`), the scrollbar
  (`:621`) draws over the last cells and the divider hit zones (`DataGrid.Input.cs:265-275`) end up outside the
  element. Scenario: README `:668-669` grid, drag the first divider to the right edge. *Fix:* clamp
  `resizeWidth <= resizeStartWidth + (contentWidth − total)` and scissor rows to `RowsRect`.
- **LIKELY‑BUG (low)** — `DataGrid.Input.cs:75-92` `SelectedRows` setter before the first refresh: `order` is empty
  so `primaryRow == -1`, Enter does nothing, first `Down` selects row 0. Call `EnsureFresh()` first.
- **LIKELY‑BUG (low)** — `Clear()`/`Remove()` brick the grid; synchronous `Refresh()` from a cell child's click drops
  the row click (see §2.4 — same holes as ListView).
- **POLISH (medium)** — `DataGrid.Input.cs:441-446` each drag tick → `SetPixelWidth` → `SetWidth`
  (`DataGridColumn.cs:252-263`): string format + `ParseTracks` + full‑tree relayout (with `AutoWidth` measuring every
  visible cell, `DataGrid.cs:579-585`). Keep an `int` during the drag, set the track on release. `SetFirstVisible`
  (`:475`) relayouts the whole menu per scroll step.
- **POLISH (medium)** — `DataGrid.Input.cs:488-521` while focused and `Selectable`, Up/Down/PageUp/PageDown/Home/End
  are always consumed (even with `order.Length == 0`, `:519`), so arrow‑key traversal can never leave the grid.
  Release at the first/last row.
- **POLISH (low)** — resizing the last column (or when no star column remains) converts it to pixels and leaves a
  gap (`DataGridColumn.cs:266-269`); `AutoWidth` measures only visible cells so auto columns jitter while scrolling
  (`DataGrid.cs:569-587`); `ToggleRow` can leave `SelectedRow == -1` with `SelectedRows.Length > 0` when the remaining
  selection is filtered out (`DataGrid.Input.cs:188-198`); filter/sort keys re‑evaluated with a closure per row on
  every refresh (`DataGrid.cs:283-300`); `ClickRow` focuses the grid on right clicks (`DataGrid.Input.cs:393-398`);
  `resizingColumn` not reset if `columns` shrinks mid‑drag (`:435-469`, throws outside any guard);
  `SelectedRow = 99999` stored unchecked until the next prune (`:59-72`); `AddColumn` with an existing id appends at
  the end while the log says "replaced" (`DataGrid.cs:177-196`); `Math.Round` per column drifts the last edge by a
  pixel or two (`:562`); `Sortable`/`Header` setters do not `InvalidateLayout` (`DataGridColumn.cs:152,160-162`).
- **DOC** — cell children: Button/Checkbox/Slider return the consumer's `Handled` (false by default), so a click on
  a checkbox in a cell also selects the row and fires `OnRowClick`; say "set `e.Handled = true`" in
  `IUIDataGridColumn.BuildCell` and README `:655-656`. `Filter` takes effect on the next tick or `Refresh()`
  (`IStardewUIApi.cs:1331-1332`); `SelectedRows` "in display order" appends filtered‑out rows (`DataGrid.Input.cs:123-130`);
  `SortKey` compares `CurrentCultureIgnoreCase` (`DataGrid.cs:368`); the grid fills the offered width — never stated.
- **POLISH (low)** — `Api/StardewUIApi.cs:628-631` `Attach` walks the whole tree per `Add*` for the duplicate‑id log;
  `BuildCell` adds several elements per cell on every scroll step (~30 tree walks per notch in the PC port).

### 2.8 HUD, toasts and player‑owned layout (new)

- **LIKELY‑BUG (medium)** — `Hosting/HudService.cs:97,123,150` iterate `huds.Values` while invoking consumer
  `OnUpdate`/draw; `CreateHud`/`DestroyHud` (`:69,78`) mutate the dictionary. The consumer callback is guarded but the
  `InvalidOperationException` is thrown by the framework's `foreach` afterwards and escapes to SMAPI every tick.
  Scenario: `hud.OnUpdate = (h, ms) => { if (done) api.DestroyHud(h.Id); }`. Iterate a snapshot
  (`InteractiveHuds()` at `:185` already does).
- **LIKELY‑BUG (medium)** — `Hosting/PlayerLayoutController.cs:149-157` `Begin` calls `menu.SetPosition(origin)`
  immediately (→ `anchor = Explicit`, `Core/UIMenu.cs:265-271`); `Released` (`:179-188`) `Remember`s regardless of
  movement (`WindowLayoutStore.cs:150-159`). A mis‑click on the title banner of a `Center` menu pins it to pixels per
  save; at another resolution it opens wherever `ResolvePosition` clamps it. Defer until the cursor moved; store the
  offset relative to the anchor.
- **LIKELY‑BUG (low, verify in‑game)** — Split screen: `huds` is process‑wide while `UpdateTicked`/`RenderedHud` are
  per screen; `OnUpdate` runs once per screen per tick (timer widgets run 2× fast) and `Inner.Hovered`/`Focus`/`Overlay`
  are single‑instance while `ScreenState` is `PerScreen` (`HudService.cs:22-36,116-144`, `Core/UIHud.cs:45`).
  `Destroy` cleans only the current screen's `ScreenState` (`:81-90`). Also (prior review) a `UIMenu` has one `Host`,
  so a menu can be open on one screen at a time — still undocumented.
- **LIKELY‑BUG (low)** — Theme/`TextScale` changes do not re‑layout HUD widgets: `ThemeSwitcher.Refresh`
  (`Rendering/ThemeSwitcher.cs:48-60`) invalidates `Menus.OpenMenus` only; HUD inner menus are never registered.
- **LIKELY‑BUG (low, verified)** — Toasts stack from `(16, uiViewport.Height − 32)` (`Hosting/ToastLayer.cs:23-24,
  111-116`); vanilla `HUDMessage.draw` uses the same corner and stacking (`HUDMessage.cs:146-153`) and `RenderedHud`
  fires after it, so a toast paints over "picked up X".
- **POLISH** — `HudService.cs:51` `HudsActive` omits vanilla's `!freezeControls && !panMode && !HostPaused &&
  gameMode == 3` (`Game1.cs:13353`); hover is sent to every interactive widget under the cursor, not the top‑most
  (`:266-269`); `ResetPlayerLayout` is not ownership‑checked (`Api/StardewUIApi.cs:596-604`) and `WindowLayoutStore.Apply`
  captures `ConsumerLayout` once (`:126`); `ui_layout_reset` clears entries but open menus keep their dragged bounds
  and re‑`Remember` them (`ModEntry.cs:97-107`); toasts slide/fade ignoring `ReducedMotion` (`ToastLayer.cs:97-100,
  130-135`) and bypass pseudo‑loc (`:54`); `Interactive` doc says the game loses a click only when an element handled
  it, but an unhandled click on the box starts a drag and is suppressed (`HudService.cs:239-247,216`); text inputs
  cannot take keyboard focus in a HUD (`:227` clears focus after every click) — undocumented.

### 2.9 Themes, rich text, tooltips, inspector, accessibility (new)

- **BUG (high)** — Stardew Access not wired (item 1). `Core/UIServices.cs:99` declares `Announcer`; grep finds no
  assignment; `Integrations/` holds only `IGenericModConfigMenuApi.cs`; `87ff9f7` deleted `IStardewAccessApi.cs` and
  the `OnGameLaunched` wiring. Dead paths: `IStardewUIApi.Announce` (`Api/StardewUIApi.cs:425`),
  `FocusManager.cs:69`, `UIMenu.cs:600` (title), `:499-518` (resting hover), every `AnnounceValue`. README `:87-91,
  856-862, 1619, 1738`, NEXUS `:32`, architecture §0/§11 advertise it. *Fix:* re‑add the API copy,
  `GetApi<IStardewAccessApi>("shoaib.stardewaccess")`, set `UIServices.Announcer = t => api.Say(t, true)`. When it is
  wired, also skip announcing bare containers: resting the cursor on empty menu space now sets `Hovered = Viewport`
  and would speak `"ScrollView"` (`UIMenu.cs:497-516`, `UIElement.cs:306-314`).
- **LIKELY‑BUG (low)** — Rich‑text labels do not react to the pseudo‑loc toggle: `Pseudo.Enabled` is consulted inside
  `RichText.Parse` (`Rendering/RichText.cs:207`) and the layout is cached until text/wrap change; `ui_pseudoloc` and
  the GMCM toggle (`ModEntry.cs:65-69,159-164`) invalidate nothing.
- **POLISH (medium)** — `Rendering/TooltipRenderer.cs:47-73` rebuilds every row each frame (delegate evaluation,
  `RichText.Parse` + `Layout` at `:162`, a `Row` + closure per block, `"RichTooltip.Line#" + index` strings at
  `:139-144,209`). Cache rows keyed on the evaluated strings.
- **POLISH (low)** — `Rendering/Theme.cs:171-183` `Theme.Current` string‑compares the configured name on every access
  and `FontScale` (`:329`) is hit from every `Measure`/`Text`/`LineHeight` (`Core/UIServices.cs:33-41`) — cache and
  version‑stamp; `ModEntry.cs:77` `AssetReady` subscription is redundant (the only loader is `Theme.LoadThemes`, so
  `Refresh()` runs re‑entrantly inside the load and invalidates every open menu on first load);
  `Rendering/InspectorRenderer.cs:211` calls `Inspector.Subject(menu)` (a tree walk) for every element → O(n²) per
  frame, plus a closure per frame at `:196`; `Hosting/Inspector.cs:58-71` toggles on `ButtonsChanged` even while a
  TextInput or the chat box owns the keyboard.
- **NIT** — `Core/TreeExporter.cs:157-219,604` has no cases for DataGrid/AutoForm/Composite/CustomHostAdapter/Slot
  (emits a "skipped" TODO) and never emits `RichText`/`AccessibleName`/`RichTooltip`/`Sealed`/`OnLink`;
  `Double(double.NaN)` emits `NaN` (invalid C#). `Hosting/DebugConsole.cs:31` help says `Mods/UIFramework/export`;
  the folder is `<mod folder>/export` (`ModEntry.cs:111`). `ModEntry.cs:79` comment `// END THEME entrywww`.
  `UIMenu.cs:540,546` guard plain tooltips with the *owner's* context while `TooltipRenderer.Evaluate` (`:151`) uses
  `element.Consumer` — a contributor's throwing tooltip is attributed differently per path.

### 2.10 Performance and allocations (per frame)

Still open from the prior review: `UIElement.Style` allocates two `UIStyle` per read (`Core/UIElement.cs:326-333`;
Button reads it 3× per frame `Button.cs:150,166,184`, Label 2× `Label.cs:145-170`, DataGrid 1× `DataGrid.cs:667`);
`ConsumerContext.Invoke` concatenates the mute key per call (`Core/ConsumerContext.cs:45,79`; `Invoke<T>` still
ignores `LogCallbacks`); `TextInput.ClipLeft` O(n²); Label re‑measure per frame and full relayout when a text delegate
returns a new string each frame; Grid array churn; ScrollView drag relayouts the menu per tick; overlay delegate/closure
per frame (`UIElement.cs:442-457`).

**Regressions in v1.1:**

- `Core/UIElement.cs:52` `Consumer` → `Sealing.ContributorOf` (`Core/Sealing.cs:19-29`) walks every ancestor on
  every `Raise`, i.e. on every bound getter every frame. Cache the effective context on attach / when `Contributor`
  is set.
- `Rendering/Draw.cs:136-165` `FitText`, called per frame by `Label.DrawCore` (`Label.cs:170`),
  `Button.DrawContent` (`:217`), `Checkbox` (`:161`), `Dropdown` (`:319`): 3–4 `SpriteFont.MeasureString` per element
  per frame even when the text fits, plus a binary‑search `Truncate` when it does not. A results grid of 60 visible
  labels ≈ 200+ measures per frame. Cache by (text, width, scale).
- Auto‑form per‑field reflection + boxing per frame (§2.5); rich tooltip rows per frame (§2.9); `Theme.Current` /
  `FontScale` per text call (§2.9); `CustomComponentBridge` proxied `WantsFocus`/`WantsOverlay` per read (§2.4);
  `Composite.cs:131,140` and `ScreenExposures` guard keys concatenated per call (the example reads
  `GetNumber("value")` from a tooltip delegate, i.e. per frame while hovered).

### 2.11 API surface, Pintail and the facade

- **Verified OK** — every member added since `29733da` is Pintail‑safe: interfaces, primitives, XNA types,
  `string[]`/`int[]`, `Func`/`Action` over proxyable types; no `ref`/`out`/`params`/tuples/generic interfaces/`event`/
  `Type`/`Attribute`/`Delegate`; no optional parameters. **All overload groups differ by parameter count**
  (`SetMargin` 1/2/4, `CreateMenu` 1/2, `ContributeTo` 4/5, `AddCustom` 3/4, `ShowToast` 1/2, `IUITooltip.Line` 1/2).
  Pintail 2.9.1 ships `ArrayProxyFactory` and `NullableProxyFactory`, so `IUISlotInfo[]` and the prior review's open
  question `UIFont?` are covered (still never exercised in game — see below). `object` escapes are by design and
  documented (`Tag`, `IUICompositeArgs.SetObject/GetObject`, `AddForm(object)`).
- **DOC (S)** — The three interface copies are *not* all byte‑identical: `ProfitCalculator/apis/IStardewUIApi.cs`
  adds `#pragma warning disable CS1591` + a blank line at `:14-15` and uses CRLF; `Api/Public` and
  `UIFrameworkExample/Api` match (md5 `0513cfe9…`). Either add the pragma to the source or `<NoWarn>CS1591</NoWarn>`
  in `ProfitCalculator.csproj` and re‑copy.
- **POLISH (M)** — No consumer teardown API (architecture §2/§15 promise "all of that consumer's menus/hotkeys can be
  torn down together"): `HotkeyService.UnregisterAll` (`:70`) and `ConsumerContext.ResetMutes` (`:108`) remain
  unreferenced; nothing removes a mod's contributions, decorators, exposures, HUDs, composites and hotkeys in one call.
- **POLISH (S)** — Silent `as` casts remain in `SetDefaultStyle` (`Api/StardewUIApi.cs:271`), `InvalidateLayout(IUIMenu)`
  (`:217`), `IUIElement.Style` (`UIElement.cs:164`), `RichTooltip` (`:156`), `DefaultButton`/`CancelButton`
  (`UIMenu.cs:250-251`) while `RequireElement`/`RequireReactive`/`RequireSignal` (`:537-556`) throw. `RequireId` is
  reused for non‑id arguments (`:295,305-325,330,343,354`) → wrong message/`paramName` for a null `ownerModId`.
- **NIT** — `IUIScreenContext.Keys` (`IStardewUIApi.cs:1077`) shadows the imported `Keys` type name inside that
  interface; `IUICompositeArgs.SetObject` can leak an unproxied framework object between mods — one sentence.
- **DOC** — `ShowToastWithIcon`: `durationMs <= 0` becomes the default and an empty text with no icon is dropped
  (`Api/StardewUIApi.cs:578,583`), undocumented; `IUIMenuOptions.PlayerLayout` "needs DrawBox" — resize also needs
  fixed `Width`+`Height` (`Core/UIMenu.cs:235`); `IUIMenuOptions.Modal` still documented as "block clicks outside"
  while the behaviour is click‑outside‑closes (`MenuHost.cs:101-104`); `OnScroll` units still differ per component
  without a side‑by‑side table; `IUICheckbox` has no `HoverSound` although README `:404` says so.

### 2.12 Consumers

**Example mod** (`UIFrameworkExample/`) — verified: no `ProjectReference` (architecture §14), tree built once at
`GameLaunched`, getter/setter pairs consistent, hotkey bound once. Polish: `ModEntry.cs:390-391` passes `Find(...)`
straight into `Add` (a typo would throw through the proxy at startup); `:301-304` `Find(...) is IUIGrid` works only
because Pintail proxies the concrete framework type — worth a README note; the divider at `:76-77,97-98` is still the
invisible `AddSpacer(…, 0, h)`; `manifest.json:11` says `MinimumVersion 1.0.0` while the mod uses 1.1 members.

**Profit Calculator port** (`ProfitCalculator/`) — verified: hard dependency `>= 1.1.0`, both screens built once,
hotkey re‑bound on config save (same id → replace), results menu re‑used through `Show` with `Refresh` + `ScrollToRow(0)`
(correct with today's `EnsureFresh`), 900 px fixed width fits the four columns at 150 % UI scale. Polish:
`ProfitCalculatorResultsMenu.cs:66` sets `grid.RowTooltip` **and** `:138` builds a second identical ~20‑block tooltip
per row for the sprite; `ModEntry.cs:143` `SButton.None` → keybind string `"None"` silently binds nothing;
`ProfitCalculatorMainMenu.cs:124` the produce‑type dropdown's labels are all "not implemented" but it stays
interactive; `ProfitCalculator.csproj:7` `<Version>1.0.0</Version>` vs manifest `2.0.0`.

**Members never exercised by either consumer** (architecture §14 "test every member from the example mod";
§0 admits "none of the v1.1 features has been exercised in game yet") — `IStardewUIApi`: `GetMenu`, `DestroyMenu`,
`OpenMenu`, `CloseMenu`, `IsOpen`, `AddPanel`, `AddCanvas`, `AddScrollView`, `Find(menu, id)`, `Remove`,
`InvalidateLayout`, `RegisterHotkey`, `UnregisterHotkey`, `CreateStyle`, `SetDefaultStyle`, `RemoveContribution`,
`ExposeBool`, `Publish`, `OnScreenBuilt`, `HasComposite`, `ListComposites`, `UndefineComposite`, `ThemeColor`,
`ReducedMotion`, `SignalBool`, `ComputedNumber`, `ComputedBool`, `BindTextToSignal`, `BindVisible`, `BindEnabled`,
`BindTextInput`, `BindCheckbox`, `BindSlider`, `Unbind`, `GetHud`, `DestroyHud`, `ShowToast` (both), `ResetPlayerLayout`.
Sub‑interfaces: all of `IUIStyle`; most of `IUIScreenContext`, `IUICompositeArgs`, `IUIComposite`, `IUIDataGrid`
(`Filter`, `MultiSelect`, `SelectedRows`, `OnRowClick`, …), `IUIForm` (never across the proxy), `IUIHud`, `IUISignal`/
`IUIComputed` (`Flag`, `Subscribe`, `Unsubscribe`), `IUIElement.Sealed`/`OnDrawOverlay`/`OnKey`/`OnFocus`/`OnBlur`/
`OnRightClick`/`OnHover`/`Style`, `IUIMenu.PlayerLayout`/`OnKey`/`OnScroll`/`OnUpdate`/`CancelButton`/`SetPosition`.
*Suggested:* a `ui_apitest` console command or a kitchen‑sink pass in the example that touches every member once.

### 2.13 Documentation drift

**architecture.md**

- §0 phase 7 (`:28`) and §13 phase 7 (`:454-460`): no `ProfitCalculator/main/ui/framework/`, no `FrameworkMainMenu`/
  `FrameworkResultsMenu`, no `ModConfig.UseUIFramework`, no GMCM toggle, dependency is `IsRequired: true`, the legacy
  `main/ui/**` screens were deleted in `df2cd8e` (so "legacy screens remain the fallback" and "delete `main/ui/**`"
  are both stale); §0 line 5 and §3.1 still describe `ProfitCalculatorResultsList`/`BaseOption`/`TextOption`/
  `DropdownOption` as "the existing code". The results grid has four columns (crop, profit, profit/day, harvests;
  `ProfitCalculatorResultsMenu.cs:92-100`), not the six listed.
- §0 branch column says `v2-integration` — no such branch exists; the work is merged to `master`. "35 tests" → **38**
  `[Fact]` (Input 8, Layout 8, MenuOverflow 3, Routing 9, ScrollAndList 4, Tooling 6). "Themes + accessibility" row
  and §11 (`:382`) cite `Integrations/IStardewAccessApi.cs` (deleted). §0 known gaps should add: fit‑content width
  (§2.1), 1.6 background modes (§2.2), gamepad items (§2.3), the child‑menu update pause (§2.7 of the prior review),
  the binding/subscription leaks (§2.5/§2.6).
- §16 status line (`:494`) "everything implemented": not true for the Profit Calculator slots
  (`settings.extra`, `results.header`, `results.footer`, `crop.hoverbox.extra`) + `recalculate` command + the example
  contributing to them (grep: zero hits under `ProfitCalculator/`), the "Contributing UI to another mod" and
  "Publishing a composite" guides (README has `#### Extension slots and screen context` `:444` and `#### Composites`
  `:515` instead), `IUICustomComponent.Build` as a member (it is the `AddCustom(…, build)` overload; deliverable list
  `:542` still names it), and a public `UIFramework.Testing` package (README `:953` says it is not one).
- §4 diagram (`:130-142`) and §12 (`:394-415`) still list `Input/`, `Themes/`, `Registry/`, `Config/`,
  `DefaultTheme.cs`, `UIFramework.Tests/` and omit ~50 files that exist (`Api/UIMenuOptions.cs`; Components
  `Composite`, `CustomComponentBridge`, `CustomHostAdapter`, `DataGrid*`, `ScrollbarGadget`, `Slot`; Core
  `Accessibility`, `AutoForm*`, `CompositeArgs`, `ConsumerContext`, `Numbers`, `PerfCounters`, `RichTooltip`,
  `Sealing`, `Signal*`, `TreeDump`, `TreeExporter`, `UIEvents`, `UIHud`, `UIRowEvent`, `UIServices`; Hosting
  `CompositeRegistry`, `DebugConsole`, `ExtensionRegistry`, `HudService`, `Inspector`, `PlayerLayoutController`,
  `ScreenContext`, `ScreenExposures`, `ToastLayer`, `WindowLayoutStore`; Rendering `InspectorRenderer`,
  `PseudoLocalizer`, `RichText`, `ThemeData`, `ThemeSwitcher`, `TooltipRenderer`; `assets/themes.json`; `NEXUS.md`;
  example `DemoSettings.cs`, `FrameBox.cs`, `VolumeGauge.cs`). §8 sketch still says "roughly 300 lines" (file is
  1 523) and differs in `CreateMenu`/`IUIMenuOptions`/`Find`/`IUIDivider`. §12 "Repository tasks" still says the test
  project is "if added".
- §11 / `ModConfig.cs:294` / `i18n/default.json:47` "the caret is the only animation" — toasts slide and fade.

**README.md** — every `api.`/`ui.` member in the samples exists on the interface (scripted check) and the v1.1 blocks
use the current signatures. Wrong claims: accessibility (`:87-91, 856-862, 1619, 1738`); `:483` demonstrates the
subscription leak; `:245` "`DestroyMenu` closes and forgets it" (hotkey survives); the prior review's list (`:258`
backdrop, `:359` "opening another closes the first", `:404` checkbox `HoverSound`, `:1045` greyed disabled, `:278/1093`
Panel fill, `:1142` `ItemCount`, `:1270/1291` `Modal`) survives verbatim. Root `README.md:16-17` lists the framework
and example as 1.0.0 (manifests 1.1.0) and `:12` Profit Calculator as 1.0.4 (manifest 2.0.0, now requires the
framework — no note). NEXUS `:32` repeats the accessibility claim. `i18n/default.json:28` `menu.close` still unused.

### 2.14 Dead code and leftovers

`HotkeyService.UnregisterAll` (`:70`), `ConsumerContext.ResetMutes` (`:108`), `UIServices.SmallTextBoxTexture` (`:108`)
+ `assets/text_box_small.png`, `OverlayLayer.Popups` (`:27`), `UIElement.LayoutDirty`, i18n `menu.close`,
`Integrations/` references in docs to a file that no longer exists. `MenuRegistry.MenusOf` and `UIServices.Translation`
are now used. Either wire the per‑consumer teardown (§2.11) or delete the first two.

---

## 3. Suggested work packages (in order)

| # | Package | Files | Effort |
|---|---|---|---|
| A | **Wire accessibility**: re‑add `Integrations/IStardewAccessApi.cs`, fetch the API in `OnGameLaunched`, set `UIServices.Announcer`; skip announcing bare containers; add a Verified‑in‑game note | `ModEntry.cs`, `Integrations/`, `Core/UIMenu.cs`, docs | S |
| B | **Binding + subscription lifecycle**: dispose‑before‑install in `SignalBindings.Add`; element‑attributed `Bindings.Drop` in `OnElementDetached`; `DropMenu` for HUD inner menus; per‑contribution subscription cleanup in `RebuildSlot`; reset contribution mute on re‑register; computed `Invalidate` semantics (or doc); struct‑model guard; `Display` fallback | `Core/SignalBindings.cs`, `Core/UIMenu.cs`, `Hosting/HudService.cs`, `Hosting/ScreenExposures.cs`, `Hosting/ExtensionRegistry.cs`, `Core/Signals.cs`, `Api/StardewUIApi.cs`, `Core/AutoFormReflection.cs` | M |
| C | **Sealing**: enforce on `IUIContainer.Add/Remove/Clear`/`Rebuild` through an ambient caller; make `Find` filtering per‑mod not per‑call; set `Contributor` on composites; document the remaining advisory cases | `Core/UIContainer.cs`, `Core/UIMenu.cs`, `Core/Sealing.cs`, `Components/Composite.cs`, `Api/StardewUIApi.cs` | M |
| D | **Measure contract** (prior package A, still the biggest layout item): unbounded fit‑content axes in `UIMenu.Relayout`; real leftover distribution in `Stack` (fixes the 0‑extent regression); `Composite` derives from `Stack`; Grid star consistency + re‑measure; ListView/DataGrid width = content; Spacer `0 → null`; LayoutEngine unknown‑token rule; ceil‑per‑child | `Core/UIMenu.cs`, `Components/Stack.cs`, `Components/Composite.cs`, `Components/Grid.cs`, `Components/ListView.cs`, `Components/DataGrid.cs`, `Components/Spacer.cs`, `Core/LayoutEngine.cs` | M–L |
| E | **DataGrid**: `EnsureFresh` on count change; resize upper clamp + row scissor; `SelectedRows` before first refresh; virtual `Clear`/`Remove` (with ListView); deferred `Refresh` during dispatch; drag without relayout per tick; release arrows at the ends; last‑column resize rule | `Components/DataGrid*.cs`, `Core/UIContainer.cs`, `Components/ListView.cs` | M |
| F | **Hosting fixes**: snapshot iteration in `HudService`; drag threshold + anchor‑relative persistence in `PlayerLayoutController`/`WindowLayoutStore`; unregister the toggle hotkey in `DestroyMenu`/replace; `ThemeSwitcher` invalidates HUD menus; toast offset above vanilla HUD messages; `HudsActive` parity; `ReducedMotion` for toasts; drop redundant `AssetReady` | `Hosting/HudService.cs`, `Hosting/PlayerLayoutController.cs`, `Hosting/WindowLayoutStore.cs`, `Hosting/MenuRegistry.cs`, `Rendering/ThemeSwitcher.cs`, `Hosting/ToastLayer.cs`, `ModEntry.cs` | M |
| G | **1.6 chrome + focus/overlay correctness** (prior B + C): background modes + `showWithoutTransparencyIfOptionIsSet`; no double dim; title inset with `DrawBox=false`; `IsUsable` in `Validate`/`CanFocus`/`HandleActivate`; `UpdateSubscription` on open; `SetHovered(null)` on close/detach; popups drawn last; preserve focus on scrollbar clicks; texture cache invalidation | `Core/UIMenu.cs`, `Hosting/MenuHost.cs`, `Core/FocusManager.cs`, `Core/OverlayLayer.cs`, `Core/EventRouter.cs`, `Components/Button.cs`, `Core/UIServices.cs` | S–M |
| H | **Dropdown + NumberInput/TextInput** (prior D + E, untouched): hover‑vs‑keyboard highlight; `RestoreOwnIndex` only on vanished value; `base.HandleKey` first; leading `-`; step vs decimals; `Clamp=false` stepping; wheel returns changed; paste as one edit + filtering; `ClipLeft`; validator fail‑open; mixed get/set validation | `Components/Dropdown.cs`, `Components/NumberInput.cs`, `Components/TextInput.cs`, `Api/StardewUIApi.cs` | M |
| I | **Gamepad** (prior G, untouched): snap on open; B clears text focus; richer snap targets filtered by visibility/collapse; re‑resolve snapped component after relayout; on‑screen keyboard or documented limitation | `Hosting/MenuHost.cs`, `Core/UIMenu.cs` | M |
| J | **Perf**: cache the consumer context on attach; cache `FitText` by (text, width, scale); allocation‑free style resolution; tuple mute keys; cached tooltip rows; `Theme.Current`/`FontScale` cache; compiled form getters; inspector subject once per frame; cached bridge `WantsFocus`/`WantsOverlay` | `Core/UIElement.cs`, `Rendering/Draw.cs`, `Core/ConsumerContext.cs`, `Rendering/TooltipRenderer.cs`, `Rendering/Theme.cs`, `Core/AutoFormField.cs`, `Rendering/InspectorRenderer.cs`, `Components/CustomComponentBridge.cs` | M |
| K | **Small polish**: pseudo‑loc `ý` glyph + rich‑text invalidation; Image hit‑test/disposed; adapter NaN/∞ and hover‑on‑move; Slider rounding; Panel min inset; Label disabled look; close‑button draw order; `Slot.MaxHeight` clip; overlay host `ElementAt`; `RequireId` messages; exporter gaps; typos | various | S |
| L | **Docs**: architecture §0/§3/§4/§8/§12/§13/§16 refresh (phase 7 reality, file list, branch names, test count, deliverables not done); README + NEXUS accessibility/leak/hotkey corrections and the prior list; interface XML additions (`Unbind`, `Cancel`, form type support, `GetString` bools, `ListSlots` timing, `Filter` timing, cell `Handled`, HUD input, `Modal`, `OnScroll` units); root README versions; identical PC API copy; example: `CreateStyle` member, divider fix, `MinimumVersion 1.1.0` | `architecture.md`, `README.md`, `NEXUS.md`, root `README.md`, `Api/Public/IStardewUIApi.cs` (×3), example | M |
| M | **Coverage**: `ui_apitest` / kitchen‑sink pass over every unexercised member (§2.12); in‑game acceptance at 75/100/150 % UI scale, resize, gamepad reach, split‑screen HUD; tests for the measure contract, re‑binding, contributor binding cleanup, DataGrid `EnsureFresh`/resize clamp | example `ModEntry.cs`, `StardewUIFramework.Tests` | M |

---

## 4. Verified OK (no action needed)

- **Signals**: `SignalScheduler` batch nesting, flush re‑entrancy guard, `pendingSet.Remove` before notify,
  `MaxNotificationsPerBatch`, `[ThreadStatic]` state; cycle detection (`IsEvaluating` → `LogCycle` once, stale value
  kept, `Begin/EndEvaluation` in `try/finally`); `RetrackDependencies` edge maintenance; `Signal.Set` compares through
  the new kind; `Version` bumps only on real changes; invariant `FromText`/`FromNumber`; `ValueBinding` chains the
  consumer setter and restores originals on `Dispose`; bindings dropped on element detach for the owner's own
  elements and on `DestroyMenu`/replace.
- **Auto‑forms**: undo/redo push/clear semantics, `refreshing` guard against recursive history entries, `Save`
  snapshot, `IsDirty` value semantics, `AwayFromZero` integer rounding with `Argument/Overflow/Format` catches,
  `TypeLimits` → clamp, exact `Enum.Parse`, static/indexer/private‑setter exclusion, inherited properties base‑first,
  `Nullable<>` skipped rather than crashing, `FindValidator` overload preference, Ctrl+Z/Y/Shift+Z bubbling.
- **Sealing model**: owner never restricted, contributor reachable below a sealed ancestor, decorators edit only
  outside sealed subtrees (through the facade), detached elements unrestricted, messages name the owning mod.
- **Slots**: ordering = priority asc then ordinal mod id; `MaxContributions` counts accepted only; vetoes skip and
  log; each contribution builds into its own `Stack` tagged with `Contributor`; `OnScreenBuilt` after contributions
  and before layout; hotkeys registered inside a contribution are replaced by id, not duplicated; `Slot` derives from
  `Stack` and manages `Visible` only on change.
- **DataGrid**: selection, `IUIRowEvent.Row`, `OnValueChanged`, `OnRowActivated`, `ScrollToRow`, column/sort/filter
  delegates all use **underlying** indices; keyboard navigation converts through `position`/`order`; stable sort with
  deterministic tiebreak, null keys → `""`, NaN via `double.CompareTo`, keys evaluated once per refresh; row reuse on
  scroll; `BuildRow`/`SyncCells` reconcile column changes; infinite/NaN available handled; header/scrollbar require
  `Target == this` + left button; capture for thumb/divider drags; `HandleScroll` returns `false` at the ends;
  keyboard runs after the consumer's `OnKey` and only while the grid itself is focused; `CommitSelection` finishes
  state before raising; `Publish` snapshots handlers.
- **HUD/rendering**: SMAPI `RenderedHud`/`RenderedActiveMenu` fire inside `PushUIMode` + `Begin(Deferred, AlphaBlend,
  PointClamp)` (`Game1.cs:13348-13368, 13435-13450`), matching `WithScissor`'s re‑`Begin`; HUD step skipped when the
  world is not drawn; toasts drawn once per frame; clip stack exception‑safe; persisted layouts clamped to the
  viewport; layout applied in the `MenuHost` ctor before `NotifyOpening` and the first `Relayout`; drag/resize only
  for clicks the tree did not handle (buttons in the title strip keep priority).
- **Rich text**: unknown/malformed tags fall back to a literal `[`, unclosed tags run to the end, `[[`/`]]` escape,
  invariant hex parse, nested colour/link stacks; measure and draw share one cached `RichLayout`; `LinkAt` uses the
  same line offsets as `Draw`; bold width includes the offset; `Theme.FontScale` applied identically in measurement
  and drawing. `TooltipRenderer.Place` clamps to the viewport; rich tooltips draw after popups; coin sprite matches
  vanilla.
- **Themes**: served via `AssetRequested`, unknown names fall back to `default` with a one‑time warning, broken asset
  falls back to vanilla, `ThemeColor` unknown keys → text colour as documented, `FontScale` clamped [0.25, 4], GMCM
  changes apply live, theme list read 5 ticks after launch so CP themes appear; `assets/themes.json` parses.
- **Tooling**: `TreeExporter`/`TreeDump` escape strings, format numbers invariantly, de‑duplicate identifiers and
  avoid keywords; emitted `Add*` lines match the current interface; export writes under the mod folder with sanitised
  names, clipboard failures swallowed (`DesktopClipboard.SetText` exists in 1.6); console commands run on the game
  thread and never mutate a tree during a draw; `PerfCounters` zero‑cost when disabled, `ConditionalWeakTable` keyed
  by menu.
- **ModEntry**: nothing touches `Game1` before `GameLaunched`; `SaveLoaded` runs `CloseAll` before `layouts.Load`;
  one facade per consumer via `GetApi(IModInfo)`; GMCM signatures match the current GMCM API; all `config.*`,
  `form.*` and `a11y.*` i18n keys exist.
- **API/consumers**: overloads differ by count; Pintail‑safe types throughout; `Api/Public` and example copies
  byte‑identical; `ApiVersion` 1.1.0 = manifest = README; example has no `ProjectReference`; both consumers build
  their trees once, keep getter/setter pairs on one settings object, and the PC results grid reads a list rewritten in
  place before `Refresh`; `master` now contains `StardewUIFramework/README.md` so the NEXUS/root links resolve; all
  38 tests contain assertions; the test project resolves game assemblies through `STARDEW_GAME_DIR` as documented.
- Everything in `code-review.md` §4 (keyboard double delivery, value pipeline, routing, layout basics, hosting
  lifecycle) still holds.

---

## Appendix A – status of `code-review.md` (29733da) findings

Verified by `git diff 29733da..HEAD` per cited file plus a read of the current code at each location.

### A.1 Headline items 1–12

| # | Item | Status | Current evidence |
|---|---|---|---|
| 1 | Fit‑content menu with star Grid / ListView is screen‑wide; Stack never distributes | **PARTIAL** | Stack measures with remaining space and clamps (`Stack.cs:92-97,131-136`); root still finite (`UIMenu.cs:338-341`); new 0‑extent symptom (§2.1) |
| 2 | `AddSpacer(…, 0, h)` + `Line` invisible | OPEN | `Spacer.cs:14-18,24,28`; example still does it |
| 3 | 1.6 three‑way background option | OPEN | `UIMenu.cs:442`; no host override |
| 4 | Dropdown hover overwrites keyboard highlight | OPEN | `Dropdown.cs:469-476` |
| 5 | Nested `WithScissor` sniffs device state | **FIXED** | `Draw.cs:210-249` clip stack — verified correct |
| 6 | `ListView.Clear()`/`Remove()` detach rows | OPEN | `UIContainer.cs:54,66`; now also DataGrid |
| 7 | NumberInput `-`/step/clamp/wheel | OPEN | `NumberInput.cs:331-339,134-142,258-265,499-508` |
| 8 | Title over first row with `DrawBox=false`; banner reserve | **PARTIAL** | banner fixed (`UIMenu.cs:338-346`); title inset open (`:326,462`) |
| 9 | Focus validation ignores hidden ancestors | OPEN | `FocusManager.cs:129-135,182-186`; `Button.cs:228-233` |
| 10 | Gamepad set | OPEN | `UIMenu.cs:590-607`; `MenuHost.cs:198-201,234-266` |
| 11 | Per‑frame allocations | OPEN, **regressed** | `UIElement.cs:52,326-333`; `Draw.cs:136-165`; `ConsumerContext.cs:45,79`; `TextInput.cs:102-110` |
| 12 | Popups drawn under `OnDrawOverlay` content | OPEN | `OverlayLayer.cs:71-87` |

### A.2 Section bullets (only the changed ones; everything not listed is OPEN at the lines given in §2 above)

- §2.1: Stack whole‑available measure → PARTIAL; all other layout bullets OPEN.
- §2.2: nested scissor FIXED; banner reserve FIXED; disabled look PARTIAL (Button/Checkbox/inputs use
  `DisabledTextColor`, Label does not); everything else OPEN.
- §2.5: Dropdown closed‑box label overflow FIXED via `FitText`; rest OPEN.
- §2.6: Checkbox label overflow FIXED (`Checkbox.cs:157-161`); Button text overflow PARTIAL (text shrinks, icon
  overflow unchanged); scroll‑into‑view on focus FIXED (`FocusManager.cs:68`, `ScrollView.cs:142-157`, with the
  caveats in §2.3); rest OPEN.
- §2.10: `MenuRegistry.MenusOf` and `UIServices.Translation` now used; rest still dead.
- §2.11: NEXUS/root README `blob/master` link resolved (`master` contains the file); §0 window‑chrome item rewritten;
  everything else OPEN.

### A.3 Work packages A–K

A PARTIAL (Stack only) · B PARTIAL (banner, clip stack) · C OPEN (scroll‑into‑view landed from F) · D OPEN (closed
label clip only) · E OPEN · F PARTIAL (scroll‑into‑view) · G OPEN · H OPEN, regressed · I PARTIAL (checkbox overflow,
disabled colour) · J PARTIAL (docs rewritten for v1.1 but the listed corrections not applied) · K PARTIAL (test project
exists, 38 tests; the measure‑contract cases cannot pass yet).

---

## Appendix B – game and engine facts used (decompiled SDV 1.6)

| Fact | Source |
|---|---|
| `drawDialogueBox(x, y, w, h, speaker, drawOnlyBox, …)` frame spans `[y+64, y+height]`; `ignoreTitleSafe` defaults true | `Game1.cs:14473ff` |
| SMAPI `RenderedHud` and `RenderedActiveMenu` are raised inside `PushUIMode()` + `Begin(Deferred, AlphaBlend, PointClamp)` | `Game1.cs:13348-13368, 13435-13450` |
| Vanilla `drawHUD` condition includes `!freezeControls && !panMode && !HostPaused && gameMode == 3` | `Game1.cs:13353` |
| `HUDMessage.draw` stacks from `(tsarea.Left + 16, tsarea.Bottom − height − heightUsed − 64)` | `HUDMessage.cs:146-153` |
| `DesktopClipboard.SetText` exists | `DesktopClipboard.cs:33` |
| The game never sets `SpriteFont.DefaultCharacter` (a missing glyph throws in `DrawString`) | grep over decompiled source, no hits |
| Pintail 2.9.1 ships `ArrayProxyFactory` and `NullableProxyFactory` | `smapi-internal/Pintail.dll` type names |
| Everything in `code-review.md` Appendix A (menu chain update/draw, keyboard dispatcher, gamepad key mapping, snapping, background modes, UI render target) | unchanged |
