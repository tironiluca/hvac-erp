import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  InstallationPdfResponse,
  RescheduleInstallationRequest,
  ScheduledInstallation,
  UpsertScheduledInstallationRequest,
} from '../models/installation.model';

@Injectable({ providedIn: 'root' })
export class InstallationsService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/installations`;

  /** Scheduled installations, optionally within an inclusive `from`–`to` window (local date-time ISO strings). */
  getList(from?: string, to?: string): Observable<ScheduledInstallation[]> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<ScheduledInstallation[]>(this.baseUrl, { params });
  }

  /** Gets one scheduled installation. */
  getById(id: number): Observable<ScheduledInstallation> {
    return this.http.get<ScheduledInstallation>(`${this.baseUrl}/${id}`);
  }

  /** Schedules a confirmed quote (one per quote, not in the past). */
  create(request: UpsertScheduledInstallationRequest): Observable<ScheduledInstallation> {
    return this.http.post<ScheduledInstallation>(this.baseUrl, request);
  }

  /** Full edit of a scheduled installation. */
  update(id: number, request: UpsertScheduledInstallationRequest): Observable<ScheduledInstallation> {
    return this.http.put<ScheduledInstallation>(`${this.baseUrl}/${id}`, request);
  }

  /** Drag-and-drop reschedule from the shared calendar. 409 if another user moved it first. */
  reschedule(id: number, request: RescheduleInstallationRequest): Observable<ScheduledInstallation> {
    return this.http.patch<ScheduledInstallation>(`${this.baseUrl}/${id}/schedule`, request);
  }

  /** Cancels a scheduled installation. */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Renders and stores the installation work order; resolves to its public URL. */
  generatePdf(id: number): Observable<InstallationPdfResponse> {
    return this.http.post<InstallationPdfResponse>(`${this.baseUrl}/${id}/pdf`, {});
  }
}
