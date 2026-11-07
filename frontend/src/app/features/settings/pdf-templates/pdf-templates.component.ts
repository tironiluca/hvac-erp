import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { Observable, Subject, debounceTime, switchMap } from 'rxjs';
import { PdfTemplatesService } from '../../../core/services/pdf-templates.service';
import { SnackbarService } from '../../../core/services/snackbar.service';
import {
  PDF_DOCUMENT_TYPES,
  PdfDocumentType,
  PdfSection,
  PdfSectionCatalogEntry,
  PdfTemplate,
  PdfTemplateCatalog,
  PdfLanguage,
  PdfTemplateDefinition,
  PdfTemplateListItem,
} from '../../../core/models/pdf-template.model';

interface SectionRow {
  section: PdfSection;
  index: number;
  entry: PdfSectionCatalogEntry;
}

function clamp(value: number, min: number, max: number, fallback: number): number {
  if (Number.isNaN(value)) return fallback;
  return Math.min(max, Math.max(min, value));
}

@Component({
  selector: 'hvac-pdf-templates',
  imports: [
    ReactiveFormsModule,
    DragDropModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatCardModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatTooltipModule,
    TranslocoModule,
  ],
  templateUrl: './pdf-templates.component.html',
  styleUrl: './pdf-templates.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PdfTemplatesComponent {
  private api = inject(PdfTemplatesService);
  private snackbar = inject(SnackbarService);
  private transloco = inject(TranslocoService);
  private sanitizer = inject(DomSanitizer);
  private destroyRef = inject(DestroyRef);

  protected readonly documentTypes = PDF_DOCUMENT_TYPES;
  protected readonly pageSizes = ['A4', 'Letter'] as const;
  protected readonly pdfLanguages: readonly PdfLanguage[] = ['en', 'it'];

  protected docType = signal<PdfDocumentType>('Quote');
  protected templates = signal<PdfTemplateListItem[]>([]);
  protected selectedId = signal<number | null>(null);
  protected stored = signal<PdfTemplate | null>(null);
  protected working = signal<PdfTemplateDefinition | null>(null);
  protected catalog = signal<PdfTemplateCatalog | null>(null);

  protected saving = signal(false);
  protected busy = signal(false);
  protected previewLoading = signal(false);
  protected previewUrl = signal<SafeResourceUrl | null>(null);

  protected nameControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(120)],
  });
  private nameValue = toSignal(this.nameControl.valueChanges, { initialValue: '' });

  private catalogCache = new Map<PdfDocumentType, PdfTemplateCatalog>();
  private previewTrigger = new Subject<void>();
  private lastObjectUrl: string | null = null;

  protected readonly selected = computed(
    () => this.templates().find((t) => t.id === this.selectedId()) ?? null,
  );

  protected readonly dirty = computed(() => {
    const stored = this.stored();
    const working = this.working();
    if (!stored || !working) return false;
    return (
      JSON.stringify(working) !== JSON.stringify(stored.definition) ||
      this.nameValue().trim() !== stored.name
    );
  });

  /** Working sections paired with their catalog entry, in working order. */
  protected readonly sectionRows = computed<SectionRow[]>(() => {
    const cat = this.catalog();
    const working = this.working();
    if (!cat || !working) return [];
    const byType = new Map(cat.sections.map((s) => [s.type, s]));
    return working.sections
      .map((section, index) => ({ section, index, entry: byType.get(section.type)! }))
      .filter((row) => !!row.entry);
  });

  /** Catalog sections that may be added more than once (only custom.text today). */
  protected readonly addableSections = computed(
    () => this.catalog()?.sections.filter((s) => s.repeatable) ?? [],
  );

  constructor() {
    effect(() => {
      const type = this.docType();
      this.loadCatalog(type);
      this.loadList(type);
    });

    this.previewTrigger
      .pipe(
        debounceTime(500),
        switchMap(() => {
          this.previewLoading.set(true);
          return this.api.preview(this.docType(), this.working()!);
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (blob) => this.showPreview(blob),
        error: () => {
          this.previewLoading.set(false);
          this.snackbar.error(this.transloco.translate('pdfTemplates.previewError'));
        },
      });

    this.destroyRef.onDestroy(() => this.revokeObjectUrl());
  }

  // --- Loading ---------------------------------------------------------------------------

  private loadCatalog(type: PdfDocumentType): void {
    const cached = this.catalogCache.get(type);
    if (cached) {
      this.catalog.set(cached);
      return;
    }
    this.api
      .catalog(type)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((cat) => {
        this.catalogCache.set(type, cat);
        if (this.docType() === type) this.catalog.set(cat);
      });
  }

  private loadList(type: PdfDocumentType, selectId?: number): void {
    this.api
      .list(type)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((list) => {
        if (this.docType() !== type) return;
        this.templates.set(list);
        const target = selectId ?? (list.find((t) => t.isActive) ?? list[0])?.id ?? null;
        if (target !== null) {
          this.selectedId.set(null); // force selectTemplate to refetch
          this.selectTemplate(target);
        } else {
          this.selectedId.set(null);
          this.stored.set(null);
          this.working.set(null);
        }
      });
  }

  protected selectTemplate(id: number): void {
    if (id === this.selectedId()) return;
    this.selectedId.set(id);
    this.api
      .get(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((template) => {
        this.stored.set(template);
        this.working.set(structuredClone(template.definition));
        this.nameControl.setValue(template.name);
        this.queuePreview();
      });
  }

  // --- Editing --------------------------------------------------------------------------

  /** Apply a mutation to a fresh clone of the working definition, then re-preview. */
  private patch(mutate: (def: PdfTemplateDefinition) => void): void {
    const current = this.working();
    if (!current) return;
    const next = structuredClone(current);
    mutate(next);
    this.working.set(next);
    this.queuePreview();
  }

  // Concrete setters (templates stay arrow-function-free).
  protected setPageSize(v: string): void {
    this.patch((d) => (d.page.size = v as PdfTemplateDefinition['page']['size']));
  }
  protected setFont(v: string): void {
    this.patch((d) => (d.page.fontFamily = v));
  }
  protected setMargin(v: string): void {
    this.patch((d) => (d.page.margin = clamp(+v, 8, 72, 30)));
  }
  protected setBaseFontSize(v: string): void {
    this.patch((d) => (d.page.baseFontSize = clamp(+v, 7, 14, 10)));
  }
  protected setAccent(v: string): void {
    this.patch((d) => (d.page.accentColor = v));
  }
  protected setLanguage(v: PdfLanguage): void {
    this.patch((d) => (d.page.language = v));
  }
  protected setHeaderFlag(
    key: 'showLogo' | 'showTitle' | 'showDocumentMeta' | 'showAgent' | 'showClientBlock',
    v: boolean,
  ): void {
    this.patch((d) => (d.header[key] = v));
  }
  protected setHeaderTitle(v: string): void {
    this.patch((d) => (d.header.titleOverride = v.trim() || null));
  }
  protected setSectionEnabled(index: number, v: boolean): void {
    this.patch((d) => (d.sections[index].enabled = v));
  }
  protected setSectionTitle(index: number, v: string): void {
    this.patch((d) => (d.sections[index].titleOverride = v.trim() || null));
  }
  protected setSectionBody(index: number, v: string): void {
    this.patch((d) => (d.sections[index].body = v || null));
  }
  protected setSectionOption(index: number, key: string, v: boolean): void {
    this.patch((d) => (d.sections[index].options[key] = v));
  }
  protected optionChecked(section: PdfSection, key: string, defaultOn: boolean): boolean {
    const value = section.options[key];
    return value === undefined ? defaultOn : value;
  }
  protected setFooterPageNumbers(v: boolean): void {
    this.patch((d) => (d.footer.showPageNumbers = v));
  }

  protected onSectionDrop(event: CdkDragDrop<unknown>): void {
    this.patch((def) => moveItemInArray(def.sections, event.previousIndex, event.currentIndex));
  }

  protected toggleColumn(index: number, entry: PdfSectionCatalogEntry, key: string, on: boolean): void {
    this.patch((def) => {
      const set = new Set(def.sections[index].columns);
      if (on) set.add(key);
      else set.delete(key);
      def.sections[index].columns = entry.columns.map((c) => c.key).filter((k) => set.has(k));
    });
  }

  protected toggleClientField(key: string, on: boolean): void {
    const order = this.catalog()?.clientFields.map((f) => f.key) ?? [];
    this.patch((def) => {
      const set = new Set(def.header.clientFields);
      if (on) set.add(key);
      else set.delete(key);
      def.header.clientFields = order.filter((k) => set.has(k));
    });
  }

  protected addSection(type: string): void {
    this.patch((def) =>
      def.sections.push({ type, enabled: true, titleOverride: '', columns: [], options: {}, body: '' }),
    );
  }

  protected removeSection(index: number): void {
    this.patch((def) => def.sections.splice(index, 1));
  }

  protected footerText(): string {
    return (this.working()?.footer.lines ?? []).join('\n');
  }

  protected setFooterText(value: string): void {
    const lines = value
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line.length > 0);
    this.patch((def) => (def.footer.lines = lines));
  }

  // --- Actions -------------------------------------------------------------------------

  protected save(): void {
    const working = this.working();
    const id = this.selectedId();
    if (!working || id === null || this.nameControl.invalid || this.saving() || !this.dirty()) return;

    this.saving.set(true);
    this.api
      .update(id, { documentType: this.docType(), name: this.nameControl.value.trim(), definition: working })
      .subscribe({
        next: (template) => {
          this.saving.set(false);
          this.applySaved(template);
          this.snackbar.success(this.transloco.translate('pdfTemplates.saved'));
        },
        error: (err: HttpErrorResponse) => {
          this.saving.set(false);
          this.snackbar.error(err.error?.detail ?? this.transloco.translate('pdfTemplates.saveError'));
        },
      });
  }

  protected revert(): void {
    const stored = this.stored();
    if (!stored) return;
    this.working.set(structuredClone(stored.definition));
    this.nameControl.setValue(stored.name);
    this.queuePreview();
  }

  protected newFromDefault(): void {
    const system = this.templates().find((t) => t.isSystemDefault);
    if (!system) return;
    this.runMutation(
      this.api.duplicate(system.id, this.uniqueName(this.transloco.translate('pdfTemplates.newName'))),
    );
  }

  protected duplicate(): void {
    const current = this.selected();
    if (!current) return;
    const suffix = this.transloco.translate('pdfTemplates.copySuffix');
    this.runMutation(this.api.duplicate(current.id, this.uniqueName(`${current.name} ${suffix}`)));
  }

  protected activate(): void {
    const id = this.selectedId();
    if (id === null || this.selected()?.isActive) return;
    this.runMutation(this.api.activate(id));
  }

  protected restoreDefaults(): void {
    const id = this.selectedId();
    if (id === null || !confirm(this.transloco.translate('pdfTemplates.restoreConfirm'))) return;
    this.saving.set(true);
    this.api.restoreDefaults(id).subscribe({
      next: (template) => {
        this.saving.set(false);
        this.applySaved(template);
        this.snackbar.success(this.transloco.translate('pdfTemplates.restored'));
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        this.snackbar.error(err.error?.detail ?? this.transloco.translate('pdfTemplates.saveError'));
      },
    });
  }

  protected remove(): void {
    const id = this.selectedId();
    const current = this.selected();
    if (id === null || !current || current.isSystemDefault || current.isActive) return;
    if (!confirm(this.transloco.translate('pdfTemplates.deleteConfirm', { name: current.name }))) return;

    this.busy.set(true);
    this.api.remove(id).subscribe({
      next: () => {
        this.busy.set(false);
        this.loadList(this.docType());
        this.snackbar.success(this.transloco.translate('pdfTemplates.deleted'));
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        this.snackbar.error(err.error?.detail ?? this.transloco.translate('pdfTemplates.saveError'));
      },
    });
  }

  private runMutation(request: Observable<PdfTemplate>): void {
    this.busy.set(true);
    request.subscribe({
      next: (template) => {
        this.busy.set(false);
        this.loadList(this.docType(), template.id);
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        this.snackbar.error(err.error?.detail ?? this.transloco.translate('pdfTemplates.saveError'));
      },
    });
  }

  private applySaved(template: PdfTemplate): void {
    this.stored.set(template);
    this.working.set(structuredClone(template.definition));
    this.nameControl.setValue(template.name);
    this.templates.update((list) =>
      list.map((t) => (t.id === template.id ? { ...t, name: template.name } : t)),
    );
    this.queuePreview();
  }

  private uniqueName(base: string): string {
    const taken = new Set(this.templates().map((t) => t.name.toLowerCase()));
    if (!taken.has(base.toLowerCase())) return base;
    for (let i = 2; i < 100; i++) {
      if (!taken.has(`${base} ${i}`.toLowerCase())) return `${base} ${i}`;
    }
    return `${base} ${Date.now()}`;
  }

  // --- Preview -------------------------------------------------------------------------

  protected queuePreview(): void {
    if (this.working()) this.previewTrigger.next();
  }

  private showPreview(blob: Blob): void {
    this.revokeObjectUrl();
    this.lastObjectUrl = URL.createObjectURL(blob);
    this.previewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(`${this.lastObjectUrl}#toolbar=0`));
    this.previewLoading.set(false);
  }

  private revokeObjectUrl(): void {
    if (this.lastObjectUrl) {
      URL.revokeObjectURL(this.lastObjectUrl);
      this.lastObjectUrl = null;
    }
  }
}
