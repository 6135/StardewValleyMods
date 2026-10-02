import type { DesignerDocument, NodeId, NodeTree, Problem } from '../model/document';
import { isTooltipBlock, tooltipBlockTypes } from '../model/metadata';
import { preorder } from '../model/ops';
import { resolverOf, templateUse, type Definition, type RefKind, type Resolver } from '../model/resolve';
import { nodeTab, tabName, tabOwner, treeOf, type MenuTab, type TabId, type Workspace, type WorkspaceTab } from '../model/workspace';
import { buildMenuObject } from '../io/export';
import { canonicalMember, didYouMean, modelMembers, type JsonObject, type Model } from '../io/dataFormat';
import { checkMenu, type OwnerTemplates, type RawProblem } from './rules';

// validate(doc) (architecture.md §8): the ported DataValidator rules and the schema pass, both over the exported
// MenuDefinition, with paths in the framework's format and the node each message is about. validateTab adds the
// workspace checks (§18.5): references the workspace cannot resolve (with "did you mean"), recursive templates, and
// owner-level definitions nothing uses. A menu template open in its own tab has the messages about its body (its menu
// is validated as a whole). The schema pass (Ajv) is its own chunk: until loadSchemaPass resolves, only the
// ported rules run, and the memoised results are recomputed once it arrives.

let checkSchema: ((menu: JsonObject) => RawProblem[]) | null = null;
let schemaLoad: Promise<void> | null = null;

/** Load the schema pass once; resolves when validateTab includes its problems. */
export function loadSchemaPass(): Promise<void> {
  schemaLoad ??= import('./schema').then(m => { checkSchema = m.checkSchema; });
  return schemaLoad;
}

/** Whether the schema pass has loaded. */
export const schemaPassLoaded = (): boolean => checkSchema !== null;

export function validate(doc: DesignerDocument, ownerTemplates: OwnerTemplates | null = null): Problem[] {
  const { value, nodeAt } = buildMenuObject(doc, { collapseShorthands: false, omitDefaults: false });
  const ported = checkMenu(value, ownerTemplates);
  const seen = new Set(ported.map(p => p.path));
  const schema = (checkSchema?.(value) ?? []).filter(p => !seen.has(p.path) && (seen.add(p.path), true));
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

// ---------------------------------------------------------------------------------------------------------------------
//  Workspace
// ---------------------------------------------------------------------------------------------------------------------

const cache = new WeakMap<WorkspaceTab, { resolver: Resolver; schema: boolean; problems: Problem[] }>();

/** The problems of one tab in its workspace (memoised per tab, workspace revision and schema pass availability). */
export function validateTab(ws: Workspace, tab: WorkspaceTab): Problem[] {
  const resolver = resolverOf(ws);
  const schema = schemaPassLoaded();
  const hit = cache.get(tab);
  if (hit && hit.resolver === resolver && hit.schema === schema) {
    return hit.problems;
  }

  const problems = [...definitionProblems(ws, tab, resolver), ...referenceProblems(tab, resolver), ...cycleProblems(ws, tab, resolver), ...unusedProblems(tab, resolver)];
  cache.set(tab, { resolver, schema, problems });
  return problems;
}

/** The owner templates the rules may expand, or null when the workspace does not hold the owner's entry. */
function ownerTemplates(resolver: Resolver, owner: string): OwnerTemplates | null {
  if (!resolver.knows('template', owner)) {
    return null;
  }

  return {
    names: resolver.names('template', owner),
    params: name => {
      const def = resolver.resolve('template', owner, name);
      const tab = def !== null && def !== 'external' && def.tabId !== null ? resolver.tab(def.tabId) : undefined;
      return tab?.kind === 'template' ? tab.doc.params : undefined;
    }
  };
}

const menuCache = new WeakMap<MenuTab, { resolver: Resolver; schema: boolean; problems: Problem[] }>();

/** The problems of a menu document as a whole, Templates included (memoised like validateTab). */
function menuProblems(tab: MenuTab, resolver: Resolver): Problem[] {
  const schema = schemaPassLoaded();
  const hit = menuCache.get(tab);
  if (hit && hit.resolver === resolver && hit.schema === schema) {
    return hit.problems;
  }

  const problems = validate(tab.doc, ownerTemplates(resolver, tab.doc.owner));
  menuCache.set(tab, { resolver, schema, problems });
  return problems;
}

function definitionProblems(ws: Workspace, tab: WorkspaceTab, resolver: Resolver): Problem[] {
  const owner = tabOwner(tab);
  switch (tab.kind) {
    case 'menu':
      return menuProblems(tab, resolver).filter(p => nodeTab(ws.tabs, tab.id, p.nodeId) === tab.id);
    case 'template': {
      const menu = tab.menu !== undefined ? resolver.tab(tab.menu) : undefined;
      if (menu?.kind === 'menu') {
        return menuProblems(menu, resolver).filter(p => nodeTab(ws.tabs, menu.id, p.nodeId) === tab.id);
      }

      // an owner template checked as the only template of an empty menu (paths read Templates.<name>…)
      const doc: DesignerDocument = {
        owner, menuId: tab.name, menu: {}, menuExtra: {}, root: '', nodes: tab.doc.nodes, templates: { [tab.name]: tab.doc }, previewState: {}
      };
      return validate(doc, ownerTemplates(resolver, owner));
    }
    case 'tooltip': {
      const problems: Problem[] = [...unknownMembers('TooltipDefinition', { ...tab.doc.fields, ...tab.doc.extra }, tab.name)];
      const root = tab.doc.nodes[tab.doc.root]!;
      if (tab.doc.fields['From'] !== undefined) {
        problems.push({ severity: 'warning', path: `${tab.name}.From`, nodeId: root.id, field: 'From', message: "a named tooltip's From is not read: only an element's RichTooltip starts from a named tooltip." });
      }

      if (root.children.length === 0) {
        problems.push({ severity: 'warning', path: `${tab.name}.Blocks`, nodeId: root.id, message: 'the tooltip has no blocks; tooltips starting from it add nothing.' });
      }

      root.children.forEach((id, i) => {
        const block = tab.doc.nodes[id]!;
        const path = `${tab.name}.Blocks[${i}]`;
        if (!isTooltipBlock(block.type)) {
          problems.push({ severity: 'error', path: `${path}.Type`, nodeId: id, field: 'Type', message: `unknown block type '${block.type}'${didYouMean(block.type, tooltipBlockTypes)}; the block is skipped.` });
        }

        problems.push(...unknownMembers('TooltipBlockDefinition', { ...block.fields, ...block.extra }, path, id));
      });
      return problems;
    }
    case 'owner':
      return unknownMembers('OwnerDefinition', { ...tab.doc.fields, ...tab.doc.extra }, tab.owner);
  }
}

function unknownMembers(model: Model, members: Record<string, unknown>, path: string, nodeId?: NodeId): Problem[] {
  return Object.keys(members).filter(k => !k.startsWith('$') && canonicalMember(model, k) === null).map(k => {
    const problem: Problem = { severity: 'warning', path: `${path}.${k}`, field: k, message: `unknown field '${k}'${didYouMean(k, modelMembers(model))}; it is ignored.` };
    return nodeId !== undefined ? { ...problem, nodeId } : problem;
  });
}

const refLabels: Record<RefKind, string> = { template: 'template', tooltip: 'named tooltip', class: 'style class', menu: 'menu', sprite: 'named sprite' };
const refSources: Record<RefKind, string> = {
  template: 'Owners (Templates)', tooltip: 'Owners (Tooltips)', class: 'Owners (Classes)', menu: 'Menus', sprite: 'Sprites'
};

/** Tooltip, class, menu and sprite references the workspace holds the owner of but not the name (templates: the rules). */
function referenceProblems(tab: WorkspaceTab, resolver: Resolver): Problem[] {
  const tree = treeOf(tab);
  return resolver.references.filter(r => r.tabId === tab.id && r.kind !== 'template' && resolver.resolve(r.kind, r.owner, r.name, tab.id) === null).map(r => {
    const node = r.nodeId !== null ? tree?.nodes[r.nodeId] : undefined;
    const problem: Problem = {
      severity: 'warning',
      path: `${node?.fields['Id'] ?? tabName(tab)}.${r.field}`,
      field: r.field,
      message: `'${r.owner}' has no ${refLabels[r.kind]} '${r.name}' in ${refSources[r.kind]}${didYouMean(r.name, resolver.names(r.kind, r.owner, tab.id))}.`
    };
    return r.nodeId !== null ? { ...problem, nodeId: r.nodeId } : problem;
  });
}

/** Template instances inside template bodies, as edges between template definitions (memoised per resolver). */
const edgeCache = new WeakMap<Resolver, { from: Definition; to: Definition; tabId: TabId; nodeId: NodeId }[]>();

function templateEdges(resolver: Resolver) {
  let edges = edgeCache.get(resolver);
  if (edges) {
    return edges;
  }

  edges = [];
  for (const def of resolver.definitions) {
    const tab = def.kind === 'template' && def.tabId !== null ? resolver.tab(def.tabId) : undefined;
    const tree: NodeTree | null = tab ? treeOf(tab) : null;
    if (!tab || !tree) {
      continue;
    }

    const body = def.nodeId !== undefined ? preorder(tree, def.nodeId) : Object.keys(tree.nodes);
    for (const id of body) {
      const use = templateUse(tree.nodes[id]!);
      const to = use ? resolver.resolve('template', def.owner, use.name, tab.id) : null;
      if (to !== null && to !== 'external') {
        edges.push({ from: def, to, tabId: tab.id, nodeId: id });
      }
    }
  }

  edgeCache.set(resolver, edges);
  return edges;
}

/** Instances that make a template expand itself (directly or through others). */
function cycleProblems(ws: Workspace, tab: WorkspaceTab, resolver: Resolver): Problem[] {
  const edges = templateEdges(resolver);
  /** A path of definitions from `start` to `goal`, or null. */
  const pathTo = (start: Definition, goal: Definition): Definition[] | null => {
    const seen = new Set<Definition>();
    const walk = (d: Definition): Definition[] | null => {
      if (d === goal) {
        return [d];
      }

      if (seen.has(d)) {
        return null;
      }

      seen.add(d);
      for (const e of edges.filter(x => x.from === d)) {
        const rest = walk(e.to);
        if (rest) {
          return [d, ...rest];
        }
      }

      return null;
    };
    return walk(start);
  };

  return edges.filter(e => nodeTab(ws.tabs, e.tabId, e.nodeId) === tab.id).flatMap(e => {
    const back = pathTo(e.to, e.from);
    return back ? [{
      severity: 'warning' as const,
      path: `Templates.${e.from.name}`,
      nodeId: e.nodeId,
      message: `recursive template: ${[e.from, ...back].map(d => d.name).join(' → ')}; the framework stops expanding at depth 8.`
    }] : [];
  });
}

/** Owner templates, named tooltips and classes nothing in the workspace uses. */
function unusedProblems(tab: WorkspaceTab, resolver: Resolver): Problem[] {
  return resolver.definitions
    .filter(d => d.tabId === tab.id && d.nodeId === undefined && (d.kind === 'template' || d.kind === 'tooltip' || d.kind === 'class') && resolver.usages(d).length === 0)
    .map(d => {
      const problem: Problem = { severity: 'info', path: d.kind === 'class' ? `Classes.${d.name}` : d.name, message: `the ${refLabels[d.kind]} '${d.name}' is not used in this workspace.` };
      const root = treeOf(tab)?.root;
      return root !== undefined ? { ...problem, nodeId: root } : problem;
    });
}
