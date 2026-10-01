import { useId, useState } from 'react';
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

/** A member that is not a plain string (objects, arrays): shown as JSON until it gets its own editor. */
export function JsonRow({ name, value, description, warning }: { name: string; value: unknown; description?: string; warning?: string }) {
  return (
    <div className="field set json">
      <label title={description}>
        {warning && <span className="warn" title={warning}>⚠</span>}
        {name}
      </label>
      <pre className="control" title="Read-only in this version">{JSON.stringify(value, null, 2)}</pre>
    </div>
  );
}
