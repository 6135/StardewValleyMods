import { useDesigner } from '../model/store';
import type { Workspace } from '../model/workspace';

// Live save (architecture.md §10, phase 6): in browsers with the File System Access API the user picks one file in
// their mod folder (a pack's content.json or assets/menus.json); while live save is on, every change re-writes that
// file a moment later, so a watching consumer (ImportDataFile(path, watch: true), or ui_reload) shows the real menu in
// game. The handle lives in memory only, and nothing but the picked file is ever written.

interface SavePickerWindow {
  showSaveFilePicker(options?: { id?: string; suggestedName?: string; types?: { description: string; accept: Record<string, string[]> }[] }): Promise<FileSystemFileHandle>;
}

export interface LiveSaveStatus {
  /** The picked file's name. */
  file: string;
  /** What is written ("the workspace (content.json)", "menu X"). */
  what: string;
  on: boolean;
  /** Last successful write, ms since the epoch. */
  lastWrite: number | null;
  error: string | null;
}

/** The text to write for a workspace; null when its source (a closed tab) is gone. */
export type LiveRender = (ws: Workspace) => string | null;

const WriteDelayMs = 500;

/** Whether the browser can save to a picked file (Chromium); the option is hidden elsewhere. */
export const liveSaveSupported = (): boolean => typeof window !== 'undefined' && 'showSaveFilePicker' in window;

let target: { handle: FileSystemFileHandle; render: LiveRender } | null = null;
let status: LiveSaveStatus | null = null;
let writing = Promise.resolve();
let watching = false;
const listeners = new Set<() => void>();

export const getLiveSave = (): LiveSaveStatus | null => status;

export function subscribeLiveSave(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function update(patch: Partial<LiveSaveStatus>): void {
  if (status) {
    status = { ...status, ...patch };
    listeners.forEach(l => l());
  }
}

/** Queue a write of the current workspace (writes never overlap). */
function writeSoon(): void {
  writing = writing.then(async () => {
    if (!target || !status?.on) {
      return;
    }

    const text = target.render(useDesigner.getState().workspace);
    if (text === null) {
      update({ on: false, error: 'Its tab was closed; live save stopped.' });
      return;
    }

    try {
      const stream = await target.handle.createWritable();
      await stream.write(text);
      await stream.close();
      update({ lastWrite: Date.now(), error: null });
    } catch (e) {
      update({ error: e instanceof Error ? e.message : String(e) });
    }
  });
}

/** Let the user pick the file to write (`suggestedName` first), then write it now and after every change. */
export async function pickLiveTarget(suggestedName: string, what: string, render: LiveRender): Promise<void> {
  let handle: FileSystemFileHandle;
  try {
    handle = await (window as unknown as SavePickerWindow).showSaveFilePicker({
      id: 'uifw-live-save',
      suggestedName,
      types: [{ description: 'JSON', accept: { 'application/json': ['.json'] } }]
    });
  } catch {
    return; // cancelled
  }

  target = { handle, render };
  status = { file: handle.name, what, on: true, lastWrite: null, error: null };
  listeners.forEach(l => l());
  if (!watching) {
    watching = true;
    let timer: ReturnType<typeof setTimeout> | undefined;
    useDesigner.subscribe((s, prev) => {
      if (s.workspace !== prev.workspace) {
        clearTimeout(timer);
        timer = setTimeout(writeSoon, WriteDelayMs);
      }
    });
  }

  writeSoon();
}

/** Turn live save on (writing now) or off; the picked file is kept. */
export function setLiveSave(on: boolean): void {
  update({ on });
  if (on) {
    writeSoon();
  }
}
