// Port of the literal parsers in StardewUIFramework/Data/Building/ValueParsers.cs and the literal-vs-expression rule
// of ExpressionValueResolver.Resolve / HasTemplate. A field is a literal when it has no `${` / `$:{` segment and parses
// as its kind; kinds that allow a bare expression (bool, int, number, optional int) treat any other text as one.
import type { Align } from './engine';
import type { FontName } from './types';

/** ExpressionValueResolver.HasTemplate: a `${` or `$:{` segment (an escaped `$${` alone is not one). */
export function hasTemplate(raw: string): boolean {
  if (!raw.includes('$')) {
    return false;
  }
  const unescaped = raw.split('$${').join('');
  return unescaped.includes('${') || unescaped.includes('$:{');
}

/** ValueParsers.TryParseBool. */
export function parseBool(text: string): boolean | undefined {
  switch (text.trim().toLowerCase()) {
    case 'true': case 'yes': case '1': return true;
    case 'false': case 'no': case '0': return false;
    default: return undefined;
  }
}

const numberPattern = /^[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?$/;

/** ValueParsers.TryParseDouble (invariant culture, finite). */
export function parseNumber(text: string): number | undefined {
  const t = text.trim();
  if (!numberPattern.test(t)) {
    return undefined;
  }
  const n = Number(t);
  return Number.isFinite(n) ? n : undefined;
}

/** ValueParsers.TryParseInt: an integer, or a float literal with no fraction ("12.0"). */
export function parseInt32(text: string): number | undefined {
  const t = text.trim();
  if (/^[+-]?\d+$/.test(t)) {
    const n = Number(t);
    return n >= -2147483648 && n <= 2147483647 ? n : undefined;
  }
  const d = parseNumber(t);
  if (d !== undefined && Math.abs(d - Math.round(d)) < 1e-9 && Math.abs(d) <= 2147483647) {
    return Math.round(d);
  }
  return undefined;
}

/** ValueParsers.OptionalInt: '' / 'auto' / 'null' → null (unset), else an int. undefined = not a literal. */
export function parseOptionalInt(text: string): number | null | undefined {
  const t = text.trim();
  if (t.length === 0 || t.toLowerCase() === 'auto' || t.toLowerCase() === 'null') {
    return null;
  }
  return parseInt32(t);
}

function parseInts(text: string): number[] | undefined {
  const parts = text.split(',').map(parseInt32);
  return parts.every(p => p !== undefined) ? parts as number[] : undefined;
}

/** ValueParsers.Margin: 'all', 'horizontal,vertical' or 'left,top,right,bottom' → [l, t, r, b]. */
export function parseMargin(text: string): [number, number, number, number] | undefined {
  const p = parseInts(text);
  if (!p) {
    return undefined;
  }
  switch (p.length) {
    case 1: return [p[0]!, p[0]!, p[0]!, p[0]!];
    case 2: return [p[0]!, p[1]!, p[0]!, p[1]!];
    case 4: return [p[0]!, p[1]!, p[2]!, p[3]!];
    default: return undefined;
  }
}

/** ValueParsers.Pair: 'a,b'. */
export function parsePair(text: string): [number, number] | undefined {
  const p = parseInts(text);
  return p && p.length === 2 ? [p[0]!, p[1]!] : undefined;
}

/** ThemeData.ParseRectangle: 'x,y,width,height'. */
export function parseRect(text: string): [number, number, number, number] | undefined {
  const p = parseInts(text);
  return p && p.length === 4 ? [p[0]!, p[1]!, p[2]!, p[3]!] : undefined;
}

function enumName(text: string): string | undefined {
  const t = text.trim();
  // TryParseEnum refuses numeric text
  return t.length > 0 && !/^[0-9-]/.test(t) ? t.toLowerCase() : undefined;
}

/** ValueParsers.Align (Left/Top → Start, Right/Bottom → End, Middle/Centre → Center). */
export function parseAlign(text: string): Align | undefined {
  const t = enumName(text);
  switch (t) {
    case 'left': case 'top': case 'start': return 'start';
    case 'right': case 'bottom': case 'end': return 'end';
    case 'middle': case 'centre': case 'center': return 'center';
    case 'stretch': return 'stretch';
    default: return undefined;
  }
}

/** ValueParsers.Font (UIFont: Small, Dialogue, Tiny). */
export function parseFont(text: string): FontName | undefined {
  const t = enumName(text);
  return t === 'small' || t === 'dialogue' || t === 'tiny' ? t : undefined;
}

export type Anchor = 'center' | 'topleft' | 'topcenter' | 'topright' | 'middleleft' | 'middleright' | 'bottomleft' | 'bottomcenter' | 'bottomright' | 'explicit';

const anchors = new Set<string>(['center', 'topleft', 'topcenter', 'topright', 'middleleft', 'middleright', 'bottomleft', 'bottomcenter', 'bottomright', 'explicit']);

/** ValueParsers.Anchor (UIAnchor). */
export function parseAnchor(text: string): Anchor | undefined {
  const t = enumName(text);
  return t !== undefined && anchors.has(t) ? t as Anchor : undefined;
}

/**
 * A CSS color for a literal color field (RichText.TryParseColor: #RRGGBB, #RRGGBBAA, R,G,B[,A] or an XNA color name,
 * which CSS knows by the same names); undefined when it is not one.
 */
export function cssColor(text: string): string | undefined {
  const t = text.trim();
  if (/^#([0-9a-f]{6}|[0-9a-f]{8})$/i.test(t)) {
    return t;
  }
  const p = parseInts(t);
  if (p && (p.length === 3 || p.length === 4)) {
    const c = p.map(v => Math.min(255, Math.max(0, v)));
    return p.length === 4 ? `rgba(${c[0]}, ${c[1]}, ${c[2]}, ${c[3]! / 255})` : `rgb(${c[0]}, ${c[1]}, ${c[2]})`;
  }
  return /^[a-z]+$/i.test(t) ? t.toLowerCase() : undefined;
}
