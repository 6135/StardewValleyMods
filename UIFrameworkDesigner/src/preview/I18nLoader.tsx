// Loads a content pack's i18n/default.json (JSONC, as SMAPI reads it) for the preview's {{i18n:key}} text (§7.2).
// The file is read in the browser and kept in memory by the caller; nothing is uploaded.
import { useState, type ReactNode } from 'react';
import { parse, printParseErrorCode, type ParseError } from 'jsonc-parser';

export interface I18nLoaderProps {
  /** Called with the key → text map of the picked file. */
  onLoad(map: Record<string, string>): void;
}

export function I18nLoader({ onLoad }: I18nLoaderProps): ReactNode {
  const [status, setStatus] = useState('');

  const load = async (file: File | undefined): Promise<void> => {
    if (!file) {
      return;
    }
    const errors: ParseError[] = [];
    const data: unknown = parse(await file.text(), errors, { allowTrailingComma: true });
    if (errors.length > 0 || !data || typeof data !== 'object' || Array.isArray(data)) {
      setStatus(errors.length > 0 ? `${file.name}: ${printParseErrorCode(errors[0]!.error)} at ${errors[0]!.offset}` : `${file.name}: not an object`);
      return;
    }
    const map: Record<string, string> = {};
    for (const [key, value] of Object.entries(data as Record<string, unknown>)) {
      if (typeof value === 'string') {
        map[key] = value;
      }
    }
    setStatus(`${file.name}: ${Object.keys(map).length} keys`);
    onLoad(map);
  };

  return (
    <label className="pv-i18n" title="Load i18n/default.json to show {{i18n:key}} text">
      i18n{' '}
      <input type="file" accept=".json,.jsonc,application/json" onChange={e => void load(e.target.files?.[0])} />
      {status ? <span className="pv-i18n-status">{status}</span> : null}
    </label>
  );
}

export default I18nLoader;
