import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { BankReconciliationService } from '../../proxy/accounting/bank-reconciliation.service';
import type { BankTransactionDto, BankReconciliationSummaryDto, MatchCandidateDto, MirrorTransactionDto } from '../../proxy/accounting/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';

@Component({
  selector: 'app-bank-reconciliation',
  standalone: true,
  imports: [
    CommonModule, FormsModule, PageModule, LocalizationPipe],
  templateUrl: './bank-reconciliation.component.html',
  styleUrls: ['./bank-reconciliation.component.scss'],
})
export class BankReconciliationComponent implements OnInit {
  private service = inject(BankReconciliationService);
  private toaster = inject(ToasterService);
  companyContext = inject(CompanyContextService);

  transactions = signal<BankTransactionDto[]>([]);
  summary = signal<BankReconciliationSummaryDto>({});
  totalCount = signal(0);
  isLoading = signal(false);
  isMatching = signal(false);

  // Match candidates for manual reconciliation
  matchCandidates = signal<MatchCandidateDto[]>([]);
  selectedTransactionId = signal<string | null>(null);
  showMatchPanel = signal(false);

  // Mirror transaction for internal transfer
  mirrorTransaction = signal<MirrorTransactionDto | null>(null);
  showTransferPanel = signal(false);
  transferTransactionId = signal<string | null>(null);

  bankAccountId = '';

  ngOnInit(): void {
    this.companyContext.load();
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

  reconcile(transactionId: string): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) {
      this.toaster.warn('Please select a company first');
      return;
    }
    this.selectedTransactionId.set(transactionId);
    this.showMatchPanel.set(true);
    this.matchCandidates.set([]);

    this.service.getMatchCandidates(transactionId, companyId).subscribe({
      next: (candidates) => this.matchCandidates.set(candidates),
      error: () => this.toaster.error('Failed to load match candidates'),
    });
  }

  selectCandidate(paymentEntryId: string, paymentNumber: string): void {
    const txId = this.selectedTransactionId();
    if (!txId) return;

    this.service.reconcile({
      transactionId: txId,
      paymentEntryId,
      matchedDocumentRef: paymentNumber,
    }).subscribe({
      next: (updated) => {
        this.transactions.update(txs => txs.map(t => t.id === txId ? updated : t));
        this.showMatchPanel.set(false);
        this.toaster.success('Reconciled');
        this.loadSummary();
      },
      error: () => this.toaster.error('Failed to reconcile'),
    });
  }

  closeMatchPanel(): void {
    this.showMatchPanel.set(false);
    this.selectedTransactionId.set(null);
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
    const companyId = this.companyContext.currentCompanyId();
    this.isMatching.set(true);
    this.service.autoMatch(this.bankAccountId, companyId).subscribe({
      next: (result: any) => {
        this.isMatching.set(false);
        const matched = result.matchedCount ?? 0;
        const partial = result.partiallyReconciledCount ?? 0;
        if (matched > 0 || partial > 0) {
          let msg = `${matched} reconciled`;
          if (partial > 0) msg += `, ${partial} partially reconciled`;
          this.toaster.success(msg);
          this.loadTransactions(0, 20);
          this.loadSummary();
        } else {
          this.toaster.info('No new matches found via reference number');
        }
      },
      error: () => {
        this.isMatching.set(false);
        this.toaster.error('Auto-match failed');
      },
    });
  }

  /** Search for mirror transaction and show internal transfer panel */
  createTransfer(transactionId: string): void {
    this.transferTransactionId.set(transactionId);
    this.mirrorTransaction.set(null);
    this.showTransferPanel.set(true);

    this.service.searchForMirrorTransaction(transactionId).subscribe({
      next: (mirror) => {
        this.mirrorTransaction.set(mirror ?? null);
      },
      error: () => {},
    });
  }

  /** Execute internal transfer creation */
  confirmTransfer(targetBankAccountGlId: string): void {
    const txId = this.transferTransactionId();
    const companyId = this.companyContext.currentCompanyId();
    if (!txId || !companyId) return;

    const mirror = this.mirrorTransaction();
    this.service.createInternalTransfer({
      bankTransactionId: txId,
      targetBankAccountGlId,
      companyId,
      mirrorTransactionId: mirror?.transactionId,
    }).subscribe({
      next: (result) => {
        const msg = mirror
          ? `Internal transfer created (PE: ${result.paymentNumber}). Both sides reconciled.`
          : `Internal transfer created (PE: ${result.paymentNumber}).`;
        this.toaster.success(msg);
        this.showTransferPanel.set(false);
        this.loadTransactions(0, 20);
        this.loadSummary();
      },
      error: () => this.toaster.error('Failed to create internal transfer'),
    });
  }

  closeTransferPanel(): void {
    this.showTransferPanel.set(false);
    this.transferTransactionId.set(null);
  }

  // --- Create Payment Entry from Transaction ---
  private http = inject(HttpClient);
  private router = inject(Router);
  private customerService = inject(CustomerService);
  private supplierService = inject(SupplierService);

  showCreatePePanel = signal(false);
  createPeTransaction = signal<BankTransactionDto | null>(null);
  pePartyType = signal<'Customer' | 'Supplier'>('Customer');
  pePartyId = signal<string>('');
  peBankAccountId = signal<string>('');
  pePartyAccountId = signal<string>('');
  peAgainstInvoiceId = signal<string>('');
  isCreatingPe = signal(false);

  customers = signal<{ id: string; name: string }[]>([]);
  suppliers = signal<{ id: string; name: string }[]>([]);

  /** Open the Create PE panel for an unreconciled transaction */
  openCreatePayment(tx: BankTransactionDto): void {
    this.createPeTransaction.set(tx);
    this.showCreatePePanel.set(true);
    // Auto-determine party type from transaction direction
    this.pePartyType.set(tx.amount > 0 ? 'Customer' : 'Supplier');
    this.peBankAccountId.set(tx.bankAccountId?.toString() ?? this.bankAccountId);
    this.pePartyId.set('');
    this.pePartyAccountId.set('');
    this.peAgainstInvoiceId.set('');

    // Load party lists
    this.customerService.getList({ skipCount: 0, maxResultCount: 200 }).subscribe({
      next: (r) => this.customers.set((r.items ?? []).map((c: any) => ({ id: c.id, name: c.customerName || c.name || c.id }))),
    });
    this.supplierService.getList({ skipCount: 0, maxResultCount: 200 }).subscribe({
      next: (r) => this.suppliers.set((r.items ?? []).map((s: any) => ({ id: s.id, name: s.supplierName || s.name || s.id }))),
    });
  }

  /** Execute the Create PE from Transaction API call */
  confirmCreatePayment(): void {
    const tx = this.createPeTransaction();
    const companyId = this.companyContext.currentCompanyId();
    if (!tx || !companyId || !this.pePartyId() || !this.peBankAccountId() || !this.pePartyAccountId()) {
      this.toaster.warn('Please fill all required fields');
      return;
    }

    this.isCreatingPe.set(true);
    this.http.post<any>('/api/app/bank-reconciliation/create-payment-entry-from-transaction', {
      bankTransactionId: tx.id,
      companyId,
      partyType: this.pePartyType(),
      partyId: this.pePartyId(),
      bankAccountId: this.peBankAccountId(),
      partyAccountId: this.pePartyAccountId(),
      againstInvoiceId: this.peAgainstInvoiceId() || undefined,
    }).subscribe({
      next: (result) => {
        this.isCreatingPe.set(false);
        this.showCreatePePanel.set(false);
        this.toaster.success(
          `Payment ${result.paymentNumber} created (${result.paymentType}, ${result.amount.toFixed(2)}). Auto-reconciled.`
        );
        this.loadTransactions(0, 20);
        this.loadSummary();
      },
      error: (err) => {
        this.isCreatingPe.set(false);
        const msg = err?.error?.error?.message || 'Failed to create payment entry';
        this.toaster.error(msg);
      },
    });
  }

  closeCreatePePanel(): void {
    this.showCreatePePanel.set(false);
    this.createPeTransaction.set(null);
  }
}
