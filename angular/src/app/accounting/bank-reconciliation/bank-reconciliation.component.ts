import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { ToasterService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { BankReconciliationService, type BankTransactionDto, type BankReconciliationSummaryDto } from '../../proxy/accounting/bank-reconciliation.service';

@Component({
  selector: 'app-bank-reconciliation',
  standalone: true,
  imports: [
    CommonModule, PageModule, LocalizationModule, MatCardModule, MatTableModule,
    MatButtonModule, MatIconModule, MatChipsModule, MatPaginatorModule, LoadingOverlayComponent,
  ],
  templateUrl: './bank-reconciliation.component.html',
  styleUrls: ['./bank-reconciliation.component.scss'],
})
export class BankReconciliationComponent implements OnInit {
  private service = inject(BankReconciliationService);
  private toaster = inject(ToasterService);

  transactions = signal<BankTransactionDto[]>([]);
  summary = signal<BankReconciliationSummaryDto>({});
  totalCount = signal(0);
  isLoading = signal(false);

  displayedColumns = ['date', 'description', 'amount', 'matchedDoc', 'status', 'actions'];

  // TODO: In production, this would come from a bank account selector
  bankAccountId = '';

  ngOnInit(): void {
    if (this.bankAccountId) {
      this.loadTransactions(0, 20);
      this.loadSummary();
    }
  }

  loadTransactions(skipCount: number, maxResultCount: number): void {
    if (!this.bankAccountId) return;
    this.isLoading.set(true);
    this.service.getTransactions({
      bankAccountId: this.bankAccountId,
      skipCount,
      maxResultCount,
      sorting: 'transactionDate DESC',
    }).subscribe({
      next: (result) => {
        this.transactions.set(result.items ?? []);
        this.totalCount.set(result.totalCount ?? 0);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.toaster.error('Failed to load transactions');
      },
    });
  }

  loadSummary(): void {
    this.service.getSummary(this.bankAccountId).subscribe({
      next: (s) => this.summary.set(s),
    });
  }

  reconcile(id: string): void {
    // In a full implementation, this would open a dialog to select the matching payment entry
    this.toaster.info('Select a payment entry to match (dialog not yet implemented)');
  }

  unreconcile(id: string): void {
    this.service.unreconcile(id).subscribe({
      next: (updated) => {
        this.transactions.update(txs => txs.map(t => t.id === id ? updated : t));
        this.toaster.success('Unreconciled');
        this.loadSummary();
      },
      error: () => this.toaster.error('Failed to unreconcile'),
    });
  }

  onPageChange(event: PageEvent): void {
    this.loadTransactions(event.pageIndex * event.pageSize, event.pageSize);
  }
}
