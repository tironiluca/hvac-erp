import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../../environments/environment';
import { DashboardService, startOfWeek } from './dashboard.service';

describe('startOfWeek', () => {
  const thursday = new Date(2026, 10, 5, 15, 30); // Thu 5 Nov 2026, local time

  it('goes back to the configured first weekday at local midnight', () => {
    expect(startOfWeek(thursday, 1)).toEqual(new Date(2026, 10, 2)); // Monday
    expect(startOfWeek(thursday, 0)).toEqual(new Date(2026, 10, 1)); // Sunday
    expect(startOfWeek(thursday, 6)).toEqual(new Date(2026, 9, 31)); // Saturday
  });

  it('returns the same day when today is the first weekday', () => {
    expect(startOfWeek(new Date(2026, 10, 2, 8), 1)).toEqual(new Date(2026, 10, 2));
  });
});

describe('DashboardService', () => {
  it('sends the week start as a local date, never via toISOString()', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);

    TestBed.inject(DashboardService).get(new Date(2026, 10, 2)).subscribe();

    const req = http.expectOne((r) => r.url === `${environment.apiUrl}/dashboard`);
    expect(req.request.params.get('weekStart')).toBe('2026-11-02T00:00:00');
    http.verify();
  });
});
