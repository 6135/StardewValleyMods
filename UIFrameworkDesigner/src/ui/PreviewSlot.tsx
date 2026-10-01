import { useCallback, useMemo, useState } from 'react';
import type { DesignerDocument, NodeId, Problem } from '../model/document';
import { buildTooltipObject } from '../io/export';
import { parseRawTab, rawTab } from '../io/workspace';
import { flattenLocals, sourceKey } from '../layout';
import { previewDocument, resolverOf } from '../model/resolve';
import { activeTab, activeUi, useDesigner } from '../model/store';
import { tabOwner, type PreviewData, type TabId, type TooltipTab, type Workspace, type WorkspaceTab } from '../model/workspace';
import { previewValues, type ExternalFunctions } from '../preview/evaluate';
import { I18nLoader } from '../preview/I18nLoader';
import { defaultPreviewSettings, PreviewPane } from '../preview/PreviewPane';
import { externalFunctions, findPreviewFunctions, findSampleSources } from '../preview/previewData';
import { PreviewStatePanel } from '../preview/PreviewStatePanel';
import { TooltipPreview } from '../preview/Tooltip';

// The center pane of the active tab, bound to the store: the schematic preview (architecture.md §7) of a menu (with
// the owner templates it uses), a template's body, or a named tooltip by itself (§18.3); or the tab's raw JSON,
// editable and applied through the import as one undo step. Read-only (phone width): preview only.

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
  const { select, setPreviewState, setI18n, setPreviewSettings, openTab, setPreviewFunction, setSampleRows } = useDesigner.getState();
  const data = workspace.previewData;
  const functions = useMemo(() => externalFunctions(data, i18n), [data, i18n]);
  const uses = useMemo(() => findPreviewFunctions(workspace), [workspace]);
  const sources = useMemo(() => findSampleSources(workspace), [workspace]);
  const resolveTooltip = useCallback((nodeId: NodeId) => (doc ? tooltipOf(workspace, tab, doc, nodeId) : undefined), [workspace, tab, doc]);
  const links = useMemo(() => menuLinks(workspace), [workspace]);
  const resolveMenuLink = useCallback((nodeId: NodeId) => links.get(nodeId)?.key, [links]);
  const openMenu = useCallback((key: string) => {
    const target = [...links.values()].find(l => l.key === key);
    if (target) {
      openTab(target.tabId);
    }
  }, [links, openTab]);

  if (!doc && tab.kind !== 'tooltip') {
    return <div className="placeholder">An owner entry has no preview: open a menu, template or tooltip tab.</div>;
  }

  return (
    <>
      {tab.kind === 'tooltip' ? <TooltipTabPreview workspace={workspace} tab={tab} functions={functions} i18n={i18n} /> : (
        <PreviewPane doc={doc!} selection={selection} onSelect={select} i18n={i18n ?? undefined} resolveTooltip={resolveTooltip}
          settings={settings} onSettingsChange={setPreviewSettings} resolveMenuLink={resolveMenuLink} onOpenMenu={openMenu}
          functions={functions} {...(data ? { sampleRows: data.rows } : {})} />
      )}
      {!readOnly && (
        <details className="preview-state">
          <summary>Preview state</summary>
          <PreviewStatePanel {...(doc ? { doc } : {})} onChange={setPreviewState} functions={uses} sources={sources} data={data}
            i18nLoaded={i18n !== null} onFunction={setPreviewFunction} onRows={setSampleRows} />
          <I18nLoader onLoad={setI18n} />
        </details>
      )}
    </>
  );
}

/**
 * A named tooltip by itself, its expressions read with the preview state of the first menu or template that shows it
 * and, for a row tooltip, the first sample row of that element's source.
 */
function TooltipTabPreview({ workspace, tab, functions, i18n }: { workspace: Workspace; tab: TooltipTab; functions: ExternalFunctions; i18n: Record<string, string> | null }) {
  const definition = useMemo(() => asObject(buildTooltipObject(tab.doc)), [tab.doc]);
  const state = useMemo(() => tooltipState(workspace, tab, functions, workspace.previewData), [workspace, tab, functions]);
  return <TooltipPreview definition={definition} state={state} functions={functions} {...(i18n ? { i18n } : {})} />;
}

function tooltipState(ws: Workspace, tab: TooltipTab, functions: ExternalFunctions, data: PreviewData | undefined): Record<string, string> {
  const resolver = resolverOf(ws);
  const def = resolver.definitions.find(d => d.kind === 'tooltip' && d.tabId === tab.id);
  for (const ref of def ? resolver.usages(def) : []) {
    const user = resolver.tab(ref.tabId);
    const doc = user ? previewDocument(ws, user) : null;
    const node = doc && ref.nodeId !== null ? doc.nodes[ref.nodeId] : undefined;
    if (!doc || !node) {
      continue;
    }

    const state = previewValues(doc, functions);
    const key = ref.field === 'RowTooltip' ? sourceKey(node.extra['Source'] ?? node.fields['Source'])?.toLowerCase() : undefined;
    const rowsKey = key !== undefined ? Object.keys(data?.rows ?? {}).find(k => k.toLowerCase() === key) : undefined;
    const row = rowsKey !== undefined ? data!.rows[rowsKey]![0] : undefined;
    const alias = node.fields['As']?.trim();
    return row === undefined ? state : { ...state, ...flattenLocals({ row, index: 0, ...(alias ? { [alias]: row, [`${alias}Index`]: 0 } : {}) }) };
  }

  return {};
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
 * the node's own; the node's MaxWidth wins.
 */
function tooltipOf(ws: Workspace, tab: WorkspaceTab, doc: DesignerDocument, nodeId: NodeId): unknown {
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
