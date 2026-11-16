import { Component, ChangeDetectionStrategy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import { AccountService } from '../../../proxy/accounting/account.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { CompanyCurrencyPipe } from '../../../shared/pipes/company-currency.pipe';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type {
  AccountDto,
  GeneralPaymentLedgerComparisonReportDto,
  GeneralPaymentLedgerComparisonRowDto,
} from '../../../proxy/accounting/models';
import type { CompanyDto } from '../../../proxy/core/models';

export function buildGlPlComparisonCsvRows(
  rows: GeneralPaymentLedgerComparisonRowDto[] | undefined,
): Record<string, string | number>[] {
  return (rows ?? []).map(r => ({
    Account: `${r.accountCode ?? ''} ${r.accountName ?? ''}`.trim(),
    'Voucher Type': r.voucherType ?? '',
    'Voucher No': r.voucherNo ?? '',
    'Party Type': r.partyType ?? '',
    Party: r.partyName ?? '',
    'Against Voucher': r.againstVoucherNo ?? '',
    'GL Balance': (r.glBalance ?? 0).toFixed(2),
    'PL Balance': (r.plBalance ?? 0).toFixed(2),
    Difference: (r.difference ?? 0).toFixed(2),
  }));
}

@Component({
  selector: 'app-general-payment-ledger-comparison',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageModule,
    LocalizationPipe,
    CompanyCurrencyPipe,
  ],
  templateUrl: './general-payment-ledger-comparison.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GeneralPaymentLedgerComparisonComponent implements OnInit {
  private fb = inject(FormBuilder);
  private reportingService = inject(ReportingService);
  private companyService = inject(CompanyService);
  private accountService = inject(AccountService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  private readonly firstOfYear = new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0];
  private readonly today = new Date().toISOString().split('T')[0];

  filters = this.fb.group({
    companyId: ['', Validators.required],
    periodStartDate: [this.firstOfYear],
    periodEndDate: [this.today],
    accountId: [''],
    partyType: [''],
    voucherType: [''],
    voucherNo: [''],
  });

  companies = signal<CompanyDto[]>([]);
  accounts = signal<AccountDto[]>([]);
  report = signal<GeneralPaymentLedgerComparisonReportDto | null>(null);
  isLoading = signal(false);

  rows = computed(() => this.report()?.rows ?? []);
  discrepancyCount = computed(() => this.rows().length);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => {
        this.companies.set(res.items ?? []);
        const defaultId = this.companyContext.currentCompanyId();
        if (defaultId && !this.filters.get('companyId')?.value) {
          this.filters.patchValue({ companyId: defaultId });
        }
        this.loadAccounts();
        if (this.filters.get('companyId')?.value) {
          this.generate();
        }
      },
      error: () => {},
    });
  }

  onCompanyChange(): void {
    this.filters.patchValue({ accountId: '' });
    this.loadAccounts();
  }

  private loadAccounts(): void {
    const companyId = this.filters.get('companyId')?.value;
    if (!companyId) {
      this.accounts.set([]);
      return;
    }
    this.accountService.getList({
      companyId,
      isGroup: false,
      skipCount: 0,
      maxResultCount: 300,
      sorting: '',
    }).subscribe({
      next: res => this.accounts.set(res.items ?? []),
      error: () => this.accounts.set([]),
    });
  }

  generate(): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }
    this.isLoading.set(true);
    const val = this.filters.getRawValue();
    this.reportingService.getGeneralAndPaymentLedgerComparison({
      companyId: val.companyId!,
      periodStartDate: val.periodStartDate || null,
      periodEndDate: val.periodEndDate || null,
      accountId: val.accountId || null,
      partyType: val.partyType || null,
      voucherType: val.voucherType || null,
      voucherNo: val.voucherNo?.trim() || null,
    }).subscribe({
      next: res => {
        this.report.set(res);
        this.isLoading.set(false);
      },
      error: err => {
        this.isLoading.set(false);
        this.toaster.error(err?.error?.error?.message ?? '::FailedToGenerateReport');
      },
    });
  }

  exportCsv(): void {
    const csvRows = buildGlPlComparisonCsvRows(this.rows());
    if (csvRows.length === 0) return;
    const rep = this.report();
    csvRows.push({
      Account: 'TOTAL',
      'Voucher Type': '',
      'Voucher No': '',
      'Party Type': '',
      Party: '',
      'Against Voucher': '',
      'GL Balance': (rep?.totalGLBalance ?? 0).toFixed(2),
      'PL Balance': (rep?.totalPLBalance ?? 0).toFixed(2),
      Difference: (rep?.totalDifference ?? 0).toFixed(2),
    });
    exportToCsv('general-payment-ledger-comparison.csv', csvRows, [
      'Account',
      'Voucher Type',
      'Voucher No',
      'Party Type',
      'Party',
      'Against Voucher',
      'GL Balance',
      'PL Balance',
      'Difference',
    ]);
  }
}
