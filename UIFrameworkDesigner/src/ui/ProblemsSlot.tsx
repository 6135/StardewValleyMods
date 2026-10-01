import { useDeferredValue, useMemo } from 'react';
import { useDesigner } from '../model/store';
import { validate } from '../validate';

// The problems pane (architecture.md §8): validation messages; clicking one selects its node.

export function ProblemsSlot() {
  const doc = useDeferredValue(useDesigner(s => s.doc));
  const select = useDesigner(s => s.select);
  const problems = useMemo(() => validate(doc), [doc]);

  return (
    <section className="pane problems" data-pane="problems" aria-label="Problems">
      <div className="pane-title">Problems ({problems.length})</div>
      <div className="pane-body">
        {problems.length === 0
          ? <div className="placeholder">No problems.</div>
          : (
            <ul className="problem-list">
              {problems.map((p, i) => (
                <li key={i} className={`problem ${p.severity}`}>
                  <button type="button" onClick={() => p.nodeId && select(p.nodeId)} disabled={!p.nodeId}>
                    <span className="problem-severity">{p.severity}</span>
                    <span className="problem-message">{p.message}</span>
                    <span className="problem-path">{p.path}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
      </div>
    </section>
  );
}
