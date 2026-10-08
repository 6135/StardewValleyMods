import { beforeEach, describe, expect, it } from 'vitest';
import { addNode, deleteNode, duplicateNode, isInSubtree, moveNode, parentOf, placementRefusal, preorder, setField, suggestId } from './ops';
import { createEmptyDocument, createNode, newNodeId } from './factory';
import { activeTab, useDesigner } from './store';
import { createWorkspace, menuTab } from './workspace';

describe('factory', () => {
  it('makes unique node ids', () => {
    expect(new Set(Array.from({ length: 100 }, newNodeId)).size).toBe(100);
  });

  it('creates an empty document with a Menu root', () => {
    const doc = createEmptyDocument();
    expect(doc.nodes[doc.root]!.type).toBe('Menu');
    expect(doc.menu['Title']).toBe('New menu');
  });
});

describe('ops', () => {
  it('adds, moves, duplicates and deletes nodes', () => {
    const doc = createEmptyDocument();
    const stack = addNode(doc, doc.root, 'Stack')!;
    const label = addNode(doc, stack, 'Label')!;
    const other = addNode(doc, doc.root, 'Panel')!;
    expect(parentOf(doc, label)).toBe(stack);
    expect(isInSubtree(doc, doc.root, label)).toBe(true);
    expect(preorder(doc, doc.root)).toEqual([doc.root, stack, label, other]);

    expect(moveNode(doc, label, other, 0)).toBe(true);
    expect(parentOf(doc, label)).toBe(other);
    expect(moveNode(doc, stack, stack, 0)).toBe(false);

    const copy = duplicateNode(doc, other)!;
    expect(doc.nodes[doc.root]!.children).toEqual([stack, other, copy]);
    expect(doc.nodes[copy]!.children).toHaveLength(1);
    expect(doc.nodes[copy]!.children[0]).not.toBe(label);

    expect(deleteNode(doc, other)).toBe(true);
    expect(doc.nodes[other]).toBeUndefined();
    expect(doc.nodes[label]).toBeUndefined();
    expect(deleteNode(doc, doc.root)).toBe(false);
  });

  it('refuses a child under a leaf element', () => {
    const doc = createEmptyDocument();
    const label = addNode(doc, doc.root, 'Label')!;
    expect(addNode(doc, label, 'Label')).toBeNull();
    expect(placementRefusal('Label', 'Label')).toMatch(/cannot hold children/);
    expect(placementRefusal('Menu', 'Label')).toBeNull();
  });

  it('sets and removes fields', () => {
    const doc = createEmptyDocument();
    const id = addNode(doc, doc.root, 'Label')!;
    expect(setField(doc, id, 'Text', 'hi')).toBe(true);
    expect(doc.nodes[id]!.fields['Text']).toBe('hi');
    expect(setField(doc, id, 'Text', 'hi')).toBe(false);
    expect(setField(doc, id, 'Text', undefined)).toBe(true);
    expect(doc.nodes[id]!.fields['Text']).toBeUndefined();
  });

  it('suggests ids that do not clash', () => {
    const doc = createEmptyDocument();
    const a = createNode('Label', { Id: suggestId(doc, 'Label') });
    doc.nodes[a.id] = a;
    doc.nodes[doc.root]!.children.push(a.id);
    expect(suggestId(doc, 'Label')).not.toBe(a.fields['Id']);
  });
});

describe('store', () => {
  beforeEach(() => useDesigner.getState().replaceWorkspace(createWorkspace([menuTab(createEmptyDocument())])));

  const state = () => useDesigner.getState();
  const doc = () => {
    const t = activeTab(state());
    if (t.kind !== 'menu') {
      throw new Error('expected a menu tab');
    }

    return t.doc;
  };
  const past = () => state().history[state().workspace.activeTab]?.past.length ?? 0;

  it('adds a node, selects it and undoes / redoes', () => {
    const id = state().addNode(doc().root, 'Label')!;
    expect(doc().nodes[id]).toBeDefined();
    expect(state().ui[state().workspace.activeTab]!.selection).toBe(id);

    state().undo();
    expect(doc().nodes[id]).toBeUndefined();
    state().redo();
    expect(doc().nodes[id]).toBeDefined();
  });

  it('records field edits as separate undo steps and clears redo on a new edit', () => {
    const id = state().addNode(doc().root, 'Label')!;
    state().setField(id, 'Text', 'a');
    state().setField(id, 'Text', 'b');
    state().undo();
    expect(doc().nodes[id]!.fields['Text']).toBe('a');
    state().setField(id, 'Text', 'c');
    state().redo();
    expect(doc().nodes[id]!.fields['Text']).toBe('c');
  });

  it('does not record a no-op edit', () => {
    const before = past();
    state().setMenuField('Title', 'New menu');
    expect(past()).toBe(before);
  });

  it('keeps undo history per tab', () => {
    const first = state().workspace.activeTab;
    state().setMenuField('Title', 'One');
    state().addTab('menu');
    state().setMenuField('Title', 'Two');
    state().undo();
    expect(doc().menu['Title']).not.toBe('Two');
    state().openTab(first);
    expect(doc().menu['Title']).toBe('One');
  });

  it('tracks dirty state until markSaved', () => {
    const id = state().workspace.activeTab;
    state().setMenuField('Title', 'Changed');
    expect(state().saved[id]).not.toBe(activeTab(state()));
    state().markSaved();
    expect(state().saved[id]).toBe(activeTab(state()));
  });

  it('caps the history at 200 entries', () => {
    for (let i = 0; i < 250; i++) {
      state().setMenuField('Title', `t${i}`);
    }

    expect(past()).toBe(200);
  });
});
