import { Routes } from '@angular/router';
import { adminGuard } from '../../core/guards/admin.guard';

export const SETTINGS_ROUTES: Routes = [
  { path: '', redirectTo: 'branding', pathMatch: 'full' },
  {
    path: 'branding',
    canActivate: [adminGuard],
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
