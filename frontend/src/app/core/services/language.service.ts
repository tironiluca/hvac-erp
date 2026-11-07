import { inject, Injectable, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';

const STORAGE_KEY = 'hvac-lang';

/**
 * Runtime UI language, persisted to `localStorage`. Injected in the root `App`
 * component (like `ThemeService`) so the stored choice is active from first paint,
 * including on the login page, which sits outside the shell.
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private transloco = inject(TranslocoService);

  /** ISO codes the app ships translations for, in display order. */
  readonly available = this.transloco.getAvailableLangs() as string[];

  private readonly langSignal = signal<string>(this.resolveInitial());
  readonly lang = this.langSignal.asReadonly();

  constructor() {
    this.apply(this.langSignal());
  }

  /** Switches and persists the UI language; ignores unknown or unchanged codes. */
  set(lang: string): void {
    if (!this.available.includes(lang) || lang === this.langSignal()) {
      return;
    }
    this.langSignal.set(lang);
    this.apply(lang);
    localStorage.setItem(STORAGE_KEY, lang);
  }

  /** Activates the translations and keeps `<html lang>` in step for screen readers and spell-check. */
  private apply(lang: string): void {
    this.transloco.setActiveLang(lang);
    document.documentElement.lang = lang;
  }

  private resolveInitial(): string {
    const stored = localStorage.getItem(STORAGE_KEY);
    return stored && this.available.includes(stored)
      ? stored
      : this.transloco.getDefaultLang();
  }
}
