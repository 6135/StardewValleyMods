import { useEffect } from 'react';
import { useDesigner } from '../model/store';
import { focusRow } from './Tree';

// Global shortcuts (architecture.md §6.3): undo / redo, duplicate, delete. Ignored while typing in a text control,
// which keeps its own undo.

function isTyping(target: EventTarget | null): boolean {
  return target instanceof HTMLElement
    && (target.isContentEditable || target.tagName === 'TEXTAREA' || target.tagName === 'SELECT'
      || (target.tagName === 'INPUT' && !['checkbox', 'radio', 'button', 'color'].includes((target as HTMLInputElement).type)));
}

export function useGlobalShortcuts(): void {
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.defaultPrevented || isTyping(e.target)) {
        return;
      }

      const store = useDesigner.getState();
      const mod = e.ctrlKey || e.metaKey;
      const key = e.key.toLowerCase();
      const selection = store.selection !== null && store.selection !== store.doc.root ? store.selection : null;
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
        const next = useDesigner.getState().selection;
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
  }, []);
}
