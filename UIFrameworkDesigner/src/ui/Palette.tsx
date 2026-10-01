import { useDraggable } from '@dnd-kit/core';
import type { PointerEventHandler } from 'react';
import { elementTypes } from '../model/metadata';
import { useDesigner } from '../model/store';
import { acceptsChildren, parentOf } from '../model/ops';

// The palette (architecture.md §6.3): every element type, grouped. Click (or Enter) adds to the selection, a pointer
// drag places it in the tree.

const groups: [string, string[]][] = [
  ['Layout', ['Stack', 'Grid', 'Panel', 'Canvas', 'ScrollView', 'Spacer', 'Slot']],
  ['Text & images', ['Label', 'Image', 'ItemImage']],
  ['Inputs', ['Button', 'Checkbox', 'TextInput', 'NumberInput', 'Dropdown', 'Slider']],
  ['Collections', ['Repeat', 'List', 'DataGrid', 'Form']],
  ['Structure', ['Switch', 'Composite', 'Template', 'Outlet']]
];

/** Palette groups limited to the types the metadata knows, plus "Other" for the rest. */
function paletteGroups(): [string, string[]][] {
  const known = new Set(elementTypes.types.map(t => t.name));
  const listed = new Set(groups.flatMap(([, types]) => types));
  const result = groups.map(([name, types]): [string, string[]] => [name, types.filter(t => known.has(t))]);
  result.push(['Other', elementTypes.types.map(t => t.name).filter(t => !listed.has(t))]);
  return result.filter(([, types]) => types.length > 0);
}

/** The drag id of a palette type. */
export const paletteDragId = (type: string) => `palette:${type}`;

export function Palette() {
  return (
    <section className="palette" aria-label="Palette">
      <div className="pane-title">Palette</div>
      <div className="palette-body">
        {paletteGroups().map(([name, types]) => (
          <div key={name} className="palette-group">
            <div className="palette-group-name">{name}</div>
            <div className="palette-items">
              {types.map(t => <PaletteItem key={t} type={t} />)}
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}

function PaletteItem({ type }: { type: string }) {
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({ id: paletteDragId(type), data: { paletteType: type } });
  return (
    <button ref={setNodeRef} type="button" className={isDragging ? 'palette-item dragging' : 'palette-item'}
      title={`Add a ${type} (click) or drag it into the tree`} {...attributes} tabIndex={0}
      onPointerDown={listeners?.['onPointerDown'] as PointerEventHandler | undefined} onClick={() => addFromPalette(type)}>
      {type}
    </button>
  );
}

/** Add to the selected container, after a selected leaf, or to the root. */
function addFromPalette(type: string): void {
  const { doc, selection, addNode } = useDesigner.getState();
  const selected = selection !== null ? doc.nodes[selection] : undefined;
  if (!selected || acceptsChildren(selected.type)) {
    addNode(selected?.id ?? doc.root, type);
    return;
  }

  const parent = parentOf(doc, selected.id) ?? doc.root;
  addNode(parent, type, doc.nodes[parent]!.children.indexOf(selected.id) + 1);
}
