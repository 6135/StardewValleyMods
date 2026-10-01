// A tiny, safe evaluator for preview sample values (architecture.md §7.2): no eval / Function. It follows the operator
// set and precedence of StardewUIFramework/Data/Expressions/Lexer.cs + Parser.cs (ternary < || < && < == != <
// < <= > >= < + - < * / % < unary ! -), their literals (numbers, '…' / "…" strings with \n \t \r \uXXXX escapes,
// true / false / null) and DataValue's conversions (+ concatenates when a side is text, / and % by 0 give 0, loose ==).
// Dotted paths (menu.count) are looked up whole in the preview state; anything else (calls, indexing, unknown names)
// makes the result undefined.
import type { DesignerDocument } from '../model/document';
import { hasTemplate } from '../layout';

type Value = number | string | boolean | null;

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

function inferValue(text: string): Value {
  const t = text.trim();
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

const power: Record<string, number> = { '?': 1, '||': 2, '&&': 3, '==': 4, '!=': 4, '<': 5, '<=': 5, '>': 5, '>=': 5, '+': 6, '-': 6, '*': 7, '/': 7, '%': 7 };

class Parser {
  private pos = 0;

  constructor(private readonly tokens: Token[], private readonly state: Record<string, string>) {}

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
      case 'id': {
        const lower = t.text.toLowerCase();
        if (this.peek().kind !== '.' && this.peek().kind !== '(') {
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

function evaluateBare(expr: string, state: Record<string, string>): Value {
  return new Parser(lex(expr), state).parseAll();
}

/**
 * Evaluates a bare expression (`menu.count + 1`) or a template (`Day ${menu.day}`) against the preview state;
 * undefined when a name has no sample value or the text uses something this evaluator does not support.
 */
export function evaluateExpression(expr: string, state: Record<string, string>): string | undefined {
  try {
    if (!hasTemplate(expr)) {
      return valueText(evaluateBare(expr, state));
    }
    const parts = segments(expr);
    if (parts.length === 1 && 'expr' in parts[0]!) {
      return valueText(evaluateBare(parts[0].expr, state));
    }
    return parts.map(p => ('text' in p ? p.text : valueText(evaluateBare(p.expr, state)))).join('');
  } catch (e) {
    if (e instanceof Unknown) {
      return undefined;
    }
    return undefined;
  }
}

/** An evaluator bound to the preview state, as LayoutOptions.evaluate expects. */
export function createEvaluator(state: Record<string, string>): (expr: string) => string | undefined {
  return expr => evaluateExpression(expr, state);
}

const namePattern = /\b(menu|session|player|config|stat|args)\.[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*/g;

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

/** Expression names (menu.x, session.x, player.x, config.x, stat.x, args.x) used anywhere in the document, sorted. */
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
