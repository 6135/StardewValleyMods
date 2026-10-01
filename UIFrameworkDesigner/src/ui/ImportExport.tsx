import { useRef, useState, useSyncExternalStore } from 'react';
import { defaultJsonExportOptions, type ImportResult, type JsonExportOptions, type JsonExportShape } from '../model/document';
import { getRecent, loadRecent, subscribeRecent } from '../io/autosave';
import { importText } from '../io/import';
import { getLiveSave, liveSaveSupported, pickLiveTarget, setLiveSave, subscribeLiveSave, type LiveRender } from '../io/liveSave';
import { shareLink, ShareLimit } from '../io/share';
import { exportTab, exportWorkspace, parseWorkspace, serializeWorkspace, type WorkspaceExportShape } from '../io/workspace';
import { activeTab, useDesigner } from '../model/store';
import { tabName } from '../model/workspace';

// Import and export dialogs (architecture.md §9.1, §10, §11, §18.4): paste or open JSON, pick a menu or open it all as
// a workspace; export the active tab or the whole workspace, or keep writing it to a file in the mod folder (live save);
// save / open `.uifw.json` workspace files; share links; the autosaved Recent workspaces.

function download(text: string, name: string): void {
  const url = URL.createObjectURL(new Blob([text], { type: 'application/json' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  a.click();
  URL.revokeObjectURL(url);
}


export function ImportButton() {
  const dialog = useRef<HTMLDialogElement>(null);
  const replaceDocument = useDesigner(s => s.replaceDocument);
  const [text, setText] = useState('');
  const [result, setResult] = useState<ImportResult | null>(null);

  const read = (value: string) => {
    setText(value);
    setResult(value.trim() ? importText(value) : null);
  };

  const pick = (index: number) => {
    const candidate = result?.candidates[index];
    if (candidate) {
      replaceDocument(candidate.document);
      dialog.current?.close();
    }
  };

  const ws = result?.workspace;
  return (
    <>
      <button type="button" onClick={() => dialog.current?.showModal()} title="Import a Menus entry, a CP content.json or a From file">Import</button>
      <dialog ref={dialog} className="io-dialog">
        <div className="io-head">
          <strong>Import</strong>
          <input type="file" accept=".json,.jsonc,application/json" onChange={async e => read(await e.target.files?.[0]?.text() ?? '')} />
          <button type="button" onClick={() => dialog.current?.close()}>Close</button>
        </div>
        <textarea className="io-text" value={text} onChange={e => read(e.target.value)} placeholder="Paste JSON here" spellCheck={false} />
        {result && (
          <div className="io-result">
            {result.hadComments && <p className="muted">Comments are not kept on export.</p>}
            {result.problems.map((p, i) => <p key={i} className={`problem ${p.severity}`}>{p.message}</p>)}
            {ws && ws.tabs.length > 1 && (
              <button type="button" onClick={() => { useDesigner.getState().replaceWorkspace(ws); dialog.current?.close(); }}>
                Open all as workspace ({ws.tabs.length} tabs: {ws.tabs.map(tabName).join(', ')})
              </button>
            )}
            {result.candidates.map((c, i) => (
              <button key={i} type="button" onClick={() => pick(i)}>Open {c.label}</button>
            ))}
          </div>
        )}
      </dialog>
    </>
  );
}

const shapes: { value: JsonExportShape; label: string }[] = [
  { value: 'entry', label: 'Menus entry' },
  { value: 'cpPatch', label: 'Content Patcher patch' },
  { value: 'fromFile', label: 'From file' }
];

const workspaceShapes: { value: WorkspaceExportShape; label: string }[] = [
  { value: 'contentJson', label: 'content.json' },
  { value: 'importData', label: 'ImportData file' }
];

/** Export the active tab (one menu in one of three shapes, or one definition), or with `workspace` the whole workspace. */
export function ExportButton({ workspace: whole = false }: { workspace?: boolean }) {
  const dialog = useRef<HTMLDialogElement>(null);
  const ws = useDesigner(s => s.workspace);
  const markSaved = useDesigner(s => s.markSaved);
  const tab = activeTab({ workspace: ws });
  const [open, setOpen] = useState(false);
  const [options, setOptions] = useState<JsonExportOptions>(defaultJsonExportOptions);
  const [shape, setShape] = useState<WorkspaceExportShape>('contentJson');
  const output = !open ? '' : whole ? exportWorkspace(ws, shape, options) : exportTab(tab, options);
  const fileName = whole ? (shape === 'contentJson' ? 'content.json' : 'ui.json') : `${tabName(tab).replace(/[^\w.-]+/g, '_')}.json`;

  /** Keep writing what this dialog shows (the workspace, or this tab) to a file the user picks. */
  const saveLive = () => {
    const tabId = tab.id;
    const render: LiveRender = whole
      ? w => exportWorkspace(w, shape, options)
      : w => {
        const t = w.tabs.find(x => x.id === tabId);
        return t ? exportTab(t, options) : null;
      };
    void pickLiveTarget(fileName, whole ? `the workspace (${shape === 'contentJson' ? 'content.json' : 'ImportData file'})` : tabName(tab), render);
  };

  const show = () => {
    setOpen(true);
    dialog.current?.showModal();
  };

  return (
    <>
      <button type="button" onClick={show} title={whole ? 'Export every tab as one file' : 'Export the active tab as JSON'}>{whole ? 'Export workspace' : 'Export'}</button>
      <dialog ref={dialog} className="io-dialog" onClose={() => setOpen(false)}>
        <div className="io-head">
          <strong>{whole ? 'Export workspace' : 'Export'}</strong>
          {whole ? (
            <select value={shape} onChange={e => setShape(e.target.value as WorkspaceExportShape)}>
              {workspaceShapes.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
            </select>
          ) : tab.kind === 'menu' && (
            <select value={options.shape} onChange={e => setOptions({ ...options, shape: e.target.value as JsonExportShape })}>
              {shapes.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
            </select>
          )}
          <label><input type="checkbox" checked={options.collapseShorthands} onChange={e => setOptions({ ...options, collapseShorthands: e.target.checked })} /> Shorthands</label>
          <label><input type="checkbox" checked={options.omitDefaults} onChange={e => setOptions({ ...options, omitDefaults: e.target.checked })} /> Omit defaults</label>
          <button type="button" onClick={() => navigator.clipboard.writeText(output)}>Copy</button>
          <button type="button" onClick={() => { download(output, fileName); if (whole) { markSaved(); } }}>Download</button>
          {liveSaveSupported() && (
            <button type="button" onClick={saveLive} title="Pick a file in your mod folder (e.g. content.json or assets/menus.json); it is re-written after every change while Live save is on">
              Save to mod folder…
            </button>
          )}
          <button type="button" onClick={() => dialog.current?.close()}>Close</button>
        </div>
        <LiveSaveStatusLine />
        <textarea className="io-text" value={output} readOnly spellCheck={false} />
      </dialog>
    </>
  );
}

/** Save the workspace as a `.uifw.json` file / open one (or any content.json) as the workspace. */
export function WorkspaceFileButtons() {
  const input = useRef<HTMLInputElement>(null);
  const markSaved = useDesigner(s => s.markSaved);

  const save = () => {
    download(serializeWorkspace(useDesigner.getState().workspace), 'workspace.uifw.json');
    markSaved();
  };

  const load = async (file: File | undefined) => {
    if (!file) {
      return;
    }

    const { workspace, problems } = parseWorkspace(await file.text());
    if (workspace) {
      useDesigner.getState().replaceWorkspace(workspace);
    } else {
      window.alert(problems.map(p => p.message).join('\n') || 'The file holds nothing the designer edits.');
    }
  };

  return (
    <>
      <button type="button" onClick={() => input.current?.click()} title="Open a .uifw.json workspace or a content.json as a workspace">Open…</button>
      <input ref={input} type="file" hidden accept=".json,.jsonc,application/json" onChange={e => { void load(e.target.files?.[0]); e.target.value = ''; }} />
      <button type="button" onClick={save} title="Save the workspace as a .uifw.json file (a valid content.json)">Save</button>
    </>
  );
}

/** Copy a link holding the whole workspace; a workspace too large for a link is offered as a download. */
export function ShareButton() {
  const dialog = useRef<HTMLDialogElement>(null);
  const [copied, setCopied] = useState(false);

  const share = () => {
    const link = shareLink(useDesigner.getState().workspace);
    if (link.length > ShareLimit) {
      dialog.current?.showModal();
      return;
    }

    void navigator.clipboard.writeText(link).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }, () => window.prompt('Copy the link:', link));
  };

  return (
    <>
      <button type="button" onClick={share} title="Copy a link that opens this workspace (it travels in the link, never uploaded)">{copied ? 'Link copied' : 'Share'}</button>
      <dialog ref={dialog} className="io-dialog io-small">
        <p>The workspace is too large for a link (over {ShareLimit / 1024} KB compressed) — download it instead and share the file.</p>
        <div className="io-head">
          <button type="button" onClick={() => { download(serializeWorkspace(useDesigner.getState().workspace), 'workspace.uifw.json'); dialog.current?.close(); }}>Download</button>
          <button type="button" onClick={() => dialog.current?.close()}>Close</button>
        </div>
      </dialog>
    </>
  );
}

/** The autosaved workspaces (newest first, the open one left out); picking one opens it. */
export function RecentMenu() {
  const recent = useSyncExternalStore(subscribeRecent, getRecent);
  const slot = useDesigner(s => s.slot);
  const others = recent.filter(e => e.slot !== slot);

  const open = (pick: string) => {
    const ws = loadRecent(pick);
    if (ws) {
      useDesigner.getState().replaceWorkspace(ws, pick);
    } else {
      window.alert('That workspace is no longer in the browser storage.');
    }
  };

  return (
    <select value="" aria-label="Recent workspaces" title="Autosaved workspaces in this browser" disabled={others.length === 0}
      onChange={e => { if (e.target.value) { open(e.target.value); } }}>
      <option value="">Recent…</option>
      {others.map(e => <option key={e.slot} value={e.slot}>{e.name} · {new Date(e.time).toLocaleString()}</option>)}
    </select>
  );
}

/** Live save status: the file, on / off, last write or error; nothing until a file was picked. */
export function LiveSaveStatusLine() {
  const status = useSyncExternalStore(subscribeLiveSave, getLiveSave);
  if (!status) {
    return null;
  }

  return (
    <span className={status.error ? 'live-save error' : 'live-save'} title={`Writes ${status.what} to ${status.file}`}>
      <label><input type="checkbox" checked={status.on} onChange={e => setLiveSave(e.target.checked)} /> Live save</label>
      {' '}{status.file}:{' '}
      {status.error ?? (status.lastWrite !== null ? `written ${new Date(status.lastWrite).toLocaleTimeString()}` : 'not written yet')}
    </span>
  );
}
