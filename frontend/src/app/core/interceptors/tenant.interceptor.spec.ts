import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../../environments/environment';
import { TenantSelectionService } from '../services/tenant-selection.service';
import { tenantInterceptor } from './tenant.interceptor';

describe('tenantInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let selection: TenantSelectionService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([tenantInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    selection = TestBed.inject(TenantSelectionService);
  });

  afterEach(() => controller.verify());

  it('sends nothing until a tenant or store is selected', () => {
    http.get(`${environment.apiUrl}/clients`).subscribe();
    const headers = controller.expectOne(`${environment.apiUrl}/clients`).request.headers;
    expect(headers.has('X-Tenant-Id')).toBe(false);
    expect(headers.has('X-Store-Id')).toBe(false);
  });

  it('sends the selected tenant and store on API calls', () => {
    selection.select(7, 12);
    http.get(`${environment.apiUrl}/quotes`).subscribe();
    const headers = controller.expectOne(`${environment.apiUrl}/quotes`).request.headers;
    expect(headers.get('X-Tenant-Id')).toBe('7');
    expect(headers.get('X-Store-Id')).toBe('12');
  });

  it('sends only the tenant when every store is selected', () => {
    selection.select(7, null);
    http.get(`${environment.apiUrl}/quotes`).subscribe();
    const headers = controller.expectOne(`${environment.apiUrl}/quotes`).request.headers;
    expect(headers.get('X-Tenant-Id')).toBe('7');
    expect(headers.has('X-Store-Id')).toBe(false);
  });

  it('never tells third-party hosts which tenant is in use', () => {
    selection.select(7, 12);
    http.get('https://example.com/data').subscribe();
    expect(
      controller.expectOne('https://example.com/data').request.headers.has('X-Tenant-Id'),
    ).toBe(false);
  });
});
