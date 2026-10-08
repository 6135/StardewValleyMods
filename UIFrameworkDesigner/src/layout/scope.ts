// Scope and value resolution for the layout builder (build.ts), after ExpressionValueResolver.Resolve: template
// arguments, row locals, typed / text fields evaluated through LayoutOptions.evaluate.
import type { NodeId } from '../model/document';
import { isRecord, scalarText, type Src } from './source';
import { CHIP_END, CHIP_EXPR, CHIP_I18N, CHIP_TOKEN, type LayoutOptions } from './types';
import { hasTemplate, parseBool, parseInt32, parseNumber, parseOptionalInt } from './values';

export interface Outlets {
  routes: Map<string, Src[]>;
  taken: Set<string>;
  scope: Scope;
}

export interface Scope {
  args: Record<string, string> | null;
  /** Row locals (RowScope.For: row, index, the As alias and <alias>Index) as JSON values, for nested sources. */
  locals: Record<string, unknown>;
  /** `locals` flattened to the dotted scalar names the evaluator reads (row.price, season.name …). */
  vars: Record<string, string>;
  instance: number;
  ownerId?: NodeId;
  hidden: boolean;
  outlets: Outlets | null;
  depth: number;
}

type Segment = { text: string } | { expr: string };

/** Template.Parse segments: literal text, `${expr}` and `$:{expr}` (braces and quoted strings nest), `$${` = literal `${`. */
function scanTemplate(raw: string): Segment[] {
  const out: Segment[] = [];
  let text = '';
  let i = 0;
  while (i < raw.length) {
    if (raw.startsWith('$${', i)) {
      text += '${';
      i += 3;
      continue;
    }
    const open = raw.startsWith('${', i) ? 2 : raw.startsWith('$:{', i) ? 3 : 0;
    if (open === 0) {
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
        if (c === '\\') {
          j++;
        } else if (c === quote) {
          quote = '';
        }
      } else if (c === '"' || c === '\'') {
        quote = c;
      } else if (c === '{') {
        depth++;
      } else if (c === '}' && --depth === 0) {
        break;
      }
    }
    if (text) {
      out.push({ text });
      text = '';
    }
    out.push({ expr: raw.substring(i + open, j).trim() });
    i = j + 1;
  }
  if (text) {
    out.push({ text });
  }
  return out;
}

const argPattern = /\bargs\.([A-Za-z_][A-Za-z0-9_]*)/g;

function argLiteral(value: string): string {
  if (parseNumber(value) !== undefined || /^(true|false|null)$/i.test(value.trim())) {
    return value.trim();
  }
  return `'${value.replace(/\\/g, '\\\\').replace(/'/g, '\\\'')}'`;
}

/** An argument by name: exact first, then case-insensitively (CompositeArgs.TryGetRaw). */
function argValue(args: Record<string, string>, name: string): string | undefined {
  if (name in args) {
    return args[name];
  }
  const key = Object.keys(args).find(k => k.toLowerCase() === name.toLowerCase());
  return key === undefined ? undefined : args[key];
}

/** Template arguments: `${args.x}` segments become the argument text, `args.x` inside expressions a literal. */
export function substituteArgs(raw: string, args: Record<string, string> | null, bare: boolean): string {
  if (!args || !raw.includes('args.')) {
    return raw;
  }
  const inExpr = (expr: string): string => expr.replace(argPattern, (m, name: string) => {
    const value = argValue(args, name);
    return value === undefined ? m : argLiteral(value);
  });
  if (!hasTemplate(raw)) {
    return bare ? inExpr(raw) : raw;
  }
  return scanTemplate(raw).map(s => {
    if ('text' in s) {
      return s.text.split('${').join('$${');
    }
    const whole = /^args\.([A-Za-z_][A-Za-z0-9_]*)$/.exec(s.expr);
    const value = whole ? argValue(args, whole[1]!) : undefined;
    if (value !== undefined) {
      return value.split('${').join('$${');
    }
    return '${' + inExpr(s.expr) + '}';
  }).join('');
}

/** CP tokens in text as chips: `{{i18n:key}}` → an i18n chip with the key, other `{{Token}}` → a token chip. */
export function chipTokens(text: string): string {
  return text.replace(/\{\{\s*([^{}]*?)\s*\}\}/g, (_m, inner: string) => {
    const i18n = /^i18n\s*:\s*(.+)$/i.exec(inner);
    return i18n ? CHIP_I18N + i18n[1]!.trim() + CHIP_END : CHIP_TOKEN + inner + CHIP_END;
  });
}

/**
 * The key of a collection source C# provides (a `hook:name` / `@name` shorthand, or a definition with a Hook), under
 * which the preview keeps its sample rows (LayoutOptions.sampleRows); undefined for any other source.
 */
export function sourceKey(source: unknown): string | undefined {
  if (isRecord(source)) {
    return typeof source.Hook === 'string' && source.Hook.trim().length > 0 ? `hook:${source.Hook.trim()}` : undefined;
  }
  if (typeof source !== 'string') {
    return undefined;
  }
  const text = source.trim();
  const name = (text.startsWith('${') && text.endsWith('}') ? text.slice(2, -1) : text).trim();
  return /^(hook:|@)\S/i.test(name) ? name : undefined;
}

/** Rich-text markup ([b], [color=…], [link=…], [icon=…]) removed; an icon keeps roughly one glyph pair of room. */
function stripMarkup(text: string): string {
  return text.replace(/\[(\/?)(b|color|link|icon)(=[^\]]*)?\]/gi, (_m, close: string, tag: string) =>
    (!close && tag.toLowerCase() === 'icon' ? '  ' : ''));
}

const truthy = (v: string): boolean => parseBool(v) ?? (v.length > 0 && parseNumber(v) !== 0 && v.toLowerCase() !== 'false');

export class Values {
  /** While `collect` runs: where the expressions that could not be evaluated go. */
  private failed: string[] | null = null;

  constructor(private readonly opts: LayoutOptions) {}

  evaluate(expr: string, scope: Scope): string | undefined {
    const v = this.opts.evaluate?.(expr, scope.vars);
    if (v === undefined && this.failed) {
      this.failed.push(expr);
    }
    return v;
  }

  /** Runs `read`, adding each expression it could not evaluate to `into`. */
  collect<T>(into: string[], read: () => T): T {
    const outer = this.failed;
    this.failed = into;
    try {
      return read();
    } finally {
      this.failed = outer;
    }
  }

  /**
   * A typed field: a literal when it parses, else (bare-expression kinds) an expression evaluated through
   * opts.evaluate; undefined (unset, the default applies) when it cannot be resolved.
   */
  typed<T>(raw: string | undefined, scope: Scope, parse: (s: string) => T | undefined, bare = true): T | undefined {
    if (raw === undefined) {
      return undefined;
    }
    const r = substituteArgs(raw, scope.args, bare);
    if (!hasTemplate(r)) {
      const literal = parse(r.split('$${').join('${'));
      if (literal !== undefined || !bare) {
        return literal;
      }
    }
    const v = this.evaluate(r, scope);
    return v === undefined ? undefined : parse(v);
  }

  bool(raw: string | undefined, scope: Scope): boolean | undefined {
    if (raw === undefined) {
      return undefined;
    }
    const r = substituteArgs(raw, scope.args, true);
    if (!hasTemplate(r)) {
      const literal = parseBool(r);
      if (literal !== undefined) {
        return literal;
      }
    }
    const v = this.evaluate(r, scope);
    return v === undefined ? undefined : truthy(v);
  }

  int(raw: string | undefined, scope: Scope): number | undefined {
    return this.typed(raw, scope, s => parseInt32(s) ?? (parseNumber(s) !== undefined ? Math.round(parseNumber(s)!) : undefined));
  }

  num(raw: string | undefined, scope: Scope): number | undefined {
    return this.typed(raw, scope, parseNumber);
  }

  /** OptionalInt: null for 'auto' / empty, undefined when unresolved. */
  optInt(raw: string | undefined, scope: Scope): number | null | undefined {
    return this.typed(raw, scope, s => {
      const v = parseOptionalInt(s);
      return v === undefined && parseNumber(s) !== undefined ? Math.round(parseNumber(s)!) : v;
    });
  }

  /** Visible / If / Condition: hidden only by a literal false or an expression that evaluates to 'false'. */
  hides(raw: string | undefined, scope: Scope): boolean {
    if (raw === undefined) {
      return false;
    }
    const r = substituteArgs(raw, scope.args, true);
    if (!hasTemplate(r) && parseBool(r) !== undefined) {
      return parseBool(r) === false;
    }
    return this.evaluate(r, scope)?.trim().toLowerCase() === 'false';
  }

  /** A text field as drawn: evaluated when possible, else literal text with token / expression chips. */
  text(raw: string | undefined, scope: Scope, rich = false): string {
    if (raw === undefined) {
      return '';
    }
    const r = substituteArgs(raw, scope.args, false);
    let out: string;
    if (hasTemplate(r)) {
      const v = this.evaluate(r, scope);
      out = v !== undefined
        ? chipTokens(v)
        : scanTemplate(r).map(s => ('text' in s ? chipTokens(s.text) : CHIP_EXPR + 'ƒ ' + s.expr + CHIP_END)).join('');
    } else {
      out = chipTokens(r.split('$${').join('${'));
    }
    return rich ? stripMarkup(out) : out;
  }
}

/** Row locals as the dotted scalar names the evaluator reads: { row: { price: 35 } } → row.price = "35". */
export function flattenLocals(locals: Record<string, unknown>): Record<string, string> {
  const out: Record<string, string> = {};
  const walk = (name: string, value: unknown): void => {
    // null as the text the evaluator reads back as null (row.item != null)
    const text = value === null ? 'null' : scalarText(value);
    if (text !== undefined) {
      out[name] = text;
    } else if (isRecord(value)) {
      for (const [k, v] of Object.entries(value)) {
        walk(`${name}.${k}`, v);
      }
    }
  };
  for (const [k, v] of Object.entries(locals)) {
    walk(k, v);
  }
  return out;
}
