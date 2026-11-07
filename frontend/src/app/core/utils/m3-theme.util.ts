import { argbFromHex, hexFromArgb, Hct, TonalPalette } from '@material/material-color-utilities';

/**
 * Runtime Material 3 theming: turn three brand seed hexes into the full set of `--mat-sys-*` colour
 * custom properties that Angular Material's `system-level-colors` mixin normally bakes in at build
 * time. `BrandingService` writes the result as inline properties on `<html>`, which override the
 * compile-time `styles.scss` theme. Uses the same HCT tonal-palette engine as
 * `ng generate @angular/material:theme-color`, so a given seed yields the same tones the schematic
 * would have produced (verified against `_theme-colors.scss`: `#2B6CB0` → P40 `#1960a3`, P80 `#a2c9ff`).
 */

export interface BrandSeeds {
  primary: string;
  tertiary: string;
  neutral: string;
}

/** The compile-time default seeds (neutral blue / amber / grey) — the fallback when the API is unreachable. */
export const DEFAULT_BRAND_SEEDS: BrandSeeds = {
  primary: '#2B6CB0',
  tertiary: '#DD6B20',
  neutral: '#5F6368',
};

type PaletteKey = 'primary' | 'secondary' | 'tertiary' | 'neutral' | 'neutralVariant' | 'error';

/** `token name (without the --mat-sys- prefix)` → `[palette, light tone, dark tone]` (M3 spec). */
const TOKEN_TONES: Record<string, [PaletteKey, number, number]> = {
  primary: ['primary', 40, 80],
  'on-primary': ['primary', 100, 20],
  'primary-container': ['primary', 90, 30],
  'on-primary-container': ['primary', 10, 90],
  'inverse-primary': ['primary', 80, 40],
  'primary-fixed': ['primary', 90, 90],
  'primary-fixed-dim': ['primary', 80, 80],
  'on-primary-fixed': ['primary', 10, 10],
  'on-primary-fixed-variant': ['primary', 30, 30],

  secondary: ['secondary', 40, 80],
  'on-secondary': ['secondary', 100, 20],
  'secondary-container': ['secondary', 90, 30],
  'on-secondary-container': ['secondary', 10, 90],
  'secondary-fixed': ['secondary', 90, 90],
  'secondary-fixed-dim': ['secondary', 80, 80],
  'on-secondary-fixed': ['secondary', 10, 10],
  'on-secondary-fixed-variant': ['secondary', 30, 30],

  tertiary: ['tertiary', 40, 80],
  'on-tertiary': ['tertiary', 100, 20],
  'tertiary-container': ['tertiary', 90, 30],
  'on-tertiary-container': ['tertiary', 10, 90],
  'tertiary-fixed': ['tertiary', 90, 90],
  'tertiary-fixed-dim': ['tertiary', 80, 80],
  'on-tertiary-fixed': ['tertiary', 10, 10],
  'on-tertiary-fixed-variant': ['tertiary', 30, 30],

  error: ['error', 40, 80],
  'on-error': ['error', 100, 20],
  'error-container': ['error', 90, 30],
  'on-error-container': ['error', 10, 90],

  background: ['neutral', 98, 6],
  'on-background': ['neutral', 10, 90],
  surface: ['neutral', 98, 6],
  'on-surface': ['neutral', 10, 90],
  'surface-dim': ['neutral', 87, 6],
  'surface-bright': ['neutral', 98, 24],
  'surface-container-lowest': ['neutral', 100, 4],
  'surface-container-low': ['neutral', 96, 10],
  'surface-container': ['neutral', 94, 12],
  'surface-container-high': ['neutral', 92, 17],
  'surface-container-highest': ['neutral', 90, 22],
  'inverse-surface': ['neutral', 20, 90],
  'inverse-on-surface': ['neutral', 95, 20],
  'surface-tint': ['primary', 40, 80],
  scrim: ['neutral', 0, 0],
  shadow: ['neutral', 0, 0],
  neutral10: ['neutral', 10, 10],

  'surface-variant': ['neutralVariant', 90, 30],
  'on-surface-variant': ['neutralVariant', 30, 80],
  outline: ['neutralVariant', 50, 60],
  'outline-variant': ['neutralVariant', 80, 30],
  'neutral-variant20': ['neutralVariant', 20, 20],
};

function normalizeHex(value: string): string {
  const trimmed = (value ?? '').trim();
  const withHash = trimmed.startsWith('#') ? trimmed : `#${trimmed}`;
  return /^#[0-9a-fA-F]{6}$/.test(withHash) ? withHash : '#000000';
}

function buildPalettes(seeds: BrandSeeds): Record<PaletteKey, TonalPalette> {
  const primaryArgb = argbFromHex(normalizeHex(seeds.primary));
  const primaryHct = Hct.fromInt(primaryArgb);
  const neutralHct = Hct.fromInt(argbFromHex(normalizeHex(seeds.neutral)));

  return {
    primary: TonalPalette.fromInt(primaryArgb),
    tertiary: TonalPalette.fromInt(argbFromHex(normalizeHex(seeds.tertiary))),
    // Secondary is a muted companion to primary (M3 derives it from primary's hue).
    secondary: TonalPalette.fromHueAndChroma(primaryHct.hue, 16),
    // Chrome ramp: keep it near-achromatic unless the admin deliberately picks a saturated neutral.
    neutral: TonalPalette.fromHueAndChroma(neutralHct.hue, Math.min(neutralHct.chroma, 4)),
    neutralVariant: TonalPalette.fromHueAndChroma(neutralHct.hue, Math.min(neutralHct.chroma + 4, 8)),
    // Error stays a fixed M3 red — not brand-driven.
    error: TonalPalette.fromInt(argbFromHex('#B3261E')),
  };
}

/** The `--mat-sys-*` colour token map for the given seeds and light/dark mode. */
export function buildSysColorVars(seeds: BrandSeeds, isDark: boolean): Record<string, string> {
  const palettes = buildPalettes(seeds);
  const vars: Record<string, string> = {};
  for (const [token, [paletteKey, lightTone, darkTone]] of Object.entries(TOKEN_TONES)) {
    vars[`--mat-sys-${token}`] = hexFromArgb(palettes[paletteKey].tone(isDark ? darkTone : lightTone));
  }
  return vars;
}

/** A single tone off the primary palette — handy for non-token accents (e.g. the login backdrop). */
export function primaryTone(seeds: BrandSeeds, tone: number): string {
  return hexFromArgb(TonalPalette.fromInt(argbFromHex(normalizeHex(seeds.primary))).tone(tone));
}
