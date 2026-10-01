import elementTypesJson from '../generated/element-types.json';
import defaultsJson from '../generated/defaults.json';
import csharpMapJson from '../generated/csharp-map.json';
import menuSchemaJson from '../generated/menu.schema.json';
import frameworkVersionText from '../generated/framework-version.txt?raw';
import type { CSharpMapFile, DefaultsFile, ElementTypeInfo, ElementTypesFile } from '../generated/types';

// The one entry point to the generated metadata bundle (architecture.md §4); nothing else imports src/generated.

export const elementTypes = elementTypesJson as ElementTypesFile;
export const defaults = defaultsJson as DefaultsFile;
export const csharpMap = csharpMapJson as CSharpMapFile;
export const menuSchema = menuSchemaJson as Record<string, unknown>;
export const frameworkVersion = frameworkVersionText.trim();

const byName = new Map(elementTypes.types.map(t => [t.name.toLowerCase(), t]));

/** The type info for a canonical or differently-cased type name; undefined for templates and custom tags. */
export function typeInfo(type: string): ElementTypeInfo | undefined {
  return byName.get(type.trim().toLowerCase());
}

/** The canonical spelling of a built-in type, or null (ElementTypes.Canonical). */
export function canonicalType(type: string | undefined | null): string | null {
  return type ? (typeInfo(type)?.name ?? null) : null;
}

/** True when the type holds children (ElementTypes.IsContainer). */
export function isContainer(type: string): boolean {
  return typeInfo(type)?.container ?? false;
}

/** True for a dotted custom tag that stands for a composite (ElementTypes.IsCustomTag). */
export function isCustomTag(type: string): boolean {
  return type.trim().includes('.') && canonicalType(type) === null;
}

/** True when an element of this type reads the member (ElementTypes.Uses). */
export function usesMember(type: string, member: string): boolean {
  const m = member.toLowerCase();
  return elementTypes.common.some(c => c.toLowerCase() === m)
    || (typeInfo(type)?.members.some(c => c.toLowerCase() === m) ?? false);
}

/** True when this type (or its children) can use the style field (ElementTypes.UsesStyle). */
export function usesStyle(type: string, field: string): boolean {
  const leaf = typeInfo(type)?.leafStyle;
  return !leaf || leaf.some(f => f.toLowerCase() === field.toLowerCase());
}

/** The data-format default of a field, or undefined. */
export function defaultOf(type: string | 'menu', field: string): string | undefined {
  return type === 'menu' ? defaults.menu[field] : defaults.elements[canonicalType(type) ?? type]?.[field];
}

/** The tooltip block kinds (TooltipBlockKinds.All, from the schema's TooltipBlockDefinition.Type enum). */
export const tooltipBlockTypes: readonly string[] = (() => {
  const defs = menuSchema['definitions'] as Record<string, { properties?: Record<string, { enum?: unknown[] }> }> | undefined;
  return (defs?.['TooltipBlockDefinition']?.properties?.['Type']?.enum ?? []).filter((v): v is string => typeof v === 'string');
})();

/** True for a tooltip block node type (Title, Line, Icon, Item, Divider, Money). */
export function isTooltipBlock(type: string): boolean {
  return tooltipBlockTypes.includes(type);
}
