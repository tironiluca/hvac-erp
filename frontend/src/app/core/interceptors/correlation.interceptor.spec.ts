import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../../environments/environment';
import { CORRELATION_HEADER, correlationInterceptor } from './correlation.interceptor';

describe('correlationInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([correlationInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('tags each API call with a fresh id the server accepts', () => {
    http.get(`${environment.apiUrl}/quotes`).subscribe();
    http.get(`${environment.apiUrl}/quotes`).subscribe();
    const [a, b] = controller.match(`${environment.apiUrl}/quotes`).map((r) => r.request.headers.get(CORRELATION_HEADER));

    expect(a).toMatch(/^spa-[0-9a-f]{32}$/);
    expect(b).not.toBe(a);
  });

  it('leaves third-party calls untouched', () => {
    http.get('https://example.com/x').subscribe();
    expect(controller.expectOne('https://example.com/x').request.headers.has(CORRELATION_HEADER)).toBe(false);
  });
});
