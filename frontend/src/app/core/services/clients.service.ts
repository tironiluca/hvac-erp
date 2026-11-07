import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Client, UpsertClientRequest } from '../models/client.model';

@Injectable({ providedIn: 'root' })
export class ClientsService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/clients`;

  /** Searches by name, code, phone or mobile; no term lists the first 100 clients. */
  search(term?: string): Observable<Client[]> {
    const params = term ? new HttpParams().set('search', term) : undefined;
    return this.http.get<Client[]>(this.baseUrl, { params });
  }

  /** Gets one client. */
  getById(id: number): Observable<Client> {
    return this.http.get<Client>(`${this.baseUrl}/${id}`);
  }

  /** Creates a client (409 when the code is taken). */
  create(request: UpsertClientRequest): Observable<Client> {
    return this.http.post<Client>(this.baseUrl, request);
  }

  /** Updates a client's registry data. */
  update(id: number, request: UpsertClientRequest): Observable<Client> {
    return this.http.put<Client>(`${this.baseUrl}/${id}`, request);
  }

  /** Deletes a client with no quotes or repairs (Admin only; 400 with the reason otherwise). */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
