import { DiscountTier, ExtraType, IncentiveScheme, Product, ServiceItem, VatOptionLookup } from '../../core/models/lookup.model';
import { DuctSystemType } from '../../core/models/quote.model';

/**
 * Client-side live price preview for the quote wizard — a pure mirror of the server's
 * `QuotePricingService` so the summary step can show a total before the quote is saved. The
 * server stays authoritative (it re-prices on every save); keep the two in step when either changes.
 */

export interface PriceEstimate {
  machinesPrice: number;
  installationPrice: number;
  finalPrice: number;
  /** The selected invoice-discount scheme's grossed-up total and the customer's share; absent when none applies. */
  invoiceDiscount?: { schemeId: number; grossedTotal: number; customerAmount: number };
  warning?: 'gasMismatch' | 'powerExceeded';
}

export interface EstimateUnitLine {
  productId: number | null;
  quantity: number;
  /** `LineAmount` incentive schemes on the line. */
  incentiveSchemeIds?: number[];
}

export interface EstimateIncentiveLine {
  incentiveSchemeId: number;
  selected: boolean;
  amount: number | null;
}

export interface EstimateDuctedUnit {
  cloudQuantity: number;
  plenumQuantity: number;
  hasFlexibleHose: boolean;
  flexibleHoseQuantity: number;
  hasRigidHose: boolean;
  rigidHoseQuantity: number;
  remoteQuantity: number;
  outlets: { systemType: DuctSystemType; ventCount: number }[];
}

export interface EstimateExtraLine {
  extraTypeId: number;
  selected: boolean;
  quantity: number;
}

/** The subset of the quote form's raw value the estimate reads. */
export interface EstimateFormValue {
  predisposed: boolean;
  vat: string;
  manualDiscount: number | null;
  externalUnit: EstimateUnitLine;
  internalUnits: EstimateUnitLine[];
  ductedUnits: EstimateDuctedUnit[];
  serviceItems: { serviceItemId: number | null; quantity: number }[];
  extras: EstimateExtraLine[];
  incentives?: EstimateIncentiveLine[];
}

export interface EstimateCatalog {
  products: readonly Product[];
  serviceItems: readonly ServiceItem[];
  extraTypes: readonly ExtraType[];
  vatOptions: readonly VatOptionLookup[];
  incentiveSchemes?: readonly IncentiveScheme[];
}

/** Base installation service-item code for N internal units (mirrors `InstallationCostItems.BaseSystemPriceFor`). */
export function baseSystemCode(internalUnitCount: number): string {
  return `BASE_SYSTEM_${Math.min(Math.max(internalUnitCount, 1), 5)}`;
}

/**
 * Service-item codes the ducted-system accessories are priced from. They must match the backend
 * `InstallationCostItems` constants (QuotePricingService); the display names are free to change.
 */
export const DUCTED_ACCESSORY_ITEMS = {
  plenum: 'DUCTED_PLENUM',
  cloud: 'DUCTED_CLOUD_MODULE',
  flexibleHose: 'DUCTED_FLEXIBLE_HOSE_M',
  rigidHose: 'DUCTED_RIGID_HOSE_M',
  remote: 'DUCTED_REMOTE',
} as const;

/**
 * Fallback VAT rows (the seeded legacy literals) used only until `GET /api/lookups/vat-options`
 * resolves — once loaded, the server rows drive the blend so a rate change needs no frontend deploy.
 */
const FALLBACK_VAT: Record<string, Pick<VatOptionLookup, 'blendShape' | 'machinesMultiplier' | 'installationMultiplier'>> = {
  Standard22: { blendShape: 'Split', machinesMultiplier: 1.22, installationMultiplier: 2.2 },
  Reduced10: { blendShape: 'Flat', machinesMultiplier: 1.1, installationMultiplier: 1 },
  Exempt: { blendShape: 'None', machinesMultiplier: 1, installationMultiplier: 1 },
};

/**
 * Cascading discount ladder (mirrors `QuotePricingService.ApplyDiscountLadder`): each tier's
 * percentage comes off the running balance, applied in `tierOrder`.
 */
export function applyDiscountLadder(listPrice: number, tiers: readonly DiscountTier[] | undefined): number {
  if (!tiers?.length) return listPrice;
  return [...tiers]
    .sort((a, b) => a.tierOrder - b.tierOrder)
    .reduce((net, tier) => net * (1 - tier.percentage / 100), listPrice);
}

const isHydronic = (product: Product) => product.isHydronic === true;

/** List unit price: hydronic / prepared / not-prepared. */
export function listUnitPrice(product: Product, predisposed: boolean): number {
  return isHydronic(product)
    ? (product.hydronicPrice ?? product.price)
    : predisposed
      ? (product.pricePrepared ?? product.price)
      : (product.priceNotPrepared ?? product.price);
}

/** Net unit price: the list price through the discount ladder. */
export function unitPrice(product: Product, predisposed: boolean): number {
  return applyDiscountLadder(listUnitPrice(product, predisposed), product.discountTiers);
}

const round2 = (value: number) => Math.round(value * 100) / 100;

/** `LineAmount` scheme (mirrors `QuotePricingService.LineAmount`): fixed per unit, else % of the list unit price per unit. */
export function lineSchemeAmount(scheme: IncentiveScheme, listUnit: number, quantity: number): number {
  return round2((scheme.defaultAmount ?? (listUnit * (scheme.percent ?? 0)) / 100) * quantity);
}

/**
 * `InvoiceDiscount` scheme (mirrors `QuotePricingService.InvoiceDiscountTotal`): the basis grossed up
 * by the below/above-threshold rate, then raised to the next rounding step (a full step even on an
 * exact multiple, as legacy did).
 */
export function invoiceDiscountTotal(scheme: IncentiveScheme, basis: number): number {
  const rate = basis < (scheme.threshold ?? 0) ? (scheme.rateBelowThreshold ?? 1) : (scheme.rateAboveThreshold ?? 1);
  const grossed = basis * rate;
  const step = scheme.roundingStep ?? 1;
  return grossed + (step - (grossed % step));
}

/** Per-outlet ducted price; the ×1.1 is embedded VAT, so callers add it after the blend. */
export function ductedOutletPrice(systemType: DuctSystemType, ventCount: number): number {
  switch (systemType) {
    case 'Standard':
      return (1030 + (ventCount - 2) * 190) * 1.1;
    case 'Airzone':
      return (2400 + (ventCount - 2) * 460) * 1.1;
    default:
      return 0;
  }
}

function resolveVat(vatCode: string, vatOptions: readonly VatOptionLookup[]) {
  return vatOptions.find((v) => v.code === vatCode) ?? FALLBACK_VAT[vatCode];
}

/** Blends VAT into machines + installation per the option's shape (mirrors the server switch). */
export function blendVat(
  vatCode: string,
  machinesPrice: number,
  installationPrice: number,
  vatOptions: readonly VatOptionLookup[],
): number {
  const vat = resolveVat(vatCode, vatOptions);
  switch (vat?.blendShape) {
    case 'Split':
      return installationPrice * vat.installationMultiplier + (machinesPrice - installationPrice) * vat.machinesMultiplier;
    case 'Flat':
      return (machinesPrice + installationPrice) * vat.machinesMultiplier;
    default:
      return machinesPrice + installationPrice;
  }
}

export function estimateQuotePrice(raw: EstimateFormValue, catalog: EstimateCatalog): PriceEstimate {
  const findProduct = (id: number | null) => catalog.products.find((p) => p.id === id);
  const findServicePrice = (code: string) => catalog.serviceItems.find((s) => s.code === code)?.price ?? 0;
  const extraTypeById = new Map(catalog.extraTypes.map((t) => [t.id, t]));
  const schemeById = new Map((catalog.incentiveSchemes ?? []).map((s) => [s.id, s]));

  let machinesPrice = 0;
  for (const line of [raw.externalUnit, ...raw.internalUnits]) {
    const product = findProduct(line.productId);
    if (!product) continue;
    machinesPrice += unitPrice(product, raw.predisposed) * line.quantity;
    // Line schemes (e.g. a warranty) charged on the machines side, before the VAT blend.
    for (const schemeId of line.incentiveSchemeIds ?? []) {
      const scheme = schemeById.get(schemeId);
      if (scheme?.kind === 'LineAmount' && scheme.addToPrice) {
        machinesPrice += lineSchemeAmount(scheme, listUnitPrice(product, raw.predisposed), line.quantity);
      }
    }
  }

  // Ducted-system accessories are priced from their service-item rows and added to the machines
  // side of the blend (mirrors QuotePricingService.PriceDuctedAccessoriesAsync).
  for (const d of raw.ductedUnits) {
    machinesPrice += (d.plenumQuantity ?? 0) * findServicePrice(DUCTED_ACCESSORY_ITEMS.plenum);
    machinesPrice += (d.cloudQuantity ?? 0) * findServicePrice(DUCTED_ACCESSORY_ITEMS.cloud);
    machinesPrice += (d.remoteQuantity ?? 0) * findServicePrice(DUCTED_ACCESSORY_ITEMS.remote);
    if (d.hasFlexibleHose) {
      machinesPrice += (d.flexibleHoseQuantity ?? 0) * findServicePrice(DUCTED_ACCESSORY_ITEMS.flexibleHose);
    }
    if (d.hasRigidHose) {
      machinesPrice += (d.rigidHoseQuantity ?? 0) * findServicePrice(DUCTED_ACCESSORY_ITEMS.rigidHose);
    }
  }

  let installationPrice = 0;
  const internalUnitCount = raw.internalUnits.filter((l) => l.productId).length;
  if (internalUnitCount > 0) {
    installationPrice += findServicePrice(baseSystemCode(internalUnitCount));
  }

  let ductedOutletsFlat = 0;
  for (const d of raw.ductedUnits) {
    for (const o of d.outlets) {
      ductedOutletsFlat += ductedOutletPrice(o.systemType, o.ventCount);
    }
  }

  for (const line of raw.serviceItems) {
    if (!line.serviceItemId) continue;
    const item = catalog.serviceItems.find((s) => s.id === line.serviceItemId);
    installationPrice += (item?.price ?? 0) * (line.quantity ?? 0);
  }

  // Extra-work lines: billed inside the VAT blend unless flagged excludedFromVat (legacy WiFi module).
  let extrasFlat = 0;
  for (const line of raw.extras) {
    const type = extraTypeById.get(line.extraTypeId);
    if (!type) continue;
    const quantity = type.quantityBased ? (line.quantity ?? 0) : line.selected ? 1 : 0;
    if (quantity <= 0) continue;
    const amount = quantity * type.defaultPrice;
    if (type.excludedFromVat) {
      extrasFlat += amount;
    } else {
      installationPrice += amount;
    }
  }

  const selectedIncentives = (raw.incentives ?? [])
    .filter((i) => i.selected)
    .map((i) => ({ line: i, scheme: schemeById.get(i.incentiveSchemeId) }))
    .filter((i): i is { line: EstimateIncentiveLine; scheme: IncentiveScheme } => !!i.scheme);
  const quoteAmount = (i: { line: EstimateIncentiveLine; scheme: IncentiveScheme }) => i.line.amount ?? i.scheme.defaultAmount ?? 0;
  const quoteAmounts = selectedIncentives.filter((i) => i.scheme.kind === 'QuoteAmount');

  const blended = blendVat(raw.vat, machinesPrice, installationPrice, catalog.vatOptions);
  const charged = quoteAmounts.filter((i) => i.scheme.addToPrice).reduce((sum, i) => sum + quoteAmount(i), 0);
  const finalPrice = blended + extrasFlat + ductedOutletsFlat + charged + (raw.manualDiscount ?? 0);

  // Invoice discount: basis = final price with the manual discount added back, plus the quote
  // amounts flagged into the basis (mirrors QuotePricingService.PriceInvoiceDiscounts).
  let invoiceDiscount: PriceEstimate['invoiceDiscount'];
  const discount = selectedIncentives.find((i) => i.scheme.kind === 'InvoiceDiscount');
  const vatExempt = resolveVat(raw.vat, catalog.vatOptions)?.blendShape === 'None';
  if (discount && !(discount.scheme.requiresVat && vatExempt)) {
    const basis =
      finalPrice -
      (raw.manualDiscount ?? 0) +
      quoteAmounts.filter((i) => i.scheme.includeInInvoiceDiscountBasis).reduce((sum, i) => sum + quoteAmount(i), 0);
    const grossedTotal = invoiceDiscountTotal(discount.scheme, basis);
    invoiceDiscount = {
      schemeId: discount.scheme.id,
      grossedTotal,
      customerAmount: round2((grossedTotal * (discount.scheme.percent ?? 100)) / 100),
    };
  }

  return { machinesPrice, installationPrice, finalPrice, invoiceDiscount, warning: compatibilityWarning(raw, findProduct) };
}

/** Mirrors `QuotePricingService.ValidateCompatibility` as a soft warning instead of a 400. */
function compatibilityWarning(
  raw: EstimateFormValue,
  findProduct: (id: number | null) => Product | undefined,
): PriceEstimate['warning'] {
  const external = findProduct(raw.externalUnit.productId);
  if (!external || isHydronic(external)) return undefined;

  const internals = raw.internalUnits.map((l) => findProduct(l.productId)).filter((p): p is Product => !!p);
  const externalGas = external.refrigerantGas?.toLowerCase();
  if (externalGas && internals.some((p) => p.refrigerantGas && p.refrigerantGas.toLowerCase() !== externalGas)) {
    return 'gasMismatch';
  }
  if (external.maxBtu != null) {
    const totalPower = internals.reduce((sum, p) => sum + (p.power ?? 0), 0);
    if (totalPower > external.maxBtu) return 'powerExceeded';
  }
  return undefined;
}
