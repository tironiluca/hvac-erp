import { DiscountTier, ExtraType, Product, ServiceItem, VatOptionLookup } from '../../core/models/lookup.model';
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
  warning?: 'gasMismatch' | 'powerExceeded';
}

export interface EstimateUnitLine {
  productId: number | null;
  quantity: number;
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
}

export interface EstimateCatalog {
  products: readonly Product[];
  serviceItems: readonly ServiceItem[];
  extraTypes: readonly ExtraType[];
  vatOptions: readonly VatOptionLookup[];
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

/** Net unit price: hydronic / prepared / not-prepared list price, then the discount ladder. */
export function unitPrice(product: Product, predisposed: boolean): number {
  const listPrice = isHydronic(product)
    ? (product.hydronicPrice ?? product.price)
    : predisposed
      ? (product.pricePrepared ?? product.price)
      : (product.priceNotPrepared ?? product.price);
  return applyDiscountLadder(listPrice, product.discountTiers);
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

/** Blends VAT into machines + installation per the option's shape (mirrors the server switch). */
export function blendVat(
  vatCode: string,
  machinesPrice: number,
  installationPrice: number,
  vatOptions: readonly VatOptionLookup[],
): number {
  const vat = vatOptions.find((v) => v.code === vatCode) ?? FALLBACK_VAT[vatCode];
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

  let machinesPrice = 0;
  const externalProduct = findProduct(raw.externalUnit.productId);
  if (externalProduct) {
    machinesPrice += unitPrice(externalProduct, raw.predisposed) * raw.externalUnit.quantity;
  }
  for (const line of raw.internalUnits) {
    const product = findProduct(line.productId);
    if (product) {
      machinesPrice += unitPrice(product, raw.predisposed) * line.quantity;
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

  const blended = blendVat(raw.vat, machinesPrice, installationPrice, catalog.vatOptions);
  const finalPrice = blended + extrasFlat + ductedOutletsFlat + (raw.manualDiscount ?? 0);

  return { machinesPrice, installationPrice, finalPrice, warning: compatibilityWarning(raw, findProduct) };
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
