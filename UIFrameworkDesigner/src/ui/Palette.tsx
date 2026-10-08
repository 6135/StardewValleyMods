import { useDraggable } from '@dnd-kit/core';
import type { PointerEventHandler } from 'react';
import { elementTypes, tooltipBlockTypes } from '../model/metadata';
import { activeTab, activeTree, activeUi, useDesigner } from '../model/store';
import { moveRefusal, parentOf } from '../model/ops';
import { subItemKind, subItemsOf } from '../model/subItems';
import { tabOwner } from '../model/workspace';
import { useResolver } from './WorkspacePane';

// The palette (architecture.md §6.3): every element type, grouped, and while a Form / DataGrid (or one of its items) is
// selected its item type (Field / Column). Click (or Enter) adds to the selection, a pointer drag places it in the tree.

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
  // the sub-items of the selected Form / DataGrid (or of the selected item's holder): "Field" / "Column"
  const items = useDesigner(s => {
    const selection = activeUi(s).selection;
    const selected = selection !== null ? activeTree(s)?.nodes[selection] : undefined;
    return selected ? subItemsOf(selected.type) ?? subItemKind(selected.type) : undefined;
  });
  const tab = useDesigner(activeTab);
  const resolver = useResolver();
  // a named tooltip holds blocks; menus and templates hold elements, and instances of the templates they can use
  const templates = resolver.names('template', tabOwner(tab), tab.id).filter(n => tab.kind !== 'template' || n !== tab.name);
  const groups: [string, string[]][] = tab.kind === 'tooltip' ? [['Blocks', [...tooltipBlockTypes]]]
    : templates.length > 0 ? [...paletteGroups(), ['Templates', templates]] : paletteGroups();

  return (
    <section className="palette" aria-label="Palette">
      <div className="pane-title">Palette</div>
      <div className="palette-body">
        {items && (
          <div className="palette-group">
            <div className="palette-group-name">{items.parent}</div>
            <div className="palette-items">
              <PaletteItem type={items.type} label={items.title} />
            </div>
          </div>
        )}
        {groups.map(([name, types]) => (
          <div key={name} className="palette-group">
            <div className="palette-group-name">{name}</div>
            <div className="palette-items">
              {types.map(t => <PaletteItem key={t} type={t} label={t} />)}
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}

function PaletteItem({ type, label }: { type: string; label: string }) {
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({ id: paletteDragId(type), data: { paletteType: type } });
  return (
    <button ref={setNodeRef} type="button" className={isDragging ? 'palette-item dragging' : 'palette-item'}
      title={`Add a ${label} (click) or drag it into the tree`} role={attributes.role} aria-disabled={attributes['aria-disabled']} aria-pressed={attributes['aria-pressed']}
      aria-roledescription={attributes['aria-roledescription']} aria-describedby={attributes['aria-describedby']} tabIndex={0}
      onPointerDown={listeners?.['onPointerDown'] as PointerEventHandler | undefined} onClick={() => addFromPalette(type)}>
      {label}
    </button>
  );
}

/** Add into the selection when it can hold the type, else after the selection, or to the root. */
function addFromPalette(type: string): void {
  const state = useDesigner.getState();
  const doc = activeTree(state);
  const selection = activeUi(state).selection;
  if (!doc) {
    return;
  }

  const { addNode } = state;
  const selected = selection !== null ? doc.nodes[selection] : undefined;
  if (!selected || moveRefusal(doc, null, selected.id, type) === null) {
    addNode(selected?.id ?? doc.root, type);
    return;
  }

  const parent = parentOf(doc, selected.id) ?? doc.root;
  addNode(parent, type, doc.nodes[parent]!.children.indexOf(selected.id) + 1);
}
