import { useSyncExternalStore } from 'react';
import { createEmptyDocument } from '../model/factory';
import { frameworkVersion } from '../model/metadata';
import { useDesigner, useHistory } from '../model/store';
import { ExportButton, ImportButton, LiveSaveStatusLine, RecentMenu, ShareButton, WorkspaceFileButtons } from './ImportExport';
import { Inspector } from './Inspector';
import { LeftPane } from './LeftPane';
import { PreviewSlot } from './PreviewSlot';
import { ProblemsSlot } from './ProblemsSlot';
import { useGlobalShortcuts } from './shortcuts';
import { TabStrip, WorkspaceLists } from './WorkspacePane';
import './app.css';

// The designer shell (architecture.md §5, §18.3): header, workspace + palette + tree, tab strip, preview, inspector,
// problems. At phone width (§14 phase 5) it is read-only and single-column: tab strip, preview, tree.

const narrowQuery = '(max-width: 699px)';

function subscribeNarrow(listener: () => void): () => void {
  const query = window.matchMedia(narrowQuery);
  query.addEventListener('change', listener);
  return () => query.removeEventListener('change', listener);
}

const isNarrow = (): boolean => window.matchMedia(narrowQuery).matches;

export function App() {
  const narrow = useSyncExternalStore(subscribeNarrow, isNarrow);
  useGlobalShortcuts(!narrow);
  if (narrow) {
    return (
      <div className="app narrow">
        <header className="header">
          <h1>UI Framework Designer</h1>
          <div className="toolbar"><RecentMenu /></div>
        </header>
        <p className="narrow-notice">Read-only on a narrow screen: editing needs a window at least 700 px wide.</p>
        <TabStrip readOnly />
        <PreviewSlot readOnly />
        <LeftPane readOnly />
      </div>
    );
  }

  return (
    <div className="app">
      <Header />
      <LeftPane />
      <TabStrip />
      <PreviewSlot />
      <Inspector />
      <ProblemsSlot />
      <WorkspaceLists />
    </div>
  );
}

function Header() {
  const { canUndo, canRedo } = useHistory();
  const undo = useDesigner(s => s.undo);
  const redo = useDesigner(s => s.redo);
  const replaceDocument = useDesigner(s => s.replaceDocument);

  return (
    <header className="header">
      <h1>UI Framework Designer</h1>
      <span className="version" title="Framework version the metadata was generated from">v{frameworkVersion}</span>
      <LiveSaveStatusLine />
      <div className="toolbar">
        <button type="button" onClick={() => replaceDocument(createEmptyDocument())} title="Start a new menu (undoable)">New</button>
        <WorkspaceFileButtons />
        <RecentMenu />
        <ImportButton />
        <ExportButton />
        <ExportButton workspace />
        <ShareButton />
        <span className="sep" />
        <button type="button" onClick={undo} disabled={!canUndo} title="Undo (Ctrl+Z)">Undo</button>
        <button type="button" onClick={redo} disabled={!canRedo} title="Redo (Ctrl+Y)">Redo</button>
      </div>
    </header>
  );
}
