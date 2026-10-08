// JSONC import of elements (import.ts): an ElementDefinition and its child list as designer nodes, shorthands expanded
// like DataValidator.NormalizeType and remembered on the node.
import type { DesignerNode, NodeId, Problem } from '../model/document';
import { createNode } from '../model/factory';
import { canonicalType, elementTypes } from '../model/metadata';
import { subItemsOf, type SubItemKind } from '../model/subItems';
import {
  builtInTypes,
  canonicalMember,
  fieldPath,
  getMember,
  indexPath,
  inferType,
  isObject,
  scalarText,
  suggest,
  typeKind,
  valueShorthands,
  type JsonObject
} from './dataFormat';

/** What converting a menu's / template's elements shares: the node table, the problem list and the template names in scope. */
export interface Context {
  nodes: Record<NodeId, DesignerNode>;
  problems: Problem[];
  isTemplate: (name: string) => boolean;
}


export function convertChildren(list: unknown[], parentPath: string, ctx: Context): NodeId[] {
  return convertElements(list, fieldPath(parentPath, 'Children'), ctx);
}

/** The elements of a list member (`listPath[i]`); a single object (a Cell written as one element) is the list itself. */
function convertElements(list: unknown, listPath: string, ctx: Context): NodeId[] {
  if (isObject(list)) {
    return [convertElement(list, listPath, ctx)];
  }

  const ids: NodeId[] = [];
  (Array.isArray(list) ? list : []).forEach((item, i) => {
    if (!isObject(item)) {
      ctx.problems.push({ severity: 'warning', path: indexPath(listPath, i), message: 'empty element; it is skipped.' });
      return;
    }

    ids.push(convertElement(item, indexPath(listPath, i, scalarText(getMember(item, 'Id'))), ctx));
  });
  return ids;
}

/** A Form's Fields / a DataGrid's Columns as sub-item nodes (model/subItems.ts). */
function convertSubItems(list: unknown[], listPath: string, kind: SubItemKind, ctx: Context): NodeId[] {
  const ids: NodeId[] = [];
  list.forEach((item, i) => {
    const path = indexPath(listPath, i, isObject(item) ? scalarText(getMember(item, 'Id')) : undefined);
    const node = createNode(kind.type);
    const text = scalarText(item);
    if (text !== undefined && kind.valueMember !== undefined) {
      node.fields[kind.valueMember] = text;
      node.shorthand = kind.valueMember;
    } else if (isObject(item)) {
      for (const [key, value] of Object.entries(item)) {
        const member = canonicalMember(kind.schema, key) ?? key;
        const memberText = scalarText(value);
        if (member === kind.elements && (isObject(value) || Array.isArray(value))) {
          node.children = convertElements(value, fieldPath(path, member), ctx);
          node.singleElement = isObject(value);
        } else if (memberText !== undefined) {
          node.fields[member] = memberText;
        } else {
          node.extra[member] = value;
        }
      }
    } else {
      ctx.problems.push({ severity: 'warning', path, message: `empty ${kind.title.toLowerCase()}; it is skipped.` });
      return;
    }

    ctx.nodes[node.id] = node;
    ids.push(node.id);
  });
  return ids;
}

/**
 * Sets the node's type from Type, a Template member or a shorthand (`{ "Label": "Hi" }` moves the value into the main
 * member); false when the type is not built in (a template instance or custom tag).
 */
function resolveType(node: DesignerNode, members: Map<string, unknown>, path: string, ctx: Context): boolean {
  const has = (m: string) => members.get(m) !== undefined && members.get(m) !== null;
  const typeValue = members.get('Type');
  members.delete('Type');
  if (has('Template') && (typeValue === undefined || typeValue === null)) {
    node.type = 'Template';
    node.shorthand = 'Template';
  } else if (typeValue !== undefined && typeValue !== null) {
    const written = (scalarText(typeValue) ?? JSON.stringify(typeValue)).trim();
    const kind = typeKind(written, ctx.isTemplate);
    node.type = kind === 'builtin' ? canonicalType(written)! : written;
    if (kind === 'unknown') {
      const suggestion = suggest(written, builtInTypes());
      const message = suggestion !== null
        ? `unknown element type '${written}' (did you mean '${suggestion}'?); the element is skipped.`
        : `'${written}' is not a built-in type, a template of this menu or its owner, or a dotted custom tag; unless a template of that name exists, the element is skipped.`;
      ctx.problems.push({ severity: suggestion !== null ? 'error' : 'warning', path: fieldPath(path, 'Type'), nodeId: node.id, field: 'Type', message });
    }

    return kind === 'builtin';
  } else {
    const inferred = inferType(has);
    if (inferred === null) {
      ctx.problems.push({ severity: 'error', path, nodeId: node.id, message: 'the element has no Type (or shorthand such as "Label": "text"); it is skipped.' });
    } else {
      node.type = inferred.type;
      node.shorthand = inferred.shorthand;
      const value = valueShorthands[inferred.shorthand];
      if (value && !has(value.main)) {
        // { "Label": "Hi" } → Type Label, Text "Hi" (the value keeps its JSON form when it is not a scalar)
        members.set(value.main, members.get(inferred.shorthand));
        members.delete(inferred.shorthand);
      }
    }
  }

  return true;
}

/** The element's members as fields (scalars) / extra values, and its child list as child nodes. */
function convertMembers(node: DesignerNode, members: Map<string, unknown>, written: Map<string, string>, builtIn: boolean, path: string, ctx: Context): void {
  // the child list: Children, or a Form's Fields / a DataGrid's Columns as sub-item nodes
  const items = subItemsOf(node.type);
  for (const [member, value] of members) {
    if (member === (items?.member ?? 'Children') && Array.isArray(value)) {
      node.children = items !== undefined ? convertSubItems(value, fieldPath(path, member), items, ctx) : convertChildren(value, path, ctx);
      continue;
    }

    const text = scalarText(value);
    // a template instance's / custom tag's extra fields are its arguments: plain values, edited like fields, under the
    // name as written (the arguments are read case-insensitively)
    const argument = !builtIn && !elementTypes.common.includes(member);
    const known = !builtIn || canonicalMember('ElementDefinition', member) !== null;
    const key = argument ? written.get(member)! : member;
    if (text !== undefined && known) {
      node.fields[key] = text;
    } else {
      node.extra[key] = value;
    }
  }
}

function convertElement(raw: JsonObject, path: string, ctx: Context): NodeId {
  // canonical member spellings (Newtonsoft matches them case-insensitively); unknown members keep theirs
  const members = new Map<string, unknown>();
  const written = new Map<string, string>();
  for (const [key, value] of Object.entries(raw)) {
    const member = canonicalMember('ElementDefinition', key) ?? key;
    members.set(member, value);
    written.set(member, key);
  }

  const node = createNode('');
  ctx.nodes[node.id] = node;
  const builtIn = resolveType(node, members, path, ctx);
  convertMembers(node, members, written, builtIn, path, ctx);
  return node.id;
}
