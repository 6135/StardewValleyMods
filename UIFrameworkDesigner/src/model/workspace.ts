import type { DesignerDocument, DesignerNode, NodeId, NodeTree, TemplateDoc } from './document';
import { createEmptyDocument, createNode, newNodeId } from './factory';
import { preorder } from './ops';

// The workspace (architecture.md §18.2): the open tabs, each a menu or one of the owner-level definitions menus share
// (an owner template, a named tooltip, the rest of an Owners entry), or a menu template open in its own tab. Every
// tab's `doc` is its unit of editing and undo; cross-tab references are answered by model/resolve.ts.

export type TabId = string;

/** Members of the Content Patcher EditData change an entry came from (LogName, When …), kept for the workspace export. */
export type PatchMembers = Record<string, unknown>;

/** An owner template (`Owners[owner].Templates[name]`): the template and the nodes of its body. */
export interface OwnerTemplateDoc extends TemplateDoc {
  nodes: Record<NodeId, DesignerNode>;
  /** Sample values for expressions in the preview; never exported. */
  previewState: Record<string, string>;
}

/** A named tooltip (`Owners[owner].Tooltips[name]`): a "Tooltip" root whose children are its blocks. */
export interface TooltipDoc extends NodeTree {
  /** TooltipDefinition string members (From, MaxWidth). */
  fields: Record<string, string>;
  /** Other members, verbatim. */
  extra: Record<string, unknown>;
  /** Written as a bare block array / a bare string (TooltipConverter); re-used on export while the tooltip still fits it. */
  shorthand?: 'blocks' | 'text';
}

/** An Owners entry without its Templates and Tooltips (their own tabs). */
export interface OwnerDoc {
  /** Plain value members (TooltipDelayMs, SharedState). */
  fields: Record<string, string>;
  /** DefaultStyle, Classes, Hotkeys and unknown members, verbatim. */
  extra: Record<string, unknown>;
}

export interface MenuTab { id: TabId; kind: 'menu'; doc: DesignerDocument; patch?: PatchMembers }
/**
 * An owner template, or with `menu` a template of that menu tab's Templates: then owner and name mirror the menu's
 * owner and the Templates key, and doc the template with its body (identified by its root node, stable across renames)
 * and the menu's preview state. The menu document holds it; the store writes the tab's edits back and refreshes the
 * tab from the menu (menuTemplateDoc, withMenuTemplate).
 */
export interface TemplateTab { id: TabId; kind: 'template'; owner: string; name: string; doc: OwnerTemplateDoc; menu?: TabId }
export interface TooltipTab { id: TabId; kind: 'tooltip'; owner: string; name: string; doc: TooltipDoc }
export interface OwnerTab { id: TabId; kind: 'owner'; owner: string; doc: OwnerDoc; patch?: PatchMembers }

export type WorkspaceTab = MenuTab | TemplateTab | TooltipTab | OwnerTab;
export type TabKind = WorkspaceTab['kind'];

/**
 * How the preview evaluates a function C# registers (`@name(...)`, RegisterFunction), which the browser cannot run:
 * an i18n lookup of the first argument (the key when the loaded map lacks it), the first argument as text, a fixed
 * value, or unknown (the call stays a chip).
 */
export interface PreviewFunction {
  kind: 'unknown' | 'i18n' | 'first' | 'fixed';
  /** The text of a 'fixed' function. */
  value?: string;
}

/** Workspace-wide preview data (architecture.md §7.2); autosaved and shared with the workspace, never exported. */
export interface PreviewData {
  /** By function name as written after `@` (matched without case). */
  functions: Record<string, PreviewFunction>;
  /** Sample rows of the collection sources C# provides (`hook:name`, `@name`), by source key (layout sourceKey). */
  rows: Record<string, unknown[]>;
}

export interface Workspace {
  /** Default owner of new tabs ("{{ModId}}"). */
  owner: string;
  /** In tab strip order. */
  tabs: WorkspaceTab[];
  activeTab: TabId;
  /** Root members of the imported content.json other than Changes (Format, ConfigSchema …), verbatim. */
  content: Record<string, unknown>;
  /** Changes the workspace does not edit (Sprites, Composites, other assets …), verbatim and in order. */
  otherChanges: unknown[];
  /** Preview functions and sample rows; absent while none are set. */
  previewData?: PreviewData;
}

export const DefaultOwner = '{{ModId}}';

/** Owner ids, menu ids and definition names match case-insensitively (the framework's dictionaries). */
export function sameName(a: string, b: string): boolean {
  return a.trim().toLowerCase() === b.trim().toLowerCase();
}

/** A menu template's tab (a template tab scoped to a menu). */
export function isMenuTemplate(tab: WorkspaceTab): tab is TemplateTab & { menu: TabId } {
  return tab.kind === 'template' && tab.menu !== undefined;
}

/** The tab whose definitions a tab sees first: a menu template's menu, else the tab itself. */
export function scopeOf(tab: WorkspaceTab): TabId {
  return tab.kind === 'template' && tab.menu !== undefined ? tab.menu : tab.id;
}

/** The menu templates of a menu tab that are open in tabs of their own. */
export function menuTemplateTabs(tabs: readonly WorkspaceTab[], menu: TabId): (TemplateTab & { menu: TabId })[] {
  return tabs.filter((t): t is TemplateTab & { menu: TabId } => isMenuTemplate(t) && t.menu === menu);
}

/** The Templates key of the menu template whose body starts at `root`, or undefined. */
function templateKey(menu: DesignerDocument, root: NodeId): string | undefined {
  return Object.keys(menu.templates).find(k => menu.templates[k]!.root === root);
}

/** The menu template whose body starts at `root`, as a template tab's name and doc; null when the menu has none. */
export function menuTemplateDoc(menu: DesignerDocument, root: NodeId): { name: string; doc: OwnerTemplateDoc } | null {
  const name = templateKey(menu, root);
  if (name === undefined) {
    return null;
  }

  const { params, fields, extra } = menu.templates[name]!;
  const nodes = Object.fromEntries(preorder(menu, root).map(id => [id, menu.nodes[id]!]));
  return { name, doc: { params, fields, extra, root, nodes, previewState: menu.previewState } };
}

/** `menu` with the template whose body started at `root` replaced by `doc` (its key kept); unchanged when it has none. */
export function withMenuTemplate(menu: DesignerDocument, root: NodeId, doc: OwnerTemplateDoc): DesignerDocument {
  const name = templateKey(menu, root);
  if (name === undefined) {
    return menu;
  }

  const nodes = { ...menu.nodes };
  for (const id of preorder(menu, root)) {
    delete nodes[id];
  }

  const { params, fields, extra } = doc;
  return { ...menu, nodes: { ...nodes, ...doc.nodes }, templates: { ...menu.templates, [name]: { params, fields, extra, root: doc.root } }, previewState: doc.previewState };
}

/** Whether two template docs hold the same values (by reference); keeps an unchanged menu template tab as it is. */
function sameTemplateDoc(a: OwnerTemplateDoc, b: OwnerTemplateDoc): boolean {
  const ids = Object.keys(a.nodes);
  return a.root === b.root && a.params === b.params && a.fields === b.fields && a.extra === b.extra && a.previewState === b.previewState
    && ids.length === Object.keys(b.nodes).length && ids.every(id => a.nodes[id] === b.nodes[id]);
}

/**
 * The tabs with the menu templates open in tabs of their own in step with their menus, after the tabs `changed` were
 * replaced: a changed menu first takes the content of its open templates when `keepOpen` (an undo / redo restoring
 * older menu content keeps what those tabs hold), then each changed menu template tab is written into its menu, and
 * every menu template tab of a changed menu is refreshed from it (name, owner, doc), or left out when the menu no
 * longer has that template.
 */
export function linkMenuTemplates(tabs: readonly WorkspaceTab[], changed: readonly TabId[], keepOpen: boolean): WorkspaceTab[] {
  const out = [...tabs];
  const at = (id: TabId) => out.findIndex(t => t.id === id);
  const menus = new Set<TabId>();
  for (const id of changed) {
    const tab = out[at(id)];
    if (tab?.kind === 'menu') {
      const doc = keepOpen ? menuTemplateTabs(out, id).reduce((d, t) => withMenuTemplate(d, t.doc.root, t.doc), tab.doc) : tab.doc;
      out[at(id)] = doc === tab.doc ? tab : { ...tab, doc };
      menus.add(id);
    }
  }

  for (const id of changed) {
    const tab = out[at(id)];
    const i = tab && isMenuTemplate(tab) ? at(tab.menu) : -1;
    const menu = out[i];
    if (tab && isMenuTemplate(tab) && menu?.kind === 'menu') {
      out[i] = { ...menu, doc: withMenuTemplate(menu.doc, tab.doc.root, tab.doc) };
      menus.add(menu.id);
    }
  }

  return out.flatMap((t): WorkspaceTab[] => {
    if (!isMenuTemplate(t) || !menus.has(t.menu)) {
      return [t];
    }

    const menu = out[at(t.menu)];
    const found = menu?.kind === 'menu' ? menuTemplateDoc(menu.doc, t.doc.root) : null;
    if (!found || menu?.kind !== 'menu') {
      return [];
    }

    const owner = menu.doc.owner;
    return [found.name === t.name && owner === t.owner && sameTemplateDoc(t.doc, found.doc) ? t : { ...t, owner, name: found.name, doc: found.doc }];
  });
}

/** The tab that shows node `nodeId` of tab `tabId`: a menu template open in its own tab shows its body's nodes. */
export function nodeTab(tabs: readonly WorkspaceTab[], tabId: TabId, nodeId: NodeId | undefined): TabId {
  return (nodeId !== undefined ? menuTemplateTabs(tabs, tabId).find(t => nodeId in t.doc.nodes)?.id : undefined) ?? tabId;
}

export function tabOwner(tab: WorkspaceTab): string {
  return tab.kind === 'menu' ? tab.doc.owner : tab.owner;
}

/** The tab's definition name: the menu id, the template / tooltip name, the owner id. */
export function tabName(tab: WorkspaceTab): string {
  switch (tab.kind) {
    case 'menu': return tab.doc.menuId;
    case 'owner': return tab.owner;
    default: return tab.name;
  }
}

/** The node tree a tab edits, or null (an owner entry has none). */
export function treeOf(tab: WorkspaceTab): NodeTree | null {
  return tab.kind === 'owner' ? null : tab.doc;
}

export const tabKindLabels: Record<TabKind, string> = { menu: 'Menu', template: 'Template', tooltip: 'Tooltip', owner: 'Owner entry' };

export function menuTab(doc: DesignerDocument, patch?: PatchMembers): MenuTab {
  return patch ? { id: newNodeId(), kind: 'menu', doc, patch } : { id: newNodeId(), kind: 'menu', doc };
}

/** A workspace of one empty menu (the single-document flow). */
export function createWorkspace(tabs: WorkspaceTab[] = [menuTab(createEmptyDocument())], owner = DefaultOwner): Workspace {
  return { owner, tabs, activeTab: tabs[0]!.id, content: {}, otherChanges: [] };
}

export function emptyTemplate(): OwnerTemplateDoc {
  const root = createNode('Template');
  return { params: {}, fields: {}, extra: {}, root: root.id, nodes: { [root.id]: root }, previewState: {} };
}

export function emptyTooltip(): TooltipDoc {
  const root = createNode('Tooltip');
  const title = createNode('Title', { Text: 'Title' });
  root.children.push(title.id);
  return { root: root.id, nodes: { [root.id]: root, [title.id]: title }, fields: {}, extra: {} };
}

/** The base name of a new template ("template" is the built-in Template type, not a template name). */
export const NewTemplateName = 'myTemplate';

/** A new tab of `kind` for `owner`, named so it does not clash with the workspace's definitions of that kind. */
export function createTab(ws: Workspace, kind: TabKind, owner = ws.owner): WorkspaceTab {
  const taken = ws.tabs.filter(t => t.kind === kind && sameName(tabOwner(t), owner)).map(tabName);
  const base = kind === 'owner' ? owner : kind === 'template' ? NewTemplateName : kind;
  let name = base;
  for (let n = 2; taken.some(t => sameName(t, name)); n++) {
    name = `${base}${n}`;
  }

  const id = newNodeId();
  switch (kind) {
    case 'menu': {
      const doc = createEmptyDocument();
      return { id, kind, doc: { ...doc, owner, menuId: name } };
    }
    case 'template': return { id, kind, owner, name, doc: emptyTemplate() };
    case 'tooltip': return { id, kind, owner, name, doc: emptyTooltip() };
    case 'owner': return { id, kind, owner, doc: { fields: {}, extra: {} } };
  }
}
