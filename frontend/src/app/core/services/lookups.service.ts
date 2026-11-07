import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ClientSource,
  ExtraType,
  IncentiveScheme,
  Installer,
  LookupItem,
  Operator,
  Product,
  ProductCategory,
  ProductPriceList,
  ProductType,
  RepairStatusLookup,
  ServiceItem,
  VatOptionLookup,
} from '../models/lookup.model';

@Injectable({ providedIn: 'root' })
export class LookupsService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/lookups`;

  /** Client marketing sources. */
  getClientSources(): Observable<ClientSource[]> {
    return this.http.get<ClientSource[]>(`${this.baseUrl}/client-sources`);
  }

  /** Installers with their crew numbers. */
  getInstallers(): Observable<Installer[]> {
    return this.http.get<Installer[]>(`${this.baseUrl}/installers`);
  }

  /** Sales operators / agents. */
  getOperators(): Observable<Operator[]> {
    return this.http.get<Operator[]>(`${this.baseUrl}/operators`);
  }

  /** Product type classification. */
  getProductTypes(): Observable<ProductType[]> {
    return this.http.get<ProductType[]>(`${this.baseUrl}/product-types`);
  }

  /** Labour / consumables price list. */
  getServiceItems(): Observable<ServiceItem[]> {
    return this.http.get<ServiceItem[]>(`${this.baseUrl}/service-items`);
  }

  /** Extra-work line types with default prices. */
  getExtraTypes(): Observable<ExtraType[]> {
    return this.http.get<ExtraType[]>(`${this.baseUrl}/extra-types`);
  }

  /** Active incentive schemes with their calculation parameters, in display order. */
  getIncentiveSchemes(): Observable<IncentiveScheme[]> {
    return this.http.get<IncentiveScheme[]>(`${this.baseUrl}/incentive-schemes`);
  }

  /** Product price lists. */
  getProductPriceLists(): Observable<ProductPriceList[]> {
    return this.http.get<ProductPriceList[]>(`${this.baseUrl}/product-price-lists`);
  }

  /** Active HVAC units, optionally filtered by category. */
  getProducts(category?: ProductCategory): Observable<Product[]> {
    const params = category ? new HttpParams().set('category', category) : undefined;
    return this.http.get<Product[]>(`${this.baseUrl}/products`, { params });
  }

  // Phase 8c enum-replacement lookup tables — the option set / ordering / metadata is server-driven;
  // the frontend still renders labels through Transloco (keyed by `code`) to stay bilingual.
  /** VAT schemes with the blend shape and multipliers the pricing engine applies. */
  getVatOptions(): Observable<VatOptionLookup[]> {
    return this.http.get<VatOptionLookup[]>(`${this.baseUrl}/vat-options`);
  }

  /** The 10 repair states in legacy ordinal order. */
  getRepairStatuses(): Observable<RepairStatusLookup[]> {
    return this.http.get<RepairStatusLookup[]>(`${this.baseUrl}/repair-statuses`);
  }

  /** Quote lifecycle milestones. */
  getQuoteStatuses(): Observable<LookupItem[]> {
    return this.http.get<LookupItem[]>(`${this.baseUrl}/quote-statuses`);
  }

  /** Ducted-system product lines (Standard / Airzone). */
  getDuctSystemTypes(): Observable<LookupItem[]> {
    return this.http.get<LookupItem[]>(`${this.baseUrl}/duct-system-types`);
  }

  /** Quote origin desks (Store / Field). */
  getQuoteDepartments(): Observable<LookupItem[]> {
    return this.http.get<LookupItem[]>(`${this.baseUrl}/quote-departments`);
  }

  /** Installation time bands. */
  getDaySlots(): Observable<LookupItem[]> {
    return this.http.get<LookupItem[]>(`${this.baseUrl}/day-slots`);
  }
}
