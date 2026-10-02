import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';
import { produce, type Draft } from 'immer';
import type { DesignerDocument, NodeId, NodeTree } from './document';
import { createNode, newNodeId } from './factory';
import * as ops from './ops';
import { renameEdits, renameRefusal, resolverOf, type Definition, type TabEdit } from './resolve';
import type { PreviewSettings } from '../preview/PreviewPane';
import {
  createTab, createWorkspace, isMenuTemplate, linkMenuTemplates, menuTab, menuTemplateDoc, menuTemplateTabs, NewTemplateName, sameName, treeOf,
  type PreviewFunction, type TabId, type TabKind, type TemplateTab, type Workspace, type WorkspaceTab
} from './workspace';

// The workspace store (architecture.md §5, §18.2): the only editable state. Each tab has its own undo history (an edit
// records the tab's previous content), so undo never jumps tabs; a rename across tabs records one entry per touched
// tab under one group and undoes them together. A menu template open in its own tab is kept in step with its menu
// (linkMenuTemplates): its edits are its own undo steps written into the menu, and an undo / redo in the menu keeps
// what the template tab holds. Selection, collapsed tree nodes and preview settings are per-tab UI
// state outside the history (and never exported). The node / menu actions keep their single-document names and act on the active tab.

export interface TabUi {
  selection: NodeId | null;
  /** Tree nodes shown collapsed. */
  collapsed: Record<NodeId, true>;
  /** Screen, zoom and rows of the preview; the defaults until changed. */
  preview?: PreviewSettings;
}

interface HistoryEntry {
  tab: WorkspaceTab;
  /** Entries of one workspace command (a rename) share a group and are undone / redone together. */
  group?: number;
}

interface TabHistory {
  past: HistoryEntry[];
  future: HistoryEntry[];
}

const HistoryLimit = 200;
const emptyUi: TabUi = { selection: null, collapsed: {} };
const emptyHistory: TabHistory = { past: [], future: [] };

export interface DesignerState {
  workspace: Workspace;
  /** The workspace's autosave slot (io/autosave.ts): a replaced workspace gets a new one unless one is given. */
  slot: string;
  ui: Record<TabId, TabUi>;
  history: Record<TabId, TabHistory>;
  /** Each tab's content when the workspace was last opened or saved; a tab is dirty while it differs. */
  saved: Record<TabId, WorkspaceTab>;
  /** A loaded i18n/default.json for the preview's {{i18n:key}} text (workspace-wide, never exported). */
  i18n: Record<string, string> | null;

  setI18n(map: Record<string, string> | null): void;
  /** Replace the whole workspace (open, import all, a Recent entry with its `slot`); clears the histories. */
  replaceWorkspace(ws: Workspace, slot?: string): void;
  /** Record the current tabs as saved (clears the dirty dots). */
  markSaved(): void;
  openTab(id: TabId): void;
  closeTab(id: TabId): void;
  moveTab(id: TabId, index: number): void;
  /** Add a new tab of `kind` (named not to clash) after the active one and open it. */
  addTab(kind: TabKind): TabId;
  /** Open the template of a menu tab whose body starts at `root` in its own tab (the one already open, else a new one). */
  openMenuTemplate(menu: TabId, root: NodeId): void;
  /** Add a template to the active menu's Templates (named not to clash) and open it. */
  addMenuTemplate(): void;
  /** Open a tab and select a node in it. */
  reveal(tabId: TabId, nodeId?: NodeId | null): void;
  /** Rename a definition and every reference to it (one undoable step per touched tab, undone together); the refusal or null. */
  rename(def: Definition, name: string): string | null;

  /** Replace the active menu's document (New, Import); one undo step. Opens a new tab when the active tab is not a menu. */
  replaceDocument(doc: DesignerDocument): void;
  /** Replace the active tab's content with `tab` (same id, the raw JSON view); one undo step. */
  replaceTab(tab: WorkspaceTab): void;
  setPreviewSettings(settings: PreviewSettings): void;
  select(id: NodeId | null): void;
  toggleCollapsed(id: NodeId, collapsed?: boolean): void;
  /** Set a node field; undefined removes it. */
  setField(nodeId: NodeId, field: string, value: string | undefined): void;
  /** Set a menu field; undefined removes it. */
  setMenuField(field: string, value: string | undefined): void;
  /** Set or remove a non-string member (JSON value) of an element. */
  setExtra(nodeId: NodeId, field: string, value: unknown): void;
  /** Set or remove a non-string menu member (State, Sources, Keys …). */
  setMenuExtra(field: string, value: unknown): void;
  setMeta(meta: { owner?: string }): void;
  /** Set a string member of the active template / tooltip / owner entry (not of a node); undefined removes it. */
  setTabField(field: string, value: string | undefined): void;
  /** Set a JSON member of the active template (Params included) / tooltip / owner entry; undefined removes it. */
  setTabExtra(field: string, value: unknown): void;
  /** Add an element of `type` under `parentId` (default index: last) and select it; null when refused. */
  addNode(parentId: NodeId, type: string, index?: number): NodeId | null;
  /** Move a node; `index` counts the new parent's children without the node. False when refused. */
  moveNode(nodeId: NodeId, newParentId: NodeId, index: number): boolean;
  /** Duplicate a node after itself and select the copy. */
  duplicateNode(nodeId: NodeId): NodeId | null;
  deleteNode(nodeId: NodeId): void;
  /** Set a preview sample value of the active menu / template; undefined removes it. */
  setPreviewState(key: string, value: string | undefined): void;
  /** Set how the preview evaluates a C# function (workspace-wide, outside the histories); undefined removes it. */
  setPreviewFunction(name: string, fn: PreviewFunction | undefined): void;
  /** Set the sample rows of a C# source (workspace-wide, outside the histories); undefined removes them. */
  setSampleRows(source: string, rows: unknown[] | undefined): void;
  undo(): void;
  redo(): void;
}

/** The active tab. */
export function activeTab(s: Pick<DesignerState, 'workspace'>): WorkspaceTab {
  return s.workspace.tabs.find(t => t.id === s.workspace.activeTab) ?? s.workspace.tabs[0]!;
}

/** The active tab's UI state. */
export function activeUi(s: Pick<DesignerState, 'workspace' | 'ui'>): TabUi {
  return s.ui[s.workspace.activeTab] ?? emptyUi;
}

/** The active tab's node tree, or null (an owner entry). */
export function activeTree(s: Pick<DesignerState, 'workspace'>): NodeTree | null {
  return treeOf(activeTab(s));
}

let groupCounter = 0;

export const useDesigner = create<DesignerState>()(
  immer((set, get) => {
    /**
     * Apply `edit` to a tab (or replace it with a new tab value); when the tab changed, record its previous content in its history (one undo step) and
     * apply `after` (selection, collapsed) in the same update. False when nothing changed.
     */
    const commit = (tabId: TabId, edit: TabEdit | WorkspaceTab, group?: number, after?: (s: Draft<DesignerState>) => void): boolean => {
      const { tabs } = get().workspace;
      const tab = tabs.find(t => t.id === tabId);
      if (!tab) {
        return false;
      }

      const next = typeof edit === 'function' ? produce(tab, edit) : edit;
      if (next === tab) {
        return false;
      }

      const linked = linkMenuTemplates(tabs.map(t => (t.id === tabId ? next : t)), [tabId], false);
      set(s => {
        place(s, linked);
        const h = (s.history[tabId] ??= { past: [], future: [] });
        h.past.push(group !== undefined ? { tab: tab as Draft<WorkspaceTab>, group } : { tab: tab as Draft<WorkspaceTab> });
        if (h.past.length > HistoryLimit) {
          h.past.shift();
        }
        h.future = [];
        after?.(s);
      });
      return true;
    };

    /** Edit the active tab's node tree (no-op for an owner entry). */
    const editTree = (edit: (tree: Draft<NodeTree>) => void, after?: (s: Draft<DesignerState>) => void): boolean =>
      commit(get().workspace.activeTab, tab => {
        if (tab.kind !== 'owner') {
          edit(tab.doc);
        }
      }, undefined, after);

    /** Edit the active tab's menu document (no-op for other tabs). */
    const editMenu = (edit: (doc: Draft<DesignerDocument>) => void): boolean =>
      commit(get().workspace.activeTab, tab => {
        if (tab.kind === 'menu') {
          edit(tab.doc);
        }
      });

    /** Put the linked tabs in place, dropping the history, UI and saved state of menu template tabs they left out. */
    const place = (s: Draft<DesignerState>, tabs: WorkspaceTab[]) => {
      const kept = new Set(tabs.map(t => t.id));
      for (const t of s.workspace.tabs) {
        if (!kept.has(t.id)) {
          delete s.history[t.id];
          delete s.ui[t.id];
          delete s.saved[t.id];
          if (s.workspace.activeTab === t.id && t.kind === 'template' && t.menu !== undefined) {
            s.workspace.activeTab = t.menu;
          }
        }
      }

      s.workspace.tabs = tabs as Draft<WorkspaceTab>[];
    };

    const ui = (s: Draft<DesignerState>): Draft<TabUi> => (s.ui[s.workspace.activeTab] ??= { selection: null, collapsed: {} });

    /** Undo (from past to future) or redo the active tab's last entry, with the other entries of its group. */
    const travel = (from: 'past' | 'future') => {
      const to = from === 'past' ? 'future' : 'past';
      const { workspace, history } = get();
      const top = history[workspace.activeTab]?.[from].at(-1);
      if (!top) {
        return;
      }

      const ids = top.group === undefined ? [workspace.activeTab]
        : Object.keys(history).filter(id => history[id]![from].at(-1)?.group === top.group);
      const tabs = [...workspace.tabs];
      set(s => {
        for (const id of ids) {
          const entry = history[id]![from].at(-1)!;
          const i = tabs.findIndex(t => t.id === id);
          const h = s.history[id]!;
          h[from].pop();
          if (i < 0) {
            continue;
          }

          h[to].push(entry.group !== undefined ? { tab: tabs[i] as Draft<WorkspaceTab>, group: entry.group } : { tab: tabs[i] as Draft<WorkspaceTab> });
          tabs[i] = entry.tab;
          // drop a selection the step removed
          const tabUi = s.ui[id];
          const tree = treeOf(entry.tab);
          if (tabUi && tabUi.selection !== null && !tree?.nodes[tabUi.selection]) {
            tabUi.selection = null;
          }
        }

        place(s, linkMenuTemplates(tabs, ids, true));
      });
    };

    const initial = createWorkspace();
    return {
      workspace: initial,
      slot: newNodeId(),
      ui: {},
      history: {},
      saved: Object.fromEntries(initial.tabs.map(t => [t.id, t])),
      i18n: null,

      setI18n: map => set({ i18n: map }),

      replaceWorkspace: (ws, slot = newNodeId()) => set({ workspace: ws, slot, ui: {}, history: {}, saved: Object.fromEntries(ws.tabs.map(t => [t.id, t])) }),

      markSaved: () => set(s => {
        s.saved = Object.fromEntries(s.workspace.tabs.map(t => [t.id, t]));
      }),

      openTab: id => set(s => {
        if (s.workspace.tabs.some(t => t.id === id)) {
          s.workspace.activeTab = id;
        }
      }),

      closeTab: id => set(s => {
        // a menu closes with its templates that are open in tabs of their own
        const closing = new Set([id, ...menuTemplateTabs(s.workspace.tabs, id).map(t => t.id)]);
        const tabs = s.workspace.tabs;
        const i = tabs.findIndex(t => t.id === id);
        if (i < 0 || tabs.length === closing.size) {
          return;
        }

        if (closing.has(s.workspace.activeTab)) {
          s.workspace.activeTab = (tabs.slice(i).find(t => !closing.has(t.id)) ?? tabs.filter(t => !closing.has(t.id)).at(-1))!.id;
        }

        s.workspace.tabs = tabs.filter(t => !closing.has(t.id));
        for (const c of closing) {
          delete s.history[c];
          delete s.ui[c];
          delete s.saved[c];
        }
      }),

      moveTab: (id, index) => set(s => {
        const tabs = s.workspace.tabs;
        const from = tabs.findIndex(t => t.id === id);
        if (from >= 0) {
          const [tab] = tabs.splice(from, 1);
          tabs.splice(Math.max(0, Math.min(index, tabs.length)), 0, tab!);
        }
      }),

      addTab: kind => {
        const tab = createTab(get().workspace, kind);
        set(s => {
          const at = s.workspace.tabs.findIndex(t => t.id === s.workspace.activeTab);
          s.workspace.tabs.splice(at + 1, 0, tab);
          s.workspace.activeTab = tab.id;
        });
        return tab.id;
      },

      openMenuTemplate: (menu, root) => {
        const { tabs } = get().workspace;
        const open = menuTemplateTabs(tabs, menu).find(t => t.doc.root === root);
        const owning = tabs.find(t => t.id === menu);
        const found = owning?.kind === 'menu' ? menuTemplateDoc(owning.doc, root) : null;
        const tab: TemplateTab | undefined = open
          ?? (found && owning?.kind === 'menu' ? { id: newNodeId(), kind: 'template', owner: owning.doc.owner, name: found.name, doc: found.doc, menu } : undefined);
        if (!tab) {
          return;
        }

        set(s => {
          if (!open) {
            s.workspace.tabs.splice(s.workspace.tabs.findIndex(t => t.id === menu) + 1, 0, tab as Draft<WorkspaceTab>);
            s.saved[tab.id] = tab as Draft<WorkspaceTab>;
          }
          s.workspace.activeTab = tab.id;
        });
      },

      addMenuTemplate: () => {
        const tab = activeTab(get());
        if (tab.kind !== 'menu') {
          return;
        }

        // not shadowing an owner template either (a menu's own template wins over the owner's of the same name)
        const taken = resolverOf(get().workspace).names('template', tab.doc.owner, tab.id);
        let name = NewTemplateName;
        for (let n = 2; taken.some(t => sameName(t, name)); n++) {
          name = `${NewTemplateName}${n}`;
        }

        const root = createNode('Template');
        commit(tab.id, t => {
          if (t.kind === 'menu') {
            t.doc.templates[name] = { params: {}, fields: {}, extra: {}, root: root.id };
            t.doc.nodes[root.id] = root;
          }
        });
        get().openMenuTemplate(tab.id, root.id);
      },

      reveal: (tabId, nodeId) => set(s => {
        if (s.workspace.tabs.some(t => t.id === tabId)) {
          s.workspace.activeTab = tabId;
          if (nodeId !== undefined) {
            ui(s).selection = nodeId;
          }
        }
      }),

      rename: (def, name) => {
        const resolver = resolverOf(get().workspace);
        const refusal = renameRefusal(resolver, def, name);
        if (refusal !== null) {
          return refusal;
        }

        groupCounter += 1;
        for (const [tabId, edits] of renameEdits(resolver, def, name)) {
          commit(tabId, tab => edits.forEach(e => e(tab)), groupCounter);
        }

        return null;
      },

      replaceDocument: doc => {
        if (activeTab(get()).kind === 'menu') {
          commit(get().workspace.activeTab, tab => {
            tab.doc = doc as Draft<DesignerDocument>;
          }, undefined, s => {
            s.ui[s.workspace.activeTab] = { ...ui(s), selection: null, collapsed: {} };
          });
        } else {
          const tab = menuTab(doc);
          set(s => {
            s.workspace.tabs.push(tab);
            s.workspace.activeTab = tab.id;
          });
        }
      },

      replaceTab: tab => {
        commit(get().workspace.activeTab, { ...tab, id: get().workspace.activeTab } as WorkspaceTab, undefined, s => {
          s.ui[s.workspace.activeTab] = { ...ui(s), selection: null, collapsed: {} };
        });
      },

      setPreviewSettings: settings => set(s => {
        ui(s).preview = settings;
      }),

      select: id => set(s => {
        ui(s).selection = id;
      }),

      toggleCollapsed: (id, collapsed) => set(s => {
        const c = ui(s).collapsed;
        if (collapsed ?? !c[id]) {
          c[id] = true;
        } else {
          delete c[id];
        }
      }),

      setField: (nodeId, field, value) => {
        editTree(t => {
          ops.setField(t, nodeId, field, value);
        });
      },

      setExtra: (nodeId, field, value) => {
        editTree(t => {
          ops.setExtra(t, nodeId, field, value);
        });
      },

      setMenuExtra: (field, value) => {
        editMenu(doc => {
          delete doc.menu[field];
          if (value === undefined) {
            delete doc.menuExtra[field];
          } else {
            doc.menuExtra[field] = value;
          }
        });
      },

      setMenuField: (field, value) => {
        editMenu(doc => {
          if (value === undefined) {
            delete doc.menu[field];
          } else if (doc.menu[field] !== value) {
            doc.menu[field] = value;
          }
        });
      },

      setMeta: ({ owner }) => {
        commit(get().workspace.activeTab, tab => {
          if (owner === undefined) {
            return;
          }

          if (tab.kind === 'menu' && tab.doc.owner !== owner) {
            tab.doc.owner = owner;
          } else if (tab.kind !== 'menu' && !isMenuTemplate(tab as WorkspaceTab) && tab.owner !== owner) {
            tab.owner = owner;
          }
        });
      },

      setTabField: (field, value) => {
        commit(get().workspace.activeTab, tab => {
          if (tab.kind === 'menu') {
            return;
          }

          const fields = tab.doc.fields;
          if (value === undefined) {
            delete fields[field];
          } else if (fields[field] !== value) {
            fields[field] = value;
            delete tab.doc.extra[field];
          }
        });
      },

      setTabExtra: (field, value) => {
        commit(get().workspace.activeTab, tab => {
          if (tab.kind === 'menu') {
            return;
          }

          if (tab.kind === 'template' && field === 'Params') {
            tab.doc.params = (value ?? {}) as Record<string, unknown>;
            return;
          }

          delete tab.doc.fields[field];
          if (value === undefined) {
            delete tab.doc.extra[field];
          } else {
            tab.doc.extra[field] = value;
          }
        });
      },

      addNode: (parentId, type, index) => {
        let id: NodeId | null = null;
        editTree(t => {
          id = ops.addNode(t, parentId, type, index);
        }, s => {
          ui(s).selection = id;
          delete ui(s).collapsed[parentId];
        });
        return id;
      },

      moveNode: (nodeId, newParentId, index) => editTree(t => {
        ops.moveNode(t, nodeId, newParentId, index);
      }),

      duplicateNode: nodeId => {
        let id: NodeId | null = null;
        editTree(t => {
          id = ops.duplicateNode(t, nodeId);
        }, s => {
          ui(s).selection = id;
        });
        return id;
      },

      deleteNode: nodeId => {
        const tree = activeTree(get());
        const parent = tree ? ops.parentOf(tree, nodeId) : null;
        if (!tree || parent === null) {
          return;
        }

        // select the next sibling, else the previous one, else the parent
        const siblings = tree.nodes[parent]!.children;
        const at = siblings.indexOf(nodeId);
        const next = siblings[at + 1] ?? siblings[at - 1] ?? parent;
        const selection = activeUi(get()).selection;
        const selectionGone = selection !== null && ops.isInSubtree(tree, nodeId, selection);
        editTree(t => {
          ops.deleteNode(t, nodeId);
        }, s => {
          if (selectionGone) {
            ui(s).selection = next;
          }
        });
      },

      setPreviewFunction: (name, fn) => set(s => {
        const data = s.workspace.previewData ??= { functions: {}, rows: {} };
        if (fn === undefined) {
          delete data.functions[name];
        } else {
          data.functions[name] = fn;
        }
      }),

      setSampleRows: (source, rows) => set(s => {
        const data = s.workspace.previewData ??= { functions: {}, rows: {} };
        if (rows === undefined) {
          delete data.rows[source];
        } else {
          data.rows[source] = rows;
        }
      }),

      setPreviewState: (key, value) => {
        commit(get().workspace.activeTab, tab => {
          if (tab.kind !== 'menu' && tab.kind !== 'template') {
            return;
          }

          const state = tab.doc.previewState;
          if (value === undefined) {
            delete state[key];
          } else if (state[key] !== value) {
            state[key] = value;
          }
        });
      },

      undo: () => travel('past'),
      redo: () => travel('future')
    };
  })
);

/** Whether undo / redo are available in the active tab, for toolbar buttons. */
export function useHistory(): { canUndo: boolean; canRedo: boolean } {
  const canUndo = useDesigner(s => (s.history[s.workspace.activeTab] ?? emptyHistory).past.length > 0);
  const canRedo = useDesigner(s => (s.history[s.workspace.activeTab] ?? emptyHistory).future.length > 0);
  return { canUndo, canRedo };
}
