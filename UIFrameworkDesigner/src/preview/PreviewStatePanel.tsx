// Preview state panel (architecture.md §7.2): sample values for the expression names the document uses, and the
// workspace's stand-ins for what only C# provides: the functions it registers (`@name(…)`) and its sources' rows.
import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import type { DesignerDocument } from '../model/document';
import type { PreviewData, PreviewFunction } from '../model/workspace';
import { findExpressionNames } from './evaluate';
import type { FunctionUse } from './previewData';
import './PreviewPane.css';

export interface PreviewStatePanelProps {
  /** The menu / template whose sample values are edited; absent for a tab without its own (a tooltip). */
  doc?: DesignerDocument;
  /** Set a sample value; undefined removes it. */
  onChange(key: string, value: string | undefined): void;
  /** The `@name(…)` functions the workspace calls (previewData.ts findPreviewFunctions). */
  functions: FunctionUse[];
  /** The C# sources the workspace reads (findSampleSources). */
  sources: string[];
  data: PreviewData | undefined;
  /** An i18n map is loaded (suggests the i18n lookup for single-key functions). */
  i18nLoaded: boolean;
  onFunction(name: string, fn: PreviewFunction | undefined): void;
  onRows(source: string, rows: unknown[] | undefined): void;
  /** Names the layout needs values for (PreviewPane onShowState): listed first and highlighted while unset. */
  needed?: string[];
  /** Changes each time `needed` is asked for: the first of them gets the focus. */
  focus?: number;
}

const noNames: string[] = [];

const functionKinds: Record<PreviewFunction['kind'], string> = {
  unknown: 'unknown (chip)',
  i18n: 'i18n lookup of first argument',
  first: 'first argument as text',
  fixed: 'fixed value'
};

/** A function's choice, matched without case. */
function choiceOf(data: PreviewData | undefined, name: string): { key: string; fn: PreviewFunction } | undefined {
  const key = Object.keys(data?.functions ?? {}).find(k => k.toLowerCase() === name.toLowerCase());
  return key !== undefined ? { key, fn: data!.functions[key]! } : undefined;
}

export function PreviewStatePanel(props: PreviewStatePanelProps): ReactNode {
  const { doc, onChange, functions, sources, data, i18nLoaded, onFunction, onRows, needed = noNames, focus } = props;
  const [extra, setExtra] = useState('');
  const listRef = useRef<HTMLDivElement>(null);
  const names = useMemo(() => {
    if (!doc) {
      return [];
    }
    const all = new Set([...findExpressionNames(doc), ...Object.keys(doc.previewState)]);
    return [...needed, ...[...all].filter(n => !needed.includes(n)).sort((a, b) => a.localeCompare(b))];
  }, [doc, needed]);

  useEffect(() => {
    const name = needed[0];
    const input = name !== undefined ? listRef.current?.querySelector<HTMLInputElement>(`#${CSS.escape(`pv-state-${name}`)}`) : null;
    input?.scrollIntoView({ block: 'nearest' });
    input?.focus();
    // only when asked again (focus), not on every edit
  }, [focus]);

  const add = (): void => {
    const key = extra.trim();
    if (key && doc && doc.previewState[key] === undefined) {
      onChange(key, '');
    }
    setExtra('');
  };

  const setFunction = (name: string, fn: PreviewFunction | undefined): void => {
    onFunction(choiceOf(data, name)?.key ?? name, fn);
  };

  return (
    <div className="pv-pane pv-state">
      <div className="pv-toolbar"><span className="pv-state-hint">sample values for expressions (never exported)</span></div>
      <div className="pv-state-list" ref={listRef}>
        {doc ? (
          <div className="pv-state-group">
            {names.length === 0 ? <p className="pv-state-hint">The menu uses no menu.*, session.*, player.*, config.*, stat.* or args.* names.</p> : null}
            {names.map(name => {
              const value = doc.previewState[name];
              return (
                <div key={name} className={needed.includes(name) && value === undefined ? 'pv-state-row pv-state-needed' : 'pv-state-row'}>
                  <label htmlFor={`pv-state-${name}`} title={needed.includes(name) ? `${name}: the layout depends on it` : name}>{name}</label>
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
        ) : null}

        <strong className="pv-state-head">Preview functions</strong>
        {functions.length === 0 ? <p className="pv-state-hint">The workspace calls no @name(…) functions registered from C#.</p> : null}
        {functions.map(({ name, singleLiteral }) => {
          const fn = choiceOf(data, name)?.fn ?? { kind: 'unknown' as const };
          return (
            <div key={name} className="pv-state-row">
              <label htmlFor={`pv-fn-${name}`} title={`@${name}`}>@{name}</label>
              <select id={`pv-fn-${name}`} value={fn.kind}
                onChange={e => setFunction(name, e.target.value === 'unknown' ? undefined : { kind: e.target.value as PreviewFunction['kind'], ...(fn.value !== undefined ? { value: fn.value } : {}) })}>
                {Object.entries(functionKinds).map(([k, label]) => <option key={k} value={k}>{label}</option>)}
              </select>
              {fn.kind === 'fixed' ? (
                <input value={fn.value ?? ''} placeholder="value" aria-label={`Value of @${name}`}
                  onChange={e => setFunction(name, { kind: 'fixed', value: e.target.value })} />
              ) : fn.kind === 'unknown' && singleLiteral && i18nLoaded ? (
                <button type="button" onClick={() => setFunction(name, { kind: 'i18n' })} title="Every call passes one string literal: look it up in the loaded i18n file">
                  Use i18n lookup for @{name}
                </button>
              ) : <span />}
            </div>
          );
        })}

        <strong className="pv-state-head">Sample rows</strong>
        {sources.length === 0 ? <p className="pv-state-hint">The workspace reads no hook: / @ sources provided by C#.</p> : null}
        {sources.map(source => (
          <RowsEditor key={source} source={source} rows={sampleRowsOf(data, source)} onRows={rows => onRows(sampleKeyOf(data, source) ?? source, rows)} />
        ))}
      </div>
    </div>
  );
}

function sampleKeyOf(data: PreviewData | undefined, source: string): string | undefined {
  return Object.keys(data?.rows ?? {}).find(k => k.toLowerCase() === source.toLowerCase());
}

function sampleRowsOf(data: PreviewData | undefined, source: string): unknown[] | undefined {
  const key = sampleKeyOf(data, source);
  return key !== undefined ? data!.rows[key] : undefined;
}

/** A source's sample rows as a JSON array, applied when the text reads back as one (empty text removes them). */
function RowsEditor({ source, rows, onRows }: { source: string; rows: unknown[] | undefined; onRows(rows: unknown[] | undefined): void }): ReactNode {
  const shown = rows !== undefined ? JSON.stringify(rows, null, 1) : '';
  const [text, setText] = useState(shown);
  const [base, setBase] = useState(shown);
  const [error, setError] = useState<string | null>(null);
  // changed elsewhere (another workspace, a share link): show it unless the text holds unapplied edits
  if (shown !== base) {
    setBase(shown);
    if (text === base) {
      setText(shown);
    }
  }

  const apply = (): void => {
    if (text.trim().length === 0) {
      setError(null);
      onRows(undefined);
      return;
    }
    try {
      const value: unknown = JSON.parse(text);
      if (!Array.isArray(value)) {
        setError('Expected a JSON array of rows.');
        return;
      }
      setError(null);
      onRows(value);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Not valid JSON.');
    }
  };

  return (
    <div className="pv-state-rows">
      <label htmlFor={`pv-rows-${source}`} title={source}>{source}{rows !== undefined ? ` (${rows.length} rows)` : ''}</label>
      <textarea id={`pv-rows-${source}`} value={text} rows={3} spellCheck={false} placeholder='[{ "Name": "…" }]'
        onChange={e => setText(e.target.value)} onBlur={apply} />
      {error ? <span className="pv-state-error">{error}</span> : null}
    </div>
  );
}

export default PreviewStatePanel;
