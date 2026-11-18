import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { AccountService } from '../../../proxy/accounting/account.service';
import { InvalidLedgerEntriesService } from '../../../proxy/accounting/invalid-ledger-entries.service';
import type { CompanyDto } from '../../../proxy/core/models';
import type {
  AccountDto,
  InvalidLedgerEntriesReportDto,
  InvalidLedgerEntryRowDto,
} from '../../../proxy/accounting/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildInvalidLedgerEntriesCsvRows(
  rows: InvalidLedgerEntryRowDto[] | undefined,
): Record<string, string>[] {
  return (rows ?? []).map(r => ({
    'Voucher Type': r.voucherType ?? '',
    'Voucher No': r.voucherNo ?? '',
    'Ledger Source': r.ledgerSource ?? '',
    Reason: r.reason ?? '',
  }));
}

@Component({
  selector: 'app-invalid-ledger-entries',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './invalid-ledger-entries.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class InvalidLedgerEntriesComponent implements OnInit {
  private fb = inject(FormBuilder);
  private invalidLedgerService = inject(InvalidLedgerEntriesService);
  private accountService = inject(AccountService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  private today = new Date();
  private monthAgo = new Date(this.today.getFullYear(), this.today.getMonth() - 1, this.today.getDate());

  filters = this.fb.group({
    companyId: ['', Validators.required],
    fromDate: [this.monthAgo.toISOString().split('T')[0], Validators.required],
    toDate: [this.today.toISOString().split('T')[0], Validators.required],
    accountId: [''],
    voucherNo: [''],
  });

  companies = signal<CompanyDto[]>([]);
  accounts = signal<AccountDto[]>([]);
  report = signal<InvalidLedgerEntriesReportDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe(res => {
      this.companies.set(res.items ?? []);
      const defaultId = this.companyContext.currentCompanyId();
      if (defaultId && !this.filters.get('companyId')?.value) {
        this.filters.patchValue({ companyId: defaultId });
      }
      this.loadAccounts();
      if (this.filters.get('companyId')?.value) {
        this.generate();
      }
    });
  }

  onCompanyChange(): void {
    this.filters.patchValue({ accountId: '' });
    this.loadAccounts();
  }

  loadAccounts(): void {
    const companyId = this.filters.get('companyId')?.value;
    if (!companyId) {
      this.accounts.set([]);
      return;
    }
    this.accountService
      .getList({ companyId, isGroup: false, skipCount: 0, maxResultCount: 500, sorting: 'accountCode' })
      .subscribe(res => {
        this.accounts.set(res.items ?? []);
      });
  }

  generate(): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }
    this.isLoading.set(true);
    const { companyId, fromDate, toDate, accountId, voucherNo } = this.filters.getRawValue();
    this.invalidLedgerService
      .getReport({
        companyId: companyId!,
        fromDate: fromDate!,
        toDate: toDate!,
        accountIds: accountId ? [accountId] : null,
        voucherNo: voucherNo?.trim() || null,
      })
      .subscribe({
        next: res => {
          this.report.set(res);
          this.isLoading.set(false);
        },
        error: err => {
          this.toaster.error(err?.error?.error?.message || 'Error generating report');
          this.isLoading.set(false);
        },
      });
  }

  exportCsv(): void {
    const r = this.report();
    if (!r?.rows?.length) return;
    exportToCsv('invalid-ledger-entries.csv', buildInvalidLedgerEntriesCsvRows(r.rows), [
      'Voucher Type',
      'Voucher No',
      'Ledger Source',
      'Reason',
    ]);
  }
}
