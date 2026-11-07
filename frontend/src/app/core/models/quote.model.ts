import { IncentiveKind } from './lookup.model';
import { FloorPlanSketch } from './floor-plan-sketch.model';

export type QuoteDepartment = 'Store' | 'Field';
export type VatOption = 'Standard22' | 'Reduced10' | 'Exempt';
export type ProductRole = 'ExternalUnit' | 'InternalUnit';
export type DuctSystemType = 'Standard' | 'Airzone';
export type QuoteStatus = 'Draft' | 'SurveyDone' | 'Recalled' | 'Confirmed';

export interface QuoteListItem {
  id: number;
  number: number;
  version: number;
  revision: number;
  clientId: number;
  clientName: string;
  clientCode: string;
  department: QuoteDepartment;
  createdAt: string;
  currentStatus: QuoteStatus;
  finalPrice: number;
}

export interface QuoteProduct {
  id: number;
  productId: number;
  brand: string;
  model: string;
  role: ProductRole;
  quantity: number;
  price: number;
  discountedPrice: number;
  incentives: QuoteProductIncentive[];
}

/** A line-level incentive (e.g. an extended warranty) with its server-computed amount. */
export interface QuoteProductIncentive {
  incentiveSchemeId: number;
  code: string;
  label: string;
  amount: number;
}

/**
 * A quote-level incentive. `amount` is the entered amount, or for an invoice discount the
 * grossed-up total; `customerAmount` is what the customer pays under an invoice discount.
 */
export interface QuoteIncentive {
  incentiveSchemeId: number;
  code: string;
  label: string;
  kind: IncentiveKind;
  amount?: number | null;
  customerAmount?: number | null;
  requiresConfirmation: boolean;
  confirmed: boolean;
}

export interface QuoteServiceItemLine {
  id: number;
  serviceItemId: number;
  serviceItemName: string;
  quantity: number;
  unitPrice: number;
}

/** One extra-work line on a quote, resolved against an {@link ExtraType} lookup row. */
export interface QuoteExtraLine {
  id: number;
  extraTypeId: number;
  extraTypeCode: string;
  extraTypeLabel: string;
  quantityBased: boolean;
  quantity: number;
  unitPrice: number;
}

export interface QuoteStatusEvent {
  id: number;
  status: QuoteStatus;
  occurredAt: string;
  notes?: string;
  changedBy?: string;
}

export interface QuoteAttachment {
  id: number;
  fileName: string;
  fileUrl: string;
  uploadedAt: string;
}

export interface DuctedUnitOutlet {
  id: number;
  position: number;
  systemType: DuctSystemType;
  ventCount: number;
}

export interface DuctedUnit {
  id: number;
  cloudQuantity: number;
  plenumQuantity: number;
  hasFlexibleHose: boolean;
  flexibleHoseQuantity: number;
  hasRigidHose: boolean;
  rigidHoseQuantity: number;
  remoteQuantity: number;
  outlets: DuctedUnitOutlet[];
}

export interface QuoteDetail {
  id: number;
  number: number;
  version: number;
  revision: number;
  parentQuoteId?: number;
  clientId: number;
  clientName: string;
  operatorId?: number;
  operatorName?: string;
  department: QuoteDepartment;
  createdAt: string;
  currentStatus: QuoteStatus;
  // Derived server-side from statusEvents — the event log is authoritative.
  surveyDone: boolean;
  surveyDate?: string;
  recalled: boolean;
  recalledDate?: string;
  recallNotes?: string;
  confirmed: boolean;
  confirmedDate?: string;
  predisposed: boolean;
  vat: VatOption;
  manualDiscount: number;
  machinesPrice: number;
  installationPrice: number;
  rounding: number;
  finalPrice: number;
  incentives: QuoteIncentive[];
  installationAddress?: string;
  installationCity?: string;
  installationPositionNotes?: string;
  floorPlanImageUrl?: string;
  floorPlanSketch?: FloorPlanSketch | null;
  pdfUrl?: string;
  products: QuoteProduct[];
  ductedUnits: DuctedUnit[];
  serviceItems: QuoteServiceItemLine[];
  extras: QuoteExtraLine[];
  statusEvents: QuoteStatusEvent[];
  attachments: QuoteAttachment[];
}

export interface QuoteProductInput {
  productId: number;
  role: ProductRole;
  quantity: number;
  /** `LineAmount` incentive schemes on this line (e.g. an extended warranty). */
  incentiveSchemeIds: number[];
}

/** A quote-level incentive; `amount` applies to `QuoteAmount` schemes (omitted: the scheme default). */
export interface QuoteIncentiveInput {
  incentiveSchemeId: number;
  amount?: number;
}

export interface DuctedUnitOutletInput {
  position: number;
  systemType: DuctSystemType;
  ventCount: number;
}

export interface DuctedUnitInput {
  cloudQuantity: number;
  plenumQuantity: number;
  hasFlexibleHose: boolean;
  flexibleHoseQuantity: number;
  hasRigidHose: boolean;
  rigidHoseQuantity: number;
  remoteQuantity: number;
  outlets: DuctedUnitOutletInput[];
}

export interface QuoteServiceItemInput {
  serviceItemId: number;
  quantity: number;
}

export interface QuoteExtraInput {
  extraTypeId: number;
  quantity: number;
}

export interface UpsertQuoteRequest {
  clientId: number;
  operatorId?: number;
  parentQuoteId?: number;
  department: QuoteDepartment;
  predisposed: boolean;
  vat: VatOption;
  manualDiscount: number;
  incentives: QuoteIncentiveInput[];
  installationAddress?: string;
  installationCity?: string;
  installationPositionNotes?: string;
  floorPlanSketch?: FloorPlanSketch | null;
  products: QuoteProductInput[];
  ductedUnits: DuctedUnitInput[];
  serviceItems: QuoteServiceItemInput[];
  extras: QuoteExtraInput[];
}

export interface RecallQuoteRequest {
  notes?: string;
}

export interface QuotePdfResponse {
  url: string;
}
