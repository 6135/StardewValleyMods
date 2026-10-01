import { useDesigner } from '../model/store';
import { PreviewPane } from '../preview/PreviewPane';
import { PreviewStatePanel } from '../preview/PreviewStatePanel';

// The schematic preview (architecture.md §7) and its sample-state panel, bound to the store.

export function PreviewSlot() {
  const doc = useDesigner(s => s.doc);
  const selection = useDesigner(s => s.selection);
  const select = useDesigner(s => s.select);
  const setPreviewState = useDesigner(s => s.setPreviewState);

  return (
    <section className="pane preview" data-pane="preview" aria-label="Preview">
      <div className="pane-title">Preview</div>
      <div className="pane-body preview-body">
        <PreviewPane doc={doc} selection={selection} onSelect={select} />
        <details className="preview-state">
          <summary>Preview state</summary>
          <PreviewStatePanel doc={doc} onChange={setPreviewState} />
        </details>
      </div>
    </section>
  );
}
