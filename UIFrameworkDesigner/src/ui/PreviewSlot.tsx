import { useCallback, useMemo, useState } from 'react';
import type { DesignerDocument, NodeId, Problem } from '../model/document';
import { buildTooltipObject } from '../io/export';
import { parseRawTab, rawTab } from '../io/workspace';
import { previewDocument, resolverOf, tooltipStandIn } from '../model/resolve';
import { activeTab, activeUi, useDesigner } from '../model/store';
import { tabOwner, type TabId, type Workspace, type WorkspaceTab } from '../model/workspace';
import { I18nLoader } from '../preview/I18nLoader';
import { defaultPreviewSettings, PreviewPane } from '../preview/PreviewPane';
import { PreviewStatePanel } from '../preview/PreviewStatePanel';

// The center pane of the active tab, bound to the store: the schematic preview (architecture.md §7) of a menu (with
// the owner templates it uses), a template's body, or a stand-in element that shows a named tooltip on hover (§18.3);
// or the tab's raw JSON, editable and applied through the import as one undo step. Read-only (phone width): preview only.

export function PreviewSlot({ readOnly = false }: { readOnly?: boolean }) {
  const tab = useDesigner(activeTab);
  const [view, setView] = useState<'preview' | 'json'>('preview');
  const json = view === 'json' && !readOnly;

  return (
    <section className="pane preview" data-pane="preview" aria-label="Preview">
      <div className="pane-title">
        {readOnly ? 'Preview' : (
          <span role="tablist" aria-label="Center view" className="view-switch">
            <button type="button" role="tab" aria-selected={!json} onClick={() => setView('preview')}>Preview</button>
            <button type="button" role="tab" aria-selected={json} onClick={() => setView('json')}>JSON</button>
          </span>
        )}
      </div>
      <div className="pane-body preview-body">
        {json ? <RawJson key={tab.id} tab={tab} /> : <Preview readOnly={readOnly} />}
      </div>
    </section>
  );
}

function Preview({ readOnly }: { readOnly: boolean }) {
  const workspace = useDesigner(s => s.workspace);
  const tab = activeTab({ workspace });
  const doc = previewDocument(workspace, tab);
  const selection = useDesigner(s => activeUi(s).selection);
  const settings = useDesigner(s => activeUi(s).preview) ?? defaultPreviewSettings;
  const i18n = useDesigner(s => s.i18n);
  const { select, setPreviewState, setI18n, setPreviewSettings, openTab } = useDesigner.getState();
  const resolveTooltip = useCallback((nodeId: NodeId) => (doc ? tooltipOf(workspace, tab, doc, nodeId) : undefined), [workspace, tab, doc]);
  const links = useMemo(() => menuLinks(workspace), [workspace]);
  const resolveMenuLink = useCallback((nodeId: NodeId) => links.get(nodeId)?.key, [links]);
  const openMenu = useCallback((key: string) => {
    const target = [...links.values()].find(l => l.key === key);
    if (target) {
      openTab(target.tabId);
    }
  }, [links, openTab]);

  return doc ? (
    <>
      <PreviewPane doc={doc} selection={selection} onSelect={select} i18n={i18n ?? undefined} resolveTooltip={resolveTooltip}
        settings={settings} onSettingsChange={setPreviewSettings} resolveMenuLink={resolveMenuLink} onOpenMenu={openMenu} />
      {!readOnly && (
        <details className="preview-state">
          <summary>Preview state</summary>
          {(tab.kind === 'menu' || tab.kind === 'template') && <PreviewStatePanel doc={doc} onChange={setPreviewState} />}
          <I18nLoader onLoad={setI18n} />
        </details>
      )}
    </>
  ) : <div className="placeholder">An owner entry has no preview: open a menu, template or tooltip tab.</div>;
}

/** The active tab's raw JSON: edited freely, applied as one undo step when it reads back without errors. */
function RawJson({ tab }: { tab: WorkspaceTab }) {
  const replaceTab = useDesigner(s => s.replaceTab);
  const exported = useMemo(() => rawTab(tab), [tab]);
  const [text, setText] = useState(exported);
  const [base, setBase] = useState(exported);
  const [problems, setProblems] = useState<Problem[]>([]);
  // the tab changed outside this view (undo, an edit elsewhere): show it, unless the text holds unapplied edits
  if (exported !== base) {
    setBase(exported);
    if (text === base) {
      setText(exported);
    }
  }

  const apply = () => {
    const result = parseRawTab(useDesigner.getState().workspace, tab, text);
    setProblems(result.tab ? [] : result.problems);
    if (result.tab) {
      replaceTab(result.tab);
    }
  };

  const revert = () => {
    setText(exported);
    setProblems([]);
  };

  return (
    <div className="raw-json">
      <div className="raw-json-bar">
        <button type="button" onClick={apply} disabled={text === exported} title="Read the JSON back and replace the tab (one undo step)">Apply</button>
        <button type="button" onClick={revert} disabled={text === exported}>Revert</button>
        <span className="muted">Comments are not kept.</span>
      </div>
      <textarea className="raw-json-text" value={text} onChange={e => setText(e.target.value)} spellCheck={false} aria-label="Raw JSON of the tab" />
      {problems.length > 0 && (
        <ul className="problem-list">{problems.map((p, i) => <li key={i} className={`problem ${p.severity}`}>{p.message}</li>)}</ul>
      )}
    </div>
  );
}

/** Each node whose action opens a menu tab of the workspace (`6135.UIFramework_OpenMenu …`): the menu key and its tab. */
function menuLinks(ws: Workspace): Map<NodeId, { key: string; tabId: TabId }> {
  const resolver = resolverOf(ws);
  const links = new Map<NodeId, { key: string; tabId: TabId }>();
  for (const ref of resolver.references) {
    if (ref.kind !== 'menu' || ref.nodeId === null || links.has(ref.nodeId)) {
      continue;
    }

    const def = resolver.resolve('menu', ref.owner, ref.name, ref.tabId);
    if (def !== null && def !== 'external' && def.tabId !== null) {
      links.set(ref.nodeId, { key: `${def.owner}/${def.name}`, tabId: def.tabId });
    }
  }

  return links;
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
