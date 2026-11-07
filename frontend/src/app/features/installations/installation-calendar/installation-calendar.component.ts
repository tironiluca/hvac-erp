import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import {
  CdkDrag,
  CdkDragDrop,
  CdkDropList,
  CdkDropListGroup,
} from '@angular/cdk/drag-drop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { InstallationsService } from '../../../core/services/installations.service';
import { InstallationRealtimeService } from '../../../core/services/installation-realtime.service';
import { CalendarPrefsService } from '../../../core/services/calendar-prefs.service';
import { LookupsService } from '../../../core/services/lookups.service';
import { SnackbarService } from '../../../core/services/snackbar.service';
import { AuthService } from '../../../core/services/auth.service';
import { Installer } from '../../../core/models/lookup.model';
import {
  DaySlot,
  InstallerSummary,
  RescheduleInstallationRequest,
  ScheduledInstallation,
} from '../../../core/models/installation.model';
import { toLocalDateTimeIso } from '../../../core/utils/date.util';

type ViewMode = 'week' | 'month';
/** A crew number, or the bucket for appointments whose installers have no crew. */
type LaneId = number | 'none';

const HALF_DAY_SLOTS: DaySlot[] = ['Morning', 'Afternoon'];
const SLOT_SORT: Record<DaySlot, number> = { FullDay: 0, Morning: 1, Afternoon: 2 };

/** Deterministic per-crew accent — index by (crew - 1); the "no crew" lane uses a neutral token. */
const CREW_COLORS = [
  '#1e88e5', '#8e24aa', '#00897b', '#f4511e',
  '#3949ab', '#c0ca33', '#d81b60', '#00acc1',
];

/** One droppable band inside a (crew, day) cell of the week grid. */
interface SlotDrop {
  id: string;
  dayKey: string;
  laneId: LaneId;
  slot: DaySlot;
  items: ScheduledInstallation[];
}

interface WeekCell {
  day: Date;
  dayKey: string;
  isToday: boolean;
  /** True when this crew has a full-day booking here — mattina/pomeriggio collapse into one row. */
  collapsed: boolean;
  rows: SlotDrop[];
}

interface WeekLane {
  laneId: LaneId;
  isNoCrew: boolean;
  color: string;
  cells: WeekCell[];
}

interface MonthCell {
  day: Date;
  dayKey: string;
  isToday: boolean;
  inMonth: boolean;
  items: ScheduledInstallation[];
}

@Component({
  selector: 'hvac-installation-calendar',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    CdkDropListGroup,
    CdkDropList,
    CdkDrag,
    MatButtonModule,
    MatButtonToggleModule,
    MatIconModule,
    MatFormFieldModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    TranslocoModule,
  ],
  templateUrl: './installation-calendar.component.html',
  styleUrl: './installation-calendar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InstallationCalendarComponent {
  private installationsService = inject(InstallationsService);
  private realtime = inject(InstallationRealtimeService);
  private prefs = inject(CalendarPrefsService);
  private lookups = inject(LookupsService);
  private snackbar = inject(SnackbarService);
  private transloco = inject(TranslocoService);
  private auth = inject(AuthService);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);

  protected loading = signal(true);
  protected connected = this.realtime.connected;

  protected viewMode = signal<ViewMode>('week');
  /** Month cells are a fixed height; appointments past this count fold into a "+N" chip. */
  protected readonly monthChipLimit = 3;
  /** Reference day: the week grid shows its Mon–Sun; the month grid shows its whole month. */
  protected anchor = signal(startOfDay(new Date()));
  private installations = signal<ScheduledInstallation[]>([]);
  /** Ids with a reschedule request in flight — suppresses the realtime echo and marks the card. */
  protected pending = signal<ReadonlySet<number>>(new Set());

  protected allInstallers = signal<Installer[]>([]);
  protected installerFilter = new FormControl<number[]>([], { nonNullable: true });
  protected crewFilter = new FormControl<number | null>(null);

  private readonly filterTick = signal(0);

  // ---- other people on the page (SignalR presence) --------------------------
  protected readonly viewers = computed(() => {
    const me = this.auth.currentUser()?.id;
    return this.realtime
      .presence()
      .filter((v) => v.id !== me)
      .map((v) => ({ id: v.id, name: this.shortName(v.name) }));
  });

  // ---- visible period ------------------------------------------------------
  protected readonly range = computed(() => {
    const a = this.anchor();
    const weekStart = this.prefs.weekStart();
    if (this.viewMode() === 'week') {
      const start = startOfWeek(a, weekStart);
      return { start, end: addDays(start, 6) };
    }
    const first = new Date(a.getFullYear(), a.getMonth(), 1);
    const last = new Date(a.getFullYear(), a.getMonth() + 1, 0);
    return { start: startOfWeek(first, weekStart), end: addDays(startOfWeek(last, weekStart), 6) };
  });

  protected readonly gridDays = computed(() => {
    const { start, end } = this.range();
    const days: Date[] = [];
    for (let d = new Date(start); d <= end; d = addDays(d, 1)) {
      days.push(new Date(d));
    }
    return days;
  });

  protected readonly weekdayHeaders = computed(() => {
    const todayKey = dateKey(new Date());
    return this.gridDays()
      .slice(0, 7)
      .map((day) => ({ day, isToday: dateKey(day) === todayKey }));
  });

  // ---- crews / filtering -------------------------------------------------
  protected readonly crews = computed(() => {
    const set = new Set<number>();
    for (const i of this.allInstallers()) {
      if (i.crew != null) set.add(i.crew);
    }
    return [...set].sort((a, b) => a - b);
  });

  private readonly crewByInstaller = computed(() => {
    const map = new Map<number, number | undefined>();
    for (const i of this.allInstallers()) map.set(i.id, i.crew ?? undefined);
    return map;
  });

  private readonly visibleInstallations = computed(() => {
    this.filterTick();
    const selected = new Set(this.installerFilter.value);
    const crew = this.crewFilter.value;
    const items = this.installations();
    if (selected.size === 0 && crew == null) return items;

    const crewOf = this.crewByInstaller();
    return items.filter((i) =>
      i.installers.some(
        (ins) =>
          (selected.size === 0 || selected.has(ins.id)) &&
          (crew == null || crewOf.get(ins.id) === crew),
      ),
    );
  });

  // ---- week grid (crews as rows, days as columns) ----------------------
  protected readonly weekLanes = computed<WeekLane[]>(() => {
    if (this.viewMode() !== 'week') return [];
    const items = this.visibleInstallations();
    const todayKey = dateKey(new Date());
    const days = this.gridDays();

    const crewFilter = this.crewFilter.value;
    const laneIds: LaneId[] =
      crewFilter != null ? [crewFilter] : [...this.crews(), 'none'];

    return laneIds.map((laneId) => ({
      laneId,
      isNoCrew: laneId === 'none',
      color: this.laneColor(laneId),
      cells: days.map((day) => {
        const dayKey = dateKey(day);
        const cellItems = items
          .filter((i) => i.date.slice(0, 10) === dayKey && this.laneOf(i) === laneId)
          .sort((a, b) => SLOT_SORT[a.slot] - SLOT_SORT[b.slot]);
        const collapsed = cellItems.some((i) => i.slot === 'FullDay');
        const rows: SlotDrop[] = collapsed
          ? [this.slotDrop(dayKey, laneId, 'FullDay', cellItems)]
          : HALF_DAY_SLOTS.map((slot) =>
              this.slotDrop(dayKey, laneId, slot, cellItems.filter((i) => i.slot === slot)),
            );
        return { day, dayKey, isToday: dayKey === todayKey, collapsed, rows };
      }),
    }));
  });

  // ---- month grid (one cell per day) -----------------------------------
  protected readonly monthWeeks = computed<MonthCell[][]>(() => {
    if (this.viewMode() !== 'month') return [];
    const items = this.visibleInstallations();
    const todayKey = dateKey(new Date());
    const anchorMonth = this.anchor().getMonth();

    const cells = this.gridDays().map((day) => {
      const dayKey = dateKey(day);
      return {
        day,
        dayKey,
        isToday: dayKey === todayKey,
        inMonth: day.getMonth() === anchorMonth,
        items: items
          .filter((i) => i.date.slice(0, 10) === dayKey)
          .sort((a, b) => SLOT_SORT[a.slot] - SLOT_SORT[b.slot]),
      };
    });

    const weeks: MonthCell[][] = [];
    for (let i = 0; i < cells.length; i += 7) weeks.push(cells.slice(i, i + 7));
    return weeks;
  });

  protected readonly totalVisible = computed(() => {
    const keys = new Set(this.gridDays().map(dateKey));
    return this.visibleInstallations().filter((i) => keys.has(i.date.slice(0, 10))).length;
  });

  constructor() {
    this.lookups.getInstallers().subscribe((list) => this.allInstallers.set(list));

    this.installerFilter.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.filterTick.update((n) => n + 1));
    this.crewFilter.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.filterTick.update((n) => n + 1));

    this.realtime.changed$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((updated) => {
      if (this.pending().has(updated.id)) return;
      this.upsert(updated);
    });
    this.realtime.deleted$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((id) => {
      this.installations.update((list) => list.filter((i) => i.id !== id));
    });

    void this.realtime.connect();
    this.destroyRef.onDestroy(() => void this.realtime.disconnect());

    this.reload();
  }

  // ---- navigation -------------------------------------------------------
  protected setView(mode: ViewMode): void {
    if (mode === this.viewMode()) return;
    this.viewMode.set(mode);
    this.reload();
  }

  protected shift(delta: number): void {
    this.anchor.update((d) =>
      this.viewMode() === 'week'
        ? addDays(d, delta * 7)
        : new Date(d.getFullYear(), d.getMonth() + delta, 1),
    );
    this.reload();
  }

  protected goToday(): void {
    this.anchor.set(startOfDay(new Date()));
    this.reload();
  }

  protected clearFilters(): void {
    this.installerFilter.setValue([]);
    this.crewFilter.setValue(null);
  }

  // ---- presence helpers ----------------------------------------------
  protected initials(name: string): string {
    const parts = name.split(/[\s.]+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || '?';
  }

  protected viewerNames(): string {
    return this.viewers().map((v) => v.name).join(', ');
  }

  private shortName(name: string): string {
    return name.includes('@') ? name.slice(0, name.indexOf('@')) : name;
  }

  // ---- cards ---------------------------------------------------------
  protected crewLabel(installation: ScheduledInstallation): string {
    return installation.installers.map((i) => i.name).join(', ');
  }

  /** First letter of the localised slot name, for the compact month chips. */
  protected slotInitial(slot: DaySlot): string {
    return this.transloco.translate('installations.slots.' + slot).charAt(0).toUpperCase();
  }

  /** Full text for a month chip whose title is ellipsised in the fixed-height cell. */
  protected chipTooltip(item: ScheduledInstallation): string {
    const crew = this.crewLabel(item) || this.transloco.translate('installations.calendar.noCrew');
    const city = item.city ? ` · ${item.city}` : '';
    return `${item.clientName} · #${item.quoteNumber}${city}\n${crew}`;
  }

  /** Lists the appointments a day cell had to hide behind its "+N" chip. */
  protected moreTooltip(cell: MonthCell): string {
    return cell.items
      .slice(this.monthChipLimit)
      .map((i) => `${this.slotInitial(i.slot)} · ${i.clientName} · #${i.quoteNumber}`)
      .join('\n');
  }

  /** Jump from a month cell into that day's week view. */
  protected openDay(day: Date): void {
    this.anchor.set(startOfDay(day));
    if (this.viewMode() !== 'week') this.viewMode.set('week');
    this.reload();
  }

  protected cardColor(installation: ScheduledInstallation): string {
    return this.laneColor(this.laneOf(installation));
  }

  protected openDetail(installation: ScheduledInstallation): void {
    this.router.navigate(['/installations', installation.id]);
  }

  protected openNew(): void {
    this.router.navigate(['/installations', 'new']);
  }

  protected openList(): void {
    this.router.navigate(['/installations', 'list']);
  }

  // ---- drag & drop -------------------------------------------------
  /** Week grid: a drop retargets day + time band + crew. */
  protected onWeekDrop(event: CdkDragDrop<SlotDrop>): void {
    if (event.previousContainer === event.container) return;
    const installation = event.item.data as ScheduledInstallation;
    const target = event.container.data;

    const targetInstallerIds =
      target.laneId === 'none'
        ? installation.installers.map((i) => i.id)
        : this.installerIdsForCrew(target.laneId);

    const sameCrew = target.laneId === 'none' || this.laneOf(installation) === target.laneId;
    if (installation.date.slice(0, 10) === target.dayKey && installation.slot === target.slot && sameCrew) {
      return;
    }

    this.reschedule(installation, target.dayKey, target.slot, targetInstallerIds);
  }

  /** Month grid: a drop moves the day only; time band and crew are kept. */
  protected onMonthDrop(event: CdkDragDrop<MonthCell>): void {
    if (event.previousContainer === event.container) return;
    const installation = event.item.data as ScheduledInstallation;
    const dayKey = event.container.data.dayKey;
    if (installation.date.slice(0, 10) === dayKey) return;

    this.reschedule(installation, dayKey, installation.slot, installation.installers.map((i) => i.id));
  }

  private reschedule(
    installation: ScheduledInstallation,
    dayKey: string,
    slot: DaySlot,
    installerIds: number[],
  ): void {
    const newDate = `${dayKey}T00:00:00`;
    const optimistic: ScheduledInstallation = {
      ...installation,
      date: newDate,
      slot,
      installers: this.summariesFor(installerIds, installation.installers),
    };
    this.upsert(optimistic);
    this.markPending(installation.id, true);

    const request: RescheduleInstallationRequest = {
      date: newDate,
      slot,
      installerIds,
      rowVersion: installation.rowVersion,
    };

    this.installationsService.reschedule(installation.id, request).subscribe({
      next: (saved) => {
        this.markPending(installation.id, false);
        this.upsert(saved);
      },
      error: (err: HttpErrorResponse) => {
        this.markPending(installation.id, false);
        this.upsert(installation); // revert the optimistic move

        if (err.status === 409) {
          this.snackbar.error(this.transloco.translate('installations.calendar.conflict'));
          this.reload();
        } else if (err.status === 400 && err.error?.detail) {
          this.snackbar.error(err.error.detail);
        } else {
          this.snackbar.error(this.transloco.translate('installations.calendar.moveError'));
        }
      },
    });
  }

  // ---- internals -------------------------------------------------
  private reload(): void {
    const { start, end } = this.range();
    this.loading.set(true);
    this.installationsService.getList(toLocalDateTimeIso(start), toLocalDateTimeIso(end)).subscribe({
      next: (list) => {
        this.installations.set(list);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private slotDrop(dayKey: string, laneId: LaneId, slot: DaySlot, items: ScheduledInstallation[]): SlotDrop {
    return { id: `${dayKey}::${laneId}::${slot}`, dayKey, laneId, slot, items };
  }

  /** Lane bucket for an appointment: the (lowest) crew among its installers, else "none". */
  private laneOf(installation: ScheduledInstallation): LaneId {
    const crewOf = this.crewByInstaller();
    const crews = installation.installers
      .map((i) => crewOf.get(i.id))
      .filter((c): c is number => c != null)
      .sort((a, b) => a - b);
    return crews.length ? crews[0] : 'none';
  }

  private laneColor(laneId: LaneId): string {
    return laneId === 'none'
      ? 'var(--mat-sys-outline)'
      : CREW_COLORS[(laneId - 1) % CREW_COLORS.length];
  }

  private installerIdsForCrew(crew: number): number[] {
    return this.allInstallers().filter((i) => i.crew === crew).map((i) => i.id);
  }

  private summariesFor(ids: number[], fallback: InstallerSummary[]): InstallerSummary[] {
    if (ids.length === 0) return fallback;
    const nameById = new Map(this.allInstallers().map((i) => [i.id, i.name]));
    return ids.map((id) => ({ id, name: nameById.get(id) ?? '' }));
  }

  private upsert(installation: ScheduledInstallation): void {
    this.installations.update((list) => {
      const idx = list.findIndex((i) => i.id === installation.id);
      if (idx === -1) return [...list, installation];
      const next = [...list];
      next[idx] = installation;
      return next;
    });
  }

  private markPending(id: number, on: boolean): void {
    this.pending.update((set) => {
      const next = new Set(set);
      if (on) next.add(id);
      else next.delete(id);
      return next;
    });
  }
}

function startOfDay(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

function startOfWeek(date: Date, weekStart: number): Date {
  const d = startOfDay(date);
  const offset = (d.getDay() - weekStart + 7) % 7;
  d.setDate(d.getDate() - offset);
  return d;
}

function addDays(date: Date, days: number): Date {
  const d = new Date(date);
  d.setDate(d.getDate() + days);
  return d;
}

function dateKey(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}
