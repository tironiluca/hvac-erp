import { toLocalDateTimeIso } from './date.util';

describe('toLocalDateTimeIso', () => {
  it('serializes the local calendar date at midnight, with no offset', () => {
    expect(toLocalDateTimeIso(new Date(2026, 2, 7))).toBe('2026-03-07T00:00:00');
  });

  it('ignores the time of day and keeps the local date even when UTC is a different day', () => {
    // 00:30 local is still the 1st locally, whatever the runner's UTC offset — toISOString() would not be.
    expect(toLocalDateTimeIso(new Date(2026, 0, 1, 0, 30))).toBe('2026-01-01T00:00:00');
    expect(toLocalDateTimeIso(new Date(2026, 11, 31, 23, 59))).toBe('2026-12-31T00:00:00');
  });

  it('zero-pads month and day', () => {
    expect(toLocalDateTimeIso(new Date(2027, 8, 4))).toBe('2027-09-04T00:00:00');
  });
});
