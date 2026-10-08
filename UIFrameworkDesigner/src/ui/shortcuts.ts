import { useEffect } from 'react';
import type { NodeId } from '../model/document';
import { activeTree, activeUi, useDesigner } from '../model/store';
import { focusRow } from './Tree';

// Global shortcuts (architecture.md §6.3): undo / redo, duplicate, delete. Ignored while typing in a text control,
// which keeps its own undo, and while the app is read-only (phone width).

function isTyping(target: EventTarget | null): boolean {
  return target instanceof HTMLElement
    && (target.isContentEditable || target.tagName === 'TEXTAREA' || target.tagName === 'SELECT'
      || (target.tagName === 'INPUT' && !['checkbox', 'radio', 'button', 'color'].includes((target as HTMLInputElement).type)));
}

type Shortcut = 'redo' | 'undo' | 'duplicate' | 'delete';

/** The shortcut a key press asks for: Ctrl/Cmd+Shift+Z or Ctrl/Cmd+Y, Ctrl/Cmd+Z, Ctrl/Cmd+D, Delete. */
function shortcutOf(e: KeyboardEvent): Shortcut | null {
  const mod = e.ctrlKey || e.metaKey;
  const key = e.key.toLowerCase();
  if (mod && !e.altKey) {
    if ((key === 'z' && e.shiftKey) || (key === 'y' && !e.shiftKey)) return 'redo';
    if (!e.shiftKey && key === 'z') return 'undo';
    if (!e.shiftKey && key === 'd') return 'duplicate';
  }
  return e.key === 'Delete' && !mod ? 'delete' : null;
}

type Store = ReturnType<typeof useDesigner.getState>;

/** Runs a shortcut on the selected node (null for the root or none); false when it does not apply. */
const shortcutHandlers: Record<Shortcut, (store: Store, selection: NodeId | null) => boolean> = {
  redo: store => {
    store.redo();
    return true;
  },
  undo: store => {
    store.undo();
    return true;
  },
  duplicate: (store, selection) => {
    const copy = selection !== null ? store.duplicateNode(selection) : null;
    if (copy) {
      focusRow(copy);
    }
    return true;
  },
  delete: (store, selection) => {
    if (selection === null) {
      return false;
    }
    store.deleteNode(selection);
    const next = activeUi(useDesigner.getState()).selection;
    if (next !== null) {
      focusRow(next);
    }
    return true;
  }
};

export function useGlobalShortcuts(enabled: boolean): void {
  useEffect(() => {
    if (!enabled) {
      return undefined;
    }

    const onKeyDown = (e: KeyboardEvent) => {
      const shortcut = e.defaultPrevented || isTyping(e.target) ? null : shortcutOf(e);
      if (shortcut === null) {
        return;
      }
      const store = useDesigner.getState();
      const current = activeUi(store).selection;
      const selection = current !== null && current !== activeTree(store)?.root ? current : null;
      if (shortcutHandlers[shortcut](store, selection)) {
        e.preventDefault();
      }
    };

    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [enabled]);
}
