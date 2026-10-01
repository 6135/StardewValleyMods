import type { FieldShape } from '../../fieldShapes';

// Literal parsing and formatting per field shape (the forms ValueParsers accepts). A value that does not parse is an
// expression (or a Content Patcher token) and is edited as text.

const boolRe = /^(true|false|yes|no|1|0)$/i;
const intRe = /^-?\d+(\.0+)?$/;
const floatRe = /^-?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?$/;
const hexRe = /^#([0-9a-f]{6}|[0-9a-f]{8})$/i;
const rgbRe = /^\s*\d+\s*,\s*\d+\s*,\s*\d+\s*(,\s*\d+\s*)?$/;
const trackRe = /^(auto|\d+(\.\d+)?|(\d+(\.\d+)?)?\*)$/i;

/** True when `value` is a literal of the shape (always true for text-like shapes). */
export function isLiteral(shape: FieldShape, value: string): boolean {
  const v = value.trim();
  switch (shape.kind) {
    case 'bool': return boolRe.test(v);
    case 'int': return intRe.test(v) || (shape.auto === true && /^(auto|null)$/i.test(v));
    case 'float': return floatRe.test(v);
    case 'enum': return enumValue(shape, v) !== undefined;
    case 'margin': return parseMargin(v) !== null;
    case 'color': return hexRe.test(v) || rgbRe.test(v) || /^[a-z]+$/i.test(v);
    case 'tracks': return v.split(',').every(t => trackRe.test(t.trim()));
    default: return true;
  }
}

export function parseBool(value: string | undefined): boolean | undefined {
  if (value === undefined || !boolRe.test(value.trim())) {
    return undefined;
  }

  return /^(true|yes|1)$/i.test(value.trim());
}

/** The canonical enum value a literal names (case-insensitive, aliases included). */
export function enumValue(shape: Extract<FieldShape, { kind: 'enum' }>, value: string): string | undefined {
  const v = value.trim().toLowerCase();
  return shape.values.find(x => x.toLowerCase() === v) ?? shape.aliases?.[v];
}

/** Margin shorthand as [left, top, right, bottom]: "all", "horizontal,vertical" or "l,t,r,b"; null when invalid. */
export function parseMargin(value: string): [number, number, number, number] | null {
  const parts = value.split(',').map(p => p.trim());
  if (!parts.every(p => /^-?\d+$/.test(p))) {
    return null;
  }

  const n = parts.map(Number);
  switch (n.length) {
    case 1: return [n[0]!, n[0]!, n[0]!, n[0]!];
    case 2: return [n[0]!, n[1]!, n[0]!, n[1]!];
    case 4: return [n[0]!, n[1]!, n[2]!, n[3]!];
    default: return null;
  }
}

/** The shortest margin shorthand for [left, top, right, bottom]. */
export function formatMargin([l, t, r, b]: readonly number[]): string {
  if (l === t && t === r && r === b) {
    return `${l}`;
  }

  return l === r && t === b ? `${l},${t}` : `${l},${t},${r},${b}`;
}

export type Track = { kind: 'auto' } | { kind: 'px'; size: number } | { kind: 'star'; weight: number };

export function parseTracks(value: string): Track[] {
  return value.split(',').map(t => t.trim()).filter(t => t.length > 0).map((t): Track => {
    if (/^auto$/i.test(t)) {
      return { kind: 'auto' };
    }

    return t.endsWith('*') ? { kind: 'star', weight: t === '*' ? 1 : Number(t.slice(0, -1)) } : { kind: 'px', size: Number(t) };
  });
}

export function formatTracks(tracks: readonly Track[]): string {
  return tracks.map(t => t.kind === 'auto' ? 'auto' : t.kind === 'px' ? `${t.size}` : t.weight === 1 ? '*' : `${t.weight}*`).join(', ');
}

let probe: CanvasRenderingContext2D | null | undefined;

/** A CSS hex color (#rrggbb) and alpha (0-255) for a color literal; null when the browser does not know the name. */
export function colorToHex(value: string): { hex: string; alpha: number } | null {
  const v = value.trim();
  if (hexRe.test(v)) {
    return { hex: v.slice(0, 7).toLowerCase(), alpha: v.length === 9 ? parseInt(v.slice(7), 16) : 255 };
  }

  if (rgbRe.test(v)) {
    const [r, g, b, a] = v.split(',').map(p => Math.max(0, Math.min(255, Number(p.trim()))));
    return { hex: '#' + [r!, g!, b!].map(c => c.toString(16).padStart(2, '0')).join(''), alpha: a ?? 255 };
  }

  if (/^[a-z]+$/i.test(v)) {
    probe ??= document.createElement('canvas').getContext('2d');
    if (probe) {
      probe.fillStyle = '#010203';
      probe.fillStyle = v;
      const resolved = String(probe.fillStyle);
      if (resolved !== '#010203' || v.toLowerCase() === 'transparent') {
        return resolved.startsWith('#') ? { hex: resolved, alpha: 255 } : { hex: '#000000', alpha: 0 };
      }
    }
  }

  return null;
}

/** A color literal for a picked #rrggbb, keeping the original's form (R,G,B[,A] or hex) and alpha. */
export function formatColor(hex: string, previous: string | undefined): string {
  const r = parseInt(hex.slice(1, 3), 16), g = parseInt(hex.slice(3, 5), 16), b = parseInt(hex.slice(5, 7), 16);
  const old = previous ? colorToHex(previous) : null;
  const alpha = old?.alpha ?? 255;
  if (previous && rgbRe.test(previous)) {
    return alpha === 255 && previous.split(',').length === 3 ? `${r},${g},${b}` : `${r},${g},${b},${alpha}`;
  }

  return alpha === 255 ? hex.toUpperCase() : `${hex}${alpha.toString(16).padStart(2, '0')}`.toUpperCase();
}
