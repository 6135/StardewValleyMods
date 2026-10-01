// Port of StardewUIFramework/Core/LayoutEngine.cs (GridTrack, AlignOffset, ParseTracks, ResolveTracks,
// DistributeWidth, SpanSize, TrackOffset) plus the C# numeric conversions the layout relies on.

export type Align = 'start' | 'center' | 'end' | 'stretch';

export interface GridTrack { type: 'auto' | 'px' | 'star'; value: number }

/** C# `(int)x` for a float: truncation toward zero; non-finite values give int.MinValue like the x64 conversion. */
export function toInt(x: number): number {
  return Number.isFinite(x) ? Math.trunc(x) : -2147483648;
}

/** C# `Math.Round(x)`: midpoint to even (banker's rounding). */
export function roundEven(x: number): number {
  const r = Math.round(x);
  return Math.abs(x % 1) === 0.5 && r % 2 !== 0 ? r - 1 : r;
}

/** True for float.IsInfinity || float.IsNaN. */
export function unbounded(x: number): boolean {
  return !Number.isFinite(x);
}

/** LayoutEngine.AlignOffset (integer division as in C#). */
export function alignOffset(align: Align, available: number, size: number): number {
  switch (align) {
    case 'center': return Math.max(0, Math.trunc((available - size) / 2));
    case 'end': return Math.max(0, available - size);
    default: return 0;
  }
}

function parseFloatOr(text: string, fallback: number): number {
  const t = text.trim();
  if (!/^[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?$/.test(t)) {
    return fallback;
  }
  return Number(t);
}

/** LayoutEngine.ParseTracks: `auto`, `120px`, `120`, `*`, `2*`; empty → one star track; unknown tokens → 0px. */
export function parseTracks(definition: string | null | undefined): GridTrack[] {
  const tracks: GridTrack[] = [];
  if (!definition || definition.trim().length === 0) {
    return [{ type: 'star', value: 1 }];
  }
  for (const raw of definition.split(',')) {
    const token = raw.trim().toLowerCase();
    if (token.length === 0) {
      continue;
    }
    if (token === 'auto') {
      tracks.push({ type: 'auto', value: 0 });
    } else if (token.endsWith('*')) {
      const weightText = token.slice(0, -1);
      const weight = weightText.length === 0 ? 1 : parseFloatOr(weightText, 1);
      tracks.push({ type: 'star', value: Math.max(0, weight) });
    } else {
      const pxText = token.endsWith('px') ? token.slice(0, -2) : token;
      tracks.push({ type: 'px', value: Math.max(0, parseFloatOr(pxText, 0)) });
    }
  }
  if (tracks.length === 0) {
    tracks.push({ type: 'star', value: 1 });
  }
  return tracks;
}

/** LayoutEngine.ResolveTracks. */
export function resolveTracks(tracks: readonly GridTrack[], autoSizes: readonly number[], available: number): number[] {
  const sizes = new Array<number>(tracks.length).fill(0);
  let used = 0;
  let starTotal = 0;
  tracks.forEach((t, i) => {
    if (t.type === 'px') {
      sizes[i] = t.value;
      used += t.value;
    } else if (t.type === 'auto') {
      sizes[i] = autoSizes[i] ?? 0;
      used += sizes[i]!;
    } else {
      starTotal += t.value;
    }
  });
  const remaining = available - used;
  const infinite = unbounded(available);
  tracks.forEach((t, i) => {
    if (t.type !== 'star') {
      return;
    }
    if (infinite || starTotal <= 0) {
      sizes[i] = autoSizes[i] ?? 0;
    } else if (remaining <= 0) {
      sizes[i] = 0;
    } else {
      sizes[i] = remaining * (t.value / starTotal);
    }
  });
  return sizes;
}

/** LayoutEngine.DistributeWidth: minimums first, then the rest shared by slack. Returns the widths. */
export function distributeWidth(min: readonly number[], natural: readonly number[], available: number): number[] {
  let minTotal = 0;
  let naturalTotal = 0;
  for (let i = 0; i < min.length; i++) {
    const low = Math.max(0, min[i]!);
    minTotal += low;
    naturalTotal += Math.max(low, natural[i]!);
  }
  const extra = available - minTotal;
  const slackTotal = naturalTotal - minTotal;
  const share = unbounded(available) || available >= naturalTotal ? 1
    : extra <= 0 || slackTotal <= 0 ? 0
    : extra / slackTotal;
  const result: number[] = [];
  for (let i = 0; i < min.length; i++) {
    const low = Math.max(0, min[i]!);
    const high = Math.max(low, natural[i]!);
    result.push(share >= 1 ? high : low + ((high - low) * share));
  }
  return result;
}

/** LayoutEngine.SpanSize: tracks [start, start + span) plus the spacing between them. */
export function spanSize(sizes: readonly number[], start: number, span: number, spacing: number): number {
  let total = 0;
  const end = Math.min(sizes.length, start + span);
  for (let i = start; i < end; i++) {
    total += sizes[i]!;
  }
  return total + (Math.max(0, end - start - 1) * spacing);
}

/** LayoutEngine.TrackOffset: offset of track `index`, spacing included. */
export function trackOffset(sizes: readonly number[], index: number, spacing: number): number {
  let offset = 0;
  for (let i = 0; i < index && i < sizes.length; i++) {
    offset += sizes[i]! + spacing;
  }
  return offset;
}
