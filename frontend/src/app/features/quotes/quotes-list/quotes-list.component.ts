import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { Router } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { TranslocoModule } from '@jsverse/transloco';
import { QuotesService } from '../../../core/services/quotes.service';
import { QuoteListItem } from '../../../core/models/quote.model';

type QuoteStatusKey = 'confirmed' | 'recalled' | 'surveyed' | 'draft';

@Component({
  selector: 'hvac-quotes-list',
  imports: [
    DatePipe,
    DecimalPipe,
    MatTableModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    TranslocoModule,
  ],
  templateUrl: './quotes-list.component.html',
  styleUrl: './quotes-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QuotesListComponent {
  private quotesService = inject(QuotesService);
  private router = inject(Router);

  protected readonly displayedColumns = ['number', 'client', 'department', 'status', 'finalPrice', 'createdAt'];
  protected loading = signal(true);
  protected quotes = signal<QuoteListItem[]>([]);

  constructor() {
    this.quotesService.getList().subscribe((quotes) => {
      this.quotes.set(quotes);
      this.loading.set(false);
    });
  }

  status(quote: QuoteListItem): QuoteStatusKey {
    switch (quote.currentStatus) {
      case 'Confirmed':
        return 'confirmed';
      case 'Recalled':
        return 'recalled';
      case 'SurveyDone':
        return 'surveyed';
      default:
        return 'draft';
    }
  }

  quoteLabel(quote: QuoteListItem): string {
    return quote.revision > 0 ? `${quote.number}/${quote.version}.${quote.revision}` : `${quote.number}/${quote.version}`;
  }

  openNew(): void {
    this.router.navigate(['/quotes', 'new']);
  }

  openDetail(quote: QuoteListItem): void {
    this.router.navigate(['/quotes', quote.id]);
  }
}
