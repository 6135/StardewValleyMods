import { canonicalType, elementTypes, isCustomTag, menuSchema } from '../model/metadata';

// Data-format knowledge shared by import, export and validation: the members of each definition model (read from the
// generated schema, i.e. the C# models' reflection order), shorthand detection exactly as DataValidator.NormalizeType
// does it, the "did you mean" suggestion and the JSON helpers both directions use.

/** A plain JSON object. */
export type JsonObject = Record<string, unknown>;

export function isObject(value: unknown): value is JsonObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/** The scalar text of a JSON value as Newtonsoft reads it into a string member (numbers / bools as their text). */
export function scalarText(value: unknown): string | undefined {
  if (typeof value === 'string') {
    return value;
  }

  if (typeof value === 'number' || typeof value === 'boolean') {
    return String(value);
  }

  return undefined;
}

// ---------------------------------------------------------------------------------------------------------------
//  Models (from menu.schema.json definitions)
// ---------------------------------------------------------------------------------------------------------------

export type Model = 'MenuDefinition' | 'ElementDefinition' | 'StyleDefinition' | 'TemplateDefinition' | 'ParamDefinition' | 'ColumnDefinition';

interface SchemaDefinition {
  properties?: Record<string, { $ref?: string }>;
}

function definitions(): Record<string, SchemaDefinition> {
  const defs = (menuSchema as { definitions?: Record<string, SchemaDefinition> }).definitions;
  return defs ?? {};
}

/** Fallback member lists when the schema bundle lacks a model (keeps the designer usable with an older bundle). */
function fallbackMembers(model: Model): string[] {
  switch (model) {
    case 'ElementDefinition': {
      const all = new Set<string>(elementTypes.common);
      for (const t of elementTypes.types) {
        for (const m of t.members) {
          all.add(m);
        }
      }

      for (const m of ['Label', 'Button', 'Checkbox', 'Image', 'Switch', 'Repeat']) {
        all.add(m);
      }

      return [...all];
    }
    case 'StyleDefinition':
      return ['Font', 'TextColor', 'HoverColor', 'BoxTexture', 'BoxSource', 'BoxScale', 'Padding', 'TextShadow', 'ClickSound', 'HoverSound'];
    case 'TemplateDefinition':
      return ['Params', 'Children', 'Horizontal', 'Spacing', 'Alignment'];
    case 'ParamDefinition':
      return ['Type', 'Default', 'Required', 'Description'];
    default:
      return [];
  }
}

const memberCache = new Map<Model, { list: string[]; byLower: Map<string, string> }>();

function membersOf(model: Model): { list: string[]; byLower: Map<string, string> } {
  let entry = memberCache.get(model);
  if (!entry) {
    const props = definitions()[model]?.properties;
    const list = props ? Object.keys(props).filter(k => !k.startsWith('$')) : fallbackMembers(model);
    entry = { list, byLower: new Map(list.map(m => [m.toLowerCase(), m])) };
    memberCache.set(model, entry);
  }

  return entry;
}

/** The members a model declares, in the C# declaration order (DataValidator.ModelProperties). */
export function modelMembers(model: Model): readonly string[] {
  return membersOf(model).list;
}

/** The declared spelling of a member (Newtonsoft matches member names case-insensitively), or null when unknown. */
export function canonicalMember(model: Model, name: string): string | null {
  return membersOf(model).byLower.get(name.toLowerCase()) ?? null;
}

/** True when the member is a plain value (string, number or bool), so a literal may be written as a JSON number / bool. */
export function isValueMember(model: Model, name: string): boolean {
  const prop = definitions()[model]?.properties?.[name];
  if (!prop) {
    // unknown to the schema (a template / custom tag argument, or no bundle): a plain value
    return canonicalMember(model, name) === null;
  }

  return prop.$ref === '#/definitions/Value';
}

/** Case-insensitive member lookup on a raw JSON object (Newtonsoft's matching). */
export function getMember(obj: JsonObject, name: string): unknown {
  if (name in obj) {
    return obj[name];
  }

  const lower = name.toLowerCase();
  for (const key of Object.keys(obj)) {
    if (key.toLowerCase() === lower) {
      return obj[key];
    }
  }

  return undefined;
}

/** True when the member is set (present and not null), as the framework's `!= null` checks see it. */
export function hasMember(obj: JsonObject, name: string): boolean {
  const value = getMember(obj, name);
  return value !== undefined && value !== null;
}

// ---------------------------------------------------------------------------------------------------------------
//  Shorthands (DataValidator.NormalizeType)
// ---------------------------------------------------------------------------------------------------------------

/** The value shorthands: member → [type, the member the value moves to]. */
export const valueShorthands: Record<string, { type: string; main: string }> = {
  Button: { type: 'Button', main: 'Text' },
  Checkbox: { type: 'Checkbox', main: 'Label' },
  Image: { type: 'Image', main: 'Sprite' },
  Label: { type: 'Label', main: 'Text' }
};

/**
 * The type a definition without `Type` gets and the shorthand that gave it, in NormalizeType's order: a template
 * instance (`Template`), then Switch, Repeat, Button, Checkbox, Image, Label, Children (a vertical Stack), Outlet.
 */
export function inferType(has: (member: string) => boolean): { type: string; shorthand: string } | null {
  for (const [member, type] of [
    ['Template', 'Template'],
    ['Switch', 'Switch'],
    ['Repeat', 'Repeat'],
    ['Button', 'Button'],
    ['Checkbox', 'Checkbox'],
    ['Image', 'Image'],
    ['Label', 'Label'],
    ['Children', 'Stack'],
    ['Outlet', 'Outlet']
  ] as const) {
    if (has(member)) {
      return { type, shorthand: member };
    }
  }

  return null;
}

/** Built-in element type names (ElementTypes.All). */
export function builtInTypes(): string[] {
  return elementTypes.types.map(t => t.name);
}

/** How an element's `Type` reads: a built-in type, a template of `templates`, a custom tag or unknown. */
export type TypeKind = 'builtin' | 'template' | 'customTag' | 'unknown';

export function typeKind(type: string, isTemplate: (name: string) => boolean): TypeKind {
  if (canonicalType(type) !== null) {
    return 'builtin';
  }

  if (isTemplate(type.trim())) {
    return 'template';
  }

  return isCustomTag(type) ? 'customTag' : 'unknown';
}

/** A case-insensitive, trimmed template-name lookup over a set of names (DataValidator.TemplateLookup). */
export function templateMatcher(names: Iterable<string>): (name: string) => boolean {
  const set = new Set<string>();
  for (const n of names) {
    set.add(n.trim().toLowerCase());
  }

  return name => set.has(name.trim().toLowerCase());
}

// ---------------------------------------------------------------------------------------------------------------
//  Suggestions (DataValidator.Suggest)
// ---------------------------------------------------------------------------------------------------------------

/** The closest candidate by edit distance (case-insensitive), or null when none is close. */
export function suggest(text: string, candidates: Iterable<string>): string | null {
  const lower = text.toLowerCase();
  let best: string | null = null;
  let bestDistance = Number.MAX_SAFE_INTEGER;
  for (const candidate of candidates) {
    const d = distance(lower, candidate.toLowerCase());
    if (d < bestDistance) {
      best = candidate;
      bestDistance = d;
    }
  }

  return best !== null && bestDistance <= Math.max(2, Math.floor(text.length / 3)) ? best : null;
}

/** " (did you mean 'x'?)" or empty. */
export function didYouMean(text: string, candidates: Iterable<string>): string {
  const s = suggest(text, candidates);
  return s !== null ? ` (did you mean '${s}'?)` : '';
}

function distance(a: string, b: string): number {
  let previous = new Array<number>(b.length + 1);
  let current = new Array<number>(b.length + 1);
  for (let j = 0; j <= b.length; j++) {
    previous[j] = j;
  }

  for (let i = 1; i <= a.length; i++) {
    current[0] = i;
    for (let j = 1; j <= b.length; j++) {
      const cost = a[i - 1] === b[j - 1] ? 0 : 1;
      current[j] = Math.min(current[j - 1]! + 1, previous[j]! + 1, previous[j - 1]! + cost);
    }

    [previous, current] = [current, previous];
  }

  return previous[b.length]!;
}

// ---------------------------------------------------------------------------------------------------------------
//  Paths (DataPath)
// ---------------------------------------------------------------------------------------------------------------

/** `parent.name` (a member). */
export function fieldPath(parent: string, name: string): string {
  return parent ? `${parent}.${name}` : name;
}

/** `parent[i]` or `parent[i](#id)` (a list item, with its id when it has one). */
export function indexPath(parent: string, index: number, id?: string | null): string {
  return `${parent}[${index}]${id ? `(#${id})` : ''}`;
}

/** The `Menus["key"]` entry prefix of a path. */
export function entryPath(key: string): string {
  return `Menus["${key}"]`;
}

/** Append one JSON pointer segment (RFC 6901 escaping). */
export function pointer(parent: string, segment: string | number): string {
  return `${parent}/${String(segment).replace(/~/g, '~0').replace(/\//g, '~1')}`;
}
