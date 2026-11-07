import { Routes } from '@angular/router';

export const INSTALLATIONS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./installation-calendar/installation-calendar.component').then((m) => m.InstallationCalendarComponent),
  },
  {
    path: 'list',
    loadComponent: () => import('./installations-list/installations-list.component').then((m) => m.InstallationsListComponent),
  },
  {
    path: 'new',
    loadComponent: () => import('./installation-form/installation-form.component').then((m) => m.InstallationFormComponent),
  },
  {
    path: ':id',
    loadComponent: () => import('./installation-form/installation-form.component').then((m) => m.InstallationFormComponent),
  },
];
