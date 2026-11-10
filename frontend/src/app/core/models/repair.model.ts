import { FloorPlanSketch } from './floor-plan-sketch.model';

export type RepairType = 'Repair' | 'Revision';

// The 10 legacy RipRev.Stato labels at their real ordinals 0–9 (Phase 8a / 8c). Backed by the
// DB-driven `repair_statuses` lookup table server-side.
export type RepairStatus =
  | 'New'
  | 'Urgent'
  | 'Scheduled'
  | 'InProgress'
  | 'AwaitingPayment'
  | 'PendingOffSeason'
  | 'ClosedByInstallers'
  | 'Closed'
  | 'AssistanceRequested'
  | 'AssistanceContacted';

export interface RepairListItem {
  id: number;
  type: RepairType;
  status: RepairStatus;
  clientId: number;
  clientName: string;
  brand?: string;
  description: string;
  urgent: boolean;
  insertedAt: string;
  total: number;
}

export interface RepairServiceItemLine {
  id: number;
  serviceItemId: number;
  serviceItemName: string;
  quantity: number;
  unitPrice: number;
}

export interface RepairScheduleEntry {
  id: number;
  installerId?: number;
  installerName?: string;
  date: string;
  notes?: string;
}

export interface RepairDetail {
  id: number;
  type: RepairType;
  status: RepairStatus;
  clientId: number;
  clientName: string;
  quoteId?: number;
  brand?: string;
  description: string;
  urgent: boolean;
  predisposed: boolean;
  allInclusive: boolean;
  unitCount: number;
  splitCount: number;
  rounding: number;
  insertedAt: string;
  installationDate?: string;
  /** Whether a work order has been generated; its (expiring) link comes from `RepairsService.getPdfUrl`. */
  hasPdf: boolean;
  total: number;
  floorPlanSketch?: FloorPlanSketch | null;
  serviceItems: RepairServiceItemLine[];
  scheduleEntries: RepairScheduleEntry[];
}

export interface RepairServiceItemInput {
  serviceItemId: number;
  quantity: number;
}

export interface UpsertRepairRequest {
  clientId: number;
  quoteId?: number;
  type: RepairType;
  brand?: string;
  description: string;
  urgent: boolean;
  predisposed: boolean;
  allInclusive: boolean;
  unitCount: number;
  splitCount: number;
  rounding: number;
  installationDate?: string;
  floorPlanSketch?: FloorPlanSketch | null;
  serviceItems: RepairServiceItemInput[];
}

export interface ChangeRepairStatusRequest {
  status: RepairStatus;
}

export interface AddRepairScheduleEntryRequest {
  installerId?: number;
  date: string;
  notes?: string;
}

export interface RepairPdfResponse {
  url: string;
}
