import { Routes } from '@angular/router';
import { adminGuard, platformAdminGuard } from '../../core/guards/admin.guard';

export const SETTINGS_ROUTES: Routes = [
  { path: '', redirectTo: 'branding', pathMatch: 'full' },
  {
    path: 'branding',
    // The branding is shared by every tenant until it becomes a per-tenant setting.
    canActivate: [platformAdminGuard],
    loadComponent: () =>
      import('./branding-settings/branding-settings.component').then((m) => m.BrandingSettingsComponent),
  },
  {
    path: 'pdf-templates',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./pdf-templates/pdf-templates.component').then((m) => m.PdfTemplatesComponent),
  },
];
