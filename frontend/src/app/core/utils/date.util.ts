/**
 * Serializes a `Date` picked from a date-only control (e.g. a Material datepicker) using its
 * local calendar date, not `Date.prototype.toISOString()` — which converts to UTC and silently
 * shifts the date backward by a day for any positive UTC offset (e.g. CEST, UTC+2: a local
 * midnight becomes 22:00 the previous day), corrupting date-only values the server treats as
 * plain calendar dates (e.g. rejecting a same-day booking as "in the past").
 */
export function toLocalDateTimeIso(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}T00:00:00`;
}
