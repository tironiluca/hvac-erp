export interface ClientSource {
  id: number;
  description: string;
}

export interface Installer {
  id: number;
  name: string;
  mobile?: string;
  email?: string;
  department?: string;
  crew?: number;
}

export interface Operator {
  id: number;
  code: string;
  name: string;
  extension?: number;
  email?: string;
}

export interface ProductType {
  id: number;
  name: string;
  description?: string;
}

export interface ServiceItem {
  id: number;
  /** Stable key of the rows the pricing engine reads (`BASE_SYSTEM_n`, `DUCTED_*`); absent on ordinary line items. */
  code?: string | null;
  name: string;
  description?: string;
  price: number;
  productTypeId?: number;
}

export interface ExtraType {
  id: number;
  code: string;
  label: string;
  description?: string;
  defaultPrice: number;
  quantityBased: boolean;
  excludedFromVat: boolean;
  sortOrder: number;
}

export interface ProductPriceList {
  id: number;
  description: string;
  vat: number;
  markup: number;
  coversExternalCode: boolean;
}

/** A row from one of the Phase 8c enum-replacement lookup tables (`GET /api/lookups/*`). */
export interface LookupItem {
  code: string;
  label: string;
  description?: string;
  color?: string;
  sortOrder: number;
}

export interface VatOptionLookup extends LookupItem {
  blendShape: 'Split' | 'Flat' | 'None';
  machinesMultiplier: number;
  installationMultiplier: number;
  pdfLabel: string;
}

export interface RepairStatusLookup extends LookupItem {
  legacyOrdinal: number;
  isTerminal: boolean;
  requiresDirectAssistance: boolean;
}

/** One rung of a price list's cascading discount ladder. */
export interface DiscountTier {
  tierOrder: number;
  percentage: number;
}

export interface Product {
  id: number;
  priceListId: number;
  brand: string;
  model: string;
  category?: ProductCategory | string;
  ean?: string;
  maxBtu?: number;
  power?: number;
  price: number;
  hydronicPrice?: number;
  /** Hydronic unit: priced at `hydronicPrice`, exempt from the refrigerant-gas match. */
  isHydronic?: boolean;
  priceNotPrepared?: number;
  pricePrepared?: number;
  refrigerantGas?: string;
  wifi: boolean;
  active: boolean;
  stock: number;
  discountTiers: DiscountTier[];
}

/** Stored `Product.category` values (backend `ProductCategories`). */
export type ProductCategory = 'Outdoor' | 'Indoor';

/** How a quote amount is set on a quote (backend `AmountModes`): fixed €, or % of the equipment list total. */
export type AmountMode = 'Fixed' | 'Percent';

/** The built-in calculations an incentive scheme can use (backend `IncentiveKinds`). */
export type IncentiveKind = 'QuoteAmount' | 'InvoiceDiscount';

/**
 * A tax incentive, fee, invoice discount or per-line option, with its calculation parameters
 * (`GET /api/lookups/incentive-schemes`). The quote form's price preview mirrors the server
 * calculation from these fields.
 */
export interface IncentiveScheme {
  id: number;
  code: string;
  label: string;
  description?: string | null;
  kind: IncentiveKind;
  defaultAmount?: number | null;
  /** QuoteAmount: the % of the equipment list total proposed for a newly ticked scheme. InvoiceDiscount: the share the customer pays. */
  percent?: number | null;
  addToPrice: boolean;
  /** A charged quote amount goes on the machines side, before VAT (else after VAT). */
  chargeBeforeVat: boolean;
  includeInInvoiceDiscountBasis: boolean;
  requiresVat: boolean;
  requiresConfirmation: boolean;
  threshold?: number | null;
  rateBelowThreshold?: number | null;
  rateAboveThreshold?: number | null;
  roundingStep?: number | null;
  sortOrder: number;
}
