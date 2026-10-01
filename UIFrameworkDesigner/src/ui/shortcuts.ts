import { useEffect } from 'react';
import { activeTree, activeUi, useDesigner } from '../model/store';
import { focusRow } from './Tree';

// Global shortcuts (architecture.md §6.3): undo / redo, duplicate, delete. Ignored while typing in a text control,
// which keeps its own undo, and while the app is read-only (phone width).

function isTyping(target: EventTarget | null): boolean {
  return target instanceof HTMLElement
    && (target.isContentEditable || target.tagName === 'TEXTAREA' || target.tagName === 'SELECT'
      || (target.tagName === 'INPUT' && !['checkbox', 'radio', 'button', 'color'].includes((target as HTMLInputElement).type)));
}

export function useGlobalShortcuts(enabled: boolean): void {
  useEffect(() => {
    if (!enabled) {
      return undefined;
    }

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.defaultPrevented || isTyping(e.target)) {
        return;
      }

      const store = useDesigner.getState();
      const mod = e.ctrlKey || e.metaKey;
      const key = e.key.toLowerCase();
      const current = activeUi(store).selection;
      const selection = current !== null && current !== activeTree(store)?.root ? current : null;
      if (mod && !e.altKey && ((key === 'z' && e.shiftKey) || (key === 'y' && !e.shiftKey))) {
        store.redo();
      } else if (mod && !e.altKey && !e.shiftKey && key === 'z') {
        store.undo();
      } else if (mod && !e.altKey && !e.shiftKey && key === 'd') {
        const copy = selection !== null ? store.duplicateNode(selection) : null;
        if (copy) {
          focusRow(copy);
        }
      } else if (e.key === 'Delete' && !mod && selection !== null) {
        store.deleteNode(selection);
        const next = activeUi(useDesigner.getState()).selection;
        if (next !== null) {
          focusRow(next);
        }
      } else {
        return;
      }

      e.preventDefault();
    };

    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [enabled]);
}
