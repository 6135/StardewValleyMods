import type { JsonExportOptions, Problem } from '../model/document';
import {
  sameName, tabKindLabels, tabName, tabOwner, type OwnerTab, type PatchMembers, type TemplateTab, type TooltipTab, type Workspace, type WorkspaceTab
} from '../model/workspace';
import { isObject, type JsonObject } from './dataFormat';
import { buildMenuObject, buildOwnerObject, buildTemplateObject, buildTooltipObject, exportJson, menuKey, printJson } from './export';
import { DesignerMember, importText } from './import';

// Workspace export and files (architecture.md §18.4): the whole workspace as a Content Patcher content.json (one
// EditData change per asset and patch, in a stable order) or an ImportData file, and the `.uifw.json` workspace file:
// that content.json plus a `$designer` member (tab order, active tab, preview state) the framework and CP ignore.

export type WorkspaceExportShape = 'contentJson' | 'importData';

const MenusAsset = 'Mods/6135.UIFramework/Menus';
const OwnersAsset = 'Mods/6135.UIFramework/Owners';

/** Saved files keep every field as written (no shorthand collapsing, no default omission). */
const asWritten: Pick<JsonExportOptions, 'collapseShorthands' | 'omitDefaults'> = { collapseShorthands: false, omitDefaults: false };

interface Entry {
  key: string;
  value: JsonObject;
  patch: PatchMembers | undefined;
}

/** The Owners entries, by owner in tab order: the owner tab's members plus the Templates and Tooltips of their tabs. */
function ownerEntries(ws: Workspace, options: Pick<JsonExportOptions, 'collapseShorthands' | 'omitDefaults'>): Entry[] {
  const owners: string[] = [];
  for (const tab of ws.tabs) {
    if (tab.kind !== 'menu' && !owners.some(o => sameName(o, tab.owner))) {
      owners.push(tab.owner);
    }
  }

  return owners.map(owner => {
    const mine = ws.tabs.filter(t => t.kind !== 'menu' && sameName(t.owner, owner));
    const ownerTab = mine.find((t): t is OwnerTab => t.kind === 'owner');
    const templates = mine.filter((t): t is TemplateTab => t.kind === 'template');
    const tooltips = mine.filter((t): t is TooltipTab => t.kind === 'tooltip');
    const extra: Record<string, unknown> = { ...ownerTab?.doc.extra };
    if (templates.length > 0) {
      extra['Templates'] = Object.fromEntries(templates.map(t => [t.name, buildTemplateObject(t.doc, t.doc.nodes, options)]));
    }

    if (tooltips.length > 0) {
      extra['Tooltips'] = Object.fromEntries(tooltips.map(t => [t.name, buildTooltipObject(t.doc)]));
    }

    return { key: ownerTab?.owner ?? owner, value: buildOwnerObject({ fields: ownerTab?.doc.fields ?? {}, extra }), patch: ownerTab?.patch };
  });
}

function menuEntries(ws: Workspace, options: Pick<JsonExportOptions, 'collapseShorthands' | 'omitDefaults'>): Entry[] {
  return ws.tabs.filter(t => t.kind === 'menu').map(t => ({ key: menuKey(t.doc), value: buildMenuObject(t.doc, options).value, patch: t.patch }));
}

/** One EditData change per distinct patch (LogName, When …), in order of first use. */
function editData(target: string, entries: Entry[], logName: string): JsonObject[] {
  const groups = new Map<string, { patch: PatchMembers; entries: JsonObject }>();
  for (const entry of entries) {
    const patch = entry.patch ?? { LogName: logName };
    const id = JSON.stringify(patch);
    const group = groups.get(id) ?? { patch, entries: {} };
    group.entries[entry.key] = entry.value;
    groups.set(id, group);
  }

  return [...groups.values()].map(({ patch, entries: e }) => {
    const { LogName, ...rest } = patch;
    return { ...(LogName !== undefined ? { LogName } : {}), Action: 'EditData', Target: target, ...rest, Entries: e };
  });
}

/** The workspace as a JSON value of the chosen shape. */
function workspaceValue(ws: Workspace, shape: WorkspaceExportShape, options: Pick<JsonExportOptions, 'collapseShorthands' | 'omitDefaults'>): JsonObject {
  const owners = ownerEntries(ws, options);
  const menus = menuEntries(ws, options);
  if (shape === 'importData') {
    const own = owners.find(o => sameName(o.key, ws.owner));
    const others = owners.filter(o => o !== own);
    const result: JsonObject = { ...ws.content };
    if (own) {
      result['Owner'] = own.value;
    }

    if (others.length > 0) {
      result['Owners'] = Object.fromEntries(others.map(o => [o.key, o.value]));
    }

    result['Menus'] = Object.fromEntries(menus.map(m => [m.key, m.value]));
    return result;
  }

  return {
    Format: '2.0.0',
    ...ws.content,
    Changes: [...editData(OwnersAsset, owners, 'UI Framework owner settings'), ...editData(MenusAsset, menus, 'UI Framework menus'), ...ws.otherChanges]
  };
}

/** The workspace as a content.json or an ImportData file. */
export function exportWorkspace(ws: Workspace, shape: WorkspaceExportShape, options: JsonExportOptions): string {
  return printJson(workspaceValue(ws, shape, options), options.indent);
}

// ---------------------------------------------------------------------------------------------------------------------
//  Workspace files (.uifw.json)
// ---------------------------------------------------------------------------------------------------------------------

interface DesignerTab {
  kind: WorkspaceTab['kind'];
  key: string;
  previewState?: Record<string, string>;
}

function tabKey(tab: WorkspaceTab): string {
  return tab.kind === 'owner' ? tab.owner : `${tabOwner(tab)}/${tabName(tab)}`;
}

/**
 * The workspace file text: the content.json as written, plus `$designer` (tab order, active tab, preview state).
 * Autosave and share links use it too (share links unindented).
 */
export function serializeWorkspace(ws: Workspace, indent = 2): string {
  const tabs: DesignerTab[] = ws.tabs.map(t => {
    const state = t.kind === 'menu' || t.kind === 'template' ? t.doc.previewState : {};
    return Object.keys(state).length > 0 ? { kind: t.kind, key: tabKey(t), previewState: state } : { kind: t.kind, key: tabKey(t) };
  });
  const active = ws.tabs.findIndex(t => t.id === ws.activeTab);
  return printJson({ ...workspaceValue(ws, 'contentJson', asWritten), [DesignerMember]: { owner: ws.owner, active, tabs } }, indent);
}

/** A workspace from a `.uifw.json` file, a content.json, an ImportData file or a single menu; null when it holds none. */
export function parseWorkspace(text: string): { workspace: Workspace | null; problems: Problem[] } {
  const result = importText(text);
  const ws = result.workspace;
  const designer = result.designer;
  if (!ws || !isObject(designer) || !Array.isArray(designer['tabs'])) {
    return { workspace: ws, problems: result.problems };
  }

  // tab order and preview state from $designer; tabs it does not list keep their place after the listed ones
  const listed = (designer['tabs'] as unknown[]).filter(isObject);
  const rest = [...ws.tabs];
  const ordered: WorkspaceTab[] = [];
  for (const entry of listed) {
    const i = rest.findIndex(t => t.kind === entry['kind'] && sameName(tabKey(t), String(entry['key'])));
    if (i < 0) {
      continue;
    }

    let [tab] = rest.splice(i, 1);
    const state = entry['previewState'];
    if ((tab!.kind === 'menu' || tab!.kind === 'template') && isObject(state)) {
      const previewState = Object.fromEntries(Object.entries(state).filter((e): e is [string, string] => typeof e[1] === 'string'));
      tab = { ...tab!, doc: { ...tab!.doc, previewState } } as WorkspaceTab;
    }

    ordered.push(tab!);
  }

  const tabs = [...ordered, ...rest];
  const active = typeof designer['active'] === 'number' ? tabs[designer['active']] : undefined;
  const owner = typeof designer['owner'] === 'string' && designer['owner'].length > 0 ? designer['owner'] : ws.owner;
  return { workspace: { ...ws, owner, tabs, activeTab: active?.id ?? ws.activeTab }, problems: result.problems };
}

/** One tab as JSON: a menu in the chosen shape; a template, tooltip or owner entry as `{ "<name>": definition }`. */
export function exportTab(tab: WorkspaceTab, options: JsonExportOptions): string {
  switch (tab.kind) {
    case 'menu': return exportJson(tab.doc, options);
    case 'template': return printJson({ [tab.name]: buildTemplateObject(tab.doc, tab.doc.nodes, options) }, options.indent);
    case 'tooltip': return printJson({ [tab.name]: buildTooltipObject(tab.doc) }, options.indent);
    case 'owner': return printJson({ [tab.owner]: buildOwnerObject(tab.doc) }, options.indent);
  }
}

// ---------------------------------------------------------------------------------------------------------------------
//  Raw JSON view of one tab
// ---------------------------------------------------------------------------------------------------------------------

/** The raw JSON view of a tab: an ImportData file holding just that tab, fields as written, so it reads back losslessly. */
export function rawTab(tab: WorkspaceTab): string {
  switch (tab.kind) {
    case 'menu': return printJson({ Menus: { [menuKey(tab.doc)]: buildMenuObject(tab.doc, asWritten).value } }, 2);
    case 'template': return printJson({ Owners: { [tab.owner]: { Templates: { [tab.name]: buildTemplateObject(tab.doc, tab.doc.nodes, asWritten) } } } }, 2);
    case 'tooltip': return printJson({ Owners: { [tab.owner]: { Tooltips: { [tab.name]: buildTooltipObject(tab.doc) } } } }, 2);
    case 'owner': return printJson({ Owners: { [tab.owner]: buildOwnerObject(tab.doc) } }, 2);
  }
}

/** The owner templates of the workspace's template tabs, for reading a menu or template on its own. */
function workspaceTemplates(ws: Workspace): Map<string, Set<string>> {
  const map = new Map<string, Set<string>>();
  for (const tab of ws.tabs) {
    if (tab.kind === 'template') {
      const key = tab.owner.trim().toLowerCase();
      map.set(key, (map.get(key) ?? new Set()).add(tab.name));
    }
  }

  return map;
}

/**
 * Read a raw JSON view back through the import: the tab it holds, of `tab`'s kind, keeping `tab`'s id, CP patch
 * members and preview state; null with the problems when the text has errors or holds no such tab.
 */
export function parseRawTab(ws: Workspace, tab: WorkspaceTab, text: string): { tab: WorkspaceTab | null; problems: Problem[] } {
  const result = importText(text, workspaceTemplates(ws));
  const errors = result.problems.filter(p => p.severity === 'error');
  const found = result.workspace?.tabs.find(t => t.kind === tab.kind);
  if (errors.length > 0 || !found) {
    return { tab: null, problems: errors.length > 0 ? errors : [{ severity: 'error', path: '', message: `the text holds no ${tabKindLabels[tab.kind].toLowerCase()}.` }] };
  }

  // the text has no preview state (never exported): the tab's own is kept
  const patch = tab.kind === 'menu' || tab.kind === 'owner' ? tab.patch : undefined;
  const doc = (found.kind === 'menu' || found.kind === 'template') && (tab.kind === 'menu' || tab.kind === 'template')
    ? { ...found.doc, previewState: tab.doc.previewState } : found.doc;
  return { tab: { ...found, doc, id: tab.id, ...(patch ? { patch } : {}) } as WorkspaceTab, problems: result.problems };
}
