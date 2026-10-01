import type { DesignerDocument, DesignerNode, NodeId, NodeTree, TemplateDoc } from './document';
import { createEmptyDocument, createNode, newNodeId } from './factory';

// The workspace (architecture.md §18.2): the open tabs, each a menu or one of the owner-level definitions menus share
// (an owner template, a named tooltip, the rest of an Owners entry). Every tab's `doc` is its unit of editing and undo;
// cross-tab references are answered by model/resolve.ts.

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
export interface TemplateTab { id: TabId; kind: 'template'; owner: string; name: string; doc: OwnerTemplateDoc }
export interface TooltipTab { id: TabId; kind: 'tooltip'; owner: string; name: string; doc: TooltipDoc }
export interface OwnerTab { id: TabId; kind: 'owner'; owner: string; doc: OwnerDoc; patch?: PatchMembers }

export type WorkspaceTab = MenuTab | TemplateTab | TooltipTab | OwnerTab;
export type TabKind = WorkspaceTab['kind'];

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
}

export const DefaultOwner = '{{ModId}}';

/** Owner ids, menu ids and definition names match case-insensitively (the framework's dictionaries). */
export function sameName(a: string, b: string): boolean {
  return a.trim().toLowerCase() === b.trim().toLowerCase();
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

/** A new tab of `kind` for `owner`, named so it does not clash with the workspace's definitions of that kind. */
export function createTab(ws: Workspace, kind: TabKind, owner = ws.owner): WorkspaceTab {
  const taken = ws.tabs.filter(t => t.kind === kind && sameName(tabOwner(t), owner)).map(tabName);
  const base = kind === 'owner' ? owner : kind;
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
