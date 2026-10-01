import { useId, useState } from 'react';
import { parse, printParseErrorCode, type ParseError } from 'jsonc-parser';
import type { FieldShape } from '../../fieldShapes';
import { isLiteral } from './literals';
import { ShapeWidget, TextWidget } from './widgets';

// One inspector row: label (description as help), the shape's widget or the ƒx expression box, and a clear button.

export interface FieldRowProps {
  name: string;
  shape: FieldShape;
  value: string | undefined;
  /** The data-format default. */
  defaultValue?: string;
  description?: string;
  /** Shown as a warning marker (e.g. "not read by Label"). */
  warning?: string;
  commit(value: string | undefined): void;
}

export function FieldRow({ name, shape, value, defaultValue, description, warning, commit }: FieldRowProps) {
  const id = useId();
  const literal = value === undefined || isLiteral(shape, value);
  const [exprWanted, setExprWanted] = useState(false);
  const expr = exprWanted || !literal;

  return (
    <div className={value === undefined ? 'field' : 'field set'}>
      <label htmlFor={id} title={description}>
        {warning && <span className="warn" title={warning}>⚠</span>}
        {name}
      </label>
      <div className="control">
        {expr
          ? <TextWidget id={id} value={value} placeholder={defaultValue ?? 'expression'} commit={commit} mono multiline={shape.kind === 'multiline'} />
          : <ShapeWidget id={id} shape={shape} value={value} placeholder={defaultValue} commit={commit} />}
      </div>
      <button type="button" className={expr ? 'fx on' : 'fx'} aria-pressed={expr} disabled={!literal}
        title={literal ? (expr ? 'Back to the widget' : 'Edit as an expression') : 'Not a literal value: clear it to use the widget'}
        onClick={() => setExprWanted(!expr)}>ƒx</button>
      <button type="button" className="icon clear" title="Remove the field" disabled={value === undefined} onClick={() => commit(undefined)}>×</button>
      {description && <div className="help">{description}</div>}
    </div>
  );
}

/** A member that is not a plain string (objects, arrays): edited as JSON (comments allowed), committed on blur when it parses. */
export function JsonRow({ name, value, description, warning, commit }: {
  name: string; value: unknown; description?: string; warning?: string; commit(value: unknown): void;
}) {
  const id = useId();
  const text = value === undefined ? '' : JSON.stringify(value, null, 2);
  const [draft, setDraft] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const apply = () => {
    if (draft === null) {
      return;
    }

    if (draft.trim() === '') {
      commit(undefined);
    } else {
      const errors: ParseError[] = [];
      const parsed: unknown = parse(draft, errors, { allowTrailingComma: true });
      if (errors.length > 0) {
        setError(`Invalid JSON: ${printParseErrorCode(errors[0]!.error)} at offset ${errors[0]!.offset}.`);
        return;
      }

      commit(parsed);
    }

    setDraft(null);
    setError(null);
  };

  return (
    <div className={value === undefined ? 'field json' : 'field set json'}>
      <label htmlFor={id} title={description}>
        {warning && <span className="warn" title={warning}>⚠</span>}
        {name}
      </label>
      <textarea id={id} className="control mono" spellCheck={false} rows={value === undefined ? 1 : Math.min(12, text.split('\n').length)}
        value={draft ?? text} placeholder="JSON value" onChange={e => setDraft(e.target.value)} onBlur={apply} />
      <button type="button" className="icon clear" title="Remove the field" disabled={value === undefined} onClick={() => commit(undefined)}>×</button>
      {error && <div className="help error">{error}</div>}
      {description && <div className="help">{description}</div>}
    </div>
  );
}
