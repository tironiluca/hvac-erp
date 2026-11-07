import { Injectable, signal } from '@angular/core';

/** Which weekday a calendar grid starts on. Values match `Date.getDay()`. */
export type WeekStart = 0 | 1 | 6; // Sunday | Monday | Saturday

const STORAGE_KEY = 'hvac-week-start';
const DEFAULT: WeekStart = 1;

/**
 * Per-user calendar display preferences, kept in `localStorage` like the theme and
 * language choices — no server round-trip, scoped to this browser.
 */
@Injectable({ providedIn: 'root' })
export class CalendarPrefsService {
  readonly weekStart = signal<WeekStart>(readStored());

  /** Persists the first weekday of calendar grids for this browser. */
  setWeekStart(value: WeekStart): void {
    this.weekStart.set(value);
    localStorage.setItem(STORAGE_KEY, String(value));
  }
}

function readStored(): WeekStart {
  const raw = Number(localStorage.getItem(STORAGE_KEY));
  return raw === 0 || raw === 6 ? raw : DEFAULT;
}
