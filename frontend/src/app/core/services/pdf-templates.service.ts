import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  PdfDocumentType,
  PdfTemplate,
  PdfTemplateCatalog,
  PdfTemplateDefinition,
  PdfTemplateListItem,
  UpsertPdfTemplateRequest,
} from '../models/pdf-template.model';

/**
 * CRUD + activation + live-preview for the admin-editable PDF layout templates
 * (`/api/pdf-templates`, admin-only). `preview()` posts an unsaved definition and gets back a PDF
 * blob the editor renders in an iframe.
 */
@Injectable({ providedIn: 'root' })
export class PdfTemplatesService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/pdf-templates`;

  /** Lists templates, optionally for one document type. */
  list(documentType?: PdfDocumentType): Observable<PdfTemplateListItem[]> {
    const params = documentType ? new HttpParams().set('documentType', documentType) : undefined;
    return this.http.get<PdfTemplateListItem[]>(this.baseUrl, { params });
  }

  /** Sections, columns and options the editor may use for a document type. */
  catalog(documentType: PdfDocumentType): Observable<PdfTemplateCatalog> {
    return this.http.get<PdfTemplateCatalog>(`${this.baseUrl}/catalog`, {
      params: new HttpParams().set('documentType', documentType),
    });
  }

  /** Gets one template with its full definition. */
  get(id: number): Observable<PdfTemplate> {
    return this.http.get<PdfTemplate>(`${this.baseUrl}/${id}`);
  }

  /** Creates a template (validated server-side against the catalog). */
  create(request: UpsertPdfTemplateRequest): Observable<PdfTemplate> {
    return this.http.post<PdfTemplate>(this.baseUrl, request);
  }

  /** Replaces a template's name and definition. */
  update(id: number, request: UpsertPdfTemplateRequest): Observable<PdfTemplate> {
    return this.http.put<PdfTemplate>(`${this.baseUrl}/${id}`, request);
  }

  /** Copies a template under a new name. */
  duplicate(id: number, name: string): Observable<PdfTemplate> {
    return this.http.post<PdfTemplate>(`${this.baseUrl}/${id}/duplicate`, { name });
  }

  /** Makes the template the active one for its document type. */
  activate(id: number): Observable<PdfTemplate> {
    return this.http.post<PdfTemplate>(`${this.baseUrl}/${id}/activate`, {});
  }

  /** Resets the template's definition to the built-in defaults. */
  restoreDefaults(id: number): Observable<PdfTemplate> {
    return this.http.post<PdfTemplate>(`${this.baseUrl}/${id}/restore-defaults`, {});
  }

  /** Deletes a template that is neither active nor a system default. */
  remove(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Renders an unsaved definition against sample data; resolves to the PDF blob. */
  preview(documentType: PdfDocumentType, definition: PdfTemplateDefinition): Observable<Blob> {
    return this.http.post(
      `${this.baseUrl}/preview`,
      { documentType, definition },
      { responseType: 'blob' },
    );
  }
}
