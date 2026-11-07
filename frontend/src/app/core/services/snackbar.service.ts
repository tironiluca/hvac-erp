import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

@Injectable({ providedIn: 'root' })
export class SnackbarService {
  private snackbar = inject(MatSnackBar);

  /** Shows a 3 s success toast. */
  success(message: string): void {
    this.snackbar.open(message, undefined, { duration: 3000, panelClass: 'success-snackbar' });
  }

  /** Shows a 5 s error toast. */
  error(message: string): void {
    this.snackbar.open(message, undefined, { duration: 5000, panelClass: 'error-snackbar' });
  }

  /** Shows a neutral 3 s toast. */
  info(message: string): void {
    this.snackbar.open(message, undefined, { duration: 3000 });
  }
}
