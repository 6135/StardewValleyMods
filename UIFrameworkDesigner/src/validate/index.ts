import type { DesignerDocument, NodeId, Problem } from '../model/document';
import { buildMenuObject } from '../io/export';
import { checkMenu, type RawProblem } from './rules';
import { checkSchema } from './schema';

// validate(doc) (architecture.md §8): the ported DataValidator rules and the schema pass, both over the exported
// MenuDefinition, with paths in the framework's format and the node each message is about.

export function validate(doc: DesignerDocument): Problem[] {
  const { value, nodeAt } = buildMenuObject(doc, { collapseShorthands: false, omitDefaults: false });
  const ported = checkMenu(value);
  const seen = new Set(ported.map(p => p.path));
  const schema = checkSchema(value).filter(p => !seen.has(p.path) && (seen.add(p.path), true));
  return [...ported, ...schema].map(p => toProblem(p, nodeAt));
}

function toProblem(raw: RawProblem, nodeAt: Map<string, NodeId>): Problem {
  const { pointer, ...problem } = raw;
  const nodeId = nearestNode(pointer, nodeAt);
  return nodeId !== undefined ? { ...problem, nodeId } : problem;
}

/** The node at the longest pointer prefix (a RowTemplate element or a column maps to its owning node). */
function nearestNode(pointer: string, nodeAt: Map<string, NodeId>): NodeId | undefined {
  let ptr = pointer;
  for (;;) {
    const id = nodeAt.get(ptr);
    if (id !== undefined || ptr === '') {
      return id;
    }

    ptr = ptr.slice(0, ptr.lastIndexOf('/'));
  }
}
