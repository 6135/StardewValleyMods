import type { DesignerNode, NodeId, NodeTree } from './document';
import { createNode, newNodeId } from './factory';
import { isContainer, isTooltipBlock, typeInfo } from './metadata';
import { subItemKind, subItemsOf } from './subItems';

// Pure tree operations on a NodeTree (a menu, an owner template or a named tooltip; no React). Mutating functions take a draft (Immer) or a tree the
// caller owns; they return false / null when the operation is refused and leave the document untouched.

/** The parent of a node, or null for a root (or an unknown id). */
export function parentOf(doc: NodeTree, id: NodeId): NodeId | null {
  for (const node of Object.values(doc.nodes)) {
    if (node.children.includes(id)) {
      return node.id;
    }
  }

  return null;
}

/** True when `id` is `ancestor` or lies in its subtree. */
export function isInSubtree(doc: NodeTree, ancestor: NodeId, id: NodeId): boolean {
  for (let current: NodeId | null = id; current !== null; current = parentOf(doc, current)) {
    if (current === ancestor) {
      return true;
    }
  }

  return false;
}

/**
 * True when nodes of this type can hold children: containers, the synthetic Menu / Template / Tooltip roots, Repeat (its
 * children are the row template, so they are authored like a container's even though IsContainer excludes it), the
 * holders of sub-items (Form, DataGrid) and sub-items with an elements member (a Column's Cell).
 */
export function acceptsChildren(type: string): boolean {
  return type === 'Menu' || type === 'Tooltip' || isContainer(type) || (typeInfo(type)?.members.includes('Children') ?? false)
    || subItemsOf(type) !== undefined || subItemKind(type)?.elements !== undefined;
}

/** Why a node of `type` cannot go into a node of `parentType`, or null when it can. */
export function placementRefusal(parentType: string, type: string): string | null {
  if (!acceptsChildren(parentType)) {
    return `${parentType} cannot hold children.`;
  }

  if (parentType === 'Tooltip' || isTooltipBlock(type)) {
    return parentType === 'Tooltip' && isTooltipBlock(type) ? null : parentType === 'Tooltip'
      ? 'A tooltip holds only blocks (Title, Line, Icon …).' : 'A tooltip block goes only in a named tooltip.';
  }

  const items = subItemsOf(parentType);
  if (items !== undefined) {
    return type === items.type ? null : `A ${parentType} holds only ${items.title.toLowerCase()} items.`;
  }

  const kind = subItemKind(type);
  return kind !== undefined ? `A ${kind.title.toLowerCase()} goes only in a ${kind.parent}.` : null;
}

/** Why a node of `type` (an existing node `nodeId`, or null for a new one) cannot go into `parentId`, or null when it can. */
export function moveRefusal(doc: NodeTree, nodeId: NodeId | null, parentId: NodeId, type: string): string | null {
  const parent = doc.nodes[parentId];
  if (!parent) {
    return 'Unknown target.';
  }

  const refusal = placementRefusal(parent.type, type);
  if (refusal !== null) {
    return refusal;
  }

  if (nodeId !== null && isInSubtree(doc, nodeId, parentId)) {
    return 'An element cannot move into itself.';
  }

  return null;
}

/** Every element `Id` set in the document (case-insensitive, lower-cased). */
function usedIds(doc: NodeTree): Set<string> {
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
export function suggestId(doc: NodeTree, type: string, existing?: string, taken = usedIds(doc)): string {
  const fromType = type.split('.').pop()!.replace(/[^A-Za-z0-9_]/g, '') || 'element';
  const base = existing ? existing.replace(/\d+$/, '') || fromType : fromType.charAt(0).toLowerCase() + fromType.slice(1);
  for (let n = 1; ; n++) {
    const candidate = `${base}${n}`;
    if (!taken.has(candidate.toLowerCase())) {
      return candidate;
    }
  }
}

/** Starter fields so a new element (or sub-item: an id and its caption) shows something. */
function starterFields(doc: NodeTree, type: string): Record<string, string> {
  const kind = subItemKind(type);
  if (kind !== undefined) {
    return { Id: suggestId(doc, kind.title), [kind.caption]: kind.title };
  }

  if (isTooltipBlock(type)) {
    return type === 'Title' || type === 'Line' ? { Text: type } : {};
  }

  const id = suggestId(doc, type);
  switch (type) {
    case 'Label': return { Id: id, Text: 'Label' };
    case 'Button': return { Id: id, Text: 'Button' };
    case 'Checkbox': return { Id: id, Label: 'Checkbox' };
    default: return { Id: id };
  }
}

/** Add a new element of `type` under `parentId` at `index` (default: last); returns its node id, or null when refused. */
export function addNode(doc: NodeTree, parentId: NodeId, type: string, index?: number): NodeId | null {
  if (moveRefusal(doc, null, parentId, type)) {
    return null;
  }

  const node = createNode(type, starterFields(doc, type));
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
export function moveNode(doc: NodeTree, nodeId: NodeId, newParentId: NodeId, index: number): boolean {
  const oldParentId = parentOf(doc, nodeId);
  if (oldParentId === null || moveRefusal(doc, nodeId, newParentId, doc.nodes[nodeId]!.type)) {
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
export function duplicateNode(doc: NodeTree, nodeId: NodeId): NodeId | null {
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
export function deleteNode(doc: NodeTree, nodeId: NodeId): boolean {
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
/** Set or remove (undefined) a non-string member; it replaces a string value of the same name. */
export function setExtra(doc: NodeTree, nodeId: NodeId, field: string, value: unknown): boolean {
  const node = doc.nodes[nodeId];
  if (!node) {
    return false;
  }

  delete node.fields[field];
  if (value === undefined) {
    delete node.extra[field];
  } else {
    node.extra[field] = value;
  }

  return true;
}

export function setField(doc: NodeTree, nodeId: NodeId, field: string, value: string | undefined): boolean {
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
export function preorder(doc: NodeTree, root: NodeId, skip?: (id: NodeId) => boolean): NodeId[] {
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
