import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ErpExport,
  QuoteAttachment,
  QuoteDetail,
  QuoteListItem,
  QuotePdfResponse,
  RecallQuoteRequest,
  UpsertQuoteRequest,
} from '../models/quote.model';
import { DocumentUrl } from '../utils/open-document';

@Injectable({ providedIn: 'root' })
export class QuotesService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/quotes`;

  /** Quotes newest first, optionally for one client. */
  getList(clientId?: number): Observable<QuoteListItem[]> {
    const params = clientId ? new HttpParams().set('clientId', clientId) : undefined;
    return this.http.get<QuoteListItem[]>(this.baseUrl, { params });
  }

  /** Gets the full quote. */
  getById(id: number): Observable<QuoteDetail> {
    return this.http.get<QuoteDetail>(`${this.baseUrl}/${id}`);
  }

  /** Creates a quote; the server assigns numbering and prices it. */
  create(request: UpsertQuoteRequest): Observable<QuoteDetail> {
    return this.http.post<QuoteDetail>(this.baseUrl, request);
  }

  /** Replaces the quote content and re-prices it (400 once confirmed). */
  update(id: number, request: UpsertQuoteRequest): Observable<QuoteDetail> {
    return this.http.put<QuoteDetail>(`${this.baseUrl}/${id}`, request);
  }

  /** Records that the site survey happened. */
  markSurveyDone(id: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/survey`, {});
  }

  /** Records a customer recall with optional notes. */
  recall(id: number, request: RecallQuoteRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/recall`, request);
  }

  /** Confirms the quote, locking it (Sales/Admin only). */
  confirm(id: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/confirm`, {});
  }

  /** The quote's export to the main ERP; null when it has none (204). */
  getErpExport(id: number): Observable<ErpExport | null> {
    return this.http.get<ErpExport | null>(`${this.baseUrl}/${id}/erp-export`);
  }

  /** Sends a confirmed quote to the main ERP now (or retries a failed send). */
  sendToErp(id: number): Observable<ErpExport> {
    return this.http.post<ErpExport>(`${this.baseUrl}/${id}/erp-export`, {});
  }

  /** Flips the customer's acceptance of an incentive that requires it (e.g. an invoice discount). */
  toggleIncentiveConfirmation(id: number, schemeId: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/incentives/${schemeId}/confirmation`, {});
  }

  /** Deletes the quote. */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Renders and stores the customer PDF; resolves to its public URL. */
  generatePdf(id: number): Observable<QuotePdfResponse> {
    return this.http.post<QuotePdfResponse>(`${this.baseUrl}/${id}/pdf`, {});
  }

  /** A link to the generated PDF that works for a few minutes. */
  getPdfUrl(id: number): Observable<DocumentUrl> {
    return this.http.get<DocumentUrl>(`${this.baseUrl}/${id}/pdf-url`);
  }

  /** A link to an attachment's file that works for a few minutes. */
  getAttachmentUrl(id: number, attachmentId: number): Observable<DocumentUrl> {
    return this.http.get<DocumentUrl>(`${this.baseUrl}/${id}/attachments/${attachmentId}/url`);
  }

  /** Uploads a photo or PDF as multipart form data (20 MB max). */
  uploadAttachment(id: number, file: File): Observable<QuoteAttachment> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.http.post<QuoteAttachment>(`${this.baseUrl}/${id}/attachments`, formData);
  }

  /** Removes an attachment from the quote and from storage. */
  deleteAttachment(id: number, attachmentId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}/attachments/${attachmentId}`);
  }
}
