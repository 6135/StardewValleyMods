import type { DesignerDocument, DesignerNode, NodeId } from './document';

let counter = 0;

/** A new designer-internal node id. */
export function newNodeId(): NodeId {
  counter += 1;
  return `n${Date.now().toString(36)}${counter.toString(36)}`;
}

export function createNode(type: string, fields: Record<string, string> = {}, extra: Record<string, unknown> = {}): DesignerNode {
  return { id: newNodeId(), type, fields, extra, children: [] };
}

/** An empty document: a titled menu with no children. */
export function createEmptyDocument(): DesignerDocument {
  const root = createNode('Menu');
  return {
    owner: '{{ModId}}',
    menuId: 'menu',
    menu: { Title: 'New menu' },
    menuExtra: {},
    root: root.id,
    nodes: { [root.id]: root },
    templates: {},
    previewState: {}
  };
}
