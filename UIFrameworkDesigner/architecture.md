# UI Framework Designer – Architecture

This document describes what has to be built to ship the **UI Framework Designer**: a static React website, hosted on GitHub Pages, where modders visually build menus for the UI Framework mod (`6135.UIFramework`, `StardewUIFramework/`) and export them as **JSON** (a `Mods/6135.UIFramework/Menus` entry, a Content Patcher patch or a standalone `From` file) or as **C#** builder code against `IStardewUIApi`.

The reference for "what a menu can be" is the framework's data format (`StardewUIFramework/Data/Model/**`, README "Data-driven UIs"). The acceptance test is the `[CP] UI Framework Example` pack: every menu in it must import into the designer, show a recognisable preview, and export back to JSON that the framework validates (`ui_validate`) with no new messages.

Background reading:

- Framework architecture: `StardewUIFramework/architecture.md` (§17 covers the data format)
- Framework README, "For modders" → "Concepts" and "API reference"
- GitHub Pages with Actions: <https://docs.github.com/en/pages/getting-started-with-github-pages/using-custom-workflows-with-github-pages>

---

## 0. Status

Progress against §14. Update this table when a phase changes; §14 stays the plan, this is the scorecard.

| Phase | State | Notes |
|---|---|---|
| 0 — Skeleton + metadata export | Done | `tools/DesignerMetadata`, `Data/Model/DataDefaults.cs`, `designer.yml` (gh-pages) |
| 1 — Document model, tree editor, inspector | Done | Not yet used on a real menu |
| 2 — JSON import / export, validation | Done | Both example files round-trip; owner templates are not loaded (info messages) |
| 3 — Schematic preview | Built, unverified | No `ui_dump` fixtures yet |
| 4 — C# export | Not started | |
| 5 — Data features, sharing, polish | Not started | |
| 6 — Optional: game-art preview, live in-game loop | Not started | |
| 7 — Workspace tabs and cross-references (§18) | Built, unverified in the browser | Steps 1–5 except autosave; the example content.json round-trips (Menus and Owners equal) and opens with no problems |

---

## 1. Goals and non-goals

### Goals

1. **Build menus without hand-writing JSON.** Add, nest, reorder, duplicate and delete elements; edit every field the data format accepts through typed widgets; undo / redo everything.
2. **Round-trip existing data.** Import any `Menus` entry, a whole CP `content.json` (pick the `EditData` patch and entry) or a `From` file, JSON with comments included, and export it back without losing fields the designer does not understand.
3. **Two outputs.** JSON in the three shapes above; C# builder code that recreates the static part of the menu through the public API.
4. **One source of truth.** Field lists, element types, enums and descriptions come from metadata generated from the framework assembly, never from hand-maintained tables in the website (§4).
5. **Zero backend.** A static site on GitHub Pages; everything runs in the browser and nothing a user builds leaves their machine unless they share a link.

### Non-goals (v1)

- **Pixel-accurate preview.** The preview is schematic (§7). The game's art cannot be redistributed, and a second full renderer would drift from the framework.
- **Evaluating data features.** Expressions, `State`, `Computed`, `Watch`, sources and actions are edited as text and previewed with placeholders or user-supplied sample values, not executed.
- **Editing C#.** C# is an export target only; importing C# builder code is out of scope.
- **Huds, Owners, Sprites, Composites, Contributions assets.** v1 edits `Menus` entries (owner templates are read for preview only). The document model is shaped so these can follow (§15).

---

## 2. Constraints that shape the design

- **Every data field is a string.** `MenuDefinition` / `ElementDefinition` members are `string?` so any field can hold an expression (`"Visible": "menu.tab == 2"`). The schema therefore types them as strings; the designer's widgets parse and format the typed value and fall back to an expression editor when the text is not a literal (§6.2).
- **Shorthands.** `{ "Label": "Hi" }`, `{ "Button": "OK" }`, `{ "Checkbox": "Enabled" }`, `{ "Image": "sprite:Owner/name" }` set the type and its main value; a definition with only `Children` is a vertical stack. Import expands them (same rules as `DataValidator`); export collapses them again by default.
- **JSON with comments and CP tokens.** Packs use comments, trailing commas and tokens (`{{ModId}}`, `{{i18n:key}}`). Import uses a JSONC parser; tokens are opaque text, shown as chips in the preview.
- **Templates and custom tags.** `"Type": "<template>"` and dotted custom tags (`"Type": "Mod.Name"`) are valid element types. The designer treats them as opaque nodes with free-form arguments unless the template is defined in the same document.
- **Static hosting.** No server, no secrets, no game files. GitHub Pages serves the site under `https://6135.github.io/StardewValleyMods/designer/` (the `designer/` folder of `gh-pages`), so the Vite `base` is a sub-path.
- **Game art is copyrighted.** Nothing from the game's `Content` folder is committed or served. A user may point the designer at their own unpacked content (§7.3); it stays in the browser.

---

## 3. What the framework already provides

| Piece | Where | Used for |
|---|---|---|
| JSON Schemas of every definition model, with XML-doc descriptions | `Data/Loading/SchemaWriter.cs` (`ui_schema`) | Inspector fields, tooltips, Ajv validation |
| Element types and the members each one reads; style fields per leaf; `Out` keys | `Data/Model/ElementTypes.cs` | Palette, which fields the inspector shows per type, "unused field" warnings |
| Shorthand expansion, type canonicalisation, "did you mean" | `Data/Loading/DataValidator.cs` | Ported to TypeScript for import and the problems pane |
| Data-format defaults (spacing 8, panel padding 16, viewport 300, image scale 4 …) | `Core/Export/TreeModelReader.cs`, `Data/Building/DataBuilder.cs` | Default display and omission on export |
| C# emitter rules: constructor arguments per kind, reserved names, `TODO` for delegates | `Core/Export/CSharpEmitter.cs` | Ported to TypeScript for the C# export (§9.2) |
| `ui_export … [json]` from a live menu | `Core/TreeExporter.cs`, `Hosting/DebugConsole.cs` | Zero-code C# fallback: load the JSON in game, `ui_open`, `ui_export` |
| Hot reload of `Menus` and imported files | `ui_reload`, `ImportDataFile(path, watch: true)` | Optional live in-game loop (§10) |

---

## 4. Metadata pipeline (the single source of truth)

The website never lists fields or types by hand. A small generator in the framework repository writes a **metadata bundle** that the designer imports at build time:

```
UIFrameworkDesigner/src/generated/
  menu.schema.json        ← SchemaWriter (unchanged output)
  element-types.json      ← ElementTypes: types, Common, Specific, LeafStyle, OutKeys, container flags
  defaults.json           ← data-format defaults (one table shared with TreeModelReader / DataBuilder)
  csharp-map.json         ← per kind: Add* method, constructor argument order (CSharpEmitter.Consumed), setter names
  framework-version.txt   ← manifest version the bundle was generated from
```

- **Generator.** A `tools/DesignerMetadata` console project referencing `UIFramework.dll` (through `InternalsVisibleTo`) calls `SchemaWriter.Write` and serialises the other tables. The tables it reads (`ElementTypes.Specific`, `CSharpEmitter.Consumed`, the default constants) are made accessible as data rather than duplicated, so the framework and the designer change together.
- **Committed output.** The bundle is committed under `src/generated/` so the Pages build does not need the game or SMAPI. Regenerate it whenever a definition model changes; the designer shows `framework-version.txt` in its footer and import warns when a document uses fields the bundle does not know.
- **No runtime fetch.** Bundled with the app; the site works offline once loaded.

---

## 5. High-level architecture

```
                ┌──────────────────────────── browser ─────────────────────────────┐
 import         │                                                                   │   export
 JSONC / CP ───►│  io/import ──► Document store (normalised tree, undo/redo) ──► io/export ──► JSON (entry / CP patch / From)
 share link ───►│                   ▲        │                                      │         └──► C# (codegen/csharp)
                │                   │        ├──► validate (Ajv + ported rules) ──► Problems pane
                │  Inspector ───────┘        ├──► layout (TS measure/arrange) ──► Preview canvas
                │  Tree / Palette ───────────┘                                    │
                │                         generated/ metadata (§4)                 │
                └───────────────────────────────────────────────────────────────────┘
```

- **Document store** (Zustand + Immer, one undo history per workspace tab) holds the only editable state. Every pane is a view of it; every edit is one undoable command.
- **io**, **validate**, **layout** and **codegen** are pure TypeScript modules with no React imports, so they can be unit-tested and reused by a CLI later.
- **React** renders panes: Palette, Tree, Preview, Inspector, Problems, Export dialog, raw JSON tab.

### 5.1 Libraries (decisions, not options)

| Need | Choice | Why |
|---|---|---|
| Build | Vite + React + TypeScript (strict) | Static output, fast dev server |
| State + undo | Zustand + Immer, per-tab history of tab snapshots | Small, command-friendly, structural sharing makes snapshots cheap |
| Tree drag and drop | dnd-kit (sortable tree) | Accessible, keyboard support |
| JSONC parse / edit | `jsonc-parser` (Microsoft) | Comments, trailing commas, error offsets |
| Schema validation | Ajv (draft-07, as `SchemaWriter` writes) | Precompiled validators |
| Raw JSON tab | CodeMirror 6 + JSON schema hints | Much lighter than Monaco |
| Share links | `lz-string` in the URL hash | No server; the hash never reaches GitHub |
| Styling | CSS modules + CSS variables | Light / dark themes without a UI kit |

---

## 6. Document model and editor

### 6.1 Document

```ts
interface DesignerDocument {
  owner: string;                 // "{{ModId}}" or a literal mod id; the Menus key is `${owner}/${menuId}`
  menuId: string;
  menu: MenuFields;              // MenuDefinition minus Children, as raw strings
  root: NodeId;                  // a synthetic root holding the menu's Children
  nodes: Record<NodeId, Node>;
  templates: Record<string, TemplateDoc>; // menu-level Templates, edited like menus
  previewState: Record<string, string>;   // sample values for expressions (§7.2), never exported
}

interface Node {
  id: NodeId;                    // designer-internal, stable across edits; not the element's "Id"
  type: string;                  // canonical type, template name or custom tag
  fields: Record<string, string>;     // every known field, raw string as in the data format
  extra: Record<string, unknown>;     // unknown members kept verbatim (round-trip)
  children: NodeId[];
  shorthand?: string;            // which shorthand the element was imported with, re-used on export
}
```

- **Normalised tree.** Nodes are stored flat and referenced by id, so moves, undo and selection are cheap and React re-renders only the changed subtree.
- **Round-trip.** Unknown members go to `extra` and are written back unchanged. Comments are not preserved in v1 (the export says so when the import had any); member order follows the framework's own JSON emitter order.
- **Sub-items.** A Form's `Fields` and a DataGrid's `Columns` are child nodes of synthetic types `FormField` / `Column` (one table, `model/subItems.ts`); a Column's children are its `Cell` elements. Import, export (with the validation pointer map), tree rules, palette, inspector (the item's schema definition) and preview all read that table.
- **Element `Id`.** Kept as a normal field. The designer suggests unique ids (`label1`, `button2`) and the problems pane flags duplicates among siblings.

### 6.2 Inspector

The inspector shows the fields `element-types.json` lists for the node's type (common fields in collapsible groups: Layout, Grid cell, Events, Advanced) with descriptions from the schema. Each field has a widget chosen from metadata:

| Field shape | Widget | Example fields |
|---|---|---|
| bool | toggle | `Horizontal`, `DrawBox`, `Wrap` |
| int / float | number with step | `Spacing`, `Width`, `Scale` |
| enum | select | `HorizontalAlign`, `Font`, `Alignment` |
| margin / padding | four-sided box editor | `Margin`, `Padding` |
| color | swatch + text (`r,g,b[,a]`, names, theme colors) | `Color`, `Tint` |
| grid tracks | track list (`auto`, `px`, `*`) | `Columns`, `Rows` |
| sprite | owner sprite picker or `Texture` + `Source` rect | `Sprite`, `Image`, `Icon` |
| action list | ordered list of action strings | `OnClick`, `OnValueChanged` |
| everything else | text | `Text`, `Placeholder` |

Every widget has an **ƒx** toggle: on, the field is a plain expression text box (syntax-highlighted, with known scope roots `menu.`, `session.`, `player.`, `config.`, `stat.`, `args.`, row aliases). A value that does not parse as the widget's literal type opens in expression mode automatically, so nothing the designer imports becomes uneditable.

Field shapes that the schema cannot express (all fields are strings) come from a small `fieldShapes.ts` keyed by field name. It is the only hand-written metadata, and the generator warns when a schema field has no entry so it cannot fall behind silently.

### 6.3 Tree and palette

- Palette lists `ElementTypes.All` grouped (Layout, Text, Inputs, Collections, Structure) plus the document's templates.
- Drag from the palette or the tree; the drop is refused (with a reason) where the target cannot hold children (`ElementTypes.IsContainer`), so invalid trees cannot be built.
- Keyboard: arrows move the selection, `Ctrl+D` duplicates, `Del` deletes, `Alt+↑/↓` reorders, `Ctrl+Z / Ctrl+Y` undo / redo.
- Selection is shared by tree, preview and inspector.

---

## 7. Preview

### 7.1 Layout

A TypeScript port of the framework's measure / arrange pass (`Core/LayoutEngine.cs` and each component's `Measure`), limited to what decides placement:

- **Containers, ported exactly:** Stack (spacing, alignment, wrap), Grid (`auto` / px / star tracks, spans, the two-pass measure), Panel (padding), Canvas (absolute `X` / `Y`), ScrollView (viewport height and clipping), Spacer, the menu window (insets, title banner, `Width` / `Height`, anchor).
- **Leaves, approximated:** text is measured with a canvas font whose metrics are tuned to the game's small / dialogue fonts; inputs, checkboxes, dropdowns and sliders use the framework's fixed sizes.
- **Fixtures keep it honest.** `ui_dump` prints the arranged bounds of a live menu. A handful of example menus are dumped once in game and committed as `fixtures/*.layout.json`; the TS layout is checked against them, with a tolerance for text width.

The preview is drawn with DOM elements (absolute positioned), not a `<canvas>`, so hover, selection outlines, and click-to-select are plain DOM events.

### 7.2 Data features in the preview

| Feature | Preview |
|---|---|
| Expression in a text field | Shown as `ƒ menu.count`; evaluated if every name has a sample value in the **Preview state** panel (a tiny evaluator for literals, paths, `+ - * / == != && ||`, string interpolation) |
| `Visible` / `If` / `Condition` | Shown, with a toggle to hide elements whose sample value is false |
| `Switch` / `Case` | Case picker in the inspector; the chosen case renders |
| `Repeat`, `List`, `DataGrid` | The row template renders N times (N set in the inspector, default 3) with `${row.*}` shown as placeholders |
| Template instances | Expanded from document templates; unknown templates and custom tags render as labelled boxes sized by `Width` / `Height` |
| `{{i18n:key}}` | The key as a chip, or the text when the user loads an `i18n/default.json` |

### 7.3 Skins

- **Schematic (default):** flat boxes in the framework's default theme colors (`assets/themes.json`, which belongs to this repository and can be bundled), so the layout and hierarchy read clearly.
- **Game art (optional, §14 phase 6):** the user picks their unpacked `Content` folder with the File System Access API. The designer reads `LooseSprites/Cursors` and the fonts for 9-slice boxes and sprites, keeps them in memory only, and falls back to schematic when unsupported (Firefox, Safari).

---

## 8. Validation

1. **Schema:** Ajv with the generated `menu.schema.json`.
2. **Ported rules:** unknown type (with "did you mean"), field not read by this type (`ElementTypes.Uses`), style field ignored by this leaf (`UsesStyle`), duplicate sibling ids, `Out` keys outside `OutKeys`, children under a non-container, missing required template params.
3. **Messages use the framework's path format** (`Children[2].Children[0].Spacing`) so they match `ui_validate` output, and clicking one selects the node.

The in-game validator stays authoritative. The designer catches structural mistakes early and does not try to check expressions or game data.

---

## 9. Export

### 9.1 JSON

| Shape | Output |
|---|---|
| Menus entry | `{ "<owner>/<menuId>": { … } }` |
| CP patch | A full `EditData` change targeting `Mods/6135.UIFramework/Menus`, ready to paste into `Changes` |
| `From` file | The bare `MenuDefinition`, for packs that keep each menu in its own file |

Options: collapse shorthands (default on), omit fields equal to the data-format default (default on), indent width. Output is copied to the clipboard or downloaded as a file.

### 9.2 C#

A TypeScript emitter that writes the same shape of code as `CSharpEmitter`: one `api.Add*` call per element with the parent variable, the element id and the constructor arguments from `csharp-map.json`, then a setter per non-default field, all inside a `CreateMenu` / options block.

- **Static subset maps directly.** Literals become typed C# (`true`, `8`, `new Color(…)`, enum members).
- **Data-only constructs do not.** Expressions, actions, `State`, `Repeat`, sources and templates have no direct builder equivalent. Each becomes a `/* TODO */` with the original text in a comment, the same convention `CSharpEmitter` uses for delegates.
- **Alternative output for data-heavy menus:** a C# snippet that calls `api.ImportData(json)` with the exported JSON as a raw string literal. The export dialog suggests it when the menu has more than a few `TODO`s.
- **Consistency check:** for every example menu, the TS output is compared to `ui_export` of the same menu loaded in game, committed as `fixtures/*.cs`. Differences are either fixed or listed as known.

---

## 10. Optional live in-game loop

In Chromium browsers the designer can save straight to a file in the user's mod folder (File System Access API). A C# consumer that loads it with `ImportDataFile(path, watch: true)`, or a CP pack plus `ui_reload`, shows the real menu in game seconds after an edit. This gives exact rendering with no second renderer. The designer only writes the file the user picked, and never anything else.

---

## 11. Persistence and sharing

- **Autosave:** the current document in `localStorage` (wrapped in `try / catch`; the app works without it). One slot per document, plus a "recent documents" list.
- **Share link:** the document compressed with `lz-string` in the URL **hash**, so it is never sent to GitHub. Links over about 8 KB fall back to "download JSON".
- **Files:** open / save `.json` documents. The saved file is plain exported JSON plus an optional `"$designer"` member (preview state, collapsed groups) that the framework ignores as an unknown member.

---

## 12. Project layout and repository changes

```
UIFrameworkDesigner/
  architecture.md
  README.md
  package.json, vite.config.ts, tsconfig.json, index.html
  src/
    generated/            ← metadata bundle (§4), committed, never edited by hand
    model/                ← DesignerDocument, node ops, store (Zustand), history
    io/                   ← import (JSONC, CP content.json, shorthand expansion), export (JSON shapes)
    validate/             ← Ajv wiring + ported DataValidator rules
    layout/               ← measure / arrange port, text metrics
    preview/              ← DOM renderer, skins, sample-state evaluator
    codegen/              ← C# emitter
    ui/                   ← React panes: Palette, Tree, Preview, Inspector, Problems, Export, RawJson
    fieldShapes.ts        ← the one hand-written table (§6.2)
  fixtures/               ← example menus, *.layout.json from ui_dump, *.cs from ui_export
tools/DesignerMetadata/   ← console project that writes src/generated (§4)
.github/workflows/designer.yml
```

Framework changes (small, no behaviour change):

- `InternalsVisibleTo("DesignerMetadata")` in `UIFramework.csproj`.
- The data-format defaults move from private constants in `TreeModelReader` into one shared table used by `TreeModelReader`, `DataBuilder` and the generator.
- `CSharpEmitter.Consumed` and the kind → `Add*` method mapping are exposed as data.

Repository:

- `.github/workflows/designer.yml`: on pushes to `master` touching `UIFrameworkDesigner/**` (or manually), run `npm ci && npm run build` and push `dist/` to the `gh-pages` branch with `peaceiris/actions-gh-pages` (same setup as the WhoIsMe repository). Leave `build.yml` and `publish.yml` unchanged.
- The root README gets a row and a link to the site.

---

## 13. Build and deploy

- `npm run dev`: Vite dev server.
- `npm run build`: type-check plus `vite build` with `base: '/StardewValleyMods/designer/'`.
- `npm run metadata`: runs `dotnet run --project tools/DesignerMetadata` against a local framework build (needs the game installed, like the mod build). CI does not run it; it only builds the site from the committed bundle.
- Pages source: "Deploy from a branch", `gh-pages` / root, in the repository settings (a one-time manual step after the first deploy creates the branch).

---

## 14. Implementation plan

### Phase 0 — Skeleton + metadata export
Vite app with the empty pane layout, Pages workflow deploying it, `tools/DesignerMetadata` writing the bundle, framework tables exposed (§12). Done when the deployed site lists every element type from `element-types.json`.

### Phase 1 — Document model, tree editor, inspector
Store with undo / redo, palette, tree with drag and drop and keyboard, inspector with every widget kind and the ƒx toggle, menu-level fields. Done when the Minimal example menu from the README can be rebuilt by hand.

### Phase 2 — JSON import / export, validation
JSONC import (entry, CP `content.json` with patch / entry picker, `From` file), shorthand expansion and collapse, unknown-member round-trip, the three export shapes, Ajv + ported rules, problems pane. Done when every menu in `[CP] UI Framework Example` and `ProfitCalculator/assets/ui.json` imports and re-exports semantically equal (compared after normalisation).

### Phase 3 — Schematic preview
Layout port, DOM renderer, click-to-select, `ui_dump` fixtures. Done when the example menus' arranged bounds match the fixtures within tolerance.

### Phase 4 — C# export
TS emitter, `TODO` handling, `ImportData` alternative, `ui_export` fixtures. Done when the static example menus match `ui_export` byte for byte (modulo the known-differences list).

### Phase 5 — Data features, sharing, polish
Preview state panel and evaluator, Repeat / Switch / template expansion in the preview, i18n file loading, autosave, share links, raw JSON tab, light / dark theme, phone-width read-only layout.

### Phase 6 — Optional
Game-art skin from a local `Content` folder; save-to-mod-folder live loop (§10).

### Phase 7 — Workspace tabs and cross-references
See §18. Done when the whole `[CP] UI Framework Example/content.json` imports as one workspace (every menu and the owner entry in tabs), its `tab` / `section` template instances and named tooltips preview from their definitions with no info messages, and the workspace exports back to an equivalent `content.json`.

---

## 15. Roadmap beyond v1

- Edit the other assets (`Huds`, `Owners` templates and classes, `Sprites`, `Composites`, `Contributions`) with the same tree / inspector. The document model already separates fields from children.
- Multi-menu projects: promoted to phase 7 (§18).
- A CLI (`npx uifw-export menu.json --cs`) reusing `io/` and `codegen/`.
- A sprite picker that browses `Sprites` entries and, with game art loaded, `Cursors` regions visually.

---

## 16. Verification

- **Round-trip:** import → export of every fixture menu is semantically equal to the input.
- **In game:** exported JSON passes `ui_validate` with no new messages, and the menu opens with `ui_open` and looks like the preview's layout.
- **Layout:** arranged bounds match the `ui_dump` fixtures.
- **C#:** emitted code matches the `ui_export` fixtures and compiles in `UIFrameworkExample`.
- **Site:** works at phone width (read-only), keyboard-only, light and dark, and offline after the first load.

---

## 17. Risks and decisions

| Risk / decision | Mitigation / choice |
|---|---|
| Metadata drifts from the framework | Generated bundle (§4); version shown in the app; unknown-field warning on import; the generator flags schema fields missing from `fieldShapes.ts` |
| Two layout engines diverge | Port only placement logic; fixtures from `ui_dump`; the live in-game loop (§10) is the exact preview |
| Comments lost on round-trip | Stated in the export dialog; v2 could edit the JSONC text in place with `jsonc-parser` edits instead of re-serialising |
| C# cannot express data features | `TODO` convention plus the `ImportData` alternative; JSON is the primary output |
| Copyrighted art | Schematic skin by default; game art only from the user's own folder, in memory |
| Share links get large | Compressed hash, file download beyond ~8 KB |
| All fields are strings | Typed widgets with the ƒx fallback; nothing imported becomes uneditable |
| Hosting in the mods repository | Keeps the generated metadata next to the framework it describes; path-filtered workflow so mod builds are unaffected. Can move to its own repository later without code changes |

---

## 18. Workspace tabs and cross-references (phase 7)

Work on several menus at once, and on the owner-level definitions they share, in one **workspace** shown as tabs. A menu can reference another tab's definition (a template, a named tooltip, another menu), and the preview, validation and export resolve the reference across tabs.

### 18.1 What can reference what (the framework's own mechanisms, nothing new)

| Reference | Written as | Defined in |
|---|---|---|
| Owner template instance | `"Type": "tab"` (+ argument fields), `Outlet` children | `Owners[owner].Templates[name]` |
| Menu template instance | the same, resolved first | the menu's `Templates` |
| Named tooltip | `"RichTooltip": { "From": "crop" }` | `Owners[owner].Tooltips[name]` (blocks, `When`, nested `From`) |
| Style class | `"Class": "hint"` | `Owners[owner].Classes[name]` |
| Another menu | `6135.UIFramework_OpenMenu owner/menu`, `_OpenMenuAsChild`, `_ToggleMenu` in an action | a `Menus` entry |
| Named sprite | `"sprite:owner/name"` | `Sprites[owner/name]` |
| Data composite (later) | dotted custom tag `"Type": "Mod.Name"` | `Composites[name]` |

A "complex tooltip" is therefore a named tooltip in its own tab: edit its blocks visually (title, lines, item, money, icon, divider, `When`), and every element that points at it with `From` previews it on hover.

### 18.2 Model

```ts
interface Workspace {
  owner: string;                         // default owner for new tabs ("{{ModId}}")
  tabs: WorkspaceTab[];                  // order = tab strip order
  activeTab: TabId;
}

type WorkspaceTab =                      // every tab's `doc` is its unit of editing and undo
  | { id: TabId; kind: 'menu'; doc: DesignerDocument; patch?: PatchMembers }
  | { id: TabId; kind: 'template'; owner: string; name: string; doc: OwnerTemplateDoc }   // TemplateDoc + nodes + previewState
  | { id: TabId; kind: 'tooltip'; owner: string; name: string; doc: TooltipDoc }          // "Tooltip" root, block nodes
  | { id: TabId; kind: 'owner'; owner: string; doc: OwnerDoc; patch?: PatchMembers };   // delay, classes, hotkeys, style, shared state
```

The workspace also keeps, verbatim, the imported content.json's other root members (`content`) and the changes it does not edit (`otherChanges`: Sprites, Composites, other assets). `patch` holds the EditData members an entry came with (LogName, When …) so export re-creates one change per asset and patch.

- Menus, owner templates and tooltips are all `NodeTree`s (`root` + `nodes`), so the tree, palette, inspector and `model/ops.ts` edit every kind. The store (`model/store.ts`, zundo removed) keeps per tab a history of the tab's previous snapshots, so undo never jumps tabs; selection and collapsed nodes are per-tab UI state. The existing node / menu actions keep their names and act on the active tab.
- `TooltipDoc` is a block tree (`TooltipBlockDefinition`): block nodes have the block kind as type (the schema's Type enum is the palette), `When`, `Color` … are ordinary fields; a tooltip written as a bare array / string is written back that way.
- The **resolver** (`src/model/resolve.ts`, pure, memoised on the tab list) indexes every definition and reference: `resolve(kind, owner, name, fromTab)` (menu templates first, then the owner's), `usages`, `names` (pickers, did-you-mean), `knows` (whether the workspace holds that owner's entry; otherwise the answer is `'external'`). Validation passes the owner templates to the ported rules; the preview gets `previewDocument(ws, tab)`: the tab as one self-contained document with the owner templates it can instantiate added after its own (DataBuilder's lookup order), so `layout/build.ts` expands them unchanged. A template tab previews its body; a tooltip tab previews a stand-in element that shows the tooltip on hover.
- Per the framework (`DataBuilder.CompileTooltip`), only an element's `RichTooltip.From` is followed, one level: a named tooltip's own `From` is not read (validation warns), so tooltips cannot form cycles.

### 18.3 UI

- **Tab strip** above the preview: one tab per menu / template / tooltip / owner entry, with a type icon, dirty dot, close, drag to reorder, and "+" (new menu, template, tooltip). Each tab keeps its own selection, collapsed tree nodes and preview settings.
- **Workspace pane** (a section above the tree): every definition grouped by kind, with a usage count; click opens its tab.
- **Go to definition:** a reference field (template type, `From`, `Class`, a menu id in an action) gets a "→" button that opens the defining tab and selects the definition; the inspector of a definition lists **Used by** (tab and node) with links back.
- **Pickers:** reference fields offer the names the workspace defines (template types in the palette under "Templates", tooltip names in `RichTooltip.From`, classes in `Class`, menu ids in `_OpenMenu` actions).
- **Rename:** renaming a template, tooltip, class or menu updates every reference in the workspace in one undoable step (a cross-tab command that records one history entry per touched tab).
- **Preview:** template instances render the resolved template; hovering an element with a `RichTooltip` (named or inline) shows the tooltip rendered from its blocks (§7 schematic skin); a button whose action opens another menu shows a link badge that opens that tab.

### 18.4 Import / export

- Importing a CP `content.json` offers "Open all as a workspace": every `Menus` entry becomes a menu tab, each `Owners` entry an owner tab plus one tab per template and tooltip. Single-menu import (today's picker) stays.
- Export per tab (today's dialog) and **Export workspace**: a full `content.json` with one `EditData` change per asset (`Menus`, `Owners`, later `Sprites`) in a stable order; or an `ImportData` file (`{ "Menus": …, "Owner": … }`) for C# mods.
- Workspaces save to a `.uifw.json` file (the exported `content.json`, fields as written, plus a `$designer` member with tab order, active tab and per-tab preview state) and autosave to `localStorage`, so a saved workspace is still a valid CP file. `io/workspace.ts` has `serializeWorkspace(ws)` / `parseWorkspace(text)` for autosave and share links.
- Order of a workspace content.json: Owners changes, Menus changes (one per distinct patch, in tab order), then the kept changes verbatim. Entries keep their data; members follow the emitter order (§6.1).

### 18.5 Validation

- The resolver removes today's info message "'tab' is not a built-in type or a template of this menu" when the owner tab defines it.
- New checks: unknown template / tooltip / class / menu / sprite reference (with "did you mean" over workspace names) when the workspace holds that owner's entry (or named sprites), required params of owner templates missing, recursive templates, and owner templates / tooltips / classes nothing uses (info). References to owners the workspace does not hold are not reported (templates keep today's info message).
- The problems pane gains a scope switch: active tab or whole workspace.

### 18.6 Steps

1. `Workspace` model + per-tab history in the store; tab strip; existing single-document flow becomes a one-tab workspace (no behaviour change).
2. Resolver; layout / validation take it; owner template tabs; workspace import of a `content.json`.
3. Named tooltip tabs with the block editor; tooltip hover preview; `RichTooltip.From` picker.
4. Go to definition, Used by, rename across tabs; menu links in actions.
5. Workspace export (`content.json`, `ImportData`), `.uifw.json` save / open, autosave.

### 18.7 Risks

| Risk | Mitigation |
|---|---|
| References can name another owner (`OtherMod/menu`) the workspace does not hold | Resolved as "external": a labelled placeholder in the preview and an info message, never an error |
| One history per tab vs. renames across tabs | Rename is a single workspace command that writes one entry per touched tab and undoes them together |
| Large packs make the preview of nested templates slow | Resolver results are memoised per workspace revision; layout of other tabs is never computed |

