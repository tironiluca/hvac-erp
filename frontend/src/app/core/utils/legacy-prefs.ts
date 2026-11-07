/**
 * Per-browser preferences were stored under `arc-*` keys before the product rename to `hvac-*`.
 * Run once before bootstrap so the services (which read `localStorage` on construction) find the
 * value under the new key: copies an old value across when the new key is unset, then drops the
 * old key. Idempotent; a no-op when storage is unavailable (private mode, blocked site data).
 */
export const LEGACY_PREF_KEYS: Readonly<Record<string, string>> = {
  'arc-theme': 'hvac-theme',
  'arc-lang': 'hvac-lang',
  'arc-week-start': 'hvac-week-start',
  'arc-nav-collapsed': 'hvac-nav-collapsed',
};

export function migrateLegacyPrefs(storage: Storage | undefined = globalThis.localStorage): void {
  if (!storage) return;
  try {
    for (const [oldKey, newKey] of Object.entries(LEGACY_PREF_KEYS)) {
      const value = storage.getItem(oldKey);
      if (value === null) continue;
      if (storage.getItem(newKey) === null) storage.setItem(newKey, value);
      storage.removeItem(oldKey);
    }
  } catch {
    // Storage access can throw (SecurityError); preferences then just start from defaults.
  }
}
