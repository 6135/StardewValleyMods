import { createEmptyDocument } from '../model/factory';
import { frameworkVersion } from '../model/metadata';
import { useDesigner, useHistory } from '../model/store';
import { ExportButton, ImportButton } from './ImportExport';
import { Inspector } from './Inspector';
import { LeftPane } from './LeftPane';
import { PreviewSlot } from './PreviewSlot';
import { ProblemsSlot } from './ProblemsSlot';
import { useGlobalShortcuts } from './shortcuts';
import './app.css';

// The designer shell (architecture.md §5): header, palette + tree, preview, inspector, problems.

export function App() {
  useGlobalShortcuts();
  return (
    <div className="app">
      <Header />
      <LeftPane />
      <PreviewSlot />
      <Inspector />
      <ProblemsSlot />
      <datalist id="sprite-prefixes">
        <option value="sprite:" />
        <option value="item:" />
        <option value="asset:" />
      </datalist>
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
      <div className="toolbar">
        <button type="button" onClick={() => replaceDocument(createEmptyDocument())} title="Start a new menu (undoable)">New</button>
        <ImportButton />
        <ExportButton />
        <span className="sep" />
        <button type="button" onClick={undo} disabled={!canUndo} title="Undo (Ctrl+Z)">Undo</button>
        <button type="button" onClick={redo} disabled={!canRedo} title="Redo (Ctrl+Y)">Redo</button>
      </div>
    </header>
  );
}
