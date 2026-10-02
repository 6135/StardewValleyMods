import { useState, type ReactNode } from 'react';
import type { DesignerDocument, DesignerNode } from '../model/document';
import { defaultOf, elementTypes, isTooltipBlock, typeInfo, usesMember } from '../model/metadata';
import { templateUse, type Definition } from '../model/resolve';
import { activeTab, activeUi, useDesigner } from '../model/store';
import { subItemKind, subItemsOf, type SubItemKind } from '../model/subItems';
import { tabName, tabOwner, treeOf, type OwnerTab, type TemplateTab, type TooltipTab, type WorkspaceTab } from '../model/workspace';
import { allowsString, describe, schemaProperties, shapeOf } from '../fieldShapes';
import { FieldRow, JsonRow } from './fields/FieldRow';
import { refListId, TextWidget } from './fields/widgets';
import { definitionOf, RenameRow, revealDefinition, showsDefinition, UsedBy, useNodeTargets, useResolver } from './WorkspacePane';

// The inspector (architecture.md §6.2, §18.3): menu fields for a menu's root, the type's fields for an element, the
// schema definition's fields for a sub-item (a Form field, a DataGrid column) or a tooltip block; the members of a
// template, named tooltip or owner entry for those tabs, with the definition's name (rename) and its uses.

interface Group {
  name: string;
  match?: (field: string) => boolean;
  open?: boolean;
}

const layoutFields = new Set(['Margin', 'MarginLeft', 'MarginTop', 'MarginRight', 'MarginBottom', 'Width', 'Height', 'MinWidth', 'MaxWidth', 'HorizontalAlign', 'VerticalAlign', 'X', 'Y']);
const gridFields = new Set(['Row', 'Column', 'RowSpan', 'ColumnSpan', 'Cell', 'Span']);
const behaviourFields = new Set(['Visible', 'Enabled', 'Condition', 'If', 'Case', 'With', 'Tooltip', 'TooltipTitle', 'Tag', 'Sealed', 'AccessibleName', 'Class', 'Style']);
const isEvent = (f: string) => /^On[A-Z]/.test(f);

/** Element groups; `Main` takes the type-specific members, `Advanced` (last) the rest. */
function elementGroups(members: Set<string>): Group[] {
  return [
    { name: 'Main', match: f => members.has(f) && !isEvent(f), open: true },
    { name: 'Layout', match: f => layoutFields.has(f), open: true },
    { name: 'Grid cell', match: f => gridFields.has(f) },
    { name: 'Behaviour', match: f => behaviourFields.has(f) },
    { name: 'Events', match: isEvent },
    { name: 'Advanced' }
  ];
}

const menuGroups: Group[] = [
  { name: 'Window', open: true, match: f => ['Title', 'Width', 'Height', 'Anchor', 'X', 'Y', 'DrawBox', 'Padding', 'ShowCloseButton', 'Modal', 'DimBackground', 'CloseOnEscape', 'PlayerLayout', 'Resizable'].includes(f) },
  { name: 'Root stack', open: true, match: f => ['Horizontal', 'Spacing', 'Alignment'].includes(f) },
  { name: 'Behaviour', match: f => ['Condition', 'Hotkey', 'DefaultButton', 'CancelButton', 'StateLifetime', 'UpdateIntervalMs', 'From'].includes(f) },
  { name: 'Events', match: isEvent },
  { name: 'Advanced' }
];

export function Inspector() {
  const tab = useDesigner(activeTab);
  const selection = useDesigner(s => activeUi(s).selection);
  const tree = treeOf(tab);
  const node = tree && selection !== null && selection !== tree.root ? tree.nodes[selection] : undefined;

  return (
    <aside className="pane inspector" aria-label="Inspector">
      <div className="pane-title">Inspector</div>
      <div className="pane-body">
        {node ? (
          subItemKind(node.type) ? <SubItemInspector key={node.id} node={node} kind={subItemKind(node.type)!} />
            : tab.kind === 'tooltip' ? <BlockInspector key={node.id} node={node} />
            : <ElementInspector key={node.id} node={node} owner={tabOwner(tab)} />
        ) : <DefinitionInspector key={tab.id} tab={tab} />}
      </div>
    </aside>
  );
}

function DefinitionInspector({ tab }: { tab: WorkspaceTab }) {
  switch (tab.kind) {
    case 'menu': return <MenuInspector doc={tab.doc} tabId={tab.id} />;
    case 'template': return <TemplateInspector tab={tab} />;
    case 'tooltip': return <TooltipInspector tab={tab} />;
    case 'owner': return <OwnerInspector tab={tab} />;
  }
}

/** The workspace definition a tab is (the subject of its rename and Used by). */
function useTabDefinition(kind: Definition['kind'], tabId: string): Definition | undefined {
  const resolver = useResolver();
  return resolver.definitions.find(d => d.kind === kind && d.tabId === tabId && d.nodeId === undefined);
}

/** The Owners entry a tab belongs to. */
function OwnerRow({ owner, id, title }: { owner: string; id: string; title: string }) {
  const setMeta = useDesigner(s => s.setMeta);
  return (
    <div className="field set">
      <label htmlFor={id} title={title}>Owner</label>
      <div className="control"><TextWidget id={id} mono value={owner} commit={v => setMeta({ owner: v ?? '' })} /></div>
    </div>
  );
}

/** A model's string members as widgets, other members as JSON, unknown members after (template, tooltip, owner entry). */
function useModelEntries(model: string, fields: Record<string, string>, extra: Record<string, unknown>, skip: Set<string>, what: string): Entry[] {
  const setTabField = useDesigner(s => s.setTabField);
  const setTabExtra = useDesigner(s => s.setTabExtra);
  const props = schemaProperties(model);
  const names = [...Object.keys(props), ...Object.keys(fields), ...Object.keys(extra)].filter((n, i, all) => !skip.has(n) && all.indexOf(n) === i);
  return names.map(name => {
    const description = describe(model, name);
    const warning = name in props ? undefined : `Not a ${what} field`;
    if (name in extra || (fields[name] === undefined && !allowsString(props[name]))) {
      return { name, set: name in extra, row: <JsonRow name={name} value={extra[name]} description={description} warning={warning} commit={v => setTabExtra(name, v)} /> };
    }

    return {
      name,
      set: fields[name] !== undefined,
      row: <FieldRow name={name} shape={shapeOf('menu', name)} value={fields[name]} description={description} warning={warning} commit={v => setTabField(name, v)} />
    };
  });
}

function TemplateInspector({ tab }: { tab: TemplateTab }) {
  const resolver = useResolver();
  const def = resolver.definitions.find(d => d.kind === 'template' && showsDefinition(tab, d));
  const menu = tab.menu !== undefined ? resolver.tab(tab.menu) : undefined;
  const setTabExtra = useDesigner(s => s.setTabExtra);
  const entries = useModelEntries('TemplateDefinition', tab.doc.fields, tab.doc.extra, new Set(['Params', 'Children']), 'template');
  return (
    <>
      <div className="inspector-head">
        <span className="type">{menu ? 'Menu template' : 'Owner template'}</span>
        <span className="muted">instances: "Type": "{tab.name}"</span>
      </div>
      <details className="group" open>
        <summary>Definition</summary>
        {def && <RenameRow def={def} label="Name" id="template-name" />}
        {menu ? <p className="note">In the Templates of menu {tabName(menu)}: only that menu can use it, before an owner template of the same name.</p>
          : <OwnerRow owner={tab.owner} id="template-owner" title="The Owners entry the template belongs to; every UI of that owner can use it." />}
        <JsonRow name="Params" value={tab.doc.params} description={describe('TemplateDefinition', 'Params')} commit={v => setTabExtra('Params', v)} />
      </details>
      <GroupedFields scope="template" groups={[{ name: 'Body stack', open: true }]} entries={entries} />
      {def && <UsedBy def={def} />}
    </>
  );
}

function TooltipInspector({ tab }: { tab: TooltipTab }) {
  const def = useTabDefinition('tooltip', tab.id);
  const entries = useModelEntries('TooltipDefinition', tab.doc.fields, tab.doc.extra, new Set(['Blocks']), 'tooltip');
  return (
    <>
      <div className="inspector-head">
        <span className="type">Named tooltip</span>
        <span className="muted">elements show it with RichTooltip From "{tab.name}"</span>
      </div>
      <details className="group" open>
        <summary>Definition</summary>
        {def && <RenameRow def={def} label="Name" id="tooltip-name" />}
        <OwnerRow owner={tab.owner} id="tooltip-owner" title="The Owners entry the tooltip belongs to." />
      </details>
      <GroupedFields scope="tooltip" groups={[{ name: 'Tooltip', open: true }]} entries={entries} />
      {def && <UsedBy def={def} />}
    </>
  );
}

/** A tooltip block: the TooltipBlockDefinition members (its Type is the node type). */
function BlockInspector({ node }: { node: DesignerNode }) {
  const setField = useDesigner(s => s.setField);
  const setExtra = useDesigner(s => s.setExtra);
  const props = schemaProperties('TooltipBlockDefinition');
  const names = [...Object.keys(props), ...Object.keys(node.fields), ...Object.keys(node.extra)].filter((n, i, all) => n !== 'Type' && all.indexOf(n) === i);
  const entries: Entry[] = names.map(name => {
    const description = describe('TooltipBlockDefinition', name);
    const warning = name in props ? undefined : 'Not a block field';
    return name in node.extra
      ? { name, set: true, row: <JsonRow name={name} value={node.extra[name]} description={description} warning={warning} commit={v => setExtra(node.id, name, v)} /> }
      : {
        name,
        set: node.fields[name] !== undefined,
        row: <FieldRow name={name} shape={shapeOf(node.type, name)} value={node.fields[name]} description={description} warning={warning} commit={v => setField(node.id, name, v)} />
      };
  });

  return (
    <>
      <div className="inspector-head">
        <span className="type">{node.type}</span>
        <span className="muted">{isTooltipBlock(node.type) ? 'a tooltip block' : 'unknown block type'}</span>
      </div>
      <GroupedFields scope={node.id} groups={[{ name: 'Block', open: true }]} entries={entries} />
    </>
  );
}

/** An Owners entry: TooltipDelayMs, DefaultStyle, Classes, Hotkeys, SharedState (its Templates and Tooltips are tabs). */
function OwnerInspector({ tab }: { tab: OwnerTab }) {
  const resolver = useResolver();
  const classes = resolver.definitions.filter(d => d.kind === 'class' && d.tabId === tab.id);
  const entries = useModelEntries('OwnerDefinition', tab.doc.fields, tab.doc.extra, new Set(['Templates', 'Tooltips']), 'owner');
  return (
    <>
      <div className="inspector-head">
        <span className="type">Owner entry</span>
        <span className="muted">its templates and tooltips are tabs of their own</span>
      </div>
      <details className="group" open>
        <summary>Entry</summary>
        <OwnerRow owner={tab.owner} id="owner-id" title="A loaded mod or content pack id, usually {{ModId}}." />
      </details>
      <GroupedFields scope="owner" groups={[{ name: 'Members', open: true }]} entries={entries} />
      {classes.map((def, i) => (
        <details key={def.name} className="group">
          <summary>Class {def.name} ({resolver.usages(def).length})</summary>
          <RenameRow def={def} label="Name" id={`class-${i}`} />
          <UsedBy def={def} />
        </details>
      ))}
    </>
  );
}

/** A field of the inspector: a string widget or a JSON editor (object and array values). */
interface Entry {
  name: string;
  row: ReactNode;
  /** True when the field has a value (opens its group). */
  set: boolean;
}

function GroupedFields({ groups, entries, scope }: { groups: Group[]; entries: Entry[]; scope: string }) {
  const buckets = groups.map(() => [] as Entry[]);
  for (const entry of entries) {
    const i = groups.findIndex(g => !g.match || g.match(entry.name));
    buckets[i]!.push(entry);
  }

  return (
    <>
      {groups.map((group, i) => buckets[i]!.length > 0 && (
        <details key={`${scope}:${group.name}`} className="group" open={group.open || buckets[i]!.some(e => e.set)}>
          <summary>{group.name}</summary>
          {buckets[i]!.map(e => <div key={e.name}>{e.row}</div>)}
        </details>
      ))}
    </>
  );
}

function ElementInspector({ node, owner }: { node: DesignerNode; owner: string }) {
  const setField = useDesigner(s => s.setField);
  const setExtra = useDesigner(s => s.setExtra);
  const resolver = useResolver();
  const tabId = useDesigner(s => s.workspace.activeTab);
  const targets = useNodeTargets(node.id);
  const goto = (field: string) => {
    const def = targets.get(field);
    return def ? () => revealDefinition(def) : undefined;
  };
  const info = typeInfo(node.type);
  const use = templateUse(node);
  const template = use ? definitionOf(resolver, 'template', owner, use.name, tabId) : undefined;
  const props = schemaProperties('ElementDefinition');
  // the child list member (Children, or a Form's Fields / a DataGrid's Columns, edited as tree items) is not a field
  const listMember = subItemsOf(node.type)?.member ?? 'Children';
  const members = new Set((info?.members ?? []).filter(m => m !== 'Children' && (m !== listMember || m in node.fields)));
  const listed = [...members, ...elementTypes.common.filter(c => c !== 'Type' && !members.has(c))];
  const extraSet = [...Object.keys(node.fields), ...Object.keys(node.extra)].filter(f => f !== 'Children' && f !== 'Type' && !listed.includes(f));
  const entries: Entry[] = [];

  for (const name of [...listed, ...new Set(extraSet)]) {
    // template instances and custom tags take any field as an argument
    const warning = info && !usesMember(node.type, name) ? `Not read by ${node.type}` : undefined;
    const description = describe('ElementDefinition', name);
    const extra = node.extra[name];
    if ((name in node.extra && typeof extra !== 'string') || (node.fields[name] === undefined && name in props && !allowsString(props[name]))) {
      entries.push({ name, set: extra !== undefined, row: <JsonRow name={name} value={extra} description={description} warning={warning} commit={v => setExtra(node.id, name, v)} /> });
    } else {
      const value = node.fields[name] ?? (typeof extra === 'string' ? extra : undefined);
      entries.push({
        name,
        set: value !== undefined,
        row: <FieldRow name={name} shape={shapeOf(node.type, name)} value={value} defaultValue={defaultOf(node.type, name)}
          description={description} warning={warning} goto={goto(name)} commit={v => setField(node.id, name, v)} />
      });
    }
  }

  return (
    <>
      <div className="inspector-head">
        <span className="type">{node.type}</span>
        {!info && <span className="muted">{node.type.includes('.') ? 'custom tag' : 'template instance'}: other fields are its arguments</span>}
        {template && use?.field === 'Type' && (
          <button type="button" className="small" title="Open the template" onClick={() => revealDefinition(template)}>→ {template.name}</button>
        )}
      </div>
      <TooltipFrom node={node} goto={goto('RichTooltip')} />
      <GroupedFields scope={node.id} groups={elementGroups(members)} entries={entries} />
      {!info && <AddArgument onAdd={name => setField(node.id, name, '')} />}
    </>
  );
}

/** RichTooltip.From: the named tooltip an element's rich tooltip starts from, picked from the workspace's tooltips. */
function TooltipFrom({ node, goto }: { node: DesignerNode; goto: (() => void) | undefined }) {
  const setExtra = useDesigner(s => s.setExtra);
  const rich = node.extra['RichTooltip'];
  const object = typeof rich === 'object' && rich !== null && !Array.isArray(rich) ? rich as Record<string, unknown> : undefined;
  if (rich !== undefined && object === undefined) {
    return null;
  }

  const from = typeof object?.['From'] === 'string' ? object['From'] : undefined;
  const commit = (value: string | undefined) => {
    const rest = Object.fromEntries(Object.entries(object ?? {}).filter(([k]) => k !== 'From'));
    setExtra(node.id, 'RichTooltip', value !== undefined ? { From: value, ...rest } : Object.keys(rest).length > 0 ? rest : undefined);
  };

  return (
    <div className={from === undefined ? 'field' : 'field set'}>
      <label htmlFor={`${node.id}-from`} title="RichTooltip.From: a named tooltip of the owner (Owners Tooltips); its blocks come first.">Tooltip from</label>
      <div className="control"><TextWidget id={`${node.id}-from`} mono list={refListId('tooltip')} value={from} placeholder="named tooltip" commit={commit} /></div>
      <button type="button" className="icon clear" title="Remove From" disabled={from === undefined} onClick={() => commit(undefined)}>×</button>
      {goto && <button type="button" className="icon goto" title="Go to the named tooltip" onClick={goto}>→</button>}
    </div>
  );
}

/** A Form field / DataGrid column: the members of its schema definition, then unknown members. */
function SubItemInspector({ node, kind }: { node: DesignerNode; kind: SubItemKind }) {
  const setField = useDesigner(s => s.setField);
  const setExtra = useDesigner(s => s.setExtra);
  const props = schemaProperties(kind.schema);
  const names = [...Object.keys(props), ...Object.keys(node.fields), ...Object.keys(node.extra)]
    .filter((n, i, all) => n !== kind.elements && all.indexOf(n) === i);
  const entries: Entry[] = [];

  for (const name of names) {
    const description = describe(kind.schema, name);
    const warning = name in props ? undefined : `Not a ${kind.title.toLowerCase()} field`;
    const extra = node.extra[name];
    if (name in node.extra || (node.fields[name] === undefined && !allowsString(props[name]))) {
      entries.push({ name, set: extra !== undefined, row: <JsonRow name={name} value={extra} description={description} warning={warning} commit={v => setExtra(node.id, name, v)} /> });
    } else {
      entries.push({
        name,
        set: node.fields[name] !== undefined,
        row: <FieldRow name={name} shape={shapeOf(node.type, name)} value={node.fields[name]} description={description} warning={warning}
          commit={v => setField(node.id, name, v)} />
      });
    }
  }

  return (
    <>
      <div className="inspector-head">
        <span className="type">{kind.title}</span>
        <span className="muted">{kind.elements ? `a ${kind.parent} item; its children are its ${kind.elements}` : `a ${kind.parent} item`}</span>
      </div>
      <GroupedFields scope={node.id} groups={[{ name: kind.title, open: true }]} entries={entries} />
    </>
  );
}

/** Adds a free-form field (an argument of a template instance or custom tag). */
function AddArgument({ onAdd }: { onAdd(name: string): void }) {
  const [name, setName] = useState('');
  const add = () => {
    const trimmed = name.trim();
    if (trimmed) {
      onAdd(trimmed);
      setName('');
    }
  };

  return (
    <div className="add-argument">
      <input type="text" placeholder="Argument name" value={name} onChange={e => setName(e.target.value)}
        onKeyDown={e => { if (e.key === 'Enter') { add(); } }} />
      <button type="button" className="small" onClick={add} disabled={!name.trim()}>Add</button>
    </div>
  );
}

function MenuInspector({ doc, tabId }: { doc: DesignerDocument; tabId: string }) {
  const setMenuField = useDesigner(s => s.setMenuField);
  const setMenuExtra = useDesigner(s => s.setMenuExtra);
  const { openMenuTemplate, addMenuTemplate } = useDesigner.getState();
  const def = useTabDefinition('menu', tabId);
  const props = schemaProperties('MenuDefinition');
  const skip = new Set(['Children', 'Templates', '$schema']);
  const names = [...Object.keys(props), ...Object.keys(doc.menu), ...Object.keys(doc.menuExtra)].filter((n, i, all) => !skip.has(n) && all.indexOf(n) === i);
  const entries: Entry[] = [];

  for (const name of names) {
    const description = describe('MenuDefinition', name);
    const warning = name in props ? undefined : 'Not a menu field';
    if (name in doc.menuExtra || (doc.menu[name] === undefined && !allowsString(props[name]))) {
      entries.push({ name, set: name in doc.menuExtra, row: <JsonRow name={name} value={doc.menuExtra[name]} description={description} warning={warning} commit={v => setMenuExtra(name, v)} /> });
    } else {
      entries.push({
        name,
        set: doc.menu[name] !== undefined,
        row: <FieldRow name={name} shape={shapeOf('menu', name)} value={doc.menu[name]} defaultValue={defaultOf('menu', name)}
          description={description} warning={warning} commit={v => setMenuField(name, v)} />
      });
    }
  }

  return (
    <>
      <div className="inspector-head">
        <span className="type">Menu</span>
        <span className="muted mono">{doc.owner}/{doc.menuId}</span>
      </div>
      <details className="group" open>
        <summary>Entry</summary>
        <OwnerRow owner={doc.owner} id="menu-owner" title="A loaded mod or content pack id, usually {{ModId}}; the Menus key is owner/menu id." />
        {def && <RenameRow def={def} label="Menu id" id="menu-id" />}
      </details>
      <GroupedFields scope="menu" groups={menuGroups} entries={entries} />
      <details className="group" open={Object.keys(doc.templates).length > 0}>
        <summary>Templates</summary>
        <ul className="used-by">
          {Object.entries(doc.templates).map(([name, t]) => (
            <li key={t.root}><button type="button" className="link" title="Open the template in its own tab" onClick={() => openMenuTemplate(tabId, t.root)}>{name}</button></li>
          ))}
        </ul>
        <button type="button" className="small" title="Add a template only this menu uses and open it" onClick={addMenuTemplate}>New menu template</button>
      </details>
      {def && <UsedBy def={def} />}
    </>
  );
}
