import { CompanyCurrencyPipe } from '../../shared/pipes/company-currency.pipe';
import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { Router } from '@angular/router';
import { BankReconciliationService } from '../../proxy/accounting/bank-reconciliation.service';
import type { BankTransactionDto, BankReconciliationSummaryDto, MatchCandidateDto, MirrorTransactionDto } from '../../proxy/accounting/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { AccountService } from '../../proxy/accounting/account.service';

@Component({
  selector: 'app-bank-reconciliation',
  standalone: true,
  imports: [CompanyCurrencyPipe, 
    CommonModule, FormsModule, PageModule, LocalizationPipe],
  templateUrl: './bank-reconciliation.component.html',
  styleUrls: ['./bank-reconciliation.component.scss'],
})
export class BankReconciliationComponent implements OnInit {
  private service = inject(BankReconciliationService);
  private toaster = inject(ToasterService);
  companyContext = inject(CompanyContextService);
  private accountService = inject(AccountService);

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

  // Bank account selector
  bankAccounts = signal<any[]>([]);
  partyAccounts = signal<any[]>([]);
  bankAccountId = '';

  // Date range filter
  fromDate = '';
  toDate = '';

  ngOnInit(): void {
    this.companyContext.load();
    this.loadBankAccounts();
    this.loadPartyAccounts();
  }

  loadBankAccounts(): void {
    const companyId = this.companyContext.currentCompanyId();
    const params: any = { skipCount: 0, maxResultCount: 200, sorting: '' };
    this.accountService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any).subscribe({
      next: (res) => {
        const bankAccts = (res.items ?? []).filter((a: any) =>
          a.accountType === 'Bank' || a.accountSubType === 5 /* Bank */
        );
        this.bankAccounts.set(bankAccts);
        // Auto-select if only one bank account
        if (bankAccts.length === 1 && !this.bankAccountId) {
          this.bankAccountId = bankAccts[0].id;
          this.onBankAccountChanged();
        }
      },
      error: () => {},
    });
  }

  loadPartyAccounts(): void {
    this.accountService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any).subscribe({
      next: (res) => {
        const partyAccts = (res.items ?? []).filter((a: any) =>
          a.accountSubType === 1 /* Receivable */ || a.accountSubType === 2 /* Payable */
        );
        this.partyAccounts.set(partyAccts);
      },
      error: () => {},
    });
  }

  onBankAccountChanged(): void {
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
        this.toaster.error('::FailedToLoadTransactions');
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
      this.toaster.warn('::PleaseSelectCompanyFirst');
      return;
    }
    this.selectedTransactionId.set(transactionId);
    this.showMatchPanel.set(true);
    this.matchCandidates.set([]);

    this.service.getMatchCandidates(transactionId, companyId).subscribe({
      next: (candidates) => this.matchCandidates.set(candidates),
      error: () => this.toaster.error('::FailedToLoadMatchCandidates'),
    });
  }

  selectCandidate(candidate: MatchCandidateDto): void {
    const txId = this.selectedTransactionId();
    if (!txId) return;

    this.service.reconcile({
      transactionId: txId,
      paymentEntryId: candidate.voucherType === 'JournalEntry' ? null : candidate.paymentEntryId,
      journalEntryId: candidate.voucherType === 'JournalEntry' ? candidate.journalEntryId : null,
      matchedDocumentRef: candidate.paymentNumber ?? undefined,
    }).subscribe({
      next: (updated) => {
        this.transactions.update(txs => txs.map(t => t.id === txId ? updated : t));
        this.showMatchPanel.set(false);
        this.toaster.success('::SuccessfullyReconciled');
        this.loadSummary();
      },
      error: () => this.toaster.error('::ReconcileFailed'),
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
        this.toaster.success('::SuccessfullyUnreconciled');
        this.loadSummary();
      },
      error: () => this.toaster.error('::UnreconcileFailed'),
    });
  }

  onPageChange(event: any): void {
    this.loadTransactions(event.pageIndex * event.pageSize, event.pageSize);
  }

  autoMatch(): void {
    if (!this.bankAccountId) {
      this.toaster.warn('::SelectBankAccountToViewTransactions');
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
          this.toaster.info('::NoNewMatchesFound');
        }
      },
      error: () => {
        this.isMatching.set(false);
        this.toaster.error('::AutoMatchFailed');
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
        this.toaster.success('::SuccessfullyCreated');
        this.showTransferPanel.set(false);
        this.loadTransactions(0, 20);
        this.loadSummary();
      },
      error: () => this.toaster.error('::FailedToCreateTransfer'),
    });
  }

  closeTransferPanel(): void {
    this.showTransferPanel.set(false);
    this.transferTransactionId.set(null);
  }

  // --- Create Payment Entry from Transaction ---
  private bankReconciliationService = inject(BankReconciliationService);
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
      this.toaster.warn('::PleaseFillAllRequiredFields');
      return;
    }

    this.isCreatingPe.set(true);
    this.bankReconciliationService.createPaymentEntryFromTransaction({
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
        const msg = err?.error?.error?.message || '::FailedToCreatePaymentEntry';
        this.toaster.error(msg);
      },
    });
  }

  closeCreatePePanel(): void {
    this.showCreatePePanel.set(false);
    this.createPeTransaction.set(null);
  }

  // --- Manually import a single bank transaction ---
  showAddTransactionPanel = signal(false);
  isAddingTransaction = signal(false);
  newTransactionDate = new Date().toISOString().substring(0, 10);
  newTransactionDescription = '';
  newTransactionAmount = 0;
  newTransactionReference = '';

  openAddTransaction(): void {
    this.newTransactionDate = new Date().toISOString().substring(0, 10);
    this.newTransactionDescription = '';
    this.newTransactionAmount = 0;
    this.newTransactionReference = '';
    this.showAddTransactionPanel.set(true);
  }

  confirmAddTransaction(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId || !this.bankAccountId || !this.newTransactionDescription.trim() || !this.newTransactionAmount) {
      this.toaster.warn('::PleaseFillAllRequiredFields');
      return;
    }

    this.isAddingTransaction.set(true);
    this.service.importTransaction({
      companyId,
      bankAccountId: this.bankAccountId,
      transactionDate: this.newTransactionDate,
      description: this.newTransactionDescription.trim(),
      amount: this.newTransactionAmount,
      referenceNumber: this.newTransactionReference.trim() || undefined,
    }).subscribe({
      next: () => {
        this.isAddingTransaction.set(false);
        this.showAddTransactionPanel.set(false);
        this.toaster.success('::SuccessfullyCreated');
        this.loadTransactions(0, 20);
        this.loadSummary();
      },
      error: () => {
        this.isAddingTransaction.set(false);
        this.toaster.error('::FailedToCreateTransfer');
      },
    });
  }

  closeAddTransactionPanel(): void {
    this.showAddTransactionPanel.set(false);
  }
}
