import { useState, type ReactNode } from 'react';
import type { DesignerDocument, DesignerNode } from '../model/document';
import { defaultOf, elementTypes, typeInfo, usesMember } from '../model/metadata';
import { useDesigner } from '../model/store';
import { subItemKind, subItemsOf, type SubItemKind } from '../model/subItems';
import { allowsString, describe, schemaProperties, shapeOf } from '../fieldShapes';
import { FieldRow, JsonRow } from './fields/FieldRow';
import { TextWidget } from './fields/widgets';

// The inspector (architecture.md §6.2): menu fields for the root, the type's fields for an element, the schema
// definition's fields for a sub-item (a Form field, a DataGrid column), grouped.

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
  const doc = useDesigner(s => s.doc);
  const selection = useDesigner(s => s.selection);
  const node = selection !== null && selection !== doc.root ? doc.nodes[selection] : undefined;

  return (
    <aside className="pane inspector" aria-label="Inspector">
      <div className="pane-title">Inspector</div>
      <div className="pane-body">
        {!node ? <MenuInspector doc={doc} />
          : subItemKind(node.type) ? <SubItemInspector key={node.id} node={node} kind={subItemKind(node.type)!} />
          : <ElementInspector key={node.id} node={node} />}
      </div>
    </aside>
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

function ElementInspector({ node }: { node: DesignerNode }) {
  const setField = useDesigner(s => s.setField);
  const setExtra = useDesigner(s => s.setExtra);
  const info = typeInfo(node.type);
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
          description={description} warning={warning} commit={v => setField(node.id, name, v)} />
      });
    }
  }

  return (
    <>
      <div className="inspector-head">
        <span className="type">{node.type}</span>
        {!info && <span className="muted">{node.type.includes('.') ? 'custom tag' : 'template instance'}: other fields are its arguments</span>}
      </div>
      <GroupedFields scope={node.id} groups={elementGroups(members)} entries={entries} />
      {!info && <AddArgument onAdd={name => setField(node.id, name, '')} />}
    </>
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

function MenuInspector({ doc }: { doc: DesignerDocument }) {
  const setMenuField = useDesigner(s => s.setMenuField);
  const setMenuExtra = useDesigner(s => s.setMenuExtra);
  const setMeta = useDesigner(s => s.setMeta);
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

  const templates = Object.keys(doc.templates);
  return (
    <>
      <div className="inspector-head">
        <span className="type">Menu</span>
        <span className="muted mono">{doc.owner}/{doc.menuId}</span>
      </div>
      <details className="group" open>
        <summary>Entry</summary>
        <div className="field set">
          <label htmlFor="menu-owner" title="A loaded mod or content pack id, usually {{ModId}}; the Menus key is owner/menu id.">Owner</label>
          <div className="control"><TextWidget id="menu-owner" mono value={doc.owner} commit={v => setMeta({ owner: v ?? '' })} /></div>
        </div>
        <div className="field set">
          <label htmlFor="menu-id" title="The menu id; the Menus key is owner/menu id.">Menu id</label>
          <div className="control"><TextWidget id="menu-id" mono value={doc.menuId} commit={v => setMeta({ menuId: v ?? '' })} /></div>
        </div>
      </details>
      <GroupedFields scope="menu" groups={menuGroups} entries={entries} />
      {templates.length > 0 && <p className="note">Templates (read-only in this version): {templates.join(', ')}.</p>}
    </>
  );
}
