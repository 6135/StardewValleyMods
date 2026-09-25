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

## What's in 1.8.0 (first release)

- Players:
  - Four themes (`default`, `dark`, `high-contrast`, `colorblind`; content packs can add more), text scaling and
    reduced motion, and screen reader announcements through Stardew Access.
  - Windows you can drag, collapse and resize (remembered per save), and toast notifications.
  - Keyboard, mouse and gamepad navigation. Clicking a control doesn't leave it highlighted, and one Escape closes the
    menu. Hovering any part of a list or grid row shows the row's tooltip.
  - Windows adapt to their size: rows of buttons wrap, form labels wrap, long titles are shortened with "...", and
    dropdowns with more choices than fit show a scrollbar.
  - Split-screen: each player opens their own menus, with their own focus, hover, popups, HUD state and inspector.
- Content pack authors:
  - Complete, interactive screens as Content Patcher data: menus, HUD widgets, settings screens, reusable templates
    and components, and additions to other mods' menus.
  - Live expressions, named state (per menu, per session, per save and cross-save settings), trigger actions, game
    state queries, map tile actions, a Content Patcher token and hot reload (`patch reload` keeps an open menu open).
  - Named tooltips (`Owners` > `Tooltips`), per-row images (`${row.sprite}`), `ShowOverMenus` for HUD widgets, and
    `SharedState` to let other packs write your `config.*` / `player.*` values.
  - `ui_validate` points at every mistake by path, and `ui_schema` gives your editor autocomplete.
- Mod authors:
  - Menus from typed handles (`AddGrid`, `AddButton`, `AddDropdown`, ...), getter/setter bindings, callbacks for every
    interaction, and custom components through an interface.
  - Extension slots so other mods can add UI to your screens, shareable composite components, a sortable / filterable
    data grid, signals and auto-generated forms with undo / redo, rich text and rich tooltips, item images (flavored
    goods keep their colors), and HUD widgets.
  - Keep your screens in JSON and offer your code to data by name: commands, functions, row sources, models
    (`ExposeModel`, or `ExposeModelSource` for a per-player model), draw hooks and composites.
  - Adaptive layout: `Resizable`, `Wrap`, `MinWidth` / `MaxWidth` and `Shrink`.
  - An in-game inspector that exports any menu, C# or data, as C# code or JSON.

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
