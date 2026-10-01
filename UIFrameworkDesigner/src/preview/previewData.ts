// Workspace preview data (architecture.md §7.2): stand-ins for the functions C# registers (`@name(…)`) and sample rows
// for the collection sources C# provides (`hook:name`, `@name`), which the browser cannot run. Found by scanning every
// tab of the workspace; the choices live in Workspace.previewData (autosaved and shared, never exported).
import { sourceKey } from '../layout';
import type { PreviewData, Workspace } from '../model/workspace';
import type { ExternalFunctions } from './evaluate';

/** A `@name(` call; group 2 is set when its only argument is a string literal. */
const callPattern = /@([A-Za-z0-9_.-]+(?:\/[A-Za-z0-9_-]+)?)\s*\((\s*(?:'(?:[^'\\]|\\.)*'|"(?:[^"\\]|\\.)*")\s*\))?/g;
const sourceMembers = new Set(['source', 'repeat', 'choicessource']);

/** Every value of every tab's document (preview state excluded), with the member name it is held in. */
function walk(ws: Workspace, visit: (value: unknown, member: string) => void): void {
  const go = (value: unknown, member: string): void => {
    visit(value, member);
    if (Array.isArray(value)) {
      value.forEach(v => go(v, member));
    } else if (value !== null && typeof value === 'object') {
      for (const [k, v] of Object.entries(value)) {
        if (k !== 'previewState') {
          go(v, k);
        }
      }
    }
  };
  ws.tabs.forEach(t => go(t.doc, ''));
}

export interface FunctionUse {
  /** As first written. */
  name: string;
  /** Every call passes one string literal (`@t('key')`). */
  singleLiteral: boolean;
}

/** The `@name(…)` functions the workspace calls, sorted by name (names match without case). */
export function findPreviewFunctions(ws: Workspace): FunctionUse[] {
  const found = new Map<string, FunctionUse>();
  walk(ws, value => {
    if (typeof value !== 'string' || !value.includes('@')) {
      return;
    }
    for (const m of value.matchAll(callPattern)) {
      const key = m[1]!.toLowerCase();
      const use = found.get(key);
      const single = m[2] !== undefined;
      found.set(key, use ? { ...use, singleLiteral: use.singleLiteral && single } : { name: m[1]!, singleLiteral: single });
    }
  });
  return [...found.values()].sort((a, b) => a.name.localeCompare(b.name));
}

/** The source keys (layout sourceKey) of the C# collection sources the workspace reads, sorted. */
export function findSampleSources(ws: Workspace): string[] {
  const found = new Map<string, string>();
  walk(ws, (value, member) => {
    const key = sourceMembers.has(member.toLowerCase()) || (value !== null && typeof value === 'object' && !Array.isArray(value)) ? sourceKey(value) : undefined;
    if (key !== undefined && !found.has(key.toLowerCase())) {
      found.set(key.toLowerCase(), key);
    }
  });
  return [...found.values()].sort((a, b) => a.localeCompare(b));
}

/** The evaluator's stand-ins for the workspace's preview functions ('unknown' ones are left out: their calls stay chips). */
export function externalFunctions(data: PreviewData | undefined, i18n: Record<string, string> | null): ExternalFunctions {
  const out: ExternalFunctions = {};
  for (const [name, fn] of Object.entries(data?.functions ?? {})) {
    switch (fn.kind) {
      case 'i18n':
        out[name.toLowerCase()] = args => (args[0] === undefined ? undefined : i18n?.[args[0]] ?? args[0]);
        break;
      case 'first':
        out[name.toLowerCase()] = args => args[0] ?? '';
        break;
      case 'fixed':
        out[name.toLowerCase()] = () => fn.value ?? '';
        break;
      case 'unknown':
        break;
    }
  }
  return out;
}
