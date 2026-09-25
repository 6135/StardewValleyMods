# UI Framework

A SMAPI library mod that other mods use to build their in-game menus, written as JSON data or C#. It provides the
windows, buttons, text and number inputs, checkboxes, dropdowns, sliders, scroll areas, lists, data grids, forms and
tooltips, plus layout, keyboard, mouse and gamepad handling, so authors can put together a screen from parts instead
of drawing and positioning everything by hand. Content Patcher packs can build complete, interactive screens (and
settings pages, HUD widgets, additions to other mods' menus) without writing any code.

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

## What's new in 1.8

- Players: clicking a button, checkbox, dropdown or slider no longer leaves it highlighted, and one Escape closes the
  menu; Tab and the arrow keys still show where focus is. A centered window no longer jumps when its content grows or
  shrinks (a line shown on hover, a tab switch). Hovering any part of a list or grid row shows the row's tooltip.
- Content pack authors: `ShowOverMenus` keeps a HUD widget visible on top of open menus; named tooltips
  (`Owners` > `Tooltips`, used with `{ "From": "name" }`); an image's `Sprite` can change per row (`${row.sprite}`).
- Players: resizing a window now lets it get narrower down to what its content needs; before, a window with
  stretched content (grids, full-width rows) could not be made narrower at all. Grabbing the resize grip no longer
  enlarges a window on its own. In narrow windows, rows of buttons move onto a second line, form labels wrap
  instead of pushing controls past the edge, text is never cut off just to make room (only when the screen itself
  is too small), a long title is shortened with "...", and lists and data grids no longer draw outside their box.
- Mod authors: `Resizable` (C# and data, default off) lets players resize a window that fits its content, not just
  fixed-size ones; `IUICustomComponent.MinimumWidth` (new, required) reports a custom component's narrowest width; `Wrap` on stacks (C# `IUIStack.Wrap`, data `"Wrap": true` on `Stack` / `Repeat` / `Outlet`) lets a row flow onto new lines (slots too: `IUISlot.Wrap`); `MinWidth` / `MaxWidth` on every element; `Shrink` lets a button, checkbox, dropdown or label shorten its text with "..." in narrow spaces; `IUIHud.ShowOverMenus`; `ImportData` / `ImportDataFile` build the menus before returning, so
  `GetMenu` and `BindToggleHotkey` work right after the import. `ImportDataFile` now takes a full path
  (`Path.Combine(helper.DirectoryPath, "assets/ui.json")`); relative paths are no longer resolved.
- Fixes: framework trigger actions no longer report "failed" after they succeeded (buttons stopped responding after
  the first click); UI Framework now lists Content Patcher as an optional dependency, so its token is accepted.

## What's new in 1.3 to 1.7

- Content pack authors: data-driven UIs. Menus, HUD widgets, settings screens, reusable templates and components, and
  additions to other mods' menus, all written as Content Patcher data, with live expressions, named state (including
  per-save and cross-save settings), trigger actions, game state queries and hot reload (`patch reload` keeps an open
  menu open).
- Mod authors: keep your screens in JSON and offer your code to data by name (commands, functions, row sources,
  models, draw hooks, composites); export any menu, C# or data, as JSON from the inspector.

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

## For content pack authors

Add `6135.UIFramework` as a dependency of your Content Patcher pack and edit `Mods/6135.UIFramework/Menus` (or `Huds`,
`Owners`, `Sprites`, `Composites`, `Contributions`). Each menu is a tree of elements with the same names as the C#
API (`Stack`, `Grid`, `Label`, `Button`, `NumberInput`, `Dropdown`, `List`, `DataGrid`, `Form`...). Text can show live
values (`"Clicked ${menu.clicks} times"`), inputs bind to state (`"Bind": "config.volume"`), and events run trigger
actions (`"OnClick": [ "AddMoney 100", "6135.UIFramework_CloseMenu" ]`). Open a menu with a hotkey, a map tile
action, any trigger action or `ui_open`. State can also drive your own patches through a Content Patcher token.
`ui_validate` points at every mistake by path, and `ui_schema` gives your editor autocomplete. A complete example
pack is on GitHub.

## For mod authors

Copy one file (`IStardewUIApi.cs`) into your project, add `6135.UIFramework` as a dependency in your manifest, and
request the API with `helper.ModRegistry.GetApi<IStardewUIApi>("6135.UIFramework")`. Menus are built from typed
handles (`AddGrid`, `AddButton`, `AddDropdown`, ...), values are bound through getter/setter delegates, every
interaction raises a callback, and you can supply fully custom components through an interface. Hotkeys accept
SMAPI keybind strings straight from your config. You can also keep your screens in JSON (imported from your mod
folder, and hot-reloaded while you develop) and register C# commands, functions, row sources and components that the
JSON reaches by name.

Full documentation, the API reference and a complete example mod are on GitHub:
<https://github.com/6135/StardewValleyMods/blob/master/StardewUIFramework/README.md>

## Source and issues

Source code: <https://github.com/6135/StardewValleyMods> (folder `StardewUIFramework`). Please report problems on
the GitHub issue tracker or in the Nexus posts tab with your SMAPI log.
