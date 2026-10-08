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
import { asBool, asNumber, builtins, compare, inferValue, looseEquals, unknown, Unknown, valueText, type Value } from './evalBuiltins';

export { money, valueText } from './evalBuiltins';

/**
 * Stand-ins for functions C# registers (RegisterFunction: string arguments, a string result), by lower-case name;
 * undefined when the call cannot be evaluated.
 */
export type ExternalFunctions = Record<string, (args: string[]) => string | undefined>;

interface Token { kind: string; text: string; value?: Value }

const escapes: Record<string, string> = { n: '\n', t: '\t', r: '\r' };

/** A '…' / "…" literal opening at `start` with its \n \t \r \uXXXX escapes; `end` is the index after its closing quote. */
function readString(source: string, start: number): { text: string; end: number } {
  const quote = source[start];
  let j = start + 1;
  let text = '';
  for (; j < source.length && source[j] !== quote; j++) {
    if (source[j] === '\\' && j + 1 < source.length) {
      const e = source[++j]!;
      if (e === 'u' && /^[0-9a-fA-F]{4}/.test(source.slice(j + 1, j + 5))) {
        text += String.fromCharCode(parseInt(source.slice(j + 1, j + 5), 16));
        j += 4;
      } else {
        text += escapes[e] ?? e;
      }
    } else {
      text += source[j];
    }
  }
  if (j >= source.length) {
    unknown();
  }
  return { text, end: j + 1 };
}

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
      const { text, end } = readString(source, i);
      tokens.push({ kind: 'str', text, value: text });
      i = end;
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
          const fn = this.functions[t.text.toLowerCase()];
          const result = fn ? fn(args) : undefined;
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
