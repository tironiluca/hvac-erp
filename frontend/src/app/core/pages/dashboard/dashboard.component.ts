import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslocoModule } from '@jsverse/transloco';
import { AuthService } from '../../services/auth.service';
import { CurrentUserService } from '../../services/current-user.service';
import { CalendarPrefsService } from '../../services/calendar-prefs.service';
import { DashboardService, startOfWeek } from '../../services/dashboard.service';
import { ActivityItem, Dashboard } from '../../models/dashboard.model';
import { RepairStatus } from '../../models/repair.model';
import { repairStatusColorClass } from '../../../features/repairs/repair-status';
import { visibleNavItems } from '../../shell/nav-items';

/** Quote status code → the `quotes.statuses.*` i18n key and `status-chip--*` modifier. */
const QUOTE_STATUS_KEY: Record<string, string> = {
  Draft: 'draft',
  SurveyDone: 'surveyed',
  Recalled: 'recalled',
  Confirmed: 'confirmed',
};

@Component({
  selector: 'hvac-dashboard',
  imports: [RouterLink, DatePipe, MatCardModule, MatIconModule, MatChipsModule, MatProgressSpinnerModule, TranslocoModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent {
  protected auth = inject(AuthService);
  private currentUser = inject(CurrentUserService);
  private dashboardService = inject(DashboardService);
  private prefs = inject(CalendarPrefsService);

  protected loading = signal(true);
  protected failed = signal(false);
  protected data = signal<Dashboard | null>(null);

  /** Pipeline tiles: each open quote status is the step the quote is waiting on. */
  protected readonly pipeline = computed(() => {
    const q = this.data()?.quotes;
    if (!q) return [];
    return [
      { key: 'awaitingSurvey', icon: 'home_work', value: q.awaitingSurvey },
      { key: 'awaitingRecall', icon: 'phone_callback', value: q.awaitingRecall },
      { key: 'awaitingConfirmation', icon: 'pending_actions', value: q.awaitingConfirmation },
      { key: 'confirmedLast30Days', icon: 'task_alt', value: q.confirmedLast30Days },
    ];
  });

  protected readonly openRepairs = computed(() =>
    (this.data()?.openRepairsByStatus ?? []).reduce((sum, s) => sum + s.count, 0),
  );

  /** Routed modules the user can open, as dashboard shortcut cards (excludes the dashboard itself). */
  protected readonly shortcuts = computed(() =>
    visibleNavItems({
      isAdmin: this.currentUser.isAdmin(),
      isPlatformAdmin: this.currentUser.isPlatformAdmin(),
    }).filter((item) => item.route && item.route !== '/'),
  );

  constructor() {
    this.dashboardService.get(startOfWeek(new Date(), this.prefs.weekStart())).subscribe({
      next: (d) => {
        this.data.set(d);
        this.loading.set(false);
      },
      error: () => {
        this.failed.set(true);
        this.loading.set(false);
      },
    });
  }

  protected quoteStatusKey(status: string): string {
    return QUOTE_STATUS_KEY[status] ?? 'draft';
  }

  protected repairChipClass(status: string): string {
    return repairStatusColorClass(status as RepairStatus);
  }

  protected activityLink(item: ActivityItem): (string | number)[] {
    return [item.kind === 'Quote' ? '/quotes' : '/repairs', item.entityId];
  }

  protected activityStatusLabel(item: ActivityItem): string {
    return item.kind === 'Quote' ? `quotes.statuses.${this.quoteStatusKey(item.status)}` : `repairs.statuses.${item.status}`;
  }
}
