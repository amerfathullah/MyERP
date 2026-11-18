import { Component, ChangeDetectionStrategy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import { DAY_BOOK_VOUCHER_TYPES, formatDayBookVoucherDate } from '../day-book/day-book.component';
import type { VoucherWiseBalanceReportDto, VoucherWiseBalanceRowDto } from '../../../proxy/accounting/models';
import type { CompanyDto } from '../../../proxy/core/models';

export function resolveVoucherRowDifference(row: VoucherWiseBalanceRowDto): number {
  if (typeof row.difference === 'number') {
    return row.difference;
  }
  return (row.debit ?? 0) - (row.credit ?? 0);
}

@Component({
  selector: 'app-voucher-wise-balance',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageModule,
    LocalizationPipe,
  ],
  templateUrl: './voucher-wise-balance.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class VoucherWiseBalanceComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private reportingService = inject(ReportingService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  private readonly today = new Date().toISOString().split('T')[0];
  private readonly defaultFromDate = (() => {
    const d = new Date();
    d.setMonth(d.getMonth() - 1);
    return d.toISOString().split('T')[0];
  })();

  readonly voucherTypes = DAY_BOOK_VOUCHER_TYPES;

  filters = this.fb.group({
    companyId: ['', Validators.required],
    fromDate: [this.defaultFromDate],
    toDate: [this.today],
    voucherType: [''],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<VoucherWiseBalanceReportDto | null>(null);
  isLoading = signal(false);

  rows = computed(() => this.report()?.rows ?? []);
  totalDifference = computed(() =>
    (this.report()?.totalDebit ?? 0) - (this.report()?.totalCredit ?? 0),
  );

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => {
        this.companies.set(res.items ?? []);
        const defaultId = this.companyContext.currentCompanyId() || res.items?.[0]?.id || '';
        if (defaultId && !this.filters.get('companyId')?.value) {
          this.filters.patchValue({ companyId: defaultId });
        }
        if (this.filters.get('companyId')?.value) {
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
    this.reportingService.getVoucherWiseBalance({
      companyId: val.companyId!,
      fromDate: val.fromDate || null,
      toDate: val.toDate || null,
      voucherType: val.voucherType?.trim() || null,
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

  formatDate(value: string | Date | undefined | null): string {
    return formatDayBookVoucherDate(value);
  }

  rowDifference(row: VoucherWiseBalanceRowDto): number {
    return resolveVoucherRowDifference(row);
  }

  drillDown(row: VoucherWiseBalanceRowDto): void {
    if (!row.voucherNo) return;
    const val = this.filters.getRawValue();
    this.router.navigate(['/accounting/reports/general-ledger'], {
      queryParams: {
        companyId: val.companyId,
        fromDate: val.fromDate || this.formatDate(row.postingDate),
        toDate: val.toDate || this.formatDate(row.postingDate),
        voucherNumber: row.voucherNo,
      },
    });
  }

  exportCsv(): void {
    const rows = this.rows();
    if (rows.length === 0) return;

    const currency = this.report()?.currencyCode ?? 'MYR';
    const val = this.filters.getRawValue();
    const data = rows.map(row => ({
      'Posting Date': this.formatDate(row.postingDate),
      'Voucher Type': row.voucherType ?? '',
      'Voucher No': row.voucherNo ?? '',
      Debit: (row.debit ?? 0).toFixed(2),
      Credit: (row.credit ?? 0).toFixed(2),
      Difference: this.rowDifference(row).toFixed(2),
      Currency: currency,
    }));
    exportToCsv(`voucher-wise-balance-${val.companyId}.csv`, data, [
      'Posting Date',
      'Voucher Type',
      'Voucher No',
      'Debit',
      'Credit',
      'Difference',
      'Currency',
    ]);
  }
}
