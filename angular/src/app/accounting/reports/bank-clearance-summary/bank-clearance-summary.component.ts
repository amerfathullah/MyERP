import { Component, ChangeDetectionStrategy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AccountService } from '../../../proxy/accounting/account.service';
import { BankClearanceService } from '../../../proxy/accounting/bank-clearance.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import {
  formatIsoDate,
  isBankOrCashLeafAccount,
} from '../cheques-and-deposits-incorrectly-cleared/cheques-and-deposits-incorrectly-cleared.component';
import type { AccountDto, BankClearanceEntryDto } from '../../../proxy/accounting/models';
import type { CompanyDto } from '../../../proxy/core/models';

export function resolveBankClearanceSignedAmount(entry: BankClearanceEntryDto): number {
  if (typeof entry.amount === 'number') {
    return entry.amount;
  }
  return (entry.debit ?? 0) - (entry.credit ?? 0);
}

@Component({
  selector: 'app-bank-clearance-summary',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageModule,
    LocalizationPipe,
  ],
  templateUrl: './bank-clearance-summary.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class BankClearanceSummaryComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private bankClearanceService = inject(BankClearanceService);
  private accountService = inject(AccountService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  private readonly today = new Date().toISOString().split('T')[0];
  private readonly defaultFromDate = (() => {
    const d = new Date();
    d.setDate(1);
    return d.toISOString().split('T')[0];
  })();

  filters = this.fb.group({
    companyId: ['', Validators.required],
    bankAccountId: ['', Validators.required],
    fromDate: [this.defaultFromDate, Validators.required],
    toDate: [this.today, Validators.required],
  });

  companies = signal<CompanyDto[]>([]);
  bankAccounts = signal<AccountDto[]>([]);
  entries = signal<BankClearanceEntryDto[]>([]);
  hasLoaded = signal(false);
  isLoading = signal(false);

  clearedCount = computed(() => this.entries().filter(e => !!e.clearanceDate).length);
  unclearedCount = computed(() => this.entries().filter(e => !e.clearanceDate).length);
  netAmount = computed(() =>
    this.entries().reduce((sum, e) => sum + resolveBankClearanceSignedAmount(e), 0),
  );
  displayCurrency = computed(() => {
    const firstCurrency = this.entries()[0]?.accountCurrency;
    if (firstCurrency) return firstCurrency;
    const selectedId = this.filters.get('bankAccountId')?.value;
    const matchedAccount = this.bankAccounts().find(a => a.id === selectedId);
    return matchedAccount?.currency ?? 'MYR';
  });

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => {
        const list = res.items ?? [];
        this.companies.set(list);
        const defaultId = this.companyContext.currentCompanyId() || list[0]?.id || '';
        if (defaultId && !this.filters.get('companyId')?.value) {
          this.filters.patchValue({ companyId: defaultId });
        }
        const companyId = this.filters.get('companyId')?.value;
        if (companyId) {
          this.loadBankAccounts(companyId);
        }
      },
      error: () => {},
    });
  }

  onCompanyChange(): void {
    const companyId = this.filters.get('companyId')?.value;
    this.filters.patchValue({ bankAccountId: '' });
    this.bankAccounts.set([]);
    this.entries.set([]);
    this.hasLoaded.set(false);
    if (companyId) {
      this.loadBankAccounts(companyId);
    }
  }

  private loadBankAccounts(companyId: string): void {
    this.accountService.getList({
      companyId,
      isGroup: false,
      skipCount: 0,
      maxResultCount: 500,
      sorting: 'accountCode',
    }).subscribe({
      next: res => {
        const filtered = (res.items ?? []).filter(isBankOrCashLeafAccount);
        this.bankAccounts.set(filtered);
        if (filtered.length > 0 && !this.filters.get('bankAccountId')?.value) {
          this.filters.patchValue({ bankAccountId: filtered[0].id ?? '' });
        }
        if (this.filters.valid) {
          this.generate();
        }
      },
      error: () => {},
    });
  }

  generate(): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }
    this.isLoading.set(true);
    const val = this.filters.getRawValue();
    this.bankClearanceService.getEntries({
      companyId: val.companyId!,
      bankAccountId: val.bankAccountId!,
      fromDate: val.fromDate!,
      toDate: val.toDate!,
      includeCleared: true,
    }).subscribe({
      next: res => {
        this.entries.set(res ?? []);
        this.hasLoaded.set(true);
        this.isLoading.set(false);
      },
      error: err => {
        this.isLoading.set(false);
        this.toaster.error(err?.error?.error?.message ?? '::FailedToGenerateReport');
      },
    });
  }

  formatDate(value: string | Date | undefined | null): string {
    return formatIsoDate(value);
  }

  signedAmount(entry: BankClearanceEntryDto): number {
    return resolveBankClearanceSignedAmount(entry);
  }

  drillDown(entry: BankClearanceEntryDto): void {
    const targetId = entry.sourceDocumentId ?? entry.documentId;
    if (!targetId) return;
    switch (entry.documentType) {
      case 'PaymentEntry':
        this.router.navigate(['/accounting/payments', targetId]);
        break;
      case 'SalesInvoice':
        this.router.navigate(['/sales/invoices', targetId]);
        break;
      case 'PurchaseInvoice':
        this.router.navigate(['/purchasing/invoices', targetId]);
        break;
      case 'JournalEntry':
        this.router.navigate(['/accounting/journal-entries', targetId]);
        break;
      default:
        break;
    }
  }

  exportCsv(): void {
    const rows = this.entries();
    if (rows.length === 0) return;

    const val = this.filters.getRawValue();
    const data = rows.map(row => ({
      'Payment Document Type': row.documentType ?? '',
      'Payment Entry': row.documentNumber ?? '',
      'Posting Date': this.formatDate(row.postingDate),
      'Cheque/Reference No': row.referenceNumber ?? '',
      'Clearance Date': this.formatDate(row.clearanceDate),
      'Against Account': row.againstAccount ?? '',
      Amount: this.signedAmount(row).toFixed(2),
      Currency: row.accountCurrency ?? this.displayCurrency(),
    }));
    exportToCsv(`bank-clearance-summary-${val.fromDate}-to-${val.toDate}.csv`, data, [
      'Payment Document Type',
      'Payment Entry',
      'Posting Date',
      'Cheque/Reference No',
      'Clearance Date',
      'Against Account',
      'Amount',
      'Currency',
    ]);
  }
}
