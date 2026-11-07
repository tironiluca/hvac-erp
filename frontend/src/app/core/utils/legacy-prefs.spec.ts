import { migrateLegacyPrefs } from './legacy-prefs';

describe('migrateLegacyPrefs', () => {
  beforeEach(() => localStorage.clear());

  it('moves a pre-rename value to the new key and removes the old one', () => {
    localStorage.setItem('arc-theme', 'dark');
    localStorage.setItem('arc-nav-collapsed', '1');

    migrateLegacyPrefs();

    expect(localStorage.getItem('hvac-theme')).toBe('dark');
    expect(localStorage.getItem('hvac-nav-collapsed')).toBe('1');
    expect(localStorage.getItem('arc-theme')).toBeNull();
    expect(localStorage.getItem('arc-nav-collapsed')).toBeNull();
  });

  it('never overwrites a value already saved under the new key', () => {
    localStorage.setItem('arc-lang', 'en');
    localStorage.setItem('hvac-lang', 'it');

    migrateLegacyPrefs();

    expect(localStorage.getItem('hvac-lang')).toBe('it');
    expect(localStorage.getItem('arc-lang')).toBeNull();
  });

  it('tolerates storage that throws', () => {
    const throwing = {
      getItem: () => {
        throw new Error('SecurityError');
      },
    } as unknown as Storage;
    expect(() => migrateLegacyPrefs(throwing)).not.toThrow();
  });
});
