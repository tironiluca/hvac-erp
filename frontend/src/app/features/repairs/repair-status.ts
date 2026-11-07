import { RepairStatus } from '../../core/models/repair.model';

// Ordinal-ordered (legacy RipRev.Stato 0–9). Used as the fallback option set before
// `GET /api/lookups/repair-statuses` resolves; the server list is authoritative for order.
export const REPAIR_STATUSES: RepairStatus[] = [
  'New',
  'Urgent',
  'Scheduled',
  'InProgress',
  'AwaitingPayment',
  'PendingOffSeason',
  'ClosedByInstallers',
  'Closed',
  'AssistanceRequested',
  'AssistanceContacted',
];

const COLOR_CLASSES: Record<RepairStatus, string> = {
  New: 'status-chip--new',
  Urgent: 'status-chip--urgent',
  Scheduled: 'status-chip--scheduled',
  InProgress: 'status-chip--in-progress',
  AwaitingPayment: 'status-chip--waiting',
  PendingOffSeason: 'status-chip--on-hold',
  ClosedByInstallers: 'status-chip--closed',
  Closed: 'status-chip--closed',
  AssistanceRequested: 'status-chip--waiting',
  AssistanceContacted: 'status-chip--waiting',
};

export function repairStatusColorClass(status: RepairStatus): string {
  return COLOR_CLASSES[status] ?? 'status-chip--new';
}
