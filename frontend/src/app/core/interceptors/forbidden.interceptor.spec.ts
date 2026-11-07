import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { vi } from 'vitest';
import { environment } from '../../../environments/environment';
import { SnackbarService } from '../services/snackbar.service';
import { forbiddenInterceptor } from './forbidden.interceptor';

describe('forbiddenInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  const snackbar = { error: vi.fn(), success: vi.fn(), info: vi.fn() };

  beforeEach(() => {
    snackbar.error.mockReset();
    TestBed.configureTestingModule({
      imports: [TranslocoTestingModule.forRoot({ preloadLangs: true, langs: { it: { common: { forbidden: 'Vietato' } } }, translocoConfig: { availableLangs: ['it'], defaultLang: 'it' } })],
      providers: [
        provideHttpClient(withInterceptors([forbiddenInterceptor])),
        provideHttpClientTesting(),
        { provide: SnackbarService, useValue: snackbar },
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  it('shows one snackbar for an API 403 and still propagates the error', () => {
    const error = vi.fn();
    http.post(`${environment.apiUrl}/quotes/1/confirm`, {}).subscribe({ error });
    controller.expectOne(`${environment.apiUrl}/quotes/1/confirm`).flush(null, { status: 403, statusText: 'Forbidden' });

    expect(snackbar.error).toHaveBeenCalledWith('Vietato');
    expect(error).toHaveBeenCalledTimes(1);
  });

  it('leaves other errors to the caller', () => {
    http.get(`${environment.apiUrl}/quotes/1`).subscribe({ error: () => undefined });
    controller.expectOne(`${environment.apiUrl}/quotes/1`).flush(null, { status: 404, statusText: 'Not Found' });

    expect(snackbar.error).not.toHaveBeenCalled();
  });
});
