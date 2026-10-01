// Preview state panel (architecture.md §7.2): sample values for the expression names the document uses.
import { useMemo, useState, type ReactNode } from 'react';
import type { DesignerDocument } from '../model/document';
import { findExpressionNames } from './evaluate';
import './PreviewPane.css';

export interface PreviewStatePanelProps {
  doc: DesignerDocument;
  /** Set a sample value; undefined removes it. */
  onChange(key: string, value: string | undefined): void;
}

export function PreviewStatePanel({ doc, onChange }: PreviewStatePanelProps): ReactNode {
  const [extra, setExtra] = useState('');
  const names = useMemo(() => {
    const all = new Set([...findExpressionNames(doc), ...Object.keys(doc.previewState)]);
    return [...all].sort((a, b) => a.localeCompare(b));
  }, [doc]);

  const add = (): void => {
    const key = extra.trim();
    if (key && doc.previewState[key] === undefined) {
      onChange(key, '');
    }
    setExtra('');
  };

  return (
    <div className="pv-pane pv-state">
      <div className="pv-toolbar"><strong>Preview state</strong><span className="pv-state-hint">sample values for expressions (never exported)</span></div>
      <div className="pv-state-list">
        {names.length === 0 ? <p className="pv-state-hint">The menu uses no menu.*, session.*, player.*, config.*, stat.* or args.* names.</p> : null}
        {names.map(name => {
          const value = doc.previewState[name];
          return (
            <div key={name} className="pv-state-row">
              <label htmlFor={`pv-state-${name}`} title={name}>{name}</label>
              <input
                id={`pv-state-${name}`}
                value={value ?? ''}
                placeholder="unset"
                onChange={e => onChange(name, e.target.value)}
              />
              <button type="button" disabled={value === undefined} onClick={() => onChange(name, undefined)} aria-label={`Clear ${name}`}>×</button>
            </div>
          );
        })}
        <div className="pv-state-row">
          <input value={extra} placeholder="other name (e.g. menu.page)" aria-label="Add a name"
            onChange={e => setExtra(e.target.value)} onKeyDown={e => { if (e.key === 'Enter') add(); }} />
          <button type="button" onClick={add} disabled={extra.trim().length === 0}>Add</button>
        </div>
      </div>
    </div>
  );
}

export default PreviewStatePanel;
