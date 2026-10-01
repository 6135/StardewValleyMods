import { useCallback } from 'react';
import type { DesignerDocument, NodeId } from '../model/document';
import { buildTooltipObject } from '../io/export';
import { previewDocument, resolverOf, tooltipStandIn } from '../model/resolve';
import { activeTab, activeUi, useDesigner } from '../model/store';
import { tabOwner, type Workspace, type WorkspaceTab } from '../model/workspace';
import { I18nLoader } from '../preview/I18nLoader';
import { PreviewPane } from '../preview/PreviewPane';
import { PreviewStatePanel } from '../preview/PreviewStatePanel';

// The schematic preview (architecture.md §7) of the active tab, bound to the store: a menu (with the owner templates
// it uses), a template's body, or a stand-in element that shows a named tooltip on hover (§18.3).

export function PreviewSlot() {
  const workspace = useDesigner(s => s.workspace);
  const tab = activeTab({ workspace });
  const doc = previewDocument(workspace, tab);
  const selection = useDesigner(s => activeUi(s).selection);
  const select = useDesigner(s => s.select);
  const setPreviewState = useDesigner(s => s.setPreviewState);
  const i18n = useDesigner(s => s.i18n);
  const setI18n = useDesigner(s => s.setI18n);
  const resolveTooltip = useCallback((nodeId: NodeId) => (doc ? tooltipOf(workspace, tab, doc, nodeId) : undefined), [workspace, tab, doc]);

  return (
    <section className="pane preview" data-pane="preview" aria-label="Preview">
      <div className="pane-title">Preview</div>
      <div className="pane-body preview-body">
        {doc ? (
          <>
            <PreviewPane key={tab.id} doc={doc} selection={selection} onSelect={select} i18n={i18n ?? undefined} resolveTooltip={resolveTooltip} />
            <details className="preview-state">
              <summary>Preview state</summary>
              {(tab.kind === 'menu' || tab.kind === 'template') && <PreviewStatePanel doc={doc} onChange={setPreviewState} />}
              <I18nLoader onLoad={setI18n} />
            </details>
          </>
        ) : <div className="placeholder">An owner entry has no preview: open a menu, template or tooltip tab.</div>}
      </div>
    </section>
  );
}

function isRecord(v: unknown): v is Record<string, unknown> {
  return typeof v === 'object' && v !== null && !Array.isArray(v);
}

/** A TooltipDefinition value (object, block array or string, TooltipConverter) as an object. */
function asObject(value: unknown): Record<string, unknown> {
  return isRecord(value) ? value : Array.isArray(value) ? { Blocks: value } : { Blocks: [{ Type: 'Line', Text: String(value) }] };
}

/**
 * The rich tooltip a node shows, From followed like DataBuilder.CompileTooltip: the named tooltip's blocks first, then
 * the node's own; the node's MaxWidth wins. A tooltip tab's stand-in shows the tab's tooltip.
 */
function tooltipOf(ws: Workspace, tab: WorkspaceTab, doc: DesignerDocument, nodeId: NodeId): unknown {
  if (tab.kind === 'tooltip' && nodeId === tooltipStandIn(tab)) {
    return asObject(buildTooltipObject(tab.doc));
  }

  const rich = doc.nodes[nodeId]?.extra['RichTooltip'] ?? doc.nodes[nodeId]?.fields['RichTooltip'];
  if (rich === undefined || rich === null) {
    return undefined;
  }

  const own = asObject(rich);
  const from = typeof own['From'] === 'string' ? own['From'] : '';
  const def = from ? resolverOf(ws).resolve('tooltip', tabOwner(tab), from) : null;
  const named = def !== null && def !== 'external' && def.tabId !== null ? ws.tabs.find(t => t.id === def.tabId) : undefined;
  if (named?.kind !== 'tooltip') {
    return own;
  }

  const shared = asObject(buildTooltipObject(named.doc));
  const blocks = (v: unknown) => (Array.isArray(v) ? v : []);
  const { From: _from, ...rest } = own;
  return { ...rest, MaxWidth: own['MaxWidth'] ?? shared['MaxWidth'], Blocks: [...blocks(shared['Blocks']), ...blocks(own['Blocks'])] };
}
