import { QuoteListItem } from './quote.model';

/** `GET /api/dashboard` — mirrors the API's DashboardDto. */
export interface Dashboard {
  weekStart: string;
  quotes: QuotePipeline;
  openRepairsByStatus: StatusCount[];
  urgentOpenRepairs: number;
  weekInstallations: DashboardInstallation[];
  recentActivity: ActivityItem[];
}

export interface QuotePipeline {
  awaitingSurvey: number;
  awaitingRecall: number;
  awaitingConfirmation: number;
  confirmedLast30Days: number;
  oldestOpen: QuoteListItem[];
}

export interface StatusCount {
  status: string;
  count: number;
}

export interface DashboardInstallation {
  id: number;
  quoteId: number;
  quoteNumber: string;
  clientName: string;
  city?: string;
  date: string;
  slot: string;
  installers: string[];
}

export interface ActivityItem {
  kind: 'Quote' | 'Repair';
  entityId: number;
  reference: string;
  clientName: string;
  status: string;
  occurredAt: string;
  changedBy?: string;
}
