import {
  closestCenter, DndContext, DragOverlay, KeyboardSensor, PointerSensor, pointerWithin, useSensor, useSensors,
  type CollisionDetection, type DragEndEvent, type DragMoveEvent, type DragStartEvent
} from '@dnd-kit/core';
import { sortableKeyboardCoordinates } from '@dnd-kit/sortable';
import { useEffect, useState } from 'react';
import type { NodeId, NodeTree } from '../model/document';
import { acceptsChildren, moveRefusal, parentOf } from '../model/ops';
import { activeTree, useDesigner } from '../model/store';
import { Palette } from './Palette';
import { focusRow, Tree, type DropTarget } from './Tree';
import { WorkspacePane } from './WorkspacePane';

// The left column: the workspace's definitions, then (for tabs with a node tree) palette above tree, sharing one
// DndContext so palette items can be dropped into the tree. Read-only (phone width): the tree alone, nothing to drag.
// A drop lands before / after the row under the pointer, or inside it when it holds children (middle of the row).

/** Pointer collisions while a pointer drives the drag, nearest row for keyboard drags. */
const collision: CollisionDetection = args => args.pointerCoordinates ? pointerWithin(args) : closestCenter(args);

interface Dragged {
  /** The dragged tree node, or null for a palette type. */
  nodeId: NodeId | null;
  /** The node's or the palette item's type. */
  type: string;
}

export function LeftPane({ readOnly = false }: { readOnly?: boolean }) {
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates })
  );
  const [dragged, setDragged] = useState<Dragged | null>(null);
  const [drop, setDrop] = useState<DropTarget | null>(null);
  const tree = useDesigner(activeTree);

  useEffect(() => {
    document.body.classList.toggle('drop-refused', drop?.refusal != null);
  }, [drop]);

  const onDragStart = ({ active }: DragStartEvent) => {
    const paletteType = active.data.current?.['paletteType'] as string | undefined;
    const node = activeTree(useDesigner.getState())?.nodes[String(active.id)];
    setDragged(paletteType ? { nodeId: null, type: paletteType } : { nodeId: String(active.id), type: node?.type ?? '' });
  };

  const onDragMove = ({ active, over, activatorEvent, delta }: DragMoveEvent) => {
    if (!dragged || !over) {
      setDrop(null);
      return;
    }

    const translated = active.rect.current.translated;
    const y = 'clientY' in activatorEvent
      ? (activatorEvent as PointerEvent).clientY + delta.y
      : translated ? translated.top + translated.height / 2 : 0;
    const current = activeTree(useDesigner.getState());
    const next = current ? computeDrop(current, dragged, String(over.id), y) : null;
    setDrop(prev => sameDrop(prev, next) ? prev : next);
  };

  const finish = () => {
    setDragged(null);
    setDrop(null);
  };

  const onDragEnd = (_: DragEndEvent) => {
    const target = drop;
    const source = dragged;
    finish();
    if (!target || !source || target.refusal) {
      return;
    }

    const store = useDesigner.getState();
    if (source.nodeId === null) {
      const id = store.addNode(target.parentId, source.type, target.index);
      if (id) {
        focusRow(id);
      }
    } else if (store.moveNode(source.nodeId, target.parentId, target.index)) {
      store.toggleCollapsed(target.parentId, false);
      store.select(source.nodeId);
      focusRow(source.nodeId);
    }
  };

  return (
    <DndContext sensors={readOnly ? [] : sensors} collisionDetection={collision} onDragStart={onDragStart} onDragMove={onDragMove}
      onDragOver={onDragMove} onDragEnd={onDragEnd} onDragCancel={finish}>
      <div className="pane left">
        {!readOnly && <WorkspacePane />}
        {tree && !readOnly && <Palette />}
        {tree && <Tree tree={tree} drop={drop} dragging={dragged !== null} readOnly={readOnly} />}
      </div>
      <DragOverlay dropAnimation={null}>
        {dragged && <div className={drop?.refusal ? 'drag-chip refused' : 'drag-chip'}>{dragged.type}</div>}
      </DragOverlay>
    </DndContext>
  );
}

function sameDrop(a: DropTarget | null, b: DropTarget | null): boolean {
  return a === b || (a !== null && b !== null && a.overId === b.overId && a.zone === b.zone && a.refusal === b.refusal);
}

/** Where a dragged node or palette element lands when released at client `y` over the row of `overId`. */
function computeDrop(doc: NodeTree, { nodeId, type }: Dragged, overId: NodeId, y: number): DropTarget | null {
  const over = doc.nodes[overId];
  const rect = document.querySelector(`.tree [data-node-id="${CSS.escape(overId)}"]`)?.getBoundingClientRect();
  if (!over || !rect || overId === nodeId) {
    return null;
  }

  const rel = (y - rect.top) / Math.max(1, rect.height);
  const isRoot = overId === doc.root;
  const zone: DropTarget['zone'] = isRoot ? 'inside'
    : acceptsChildren(over.type) ? (rel < 0.25 ? 'before' : rel > 0.75 ? 'after' : 'inside')
    : rel < 0.5 ? 'before' : 'after';

  if (zone === 'inside') {
    const children = over.children.filter(c => c !== nodeId);
    return { overId, zone, parentId: overId, index: children.length, refusal: moveRefusal(doc, nodeId, overId, type) };
  }

  const parentId = parentOf(doc, overId)!;
  const siblings = doc.nodes[parentId]!.children.filter(c => c !== nodeId);
  const index = siblings.indexOf(overId) + (zone === 'after' ? 1 : 0);
  return { overId, zone, parentId, index, refusal: moveRefusal(doc, nodeId, parentId, type) };
}
