import { compressToEncodedURIComponent, decompressFromEncodedURIComponent } from 'lz-string';
import type { DesignerDocument } from '../model/document';
import { buildMenuObject, printJson } from './export';
import { DesignerMember, importText } from './import';

// Share links (architecture.md §11): the From-file JSON of the document plus a "$designer" member (owner, menu id,
// preview state) that the framework ignores, compressed with lz-string for the URL hash. Decoding is a plain import,
// so a share link and a saved file are the same format.

/** The document as a URL-safe string for the location hash (without the leading '#'). */
export function encodeShare(doc: DesignerDocument): string {
  return compressToEncodedURIComponent(shareJson(doc));
}

/** The document a share hash holds, or null when it is not one. Accepts the hash with or without its leading '#'. */
export function decodeShare(hash: string): DesignerDocument | null {
  const text = hash.startsWith('#') ? hash.slice(1) : hash;
  if (text.length === 0) {
    return null;
  }

  let json: string | null;
  try {
    json = decompressFromEncodedURIComponent(text);
  } catch {
    return null;
  }

  if (!json) {
    return null;
  }

  return importText(json).candidates[0]?.document ?? null;
}

/** The JSON a share link (or a saved designer file) holds: the From-file shape with a "$designer" member first. */
export function shareJson(doc: DesignerDocument, indent = 0): string {
  const { value } = buildMenuObject(doc, { collapseShorthands: false, omitDefaults: false });
  return printJson({ [DesignerMember]: { owner: doc.owner, menuId: doc.menuId, previewState: doc.previewState }, ...value }, indent);
}
