import { describe, expect, it } from 'vitest';
import { checkSchema, frameworkPath } from './schema';
import { validate } from './index';
import { importText } from '../io/import';

describe('frameworkPath', () => {
  const menu = { Children: [{ Id: 'a' }, { Label: 'x', Children: [{ Spacing: '1' }] }] };

  it('formats JSON pointers in the framework path format', () => {
    expect(frameworkPath(menu, '')).toBe('');
    expect(frameworkPath(menu, '/Children/0')).toBe('Children[0](#a)');
    expect(frameworkPath(menu, '/Children/1/Children/0/Spacing')).toBe('Children[1].Children[0].Spacing');
  });

  it('unescapes pointer segments', () => {
    expect(frameworkPath({ 'a/b': {} }, '/a~1b')).toBe('a/b');
  });
});

describe('checkSchema', () => {
  it('accepts a minimal menu', () => {
    expect(checkSchema({ Title: 'Hi' })).toEqual([]);
  });

  it('warns about unknown members', () => {
    const problems = checkSchema({ Title: 'Hi', Bogus: 'x' });
    expect(problems).toContainEqual(expect.objectContaining({ severity: 'warning', field: 'Bogus', path: 'Bogus' }));
  });

  it('errors on a wrongly typed member', () => {
    const problems = checkSchema({ Children: 'nope' });
    expect(problems.some(p => p.severity === 'error' && p.field === 'Children')).toBe(true);
  });
});

describe('validate', () => {
  const menu = (children: string) => importText(`{ "Menus": { "mod/a": { "Title": "T", "Children": [ ${children} ] } } }`).candidates[0]!.document;

  it('reports a problem and attaches the node it is about', () => {
    const doc = menu('{ "Label": "x", "Bogus": "1" }');
    const problems = validate(doc);
    const bogus = problems.find(p => p.message.includes('Bogus'));
    expect(bogus).toBeDefined();
    expect(doc.nodes[bogus!.nodeId!]).toBeDefined();
  });

  it('finds no errors in a simple valid menu', () => {
    expect(validate(menu('{ "Label": "x" }')).filter(p => p.severity === 'error')).toEqual([]);
  });
});
