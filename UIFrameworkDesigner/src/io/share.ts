import { compressToEncodedURIComponent, decompressFromEncodedURIComponent } from 'lz-string';
import type { Workspace } from '../model/workspace';
import { parseWorkspace, serializeWorkspace } from './workspace';

// Share links (architecture.md §11): the whole workspace file (serializeWorkspace, unindented) compressed with lz-string
// in the URL hash (`#w=…`), so it is never sent to the server. Decoding is a plain workspace parse, so a share link and
// a saved `.uifw.json` file are the same format.

/** Links longer than this are offered as a download instead (some chat apps and browsers cut longer URLs). */
export const ShareLimit = 8 * 1024;

const HashPrefix = '#w=';

/** The page URL with the workspace in its hash. */
export function shareLink(ws: Workspace): string {
  const { origin, pathname, search } = window.location;
  return `${origin}${pathname}${search}${HashPrefix}${compressToEncodedURIComponent(serializeWorkspace(ws, 0))}`;
}

/** The workspace a `#w=` hash holds, clearing the hash; null when the hash is not a share link or holds none. */
export function takeSharedWorkspace(): Workspace | null {
  const hash = window.location.hash;
  if (!hash.startsWith(HashPrefix)) {
    return null;
  }

  window.history.replaceState(null, '', `${window.location.pathname}${window.location.search}`);
  let text: string | null;
  try {
    text = decompressFromEncodedURIComponent(hash.slice(HashPrefix.length));
  } catch {
    return null;
  }

  return text ? parseWorkspace(text).workspace : null;
}
