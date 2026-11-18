import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { ItemService } from '../../../proxy/inventory/item.service';
import { StockDiagnosticsService } from '../../../proxy/inventory/stock-diagnostics.service';
import type { CompanyDto } from '../../../proxy/core/models';
import type {
  IncorrectSerialNoValuationReportDto,
  IncorrectSerialNoValuationRowDto,
  ItemDto,
} from '../../../proxy/inventory/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildIncorrectSerialNoValuationCsvRows(
  rows: IncorrectSerialNoValuationRowDto[] | undefined,
): Record<string, string | number>[] {
  return (rows ?? []).map(r => ({
    'Posting Date': r.postingDate ? r.postingDate.split('T')[0] : '',
    'Voucher Type': r.voucherType ?? '',
    'Voucher No': r.voucherNo ?? '',
    'Item Code': r.itemCode ?? '',
    Warehouse: r.warehouseName ?? '',
    'Serial No': r.serialNoNumber ?? '',
    Qty: r.qty ?? 0,
    'Valuation Rate': (r.valuationRate ?? 0).toFixed(4),
  }));
}

@Component({
  selector: 'app-incorrect-serial-no-valuation',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './incorrect-serial-no-valuation.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class IncorrectSerialNoValuationComponent implements OnInit {
  private fb = inject(FormBuilder);
  private diagnosticsService = inject(StockDiagnosticsService);
  private itemService = inject(ItemService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  private today = new Date();
  private monthAgo = new Date(this.today.getFullYear(), this.today.getMonth() - 1, this.today.getDate());

  filters = this.fb.group({
    companyId: [''],
    itemId: [''],
    fromDate: [this.monthAgo.toISOString().split('T')[0]],
    toDate: [this.today.toISOString().split('T')[0]],
  });

  companies = signal<CompanyDto[]>([]);
  items = signal<ItemDto[]>([]);
  report = signal<IncorrectSerialNoValuationReportDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe(res => {
      this.companies.set(res.items ?? []);
      const defaultId = this.companyContext.currentCompanyId();
      if (defaultId && !this.filters.get('companyId')?.value) {
        this.filters.patchValue({ companyId: defaultId });
      }
      this.loadItems();
      this.generate();
    });
  }

  onCompanyChange(): void {
    this.filters.patchValue({ itemId: '' });
    this.loadItems();
  }

  loadItems(): void {
    const companyId = this.filters.get('companyId')?.value || undefined;
    this.itemService
      .getList({ companyId, hasSerialNo: true, skipCount: 0, maxResultCount: 500, sorting: 'itemCode' })
      .subscribe(res => {
        this.items.set(res.items ?? []);
      });
  }

  generate(): void {
    this.isLoading.set(true);
    const { companyId, itemId, fromDate, toDate } = this.filters.getRawValue();
    this.diagnosticsService
      .getIncorrectSerialNoValuation({
        companyId: companyId || null,
        itemId: itemId || null,
        fromDate: fromDate || null,
        toDate: toDate || null,
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
    exportToCsv('incorrect-serial-no-valuation.csv', buildIncorrectSerialNoValuationCsvRows(r.rows), [
      'Posting Date',
      'Voucher Type',
      'Voucher No',
      'Item Code',
      'Warehouse',
      'Serial No',
      'Qty',
      'Valuation Rate',
    ]);
  }
}
