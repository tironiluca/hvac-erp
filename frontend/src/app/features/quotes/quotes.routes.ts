import { Routes } from '@angular/router';

export const QUOTES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./quotes-list/quotes-list.component').then((m) => m.QuotesListComponent),
  },
  {
    path: 'new',
    loadComponent: () => import('./quote-form/quote-form.component').then((m) => m.QuoteFormComponent),
  },
  {
    path: ':id',
    loadComponent: () => import('./quote-form/quote-form.component').then((m) => m.QuoteFormComponent),
  },
];
