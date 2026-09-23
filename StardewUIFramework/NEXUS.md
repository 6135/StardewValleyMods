# UI Framework

A SMAPI library mod that other mods use to build their in-game menus. It provides the windows, buttons, text and
number inputs, checkboxes, dropdowns, sliders, scroll areas, lists and tooltips, plus layout, keyboard, mouse and
gamepad handling, so mod authors can put together a screen from parts instead of drawing and positioning everything
by hand.

## For players

- **It does nothing on its own.** Install it because another mod lists it as a requirement; if nothing requires it
  you can remove it.
- Requirements: Stardew Valley 1.6 or later and SMAPI 4.x. Generic Mod Config Menu is optional.
- Install: unzip into `Stardew Valley/Mods` so that `Mods/UIFramework/manifest.json` exists, then launch the game
  through SMAPI.
- Config (`Mods/UIFramework/config.json`, also editable through Generic Mod Config Menu):
  - `TooltipDelayMs` (default 400): how long the cursor must rest on an element before its tooltip appears.
  - `DebugOverlay` (default false): draw element bounds and ids in framework menus. For mod authors.
  - `LogCallbacks` (default false): trace-log every mod callback the framework invokes. For mod authors.
  - `Theme` (default `default`), `TextScale` (default 1.0) and `ReducedMotion` (default false): look, text size and
    animation of every framework menu. `PseudoLocalize` and `InspectorHotkey` (default F10) are for mod authors.
- Console commands: `ui_theme [name]` lists or switches themes, `ui_layout_reset` forgets moved / resized windows,
  `ui_debug` toggles the debug overlay, `ui_list` lists the framework menus that are open.
- Works in single player, multiplayer and split-screen. No Harmony patches.

**Not the same as StardewUI.** focustense's StardewUI is a different framework built around StarML markup. UI
Framework is a code-first C# builder API with no markup language. A mod that depends on one does not need the other,
and both can be installed together.

## What's new in 1.2

- Mod authors: an item element (and a tooltip row) that draws an actual item instance, so flavored goods like
  Starfruit Wine or Blueberry Jelly show their real colors.
- Players: dropdowns with more choices than fit now show a small scrollbar, so it's clear the list scrolls.

## What's new in 1.1

- Players: four themes (`default`, `dark`, `high-contrast`, `colorblind`; content packs can add more), text scaling
  and reduced motion, screen reader announcements through Stardew Access, windows you can drag, collapse and resize
  (remembered per save), and toast notifications.
- Mod authors: extension slots so other mods can add UI to your screens, shareable composite components, a
  sortable / filterable data grid, signals and auto-generated forms with undo / redo, rich text and rich tooltips,
  HUD widgets, and an in-game inspector that exports a menu as C# code.

## For mod authors

Copy one file (`IStardewUIApi.cs`) into your project, add `6135.UIFramework` as a dependency in your manifest, and
request the API with `helper.ModRegistry.GetApi<IStardewUIApi>("6135.UIFramework")`. Menus are built from typed
handles (`AddGrid`, `AddButton`, `AddDropdown`, ...), values are bound through getter/setter delegates, every
interaction raises a callback, and you can supply fully custom components through an interface. Hotkeys accept
SMAPI keybind strings straight from your config.

Full documentation, the API reference and a complete example mod are on GitHub:
<https://github.com/6135/StardewValleyMods/blob/master/StardewUIFramework/README.md>

## Source and issues

Source code: <https://github.com/6135/StardewValleyMods> (folder `StardewUIFramework`). Please report problems on
the GitHub issue tracker or in the Nexus posts tab with your SMAPI log.
