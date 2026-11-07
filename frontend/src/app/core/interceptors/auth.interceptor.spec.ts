import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../../environments/environment';
import { AuthService } from '../services/auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let token: string | undefined;

  beforeEach(() => {
    token = 'jwt-123';
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { currentSession: () => (token ? { access_token: token } : null) } },
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('attaches the Supabase access token to API calls', () => {
    http.get(`${environment.apiUrl}/clients`).subscribe();
    expect(controller.expectOne(`${environment.apiUrl}/clients`).request.headers.get('Authorization')).toBe('Bearer jwt-123');
  });

  it('never leaks the token to third-party hosts', () => {
    http.get('https://example.com/data').subscribe();
    expect(controller.expectOne('https://example.com/data').request.headers.has('Authorization')).toBe(false);
  });

  it('sends API calls unauthenticated when signed out', () => {
    token = undefined;
    http.get(`${environment.apiUrl}/branding`).subscribe();
    expect(controller.expectOne(`${environment.apiUrl}/branding`).request.headers.has('Authorization')).toBe(false);
  });
});
