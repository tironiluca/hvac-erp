import { IncentiveScheme, Product, ServiceItem, VatOptionLookup } from '../../core/models/lookup.model';
import {
  applyDiscountLadder,
  baseSystemCode,
  blendVat,
  defaultQuoteAmount,
  DUCTED_ACCESSORY_ITEMS,
  EstimateCatalog,
  EstimateFormValue,
  estimateQuotePrice,
} from './quote-price-estimate';

// Cases mirror backend QuotePricingServiceTests so the preview and the server engine stay aligned.

function product(id: number, price: number, extra: Partial<Product> = {}): Product {
  return { id, priceListId: 1, brand: 'Daikin', model: `M${id}`, price, wifi: false, active: true, stock: 0, discountTiers: [], refrigerantGas: 'R32', ...extra };
}

const serviceItems: ServiceItem[] = [
  { id: 1, code: 'BASE_SYSTEM_1', name: 'Single split', price: 400 },
  { id: 2, code: 'BASE_SYSTEM_2', name: 'Dual split', price: 600 },
  { id: 3, code: 'BASE_SYSTEM_3', name: 'Trial split', price: 800 },
  { id: 4, code: 'BASE_SYSTEM_4', name: 'Quad split', price: 1000 },
  { id: 5, code: 'BASE_SYSTEM_5', name: 'Penta split', price: 1200 },
  { id: 6, code: DUCTED_ACCESSORY_ITEMS.plenum, name: 'Ducted plenum', price: 120 },
  { id: 7, code: DUCTED_ACCESSORY_ITEMS.flexibleHose, name: 'Flexible hose (m)', price: 22 },
  { id: 8, name: 'Wall bracket', price: 40 },
];

const vatOptions: VatOptionLookup[] = [
  { code: 'Standard22', label: '22%', sortOrder: 1, blendShape: 'Split', machinesMultiplier: 1.22, installationMultiplier: 2.2, pdfLabel: '' },
  { code: 'Reduced10', label: '10%', sortOrder: 2, blendShape: 'Flat', machinesMultiplier: 1.1, installationMultiplier: 1, pdfLabel: '' },
  { code: 'Exempt', label: 'Esente', sortOrder: 3, blendShape: 'None', machinesMultiplier: 1, installationMultiplier: 1, pdfLabel: '' },
];

const external = product(1, 1000, { maxBtu: 10000 });
const internal = product(2, 300, { power: 2500 });

function catalog(extra: Partial<EstimateCatalog> = {}): EstimateCatalog {
  return { products: [external, internal], serviceItems, extraTypes: [], vatOptions, ...extra };
}

function form(extra: Partial<EstimateFormValue> = {}): EstimateFormValue {
  return {
    predisposed: true,
    vat: 'Exempt',
    manualDiscount: 0,
    externalUnit: { productId: external.id, quantity: 1 },
    internalUnits: [{ productId: internal.id, quantity: 1 }],
    ductedUnits: [],
    serviceItems: [],
    extras: [],
    ...extra,
  };
}

describe('estimateQuotePrice', () => {
  it('passes machines + installation through untouched when VAT-exempt', () => {
    expect(estimateQuotePrice(form(), catalog())).toEqual({
      machinesPrice: 1300,
      installationPrice: 400,
      finalPrice: 1700,
      incentiveAmounts: {},
      invoiceDiscount: undefined,
      warning: undefined,
    });
  });

  it('applies the Split blend from the VAT lookup row', () => {
    expect(estimateQuotePrice(form({ vat: 'Standard22' }), catalog()).finalPrice).toBeCloseTo(1978, 6);
  });

  it('applies the Flat blend from the VAT lookup row', () => {
    expect(estimateQuotePrice(form({ vat: 'Reduced10' }), catalog()).finalPrice).toBeCloseTo(1870, 6);
  });

  it('follows edited VAT multipliers rather than hard-coded rates', () => {
    const edited = vatOptions.map((v) => (v.code === 'Reduced10' ? { ...v, machinesMultiplier: 1.05 } : v));
    expect(estimateQuotePrice(form({ vat: 'Reduced10' }), catalog({ vatOptions: edited })).finalPrice).toBeCloseTo(1785, 6);
  });

  it('falls back to the seeded rates before the VAT lookup has loaded', () => {
    expect(estimateQuotePrice(form({ vat: 'Standard22' }), catalog({ vatOptions: [] })).finalPrice).toBeCloseTo(1978, 6);
  });

  it('adds ducted outlets flat after the blend and accessories on the machines side', () => {
    const result = estimateQuotePrice(
      form({
        vat: 'Standard22',
        internalUnits: [],
        ductedUnits: [
          {
            cloudQuantity: 0,
            plenumQuantity: 2,
            hasFlexibleHose: true,
            flexibleHoseQuantity: 5,
            hasRigidHose: false,
            rigidHoseQuantity: 99,
            remoteQuantity: 0,
            outlets: [
              { systemType: 'Standard', ventCount: 4 },
              { systemType: 'Airzone', ventCount: 3 },
            ],
          },
        ],
      }),
      catalog(),
    );

    expect(result.machinesPrice).toBe(1000 + 240 + 110);
    expect(result.finalPrice).toBeCloseTo(1350 * 1.22 + 1551 + 3146, 6);
  });

  it('splits extras between the VAT-blended installation and flat lines', () => {
    const result = estimateQuotePrice(
      form({
        vat: 'Reduced10',
        internalUnits: [],
        extras: [
          { extraTypeId: 10, selected: false, quantity: 3 },
          { extraTypeId: 11, selected: true, quantity: 0 },
          { extraTypeId: 12, selected: false, quantity: 0 },
        ],
      }),
      catalog({
        extraTypes: [
          { id: 10, code: 'WIRING', label: 'Wiring', defaultPrice: 20, quantityBased: true, excludedFromVat: false, sortOrder: 1 },
          { id: 11, code: 'WIFI', label: 'WiFi', defaultPrice: 50, quantityBased: false, excludedFromVat: true, sortOrder: 2 },
          { id: 12, code: 'LADDER', label: 'Ladder', defaultPrice: 999, quantityBased: false, excludedFromVat: false, sortOrder: 3 },
        ],
      }),
    );

    expect(result.installationPrice).toBe(60);
    expect(result.finalPrice).toBeCloseTo(1060 * 1.1 + 50, 6);
  });

  it('adds itemised service lines and the manual discount', () => {
    const result = estimateQuotePrice(
      form({ internalUnits: [], manualDiscount: -100, serviceItems: [{ serviceItemId: 8, quantity: 2.5 }, { serviceItemId: null, quantity: 9 }] }),
      catalog(),
    );

    expect(result.installationPrice).toBe(100);
    expect(result.finalPrice).toBe(1000);
  });

  it('uses the not-prepared price when the site is not predisposed', () => {
    const p = product(3, 999, { pricePrepared: 800, priceNotPrepared: 900 });
    const base = form({ externalUnit: { productId: 3, quantity: 1 }, internalUnits: [] });

    expect(estimateQuotePrice({ ...base, predisposed: true }, catalog({ products: [p] })).machinesPrice).toBe(800);
    expect(estimateQuotePrice({ ...base, predisposed: false }, catalog({ products: [p] })).machinesPrice).toBe(900);
  });

  it('warns on refrigerant mismatch and on exceeded capacity', () => {
    const otherGas = product(4, 300, { refrigerantGas: 'R410A' });
    const big = product(5, 300, { power: 9000 });

    expect(estimateQuotePrice(form({ internalUnits: [{ productId: 4, quantity: 1 }] }), catalog({ products: [external, otherGas] })).warning).toBe('gasMismatch');
    expect(
      estimateQuotePrice(form({ internalUnits: [{ productId: 5, quantity: 1 }, { productId: 2, quantity: 1 }] }), catalog({ products: [external, internal, big] }))
        .warning,
    ).toBe('powerExceeded');
  });

  it('never warns for hydronic external units', () => {
    const hydronic = product(6, 5000, { brand: 'Acme Hydronic', isHydronic: true, hydronicPrice: 4000, refrigerantGas: 'Water', maxBtu: 1 });
    const result = estimateQuotePrice(form({ externalUnit: { productId: 6, quantity: 1 } }), catalog({ products: [hydronic, internal] }));

    expect(result.warning).toBeUndefined();
    expect(result.machinesPrice).toBe(4300);
  });
});

describe('pricing helpers', () => {
  it('cascades discount tiers on the running balance in tier order', () => {
    expect(applyDiscountLadder(1000, [{ tierOrder: 2, percentage: 5 }, { tierOrder: 1, percentage: 10 }])).toBeCloseTo(855, 6);
    expect(applyDiscountLadder(1000, undefined)).toBe(1000);
  });

  it('maps internal-unit count to the base system service-item code', () => {
    expect([0, 1, 2, 3, 4, 5, 9].map(baseSystemCode)).toEqual([
      'BASE_SYSTEM_1', 'BASE_SYSTEM_1', 'BASE_SYSTEM_2', 'BASE_SYSTEM_3', 'BASE_SYSTEM_4', 'BASE_SYSTEM_5', 'BASE_SYSTEM_5',
    ]);
  });

  it('passes totals through for an unknown VAT code', () => {
    expect(blendVat('Nope', 100, 50, vatOptions)).toBe(150);
  });
});

// Mirrors the backend QuotePricingServiceTests incentive cases.
describe('incentive schemes', () => {
  const base = { description: null, addToPrice: false, chargeBeforeVat: false, includeInInvoiceDiscountBasis: false, requiresVat: false, requiresConfirmation: false, sortOrder: 0 };
  const warranty: IncentiveScheme = { ...base, id: 40, code: 'WARRANTY', label: 'Warranty', kind: 'QuoteAmount', percent: 8, defaultAmount: 100, addToPrice: true, chargeBeforeVat: true };
  const deduction: IncentiveScheme = { ...base, id: 10, code: 'DEDUCTION', label: 'Deduction', kind: 'QuoteAmount', includeInInvoiceDiscountBasis: true };
  const fee: IncentiveScheme = { ...base, id: 20, code: 'FEE', label: 'Fee', kind: 'QuoteAmount', defaultAmount: 80, addToPrice: true };
  const discount: IncentiveScheme = {
    ...base, id: 30, code: 'DISCOUNT', label: 'Invoice discount', kind: 'InvoiceDiscount', percent: 50,
    threshold: 2000, rateBelowThreshold: 1.3, rateAboveThreshold: 1.2, roundingStep: 50, requiresVat: true, requiresConfirmation: true,
  };
  const schemes = catalog({ incentiveSchemes: [warranty, deduction, fee, discount] });
  const pick = (id: number, value: number | null = null, mode: 'Fixed' | 'Percent' | null = null) => ({ incentiveSchemeId: id, selected: true, mode, value });

  it('takes a percentage warranty on the equipment list total, once per quote, before VAT', () => {
    const result = estimateQuotePrice(form({ internalUnits: [{ productId: internal.id, quantity: 2 }], incentives: [pick(warranty.id)] }), schemes);

    // Equipment list 1000 + 2 × 300 = 1600 → default 8% = 128 on the machines side; base install 400.
    expect(result.incentiveAmounts).toEqual({ [warranty.id]: 128 });
    expect(result.machinesPrice).toBe(1728);
    expect(result.finalPrice).toBe(2128);
  });

  it('charges a fixed warranty once for the whole quote, whatever the quantity', () => {
    const result = estimateQuotePrice(
      form({ internalUnits: [{ productId: internal.id, quantity: 2 }], incentives: [pick(warranty.id, 100, 'Fixed')] }),
      schemes,
    );

    expect(result.machinesPrice).toBe(1600 + 100);
  });

  it('starts a newly ticked scheme from its default: the % when it has one, else the € amount', () => {
    expect(defaultQuoteAmount(warranty)).toEqual({ mode: 'Percent', value: 8 });
    expect(defaultQuoteAmount(fee)).toEqual({ mode: 'Fixed', value: 80 });
  });

  it('charges a quote amount after VAT, defaulting to the scheme amount', () => {
    const result = estimateQuotePrice(form({ vat: 'Reduced10', internalUnits: [], incentives: [pick(fee.id)] }), schemes);

    // 1000 × 1.1 = 1100, + 80 (no indoor unit, so no base installation).
    expect(result.finalPrice).toBeCloseTo(1180, 6);
  });

  it('grosses the invoice discount from the basis, adding back the manual discount and flagged amounts', () => {
    const result = estimateQuotePrice(
      form({ vat: 'Reduced10', internalUnits: [], manualDiscount: -200, incentives: [pick(discount.id), pick(deduction.id, 100, 'Fixed')] }),
      catalog({ products: [product(1, 2000)], incentiveSchemes: [deduction, discount] }),
    );

    // Final = 2200 − 200 = 2000; basis = 2000 + 200 + 100 = 2300 ≥ 2000 → × 1.2 = 2760 → 2800; customer pays 50%.
    expect(result.finalPrice).toBe(2000);
    expect(result.invoiceDiscount).toEqual({ schemeId: discount.id, grossedTotal: 2800, customerAmount: 1400 });
  });

  it('adds a full rounding step on an exact multiple, like legacy', () => {
    const result = estimateQuotePrice(
      form({ vat: 'Reduced10', internalUnits: [], incentives: [pick(discount.id)] }),
      catalog({ products: [product(1, 2500)], incentiveSchemes: [discount] }),
    );

    // 2500 × 1.1 = 2750 ≥ 2000 → × 1.2 = 3300 (exact) → 3350.
    expect(result.invoiceDiscount?.grossedTotal).toBeCloseTo(3350, 6);
  });

  it('offers no invoice discount on a VAT-exempt quote when the scheme needs VAT', () => {
    expect(estimateQuotePrice(form({ incentives: [pick(discount.id)] }), schemes).invoiceDiscount).toBeUndefined();
  });
});
