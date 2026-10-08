// The builder as the per-type creators (buildCreate.ts, buildData.ts) see it, and the data defaults they fall back on.
import type { NodeId } from '../model/document';
import { defaultOf } from '../model/metadata';
import type { ElementInfo, LayoutContext, LContainer, LElement, LStack } from './elements';
import type { Scope, Values } from './scope';
import type { Src } from './source';
import type { LayoutOptions } from './types';
import { parseBool, parseNumber } from './values';

/** What a creator uses of the builder (build.ts Builder). */
export interface BuildHost {
  readonly resolve: Values;
  readonly ctx: LayoutContext;
  readonly opts: LayoutOptions;
  synthetic(nodeId: NodeId, kind: string, scope: Scope, label?: string): ElementInfo;
  buildChildren(parent: LContainer, children: Src[], scope: Scope): void;
  buildOne(parent: LContainer, src: Src, scope: Scope): void;
  /** IUIStack members (ApplyTypeMembers): Horizontal, Spacing, Alignment, Wrap. */
  applyStack(stack: LStack, src: Src, scope: Scope): void;
  buildOutlet(placeholder: LStack, src: Src, scope: Scope): void;
  selectPage(src: Src, scope: Scope): Src | undefined;
  inlineRows(source: unknown, scope: Scope): unknown[] | null;
  rowScope(scope: Scope, rows: unknown[] | null, index: number, as: string | undefined): Scope;
  rowTemplate(src: Src): Src[];
  gridColumns(src: Src): string | undefined;
  styleField(src: Src, name: string): string | undefined;
  font(src: Src, scope: Scope, own: boolean): 'small' | 'dialogue' | 'tiny';
  sourcedChoices(src: Src, scope: Scope): { values: string[]; labels: string[] } | null;
  stringList(src: Src, name: string): string[];
}

/** Creates the element of a built-in type; `info` makes its ElementInfo with an optional label. */
export type Creator = (b: BuildHost, src: Src, type: string, scope: Scope, info: (label?: string) => ElementInfo) => LElement;

export function defNum(type: string, field: string, fallback: number): number {
  const v = defaultOf(type, field);
  const n = v === undefined ? undefined : parseNumber(v);
  return n ?? fallback;
}

export function defBool(type: string, field: string, fallback: boolean): boolean {
  const v = defaultOf(type, field);
  return (v === undefined ? undefined : parseBool(v)) ?? fallback;
}

export function defText(type: string, field: string, fallback: string): string {
  return defaultOf(type, field) ?? fallback;
}
