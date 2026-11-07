import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ReactiveFormsModule, FormControl, FormGroup, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { ClientsService } from '../../../core/services/clients.service';
import { LookupsService } from '../../../core/services/lookups.service';
import { SnackbarService } from '../../../core/services/snackbar.service';
import { CurrentUserService } from '../../../core/services/current-user.service';
import { Client, UpsertClientRequest } from '../../../core/models/client.model';
import { ClientSource } from '../../../core/models/lookup.model';

export interface ClientFormDialogData {
  client?: Client;
}

@Component({
  selector: 'hvac-client-form-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    TranslocoModule,
  ],
  templateUrl: './client-form-dialog.component.html',
  styleUrl: './client-form-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClientFormDialogComponent {
  private dialogRef = inject(MatDialogRef<ClientFormDialogComponent>);
  private clientsService = inject(ClientsService);
  private lookupsService = inject(LookupsService);
  private snackbar = inject(SnackbarService);
  protected isAdmin = inject(CurrentUserService).isAdmin;
  private transloco = inject(TranslocoService);
  protected data = inject<ClientFormDialogData>(MAT_DIALOG_DATA);

  protected readonly isEdit = !!this.data.client;
  protected submitting = signal(false);
  protected clientSources = signal<ClientSource[]>([]);
  protected referralOptions = signal<Client[]>([]);

  protected form = new FormGroup({
    code: new FormControl(this.data.client?.code ?? '', { nonNullable: true, validators: [Validators.required] }),
    fullName: new FormControl(this.data.client?.fullName ?? '', { nonNullable: true, validators: [Validators.required] }),
    address: new FormControl(this.data.client?.address ?? ''),
    city: new FormControl(this.data.client?.city ?? ''),
    phone: new FormControl(this.data.client?.phone ?? ''),
    mobile: new FormControl(this.data.client?.mobile ?? ''),
    email: new FormControl(this.data.client?.email ?? '', { validators: [Validators.email] }),
    taxCode: new FormControl(this.data.client?.taxCode ?? ''),
    shippingAddress: new FormControl(this.data.client?.shippingAddress ?? ''),
    shippingCity: new FormControl(this.data.client?.shippingCity ?? ''),
    sourceId: new FormControl<number | null>(this.data.client?.sourceId ?? null),
  });

  /** Holds a `Client` once selected from the autocomplete, or a plain search string while typing. */
  protected referralControl = new FormControl<Client | string | null>(null);

  constructor() {
    this.lookupsService.getClientSources().subscribe((sources) => this.clientSources.set(sources));

    const referredById = this.data.client?.referredByClientId;
    if (referredById) {
      this.clientsService.getById(referredById).subscribe((referrer) => this.referralControl.setValue(referrer));
    }

    this.referralControl.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((value) => {
          const term = typeof value === 'string' ? value : value?.fullName;
          return term && term.length >= 2 ? this.clientsService.search(term) : of([]);
        }),
      )
      .subscribe((results) => this.referralOptions.set(results.filter((c) => c.id !== this.data.client?.id)));
  }

  displayReferral(value: Client | string | null): string {
    return typeof value === 'string' ? value : (value?.fullName ?? '');
  }

  onReferralSelected(event: MatAutocompleteSelectedEvent): void {
    this.referralControl.setValue(event.option.value as Client);
  }

  clearReferral(): void {
    this.referralControl.setValue(null);
  }

  async submit(): Promise<void> {
    if (this.form.invalid || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    const raw = this.form.getRawValue();
    const referral = this.referralControl.value;

    const request: UpsertClientRequest = {
      code: raw.code,
      fullName: raw.fullName,
      address: raw.address || undefined,
      city: raw.city || undefined,
      phone: raw.phone || undefined,
      mobile: raw.mobile || undefined,
      email: raw.email || undefined,
      taxCode: raw.taxCode || undefined,
      shippingAddress: raw.shippingAddress || undefined,
      shippingCity: raw.shippingCity || undefined,
      sourceId: raw.sourceId ?? undefined,
      referredByClientId: typeof referral === 'object' && referral ? referral.id : undefined,
    };

    const save$ = this.isEdit
      ? this.clientsService.update(this.data.client!.id, request)
      : this.clientsService.create(request);

    save$.subscribe({
      next: (client) => {
        this.snackbar.success(this.transloco.translate(this.isEdit ? 'clients.updated' : 'clients.created'));
        this.dialogRef.close(client);
      },
      error: (err: HttpErrorResponse) => {
        const key = err.status === 409 ? 'clients.duplicateCode' : 'clients.saveError';
        this.snackbar.error(this.transloco.translate(key));
        this.submitting.set(false);
      },
    });
  }

  /** Admin-only hard delete; the API refuses (400) while the client still has quotes or repairs. */
  deleteClient(): void {
    const client = this.data.client;
    if (!client || this.submitting() || !window.confirm(this.transloco.translate('clients.deletePrompt'))) {
      return;
    }
    this.submitting.set(true);
    this.clientsService.delete(client.id).subscribe({
      next: () => {
        this.snackbar.success(this.transloco.translate('clients.deleted'));
        this.dialogRef.close(true);
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        if (err.status === 400) {
          this.snackbar.error(this.transloco.translate('clients.deleteBlocked'));
        } else if (err.status !== 403) {
          this.snackbar.error(this.transloco.translate('clients.deleteError'));
        }
      },
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
