import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { AccountService } from '../../../proxy/accounting/account.service';
import { AccountSubType } from '../../../proxy/accounting/account-sub-type.enum';
import { StockDiagnosticsService } from '../../../proxy/inventory/stock-diagnostics.service';
import type { CompanyDto } from '../../../proxy/core/models';
import type { AccountDto } from '../../../proxy/accounting/models';
import type {
  IncorrectStockValueReportDto,
  IncorrectStockValueRowDto,
} from '../../../proxy/inventory/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildIncorrectStockValueCsvRows(
  rows: IncorrectStockValueRowDto[] | undefined,
): Record<string, string | number>[] {
  return (rows ?? []).map(r => ({
    'Posting Date': r.postingDate ? r.postingDate.split('T')[0] : '',
    'Voucher Type': r.voucherType ?? '',
    'Voucher No': r.voucherNo ?? '',
    'Item Code': r.itemCode ?? '',
    Warehouse: r.warehouseName ?? '',
    'Expected Stock Value': (r.expectedStockValue ?? 0).toFixed(2),
    'Stock Value': (r.stockValue ?? 0).toFixed(2),
    'Difference Value': (r.differenceValue ?? 0).toFixed(2),
  }));
}

@Component({
  selector: 'app-incorrect-stock-value-report',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './incorrect-stock-value-report.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class IncorrectStockValueReportComponent implements OnInit {
  private fb = inject(FormBuilder);
  private diagnosticsService = inject(StockDiagnosticsService);
  private accountService = inject(AccountService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    accountId: ['', Validators.required],
    fromDate: [''],
  });

  companies = signal<CompanyDto[]>([]);
  stockAccounts = signal<AccountDto[]>([]);
  report = signal<IncorrectStockValueReportDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe(res => {
      this.companies.set(res.items ?? []);
      const defaultId = this.companyContext.currentCompanyId();
      if (defaultId && !this.filters.get('companyId')?.value) {
        this.filters.patchValue({ companyId: defaultId });
      }
      this.loadStockAccounts();
    });
  }

  onCompanyChange(): void {
    this.filters.patchValue({ accountId: '' });
    this.loadStockAccounts();
  }

  loadStockAccounts(): void {
    const companyId = this.filters.get('companyId')?.value;
    if (!companyId) {
      this.stockAccounts.set([]);
      return;
    }
    this.accountService
      .getList({
        companyId,
        accountSubType: AccountSubType.Stock,
        isGroup: false,
        skipCount: 0,
        maxResultCount: 100,
        sorting: 'accountCode',
      })
      .subscribe(res => {
        const items = res.items ?? [];
        this.stockAccounts.set(items);
        if (items.length > 0 && !this.filters.get('accountId')?.value) {
          this.filters.patchValue({ accountId: items[0].id ?? '' });
        }
      });
  }

  generate(): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }
    this.isLoading.set(true);
    const { companyId, accountId, fromDate } = this.filters.getRawValue();
    this.diagnosticsService
      .getIncorrectStockValue({
        companyId: companyId!,
        accountId: accountId!,
        fromDate: fromDate || null,
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
    exportToCsv('incorrect-stock-value-report.csv', buildIncorrectStockValueCsvRows(r.rows), [
      'Posting Date',
      'Voucher Type',
      'Voucher No',
      'Item Code',
      'Warehouse',
      'Expected Stock Value',
      'Stock Value',
      'Difference Value',
    ]);
  }
}
