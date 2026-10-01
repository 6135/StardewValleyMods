import { useDesigner } from '../model/store';
import { tabName, type Workspace } from '../model/workspace';
import { takeSharedWorkspace } from './share';
import { parseWorkspace, serializeWorkspace } from './workspace';

// Autosave (architecture.md §11): every workspace in its own localStorage slot (the workspace file text), written a
// moment after each change, plus a Recent list of the last few. Every storage access is wrapped in try / catch: without
// storage (private mode, blocked, full) the app works and simply forgets.

const Prefix = 'uifw-designer:';
const RecentKey = `${Prefix}recent`;
const RecentLimit = 5;
const SaveDelayMs = 800;

export interface RecentEntry {
  slot: string;
  name: string;
  /** Last autosave, ms since the epoch. */
  time: number;
}

const slotKey = (slot: string): string => `${Prefix}workspace:${slot}`;

function read(key: string): string | null {
  try {
    return window.localStorage.getItem(key);
  } catch {
    return null;
  }
}

/** Write or (null) remove a key; false when storage refused. */
function write(key: string, value: string | null): boolean {
  try {
    if (value === null) {
      window.localStorage.removeItem(key);
    } else {
      window.localStorage.setItem(key, value);
    }
    return true;
  } catch {
    return false;
  }
}

function readRecent(): RecentEntry[] {
  try {
    const value: unknown = JSON.parse(read(RecentKey) ?? '[]');
    return Array.isArray(value)
      ? value.filter((e): e is RecentEntry => typeof e?.slot === 'string' && typeof e?.name === 'string' && typeof e?.time === 'number')
      : [];
  } catch {
    return [];
  }
}

let recent = readRecent();
const listeners = new Set<() => void>();

/** The Recent list, newest first (a stable snapshot for useSyncExternalStore). */
export const getRecent = (): readonly RecentEntry[] => recent;

export function subscribeRecent(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** A workspace's name in the Recent list: its first tabs' names. */
function workspaceName(ws: Workspace): string {
  const names = ws.tabs.map(tabName);
  return names.slice(0, 3).join(', ') + (names.length > 3 ? ` +${names.length - 3}` : '');
}

function save(ws: Workspace, slot: string): void {
  if (!write(slotKey(slot), serializeWorkspace(ws))) {
    return;
  }

  const next = [{ slot, name: workspaceName(ws), time: Date.now() }, ...recent.filter(e => e.slot !== slot)];
  next.slice(RecentLimit).forEach(e => write(slotKey(e.slot), null));
  recent = next.slice(0, RecentLimit);
  write(RecentKey, JSON.stringify(recent));
  listeners.forEach(l => l());
}

/** The workspace autosaved in a Recent slot, or null when it is gone or unreadable. */
export function loadRecent(slot: string): Workspace | null {
  const text = read(slotKey(slot));
  return text ? parseWorkspace(text).workspace : null;
}

/**
 * Autosave every change from now on, then open the share link in the hash (a new workspace; the autosaved one stays
 * in Recent) or restore the last workspace. Share links pasted into the open page are opened too.
 */
export function startAutosave(): void {
  let pending: { ws: Workspace; slot: string } | null = null;
  let timer: ReturnType<typeof setTimeout> | undefined;
  const flush = () => {
    clearTimeout(timer);
    if (pending) {
      save(pending.ws, pending.slot);
      pending = null;
    }
  };

  useDesigner.subscribe((s, prev) => {
    if (s.workspace === prev.workspace && s.slot === prev.slot) {
      return;
    }

    // a replaced workspace: the previous one's last changes go to its own slot first
    if (pending && pending.slot !== s.slot) {
      flush();
    }

    pending = { ws: s.workspace, slot: s.slot };
    clearTimeout(timer);
    timer = setTimeout(flush, SaveDelayMs);
  });
  window.addEventListener('pagehide', flush);

  const { replaceWorkspace } = useDesigner.getState();
  const openShared = (): boolean => {
    const shared = takeSharedWorkspace();
    if (shared) {
      replaceWorkspace(shared);
    }
    return shared !== null;
  };

  window.addEventListener('hashchange', openShared);
  const last = recent[0];
  const restored = last ? loadRecent(last.slot) : null;
  if (!openShared() && last && restored) {
    replaceWorkspace(restored, last.slot);
  }
}
