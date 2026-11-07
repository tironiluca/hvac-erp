import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { FGasRecord, UpsertFGasRecordRequest } from '../models/fgas-record.model';

@Injectable({ providedIn: 'root' })
export class FGasRecordsService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/fgas-records`;

  /** F-Gas records attached to a quote. */
  getForQuote(quoteId: number): Observable<FGasRecord[]> {
    return this.http.get<FGasRecord[]>(this.baseUrl, { params: new HttpParams().set('quoteId', quoteId) });
  }

  /** F-Gas records attached to a repair. */
  getForRepair(repairId: number): Observable<FGasRecord[]> {
    return this.http.get<FGasRecord[]>(this.baseUrl, { params: new HttpParams().set('repairId', repairId) });
  }

  /** Appends an F-Gas record to exactly one quote or repair. */
  create(request: UpsertFGasRecordRequest): Observable<FGasRecord> {
    return this.http.post<FGasRecord>(this.baseUrl, request);
  }

  /** Deletes an F-Gas record entered in error. */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
