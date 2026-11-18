import { Component, ChangeDetectionStrategy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AccountService } from '../../../proxy/accounting/account.service';
import { AccountSubType } from '../../../proxy/accounting/account-sub-type.enum';
import { BankReconciliationService } from '../../../proxy/accounting/bank-reconciliation.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type { AccountDto, BankStatementEntryDto } from '../../../proxy/accounting/models';
import type { CompanyDto } from '../../../proxy/core/models';

export function formatIsoDate(value: string | Date | undefined | null): string {
  if (!value) return '';
  return String(value).split('T')[0];
}

export function isBankOrCashLeafAccount(account: AccountDto): boolean {
  return !account.isGroup
    && account.isActive !== false
    && (account.accountSubType === AccountSubType.BankAccount
      || account.accountSubType === AccountSubType.CashAccount);
}

@Component({
  selector: 'app-cheques-and-deposits-incorrectly-cleared',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageModule,
    LocalizationPipe,
  ],
  templateUrl: './cheques-and-deposits-incorrectly-cleared.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class ChequesAndDepositsIncorrectlyClearedComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private bankReconciliationService = inject(BankReconciliationService);
  private accountService = inject(AccountService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  private readonly today = new Date().toISOString().split('T')[0];

  filters = this.fb.group({
    companyId: ['', Validators.required],
    bankAccountId: ['', Validators.required],
    reportDate: [this.today, Validators.required],
  });

  companies = signal<CompanyDto[]>([]);
  bankAccounts = signal<AccountDto[]>([]);
  entries = signal<BankStatementEntryDto[]>([]);
  hasLoaded = signal(false);
  isLoading = signal(false);

  entryCount = computed(() => this.entries().length);
  totalDebit = computed(() => this.entries().reduce((sum, e) => sum + (e.debit ?? 0), 0));
  totalCredit = computed(() => this.entries().reduce((sum, e) => sum + (e.credit ?? 0), 0));
  netDifference = computed(() => this.totalDebit() - this.totalCredit());
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
    this.bankReconciliationService.getIncorrectlyClearedEntries({
      companyId: val.companyId!,
      bankAccountId: val.bankAccountId!,
      reportDate: val.reportDate!,
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

  drillDown(entry: BankStatementEntryDto): void {
    if (!entry.documentId) return;
    if (entry.documentType === 'Payment Entry') {
      this.router.navigate(['/accounting/payments', entry.documentId]);
    } else if (entry.documentType === 'Journal Entry') {
      this.router.navigate(['/accounting/journal-entries', entry.documentId]);
    }
  }

  exportCsv(): void {
    const rows = this.entries();
    if (rows.length === 0) return;

    const reportDate = this.filters.get('reportDate')?.value || this.today;
    const data = rows.map(row => ({
      'Posting Date': this.formatDate(row.postingDate),
      'Clearance Date': this.formatDate(row.clearanceDate),
      'Voucher Type': row.documentType ?? '',
      'Document Number': row.documentNumber ?? '',
      'Reference Number': row.referenceNumber ?? '',
      Debit: (row.debit ?? 0).toFixed(2),
      Credit: (row.credit ?? 0).toFixed(2),
      Currency: row.accountCurrency ?? this.displayCurrency(),
    }));
    exportToCsv(`cheques-and-deposits-incorrectly-cleared-${reportDate}.csv`, data, [
      'Posting Date',
      'Clearance Date',
      'Voucher Type',
      'Document Number',
      'Reference Number',
      'Debit',
      'Credit',
      'Currency',
    ]);
  }
}
