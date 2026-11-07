import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { FGasRecordsService } from '../../core/services/fgas-records.service';
import { SnackbarService } from '../../core/services/snackbar.service';
import { FGasRecord } from '../../core/models/fgas-record.model';

const COMMON_GAS_TYPES = ['R32', 'R410A', 'R290', 'R134a', 'R404A'];

@Component({
  selector: 'hvac-fgas-records-section',
  imports: [
    ReactiveFormsModule,
    DatePipe,
    DecimalPipe,
    MatFormFieldModule,
    MatInputModule,
    MatAutocompleteModule,
    MatCheckboxModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    TranslocoModule,
  ],
  templateUrl: './fgas-records-section.component.html',
  styleUrl: './fgas-records-section.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FGasRecordsSectionComponent {
  quoteId = input<number | null>(null);
  repairId = input<number | null>(null);

  private fgasService = inject(FGasRecordsService);
  private snackbar = inject(SnackbarService);
  private transloco = inject(TranslocoService);

  protected readonly commonGasTypes = COMMON_GAS_TYPES;

  protected loading = signal(true);
  protected adding = signal(false);
  protected records = signal<FGasRecord[]>([]);

  protected form = new FormGroup({
    gasType: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    kilogramsOfGas: new FormControl<number | null>(null, { validators: [Validators.required, Validators.min(0.01)] }),
    isRefill: new FormControl(false, { nonNullable: true }),
    hasExtraCharge: new FormControl(false, { nonNullable: true }),
  });

  constructor() {
    effect(() => {
      const quoteId = this.quoteId();
      const repairId = this.repairId();
      const load$ = quoteId ? this.fgasService.getForQuote(quoteId) : repairId ? this.fgasService.getForRepair(repairId) : null;

      if (!load$) {
        this.loading.set(false);
        return;
      }

      load$.subscribe((records) => {
        this.records.set(records);
        this.loading.set(false);
      });
    });
  }

  protected add(): void {
    if (this.form.invalid || this.adding()) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    this.adding.set(true);
    this.fgasService
      .create({
        quoteId: this.quoteId() ?? undefined,
        repairId: this.repairId() ?? undefined,
        gasType: raw.gasType,
        kilogramsOfGas: raw.kilogramsOfGas as number,
        isRefill: raw.isRefill,
        hasExtraCharge: raw.hasExtraCharge,
      })
      .subscribe({
        next: (record) => {
          this.adding.set(false);
          this.records.update((records) => [record, ...records]);
          this.form.reset({ gasType: '', kilogramsOfGas: null, isRefill: false, hasExtraCharge: false });
        },
        error: () => {
          this.adding.set(false);
          this.snackbar.error(this.transloco.translate('fgas.saveError'));
        },
      });
  }

  protected remove(record: FGasRecord): void {
    this.fgasService.delete(record.id).subscribe(() => {
      this.records.update((records) => records.filter((r) => r.id !== record.id));
    });
  }
}
