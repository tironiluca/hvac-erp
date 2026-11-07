import { Injectable, signal } from '@angular/core';

type ThemeMode = 'light' | 'dark';

const STORAGE_KEY = 'hvac-theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  mode = signal<ThemeMode>((localStorage.getItem(STORAGE_KEY) as ThemeMode) || 'light');

  constructor() {
    document.documentElement.classList.toggle('dark-theme', this.mode() === 'dark');
  }

  /** Applies and persists light / dark mode. */
  set(mode: ThemeMode): void {
    this.mode.set(mode);
    document.documentElement.classList.toggle('dark-theme', mode === 'dark');
    localStorage.setItem(STORAGE_KEY, mode);
  }

  /** Switches between light and dark mode. */
  toggle(): void {
    this.set(this.mode() === 'light' ? 'dark' : 'light');
  }
}
