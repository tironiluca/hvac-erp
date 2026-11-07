import { buildSysColorVars, DEFAULT_BRAND_SEEDS, primaryTone } from './m3-theme.util';

describe('m3-theme util', () => {
  it('reproduces the schematic tones for the default brand seed', () => {
    // Verified against the Angular Material theme-color schematic output (_theme-colors.scss).
    expect(primaryTone(DEFAULT_BRAND_SEEDS, 40)).toBe('#1960a3');
    expect(primaryTone(DEFAULT_BRAND_SEEDS, 80)).toBe('#a2c9ff');
  });

  it('emits the full --mat-sys-* colour token set as hex values', () => {
    const vars = buildSysColorVars(DEFAULT_BRAND_SEEDS, false);
    const keys = Object.keys(vars);

    expect(keys.length).toBe(51);
    expect(keys.every((k) => k.startsWith('--mat-sys-'))).toBe(true);
    expect(Object.values(vars).every((v) => /^#[0-9a-f]{6}$/.test(v))).toBe(true);
  });

  it('maps light and dark mode onto the M3 tones', () => {
    const light = buildSysColorVars(DEFAULT_BRAND_SEEDS, false);
    const dark = buildSysColorVars(DEFAULT_BRAND_SEEDS, true);

    expect(light['--mat-sys-primary']).toBe('#1960a3'); // P40
    expect(dark['--mat-sys-primary']).toBe('#a2c9ff'); // P80
    expect(light['--mat-sys-surface-tint']).toBe(light['--mat-sys-primary']);
  });

  it('keeps the error palette fixed regardless of brand seeds', () => {
    const a = buildSysColorVars(DEFAULT_BRAND_SEEDS, false);
    const b = buildSysColorVars({ primary: '#00ff00', tertiary: '#ff00ff', neutral: '#123456' }, false);

    expect(b['--mat-sys-error']).toBe(a['--mat-sys-error']);
    expect(b['--mat-sys-primary']).not.toBe(a['--mat-sys-primary']);
  });

  it('tolerates seeds without a leading # and falls back to black for garbage', () => {
    expect(primaryTone({ ...DEFAULT_BRAND_SEEDS, primary: '2B6CB0' }, 40)).toBe('#1960a3');
    expect(primaryTone({ ...DEFAULT_BRAND_SEEDS, primary: 'not-a-colour' }, 40)).toBe(
      primaryTone({ ...DEFAULT_BRAND_SEEDS, primary: '#000000' }, 40),
    );
  });
});
