import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { BankReconciliationService } from '../../proxy/accounting/bank-reconciliation.service';
import type { BankTransactionDto, BankReconciliationSummaryDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-bank-reconciliation',
  standalone: true,
  imports: [
    CommonModule, PageModule, LocalizationPipe],
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
  isMatching = signal(false);
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

  onPageChange(event: any): void {
    this.loadTransactions(event.pageIndex * event.pageSize, event.pageSize);
  }

  autoMatch(): void {
    if (!this.bankAccountId) {
      this.toaster.warn('Please select a bank account first');
      return;
    }
    this.isMatching.set(true);
    this.service.autoMatch(this.bankAccountId, '').subscribe({
      next: (result: any) => {
        this.isMatching.set(false);
        if (result.matchedCount > 0) {
          this.toaster.success(`Auto-matched ${result.matchedCount} transaction(s)`);
          this.loadTransactions(0, 20);
          this.loadSummary();
        } else {
          this.toaster.info('No new matches found');
        }
      },
      error: () => {
        this.isMatching.set(false);
        this.toaster.error('Auto-match failed');
      },
    });
  }
}
