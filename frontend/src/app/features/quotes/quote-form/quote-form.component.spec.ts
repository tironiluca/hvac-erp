import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ClientsService } from '../../../core/services/clients.service';
import { LookupsService } from '../../../core/services/lookups.service';
import { QuotesService } from '../../../core/services/quotes.service';
import { SnackbarService } from '../../../core/services/snackbar.service';
import { Product, VatOptionLookup } from '../../../core/models/lookup.model';
import { QuoteDetail } from '../../../core/models/quote.model';
import { QuoteFormComponent } from './quote-form.component';

const external: Product = { id: 1, priceListId: 1, brand: 'Daikin', model: 'EXT', price: 1000, wifi: false, active: true, stock: 0, discountTiers: [], refrigerantGas: 'R32' };
const internal: Product = { ...external, id: 2, model: 'INT', price: 300, power: 2500 };
const vatOptions: VatOptionLookup[] = [
  { code: 'Standard22', label: '22%', sortOrder: 1, blendShape: 'Split', machinesMultiplier: 1.22, installationMultiplier: 2.2, pdfLabel: '' },
  { code: 'Exempt', label: 'Esente', sortOrder: 2, blendShape: 'None', machinesMultiplier: 1, installationMultiplier: 1, pdfLabel: '' },
];
const client = { id: 9, code: 'CP0009', fullName: 'Mario Rossi' };

function detail(overrides: Partial<QuoteDetail> = {}): QuoteDetail {
  return {
    id: 5, number: 12, version: 1, revision: 0, clientId: 9, clientName: 'Mario Rossi', department: 'Store',
    createdAt: '2026-01-01T00:00:00Z', currentStatus: 'Draft', surveyDone: false, recalled: false, confirmed: false,
    predisposed: true, vat: 'Exempt', manualDiscount: 0, machinesPrice: 1000, installationPrice: 0, rounding: 0,
    finalPrice: 1000, eneaEligible: false, raeeEligible: false, sfRequested: false, sfConfirmed: false,
    products: [{ id: 1, productId: 1, brand: 'Daikin', model: 'EXT', role: 'ExternalUnit', quantity: 1, price: 1000, discountedPrice: 1000, warranty: false }],
    ductedUnits: [], serviceItems: [], extras: [], statusEvents: [], attachments: [],
    ...overrides,
  } as QuoteDetail;
}

describe('QuoteFormComponent', () => {
  let quotes: { getById: ReturnType<typeof vi.fn>; create: ReturnType<typeof vi.fn>; update: ReturnType<typeof vi.fn> };

  function setup(params: Record<string, string> = {}, loaded: QuoteDetail = detail()) {
    quotes = {
      getById: vi.fn(() => of(loaded)),
      create: vi.fn(() => of(detail({ id: 77 }))),
      update: vi.fn(() => of(detail())),
    };
    TestBed.configureTestingModule({
      imports: [QuoteFormComponent, TranslocoTestingModule.forRoot({ langs: { it: {} }, translocoConfig: { availableLangs: ['it'], defaultLang: 'it' } })],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(params), queryParamMap: convertToParamMap({}) } } },
        { provide: QuotesService, useValue: quotes },
        { provide: ClientsService, useValue: { getById: () => of(client), search: () => of([]) } },
        { provide: SnackbarService, useValue: { success: vi.fn(), error: vi.fn(), info: vi.fn() } },
        {
          provide: LookupsService,
          useValue: {
            getOperators: () => of([]),
            getProducts: (kind: string) => of(kind === 'Outdoor' ? [external] : [internal]),
            getServiceItems: () => of([{ id: 1, code: 'BASE_SYSTEM_1', name: 'Single split', price: 400 }]),
            getVatOptions: () => of(vatOptions),
            getQuoteDepartments: () => of([]),
            getDuctSystemTypes: () => of([]),
            getExtraTypes: () => of([]),
          },
        },
      ],
    });
    // Constructed but not rendered: the logic under test is form state + pricing, not the template.
    return TestBed.createComponent(QuoteFormComponent).componentInstance;
  }

  it('previews the price live from the form and the server-provided lookups', () => {
    const component = setup();
    component['form'].controls.vat.setValue('Standard22');
    component['form'].controls.externalUnit.controls.productId.setValue(1);
    component['addInternalUnit']();
    component['form'].controls.internalUnits.at(0).controls.productId.setValue(2);

    const estimate = component['estimate']();
    expect(estimate.machinesPrice).toBe(1300);
    expect(estimate.installationPrice).toBe(400);
    expect(estimate.finalPrice).toBeCloseTo(1978, 6);
  });

  it('does not submit without a selected client', async () => {
    const component = setup();
    component['form'].controls.externalUnit.controls.productId.setValue(1);

    await component['submit']();

    expect(quotes.create).not.toHaveBeenCalled();
  });

  it('creates the quote with mapped product lines and navigates to it', async () => {
    const component = setup();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    component['clientControl'].setValue(client as never);
    component['form'].controls.vat.setValue('Exempt');
    component['form'].controls.externalUnit.controls.productId.setValue(1);

    await component['submit']();

    expect(quotes.create).toHaveBeenCalledTimes(1);
    const request = quotes.create.mock.calls[0][0];
    expect(request.clientId).toBe(9);
    expect(request.products).toEqual([{ productId: 1, role: 'ExternalUnit', quantity: 1, warranty: false }]);
    expect(navigate).toHaveBeenCalledWith(['/quotes', 77], { replaceUrl: true });
  });

  it('loads an existing draft quote into an editable form', () => {
    const component = setup({ id: '5' });

    expect(quotes.getById).toHaveBeenCalledWith(5);
    expect(component['form'].controls.externalUnit.controls.productId.value).toBe(1);
    expect(component['form'].disabled).toBe(false);
  });

  it('locks the form when editing a confirmed quote', () => {
    const component = setup({ id: '5' }, detail({ currentStatus: 'Confirmed', confirmed: true }));

    expect(component['form'].disabled).toBe(true);
    expect(component['clientControl'].disabled).toBe(true);
  });
});
