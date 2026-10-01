import { useState, type ReactNode } from 'react';
import type { DesignerDocument, DesignerNode } from '../model/document';
import { defaultOf, elementTypes, typeInfo, usesMember } from '../model/metadata';
import { useDesigner } from '../model/store';
import { allowsString, describe, schemaProperties, shapeOf } from '../fieldShapes';
import { FieldRow, JsonRow } from './fields/FieldRow';
import { TextWidget } from './fields/widgets';

// The inspector (architecture.md §6.2): menu fields for the root, the type's fields for an element, grouped.

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
        {node ? <ElementInspector key={node.id} node={node} /> : <MenuInspector doc={doc} />}
      </div>
    </aside>
  );
}

/** A field of the inspector: a string widget, a read-only JSON value, or (object-only and unset) nothing. */
interface Entry {
  name: string;
  row: ReactNode;
  /** True when the field has a value (opens its group). */
  set: boolean;
}

function GroupedFields({ groups, entries, objectOnly, scope }: { groups: Group[]; entries: Entry[]; objectOnly: string[]; scope: string }) {
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
      {objectOnly.length > 0 && (
        <p className="note">Not set (object values, edited in a later version): {objectOnly.join(', ')}.</p>
      )}
    </>
  );
}

function ElementInspector({ node }: { node: DesignerNode }) {
  const setField = useDesigner(s => s.setField);
  const info = typeInfo(node.type);
  const props = schemaProperties('ElementDefinition');
  const members = new Set((info?.members ?? []).filter(m => m !== 'Children'));
  const listed = [...members, ...elementTypes.common.filter(c => c !== 'Type' && !members.has(c))];
  const extraSet = [...Object.keys(node.fields), ...Object.keys(node.extra)].filter(f => f !== 'Children' && f !== 'Type' && !listed.includes(f));
  const entries: Entry[] = [];
  const objectOnly: string[] = [];

  for (const name of [...listed, ...new Set(extraSet)]) {
    // template instances and custom tags take any field as an argument
    const warning = info && !usesMember(node.type, name) ? `Not read by ${node.type}` : undefined;
    const description = describe('ElementDefinition', name);
    const extra = node.extra[name];
    if (name in node.extra && typeof extra !== 'string') {
      entries.push({ name, set: true, row: <JsonRow name={name} value={extra} description={description} warning={warning} /> });
    } else if (node.fields[name] === undefined && name in props && !allowsString(props[name])) {
      objectOnly.push(name);
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
      <GroupedFields scope={node.id} groups={elementGroups(members)} entries={entries} objectOnly={objectOnly} />
      {!info && <AddArgument onAdd={name => setField(node.id, name, '')} />}
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
  const setMeta = useDesigner(s => s.setMeta);
  const props = schemaProperties('MenuDefinition');
  const skip = new Set(['Children', 'Templates', '$schema']);
  const names = [...Object.keys(props), ...Object.keys(doc.menu), ...Object.keys(doc.menuExtra)].filter((n, i, all) => !skip.has(n) && all.indexOf(n) === i);
  const entries: Entry[] = [];
  const objectOnly: string[] = [];

  for (const name of names) {
    const description = describe('MenuDefinition', name);
    const warning = name in props ? undefined : 'Not a menu field';
    if (name in doc.menuExtra) {
      entries.push({ name, set: true, row: <JsonRow name={name} value={doc.menuExtra[name]} description={description} warning={warning} /> });
    } else if (doc.menu[name] === undefined && !allowsString(props[name])) {
      objectOnly.push(name);
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
      <GroupedFields scope="menu" groups={menuGroups} entries={entries} objectOnly={objectOnly} />
      {templates.length > 0 && <p className="note">Templates (read-only in this version): {templates.join(', ')}.</p>}
    </>
  );
}
