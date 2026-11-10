import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { memberGuard } from './core/guards/admin.guard';

export const routes: Routes = [
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },
  {
    // Signed in, but not a member of any company yet.
    path: 'no-access',
    loadComponent: () => import('./core/pages/no-access/no-access.component').then((m) => m.NoAccessComponent),
    canActivate: [authGuard],
  },
  {
    path: '',
    loadComponent: () => import('./core/shell/shell.component').then((m) => m.ShellComponent),
    canActivate: [authGuard, memberGuard],
    children: [
      {
        path: '',
        loadComponent: () => import('./core/pages/dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'clients',
        loadChildren: () => import('./features/clients/clients.routes').then((m) => m.CLIENTS_ROUTES),
      },
      {
        path: 'quotes',
        loadChildren: () => import('./features/quotes/quotes.routes').then((m) => m.QUOTES_ROUTES),
      },
      {
        path: 'repairs',
        loadChildren: () => import('./features/repairs/repairs.routes').then((m) => m.REPAIRS_ROUTES),
      },
      {
        path: 'installations',
        loadChildren: () => import('./features/installations/installations.routes').then((m) => m.INSTALLATIONS_ROUTES),
      },
      {
        path: 'settings',
        loadChildren: () => import('./features/settings/settings.routes').then((m) => m.SETTINGS_ROUTES),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
