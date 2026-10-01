import { create, useStore } from 'zustand';
import { immer } from 'zustand/middleware/immer';
import { temporal } from 'zundo';
import type { DesignerDocument, NodeId } from './document';
import { createEmptyDocument } from './factory';
import * as ops from './ops';

// The document store (architecture.md §5): the only editable state. Every edit of `doc` is one undoable step (zundo
// tracks `doc` only); selection and collapsed tree nodes are UI state outside the history.

export interface DesignerState {
  doc: DesignerDocument;
  selection: NodeId | null;
  /** Tree nodes shown collapsed. */
  collapsed: Record<NodeId, true>;

  /** Replace the whole document (New, Import); one undo step. */
  replaceDocument(doc: DesignerDocument): void;
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
  setMeta(meta: { owner?: string; menuId?: string }): void;
  /** Add an element of `type` under `parentId` (default index: last) and select it; null when refused. */
  addNode(parentId: NodeId, type: string, index?: number): NodeId | null;
  /** Move a node; `index` counts the new parent's children without the node. False when refused. */
  moveNode(nodeId: NodeId, newParentId: NodeId, index: number): boolean;
  /** Duplicate a node after itself and select the copy. */
  duplicateNode(nodeId: NodeId): NodeId | null;
  deleteNode(nodeId: NodeId): void;
  /** Set a preview sample value; undefined removes it. */
  setPreviewState(key: string, value: string | undefined): void;
  undo(): void;
  redo(): void;
}

type HistoryState = Pick<DesignerState, 'doc'>;

export const useDesigner = create<DesignerState>()(
  temporal(
    immer((set, get) => ({
      doc: createEmptyDocument(),
      selection: null,
      collapsed: {},

      replaceDocument: doc => set({ doc, selection: null, collapsed: {} }),

      select: id => set({ selection: id }),

      toggleCollapsed: (id, collapsed) => set(s => {
        if (collapsed ?? !s.collapsed[id]) {
          s.collapsed[id] = true;
        } else {
          delete s.collapsed[id];
        }
      }),

      setField: (nodeId, field, value) => set(s => {
        ops.setField(s.doc, nodeId, field, value);
      }),

      setExtra: (nodeId, field, value) => set(s => {
        ops.setExtra(s.doc, nodeId, field, value);
      }),

      setMenuExtra: (field, value) => set(s => {
        delete s.doc.menu[field];
        if (value === undefined) {
          delete s.doc.menuExtra[field];
        } else {
          s.doc.menuExtra[field] = value;
        }
      }),

      setMenuField: (field, value) => set(s => {
        if (value === undefined) {
          delete s.doc.menu[field];
        } else if (s.doc.menu[field] !== value) {
          s.doc.menu[field] = value;
        }
      }),

      setMeta: ({ owner, menuId }) => set(s => {
        if (owner !== undefined && owner !== s.doc.owner) {
          s.doc.owner = owner;
        }
        if (menuId !== undefined && menuId !== s.doc.menuId) {
          s.doc.menuId = menuId;
        }
      }),

      addNode: (parentId, type, index) => {
        let id: NodeId | null = null;
        set(s => {
          id = ops.addNode(s.doc, parentId, type, index);
          if (id) {
            s.selection = id;
            delete s.collapsed[parentId];
          }
        });
        return id;
      },

      moveNode: (nodeId, newParentId, index) => {
        let moved = false;
        set(s => {
          moved = ops.moveNode(s.doc, nodeId, newParentId, index);
        });
        return moved;
      },

      duplicateNode: nodeId => {
        let id: NodeId | null = null;
        set(s => {
          id = ops.duplicateNode(s.doc, nodeId);
          if (id) {
            s.selection = id;
          }
        });
        return id;
      },

      deleteNode: nodeId => set(s => {
        const parent = ops.parentOf(s.doc, nodeId);
        if (parent === null) {
          return;
        }

        // select the next sibling, else the previous one, else the parent
        const siblings = s.doc.nodes[parent]!.children;
        const at = siblings.indexOf(nodeId);
        const next = siblings[at + 1] ?? siblings[at - 1] ?? parent;
        const selectionGone = s.selection !== null && ops.isInSubtree(s.doc, nodeId, s.selection);
        ops.deleteNode(s.doc, nodeId);
        if (selectionGone) {
          s.selection = next;
        }
      }),

      setPreviewState: (key, value) => set(s => {
        if (value === undefined) {
          delete s.doc.previewState[key];
        } else if (s.doc.previewState[key] !== value) {
          s.doc.previewState[key] = value;
        }
      }),

      undo: () => {
        useDesigner.temporal.getState().undo();
        dropStaleSelection(get, set);
      },

      redo: () => {
        useDesigner.temporal.getState().redo();
        dropStaleSelection(get, set);
      }
    })),
    {
      partialize: (s): HistoryState => ({ doc: s.doc }),
      // a step only when the document changed (Immer keeps its identity otherwise)
      equality: (a, b) => a.doc === b.doc,
      limit: 200
    }
  )
);

/** Clear a selection that undo / redo removed from the document. */
function dropStaleSelection(get: () => DesignerState, set: (partial: Partial<DesignerState>) => void): void {
  const { selection, doc } = get();
  if (selection !== null && !doc.nodes[selection]) {
    set({ selection: null });
  }
}

/** Whether undo / redo are available, for toolbar buttons. */
export function useHistory(): { canUndo: boolean; canRedo: boolean } {
  const canUndo = useStore(useDesigner.temporal, s => s.pastStates.length > 0);
  const canRedo = useStore(useDesigner.temporal, s => s.futureStates.length > 0);
  return { canUndo, canRedo };
}
