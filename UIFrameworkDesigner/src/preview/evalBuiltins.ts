// Values, DataValue conversions and the framework's pure built-in functions for the preview evaluator (evaluate.ts):
// StardewUIFramework/Data/Expressions/BuiltinFunctions.cs plus GameFunctions' itemName over gameData.ts.
import { itemDisplayName } from './gameData';

export type Value = number | string | boolean | null | Value[];

/** Thrown when an expression uses something the preview cannot evaluate. */
export class Unknown extends Error {}

export const unknown = (): never => {
  throw new Unknown();
};

export function fromJson(value: unknown): Value {
  if (Array.isArray(value)) return value.map(fromJson);
  if (value === null || typeof value === 'number' || typeof value === 'string' || typeof value === 'boolean') return value;
  return JSON.stringify(value);
}

export function inferValue(text: string): Value {
  const t = text.trim();
  if (t.startsWith('[')) {
    try {
      return fromJson(JSON.parse(t));
    } catch {
      return text;
    }
  }
  if (/^(true|false)$/i.test(t)) {
    return t.toLowerCase() === 'true';
  }
  if (/^null$/i.test(t)) {
    return null;
  }
  if (/^[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?$/.test(t)) {
    return Number(t);
  }
  return text;
}

export function asBool(v: Value): boolean {
  if (typeof v === 'boolean') return v;
  if (Array.isArray(v)) return v.length > 0;
  if (typeof v === 'number') return v !== 0;
  if (typeof v === 'string') {
    const t = v.trim().toLowerCase();
    return t.length > 0 && t !== 'false' && t !== '0' && t !== 'no';
  }
  return false;
}

export function asNumber(v: Value): number {
  if (typeof v === 'number') return v;
  if (typeof v === 'boolean') return v ? 1 : 0;
  if (typeof v === 'string') {
    const n = Number(v.trim());
    return v.trim().length > 0 && Number.isFinite(n) ? n : 0;
  }
  return 0;
}

export function valueText(v: Value): string {
  if (v === null) return '';
  if (Array.isArray(v)) return v.map(valueText).join(', ');
  if (typeof v === 'boolean') return v ? 'true' : 'false';
  return String(v);
}

const numeric = (v: Value): boolean => typeof v === 'number' || (typeof v === 'string' && v.trim().length > 0 && Number.isFinite(Number(v.trim())));

/** DataValue.LooseEquals. */
export function looseEquals(a: Value, b: Value): boolean {
  if (a === null || b === null) return a === b;
  if (typeof a === 'boolean' || typeof b === 'boolean') return asBool(a) === asBool(b);
  if (typeof a === 'number' || typeof b === 'number') {
    const other = typeof a === 'number' ? b : a;
    return numeric(other) ? asNumber(a) === asNumber(b) : valueText(a) === valueText(b);
  }
  return a === b;
}

export function compare(a: Value, b: Value): number {
  if (numeric(a) && numeric(b)) return asNumber(a) - asNumber(b);
  const x = valueText(a);
  const y = valueText(b);
  return x < y ? -1 : x > y ? 1 : 0;
}

const asList = (v: Value): Value[] => (Array.isArray(v) ? v : v === null ? [] : [v]);

/** Math.Round(x, digits, MidpointRounding.AwayFromZero). */
function roundAway(x: number, digits: number): number {
  const f = 10 ** digits;
  return Math.sign(x) * Math.round(Math.abs(x) * f) / f;
}

const clampInt = (x: number, lo: number, hi: number): number => Math.trunc(Math.min(Math.max(x, lo), hi));

/** Invariant thousands separators in the integer part of a plain decimal text. */
function group(text: string): string {
  const [int, frac] = text.split('.');
  const grouped = int!.replace(/\B(?=(\d{3})+(?!\d))/g, ',');
  return frac !== undefined ? `${grouped}.${frac}` : grouped;
}

/** Standard numeric format strings F N P C E G with an optional precision (D and X throw on a double in .NET). */
function formatStandard(n: number, letter: string, p: number | undefined): string {
  switch (letter.toUpperCase()) {
    case 'F': return n.toFixed(p ?? 2);
    case 'N': return group(n.toFixed(p ?? 2));
    case 'P': return group((n * 100).toFixed(p ?? 2)) + ' %';
    case 'C': return (n < 0 ? '-' : '') + '¤' + group(Math.abs(n).toFixed(p ?? 2));
    case 'G': return p ? String(Number(n.toPrecision(p))) : String(n);
    case 'E': {
      const [mantissa, exponent] = n.toExponential(p ?? 6).split('e');
      const e = Number(exponent);
      return mantissa! + (letter === 'e' ? 'e' : 'E') + (e < 0 ? '-' : '+') + String(Math.abs(e)).padStart(3, '0');
    }
    default: return unknown();
  }
}

/** Custom format literals: quote marks are dropped. */
const stripQuotes = (text: string): string => text.replaceAll('\'', '').replaceAll('"', '');

/** |value| with `decimals` places ('0' required, '#' optional: trailing optional zeros dropped). */
function fixedDecimals(value: number, decimals: string): string {
  const minDecimals = decimals.replace(/#/g, '').length;
  const text = Math.abs(value).toFixed(decimals.length);
  if (decimals.length <= minDecimals) {
    return text;
  }
  let end = text.length;
  for (let n = decimals.length - minDecimals; n > 0 && text[end - 1] === '0'; n--) {
    end--;
  }
  return text.slice(0, end).replace(/\.$/, '');
}

/** Custom numeric format strings like "#,0.00" / "0%": prefix, integer digits, optional decimals, suffix. */
function formatCustom(n: number, pattern: string): string {
  const m = /^([^#0,.]*)([#0,]*)(?:\.([#0]*))?([^#0,.]*)$/.exec(pattern);
  if (!m || (!m[2] && m[3] === undefined)) return unknown();
  const prefix = stripQuotes(m[1]!);
  const suffix = stripQuotes(m[4]!);
  const value = (prefix + suffix).includes('%') ? n * 100 : n;
  const text = fixedDecimals(value, m[3] ?? '');
  const [intText, frac] = text.split('.');
  const intPattern = m[2] ?? '';
  const minInt = intPattern.replace(/[#,]/g, '').length;
  let int = intText === '0' && minInt === 0 ? '' : intText!.padStart(minInt, '0');
  if (intPattern.includes(',')) int = group(int);
  const sign = value < 0 && /[1-9]/.test(text) ? '-' : '';
  return sign + prefix + int + (frac !== undefined ? '.' + frac : '') + suffix;
}

/** double.ToString(pattern, InvariantCulture): the standard formats F N P C E G and custom ones like "#,0.00" / "0%". */
function formatNumber(n: number, pattern: string): string {
  if (pattern.length > 64) unknown();
  const std = /^([A-Za-z])(\d*)$/.exec(pattern);
  if (!std) {
    return formatCustom(n, pattern);
  }
  const p = std[2] ? Number(std[2]) : undefined;
  if (p !== undefined && p > 30) unknown();
  return formatStandard(n, std[1]!, p);
}

/** BuiltinFunctions.Money: rounded away from zero, invariant thousands separators, g suffix (1,234g). */
export function money(amount: number): string {
  const rounded = roundAway(Math.min(Math.max(amount, -1e15), 1e15), 0);
  return (rounded < 0 ? '-' : '') + group(String(Math.abs(rounded))) + 'g';
}

export interface Builtin { min: number; max: number; run(a: Value[]): Value }

const fn = (min: number, max: number, run: (a: Value[]) => Value): Builtin => ({ min, max, run });

function extreme(a: Value[], pickMax: boolean): Value {
  const values = a.length === 1 && Array.isArray(a[0]) ? a[0] : a;
  if (values.length === 0) return null;
  const numbers = values.map(asNumber);
  return pickMax ? Math.max(...numbers) : Math.min(...numbers);
}

/** BuiltinFunctions.RegisterAll plus GameFunctions' itemName, by lower-case name (FunctionRegistry ignores case). */
export const builtins: Record<string, Builtin> = {
  round: fn(1, 2, a => roundAway(asNumber(a[0]!), a.length > 1 ? clampInt(asNumber(a[1]!), 0, 15) : 0)),
  floor: fn(1, 1, a => Math.floor(asNumber(a[0]!))),
  ceil: fn(1, 1, a => Math.ceil(asNumber(a[0]!))),
  abs: fn(1, 1, a => Math.abs(asNumber(a[0]!))),
  min: fn(1, Infinity, a => extreme(a, false)),
  max: fn(1, Infinity, a => extreme(a, true)),
  clamp: fn(3, 3, a => Math.min(Math.max(asNumber(a[0]!), asNumber(a[1]!)), asNumber(a[2]!))),
  format: fn(2, 2, a => formatNumber(asNumber(a[0]!), valueText(a[1]!))),
  money: fn(1, 1, a => money(asNumber(a[0]!))),
  percent: fn(1, 2, a => {
    const digits = a.length > 1 ? clampInt(asNumber(a[1]!), 0, 10) : 0;
    return (roundAway(asNumber(a[0]!) * 100, digits) || 0).toFixed(digits) + '%';
  }),
  len: fn(1, 1, a => (a[0] === null ? 0 : Array.isArray(a[0]) ? a[0].length : valueText(a[0]!).length)),
  upper: fn(1, 1, a => valueText(a[0]!).toUpperCase()),
  lower: fn(1, 1, a => valueText(a[0]!).toLowerCase()),
  trim: fn(1, 1, a => valueText(a[0]!).trim()),
  contains: fn(2, 3, a => {
    const ignoreCase = a.length > 2 && asBool(a[2]!);
    const needle = valueText(a[1]!);
    if (Array.isArray(a[0])) {
      return a[0].some(item => (ignoreCase ? valueText(item).toLowerCase() === needle.toLowerCase() : looseEquals(item, a[1]!)));
    }
    const hay = valueText(a[0]!);
    return ignoreCase ? hay.toLowerCase().includes(needle.toLowerCase()) : hay.includes(needle);
  }),
  replace: fn(3, 3, a => {
    const text = valueText(a[0]!);
    const search = valueText(a[1]!);
    return search.length === 0 ? text : text.split(search).join(valueText(a[2]!));
  }),
  substring: fn(2, 3, a => {
    const text = valueText(a[0]!);
    let start = Math.trunc(asNumber(a[1]!));
    if (start < 0) start = Math.max(0, text.length + start);
    if (start >= text.length) return '';
    const length = a.length > 2 ? Math.max(0, Math.trunc(asNumber(a[2]!))) : text.length - start;
    return text.slice(start, start + Math.min(length, text.length - start));
  }),
  join: fn(1, 2, a => asList(a[0]!).map(valueText).join(a.length > 1 ? valueText(a[1]!) : ', ')),
  quote: fn(1, 1, a => '"' + valueText(a[0]!).replace(/"/g, '\\"') + '"'),
  num: fn(1, 1, a => asNumber(a[0]!)),
  str: fn(1, 1, a => valueText(a[0]!)),
  bool: fn(1, 1, a => asBool(a[0]!)),
  itemname: fn(1, 1, a => {
    const id = valueText(a[0]!).trim();
    return id.length === 0 ? '' : itemDisplayName(id);
  })
};
