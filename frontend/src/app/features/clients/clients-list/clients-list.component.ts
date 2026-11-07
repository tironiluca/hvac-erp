import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { TranslocoModule } from '@jsverse/transloco';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { ClientsService } from '../../../core/services/clients.service';
import { Client } from '../../../core/models/client.model';
import { ClientFormDialogComponent } from '../client-form-dialog/client-form-dialog.component';

@Component({
  selector: 'hvac-clients-list',
  imports: [
    ReactiveFormsModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    TranslocoModule,
  ],
  templateUrl: './clients-list.component.html',
  styleUrl: './clients-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClientsListComponent {
  private clientsService = inject(ClientsService);
  private dialog = inject(MatDialog);

  protected readonly displayedColumns = ['code', 'fullName', 'city', 'contact', 'source'];
  protected searchControl = new FormControl('');
  protected loading = signal(true);
  protected clients = signal<Client[]>([]);

  constructor() {
    this.runSearch();

    this.searchControl.valueChanges.pipe(debounceTime(300), distinctUntilChanged()).subscribe((term) => {
      this.runSearch(term ?? undefined);
    });
  }

  openCreateDialog(): void {
    const ref = this.dialog.open(ClientFormDialogComponent, { data: {} });
    ref.afterClosed().subscribe((result) => {
      if (result) {
        this.runSearch(this.searchControl.value ?? undefined);
      }
    });
  }

  openEditDialog(client: Client): void {
    const ref = this.dialog.open(ClientFormDialogComponent, { data: { client } });
    ref.afterClosed().subscribe((result) => {
      if (result) {
        this.runSearch(this.searchControl.value ?? undefined);
      }
    });
  }

  private runSearch(term?: string): void {
    this.loading.set(true);
    this.clientsService.search(term).subscribe((clients) => {
      this.clients.set(clients);
      this.loading.set(false);
    });
  }
}
