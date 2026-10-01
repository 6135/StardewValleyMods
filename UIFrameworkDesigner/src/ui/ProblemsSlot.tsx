import { useDeferredValue, useMemo, useState } from 'react';
import type { Problem } from '../model/document';
import { useDesigner } from '../model/store';
import { tabName, type TabId } from '../model/workspace';
import { validateTab } from '../validate';

// The problems pane (architecture.md §8, §18.5): validation messages of the active tab or of the whole workspace;
// clicking one opens its tab and selects its node.

type Scope = 'tab' | 'workspace';

export function ProblemsSlot() {
  const workspace = useDeferredValue(useDesigner(s => s.workspace));
  const reveal = useDesigner(s => s.reveal);
  const [scope, setScope] = useState<Scope>('tab');
  const problems = useMemo(() => {
    const tabs = scope === 'tab' ? workspace.tabs.filter(t => t.id === workspace.activeTab) : workspace.tabs;
    return tabs.flatMap(tab => validateTab(workspace, tab).map((problem): { tabId: TabId; tab: string; problem: Problem } => ({ tabId: tab.id, tab: tabName(tab), problem })));
  }, [workspace, scope]);

  return (
    <section className="pane problems" data-pane="problems" aria-label="Problems">
      <div className="pane-title">
        Problems ({problems.length})
        <select className="problems-scope" value={scope} onChange={e => setScope(e.target.value as Scope)} aria-label="Problems of">
          <option value="tab">This tab</option>
          <option value="workspace">Workspace</option>
        </select>
      </div>
      <div className="pane-body">
        {problems.length === 0
          ? <div className="placeholder">No problems.</div>
          : (
            <ul className="problem-list">
              {problems.map(({ tabId, tab, problem: p }, i) => (
                <li key={i} className={`problem ${p.severity}`}>
                  <button type="button" onClick={() => reveal(tabId, p.nodeId ?? null)}>
                    <span className="problem-severity">{p.severity}</span>
                    {scope === 'workspace' && <span className="problem-tab">{tab}</span>}
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
