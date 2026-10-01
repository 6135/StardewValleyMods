import { useState, type DragEvent } from 'react';
import type { NodeId } from '../model/document';
import { resolverOf, type Definition, type RefKind, type Resolver } from '../model/resolve';
import { activeTab, useDesigner } from '../model/store';
import { tabKindLabels, tabName, tabOwner, treeOf, type TabKind } from '../model/workspace';
import { refListId, TextWidget } from './fields/widgets';

// Workspace UI (architecture.md §18.3): the tab strip, the definitions pane above the tree, the pickers' name lists,
// "Used by" and rename of a definition.

/** The resolver of the current workspace revision. */
export function useResolver(): Resolver {
  return useDesigner(s => resolverOf(s.workspace));
}

/** Open the tab that defines `def` with nothing selected (the inspector shows the definition). */
export function revealDefinition(def: Definition): void {
  if (def.tabId !== null) {
    useDesigner.getState().reveal(def.tabId, null);
  }
}

/** The definition a reference names when the workspace holds it, else undefined. */
export function definitionOf(resolver: Resolver, kind: RefKind, owner: string, name: string, fromTab: string): Definition | undefined {
  const def = resolver.resolve(kind, owner, name, fromTab);
  return def !== null && def !== 'external' && def.tabId !== null ? def : undefined;
}

const kindBadges: Record<TabKind, string> = { menu: 'M', template: 'T', tooltip: 'Tip', owner: 'O' };

/** The workspace's tabs; read-only (phone width): switching tabs only. */
export function TabStrip({ readOnly = false }: { readOnly?: boolean }) {
  const workspace = useDesigner(s => s.workspace);
  const saved = useDesigner(s => s.saved);
  const { openTab, closeTab, moveTab, addTab } = useDesigner.getState();
  const [dragged, setDragged] = useState<string | null>(null);

  const close = (id: string, dirty: boolean) => {
    if (!dirty || window.confirm('Close this tab? Its unsaved changes are lost.')) {
      closeTab(id);
    }
  };

  const drop = (e: DragEvent, index: number) => {
    e.preventDefault();
    if (dragged !== null) {
      moveTab(dragged, index);
    }
    setDragged(null);
  };

  return (
    <nav className="tabs" role="tablist" aria-label="Workspace tabs" onDragOver={e => e.preventDefault()} onDrop={e => drop(e, workspace.tabs.length)}>
      {workspace.tabs.map((tab, i) => {
        const dirty = saved[tab.id] !== tab;
        const active = tab.id === workspace.activeTab;
        return (
          <div key={tab.id} role="tab" aria-selected={active} tabIndex={active ? 0 : -1} draggable={!readOnly} className={active ? 'tab active' : 'tab'}
            title={`${tabKindLabels[tab.kind]} ${tabOwner(tab)}/${tabName(tab)}`} onClick={() => openTab(tab.id)}
            onKeyDown={e => { if (e.key === 'Enter') { openTab(tab.id); } }}
            onDragStart={() => setDragged(tab.id)} onDragOver={e => e.preventDefault()} onDrop={e => { e.stopPropagation(); drop(e, i); }}>
            <span className={`tab-kind ${tab.kind}`}>{kindBadges[tab.kind]}</span>
            <span className="tab-name">{tabName(tab)}</span>
            {dirty && <span className="tab-dirty" title="Changed since the workspace was opened or saved">●</span>}
            {workspace.tabs.length > 1 && !readOnly && (
              <button type="button" className="icon tab-close" title="Close the tab" onClick={e => { e.stopPropagation(); close(tab.id, dirty); }}>×</button>
            )}
          </div>
        );
      })}
      {!readOnly && <select className="tab-add" value="" aria-label="New tab" onChange={e => { if (e.target.value) { addTab(e.target.value as TabKind); } }}>
        <option value="">+</option>
        <option value="menu">New menu</option>
        <option value="template">New owner template</option>
        <option value="tooltip">New named tooltip</option>
        {!workspace.tabs.some(t => t.kind === 'owner') && <option value="owner">New owner entry</option>}
      </select>}
    </nav>
  );
}

const sections: { kind: RefKind; title: string }[] = [
  { kind: 'menu', title: 'Menus' },
  { kind: 'template', title: 'Templates' },
  { kind: 'tooltip', title: 'Tooltips' },
  { kind: 'class', title: 'Classes' },
  { kind: 'sprite', title: 'Sprites' }
];

/** Every definition of the workspace by kind, with its usage count; a click opens it. */
export function WorkspacePane() {
  const resolver = useResolver();
  const activeId = useDesigner(s => s.workspace.activeTab);

  return (
    <details className="workspace-pane" open>
      <summary className="pane-title">Workspace</summary>
      <div className="workspace-body">
        {sections.map(({ kind, title }) => {
          const defs = resolver.definitions.filter(d => d.kind === kind);
          return defs.length > 0 && (
            <div key={kind} className="ws-group">
              <div className="palette-group-name">{title}</div>
              {defs.map((d, i) => (
                <button key={i} type="button" className={d.tabId === activeId && d.nodeId === undefined && kind !== 'class' ? 'ws-item active' : 'ws-item'}
                  disabled={d.tabId === null} title={`${d.owner}/${d.name}${d.nodeId !== undefined ? ' (menu template)' : ''}`} onClick={() => revealDefinition(d)}>
                  <span className="ws-name">{d.name}</span>
                  <span className="ws-count" title="Uses in the workspace">{resolver.usages(d).length}</span>
                </button>
              ))}
            </div>
          );
        })}
      </div>
    </details>
  );
}

const spritePrefixes = ['sprite:', 'item:', 'asset:'];

/** The pickers' suggestions: the names the active tab can reference (ui/fields/widgets.tsx refListId). */
export function WorkspaceLists() {
  const resolver = useResolver();
  const tab = useDesigner(activeTab);
  const owner = tabOwner(tab);
  const names = (kind: RefKind) => resolver.names(kind, owner, tab.id);
  const menus = resolver.definitions.filter(d => d.kind === 'menu').map(d => `6135.UIFramework_OpenMenuAsChild ${d.owner}/${d.name}`);
  const sprites = resolver.definitions.filter(d => d.kind === 'sprite').map(d => `sprite:${d.owner}/${d.name}`);
  const lists: [RefKind, string[]][] = [
    ['template', names('template').filter(n => tab.kind !== 'template' || n !== tab.name)],
    ['tooltip', names('tooltip')],
    ['class', names('class')],
    ['menu', menus],
    ['sprite', [...spritePrefixes, ...sprites]]
  ];

  return (
    <>
      {lists.map(([kind, values]) => (
        <datalist key={kind} id={refListId(kind)}>
          {values.map(v => <option key={v} value={v} />)}
        </datalist>
      ))}
    </>
  );
}

/** The references to a definition, with links to them. */
export function UsedBy({ def }: { def: Definition }) {
  const resolver = useResolver();
  const usages = resolver.usages(def);
  const reveal = useDesigner(s => s.reveal);

  return (
    <details className="group" open>
      <summary>Used by ({usages.length})</summary>
      {usages.length === 0 ? <p className="note">Nothing in the workspace uses it.</p> : (
        <ul className="used-by">
          {usages.map((r, i) => {
            const tab = resolver.tab(r.tabId);
            const node = tab && r.nodeId !== null ? treeOf(tab)?.nodes[r.nodeId] : undefined;
            return (
              <li key={i}>
                <button type="button" className="link" onClick={() => reveal(r.tabId, r.nodeId)}>
                  {tab ? tabName(tab) : r.tabId} › {node ? (node.fields['Id'] ? `#${node.fields['Id']}` : node.type) : 'entry'} <span className="muted">{r.field}</span>
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </details>
  );
}

/** A definition's name: renaming it updates every reference in the workspace (one undoable step). */
export function RenameRow({ def, label, id }: { def: Definition; label: string; id: string }) {
  const rename = useDesigner(s => s.rename);
  const [error, setError] = useState<string | null>(null);

  return (
    <div className="field set">
      <label htmlFor={id} title="Renaming updates every reference in the workspace.">{label}</label>
      <div className="control">
        <TextWidget id={id} mono value={def.name} commit={v => setError(v === undefined ? 'The name must not be empty.' : v === def.name ? null : rename(def, v))} />
      </div>
      {error && <div className="help error">{error}</div>}
    </div>
  );
}

/** Go-to targets of a node's reference fields (Type / Template, Class, RichTooltip, menu actions), by field. */
export function useNodeTargets(nodeId: NodeId): Map<string, Definition> {
  const resolver = useResolver();
  const tabId = useDesigner(s => s.workspace.activeTab);
  const targets = new Map<string, Definition>();
  for (const r of resolver.references) {
    if (r.tabId === tabId && r.nodeId === nodeId && !targets.has(r.field)) {
      const def = definitionOf(resolver, r.kind, r.owner, r.name, tabId);
      if (def) {
        targets.set(r.field, def);
      }
    }
  }

  return targets;
}
