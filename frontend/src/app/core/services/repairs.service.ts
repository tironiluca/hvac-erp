import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddRepairScheduleEntryRequest,
  ChangeRepairStatusRequest,
  RepairDetail,
  RepairListItem,
  RepairPdfResponse,
  RepairScheduleEntry,
  UpsertRepairRequest,
} from '../models/repair.model';

@Injectable({ providedIn: 'root' })
export class RepairsService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/repairs`;

  /** Repairs newest first, optionally for one client. */
  getList(clientId?: number): Observable<RepairListItem[]> {
    const params = clientId ? new HttpParams().set('clientId', clientId) : undefined;
    return this.http.get<RepairListItem[]>(this.baseUrl, { params });
  }

  /** Gets the full repair. */
  getById(id: number): Observable<RepairDetail> {
    return this.http.get<RepairDetail>(`${this.baseUrl}/${id}`);
  }

  /** Creates a repair or revision. */
  create(request: UpsertRepairRequest): Observable<RepairDetail> {
    return this.http.post<RepairDetail>(this.baseUrl, request);
  }

  /** Replaces the repair content and service lines. */
  update(id: number, request: UpsertRepairRequest): Observable<RepairDetail> {
    return this.http.put<RepairDetail>(`${this.baseUrl}/${id}`, request);
  }

  /** Moves the repair to another status (appended to its history). */
  changeStatus(id: number, request: ChangeRepairStatusRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/status`, request);
  }

  /** Books an appointment, optionally assigned to an installer. */
  addScheduleEntry(id: number, request: AddRepairScheduleEntryRequest): Observable<RepairScheduleEntry> {
    return this.http.post<RepairScheduleEntry>(`${this.baseUrl}/${id}/schedule`, request);
  }

  /** Deletes the repair with its lines, appointments, history and F-Gas records. */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Renders and stores the work order; resolves to its public URL. */
  generatePdf(id: number): Observable<RepairPdfResponse> {
    return this.http.post<RepairPdfResponse>(`${this.baseUrl}/${id}/pdf`, {});
  }
}
