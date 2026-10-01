// A tiny, safe evaluator for preview sample values (architecture.md §7.2): no eval / Function. It follows the operator
// set and precedence of StardewUIFramework/Data/Expressions/Lexer.cs + Parser.cs (ternary < || < && < == != <
// < <= > >= < + - < * / % < unary ! -), their literals (numbers, '…' / "…" strings with \n \t \r \uXXXX escapes,
// true / false / null) and DataValue's conversions (+ concatenates when a side is text, / and % by 0 give 0, loose ==).
// Dotted paths (menu.count) are looked up whole in the preview state (a JSON array text reads as a list). Calls run the
// framework's pure built-ins (Data/Expressions/BuiltinFunctions.cs, same names, arity and semantics) and itemName from
// Data/GameFunctions.cs over a small bundled table (gameData.ts); `@name(…)` calls (functions C# registers, Lexer
// AtReference + ExternalCallNode) run the preview's stand-ins (ExternalFunctions); anything else (other game
// functions, indexing, unknown names) makes the result undefined.
import type { DesignerDocument } from '../model/document';
import { CHIP_END, CHIP_EXPR, hasTemplate } from '../layout';
import { itemDisplayName } from './gameData';

type Value = number | string | boolean | null | Value[];

/**
 * Stand-ins for functions C# registers (RegisterFunction: string arguments, a string result), by lower-case name;
 * undefined when the call cannot be evaluated.
 */
export type ExternalFunctions = Record<string, (args: string[]) => string | undefined>;

class Unknown extends Error {}

const unknown = (): never => {
  throw new Unknown();
};

interface Token { kind: string; text: string; value?: Value }

function lex(source: string): Token[] {
  const tokens: Token[] = [];
  let i = 0;
  while (i < source.length) {
    const c = source[i]!;
    if (/\s/.test(c)) {
      i++;
      continue;
    }
    if (/[0-9]/.test(c)) {
      const m = /^\d+(\.\d+)?([eE][+-]?\d+)?/.exec(source.slice(i))!;
      tokens.push({ kind: 'num', text: m[0], value: Number(m[0]) });
      i += m[0].length;
      continue;
    }
    if (/[A-Za-z_]/.test(c)) {
      const m = /^[A-Za-z_][A-Za-z0-9_]*/.exec(source.slice(i))!;
      tokens.push({ kind: 'id', text: m[0] });
      i += m[0].length;
      continue;
    }
    if (c === '@') {
      // Lexer.ReadReference: @name, @owner/name (identifier characters, '.', '-')
      const m = /^[A-Za-z0-9_.-]+(?:\/[A-Za-z0-9_-]+)?/.exec(source.slice(i + 1)) ?? unknown();
      tokens.push({ kind: 'at', text: m[0] });
      i += 1 + m[0].length;
      continue;
    }
    if (c === '"' || c === '\'') {
      let j = i + 1;
      let text = '';
      for (; j < source.length && source[j] !== c; j++) {
        if (source[j] === '\\' && j + 1 < source.length) {
          const e = source[++j]!;
          if (e === 'u' && /^[0-9a-fA-F]{4}/.test(source.slice(j + 1, j + 5))) {
            text += String.fromCharCode(parseInt(source.slice(j + 1, j + 5), 16));
            j += 4;
          } else {
            text += e === 'n' ? '\n' : e === 't' ? '\t' : e === 'r' ? '\r' : e;
          }
        } else {
          text += source[j];
        }
      }
      if (j >= source.length) {
        unknown();
      }
      tokens.push({ kind: 'str', text, value: text });
      i = j + 1;
      continue;
    }
    const two = source.slice(i, i + 2);
    if (['==', '!=', '<=', '>=', '&&', '||'].includes(two)) {
      tokens.push({ kind: two, text: two });
      i += 2;
      continue;
    }
    if ('()[],.?:+-*/%!<>'.includes(c)) {
      tokens.push({ kind: c, text: c });
      i++;
      continue;
    }
    unknown();
  }
  tokens.push({ kind: 'end', text: '' });
  return tokens;
}

function fromJson(value: unknown): Value {
  if (Array.isArray(value)) return value.map(fromJson);
  if (value === null || typeof value === 'number' || typeof value === 'string' || typeof value === 'boolean') return value;
  return JSON.stringify(value);
}

function inferValue(text: string): Value {
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

function asBool(v: Value): boolean {
  if (typeof v === 'boolean') return v;
  if (Array.isArray(v)) return v.length > 0;
  if (typeof v === 'number') return v !== 0;
  if (typeof v === 'string') {
    const t = v.trim().toLowerCase();
    return t.length > 0 && t !== 'false' && t !== '0' && t !== 'no';
  }
  return false;
}

function asNumber(v: Value): number {
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
function looseEquals(a: Value, b: Value): boolean {
  if (a === null || b === null) return a === b;
  if (typeof a === 'boolean' || typeof b === 'boolean') return asBool(a) === asBool(b);
  if (typeof a === 'number' || typeof b === 'number') {
    const other = typeof a === 'number' ? b : a;
    return numeric(other) ? asNumber(a) === asNumber(b) : valueText(a) === valueText(b);
  }
  return a === b;
}

function compare(a: Value, b: Value): number {
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

/** double.ToString(pattern, InvariantCulture): the standard formats F N P C E G and custom ones like "#,0.00" / "0%". */
function formatNumber(n: number, pattern: string): string {
  if (pattern.length > 64) unknown();
  const std = /^([A-Za-z])(\d*)$/.exec(pattern);
  if (std) {
    const p = std[2] ? Number(std[2]) : undefined;
    if (p !== undefined && p > 30) unknown();
    switch (std[1]!.toUpperCase()) {
      case 'F': return n.toFixed(p ?? 2);
      case 'N': return group(n.toFixed(p ?? 2));
      case 'P': return group((n * 100).toFixed(p ?? 2)) + ' %';
      case 'C': return (n < 0 ? '-' : '') + '¤' + group(Math.abs(n).toFixed(p ?? 2));
      case 'G': return p ? String(Number(n.toPrecision(p))) : String(n);
      case 'E': {
        const [mantissa, exponent] = n.toExponential(p ?? 6).split('e');
        const e = Number(exponent);
        return `${mantissa}${std[1] === 'e' ? 'e' : 'E'}${e < 0 ? '-' : '+'}${String(Math.abs(e)).padStart(3, '0')}`;
      }
      default: return unknown(); // D and X throw on a double in .NET
    }
  }
  const m = /^([^#0,.]*)([#0,]*)(?:\.([#0]*))?([^#0,.]*)$/.exec(pattern);
  if (!m || (!m[2] && m[3] === undefined)) return unknown();
  const prefix = m[1]!.replace(/['"]/g, '');
  const suffix = m[4]!.replace(/['"]/g, '');
  const value = (prefix + suffix).includes('%') ? n * 100 : n;
  const decimals = m[3] ?? '';
  const minDecimals = decimals.replace(/#/g, '').length;
  let text = Math.abs(value).toFixed(decimals.length);
  if (decimals.length > minDecimals) {
    text = text.replace(new RegExp(`0{1,${decimals.length - minDecimals}}$`), '').replace(/\.$/, '');
  }
  const [intText, frac] = text.split('.');
  const minInt = (m[2] ?? '').replace(/[#,]/g, '').length;
  let int = intText === '0' && minInt === 0 ? '' : intText!.padStart(minInt, '0');
  if ((m[2] ?? '').includes(',')) int = group(int);
  const sign = value < 0 && /[1-9]/.test(text) ? '-' : '';
  return sign + prefix + int + (frac !== undefined ? '.' + frac : '') + suffix;
}

/** BuiltinFunctions.Money: rounded away from zero, invariant thousands separators, g suffix (1,234g). */
export function money(amount: number): string {
  const rounded = roundAway(Math.min(Math.max(amount, -1e15), 1e15), 0);
  return (rounded < 0 ? '-' : '') + group(String(Math.abs(rounded))) + 'g';
}

interface Builtin { min: number; max: number; run(a: Value[]): Value }

const fn = (min: number, max: number, run: (a: Value[]) => Value): Builtin => ({ min, max, run });

function extreme(a: Value[], pickMax: boolean): Value {
  const values = a.length === 1 && Array.isArray(a[0]) ? a[0] : a;
  if (values.length === 0) return null;
  const numbers = values.map(asNumber);
  return pickMax ? Math.max(...numbers) : Math.min(...numbers);
}

/** BuiltinFunctions.RegisterAll plus GameFunctions' itemName, by lower-case name (FunctionRegistry ignores case). */
const builtins: Record<string, Builtin> = {
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

const power: Record<string, number> = { '?': 1, '||': 2, '&&': 3, '==': 4, '!=': 4, '<': 5, '<=': 5, '>': 5, '>=': 5, '+': 6, '-': 6, '*': 7, '/': 7, '%': 7 };

class Parser {
  private pos = 0;

  constructor(private readonly tokens: Token[], private readonly state: Record<string, string>, private readonly functions: ExternalFunctions) {}

  private peek(): Token { return this.tokens[this.pos]!; }
  private next(): Token { return this.tokens[this.pos++]!; }

  parseAll(): Value {
    const v = this.expression(0);
    if (this.peek().kind !== 'end') unknown();
    return v;
  }

  private expect(kind: string): void {
    if (this.next().kind !== kind) unknown();
  }

  private expression(min: number): Value {
    let left = this.prefix();
    for (;;) {
      const op = this.peek().kind;
      const p = power[op];
      if (p === undefined || p <= min) {
        return left;
      }
      this.next();
      if (op === '?') {
        const yes = this.expression(0);
        this.expect(':');
        const no = this.expression(0);
        left = asBool(left) ? yes : no;
        continue;
      }
      const right = this.expression(p);
      left = this.binary(op, left, right);
    }
  }

  private binary(op: string, a: Value, b: Value): Value {
    switch (op) {
      case '||': return asBool(a) || asBool(b);
      case '&&': return asBool(a) && asBool(b);
      case '==': return looseEquals(a, b);
      case '!=': return !looseEquals(a, b);
      case '<': return compare(a, b) < 0;
      case '<=': return compare(a, b) <= 0;
      case '>': return compare(a, b) > 0;
      case '>=': return compare(a, b) >= 0;
      case '+': return typeof a === 'string' || typeof b === 'string' ? valueText(a) + valueText(b) : asNumber(a) + asNumber(b);
      case '-': return asNumber(a) - asNumber(b);
      case '*': return asNumber(a) * asNumber(b);
      case '/': return asNumber(b) === 0 ? 0 : asNumber(a) / asNumber(b);
      case '%': return asNumber(b) === 0 ? 0 : asNumber(a) % asNumber(b);
      default: return unknown();
    }
  }

  private prefix(): Value {
    const t = this.next();
    switch (t.kind) {
      case 'num':
      case 'str':
        return t.value!;
      case '(': {
        const v = this.expression(0);
        this.expect(')');
        return v;
      }
      case '!':
        return !asBool(this.expression(8));
      case '-':
        return -asNumber(this.expression(8));
      case 'at': {
        if (this.peek().kind === '(') {
          const args = this.arguments().map(valueText);
          const result = this.functions[t.text.toLowerCase()]?.(args);
          return result === undefined ? unknown() : result;
        }
        const path = '@' + t.text;
        return Object.prototype.hasOwnProperty.call(this.state, path) ? inferValue(this.state[path]!) : unknown();
      }
      case 'id': {
        const lower = t.text.toLowerCase();
        if (this.peek().kind === '(') {
          return this.call(lower);
        }
        if (this.peek().kind !== '.') {
          if (lower === 'true') return true;
          if (lower === 'false') return false;
          if (lower === 'null') return null;
        }
        let path = t.text;
        while (this.peek().kind === '.' && this.tokens[this.pos + 1]?.kind === 'id') {
          this.next();
          path += '.' + this.next().text;
        }
        const k = this.peek().kind;
        if (k === '(' || k === '[' || k === '.') unknown();
        return Object.prototype.hasOwnProperty.call(this.state, path) ? inferValue(this.state[path]!) : unknown();
      }
      default:
        return unknown();
    }
  }

  /** `( a, b … )` after a function name. */
  private arguments(): Value[] {
    this.next();
    const args: Value[] = [];
    if (this.peek().kind !== ')') {
      for (;;) {
        args.push(this.expression(0));
        if (this.peek().kind !== ',') break;
        this.next();
      }
    }
    this.expect(')');
    return args;
  }

  private call(name: string): Value {
    const args = this.arguments();
    const builtin = Object.prototype.hasOwnProperty.call(builtins, name) ? builtins[name]! : unknown();
    return args.length >= builtin.min && args.length <= builtin.max ? builtin.run(args) : unknown();
  }
}

/** Template.Parse segments: text, `${expr}` / `$:{expr}`, `$${` = literal `${`. */
function segments(raw: string): Array<{ text: string } | { expr: string }> {
  const out: Array<{ text: string } | { expr: string }> = [];
  let text = '';
  let i = 0;
  while (i < raw.length) {
    if (raw.startsWith('$${', i)) {
      text += '${';
      i += 3;
      continue;
    }
    const open = raw.startsWith('${', i) ? 2 : raw.startsWith('$:{', i) ? 3 : 0;
    if (!open) {
      text += raw[i];
      i++;
      continue;
    }
    let j = i + open;
    let depth = 1;
    let quote = '';
    for (; j < raw.length; j++) {
      const c = raw[j]!;
      if (quote) {
        if (c === '\\') j++;
        else if (c === quote) quote = '';
      } else if (c === '"' || c === '\'') quote = c;
      else if (c === '{') depth++;
      else if (c === '}' && --depth === 0) break;
    }
    if (text) out.push({ text });
    text = '';
    out.push({ expr: raw.substring(i + open, j) });
    i = j + 1;
  }
  if (text) out.push({ text });
  return out;
}

function evaluateBare(expr: string, state: Record<string, string>, functions: ExternalFunctions): Value {
  return new Parser(lex(expr), state, functions).parseAll();
}

/**
 * Evaluates a bare expression (`menu.count + 1`) or a template (`Day ${menu.day}`) against the preview state;
 * undefined when a name has no sample value or the text uses something this evaluator does not support.
 */
export function evaluateExpression(expr: string, state: Record<string, string>, functions: ExternalFunctions = {}): string | undefined {
  try {
    if (!hasTemplate(expr)) {
      return valueText(evaluateBare(expr, state, functions));
    }
    const parts = segments(expr);
    if (parts.length === 1 && 'expr' in parts[0]!) {
      return valueText(evaluateBare(parts[0].expr, state, functions));
    }
    return parts.map(p => ('text' in p ? p.text : valueText(evaluateBare(p.expr, state, functions)))).join('');
  } catch (e) {
    if (e instanceof Unknown) {
      return undefined;
    }
    return undefined;
  }
}

/** A text template (`Profit: ${money(row.profit)}`) with each `${…}` the preview cannot evaluate left as an expression chip. */
export function evaluateTemplateText(raw: string, state: Record<string, string>, functions: ExternalFunctions = {}): string {
  return segments(raw).map(p => {
    if ('text' in p) return p.text;
    try {
      return valueText(evaluateBare(p.expr, state, functions));
    } catch {
      return CHIP_EXPR + 'ƒ ' + p.expr + CHIP_END;
    }
  }).join('');
}

const scalar = (value: unknown): string | undefined =>
  (typeof value === 'string' ? value : typeof value === 'number' || typeof value === 'boolean' ? String(value) : undefined);

const table = (value: unknown): Record<string, unknown> =>
  (value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : {});

/**
 * The values expressions read in the preview: the menu's State defaults as menu.<key> (a one-time "$:{…}" evaluated,
 * arrays / objects as their JSON text), the preview state over them, then Computed as menu.<name> (in order, unless the
 * preview state sets that name).
 */
export function previewValues(doc: DesignerDocument, functions: ExternalFunctions = {}): Record<string, string> {
  const state: Record<string, string> = {};
  for (const [key, value] of Object.entries(table(doc.menuExtra.State))) {
    const text = scalar(value) ?? JSON.stringify(value);
    const evaluated = text.trimStart().startsWith('$:{') ? evaluateExpression(text, state, functions) : text;
    if (evaluated !== undefined) {
      state[`menu.${key.trim()}`] = evaluated;
    }
  }
  Object.assign(state, doc.previewState);
  for (const [key, value] of Object.entries(table(doc.menuExtra.Computed))) {
    const name = `menu.${key.trim()}`;
    const text = scalar(value);
    const result = text !== undefined && !(name in doc.previewState) ? evaluateExpression(text, state, functions) : undefined;
    if (result !== undefined) {
      state[name] = result;
    }
  }
  return state;
}

/** An evaluator over the document's preview values (previewValues) and a place's row locals, as LayoutOptions.evaluate expects. */
export function createEvaluator(doc: DesignerDocument, functions: ExternalFunctions = {}): (expr: string, locals: Record<string, string>) => string | undefined {
  const state = previewValues(doc, functions);
  return (expr, locals) => evaluateExpression(expr, Object.keys(locals).length > 0 ? { ...state, ...locals } : state, functions);
}

const namePattern = /\b(menu|session|player|config|stat|args|model)\.[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*/g;

function scan(value: unknown, found: Set<string>): void {
  if (typeof value === 'string') {
    for (const m of value.matchAll(namePattern)) {
      found.add(m[0]);
    }
  } else if (Array.isArray(value)) {
    value.forEach(v => scan(v, found));
  } else if (value && typeof value === 'object') {
    Object.values(value as Record<string, unknown>).forEach(v => scan(v, found));
  }
}

/** Expression names (menu.x, session.x, player.x, config.x, stat.x, args.x, model.x — objects exposed from C#) used anywhere in the document, sorted. */
export function findExpressionNames(doc: DesignerDocument): string[] {
  const found = new Set<string>();
  scan(doc.menu, found);
  scan(doc.menuExtra, found);
  // State / Computed names are menu.* values
  for (const member of ['State', 'Computed']) {
    const table = doc.menuExtra[member];
    if (table && typeof table === 'object' && !Array.isArray(table)) {
      Object.keys(table).forEach(k => found.add(`menu.${k.trim()}`));
    }
  }
  for (const node of Object.values(doc.nodes)) {
    scan(node.fields, found);
    scan(node.extra, found);
  }
  for (const template of Object.values(doc.templates)) {
    scan(template.fields, found);
    scan(template.extra, found);
    scan(template.params, found);
  }
  return [...found].sort((a, b) => a.localeCompare(b));
}

/**
 * The expression names (as findExpressionNames) the given expressions read that have no preview value, in first-use
 * order; a menu.* Computed member without a value stands for the names its own expression reads.
 */
export function unknownNames(exprs: string[], doc: DesignerDocument, functions: ExternalFunctions = {}): string[] {
  const values = previewValues(doc, functions);
  const computed = table(doc.menuExtra.Computed);
  const seen = new Set<string>();
  const unknown: string[] = [];
  const visit = (expr: string): void => {
    for (const m of expr.matchAll(namePattern)) {
      const name = m[0];
      if (seen.has(name) || name in values) continue;
      seen.add(name);
      const key = Object.keys(computed).find(k => `menu.${k.trim()}` === name);
      const source = key !== undefined && !(name in doc.previewState) ? scalar(computed[key]) : undefined;
      if (source !== undefined) {
        visit(source);
      } else {
        unknown.push(name);
      }
    }
  };
  new Set(exprs).forEach(visit);
  return unknown;
}
