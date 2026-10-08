import Ajv, { type ErrorObject, type ValidateFunction } from 'ajv';
import type { Problem } from '../model/document';
import { menuSchema } from '../model/metadata';
import { fieldPath, indexPath, isObject, scalarText, type JsonObject } from '../io/dataFormat';
import type { RawProblem } from './rules';

// The schema pass (architecture.md §8): Ajv with the generated menu.schema.json (draft-07, as SchemaWriter writes it),
// compiled once. Errors inside oneOf / anyOf branches are folded into the branch point's single message.

let compiled: ValidateFunction | null = null;

function validator(): ValidateFunction {
  if (compiled === null) {
    const ajv = new Ajv({ strict: false, allErrors: true, validateSchema: false });
    compiled = ajv.compile(menuSchema);
  }

  return compiled;
}

/** Check a MenuDefinition object against the schema. */
export function checkSchema(menu: JsonObject): RawProblem[] {
  const validate = validator();
  if (validate(menu)) {
    return [];
  }

  const problems: RawProblem[] = [];
  for (const error of validate.errors ?? []) {
    const p = toProblem(menu, error as ErrorObject & { propertyName?: string });
    if (p !== null) {
      problems.push(p);
    }
  }

  return problems;
}

function toProblem(menu: JsonObject, error: ErrorObject & { propertyName?: string }): RawProblem | null {
  if (error.keyword === 'if' || error.keyword === 'propertyNames' || /\/(oneOf|anyOf)\/\d+\//.test(error.schemaPath)) {
    return null;
  }

  const ptr = error.instancePath;
  const path = frameworkPath(menu, ptr);
  const unknown = error.propertyName ?? (error.keyword === 'additionalProperties' ? String((error.params as { additionalProperty?: string }).additionalProperty) : undefined);
  if (unknown !== undefined) {
    return { severity: 'warning', path: fieldPath(path, unknown), pointer: ptr, field: unknown, message: `unknown field '${unknown}'; it is ignored.` };
  }

  let severity: Problem['severity'] = 'warning';
  let message: string;
  switch (error.keyword) {
    case 'type':
      severity = 'error';
      message = `must be ${String((error.params as { type?: unknown }).type).replace(/,/g, ', ')}; the menu cannot be read.`;
      break;
    case 'oneOf':
    case 'anyOf':
      severity = 'error';
      message = 'the value has none of the forms this member accepts; the menu cannot be read.';
      break;
    case 'enum':
      message = `must be one of ${((error.params as { allowedValues?: unknown[] }).allowedValues ?? []).map(String).join(', ')}.`;
      break;
    default:
      message = error.message ?? error.keyword;
      break;
  }

  const segments = ptr.split('/');
  const last = segments[segments.length - 1];
  const field = last !== undefined && last.length > 0 && !/^\d+$/.test(last) ? unescape(last) : undefined;
  return field !== undefined ? { severity, path, pointer: ptr, field, message } : { severity, path, pointer: ptr, message };
}

function childAt(parent: unknown, key: string | number): unknown {
  return Object.entries(parent as object).find(([k]) => k === String(key))?.[1];
}

/** `/Children/2/Spacing` → `Children[2](#id).Spacing` (ids read from the checked value, like DataPath.Index). */
export function frameworkPath(root: unknown, ptr: string): string {
  let path = '';
  let value: unknown = root;
  for (const raw of ptr.split('/').slice(1)) {
    const segment = unescape(raw);
    if (Array.isArray(value)) {
      const index = Number(segment);
      value = childAt(value, index);
      path = indexPath(path, index, isObject(value) ? scalarText(value.Id) : undefined);
    } else {
      value = isObject(value) ? childAt(value, segment) : undefined;
      path = fieldPath(path, segment);
    }
  }

  return path;
}

function unescape(segment: string): string {
  return segment.replace(/~1/g, '/').replace(/~0/g, '~');
}
