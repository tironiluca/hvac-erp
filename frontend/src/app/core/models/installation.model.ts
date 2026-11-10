import { FloorPlanSketch } from './floor-plan-sketch.model';

export type DaySlot = 'Morning' | 'Afternoon' | 'FullDay';

export interface InstallerSummary {
  id: number;
  name: string;
}

/** A user currently viewing the shared calendar (SignalR presence). Includes this client. */
export interface CalendarPresenceUser {
  id: string;
  name: string;
}

export interface ScheduledInstallation {
  id: number;
  /** The store the installation belongs to (its quote's store). */
  storeId: number;
  quoteId: number;
  quoteNumber: string;
  clientName: string;
  city?: string | null;
  date: string;
  slot: DaySlot;
  notes?: string;
  /** Whether a work order has been generated; its (expiring) link comes from `InstallationsService.getPdfUrl`. */
  hasReportPdf: boolean;
  /** Optimistic-concurrency token echoed back on reschedule; a mismatch is a 409. */
  rowVersion: string;
  updatedAt?: string | null;
  updatedBy?: string | null;
  installers: InstallerSummary[];
  floorPlanSketch?: FloorPlanSketch | null;
}

export interface UpsertScheduledInstallationRequest {
  quoteId: number;
  date: string;
  slot: DaySlot;
  notes?: string;
  installerIds: number[];
  floorPlanSketch?: FloorPlanSketch | null;
}

/** `PATCH /api/installations/{id}/schedule` — drag-and-drop reschedule (date / slot / crew only). */
export interface RescheduleInstallationRequest {
  date: string;
  slot: DaySlot;
  installerIds: number[];
  rowVersion: string;
}

export interface InstallationPdfResponse {
  url: string;
}
