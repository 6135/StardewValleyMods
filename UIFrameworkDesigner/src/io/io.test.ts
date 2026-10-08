import { afterEach, describe, expect, it, vi } from 'vitest';
import { buildMenuObject, exportJson } from './export';
import { importText } from './import';
import { exportWorkspace, parseWorkspace, serializeWorkspace } from './workspace';
import { shareLink, takeSharedWorkspace } from './share';
import { defaultJsonExportOptions } from '../model/document';
import { createEmptyDocument } from '../model/factory';
import { createWorkspace, menuTab } from '../model/workspace';

const fixtures = Object.entries(import.meta.glob<string>('../../fixtures/*.json', { query: '?raw', import: 'default', eager: true })).map(([name, text]) => ({ name, text }));

const errorsOf = (problems: { severity: string }[]) => problems.filter(p => p.severity === 'error');

for (const { name, text } of fixtures) describe(`fixture ${name}`, () => {
  it('imports without errors', () => {
    const result = importText(text);
    expect(errorsOf(result.problems)).toEqual([]);
    expect(result.workspace).not.toBeNull();
    expect(result.candidates.length).toBeGreaterThan(0);
  });

  it('round-trips through the workspace file', () => {
    const ws = parseWorkspace(text).workspace!;
    const once = serializeWorkspace(ws);
    const again = parseWorkspace(once);
    expect(errorsOf(again.problems)).toEqual([]);
    expect(serializeWorkspace(again.workspace!)).toBe(once);
  });

  it('round-trips through the Content Patcher export', () => {
    const ws = parseWorkspace(text).workspace!;
    const json = exportWorkspace(ws, 'contentJson', defaultJsonExportOptions);
    const again = importText(json);
    expect(errorsOf(again.problems)).toEqual([]);
    expect(exportWorkspace(again.workspace!, 'contentJson', defaultJsonExportOptions)).toBe(json);
  });
});

describe('importText', () => {
  it('reads JSONC with comments and trailing commas', () => {
    const r = importText('{ /* c */ "Menus": { "mod/a": { "Title": "T", "Children": [ { "Label": "hi", }, ], }, }, }');
    expect(r.hadComments).toBe(true);
    expect(r.problems).toEqual([]);
    expect(r.candidates).toHaveLength(1);
    expect(r.candidates[0]!.document.menu['Title']).toBe('T');
  });

  it('reports syntax errors with line and column', () => {
    const r = importText('{\n  "Menus": { "mod/a": { "Title": } }\n}');
    expect(r.problems.some(p => p.severity === 'error' && /line 2, column/.test(p.message))).toBe(true);
  });

  it('reports text without a menu', () => {
    const r = importText('[]');
    expect(r.workspace).toBeNull();
    expect(r.problems.some(p => /no menu found/.test(p.message))).toBe(true);
  });
});

describe('export', () => {
  it('writes a Menus entry keyed owner/menuId', () => {
    const doc = createEmptyDocument();
    const out = JSON.parse(exportJson(doc, { ...defaultJsonExportOptions, shape: 'entry' })) as Record<string, unknown>;
    expect(Object.keys(out)).toEqual(['{{ModId}}/menu']);
    expect(buildMenuObject(doc, { collapseShorthands: true, omitDefaults: true }).value['Title']).toBe('New menu');
  });

  it('writes a CP EditData patch targeting the Menus asset', () => {
    const out = JSON.parse(exportJson(createEmptyDocument(), { ...defaultJsonExportOptions, shape: 'cpPatch' })) as Record<string, unknown>;
    expect(out['Action']).toBe('EditData');
    expect(out['Target']).toBe('Mods/6135.UIFramework/Menus');
  });
});

describe('share links', () => {
  afterEach(() => vi.unstubAllGlobals());

  function stubWindow(hash: string) {
    const location = { origin: 'https://x.test', pathname: '/designer/', search: '', hash };
    const replaceState = vi.fn(() => { location.hash = ''; });
    vi.stubGlobal('window', { location, history: { replaceState } });
    return { location, replaceState };
  }

  it('round-trips a workspace through the URL hash', () => {
    const ws = createWorkspace([menuTab(createEmptyDocument())]);
    const w = stubWindow('');
    const link = shareLink(ws);
    expect(link.startsWith('https://x.test/designer/#w=')).toBe(true);
    expect(link).not.toMatch(/[ "{}]/);

    w.location.hash = link.slice(link.indexOf('#'));
    const back = takeSharedWorkspace();
    expect(back).not.toBeNull();
    expect(serializeWorkspace(back!)).toBe(serializeWorkspace(parseWorkspace(serializeWorkspace(ws)).workspace!));
    expect(w.replaceState).toHaveBeenCalledWith(null, '', '/designer/');
  });

  it('ignores a hash that is not a share link', () => {
    const w = stubWindow('#other');
    expect(takeSharedWorkspace()).toBeNull();
    expect(w.replaceState).not.toHaveBeenCalled();
  });

  it('returns null for an empty or corrupt share hash', () => {
    stubWindow('#w=');
    expect(takeSharedWorkspace()).toBeNull();
    stubWindow('#w=!!!notlz!!!');
    expect(takeSharedWorkspace()).toBeNull();
  });
});
