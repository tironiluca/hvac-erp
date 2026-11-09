import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../../environments/environment';
import { QuotesService } from './quotes.service';

describe('QuotesService', () => {
  let service: QuotesService;
  let http: HttpTestingController;
  const base = `${environment.apiUrl}/quotes`;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(QuotesService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists quotes, optionally filtered by client', () => {
    service.getList().subscribe();
    expect(http.expectOne(base).request.params.keys()).toEqual([]);

    service.getList(7).subscribe();
    const req = http.expectOne((r) => r.url === base);
    expect(req.request.params.get('clientId')).toBe('7');
  });

  it('posts workflow transitions to their dedicated endpoints', () => {
    service.markSurveyDone(1).subscribe();
    service.recall(1, { notes: 'later' }).subscribe();
    service.confirm(1).subscribe();
    service.toggleIncentiveConfirmation(1, 3).subscribe();

    for (const [path, body] of [
      ['survey', {}],
      ['recall', { notes: 'later' }],
      ['confirm', {}],
      ['incentives/3/confirmation', {}],
    ] as const) {
      const req = http.expectOne(`${base}/1/${path}`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(body);
      req.flush(null);
    }
  });

  it('uploads attachments as multipart form data', () => {
    const file = new File(['x'], 'photo.jpg', { type: 'image/jpeg' });
    service.uploadAttachment(3, file).subscribe();

    const req = http.expectOne(`${base}/3/attachments`);
    expect(req.request.body).toBeInstanceOf(FormData);
    expect((req.request.body as FormData).get('file')).toBeInstanceOf(File);
  });

  it('deletes quotes and attachments', () => {
    service.delete(4).subscribe();
    service.deleteAttachment(4, 9).subscribe();

    expect(http.expectOne(`${base}/4`).request.method).toBe('DELETE');
    expect(http.expectOne(`${base}/4/attachments/9`).request.method).toBe('DELETE');
  });

  it('reads and triggers the ERP export of a quote', () => {
    let status: string | undefined;
    service.getErpExport(4).subscribe((e) => (status = e?.status));
    service.sendToErp(4).subscribe();

    http.expectOne({ method: 'GET', url: `${base}/4/erp-export` }).flush({ quoteId: 4, status: 'Sent' });
    const send = http.expectOne({ method: 'POST', url: `${base}/4/erp-export` });
    send.flush({ quoteId: 4, status: 'Sent' });

    expect(status).toBe('Sent');
  });
});
