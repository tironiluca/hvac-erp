import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { TenantSelectionService } from '../services/tenant-selection.service';

/** Tells the API which tenant and store this session works in (`X-Tenant-Id` / `X-Store-Id`). */
export const tenantInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(environment.apiUrl)) {
    return next(req);
  }

  const selection = inject(TenantSelectionService);
  const headers: Record<string, string> = {};
  const tenantId = selection.tenantId();
  const storeId = selection.storeId();
  if (tenantId !== null) {
    headers['X-Tenant-Id'] = String(tenantId);
  }
  if (storeId !== null) {
    headers['X-Store-Id'] = String(storeId);
  }

  return Object.keys(headers).length ? next(req.clone({ setHeaders: headers })) : next(req);
};
