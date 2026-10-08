import { useEffect, useRef, useState, type FocusEvent, type KeyboardEvent } from 'react';
import type { FieldShape } from '../../fieldShapes';
import type { RefKind } from '../../model/resolve';
import {
  colorToHex, enumValue, formatColor, formatMargin, formatTracks, parseBool, parseMargin, parseTracks, type Track
} from './literals';

// One widget per field shape (architecture.md §6.2). Every widget gets the raw value and commits a raw string, or
// undefined to remove the field; text edits commit on blur / Enter so typing is one undo step, not one per key.

export interface WidgetProps {
  value: string | undefined;
  /** The data-format default, shown as placeholder. */
  placeholder?: string;
  commit(value: string | undefined): void;
  id?: string;
}

/** Local text state that follows the committed value until edited; `flush` commits it ('' removes the field). */
function useDraft(value: string | undefined, commit: (value: string | undefined) => void) {
  // the edit is tied to the committed value it started from, so a changed value discards it
  const [edit, setEdit] = useState<{ base: string | undefined; text: string } | null>(null);
  const draft = edit !== null && edit.base === value ? edit.text : value ?? '';
  const setDraft = (text: string) => setEdit({ base: value, text });

  const flush = (text = draft) => {
    const next = text === '' ? undefined : text;
    if (next !== value) {
      commit(next);
    }
  };
  const reset = () => setEdit(null);
  return { draft, setDraft, flush, reset };
}

function textKeys(flush: () => void, reset: () => void, multiline = false) {
  return (e: KeyboardEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && (!multiline || e.ctrlKey || e.metaKey)) {
      e.preventDefault();
      flush();
    } else if (e.key === 'Escape') {
      reset();
    }
  };
}

/** Plain text (also the expression editor when `mono`). */
export function TextWidget({ value, placeholder, commit, id, mono, multiline, list }: WidgetProps & { mono?: boolean; multiline?: boolean; list?: string }) {
  const { draft, setDraft, flush, reset } = useDraft(value, commit);
  const className = mono ? 'w-text mono' : 'w-text';
  if (multiline) {
    return (
      <textarea id={id} className={className} rows={Math.min(6, Math.max(2, draft.split('\n').length))} value={draft} placeholder={placeholder}
        onChange={e => setDraft(e.target.value)} onBlur={() => flush()} onKeyDown={textKeys(flush, reset, true)} />
    );
  }

  return (
    <input id={id} className={className} type="text" value={draft} placeholder={placeholder} list={list} spellCheck={!mono}
      onChange={e => setDraft(e.target.value)} onBlur={() => flush()} onKeyDown={textKeys(flush, reset)} />
  );
}

/** Whole or decimal number; arrow keys step the draft. */
export function NumberWidget({ value, placeholder, commit, id, step = 1, auto }: WidgetProps & { step?: number; auto?: boolean }) {
  const { draft, setDraft, flush, reset } = useDraft(value, commit);
  const onKey = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'ArrowUp' || e.key === 'ArrowDown') {
      const current = Number(draft === '' || /^auto$/i.test(draft) ? (placeholder ?? 0) : draft);
      if (!Number.isNaN(current)) {
        e.preventDefault();
        const delta = (e.key === 'ArrowUp' ? 1 : -1) * step * (e.shiftKey ? 10 : 1);
        setDraft(String(Math.round((current + delta) * 1000) / 1000));
      }
    } else {
      textKeys(flush, reset)(e);
    }
  };

  return (
    <input id={id} className="w-number" type="text" inputMode={step === 1 ? 'numeric' : 'decimal'} value={draft}
      placeholder={placeholder ?? (auto ? 'auto' : '')} onChange={e => setDraft(e.target.value)} onBlur={() => flush()} onKeyDown={onKey} />
  );
}

/** A toggle; unset shows the default dimmed. */
export function BoolWidget({ value, placeholder, commit, id }: WidgetProps) {
  const explicit = parseBool(value);
  const shown = explicit ?? parseBool(placeholder) ?? false;
  return (
    <label className={explicit === undefined ? 'w-bool unset' : 'w-bool'}>
      <input id={id} type="checkbox" checked={shown} onChange={() => commit(shown ? 'false' : 'true')} />
      <span>{shown ? 'true' : 'false'}{explicit === undefined ? ' (default)' : ''}</span>
    </label>
  );
}

export function EnumWidget({ value, placeholder, commit, id, shape }: WidgetProps & { shape: Extract<FieldShape, { kind: 'enum' }> }) {
  const current = value === undefined ? '' : (enumValue(shape, value) ?? value);
  return (
    <select id={id} className="w-enum" value={current} onChange={e => commit(e.target.value === '' ? undefined : e.target.value)}>
      <option value="">{placeholder ? `default (${placeholder})` : '—'}</option>
      {shape.values.map(v => <option key={v} value={v}>{v}</option>)}
    </select>
  );
}

/** Four sides (left, top, right, bottom), written back as the shortest shorthand. */
export function MarginWidget({ value, placeholder, commit, id }: WidgetProps) {
  const parsed = value === undefined ? null : parseMargin(value);
  const initial = parsed ? parsed.map(String) : ['', '', '', ''];
  const [draft, setDraft] = useState(initial);
  const [seen, setSeen] = useState(value);
  if (seen !== value) {
    setSeen(value);
    setDraft(initial);
  }

  const flush = () => {
    if (draft.every(d => d.trim() === '')) {
      if (value !== undefined) {
        commit(undefined);
      }
      return;
    }

    const sides = draft.map(d => Number(d.trim() || 0));
    if (sides.every(n => Number.isInteger(n))) {
      const next = formatMargin(sides);
      if (next !== value) {
        commit(next);
      }
    }
  };
  const onBlur = (e: FocusEvent<HTMLDivElement>) => {
    if (!e.currentTarget.contains(e.relatedTarget as Node | null)) {
      flush();
    }
  };
  const fallback = placeholder ? parseMargin(placeholder) : null;

  return (
    <div className="w-margin" onBlur={onBlur}>
      {['Left', 'Top', 'Right', 'Bottom'].map((side, i) => (
        <input key={side} id={i === 0 ? id : undefined} type="text" inputMode="numeric" title={side} aria-label={side}
          value={draft[i]} placeholder={fallback ? String(fallback[i]) : side[0]}
          onChange={e => setDraft(draft.map((d, j) => j === i ? e.target.value : d))}
          onKeyDown={e => { if (e.key === 'Enter') { flush(); } }} />
      ))}
    </div>
  );
}

/** A swatch plus the raw text; the picker commits once when closed, keeping the value's form and alpha. */
export function ColorWidget({ value, placeholder, commit, id }: WidgetProps) {
  const swatch = useRef<HTMLInputElement>(null);
  const resolved = value ? colorToHex(value) : placeholder ? colorToHex(placeholder) : null;
  const latest = useRef({ value, commit });
  latest.current = { value, commit };

  useEffect(() => {
    const input = swatch.current;
    if (!input) {
      return;
    }

    const onChange = () => latest.current.commit(formatColor(input.value, latest.current.value));
    input.addEventListener('change', onChange);
    return () => input.removeEventListener('change', onChange);
  }, [resolved?.hex]);

  return (
    <div className="w-color">
      <input ref={swatch} key={resolved?.hex} type="color" aria-label="Pick a color" defaultValue={resolved?.hex ?? '#000000'} />
      <TextWidget id={id} value={value} placeholder={placeholder ?? '#RRGGBB, R,G,B[,A] or a name'} commit={commit} />
    </div>
  );
}

/** Grid tracks: auto, pixels or star weights. */
export function TracksWidget({ value, commit, id }: WidgetProps) {
  const tracks = value ? parseTracks(value) : [];
  const write = (next: Track[]) => commit(next.length ? formatTracks(next) : undefined);
  const update = (i: number, track: Track) => write(tracks.map((t, j) => j === i ? track : t));

  return (
    <div className="w-tracks">
      {tracks.map((t, i) => (
        <div key={i} className="track">
          <select id={i === 0 ? id : undefined} aria-label={`Track ${i + 1} kind`} value={t.kind}
            onChange={e => update(i, e.target.value === 'auto' ? { kind: 'auto' } : e.target.value === 'px' ? { kind: 'px', size: 100 } : { kind: 'star', weight: 1 })}>
            <option value="auto">auto</option>
            <option value="px">px</option>
            <option value="star">*</option>
          </select>
          {t.kind !== 'auto' && (
            <NumberWidget value={String(t.kind === 'px' ? t.size : t.weight)} step={t.kind === 'px' ? 1 : 0.5}
              commit={v => {
                const n = Number(v ?? (t.kind === 'px' ? 0 : 1));
                if (!Number.isNaN(n) && n >= 0) {
                  update(i, t.kind === 'px' ? { kind: 'px', size: n } : { kind: 'star', weight: n });
                }
              }} />
          )}
          <button type="button" className="icon" title="Remove track" onClick={() => write(tracks.filter((_, j) => j !== i))}>×</button>
        </div>
      ))}
      <button type="button" className="small" onClick={() => write([...tracks, { kind: 'star', weight: 1 }])}>+ track</button>
    </div>
  );
}

/** The id of the datalist with the workspace's names of a reference kind (ui/WorkspaceLists.tsx); menus list open actions. */
export function refListId(ref: RefKind): string {
  return `ref-${ref}`;
}

/** The editor for a shape in literal mode. */
export function ShapeWidget({ shape, ...props }: WidgetProps & { shape: FieldShape }) {
  switch (shape.kind) {
    case 'bool': return <BoolWidget value={props.value} placeholder={props.placeholder} commit={props.commit} id={props.id} />;
    case 'int': return <NumberWidget value={props.value} placeholder={props.placeholder} commit={props.commit} id={props.id} auto={shape.auto} />;
    case 'float': return <NumberWidget value={props.value} placeholder={props.placeholder} commit={props.commit} id={props.id} step={0.1} />;
    case 'enum': return <EnumWidget value={props.value} placeholder={props.placeholder} commit={props.commit} id={props.id} shape={shape} />;
    case 'margin': return <MarginWidget value={props.value} placeholder={props.placeholder} commit={props.commit} id={props.id} />;
    case 'color': return <ColorWidget value={props.value} placeholder={props.placeholder} commit={props.commit} id={props.id} />;
    case 'tracks': return <TracksWidget value={props.value} placeholder={props.placeholder} commit={props.commit} id={props.id} />;
    case 'sprite': return <TextWidget value={props.value} commit={props.commit} id={props.id} mono list={refListId('sprite')} placeholder={props.placeholder ?? 'sprite:Owner/name, item:(O)24, asset:Path@x,y,w,h'} />;
    case 'actions': return <TextWidget value={props.value} commit={props.commit} id={props.id} mono list={refListId('menu')} placeholder={props.placeholder ?? 'a trigger action, e.g. AddMoney 100'} />;
    case 'ref': return <TextWidget value={props.value} placeholder={props.placeholder} commit={props.commit} id={props.id} mono list={refListId(shape.ref)} />;
    case 'multiline': return <TextWidget value={props.value} placeholder={props.placeholder} commit={props.commit} id={props.id} multiline />;
    case 'text': return <TextWidget value={props.value} placeholder={props.placeholder} commit={props.commit} id={props.id} />;
  }
}
