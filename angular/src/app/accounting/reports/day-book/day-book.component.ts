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
import { CompanyCurrencyPipe } from '../../../shared/pipes/company-currency.pipe';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type { DayBookReportDto, DayBookRowDto } from '../../../proxy/accounting/models';
import type { CompanyDto } from '../../../proxy/core/models';

export const DAY_BOOK_VOUCHER_TYPES: readonly string[] = [
  'SalesInvoice',
  'PurchaseInvoice',
  'PaymentEntry',
  'JournalEntry',
  'DeliveryNote',
  'PurchaseReceipt',
  'StockEntry',
];

export function formatDayBookVoucherDate(postingDate: string | Date | undefined | null): string {
  if (!postingDate) return '';
  return String(postingDate).split('T')[0];
}

@Component({
  selector: 'app-day-book',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageModule,
    LocalizationPipe,
    CompanyCurrencyPipe,
  ],
  templateUrl: './day-book.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class DayBookComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private reportingService = inject(ReportingService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  private readonly today = new Date().toISOString().split('T')[0];

  readonly voucherTypes = DAY_BOOK_VOUCHER_TYPES;

  filters = this.fb.group({
    companyId: ['', Validators.required],
    fromDate: [this.today, Validators.required],
    toDate: [this.today, Validators.required],
    voucherType: [''],
    detailed: [false],
    showRemarks: [false],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<DayBookReportDto | null>(null);
  isLoading = signal(false);

  rows = computed(() => this.report()?.rows ?? []);
  summaryCount = computed(() => this.report()?.voucherCount ?? 0);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => {
        this.companies.set(res.items ?? []);
        const defaultId = this.companyContext.currentCompanyId();
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
    this.reportingService.getDayBook({
      companyId: val.companyId!,
      fromDate: val.fromDate!,
      toDate: val.toDate!,
      voucherType: val.voucherType?.trim() || null,
      detailed: val.detailed ?? false,
      showRemarks: val.showRemarks ?? false,
      includeDefaultBookEntries: true,
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

  shouldShowRemarksColumn(): boolean {
    return !!(this.filters.get('showRemarks')?.value);
  }

  formatDisplayDate(row: DayBookRowDto): string {
    if ((row.indent ?? 0) > 0) return '';
    return formatDayBookVoucherDate(row.postingDate);
  }

  drillDown(row: DayBookRowDto): void {
    if ((row.indent ?? 0) > 0 || !row.voucherNo) return;
    const val = this.filters.getRawValue();
    this.router.navigate(['/accounting/reports/general-ledger'], {
      queryParams: {
        companyId: val.companyId,
        fromDate: val.fromDate,
        toDate: val.toDate,
        voucherNumber: row.voucherNo,
      },
    });
  }

  exportCsv(): void {
    const rows = this.rows();
    if (rows.length === 0) return;
    const includeRemarks = this.shouldShowRemarksColumn();
    const headers = ['Date', 'Particulars', 'Voucher Type', 'Voucher No', 'Account', 'Debit', 'Credit'];
    if (includeRemarks) headers.push('Remarks');

    const data = rows.map(r => {
      const record: Record<string, string | number> = {
        Date: this.formatDisplayDate(r),
        Particulars: r.particulars ?? '',
        'Voucher Type': r.voucherType ?? '',
        'Voucher No': r.voucherNo ?? '',
        Account: r.account ?? '',
        Debit: r.debit !== undefined && r.debit !== null ? r.debit.toFixed(2) : '',
        Credit: r.credit !== undefined && r.credit !== null ? r.credit.toFixed(2) : '',
      };
      if (includeRemarks) {
        record['Remarks'] = r.remarks ?? '';
      }
      return record;
    });

    exportToCsv('day-book.csv', data, headers);
  }
}
