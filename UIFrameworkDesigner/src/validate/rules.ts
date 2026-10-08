import type { Problem } from '../model/document';
import { canonicalType, elementTypes, isContainer, usesMember, usesStyle } from '../model/metadata';
import { countDefaultOutlets, expandShorthand, expandTag, isIdentifier, isTrue, memberKey, stringList } from './ruleHelpers';
import {
  builtInTypes,
  canonicalMember,
  didYouMean,
  fieldPath,
  getMember,
  indexPath,
  inferType,
  isObject,
  modelMembers,
  pointer,
  scalarText,
  suggest,
  templateMatcher,
  typeKind,
  type JsonObject,
  type Model
} from '../io/dataFormat';

// The structural part of DataValidator, ported over the exported MenuDefinition JSON (the same object the schema pass
// checks): types and shorthands, unknown and unused members, ids, Out keys, style fields, collections, templates and
// their arguments. Messages keep the framework's wording. Expressions, actions, sprites, items and game data are left
// to the in-game validator (architecture.md §8).

/** A problem plus the JSON pointer of the value it is about (mapped to a node by the caller). */
export interface RawProblem extends Problem {
  pointer: string;
}

const ParamTypes = ['string', 'number', 'bool', 'any'];

/**
 * The owner-level templates a menu's instances can expand (from the workspace resolver, model/resolve.ts): their names
 * and Params; null when the owner's Owners entry is not in the workspace (instances of other templates are assumed to
 * name one of it).
 */
export interface OwnerTemplates {
  names: string[];
  params(name: string): unknown;
}

/** Check a MenuDefinition object. */
export function checkMenu(menu: JsonObject, ownerTemplates: OwnerTemplates | null = null): RawProblem[] {
  return new Checker(menu, ownerTemplates).run();
}

/** One element after NormalizeType: its canonical members (shorthands expanded, tag arguments moved into Args). */
interface Normalized {
  type: string;
  members: Map<string, unknown>;
  /** The template an instance expands ("Template" type), when it is one. */
  template?: string;
}

class Checker {
  private readonly problems: RawProblem[] = [];
  private readonly templates = new Map<string, JsonObject>();
  private readonly isLocalTemplate: (name: string) => boolean;
  /** A template of the menu or (when the workspace holds the owner's entry) of the owner. */
  private readonly isKnownTemplate: (name: string) => boolean;
  private bodyDepth = 0;

  constructor(private readonly menu: JsonObject, private readonly ownerTemplates: OwnerTemplates | null) {
    const templates = getMember(menu, 'Templates');
    if (isObject(templates)) {
      for (const [name, def] of Object.entries(templates)) {
        if (isObject(def)) {
          this.templates.set(name.trim().toLowerCase(), def);
        }
      }
    }

    this.isLocalTemplate = templateMatcher(this.templates.keys());
    const isOwnerTemplate = templateMatcher(ownerTemplates?.names ?? []);
    this.isKnownTemplate = name => this.isLocalTemplate(name) || isOwnerTemplate(name);
  }

  /** The Params of a template an instance names: the menu's first, then the owner's; undefined when unknown. */
  private templateParams(name: string): unknown {
    const local = this.templates.get(name.trim().toLowerCase());
    if (local) {
      return getMember(local, 'Params');
    }

    const owner = this.ownerTemplates?.names.find(n => n.trim().toLowerCase() === name.trim().toLowerCase());
    return owner !== undefined ? this.ownerTemplates!.params(owner) : undefined;
  }

  /** "'x' is not … of the owner's Owners entry" for a name no template of the menu or owner has (did you mean). */
  private missingTemplate(name: string): string {
    return `'${name}' is not a template of this menu or of the owner's Owners entry${didYouMean(name, [...this.templates.keys(), ...(this.ownerTemplates?.names ?? [])])}`;
  }

  run(): RawProblem[] {
    const menu = this.menu;
    this.unknown(menu, 'MenuDefinition', '', '');
    const templates = getMember(menu, 'Templates');
    if (isObject(templates)) {
      this.templateDefinitions(templates, 'Templates', pointer('', 'Templates'));
    }

    const types = new Map<string, string>();
    this.children(getMember(menu, 'Children'), '', '', types);
    this.buttonRef(menu, 'DefaultButton', types);
    this.buttonRef(menu, 'CancelButton', types);
    return this.problems;
  }

  private add(severity: Problem['severity'], path: string, ptr: string, message: string, field?: string): void {
    this.problems.push(field !== undefined ? { severity, path, pointer: ptr, message, field } : { severity, path, pointer: ptr, message });
  }

  /** A member of the element at `path` / `ptr`. */
  private addField(severity: Problem['severity'], path: string, ptr: string, member: string, message: string): void {
    this.add(severity, fieldPath(path, member), ptr, message, member);
  }

  private unknown(obj: JsonObject, model: Model, path: string, ptr: string): void {
    const known = modelMembers(model);
    if (known.length === 0) {
      return;
    }

    for (const name of Object.keys(obj)) {
      if (!name.startsWith('$') && canonicalMember(model, name) === null) {
        this.addField('warning', path, ptr, name, `unknown field '${name}'${didYouMean(name, known)}; it is ignored.`);
      }
    }
  }

  // -------------------------------------------------------------------------------------------------------------
  //  Elements
  // -------------------------------------------------------------------------------------------------------------

  private children(list: unknown, path: string, ptr: string, types: Map<string, string>): void {
    if (!Array.isArray(list)) {
      return;
    }

    const listPath = fieldPath(path, 'Children');
    const listPtr = pointer(ptr, 'Children');
    this.elementList(list, listPath, listPtr, types);
  }

  /** Check a list of elements (`listPath[i]`); a single object (a RowTemplate / Cell written as one element) is item 0. */
  private elementList(list: unknown, listPath: string, listPtr: string, types: Map<string, string>): void {
    const items = Array.isArray(list) ? list : isObject(list) ? [list] : [];
    items.forEach((item, i) => {
      const itemPtr = Array.isArray(list) ? pointer(listPtr, i) : listPtr;
      if (!isObject(item)) {
        this.add('warning', indexPath(listPath, i), itemPtr, 'empty element; it is skipped.');
        return;
      }

      this.element(item, indexPath(listPath, i, scalarText(getMember(item, 'Id'))), itemPtr, types);
    });
  }

  private element(def: JsonObject, path: string, ptr: string, types: Map<string, string>): void {
    const n = this.normalize(def, path, ptr);
    this.registerId(scalarText(n?.members.get('Id') ?? getMember(def, 'Id'))?.trim(), n?.type ?? '', path, ptr, types);
    if (n === null) {
      return;
    }

    const { type, members } = n;
    this.unusedMembers(type, members, path, ptr);
    this.typeChecks(n, path, ptr);
    this.collection(def, type, members, path, ptr);
    this.styleChecks(type, members.get('Style'), path, ptr);
    this.valueChecks(type, members, path, ptr);
    if (isContainer(type)) {
      this.children(members.get('Children'), path, ptr, types);
    }
  }

  /** Records the element's id and type in the menu's id table; a repeated id is reported. */
  private registerId(id: string | undefined, type: string, path: string, ptr: string, types: Map<string, string>): void {
    if (!id) {
      return;
    }

    if (types.has(id)) {
      this.addField('warning', path, ptr, 'Id', `id '${id}' is used more than once in this menu; lookups by id return the first one.`);
    } else {
      types.set(id, type);
    }
  }

  /** Members the type does not use. */
  private unusedMembers(type: string, members: Map<string, unknown>, path: string, ptr: string): void {
    for (const [member, value] of members) {
      if (value !== undefined && value !== null && canonicalMember('ElementDefinition', member) !== null && !usesMember(type, member)) {
        this.addField('warning', path, ptr, member, `${member} is not used by a ${type}; it is ignored.`);
      }
    }
  }

  /** A Composite's name, a template instance's arguments, an Outlet outside a body, unknown Out keys, a Switch's pages. */
  private typeChecks(n: Normalized, path: string, ptr: string): void {
    const { type, members } = n;
    const get = (m: string) => members.get(m);
    if (type === 'Composite' && !scalarText(get('Composite'))?.trim()) {
      this.addField('error', path, ptr, 'Composite', 'a Composite needs the name of a composite defined in C# or the Composites asset ("Composite": "ModId.Name"); nothing is built.');
    }

    if (type === 'Template' && n.template !== undefined) {
      this.args(n.template, this.templateParams(n.template), get('Args'), path, ptr);
    }

    if (type === 'Outlet' && this.bodyDepth === 0) {
      this.add('warning', path, ptr, 'an Outlet placeholder only receives children inside a template or data composite body; here it shows its own children.');
    }

    const out = get('Out');
    if (isObject(out)) {
      for (const key of Object.keys(out)) {
        if (!elementTypes.outKeys.some(k => k.toLowerCase() === key.trim().toLowerCase())) {
          this.add('warning', fieldPath(fieldPath(path, 'Out'), key), ptr, `unknown output '${key}'${didYouMean(key, elementTypes.outKeys)}; it is ignored.`, 'Out');
        }
      }
    }

    const children = get('Children');
    if (type === 'Switch' && (!Array.isArray(children) || children.length === 0)) {
      this.addField('warning', path, ptr, 'Children', 'a Switch needs pages (children with a Case).');
    }
  }

  /** Unknown Style fields and the ones the type does not use. */
  private styleChecks(type: string, style: unknown, path: string, ptr: string): void {
    if (isObject(style)) {
      const stylePath = fieldPath(path, 'Style');
      this.unknown(style, 'StyleDefinition', stylePath, ptr);
      for (const [key, value] of Object.entries(style)) {
        const prop = canonicalMember('StyleDefinition', key);
        if (prop !== null && value !== null && !usesStyle(type, prop)) {
          this.add('warning', fieldPath(stylePath, prop), ptr, `a ${type} does not use Style.${prop}; it is ignored.`, 'Style');
        }
      }
    }
  }

  /** An Image's Source, a Dropdown's Choices / ChoicesSource, and Labels matching the Choices. */
  private valueChecks(type: string, members: Map<string, unknown>, path: string, ptr: string): void {
    const get = (m: string) => members.get(m);
    const set = (m: string) => get(m) !== undefined && get(m) !== null;
    if (type === 'Image' && set('Source') && typeof get('Source') !== 'string') {
      this.addField('warning', path, ptr, 'Source', "an Image's Source is a rectangle 'x,y,width,height'; it is ignored.");
    }

    if (type === 'Dropdown') {
      if (set('ChoicesSource')) {
        if (set('Choices')) {
          this.addField('warning', path, ptr, 'Choices', 'Choices are ignored when a ChoicesSource is set.');
        }
      } else if (stringList(get('Choices')).length === 0) {
        this.addField('warning', path, ptr, 'Choices', 'a Dropdown needs Choices (or a ChoicesSource).');
      }
    }

    if (set('Labels') && set('Choices')) {
      const labels = stringList(get('Labels')).length;
      const choices = stringList(get('Choices')).length;
      if (labels !== choices) {
        this.addField('warning', path, ptr, 'Labels', `${labels} label(s) for ${choices} choice(s); missing labels show the value.`);
      }
    }
  }

  /** NormalizeType: the element's type and members, or null (reported) when it has no usable type. */
  private normalize(def: JsonObject, path: string, ptr: string): Normalized | null {
    const members = new Map<string, unknown>();
    const unknownNames: string[] = [];
    for (const [key, value] of Object.entries(def)) {
      const member = canonicalMember('ElementDefinition', key);
      if (member === null) {
        unknownNames.push(key);
      }

      members.set(member ?? key, value);
    }

    const set = (m: string) => members.get(m) !== undefined && members.get(m) !== null;
    const typeText = set('Type') ? (scalarText(members.get('Type')) ?? '').trim() : null;
    const kind = typeText !== null ? typeKind(typeText, this.isKnownTemplate) : null;
    const hasTemplate = set('Template');

    if (hasTemplate || kind === 'template' || kind === 'customTag' || kind === 'unknown') {
      return this.normalizeInstance(members, unknownNames, typeText, kind, path, ptr);
    }

    for (const name of unknownNames) {
      if (!name.startsWith('$')) {
        this.addField('warning', path, ptr, name, `unknown field '${name}'${didYouMean(name, modelMembers('ElementDefinition'))}; it is ignored.`);
      }
    }

    let type: string;
    if (typeText !== null) {
      type = canonicalType(typeText)!;
    } else {
      const inferred = inferType(set);
      if (inferred === null) {
        this.add('error', path, ptr, 'the element has no Type (or shorthand such as "Label": "text"); it is skipped.');
        return null;
      }

      type = inferred.type;
    }

    expandShorthand(type, members);
    members.set('Type', type);
    return { type, members };
  }

  /**
   * NormalizeType for a template instance or custom tag (of `kind`, the Type's): Template / Composite set, other
   * members moved into Args; null (reported) when the type is unknown but close to a built-in one.
   */
  private normalizeInstance(members: Map<string, unknown>, unknownNames: string[], typeText: string | null, kind: ReturnType<typeof typeKind> | null, path: string, ptr: string): Normalized | null {
    const hasTemplate = members.get('Template') !== undefined && members.get('Template') !== null;
    if (kind === 'unknown' && !hasTemplate && !this.unknownType(typeText!, path, ptr)) {
      return null;
    }

    const isComposite = kind === 'customTag' && !hasTemplate;
    const name = isComposite ? typeText! : scalarText(members.get('Template')) ?? typeText!;
    expandTag(members, unknownNames);
    if (isComposite) {
      if (members.get('Composite') === undefined || members.get('Composite') === null) {
        members.set('Composite', name);
      }

      members.set('Type', 'Composite');
      return { type: 'Composite', members };
    }

    members.set('Template', name.trim());
    members.set('Type', 'Template');
    if (hasTemplate && !this.isKnownTemplate(name)) {
      if (this.ownerTemplates === null) {
        this.addField('info', path, ptr, 'Template', `'${name}' is not a template of this menu; it must be a template of the owner's Owners entry, otherwise the instance is empty.`);
      } else {
        this.addField('warning', path, ptr, 'Template', `${this.missingTemplate(name)}; the instance is empty.`);
      }
    }

    return { type: 'Template', members, template: name.trim() };
  }

  /** Reports a type that is neither built in nor a known template; false when the element is skipped (a near built-in name). */
  private unknownType(typeText: string, path: string, ptr: string): boolean {
    const suggestion = suggest(typeText, builtInTypes());
    if (suggestion !== null) {
      this.addField('error', path, ptr, 'Type', `unknown element type '${typeText}' (did you mean '${suggestion}'?); the element is skipped.`);
      return false;
    }

    if (this.ownerTemplates === null) {
      // most likely a template of the owner's Owners entry, which the workspace does not hold
      this.addField('info', path, ptr, 'Type', `'${typeText}' is not a built-in type or a template of this menu; it must be a template of the owner's Owners entry, otherwise the element is skipped.`);
    } else {
      this.addField('warning', path, ptr, 'Type', `${this.missingTemplate(typeText)} or a built-in type; the element is skipped.`);
    }

    return true;
  }

  /** CheckArgs: required parameters present, arguments that are no parameter reported. */
  private args(name: string, params: unknown, args: unknown, path: string, ptr: string): void {
    if (!isObject(params)) {
      return;
    }

    const given = isObject(args) ? Object.keys(args) : [];
    for (const [param, def] of Object.entries(params)) {
      if (isObject(def) && isTrue(getMember(def, 'Required')) && !given.some(k => k.trim().toLowerCase() === param.trim().toLowerCase())) {
        this.add('error', path, ptr, `template '${name}' needs the argument '${param}' (Required); the body reads it as empty.`);
      }
    }

    for (const arg of given) {
      if (!Object.keys(params).some(p => p.trim().toLowerCase() === arg.trim().toLowerCase())) {
        this.add('warning', fieldPath(path, arg), ptr, `template '${name}' has no parameter '${arg}'${didYouMean(arg, Object.keys(params))}.`, arg);
      }
    }
  }

  // -------------------------------------------------------------------------------------------------------------
  //  Collections (CheckCollection), with their row / cell templates checked under their own id tables
  // -------------------------------------------------------------------------------------------------------------

  private collection(def: JsonObject, type: string, members: Map<string, unknown>, path: string, ptr: string): void {
    const get = (m: string) => members.get(m);
    const set = (m: string) => get(m) !== undefined && get(m) !== null;
    switch (type) {
      case 'List': {
        if (!set('Source')) {
          this.addField('warning', path, ptr, 'Source', 'a List needs a Source; it shows no rows.');
        }

        const row = get('RowTemplate');
        if (!set('RowTemplate') || (Array.isArray(row) && row.length === 0)) {
          this.addField('warning', path, ptr, 'RowTemplate', 'a List needs a RowTemplate (the elements of one row); its rows are empty.');
        }

        this.templateTree(row, fieldPath(path, 'RowTemplate'), pointer(ptr, memberKey(def, 'RowTemplate')));
        break;
      }

      case 'DataGrid':
        if (!set('Source')) {
          this.addField('warning', path, ptr, 'Source', 'a DataGrid needs a Source; it shows no rows.');
        }

        this.gridColumns(def, members, path, ptr);
        break;

      case 'Repeat': {
        const children = get('Children');
        if (!Array.isArray(children) || children.length === 0) {
          this.addField('warning', path, ptr, 'Children', 'a Repeat needs Children (the elements copied for each row).');
        }

        this.templateTree(children, fieldPath(path, 'Children'), pointer(ptr, memberKey(def, 'Children')));
        break;
      }

      case 'Grid': {
        const columns = get('Columns');
        if (Array.isArray(columns)) {
          columns.forEach((column, i) => {
            if (isObject(column) && ['Id', 'Header', 'Text', 'Cell'].some(m => getMember(column, m) != null)) {
              this.add('warning', indexPath(fieldPath(path, 'Columns'), i, scalarText(getMember(column, 'Id'))), ptr,
                "a Grid's columns only have a Width (a track); the other members are for a DataGrid.", 'Columns');
            }
          });
        }

        break;
      }

      case 'Form':
        if (set('Model')) {
          if (set('Fields')) {
            this.addField('warning', path, ptr, 'Fields', 'Fields are ignored when the Form has a Model (its members are the fields).');
          }
        } else {
          this.formFields(get('Fields'), fieldPath(path, 'Fields'), ptr, pointer(ptr, memberKey(def, 'Fields')));
        }

        break;
    }

    const as = scalarText(get('As'));
    if (as !== undefined && !isIdentifier(as.trim())) {
      this.addField('error', path, ptr, 'As', `'${as}' is not a variable name (letters, digits and _ , not starting with a digit).`);
    }
  }

  private gridColumns(def: JsonObject, members: Map<string, unknown>, path: string, ptr: string): void {
    const columns = members.get('Columns');
    const list = Array.isArray(columns) ? columns : [];
    if (list.length === 0) {
      this.addField('warning', path, ptr, 'Columns', 'a DataGrid needs Columns ([{ "Id": "name", "Header": "Name", "Text": "${row.name}" }, ...]).');
      return;
    }

    const ids = new Set<string>();
    const columnsPtr = pointer(ptr, memberKey(def, 'Columns'));
    list.forEach((column, i) => {
      if (!isObject(column)) {
        return;
      }

      const columnPath = indexPath(fieldPath(path, 'Columns'), i, scalarText(getMember(column, 'Id')));
      const columnPtr = pointer(columnsPtr, i);
      this.unknown(column, 'ColumnDefinition', columnPath, columnPtr);
      const id = scalarText(getMember(column, 'Id'))?.trim();
      if (!id) {
        this.add('error', fieldPath(columnPath, 'Id'), columnPtr, 'a DataGrid column needs an Id; the column is skipped.', 'Id');
        return;
      }

      if (ids.has(id)) {
        this.add('warning', fieldPath(columnPath, 'Id'), columnPtr, `column id '${id}' is used more than once; the last one wins.`, 'Id');
      }

      ids.add(id);
      this.templateTree(getMember(column, 'Cell'), fieldPath(columnPath, 'Cell'), pointer(columnPtr, memberKey(column, 'Cell')));
    });

    const sort = scalarText(members.get('Sort'))?.trim();
    if (sort) {
      const sortColumn = sort.split(' ')[0]!;
      if (!ids.has(sortColumn)) {
        this.addField('warning', path, ptr, 'Sort', `no column has the id '${sortColumn}'${didYouMean(sortColumn, ids)}.`);
      }
    }
  }

  private formFields(fields: unknown, path: string, ptr: string, listPtr: string): void {
    if (!Array.isArray(fields) || fields.length === 0) {
      this.add('warning', path, ptr, 'a Form needs Fields.', 'Fields');
      return;
    }

    const ids = new Set<string>();
    fields.forEach((field, i) => {
      if (!isObject(field)) {
        return;
      }

      const id = scalarText(getMember(field, 'Id'))?.trim();
      const fieldItem = indexPath(path, i, id);
      const fieldPtr = pointer(listPtr, i);
      this.unknown(field, 'FormFieldDefinition', fieldItem, fieldPtr);
      if (id === undefined && getMember(field, 'Bind') == null) {
        this.add('warning', fieldItem, fieldPtr, 'the field has no Id or Bind; it is named field<index> and bound to menu.field<index>.');
      } else if (id !== undefined && ids.has(id)) {
        this.add('warning', fieldPath(fieldItem, 'Id'), fieldPtr, `field id '${id}' is used more than once in this form.`, 'Id');
      } else if (id !== undefined) {
        ids.add(id);
      }

      const kind = scalarText(getMember(field, 'Kind'))?.trim();
      if (kind !== undefined && !['checkbox', 'number', 'integer', 'text', 'dropdown'].includes(kind.toLowerCase())) {
        this.add('error', fieldPath(fieldItem, 'Kind'), fieldPtr, `'${kind}' is not Checkbox, Number, Integer, Text or Dropdown; the field is skipped.`, 'Kind');
      }
    });
  }

  /** A row / cell template: its elements are checked with their own id table. */
  private templateTree(list: unknown, listPath: string, listPtr: string): void {
    if (list === undefined || list === null) {
      return;
    }

    this.elementList(list, listPath, listPtr, new Map());
  }

  // -------------------------------------------------------------------------------------------------------------
  //  Templates (CheckTemplateDefinitions / CheckTemplateBody / CheckParams)
  // -------------------------------------------------------------------------------------------------------------

  private templateDefinitions(templates: JsonObject, path: string, ptr: string): void {
    for (const [name, def] of Object.entries(templates)) {
      const templatePath = fieldPath(path, name);
      const templatePtr = pointer(ptr, name);
      if (!isObject(def)) {
        continue;
      }

      if (name.trim().length === 0 || name.includes('.') || canonicalType(name) !== null) {
        this.add('error', templatePath, templatePtr, `'${name}' cannot name a template: it must not be empty, contain a dot (custom tags) or be a built-in type.`);
        continue;
      }

      this.unknown(def, 'TemplateDefinition', templatePath, templatePtr);
      const params = getMember(def, 'Params');
      if (isObject(params)) {
        this.params(params, fieldPath(templatePath, 'Params'), pointer(templatePtr, memberKey(def, 'Params')));
      }

      const children = getMember(def, 'Children');
      if (!Array.isArray(children) || children.length === 0) {
        this.add('warning', fieldPath(templatePath, 'Children'), templatePtr, `'${name}' has no body (Children); its instances are empty.`);
        continue;
      }

      this.bodyDepth++;
      try {
        this.children(children, templatePath, templatePtr, new Map());
        const defaults = countDefaultOutlets(children);
        if (defaults > 1) {
          this.add('warning', fieldPath(templatePath, 'Children'), templatePtr, `'${name}' has ${defaults} default Outlets; the instance's children go into the first one.`);
        }
      } finally {
        this.bodyDepth--;
      }
    }
  }

  private params(params: JsonObject, path: string, ptr: string): void {
    for (const [name, def] of Object.entries(params)) {
      const paramPath = fieldPath(path, name);
      const paramPtr = pointer(ptr, name);
      if (!isObject(def)) {
        continue;
      }

      this.unknown(def, 'ParamDefinition', paramPath, paramPtr);
      if (!isIdentifier(name.trim())) {
        this.add('error', paramPath, paramPtr, `'${name}' is not a parameter name (letters, digits and _ , not starting with a digit); args.${name} cannot read it.`);
      }

      const type = scalarText(getMember(def, 'Type'));
      if (type !== undefined && !ParamTypes.includes(type.trim().toLowerCase())) {
        this.add('warning', fieldPath(paramPath, 'Type'), paramPtr, `'${type}' is not string, number, bool or any${didYouMean(type, ParamTypes)}; the value is used as is.`);
      }

      const required = scalarText(getMember(def, 'Required'));
      if (required !== undefined && !['true', 'false'].includes(required.trim().toLowerCase())) {
        this.add('warning', fieldPath(paramPath, 'Required'), paramPtr, `'${required}' is not true or false.`);
      }

      if (getMember(def, 'Default') != null && isTrue(getMember(def, 'Required'))) {
        this.add('info', fieldPath(paramPath, 'Default'), paramPtr, "a Required parameter's Default is never used.");
      }
    }
  }

  // -------------------------------------------------------------------------------------------------------------
  //  Menu
  // -------------------------------------------------------------------------------------------------------------

  private buttonRef(menu: JsonObject, member: string, types: Map<string, string>): void {
    const id = scalarText(getMember(menu, member))?.trim();
    if (!id) {
      return;
    }

    const type = types.get(id);
    if (type === undefined) {
      this.add('warning', member, '', `no element has the id '${id}'.`, member);
    } else if (type !== 'Button') {
      this.add('warning', member, '', `'${id}' is a ${type}, not a Button.`, member);
    }
  }
}
