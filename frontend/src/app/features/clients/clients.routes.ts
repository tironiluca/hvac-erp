import { Routes } from '@angular/router';

export const CLIENTS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./clients-list/clients-list.component').then((m) => m.ClientsListComponent),
  },
];
