import { Routes } from '@angular/router';

export const REPAIRS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./repairs-list/repairs-list.component').then((m) => m.RepairsListComponent),
  },
  {
    path: 'new',
    loadComponent: () => import('./repair-form/repair-form.component').then((m) => m.RepairFormComponent),
  },
  {
    path: ':id',
    loadComponent: () => import('./repair-form/repair-form.component').then((m) => m.RepairFormComponent),
  },
];
