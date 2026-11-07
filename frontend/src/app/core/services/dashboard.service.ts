import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Dashboard } from '../models/dashboard.model';
import { toLocalDateTimeIso } from '../utils/date.util';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private http = inject(HttpClient);

  /** Loads the dashboard; installations cover the 7 days from `weekStart` (sent as a local date). */
  get(weekStart: Date): Observable<Dashboard> {
    const params = new HttpParams().set('weekStart', toLocalDateTimeIso(weekStart));
    return this.http.get<Dashboard>(`${environment.apiUrl}/dashboard`, { params });
  }
}

/** The first day of the week containing `today`, honouring the user's week-start preference (`Date.getDay()` value). */
export function startOfWeek(today: Date, weekStartsOn: number): Date {
  const start = new Date(today.getFullYear(), today.getMonth(), today.getDate());
  start.setDate(start.getDate() - ((start.getDay() - weekStartsOn + 7) % 7));
  return start;
}
