// Helpers of the structural rules (rules.ts): NormalizeType's tag / shorthand expansion and small value readers.
import { canonicalType, elementTypes } from '../model/metadata';
import { getMember, inferType, isObject, scalarText, type JsonObject } from '../io/dataFormat';

/** Members ExpandTag leaves on a custom tag / template instance (the rest become its arguments). */
const TagKeeps = new Set(['Type', 'Children', 'Composite', 'Args', 'ContentTarget', 'On', 'Template']);

/** ExpandTag: every member that is not common to all elements (nor kept) moves into Args; explicit Args win. */
export function expandTag(members: Map<string, unknown>, unknownNames: string[]): void {
  const args: JsonObject = {};
  const common = new Set(elementTypes.common.map(c => c.toLowerCase()));
  for (const [member, value] of [...members]) {
    const known = !unknownNames.includes(member);
    if (known && (TagKeeps.has(member) || common.has(member.toLowerCase()))) {
      continue;
    }

    if (!known && member.startsWith('$')) {
      members.delete(member);
      continue;
    }

    if (value !== undefined && value !== null) {
      args[member] = value;
    }

    members.delete(member);
  }

  const explicit = members.get('Args');
  if (isObject(explicit)) {
    Object.assign(args, explicit);
  }

  if (Object.keys(args).length > 0) {
    members.set('Args', args);
  }
}

/** NormalizeType's shorthands: { "Label": "Hi" } → Text, Button → Text, Checkbox (or Text) → Label, Image → Sprite; explicit members win. */
export function expandShorthand(type: string, members: Map<string, unknown>): void {
  const set = (m: string) => members.get(m) !== undefined && members.get(m) !== null;
  const take = (m: string) => {
    const v = members.get(m);
    members.delete(m);
    return v;
  };
  const fill = (m: string, v: unknown) => {
    if (!set(m) && v !== undefined && v !== null) {
      members.set(m, v);
    }
  };
  switch (type) {
    case 'Label':
      fill('Text', take('Label'));
      break;
    case 'Button':
      fill('Text', take('Button'));
      break;
    case 'Checkbox': {
      const checkbox = take('Checkbox');
      const text = take('Text');
      fill('Label', checkbox ?? text);
      break;
    }
    case 'Image':
      fill('Sprite', take('Image'));
      break;
  }
}

/** The key a member is written with in `obj` (its own spelling), for JSON pointers. */
export function memberKey(obj: JsonObject, member: string): string {
  return Object.keys(obj).find(k => k.toLowerCase() === member.toLowerCase()) ?? member;
}

/** StringListConverter: an array, or one comma-separated string. */
export function stringList(value: unknown): string[] {
  if (Array.isArray(value)) {
    return value.map(v => scalarText(v) ?? '');
  }

  const text = scalarText(value);
  return text === undefined ? [] : text.split(',').map(s => s.trim()).filter(s => s.length > 0);
}

export function isTrue(value: unknown): boolean {
  return scalarText(value)?.trim().toLowerCase() === 'true';
}

export function isIdentifier(text: string): boolean {
  return /^[\p{L}_][\p{L}\p{N}_]*$/u.test(text);
}

/** CountOutlets(children, ""): default Outlets of a body, not looking into nested instances. */
export function countDefaultOutlets(children: unknown[]): number {
  let count = 0;
  for (const child of children) {
    if (!isObject(child)) {
      continue;
    }

    const typeText = scalarText(getMember(child, 'Type'));
    const type = typeText !== undefined ? canonicalType(typeText) : inferType(m => getMember(child, m) != null)?.type ?? null;
    const outlet = (scalarText(getMember(child, 'Outlet')) ?? '').trim();
    if (type === 'Outlet' && (outlet === '' || outlet.toLowerCase() === 'default')) {
      count++;
    }

    const nested = getMember(child, 'Children');
    if (type !== 'Template' && type !== 'Composite' && type !== null && Array.isArray(nested)) {
      count += countDefaultOutlets(nested);
    }
  }

  return count;
}
