import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { provideNativeDateAdapter } from '@angular/material/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { InstallationsService } from '../../../core/services/installations.service';
import { ClientsService } from '../../../core/services/clients.service';
import { QuotesService } from '../../../core/services/quotes.service';
import { LookupsService } from '../../../core/services/lookups.service';
import { SnackbarService } from '../../../core/services/snackbar.service';
import { Client } from '../../../core/models/client.model';
import { Installer, LookupItem } from '../../../core/models/lookup.model';
import { QuoteListItem } from '../../../core/models/quote.model';
import { FloorPlanSketch } from '../../../core/models/floor-plan-sketch.model';
import { DaySlot, ScheduledInstallation, UpsertScheduledInstallationRequest } from '../../../core/models/installation.model';
import { FloorPlanSketchComponent } from '../../../shared/floor-plan-sketch/floor-plan-sketch.component';
import { toLocalDateTimeIso } from '../../../core/utils/date.util';

@Component({
  selector: 'hvac-installation-form',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatAutocompleteModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    TranslocoModule,
    FloorPlanSketchComponent,
  ],
  providers: [provideNativeDateAdapter()],
  templateUrl: './installation-form.component.html',
  styleUrl: './installation-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InstallationFormComponent {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private installationsService = inject(InstallationsService);
  private clientsService = inject(ClientsService);
  private quotesService = inject(QuotesService);
  private lookupsService = inject(LookupsService);
  private snackbar = inject(SnackbarService);
  private transloco = inject(TranslocoService);

  protected loading = signal(true);
  protected saving = signal(false);
  protected generatingPdf = signal(false);

  protected installationId = signal<number | null>(null);
  protected installation = signal<ScheduledInstallation | null>(null);

  protected installers = signal<Installer[]>([]);
  // Option set + order from `GET /api/lookups/day-slots`; labels via Transloco.
  protected daySlots = signal<LookupItem[]>([]);

  protected clientControl = new FormControl<Client | string | null>(null);
  protected clientOptions = signal<Client[]>([]);
  protected quoteOptions = signal<QuoteListItem[]>([]);
  protected quoteIdControl = new FormControl<number | null>(null, { validators: [Validators.required] });

  protected form = new FormGroup({
    date: new FormControl<Date | null>(null, { validators: [Validators.required] }),
    slot: new FormControl<DaySlot>('Morning', { nonNullable: true, validators: [Validators.required] }),
    notes: new FormControl(''),
    installerIds: new FormControl<number[]>([], { nonNullable: true }),
    floorPlanSketch: new FormControl<FloorPlanSketch | null>(null),
  });

  constructor() {
    this.lookupsService.getInstallers().subscribe((installers) => this.installers.set(installers));
    this.lookupsService.getDaySlots().subscribe((slots) => this.daySlots.set(slots));

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

    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      const id = Number(idParam);
      this.installationId.set(id);
      this.installationsService.getById(id).subscribe((detail) => {
        this.populateForm(detail);
        this.installation.set(detail);
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
    const client = event.option.value as Client;
    this.clientControl.setValue(client);
    this.quoteIdControl.setValue(null);
    this.quoteOptions.set([]);
    this.quotesService.getList(client.id).subscribe((quotes) => {
      this.quoteOptions.set(quotes.filter((q) => q.currentStatus === 'Confirmed'));
    });
  }

  protected quoteLabel(quote: QuoteListItem): string {
    const label = quote.revision > 0 ? `${quote.number}/${quote.version}.${quote.revision}` : `${quote.number}/${quote.version}`;
    return `${label} — ${quote.finalPrice.toFixed(2)} €`;
  }

  protected async submit(): Promise<void> {
    const id = this.installationId();
    const quoteId = id ? this.installation()?.quoteId : this.quoteIdControl.value;

    if (this.form.invalid || !quoteId || this.saving()) {
      this.form.markAllAsTouched();
      this.quoteIdControl.markAsTouched();
      return;
    }

    this.saving.set(true);
    const raw = this.form.getRawValue();

    const request: UpsertScheduledInstallationRequest = {
      quoteId,
      date: toLocalDateTimeIso(raw.date!),
      slot: raw.slot,
      notes: raw.notes || undefined,
      installerIds: raw.installerIds,
      floorPlanSketch: raw.floorPlanSketch ?? null,
    };

    const save$ = id ? this.installationsService.update(id, request) : this.installationsService.create(request);
    save$.subscribe({
      next: (detail) => {
        this.saving.set(false);
        this.snackbar.success(this.transloco.translate(id ? 'installations.updated' : 'installations.created'));
        if (id) {
          this.installation.set(detail);
        } else {
          this.router.navigate(['/installations', detail.id], { replaceUrl: true });
        }
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        const message = err.status === 400 && err.error?.detail ? err.error.detail : this.transloco.translate('installations.saveError');
        this.snackbar.error(message);
      },
    });
  }

  protected generatePdf(): void {
    const id = this.installationId();
    if (!id) return;
    this.generatingPdf.set(true);
    this.installationsService.generatePdf(id).subscribe({
      next: (res) => {
        this.generatingPdf.set(false);
        this.installation.update((i) => (i ? { ...i, reportPdfUrl: res.url } : i));
        window.open(res.url, '_blank');
      },
      error: () => {
        this.generatingPdf.set(false);
        this.snackbar.error(this.transloco.translate('installations.pdfError'));
      },
    });
  }

  protected deleteInstallation(): void {
    const id = this.installationId();
    if (!id || !window.confirm(this.transloco.translate('installations.deletePrompt'))) return;
    this.installationsService.delete(id).subscribe(() => {
      this.snackbar.success(this.transloco.translate('installations.deleted'));
      this.router.navigate(['/installations']);
    });
  }

  private populateForm(detail: ScheduledInstallation): void {
    this.form.patchValue({
      date: new Date(detail.date),
      slot: detail.slot,
      notes: detail.notes ?? '',
      installerIds: detail.installers.map((i) => i.id),
      floorPlanSketch: detail.floorPlanSketch ?? null,
    });
  }
}
