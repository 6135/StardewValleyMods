import type { Draft } from 'immer';
import type { DesignerDocument, DesignerNode, NodeId, TemplateDoc } from './document';
import { canonicalType, isCustomTag, isTooltipBlock } from './metadata';
import { subItemKind } from './subItems';
import { sameName, tabOwner, treeOf, type TabId, type Workspace, type WorkspaceTab } from './workspace';

// The resolver (architecture.md §18.2): what a reference in one tab means in the workspace. Pure; one instance per
// workspace revision (memoised on the tab list), so layout, validation and the UI share its indexes.

/** The kinds of cross-references (§18.1). Composites are not resolved yet. */
export type RefKind = 'template' | 'tooltip' | 'class' | 'menu' | 'sprite';

/** Something a reference can name. */
export interface Definition {
  kind: RefKind;
  owner: string;
  name: string;
  /** The tab that defines it; null for a sprite (kept with the changes the workspace does not edit). */
  tabId: TabId | null;
  /** A menu-level template: its root node in the menu tab. */
  nodeId?: NodeId;
}

/** One use of a definition. */
export interface Reference {
  kind: RefKind;
  owner: string;
  name: string;
  tabId: TabId;
  /** The node that holds it; null for a menu-level member or an owner entry member. */
  nodeId: NodeId | null;
  /** The member it is written in ("Type", "Template", "Class", "RichTooltip", "OnClick", "Hotkeys" …). */
  field: string;
}

/** A definition, 'external' when its owner's definitions are not in the workspace, or null when it is missing. */
export type Resolution = Definition | 'external' | null;

export interface Resolver {
  readonly definitions: readonly Definition[];
  readonly references: readonly Reference[];
  /** Menu templates of `fromTab` first (templates), then the owner's. */
  resolve(kind: RefKind, owner: string, name: string, fromTab?: TabId): Resolution;
  /** The references that resolve to `def`. */
  usages(def: Definition): Reference[];
  /** True when the workspace holds the definitions of `kind` for `owner` (its Owners entry / named sprites). */
  knows(kind: RefKind, owner: string): boolean;
  /** The names a reference of `kind` from `fromTab` can use (pickers, "did you mean"). */
  names(kind: RefKind, owner: string, fromTab?: TabId): string[];
  tab(id: TabId): WorkspaceTab | undefined;
}

/** `6135.UIFramework_OpenMenu owner/menu` (also _OpenMenuAsChild, _ToggleMenu) in an action; group 2 is the key. */
const menuAction = /(\b6135\.UIFramework_(?:OpenMenuAsChild|OpenMenu|ToggleMenu)\s+)([^\s"']+)/gi;
const spriteRef = /\bsprite:([^\s,;"'@]+)/gi;
const SpritesAsset = 'mods/6135.uiframework/sprites';

/** Split `owner/name`; null without a slash. */
export function splitKey(key: string): { owner: string; name: string } | null {
  const slash = key.indexOf('/');
  return slash > 0 ? { owner: key.slice(0, slash).trim(), name: key.slice(slash + 1).trim() } : null;
}

/** The names in a Class value ("header big", "a, b"). */
export function classNames(value: string): string[] {
  return value.split(/[ ,]+/).filter(n => n.length > 0);
}

/** A menu-level template of `doc` by name (any case). */
export function localTemplate(doc: DesignerDocument, name: string): TemplateDoc | undefined {
  const found = Object.keys(doc.templates).find(k => sameName(k, name));
  return found === undefined ? undefined : doc.templates[found];
}

/** The template a node instantiates (its Type, or the Template member of a "Template" element), or null. */
export function templateUse(node: DesignerNode): { name: string; field: 'Type' | 'Template' } | null {
  const canonical = canonicalType(node.type);
  if (canonical === 'Template') {
    const name = node.fields['Template']?.trim();
    return name ? { name, field: 'Template' } : null;
  }

  return canonical === null && node.type.length > 0 && !isCustomTag(node.type) && !subItemKind(node.type) && !isTooltipBlock(node.type)
    && node.type !== 'Menu' && node.type !== 'Tooltip' ? { name: node.type.trim(), field: 'Type' } : null;
}

/** Every string inside a JSON value. */
function* strings(value: unknown): Generator<string> {
  if (typeof value === 'string') {
    yield value;
  } else if (Array.isArray(value)) {
    for (const v of value) {
      yield* strings(v);
    }
  } else if (value !== null && typeof value === 'object') {
    for (const v of Object.values(value)) {
      yield* strings(v);
    }
  }
}

function isRecord(v: unknown): v is Record<string, unknown> {
  return typeof v === 'object' && v !== null && !Array.isArray(v);
}

// ---------------------------------------------------------------------------------------------------------------------
//  Collecting definitions and references
// ---------------------------------------------------------------------------------------------------------------------

function collectDefinitions(ws: Workspace): Definition[] {
  const defs: Definition[] = [];
  for (const tab of ws.tabs) {
    switch (tab.kind) {
      case 'menu':
        defs.push({ kind: 'menu', owner: tab.doc.owner, name: tab.doc.menuId, tabId: tab.id });
        for (const [name, t] of Object.entries(tab.doc.templates)) {
          defs.push({ kind: 'template', owner: tab.doc.owner, name, tabId: tab.id, nodeId: t.root });
        }
        break;
      case 'template':
      case 'tooltip':
        defs.push({ kind: tab.kind, owner: tab.owner, name: tab.name, tabId: tab.id });
        break;
      case 'owner': {
        const classes = tab.doc.extra['Classes'];
        for (const name of isRecord(classes) ? Object.keys(classes) : []) {
          defs.push({ kind: 'class', owner: tab.owner, name, tabId: tab.id });
        }
        break;
      }
    }
  }

  for (const change of ws.otherChanges) {
    const entries = isRecord(change) && typeof change['Target'] === 'string' && change['Target'].trim().toLowerCase() === SpritesAsset ? change['Entries'] : undefined;
    for (const key of isRecord(entries) ? Object.keys(entries) : []) {
      const k = splitKey(key);
      if (k) {
        defs.push({ kind: 'sprite', owner: k.owner, name: k.name, tabId: null });
      }
    }
  }

  return defs;
}

/** TooltipDefinition members of an element. */
const tooltipMembers = ['RichTooltip', 'RowTooltip'];
/** Members holding element definitions (ElementDefinition.Children / RowTemplate, ColumnDefinition.Cell). */
const elementLists = new Set(['children', 'rowtemplate', 'cell']);

/** The tooltip a TooltipDefinition value names in From, or ''. */
function tooltipFrom(value: unknown): string {
  return isRecord(value) && typeof value['From'] === 'string' ? value['From'].trim() : '';
}

/**
 * The template, class and tooltip references of the element definitions inside a raw JSON member (a RowTemplate,
 * column cells, a Composite's Children …) and the tooltips its TooltipDefinition members name.
 */
function rawReferences(value: unknown, member: string, push: (kind: RefKind, name: string) => void): void {
  if (Array.isArray(value)) {
    value.forEach(v => rawReferences(v, member, push));
    return;
  }

  if (!isRecord(value)) {
    return;
  }

  const from = tooltipMembers.includes(member) ? tooltipFrom(value) : '';
  if (from) {
    push('tooltip', from);
  }

  if (elementLists.has(member.toLowerCase())) {
    const use = rawTemplateUse(value);
    if (use) {
      push('template', use.name);
    }

    for (const name of classNames(typeof value['Class'] === 'string' ? value['Class'] : '')) {
      push('class', name);
    }
  }

  for (const [k, v] of Object.entries(value)) {
    rawReferences(v, k, push);
  }
}

/** The template a raw element definition instantiates (templateUse). */
function rawTemplateUse(element: Record<string, unknown>): ReturnType<typeof templateUse> {
  const type = typeof element['Type'] === 'string' ? element['Type'] : '';
  const template = typeof element['Template'] === 'string' ? element['Template'] : undefined;
  return templateUse({ id: '', type, fields: template !== undefined ? { Template: template } : {}, extra: {}, children: [] });
}

/** A raw JSON member with the references rawReferences finds to `kind` `from` renamed to `to`. */
function renameRaw(value: unknown, member: string, kind: RefKind, from: string, to: string): unknown {
  if (Array.isArray(value)) {
    return value.map(v => renameRaw(v, member, kind, from, to));
  }

  if (!isRecord(value)) {
    return value;
  }

  const out = Object.fromEntries(Object.entries(value).map(([k, v]) => [k, renameRaw(v, k, kind, from, to)]));
  if (kind === 'tooltip' && tooltipMembers.includes(member) && sameName(tooltipFrom(out), from)) {
    out['From'] = to;
  }

  if (elementLists.has(member.toLowerCase())) {
    const use = kind === 'template' ? rawTemplateUse(out) : null;
    if (use && sameName(use.name, from)) {
      out[use.field] = to;
    }

    if (kind === 'class' && typeof out['Class'] === 'string') {
      out['Class'] = classNames(out['Class']).map(n => (sameName(n, from) ? to : n)).join(' ');
    }
  }

  return out;
}

/** The menu and sprite references in a text. */
function textReferences(text: string, push: (kind: RefKind, owner: string, name: string) => void): void {
  for (const m of text.matchAll(menuAction)) {
    const k = splitKey(m[2]!);
    if (k) {
      push('menu', k.owner, k.name);
    }
  }

  for (const m of text.matchAll(spriteRef)) {
    const k = splitKey(m[1]!);
    if (k) {
      push('sprite', k.owner, k.name);
    }
  }
}

function collectReferences(ws: Workspace): Reference[] {
  const refs: Reference[] = [];
  for (const tab of ws.tabs) {
    const owner = tabOwner(tab);
    const at = (nodeId: NodeId | null, field: string) => (kind: RefKind, refOwner: string, name: string) =>
      refs.push({ kind, owner: refOwner, name, tabId: tab.id, nodeId, field });
    const scan = (nodeId: NodeId | null, field: string, value: unknown) => {
      for (const text of strings(value)) {
        textReferences(text, at(nodeId, field));
      }
    };

    const tree = treeOf(tab);
    for (const node of tree ? Object.values(tree.nodes) : []) {
      const use = node.id !== tree!.root ? templateUse(node) : null;
      if (use) {
        refs.push({ kind: 'template', owner, name: use.name, tabId: tab.id, nodeId: node.id, field: use.field });
      }

      for (const name of classNames(node.fields['Class'] ?? '')) {
        refs.push({ kind: 'class', owner, name, tabId: tab.id, nodeId: node.id, field: 'Class' });
      }

      for (const [field, value] of [...Object.entries(node.fields), ...Object.entries(node.extra)]) {
        scan(node.id, field, value);
        rawReferences(value, field, (kind, name) => refs.push({ kind, owner, name, tabId: tab.id, nodeId: node.id, field }));
      }
    }

    const members = tab.kind === 'menu' ? [...Object.entries(tab.doc.menu), ...Object.entries(tab.doc.menuExtra)]
      : tab.kind === 'owner' || tab.kind === 'tooltip' ? [...Object.entries(tab.doc.fields), ...Object.entries(tab.doc.extra)] : [];
    // a named tooltip's own From
    const from = tab.kind === 'tooltip' ? tab.doc.fields['From']?.trim() : '';
    if (from) {
      refs.push({ kind: 'tooltip', owner, name: from, tabId: tab.id, nodeId: null, field: 'From' });
    }

    for (const [field, value] of members) {
      scan(null, field, value);
      rawReferences(value, field, (kind, name) => refs.push({ kind, owner, name, tabId: tab.id, nodeId: null, field }));
    }
  }

  return refs;
}

// ---------------------------------------------------------------------------------------------------------------------
//  Resolver
// ---------------------------------------------------------------------------------------------------------------------

const cache = new WeakMap<readonly WorkspaceTab[], { otherChanges: unknown[]; resolver: Resolver }>();

/** The resolver of a workspace revision (memoised). */
export function resolverOf(ws: Workspace): Resolver {
  const hit = cache.get(ws.tabs);
  if (hit && hit.otherChanges === ws.otherChanges) {
    return hit.resolver;
  }

  const resolver = createResolver(ws);
  cache.set(ws.tabs, { otherChanges: ws.otherChanges, resolver });
  return resolver;
}

function createResolver(ws: Workspace): Resolver {
  const definitions = collectDefinitions(ws);
  const references = collectReferences(ws);
  const tabs = new Map(ws.tabs.map(t => [t.id, t]));
  // owners whose Owners entry (owner, template or tooltip tab) / named sprites the workspace holds: references to
  // other owners cannot be checked here
  const entryOwners = ws.tabs.filter(t => t.kind !== 'menu').map(tabOwner);
  const spriteOwners = definitions.filter(d => d.kind === 'sprite').map(d => d.owner);
  const known = (kind: RefKind, owner: string) => (kind === 'sprite' ? spriteOwners : entryOwners).some(o => sameName(o, owner));

  const candidates = (kind: RefKind, owner: string, fromTab?: TabId): Definition[] => {
    const own = definitions.filter(d => d.kind === kind && sameName(d.owner, owner) && d.nodeId === undefined);
    return kind === 'template' ? [...definitions.filter(d => d.kind === kind && d.nodeId !== undefined && d.tabId === fromTab), ...own] : own;
  };

  const resolve = (kind: RefKind, owner: string, name: string, fromTab?: TabId): Resolution =>
    candidates(kind, owner, fromTab).find(d => sameName(d.name, name)) ?? (known(kind, owner) ? null : 'external');

  const used = new Map<Definition, Reference[]>();
  for (const ref of references) {
    const def = resolve(ref.kind, ref.owner, ref.name, ref.tabId);
    if (def !== null && def !== 'external') {
      used.set(def, [...(used.get(def) ?? []), ref]);
    }
  }

  return {
    definitions,
    references,
    resolve,
    knows: known,
    usages: def => used.get(def) ?? [],
    names: (kind, owner, fromTab) => [...new Set(candidates(kind, owner, fromTab).map(d => d.name))],
    tab: id => tabs.get(id)
  };
}

// ---------------------------------------------------------------------------------------------------------------------
//  The document a tab previews
// ---------------------------------------------------------------------------------------------------------------------

const previewCache = new WeakMap<object, { resolver: Resolver; doc: DesignerDocument | null }>();

/**
 * The tab as one self-contained menu document for the preview, the way DataBuilder sees it: the menu (or a template's
 * body) with the owner templates it can instantiate added after its own (menu templates win). Null for a tooltip
 * (previewed by itself, preview/Tooltip.tsx) and an owner entry.
 */
export function previewDocument(ws: Workspace, tab: WorkspaceTab): DesignerDocument | null {
  const resolver = resolverOf(ws);
  const hit = previewCache.get(tab.doc);
  if (hit && hit.resolver === resolver) {
    return hit.doc;
  }

  const doc = buildPreviewDocument(ws, tab);
  previewCache.set(tab.doc, { resolver, doc });
  return doc;
}

function buildPreviewDocument(ws: Workspace, tab: WorkspaceTab): DesignerDocument | null {
  const owner = tabOwner(tab);
  const withOwnerTemplates = (doc: DesignerDocument): DesignerDocument => {
    const extra = ws.tabs.filter((t): t is Extract<WorkspaceTab, { kind: 'template' }> =>
      t.kind === 'template' && t.id !== tab.id && sameName(t.owner, owner) && localTemplate(doc, t.name) === undefined);
    if (extra.length === 0) {
      return doc;
    }

    const templates = { ...doc.templates };
    let nodes = doc.nodes;
    for (const t of extra) {
      templates[t.name] = t.doc;
      nodes = { ...nodes, ...t.doc.nodes };
    }

    return { ...doc, templates, nodes };
  };

  switch (tab.kind) {
    case 'menu':
      return withOwnerTemplates(tab.doc);
    case 'template': {
      const { fields, root, nodes, previewState } = tab.doc;
      const menu: Record<string, string> = { Title: tab.name };
      for (const f of ['Horizontal', 'Spacing', 'Alignment']) {
        if (fields[f] !== undefined) {
          menu[f] = fields[f];
        }
      }

      return withOwnerTemplates({ owner, menuId: tab.name, menu, menuExtra: {}, root, nodes, templates: {}, previewState });
    }
    case 'tooltip':
    case 'owner':
      return null;
  }
}

// ---------------------------------------------------------------------------------------------------------------------
//  Rename
// ---------------------------------------------------------------------------------------------------------------------

export type TabEdit = (tab: Draft<WorkspaceTab>) => void;

/** Rename a key of an object keeping the member order. */
function renameKey<T>(obj: Record<string, T>, from: string, to: string): Record<string, T> {
  return Object.fromEntries(Object.entries(obj).map(([k, v]) => [k === from ? to : k, v]));
}

/** Replace in every string of a JSON value. */
function mapStrings(value: unknown, f: (s: string) => string): unknown {
  if (typeof value === 'string') {
    return f(value);
  }

  if (Array.isArray(value)) {
    return value.map(v => mapStrings(v, f));
  }

  return isRecord(value) ? Object.fromEntries(Object.entries(value).map(([k, v]) => [k, mapStrings(v, f)])) : value;
}

/** Why `name` cannot be the new name of `def`, or null. */
export function renameRefusal(resolver: Resolver, def: Definition, name: string): string | null {
  const trimmed = name.trim();
  if (trimmed.length === 0) {
    return 'The name must not be empty.';
  }

  if (/[\s/]/.test(trimmed) || (def.kind === 'template' && (trimmed.includes('.') || canonicalType(trimmed) !== null))) {
    return def.kind === 'template' ? 'A template name has no spaces, slashes or dots and is not a built-in type.' : 'The name has no spaces or slashes.';
  }

  const taken = resolver.definitions.some(d => d !== def && d.kind === def.kind && sameName(d.owner, def.owner) && sameName(d.name, trimmed)
    && (def.nodeId === undefined || d.tabId === def.tabId));
  return taken ? `'${trimmed}' is already defined.` : null;
}

/**
 * The edits that rename `def` to `name` in every tab (the definition and each reference that resolves to it); the
 * store applies them as one undoable workspace step.
 */
export function renameEdits(resolver: Resolver, def: Definition, name: string): Map<TabId, TabEdit[]> {
  const to = name.trim();
  const edits = new Map<TabId, TabEdit[]>();
  const add = (tabId: TabId, edit: TabEdit) => edits.set(tabId, [...(edits.get(tabId) ?? []), edit]);

  if (def.tabId !== null) {
    add(def.tabId, tab => {
      if (def.kind === 'template' && def.nodeId !== undefined && tab.kind === 'menu') {
        tab.doc.templates = renameKey(tab.doc.templates, def.name, to) as typeof tab.doc.templates;
      } else if ((tab.kind === 'template' || tab.kind === 'tooltip') && tab.kind === def.kind) {
        tab.name = to;
      } else if (def.kind === 'menu' && tab.kind === 'menu') {
        tab.doc.menuId = to;
      } else if (def.kind === 'class' && tab.kind === 'owner' && isRecord(tab.doc.extra['Classes'])) {
        tab.doc.extra['Classes'] = renameKey(tab.doc.extra['Classes'], def.name, to);
      }
    });
  }

  const renameMenu = (text: string) => text.replace(menuAction, (all, prefix: string, key: string) => {
    const k = splitKey(key);
    return k && sameName(k.owner, def.owner) && sameName(k.name, def.name) ? `${prefix}${k.owner}/${to}` : all;
  });

  for (const ref of resolver.usages(def)) {
    add(ref.tabId, tab => {
      if (def.kind === 'menu') {
        if (ref.nodeId !== null && tab.kind !== 'owner') {
          const node = tab.doc.nodes[ref.nodeId];
          if (node && ref.field in node.fields) {
            node.fields[ref.field] = renameMenu(node.fields[ref.field]!);
          } else if (node) {
            node.extra[ref.field] = mapStrings(node.extra[ref.field], renameMenu);
          }
        } else if (tab.kind === 'menu') {
          if (ref.field in tab.doc.menu) {
            tab.doc.menu[ref.field] = renameMenu(tab.doc.menu[ref.field]!);
          } else {
            tab.doc.menuExtra[ref.field] = mapStrings(tab.doc.menuExtra[ref.field], renameMenu);
          }
        } else if (tab.kind === 'owner') {
          if (ref.field in tab.doc.fields) {
            tab.doc.fields[ref.field] = renameMenu(tab.doc.fields[ref.field]!);
          } else {
            tab.doc.extra[ref.field] = mapStrings(tab.doc.extra[ref.field], renameMenu);
          }
        }

        return;
      }

      const node = ref.nodeId !== null && tab.kind !== 'owner' ? tab.doc.nodes[ref.nodeId] : undefined;
      if (!node) {
        // a member of the menu, owner entry or named tooltip
        if (tab.kind === 'tooltip' && ref.field === 'From') {
          tab.doc.fields['From'] = to;
        } else if (tab.kind === 'menu' && ref.field in tab.doc.menuExtra) {
          tab.doc.menuExtra[ref.field] = renameRaw(tab.doc.menuExtra[ref.field], ref.field, def.kind, def.name, to);
        } else if ((tab.kind === 'owner' || tab.kind === 'tooltip') && ref.field in tab.doc.extra) {
          tab.doc.extra[ref.field] = renameRaw(tab.doc.extra[ref.field], ref.field, def.kind, def.name, to);
        }

        return;
      }

      if (ref.field in node.extra) {
        node.extra[ref.field] = renameRaw(node.extra[ref.field], ref.field, def.kind, def.name, to);
      } else if (def.kind === 'template') {
        if (ref.field === 'Type') {
          node.type = to;
        } else {
          node.fields['Template'] = to;
        }
      } else if (def.kind === 'class') {
        node.fields['Class'] = classNames(node.fields['Class'] ?? '').map(n => (sameName(n, def.name) ? to : n)).join(' ');
      }
    });
  }

  return edits;
}
