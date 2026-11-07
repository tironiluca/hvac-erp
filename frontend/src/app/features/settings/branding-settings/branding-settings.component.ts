import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { debounceTime } from 'rxjs';
import { BrandingService } from '../../../core/services/branding.service';
import { CalendarPrefsService, WeekStart } from '../../../core/services/calendar-prefs.service';
import { SnackbarService } from '../../../core/services/snackbar.service';
import { BrandingAssetKind, PortalBranding } from '../../../core/models/branding.model';
import { DEFAULT_BRAND_SEEDS } from '../../../core/utils/m3-theme.util';

const HEX = /^#[0-9a-fA-F]{6}$/;
const MAX_ASSET_BYTES = 5_000_000;

interface AssetSlot {
  kind: BrandingAssetKind;
  labelKey: string;
  hintKey: string;
  accept: string;
}

@Component({
  selector: 'hvac-branding-settings',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatIconModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule,
    TranslocoModule,
  ],
  templateUrl: './branding-settings.component.html',
  styleUrl: './branding-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BrandingSettingsComponent {
  protected branding = inject(BrandingService);
  protected calendarPrefs = inject(CalendarPrefsService);
  private snackbar = inject(SnackbarService);
  private transloco = inject(TranslocoService);
  private destroyRef = inject(DestroyRef);

  protected saving = signal(false);
  protected uploading = signal<BrandingAssetKind | null>(null);

  protected readonly assetSlots: AssetSlot[] = [
    { kind: 'logo', labelKey: 'settings.logo', hintKey: 'settings.logoHint', accept: 'image/png,image/jpeg,image/webp,image/svg+xml' },
    { kind: 'logo-dark', labelKey: 'settings.logoDark', hintKey: 'settings.logoDarkHint', accept: 'image/png,image/jpeg,image/webp,image/svg+xml' },
    { kind: 'favicon', labelKey: 'settings.favicon', hintKey: 'settings.faviconHint', accept: 'image/png,image/x-icon,image/vnd.microsoft.icon' },
    { kind: 'login-background', labelKey: 'settings.loginBackground', hintKey: 'settings.loginBackgroundHint', accept: 'image/png,image/jpeg,image/webp' },
  ];

  protected form = new FormGroup({
    primarySeed: new FormControl(DEFAULT_BRAND_SEEDS.primary, { nonNullable: true, validators: [Validators.required, Validators.pattern(HEX)] }),
    tertiarySeed: new FormControl(DEFAULT_BRAND_SEEDS.tertiary, { nonNullable: true, validators: [Validators.required, Validators.pattern(HEX)] }),
    neutralSeed: new FormControl(DEFAULT_BRAND_SEEDS.neutral, { nonNullable: true, validators: [Validators.required, Validators.pattern(HEX)] }),
    portalTitle: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
  });

  /** True once the stored record differs from what's in the form (drives Save/Reset enablement). */
  protected readonly dirtyFromStored = computed(() => {
    const stored = this.branding.branding();
    if (!stored) return this.form.dirty;
    const v = this.formValue();
    return (
      v.primarySeed !== stored.primarySeed ||
      v.tertiarySeed !== stored.tertiarySeed ||
      v.neutralSeed !== stored.neutralSeed ||
      v.portalTitle !== stored.portalTitle
    );
  });

  private formValue = signal(this.form.getRawValue());

  constructor() {
    const stored = this.branding.branding();
    if (stored) {
      this.patchFromStored(stored);
    } else {
      this.branding.load().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((b) => this.patchFromStored(b));
    }

    this.form.valueChanges.pipe(debounceTime(120), takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.formValue.set(this.form.getRawValue());
      const { primarySeed, tertiarySeed, neutralSeed, portalTitle } = this.form.controls;
      if (primarySeed.valid && tertiarySeed.valid && neutralSeed.valid) {
        this.branding.preview({
          primarySeed: primarySeed.value,
          tertiarySeed: tertiarySeed.value,
          neutralSeed: neutralSeed.value,
          portalTitle: portalTitle.value,
        });
      }
    });

    // Drop the live preview if the admin leaves without saving.
    this.destroyRef.onDestroy(() => this.branding.preview(null));
  }

  private patchFromStored(b: PortalBranding): void {
    this.form.reset(
      {
        primarySeed: b.primarySeed,
        tertiarySeed: b.tertiarySeed,
        neutralSeed: b.neutralSeed,
        portalTitle: b.portalTitle,
      },
      { emitEvent: false },
    );
    this.formValue.set(this.form.getRawValue());
  }

  protected syncHex(control: 'primarySeed' | 'tertiarySeed' | 'neutralSeed', value: string): void {
    this.form.controls[control].setValue(value);
    this.form.controls[control].markAsDirty();
  }

  /** Local (per-browser) preference — applies immediately, no Save needed. */
  protected setWeekStart(value: WeekStart): void {
    this.calendarPrefs.setWeekStart(value);
  }

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      return;
    }
    this.saving.set(true);
    this.branding.update(this.form.getRawValue()).subscribe({
      next: (b) => {
        this.patchFromStored(b);
        this.saving.set(false);
        this.snackbar.success(this.transloco.translate('settings.saved'));
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        this.snackbar.error(err.error?.detail ?? this.transloco.translate('settings.saveError'));
      },
    });
  }

  protected revertChanges(): void {
    this.branding.revert().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((b) => {
      this.patchFromStored(b);
      this.snackbar.info(this.transloco.translate('settings.reverted'));
    });
  }

  protected onFileSelected(kind: BrandingAssetKind, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    if (file.size > MAX_ASSET_BYTES) {
      this.snackbar.error(this.transloco.translate('settings.assetTooLarge'));
      return;
    }

    this.uploading.set(kind);
    this.branding.uploadAsset(kind, file).subscribe({
      next: () => {
        this.uploading.set(null);
        this.snackbar.success(this.transloco.translate('settings.assetUploaded'));
      },
      error: (err: HttpErrorResponse) => {
        this.uploading.set(null);
        this.snackbar.error(err.error?.detail ?? this.transloco.translate('settings.assetError'));
      },
    });
  }

  protected assetUrl(kind: BrandingAssetKind): string | null {
    const b = this.branding.branding();
    if (!b) return null;
    return kind === 'logo'
      ? (b.logoUrl ?? null)
      : kind === 'logo-dark'
        ? (b.logoDarkUrl ?? null)
        : kind === 'favicon'
          ? (b.faviconUrl ?? null)
          : (b.loginBackgroundUrl ?? null);
  }
}
