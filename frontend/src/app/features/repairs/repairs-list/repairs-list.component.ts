import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { Router } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslocoModule } from '@jsverse/transloco';
import { RepairsService } from '../../../core/services/repairs.service';
import { RepairListItem } from '../../../core/models/repair.model';
import { repairStatusColorClass } from '../repair-status';

@Component({
  selector: 'hvac-repairs-list',
  imports: [
    DatePipe,
    DecimalPipe,
    MatTableModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatTooltipModule,
    TranslocoModule,
  ],
  templateUrl: './repairs-list.component.html',
  styleUrl: './repairs-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RepairsListComponent {
  private repairsService = inject(RepairsService);
  private router = inject(Router);

  protected readonly displayedColumns = ['type', 'client', 'brand', 'status', 'total', 'insertedAt'];
  protected loading = signal(true);
  protected repairs = signal<RepairListItem[]>([]);

  constructor() {
    this.repairsService.getList().subscribe((repairs) => {
      this.repairs.set(repairs);
      this.loading.set(false);
    });
  }

  openNew(): void {
    this.router.navigate(['/repairs', 'new']);
  }

  openDetail(repair: RepairListItem): void {
    this.router.navigate(['/repairs', repair.id]);
  }

  statusClass(repair: RepairListItem): string {
    return repairStatusColorClass(repair.status);
  }
}
