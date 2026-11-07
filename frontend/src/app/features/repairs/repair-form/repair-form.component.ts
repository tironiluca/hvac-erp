import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { provideNativeDateAdapter } from '@angular/material/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DatePipe, DecimalPipe } from '@angular/common';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { FGasRecordsSectionComponent } from '../../../shared/fgas-records-section/fgas-records-section.component';
import { FloorPlanSketchComponent } from '../../../shared/floor-plan-sketch/floor-plan-sketch.component';
import { RepairsService } from '../../../core/services/repairs.service';
import { ClientsService } from '../../../core/services/clients.service';
import { LookupsService } from '../../../core/services/lookups.service';
import { SnackbarService } from '../../../core/services/snackbar.service';
import { Client } from '../../../core/models/client.model';
import { Installer, ServiceItem as ServiceItemLookup } from '../../../core/models/lookup.model';
import { FloorPlanSketch } from '../../../core/models/floor-plan-sketch.model';
import { RepairDetail, RepairStatus, RepairType, UpsertRepairRequest } from '../../../core/models/repair.model';
import { REPAIR_STATUSES, repairStatusColorClass } from '../repair-status';
import { toLocalDateTimeIso } from '../../../core/utils/date.util';

type ServiceItemGroup = FormGroup<{
  serviceItemId: FormControl<number | null>;
  quantity: FormControl<number>;
}>;

@Component({
  selector: 'hvac-repair-form',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    DatePipe,
    DecimalPipe,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatAutocompleteModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatTooltipModule,
    TranslocoModule,
    FGasRecordsSectionComponent,
    FloorPlanSketchComponent,
  ],
  providers: [provideNativeDateAdapter()],
  templateUrl: './repair-form.component.html',
  styleUrl: './repair-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RepairFormComponent {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private repairsService = inject(RepairsService);
  private clientsService = inject(ClientsService);
  private lookupsService = inject(LookupsService);
  private snackbar = inject(SnackbarService);
  private transloco = inject(TranslocoService);

  // Option set + order come from `GET /api/lookups/repair-statuses`; labels stay Transloco-driven.
  protected statuses = signal<RepairStatus[]>(REPAIR_STATUSES);

  protected loading = signal(true);
  protected saving = signal(false);
  protected generatingPdf = signal(false);
  protected addingSchedule = signal(false);

  protected repairId = signal<number | null>(null);
  protected repair = signal<RepairDetail | null>(null);

  protected serviceItemsList = signal<ServiceItemLookup[]>([]);
  protected installers = signal<Installer[]>([]);

  protected clientControl = new FormControl<Client | string | null>(null, {
    validators: [(control) => (control.value && typeof control.value === 'object' ? null : { required: true })],
  });
  protected clientOptions = signal<Client[]>([]);

  protected scheduleInstallerId = new FormControl<number | null>(null);
  protected scheduleDate = new FormControl<Date | null>(null);
  protected scheduleNotes = new FormControl('');

  protected form = new FormGroup({
    type: new FormControl<RepairType>('Repair', { nonNullable: true, validators: [Validators.required] }),
    quoteId: new FormControl<number | null>(null),
    brand: new FormControl(''),
    description: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    urgent: new FormControl(false, { nonNullable: true }),
    predisposed: new FormControl(false, { nonNullable: true }),
    allInclusive: new FormControl(false, { nonNullable: true }),
    unitCount: new FormControl(1, { nonNullable: true, validators: [Validators.min(0)] }),
    splitCount: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }),
    rounding: new FormControl(0, { nonNullable: true }),
    installationDate: new FormControl<Date | null>(null),
    floorPlanSketch: new FormControl<FloorPlanSketch | null>(null),
    serviceItems: new FormArray<ServiceItemGroup>([]),
  });

  private formTick = toSignal(this.form.valueChanges, { initialValue: null });
  protected formValue = computed(() => {
    this.formTick();
    return this.form.getRawValue();
  });

  protected estimatedTotal = computed(() => {
    const raw = this.formValue();
    const total = raw.serviceItems.reduce((sum, line) => {
      const item = this.serviceItemsList().find((s) => s.id === line.serviceItemId);
      return sum + (item?.price ?? 0) * (line.quantity ?? 0);
    }, 0);
    return total + (raw.rounding ?? 0);
  });

  constructor() {
    this.lookupsService.getServiceItems().subscribe((items) => this.serviceItemsList.set(items));
    this.lookupsService.getInstallers().subscribe((installers) => this.installers.set(installers));
    this.lookupsService
      .getRepairStatuses()
      .subscribe((rows) => this.statuses.set(rows.map((r) => r.code as RepairStatus)));

    this.clientControl.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((value) => {
          const term = typeof value === 'string' ? value : value?.fullName;
          return term && term.length >= 2 ? this.clientsService.search(term) : of([]);
        }),
      )
      .subscribe((results) => this.clientOptions.set(results));

    this.form.controls.type.valueChanges.subscribe((type) => {
      if (type === 'Revision') {
        this.form.controls.urgent.setValue(false);
        this.form.controls.urgent.disable();
        this.form.controls.predisposed.setValue(true);
        this.form.controls.predisposed.disable();
      } else {
        this.form.controls.urgent.enable();
        this.form.controls.predisposed.enable();
      }
    });

    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      const id = Number(idParam);
      this.repairId.set(id);
      this.repairsService.getById(id).subscribe((detail) => {
        this.populateForm(detail);
        this.repair.set(detail);
        this.loading.set(false);
      });
    } else {
      this.loading.set(false);
    }
  }

  protected displayClient(value: Client | string | null): string {
    return typeof value === 'string' ? value : (value?.fullName ?? '');
  }

  protected onClientSelected(event: MatAutocompleteSelectedEvent): void {
    this.clientControl.setValue(event.option.value as Client);
  }

  protected addServiceItem(): void {
    this.form.controls.serviceItems.push(this.createServiceItemGroup());
  }

  protected removeServiceItem(index: number): void {
    this.form.controls.serviceItems.removeAt(index);
  }

  protected statusClass(status: RepairStatus): string {
    return repairStatusColorClass(status);
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid || this.clientControl.invalid || this.saving()) {
      this.form.markAllAsTouched();
      this.clientControl.markAsTouched();
      return;
    }

    const client = this.clientControl.value;
    if (!client || typeof client !== 'object') {
      return;
    }

    this.saving.set(true);
    const raw = this.form.getRawValue();

    const request: UpsertRepairRequest = {
      clientId: client.id,
      quoteId: raw.quoteId ?? undefined,
      type: raw.type,
      brand: raw.brand || undefined,
      description: raw.description,
      urgent: raw.urgent,
      predisposed: raw.predisposed,
      allInclusive: raw.allInclusive,
      unitCount: raw.unitCount,
      splitCount: raw.splitCount,
      rounding: raw.rounding,
      installationDate: raw.installationDate ? toLocalDateTimeIso(raw.installationDate) : undefined,
      floorPlanSketch: raw.floorPlanSketch ?? null,
      serviceItems: raw.serviceItems
        .filter((s) => s.serviceItemId)
        .map((s) => ({ serviceItemId: s.serviceItemId as number, quantity: s.quantity })),
    };

    const id = this.repairId();
    const save$ = id ? this.repairsService.update(id, request) : this.repairsService.create(request);
    save$.subscribe({
      next: (detail) => {
        this.saving.set(false);
        this.snackbar.success(this.transloco.translate(id ? 'repairs.updated' : 'repairs.created'));
        if (id) {
          this.repair.set(detail);
        } else {
          this.router.navigate(['/repairs', detail.id], { replaceUrl: true });
        }
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        const message = err.status === 400 && err.error?.detail ? err.error.detail : this.transloco.translate('repairs.saveError');
        this.snackbar.error(message);
      },
    });
  }

  protected changeStatus(status: RepairStatus): void {
    const id = this.repairId();
    if (!id) return;
    this.repairsService.changeStatus(id, { status }).subscribe(() => {
      this.snackbar.success(this.transloco.translate('repairs.statusChanged'));
      this.repair.update((r) => (r ? { ...r, status } : r));
    });
  }

  protected addSchedule(): void {
    const id = this.repairId();
    const date = this.scheduleDate.value;
    if (!id || !date) return;

    this.addingSchedule.set(true);
    this.repairsService
      .addScheduleEntry(id, {
        installerId: this.scheduleInstallerId.value ?? undefined,
        date: toLocalDateTimeIso(date),
        notes: this.scheduleNotes.value || undefined,
      })
      .subscribe({
        next: (entry) => {
          this.addingSchedule.set(false);
          this.repair.update((r) => (r ? { ...r, scheduleEntries: [...r.scheduleEntries, entry] } : r));
          this.scheduleInstallerId.setValue(null);
          this.scheduleDate.setValue(null);
          this.scheduleNotes.setValue('');
        },
        error: () => {
          this.addingSchedule.set(false);
          this.snackbar.error(this.transloco.translate('repairs.scheduleError'));
        },
      });
  }

  protected deleteRepair(): void {
    const id = this.repairId();
    if (!id || !window.confirm(this.transloco.translate('repairs.deletePrompt'))) return;
    this.repairsService.delete(id).subscribe(() => {
      this.snackbar.success(this.transloco.translate('repairs.deleted'));
      this.router.navigate(['/repairs']);
    });
  }

  protected generatePdf(): void {
    const id = this.repairId();
    if (!id) return;
    this.generatingPdf.set(true);
    this.repairsService.generatePdf(id).subscribe({
      next: (res) => {
        this.generatingPdf.set(false);
        this.repair.update((r) => (r ? { ...r, pdfUrl: res.url } : r));
        window.open(res.url, '_blank');
      },
      error: () => {
        this.generatingPdf.set(false);
        this.snackbar.error(this.transloco.translate('repairs.pdfError'));
      },
    });
  }

  private populateForm(detail: RepairDetail): void {
    this.clientsService.getById(detail.clientId).subscribe((client) => this.clientControl.setValue(client));

    this.form.patchValue({
      type: detail.type,
      quoteId: detail.quoteId ?? null,
      brand: detail.brand ?? '',
      description: detail.description,
      urgent: detail.urgent,
      predisposed: detail.predisposed,
      allInclusive: detail.allInclusive,
      unitCount: detail.unitCount,
      splitCount: detail.splitCount,
      rounding: detail.rounding,
      installationDate: detail.installationDate ? new Date(detail.installationDate) : null,
      floorPlanSketch: detail.floorPlanSketch ?? null,
    });

    for (const item of detail.serviceItems) {
      this.form.controls.serviceItems.push(this.createServiceItemGroup(item.serviceItemId, item.quantity));
    }
  }

  private createServiceItemGroup(serviceItemId: number | null = null, quantity = 1): ServiceItemGroup {
    return new FormGroup({
      serviceItemId: new FormControl<number | null>(serviceItemId, { validators: [Validators.required] }),
      quantity: new FormControl(quantity, { nonNullable: true, validators: [Validators.required, Validators.min(0.01)] }),
    });
  }
}
