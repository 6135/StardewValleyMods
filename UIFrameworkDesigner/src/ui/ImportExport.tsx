import { useRef, useState } from 'react';
import { defaultJsonExportOptions, type ImportResult, type JsonExportOptions, type JsonExportShape } from '../model/document';
import { importText } from '../io/import';
import { exportTab, exportWorkspace, parseWorkspace, serializeWorkspace, type WorkspaceExportShape } from '../io/workspace';
import { activeTab, useDesigner } from '../model/store';
import { tabName, type Workspace } from '../model/workspace';

// Import and export dialogs (architecture.md §9.1, §18.4): paste or open JSON, pick a menu or open it all as a
// workspace; export the active tab or the whole workspace; save / open `.uifw.json` workspace files.

function download(text: string, name: string): void {
  const url = URL.createObjectURL(new Blob([text], { type: 'application/json' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  a.click();
  URL.revokeObjectURL(url);
}

/** Replace the workspace after confirming when tabs have unsaved changes. */
function openWorkspace(ws: Workspace): boolean {
  const { workspace, saved, replaceWorkspace } = useDesigner.getState();
  if (workspace.tabs.some(t => saved[t.id] !== t) && !window.confirm('Replace the workspace? Unsaved changes in its tabs are lost.')) {
    return false;
  }

  replaceWorkspace(ws);
  return true;
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
              <button type="button" onClick={() => { if (openWorkspace(ws)) { dialog.current?.close(); } }}>
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
          <button type="button" onClick={() => dialog.current?.close()}>Close</button>
        </div>
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
      openWorkspace(workspace);
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
