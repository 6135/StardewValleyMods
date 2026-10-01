import type { DesignerDocument, DesignerNode, NodeId } from './document';
import { createNode, newNodeId } from './factory';
import { isContainer, typeInfo } from './metadata';

// Pure tree operations on a DesignerDocument (no React). Mutating functions take a draft (Immer) or a document the
// caller owns; they return false / null when the operation is refused and leave the document untouched.

/** The parent of a node, or null for a root (or an unknown id). */
export function parentOf(doc: DesignerDocument, id: NodeId): NodeId | null {
  for (const node of Object.values(doc.nodes)) {
    if (node.children.includes(id)) {
      return node.id;
    }
  }

  return null;
}

/** True when `id` is `ancestor` or lies in its subtree. */
export function isInSubtree(doc: DesignerDocument, ancestor: NodeId, id: NodeId): boolean {
  for (let current: NodeId | null = id; current !== null; current = parentOf(doc, current)) {
    if (current === ancestor) {
      return true;
    }
  }

  return false;
}

/**
 * True when nodes of this type can hold children: containers, the synthetic Menu / Template roots, and Repeat (its
 * children are the row template, so they are authored like a container's even though IsContainer excludes it).
 */
export function acceptsChildren(type: string): boolean {
  return type === 'Menu' || isContainer(type) || (typeInfo(type)?.members.includes('Children') ?? false);
}

/** Why a node cannot go into `parentId`, or null when it can. */
export function moveRefusal(doc: DesignerDocument, nodeId: NodeId | null, parentId: NodeId): string | null {
  const parent = doc.nodes[parentId];
  if (!parent) {
    return 'Unknown target.';
  }

  if (!acceptsChildren(parent.type)) {
    return `${parent.type} cannot hold children.`;
  }

  if (nodeId !== null && isInSubtree(doc, nodeId, parentId)) {
    return 'An element cannot move into itself.';
  }

  return null;
}

/** Every element `Id` set in the document (case-insensitive, lower-cased). */
function usedIds(doc: DesignerDocument): Set<string> {
  const ids = new Set<string>();
  for (const node of Object.values(doc.nodes)) {
    const id = node.fields['Id'];
    if (id) {
      ids.add(id.trim().toLowerCase());
    }
  }

  return ids;
}

/** A unique element id for a new node of `type` or a copy of `existing`: label1, button2, textInput1 … */
export function suggestId(doc: DesignerDocument, type: string, existing?: string, taken = usedIds(doc)): string {
  const fromType = type.split('.').pop()!.replace(/[^A-Za-z0-9_]/g, '') || 'element';
  const base = existing ? existing.replace(/\d+$/, '') || fromType : fromType.charAt(0).toLowerCase() + fromType.slice(1);
  for (let n = 1; ; n++) {
    const candidate = `${base}${n}`;
    if (!taken.has(candidate.toLowerCase())) {
      return candidate;
    }
  }
}

/** Starter fields so a new element shows something. */
function starterFields(type: string): Record<string, string> {
  switch (type) {
    case 'Label': return { Text: 'Label' };
    case 'Button': return { Text: 'Button' };
    case 'Checkbox': return { Label: 'Checkbox' };
    default: return {};
  }
}

/** Add a new element of `type` under `parentId` at `index` (default: last); returns its node id, or null when refused. */
export function addNode(doc: DesignerDocument, parentId: NodeId, type: string, index?: number): NodeId | null {
  if (moveRefusal(doc, null, parentId)) {
    return null;
  }

  const node = createNode(type, { Id: suggestId(doc, type), ...starterFields(type) });
  doc.nodes[node.id] = node;
  insertChild(doc.nodes[parentId]!, node.id, index);
  return node.id;
}

function insertChild(parent: DesignerNode, id: NodeId, index?: number): void {
  const at = index === undefined ? parent.children.length : Math.max(0, Math.min(index, parent.children.length));
  parent.children.splice(at, 0, id);
}

/**
 * Move a node under `newParentId` at `index`, counted in the new parent's children after the node was taken out of its
 * old place. Refused (false) into a node that cannot hold children, into the node's own subtree, or for a root.
 */
export function moveNode(doc: DesignerDocument, nodeId: NodeId, newParentId: NodeId, index: number): boolean {
  const oldParentId = parentOf(doc, nodeId);
  if (oldParentId === null || moveRefusal(doc, nodeId, newParentId)) {
    return false;
  }

  const oldParent = doc.nodes[oldParentId]!;
  const newParent = doc.nodes[newParentId]!;
  const oldIndex = oldParent.children.indexOf(nodeId);
  if (oldParentId === newParentId && oldIndex === Math.max(0, Math.min(index, oldParent.children.length - 1))) {
    return false;
  }

  oldParent.children.splice(oldIndex, 1);
  insertChild(newParent, nodeId, index);
  return true;
}

/** Copy a node and its subtree right after it, with fresh node ids and element ids; returns the copy's id. */
export function duplicateNode(doc: DesignerDocument, nodeId: NodeId): NodeId | null {
  const parentId = parentOf(doc, nodeId);
  if (parentId === null) {
    return null;
  }

  const taken = usedIds(doc);
  const copy = (id: NodeId): NodeId => {
    const source = doc.nodes[id]!;
    const fields = { ...source.fields };
    const elementId = fields['Id'];
    if (elementId && /^[A-Za-z0-9_]+$/.test(elementId.trim())) {
      fields['Id'] = suggestId(doc, source.type, elementId.trim(), taken);
      taken.add(fields['Id'].toLowerCase());
    }

    const clone: DesignerNode = {
      ...source,
      id: newNodeId(),
      fields,
      extra: structuredClone(source.extra),
      children: source.children.map(copy)
    };
    doc.nodes[clone.id] = clone;
    return clone.id;
  };

  const cloneId = copy(nodeId);
  const parent = doc.nodes[parentId]!;
  parent.children.splice(parent.children.indexOf(nodeId) + 1, 0, cloneId);
  return cloneId;
}

/** Delete a node and its subtree; false for a root. */
export function deleteNode(doc: DesignerDocument, nodeId: NodeId): boolean {
  const parentId = parentOf(doc, nodeId);
  if (parentId === null) {
    return false;
  }

  const parent = doc.nodes[parentId]!;
  parent.children.splice(parent.children.indexOf(nodeId), 1);
  const remove = (id: NodeId): void => {
    const node = doc.nodes[id];
    if (node) {
      node.children.forEach(remove);
      delete doc.nodes[id];
    }
  };
  remove(nodeId);
  return true;
}

/** Set (or with undefined, remove) a string field of a node. */
export function setField(doc: DesignerDocument, nodeId: NodeId, field: string, value: string | undefined): boolean {
  const node = doc.nodes[nodeId];
  if (!node || node.fields[field] === value) {
    return false;
  }

  if (value === undefined) {
    delete node.fields[field];
  } else {
    node.fields[field] = value;
  }

  return true;
}

/** The node ids of the tree under `root` in display order (pre-order), the root first. */
export function preorder(doc: DesignerDocument, root: NodeId, skip?: (id: NodeId) => boolean): NodeId[] {
  const out: NodeId[] = [];
  const walk = (id: NodeId): void => {
    out.push(id);
    if (!skip?.(id)) {
      doc.nodes[id]?.children.forEach(walk);
    }
  };
  walk(root);
  return out;
}
