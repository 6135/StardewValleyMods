import { useRef, useState } from 'react';
import { defaultJsonExportOptions, type ImportResult, type JsonExportOptions, type JsonExportShape } from '../model/document';
import { exportJson, menuKey } from '../io/export';
import { importText } from '../io/import';
import { useDesigner } from '../model/store';

// Import and export dialogs (architecture.md §9.1): paste or open JSON, pick a menu; export in one of the three shapes.

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

export function ExportButton() {
  const dialog = useRef<HTMLDialogElement>(null);
  const doc = useDesigner(s => s.doc);
  const [open, setOpen] = useState(false);
  const [options, setOptions] = useState<JsonExportOptions>(defaultJsonExportOptions);
  const output = open ? exportJson(doc, options) : '';

  const show = () => {
    setOpen(true);
    dialog.current?.showModal();
  };

  const download = () => {
    const url = URL.createObjectURL(new Blob([output], { type: 'application/json' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = `${menuKey(doc).replace(/[^\w.-]+/g, '_')}.json`;
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <>
      <button type="button" onClick={show} title="Export JSON">Export</button>
      <dialog ref={dialog} className="io-dialog" onClose={() => setOpen(false)}>
        <div className="io-head">
          <strong>Export</strong>
          <select value={options.shape} onChange={e => setOptions({ ...options, shape: e.target.value as JsonExportShape })}>
            {shapes.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
          </select>
          <label><input type="checkbox" checked={options.collapseShorthands} onChange={e => setOptions({ ...options, collapseShorthands: e.target.checked })} /> Shorthands</label>
          <label><input type="checkbox" checked={options.omitDefaults} onChange={e => setOptions({ ...options, omitDefaults: e.target.checked })} /> Omit defaults</label>
          <button type="button" onClick={() => navigator.clipboard.writeText(output)}>Copy</button>
          <button type="button" onClick={download}>Download</button>
          <button type="button" onClick={() => dialog.current?.close()}>Close</button>
        </div>
        <textarea className="io-text" value={output} readOnly spellCheck={false} />
      </dialog>
    </>
  );
}
