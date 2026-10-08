import { useSortable, SortableContext } from '@dnd-kit/sortable';
import type { KeyboardEvent, KeyboardEventHandler, PointerEventHandler } from 'react';
import type { DesignerNode, NodeId, NodeTree } from '../model/document';
import { acceptsChildren, parentOf } from '../model/ops';
import { subItemKind } from '../model/subItems';
import { activeTab, activeUi, useDesigner } from '../model/store';
import { tabKindLabels, tabName, type WorkspaceTab } from '../model/workspace';

// The element tree (architecture.md §6.3). Rows are dnd-kit sortables that stay in place while dragging (the drop
// position is shown as a line / outline instead); LeftPane owns the DndContext and computes the drop target.

export interface DropTarget {
  overId: NodeId;
  zone: 'before' | 'inside' | 'after';
  parentId: NodeId;
  /** Index in the parent's children without the dragged node. */
  index: number;
  /** Why the drop is refused, or null. */
  refusal: string | null;
}

export interface VisibleRow {
  id: NodeId;
  depth: number;
}

/** The rows shown, in order (children of collapsed nodes hidden). */
export function visibleRows(doc: NodeTree, collapsed: Record<NodeId, true>): VisibleRow[] {
  const rows: VisibleRow[] = [];
  const walk = (id: NodeId, depth: number): void => {
    rows.push({ id, depth });
    if (!collapsed[id]) {
      doc.nodes[id]?.children.forEach(c => walk(c, depth + 1));
    }
  };
  walk(doc.root, 0);
  return rows;
}

/** Focus the row of a node (after keyboard selection). */
export function focusRow(id: NodeId): void {
  requestAnimationFrame(() => document.querySelector<HTMLElement>(`.tree [data-node-id="${CSS.escape(id)}"]`)?.focus());
}

/** No item shifting while dragging: the tree shows the drop target instead. */
const stayInPlace = () => null;

/** The tree around the selected row, for keyboard navigation. */
interface TreeNav {
  rows: VisibleRow[];
  collapsed: Record<NodeId, true>;
  current: NodeId;
  at: number;
  node: DesignerNode;
  parent: NodeId | null;
  go(id: NodeId | undefined): void;
  toggleCollapsed(id: NodeId, value: boolean): void;
}

/** Arrow keys move through the rows (Left / Right collapse, expand or go to the parent / first child), Home / End jump. */
const treeKeys: Record<string, (nav: TreeNav) => void> = {
  ArrowUp: nav => nav.go(nav.rows[nav.at - 1]?.id),
  ArrowDown: nav => nav.go(nav.rows[nav.at + 1]?.id),
  ArrowLeft: nav => {
    if (nav.node.children.length > 0 && !nav.collapsed[nav.current]) {
      nav.toggleCollapsed(nav.current, true);
    } else {
      nav.go(nav.parent ?? undefined);
    }
  },
  ArrowRight: nav => {
    if (nav.collapsed[nav.current]) {
      nav.toggleCollapsed(nav.current, false);
    } else {
      nav.go(nav.node.children[0]);
    }
  },
  Home: nav => nav.go(nav.rows[0]?.id),
  End: nav => nav.go(nav.rows[nav.rows.length - 1]?.id)
};

/** Handles a key on the tree (Alt+Up / Down moves the selected node among its siblings); false when the key is not used. */
function handleTreeKey(e: KeyboardEvent<HTMLDivElement>, doc: NodeTree, rows: VisibleRow[], collapsed: Record<NodeId, true>, readOnly: boolean): boolean {
  const { select, toggleCollapsed, moveNode } = useDesigner.getState();
  const selection = activeUi(useDesigner.getState()).selection;
  const current = selection !== null && doc.nodes[selection] ? selection : doc.root;
  const parent = parentOf(doc, current);
  if (e.altKey && (e.key === 'ArrowUp' || e.key === 'ArrowDown')) {
    if (parent !== null && !readOnly) {
      const index = doc.nodes[parent]!.children.indexOf(current) + (e.key === 'ArrowUp' ? -1 : 1);
      if (index >= 0 && moveNode(current, parent, index)) {
        focusRow(current);
      }
    }
    return true;
  }
  const handler = Object.prototype.hasOwnProperty.call(treeKeys, e.key) ? treeKeys[e.key]! : null;
  if (!handler) {
    return false;
  }
  const go = (id: NodeId | undefined) => {
    if (id !== undefined) {
      select(id);
      focusRow(id);
    }
  };
  handler({ rows, collapsed, current, at: rows.findIndex(r => r.id === current), node: doc.nodes[current]!, parent, go, toggleCollapsed });
  return true;
}

export function Tree({ tree: doc, drop, dragging, readOnly = false }: { tree: NodeTree; drop: DropTarget | null; dragging: boolean; readOnly?: boolean }) {
  const tab = useDesigner(activeTab);
  const collapsed = useDesigner(s => activeUi(s).collapsed);
  const rows = visibleRows(doc, collapsed);

  const onKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    if (!dragging && handleTreeKey(e, doc, rows, collapsed, readOnly)) {
      e.preventDefault();
    }
  };

  return (
    <section className="tree-pane" aria-label="Element tree">
      <div className="pane-title">Tree</div>
      <SortableContext items={rows.map(r => r.id)} strategy={stayInPlace}>
        <div className="tree" role="tree" aria-label="Elements" onKeyDown={onKeyDown}>
          {rows.map(r => <Row key={r.id} node={doc.nodes[r.id]!} depth={r.depth} isRoot={r.id === doc.root} tab={tab}
            collapsed={collapsed[r.id] === true} drop={drop?.overId === r.id ? drop : null} />)}
        </div>
      </SortableContext>
      {drop?.refusal && <div className="drop-refusal" role="status">{drop.refusal}</div>}
    </section>
  );
}

/**
 * A short text preview of a node: its text, label or main value (a sub-item's caption: a field's Label, a column's
 * Header); for the root, the menu title or the template / tooltip name.
 */
function preview(node: DesignerNode, tab: WorkspaceTab, isRoot: boolean): string | undefined {
  if (isRoot) {
    return tab.kind === 'menu' ? tab.doc.menu['Title'] : tabName(tab);
  }

  const f = node.fields;
  const kind = subItemKind(node.type);
  if (kind !== undefined) {
    return f[kind.caption];
  }

  return f['Text'] ?? f['Label'] ?? f['Button'] ?? f['Checkbox'] ?? f['Switch'] ?? f['Repeat'] ?? f['Source'] ?? f['Sprite'] ?? f['Image'] ?? f['Item'] ?? f['Case'];
}

function Row({ node, depth, isRoot, tab, collapsed, drop }: { node: DesignerNode; depth: number; isRoot: boolean; tab: WorkspaceTab; collapsed: boolean; drop: DropTarget | null }) {
  const selected = useDesigner(s => activeUi(s).selection === node.id || (activeUi(s).selection === null && isRoot));
  const select = useDesigner(s => s.select);
  const toggleCollapsed = useDesigner(s => s.toggleCollapsed);
  const { attributes, listeners, setNodeRef, isDragging } = useSortable({ id: node.id, disabled: { draggable: isRoot, droppable: false } });
  const text = preview(node, tab, isRoot);
  const elementId = isRoot ? undefined : node.fields['Id'];
  const hasChildren = node.children.length > 0;
  const classes = ['row', selected && 'selected', isDragging && 'dragging', !isRoot && !acceptsChildren(node.type) && 'leaf',
    drop && `drop-${drop.zone}`, drop?.refusal && 'drop-refused'].filter(Boolean).join(' ');

  return (
    <div ref={setNodeRef} aria-disabled={attributes['aria-disabled']} aria-pressed={attributes['aria-pressed']}
      aria-roledescription={attributes['aria-roledescription']} aria-describedby={attributes['aria-describedby']}
      onPointerDown={listeners?.['onPointerDown'] as PointerEventHandler | undefined}
      onKeyDown={listeners?.['onKeyDown'] as KeyboardEventHandler | undefined} role="treeitem" aria-level={depth + 1} aria-selected={selected}
      aria-expanded={hasChildren ? !collapsed : undefined} tabIndex={selected ? 0 : -1} data-node-id={node.id}
      className={classes} style={{ paddingLeft: `${depth * 14 + 4}px` }}
      onClick={() => select(node.id)}>
      <button type="button" className="caret" tabIndex={-1} aria-label={collapsed ? 'Expand' : 'Collapse'} hidden={!hasChildren}
        onClick={e => { e.stopPropagation(); toggleCollapsed(node.id); }} onPointerDown={e => e.stopPropagation()}>
        {collapsed ? '▸' : '▾'}
      </button>
      <span className="row-type">{isRoot ? tabKindLabels[tab.kind] : node.type}</span>
      {elementId && <span className="row-id">#{elementId}</span>}
      {text && <span className="row-text">{text}</span>}
    </div>
  );
}
