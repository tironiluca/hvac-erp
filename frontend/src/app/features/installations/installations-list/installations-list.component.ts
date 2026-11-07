import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { TranslocoModule } from '@jsverse/transloco';
import { InstallationsService } from '../../../core/services/installations.service';
import { ScheduledInstallation } from '../../../core/models/installation.model';

@Component({
  selector: 'hvac-installations-list',
  imports: [DatePipe, MatTableModule, MatIconModule, MatButtonModule, MatProgressSpinnerModule, MatChipsModule, TranslocoModule],
  templateUrl: './installations-list.component.html',
  styleUrl: './installations-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InstallationsListComponent {
  private installationsService = inject(InstallationsService);
  private router = inject(Router);

  protected readonly displayedColumns = ['date', 'slot', 'client', 'crew', 'pdf'];
  protected loading = signal(true);
  protected installations = signal<ScheduledInstallation[]>([]);

  constructor() {
    this.installationsService.getList().subscribe((installations) => {
      this.installations.set(installations);
      this.loading.set(false);
    });
  }

  crewLabel(installation: ScheduledInstallation): string {
    return installation.installers.map((i) => i.name).join(', ') || '-';
  }

  openCalendar(): void {
    this.router.navigate(['/installations']);
  }

  openNew(): void {
    this.router.navigate(['/installations', 'new']);
  }

  openDetail(installation: ScheduledInstallation): void {
    this.router.navigate(['/installations', installation.id]);
  }
}
