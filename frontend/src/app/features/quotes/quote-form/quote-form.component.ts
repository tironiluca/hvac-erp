import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { BreakpointObserver } from '@angular/cdk/layout';
import { StepperOrientation } from '@angular/cdk/stepper';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatStepperModule } from '@angular/material/stepper';
import { MatChipsModule } from '@angular/material/chips';
import { MatCardModule } from '@angular/material/card';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DatePipe, DecimalPipe } from '@angular/common';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { debounceTime, distinctUntilChanged, map, of, switchMap } from 'rxjs';
import { FGasRecordsSectionComponent } from '../../../shared/fgas-records-section/fgas-records-section.component';
import { FloorPlanSketchComponent } from '../../../shared/floor-plan-sketch/floor-plan-sketch.component';
import { QuotesService } from '../../../core/services/quotes.service';
import { ClientsService } from '../../../core/services/clients.service';
import { LookupsService } from '../../../core/services/lookups.service';
import { SnackbarService } from '../../../core/services/snackbar.service';
import { Client } from '../../../core/models/client.model';
import {
  ExtraType,
  LookupItem,
  Operator,
  Product,
  ServiceItem as ServiceItemLookup,
  VatOptionLookup,
} from '../../../core/models/lookup.model';
import {
  DuctSystemType,
  DuctedUnit,
  QuoteAttachment,
  QuoteDetail,
  QuoteExtraInput,
  QuoteExtraLine,
  QuoteProductInput,
  QuoteStatus,
  UpsertQuoteRequest,
  VatOption,
} from '../../../core/models/quote.model';
import { FloorPlanSketch } from '../../../core/models/floor-plan-sketch.model';
import { PriceEstimate, estimateQuotePrice } from '../quote-price-estimate';

type UnitGroup = FormGroup<{
  productId: FormControl<number | null>;
  quantity: FormControl<number>;
  warranty: FormControl<boolean>;
}>;

type OutletGroup = FormGroup<{
  systemType: FormControl<DuctSystemType>;
  ventCount: FormControl<number>;
}>;

type DuctedUnitGroup = FormGroup<{
  cloudQuantity: FormControl<number>;
  plenumQuantity: FormControl<number>;
  hasFlexibleHose: FormControl<boolean>;
  flexibleHoseQuantity: FormControl<number>;
  hasRigidHose: FormControl<boolean>;
  rigidHoseQuantity: FormControl<number>;
  remoteQuantity: FormControl<number>;
  outlets: FormArray<OutletGroup>;
}>;

type ServiceItemGroup = FormGroup<{
  serviceItemId: FormControl<number | null>;
  quantity: FormControl<number>;
}>;

type ExtraGroup = FormGroup<{
  extraTypeId: FormControl<number>;
  label: FormControl<string>;
  quantityBased: FormControl<boolean>;
  selected: FormControl<boolean>;
  quantity: FormControl<number>;
}>;

@Component({
  selector: 'hvac-quote-form',
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
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatStepperModule,
    MatChipsModule,
    MatCardModule,
    MatTooltipModule,
    TranslocoModule,
    FGasRecordsSectionComponent,
    FloorPlanSketchComponent,
  ],
  templateUrl: './quote-form.component.html',
  styleUrl: './quote-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QuoteFormComponent {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private quotesService = inject(QuotesService);
  private clientsService = inject(ClientsService);
  private lookupsService = inject(LookupsService);
  private snackbar = inject(SnackbarService);
  private transloco = inject(TranslocoService);
  private breakpoints = inject(BreakpointObserver);

  /** Horizontal stepper on tablet/desktop, vertical (stacked) below 800px so the 6 step labels fit. */
  protected stepperOrientation = toSignal(
    this.breakpoints
      .observe('(min-width: 800px)')
      .pipe(map((state): StepperOrientation => (state.matches ? 'horizontal' : 'vertical'))),
    { initialValue: 'horizontal' as StepperOrientation },
  );

  protected loading = signal(true);
  protected saving = signal(false);
  protected uploadingAttachment = signal(false);
  protected generatingPdf = signal(false);
  protected togglingSf = signal(false);

  protected quoteId = signal<number | null>(null);
  protected quote = signal<QuoteDetail | null>(null);
  private parentQuoteId?: number;

  protected operators = signal<Operator[]>([]);
  protected externalProducts = signal<Product[]>([]);
  protected internalProducts = signal<Product[]>([]);
  protected serviceItemsList = signal<ServiceItemLookup[]>([]);
  protected extraTypes = signal<ExtraType[]>([]);

  // Phase 8c lookup tables — option set + order from the server, labels via Transloco.
  protected vatOptions = signal<VatOptionLookup[]>([]);
  protected departments = signal<LookupItem[]>([]);
  protected ductSystemTypes = signal<LookupItem[]>([]);

  /** Quote extras held until the extras form array can be built from the lookup. */
  private pendingExtras: QuoteExtraLine[] = [];

  protected clientControl = new FormControl<Client | string | null>(null, {
    validators: [(control) => (control.value && typeof control.value === 'object' ? null : { required: true })],
  });
  protected clientOptions = signal<Client[]>([]);

  protected form = new FormGroup({
    operatorId: new FormControl<number | null>(null),
    department: new FormControl<'Store' | 'Field'>('Store', { nonNullable: true, validators: [Validators.required] }),
    predisposed: new FormControl(false, { nonNullable: true }),
    vat: new FormControl<VatOption>('Standard22', { nonNullable: true, validators: [Validators.required] }),
    manualDiscount: new FormControl(0, { nonNullable: true }),
    eneaEligible: new FormControl(false, { nonNullable: true }),
    eneaPrice: new FormControl<number | null>(null),
    raeeEligible: new FormControl(false, { nonNullable: true }),
    raeePrice: new FormControl<number | null>(null),
    sfRequested: new FormControl(false, { nonNullable: true }),
    installationAddress: new FormControl(''),
    installationCity: new FormControl(''),
    installationPositionNotes: new FormControl(''),
    floorPlanSketch: new FormControl<FloorPlanSketch | null>(null),
    externalUnit: this.createUnitGroup(),
    internalUnits: new FormArray<UnitGroup>([]),
    ductedUnits: new FormArray<DuctedUnitGroup>([]),
    serviceItems: new FormArray<ServiceItemGroup>([]),
    extras: new FormArray<ExtraGroup>([]),
  });

  private formTick = toSignal(this.form.valueChanges, { initialValue: null });
  protected formValue = computed(() => {
    this.formTick();
    return this.form.getRawValue();
  });

  protected estimate = computed<PriceEstimate>(() =>
    estimateQuotePrice(this.formValue(), {
      products: [...this.externalProducts(), ...this.internalProducts()],
      serviceItems: this.serviceItemsList(),
      extraTypes: this.extraTypes(),
      vatOptions: this.vatOptions(),
    }),
  );

  constructor() {
    this.lookupsService.getOperators().subscribe((o) => this.operators.set(o));
    this.lookupsService.getProducts('Outdoor').subscribe((p) => this.externalProducts.set(p));
    this.lookupsService.getProducts('Indoor').subscribe((p) => this.internalProducts.set(p));
    this.lookupsService.getServiceItems().subscribe((s) => this.serviceItemsList.set(s));
    this.lookupsService.getVatOptions().subscribe((v) => this.vatOptions.set(v));
    this.lookupsService.getQuoteDepartments().subscribe((d) => this.departments.set(d));
    this.lookupsService.getDuctSystemTypes().subscribe((t) => this.ductSystemTypes.set(t));
    this.lookupsService.getExtraTypes().subscribe((types) => {
      this.extraTypes.set([...types].sort((a, b) => a.sortOrder - b.sortOrder));
      this.buildExtrasArray();
    });

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
      this.quoteId.set(id);
      this.quotesService.getById(id).subscribe((detail) => {
        this.populateForm(detail);
        this.quote.set(detail);
        this.loading.set(false);
      });
    } else {
      this.loading.set(false);
      const clientIdParam = this.route.snapshot.queryParamMap.get('clientId');
      const parentQuoteIdParam = this.route.snapshot.queryParamMap.get('parentQuoteId');
      if (parentQuoteIdParam) {
        this.parentQuoteId = Number(parentQuoteIdParam);
      }
      if (clientIdParam) {
        this.clientsService.getById(Number(clientIdParam)).subscribe((client) => this.clientControl.setValue(client));
      }
    }
  }

  protected displayClient(value: Client | string | null): string {
    return typeof value === 'string' ? value : (value?.fullName ?? '');
  }

  protected onClientSelected(event: MatAutocompleteSelectedEvent): void {
    this.clientControl.setValue(event.option.value as Client);
  }

  protected statusKey(q: { currentStatus: QuoteStatus }): 'draft' | 'surveyed' | 'recalled' | 'confirmed' {
    switch (q.currentStatus) {
      case 'Confirmed':
        return 'confirmed';
      case 'Recalled':
        return 'recalled';
      case 'SurveyDone':
        return 'surveyed';
      default:
        return 'draft';
    }
  }

  protected addInternalUnit(): void {
    this.form.controls.internalUnits.push(this.createUnitGroup());
  }

  protected removeInternalUnit(index: number): void {
    this.form.controls.internalUnits.removeAt(index);
  }

  protected addDuctedUnit(): void {
    this.form.controls.ductedUnits.push(this.createDuctedUnitGroup());
  }

  protected removeDuctedUnit(index: number): void {
    this.form.controls.ductedUnits.removeAt(index);
  }

  protected addOutlet(ductedIndex: number): void {
    this.form.controls.ductedUnits.at(ductedIndex).controls.outlets.push(this.createOutletGroup());
  }

  protected removeOutlet(ductedIndex: number, outletIndex: number): void {
    this.form.controls.ductedUnits.at(ductedIndex).controls.outlets.removeAt(outletIndex);
  }

  protected addServiceItem(): void {
    this.form.controls.serviceItems.push(this.createServiceItemGroup());
  }

  protected removeServiceItem(index: number): void {
    this.form.controls.serviceItems.removeAt(index);
  }

  protected quoteLabel(detail: QuoteDetail): string {
    return detail.revision > 0 ? `${detail.number}/${detail.version}.${detail.revision}` : `${detail.number}/${detail.version}`;
  }

  protected async submit(): Promise<void> {
    this.clientControl.updateValueAndValidity();
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

    const products: QuoteProductInput[] = [];
    if (raw.externalUnit.productId) {
      products.push({
        productId: raw.externalUnit.productId,
        role: 'ExternalUnit',
        quantity: raw.externalUnit.quantity,
        warranty: raw.externalUnit.warranty,
      });
    }
    for (const line of raw.internalUnits) {
      if (line.productId) {
        products.push({ productId: line.productId, role: 'InternalUnit', quantity: line.quantity, warranty: line.warranty });
      }
    }

    const extras: QuoteExtraInput[] = [];
    for (const line of raw.extras) {
      const quantity = line.quantityBased ? line.quantity : line.selected ? 1 : 0;
      if (quantity > 0) {
        extras.push({ extraTypeId: line.extraTypeId, quantity });
      }
    }

    const request: UpsertQuoteRequest = {
      clientId: client.id,
      operatorId: raw.operatorId ?? undefined,
      parentQuoteId: this.parentQuoteId,
      department: raw.department,
      predisposed: raw.predisposed,
      vat: raw.vat,
      manualDiscount: raw.manualDiscount,
      eneaEligible: raw.eneaEligible,
      eneaPrice: raw.eneaEligible ? (raw.eneaPrice ?? undefined) : undefined,
      raeeEligible: raw.raeeEligible,
      raeePrice: raw.raeeEligible ? (raw.raeePrice ?? undefined) : undefined,
      sfRequested: raw.sfRequested,
      sfConfirmed: this.quote()?.sfConfirmed ?? false,
      installationAddress: raw.installationAddress?.trim() || undefined,
      installationCity: raw.installationCity?.trim() || undefined,
      installationPositionNotes: raw.installationPositionNotes || undefined,
      floorPlanSketch: raw.floorPlanSketch ?? null,
      products,
      ductedUnits: raw.ductedUnits.map((d) => ({
        cloudQuantity: d.cloudQuantity,
        plenumQuantity: d.plenumQuantity,
        hasFlexibleHose: d.hasFlexibleHose,
        flexibleHoseQuantity: d.flexibleHoseQuantity,
        hasRigidHose: d.hasRigidHose,
        rigidHoseQuantity: d.rigidHoseQuantity,
        remoteQuantity: d.remoteQuantity,
        outlets: d.outlets.map((o, index) => ({
          position: index + 1,
          systemType: o.systemType,
          ventCount: o.ventCount,
        })),
      })),
      serviceItems: raw.serviceItems
        .filter((s) => s.serviceItemId)
        .map((s) => ({ serviceItemId: s.serviceItemId as number, quantity: s.quantity })),
      extras,
    };

    const id = this.quoteId();
    const save$ = id ? this.quotesService.update(id, request) : this.quotesService.create(request);
    save$.subscribe({
      next: (detail) => {
        this.saving.set(false);
        this.snackbar.success(this.transloco.translate(id ? 'quotes.updated' : 'quotes.created'));
        if (id) {
          this.quote.set(detail);
        } else {
          this.router.navigate(['/quotes', detail.id], { replaceUrl: true });
        }
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        const message = err.status === 400 && err.error?.detail ? err.error.detail : this.transloco.translate('quotes.saveError');
        this.snackbar.error(message);
      },
    });
  }

  protected markSurveyDone(): void {
    const id = this.quoteId();
    if (!id) return;
    this.quotesService.markSurveyDone(id).subscribe(() => {
      this.snackbar.success(this.transloco.translate('quotes.surveyMarked'));
      this.reloadQuote(id);
    });
  }

  protected recall(): void {
    const id = this.quoteId();
    if (!id) return;
    const notes = window.prompt(this.transloco.translate('quotes.recallPrompt')) ?? undefined;
    this.quotesService.recall(id, { notes }).subscribe(() => {
      this.snackbar.success(this.transloco.translate('quotes.recalled'));
      this.reloadQuote(id);
    });
  }

  protected confirmQuote(): void {
    const id = this.quoteId();
    if (!id || !window.confirm(this.transloco.translate('quotes.confirmPrompt'))) return;
    this.quotesService.confirm(id).subscribe(() => {
      this.snackbar.success(this.transloco.translate('quotes.confirmed'));
      this.reloadQuote(id);
    });
  }

  protected toggleSfConfirmation(): void {
    const id = this.quoteId();
    if (!id || this.togglingSf()) return;
    this.togglingSf.set(true);
    this.quotesService.toggleSfConfirmation(id).subscribe({
      next: () => {
        this.togglingSf.set(false);
        this.snackbar.success(this.transloco.translate('quotes.sfConfirmationToggled'));
        this.reloadQuote(id);
      },
      error: (err: HttpErrorResponse) => {
        this.togglingSf.set(false);
        const message = err.status === 400 && err.error?.detail ? err.error.detail : this.transloco.translate('quotes.saveError');
        this.snackbar.error(message);
      },
    });
  }

  protected deleteQuote(): void {
    const id = this.quoteId();
    if (!id || !window.confirm(this.transloco.translate('quotes.deletePrompt'))) return;
    this.quotesService.delete(id).subscribe(() => {
      this.snackbar.success(this.transloco.translate('quotes.deleted'));
      this.router.navigate(['/quotes']);
    });
  }

  protected generatePdf(): void {
    const id = this.quoteId();
    if (!id) return;
    this.generatingPdf.set(true);
    this.quotesService.generatePdf(id).subscribe({
      next: (res) => {
        this.generatingPdf.set(false);
        this.reloadQuote(id);
        window.open(res.url, '_blank');
      },
      error: () => {
        this.generatingPdf.set(false);
        this.snackbar.error(this.transloco.translate('quotes.pdfError'));
      },
    });
  }

  protected newRevisionForClient(): void {
    const q = this.quote();
    if (!q) return;
    this.router.navigate(['/quotes', 'new'], { queryParams: { clientId: q.clientId, parentQuoteId: q.id } });
  }

  protected onFileSelected(event: Event): void {
    const id = this.quoteId();
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!id || !file) return;

    this.uploadingAttachment.set(true);
    this.quotesService.uploadAttachment(id, file).subscribe({
      next: (attachment) => {
        this.uploadingAttachment.set(false);
        this.quote.update((q) => (q ? { ...q, attachments: [...q.attachments, attachment] } : q));
        input.value = '';
      },
      error: () => {
        this.uploadingAttachment.set(false);
        this.snackbar.error(this.transloco.translate('quotes.attachmentError'));
        input.value = '';
      },
    });
  }

  protected removeAttachment(attachment: QuoteAttachment): void {
    const id = this.quoteId();
    if (!id) return;
    this.quotesService.deleteAttachment(id, attachment.id).subscribe(() => {
      this.quote.update((q) => (q ? { ...q, attachments: q.attachments.filter((a) => a.id !== attachment.id) } : q));
    });
  }

  private reloadQuote(id: number): void {
    this.quotesService.getById(id).subscribe((detail) => this.quote.set(detail));
  }

  private populateForm(detail: QuoteDetail): void {
    this.clientsService.getById(detail.clientId).subscribe((client) => this.clientControl.setValue(client));

    this.form.patchValue({
      operatorId: detail.operatorId ?? null,
      department: detail.department,
      predisposed: detail.predisposed,
      vat: detail.vat,
      manualDiscount: detail.manualDiscount,
      eneaEligible: detail.eneaEligible,
      eneaPrice: detail.eneaPrice ?? null,
      raeeEligible: detail.raeeEligible,
      raeePrice: detail.raeePrice ?? null,
      sfRequested: detail.sfRequested,
      installationAddress: detail.installationAddress ?? '',
      installationCity: detail.installationCity ?? '',
      installationPositionNotes: detail.installationPositionNotes ?? '',
      floorPlanSketch: detail.floorPlanSketch ?? null,
    });

    const external = detail.products.find((p) => p.role === 'ExternalUnit');
    if (external) {
      this.form.controls.externalUnit.patchValue({
        productId: external.productId,
        quantity: external.quantity,
        warranty: external.warranty,
      });
    }

    for (const p of detail.products.filter((p) => p.role === 'InternalUnit')) {
      this.form.controls.internalUnits.push(this.createUnitGroup(p.productId, p.quantity, p.warranty));
    }
    for (const d of detail.ductedUnits) {
      this.form.controls.ductedUnits.push(this.createDuctedUnitGroup(d));
    }
    for (const s of detail.serviceItems) {
      this.form.controls.serviceItems.push(this.createServiceItemGroup(s.serviceItemId, s.quantity));
    }

    this.pendingExtras = detail.extras;
    this.buildExtrasArray();

    if (detail.currentStatus === 'Confirmed') {
      this.form.disable({ emitEvent: false });
      this.clientControl.disable({ emitEvent: false });
    }
  }

  /** (Re)builds the extras form array from the lookup, overlaying any extras on the loaded quote. */
  private buildExtrasArray(): void {
    const types = this.extraTypes();
    if (types.length === 0) {
      return;
    }

    const saved = new Map(this.pendingExtras.map((e) => [e.extraTypeId, e]));
    this.form.controls.extras.clear({ emitEvent: false });

    for (const type of types) {
      const line = saved.get(type.id);
      this.form.controls.extras.push(
        new FormGroup({
          extraTypeId: new FormControl(type.id, { nonNullable: true }),
          label: new FormControl(type.label, { nonNullable: true }),
          quantityBased: new FormControl(type.quantityBased, { nonNullable: true }),
          selected: new FormControl(!type.quantityBased && line != null, { nonNullable: true }),
          quantity: new FormControl(type.quantityBased ? (line?.quantity ?? 0) : 1, { nonNullable: true, validators: [Validators.min(0)] }),
        }),
        { emitEvent: false },
      );
    }

    if (this.quote()?.currentStatus === 'Confirmed') {
      this.form.controls.extras.disable({ emitEvent: false });
    }
  }

  private createUnitGroup(productId: number | null = null, quantity = 1, warranty = false): UnitGroup {
    return new FormGroup({
      productId: new FormControl<number | null>(productId, { validators: [Validators.required] }),
      quantity: new FormControl(quantity, { nonNullable: true, validators: [Validators.required, Validators.min(1)] }),
      warranty: new FormControl(warranty, { nonNullable: true }),
    });
  }

  private createOutletGroup(systemType: DuctSystemType = 'Standard', ventCount = 2): OutletGroup {
    return new FormGroup({
      systemType: new FormControl<DuctSystemType>(systemType, { nonNullable: true }),
      ventCount: new FormControl(ventCount, { nonNullable: true, validators: [Validators.required, Validators.min(1)] }),
    });
  }

  private createDuctedUnitGroup(d?: DuctedUnit): DuctedUnitGroup {
    const outlets = new FormArray<OutletGroup>([]);
    if (d && d.outlets.length > 0) {
      for (const o of d.outlets) {
        outlets.push(this.createOutletGroup(o.systemType, o.ventCount));
      }
    } else {
      outlets.push(this.createOutletGroup());
    }

    return new FormGroup({
      cloudQuantity: new FormControl(d?.cloudQuantity ?? 0, { nonNullable: true }),
      plenumQuantity: new FormControl(d?.plenumQuantity ?? 0, { nonNullable: true }),
      hasFlexibleHose: new FormControl(d?.hasFlexibleHose ?? false, { nonNullable: true }),
      flexibleHoseQuantity: new FormControl(d?.flexibleHoseQuantity ?? 0, { nonNullable: true }),
      hasRigidHose: new FormControl(d?.hasRigidHose ?? false, { nonNullable: true }),
      rigidHoseQuantity: new FormControl(d?.rigidHoseQuantity ?? 0, { nonNullable: true }),
      remoteQuantity: new FormControl(d?.remoteQuantity ?? 0, { nonNullable: true }),
      outlets,
    });
  }

  private createServiceItemGroup(serviceItemId: number | null = null, quantity = 1): ServiceItemGroup {
    return new FormGroup({
      serviceItemId: new FormControl<number | null>(serviceItemId, { validators: [Validators.required] }),
      quantity: new FormControl(quantity, { nonNullable: true, validators: [Validators.required, Validators.min(0.01)] }),
    });
  }
}
