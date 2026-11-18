import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { StockDiagnosticsService } from '../../../proxy/inventory/stock-diagnostics.service';
import { ItemService } from '../../../proxy/inventory/item.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import type { CompanyDto } from '../../../proxy/core/models';
import type {
  IncorrectBalanceQtyReportDto,
  IncorrectBalanceQtyRowDto,
  ItemDto,
  WarehouseDto,
} from '../../../proxy/inventory/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildIncorrectBalanceQtyCsvRows(
  rows: IncorrectBalanceQtyRowDto[] | undefined,
): Record<string, string | number>[] {
  return (rows ?? []).map(r => ({
    'Posting Date': r.postingDate ? r.postingDate.split('T')[0] : '',
    'Voucher Type': r.voucherType ?? '',
    'Voucher No': r.voucherNo ?? '',
    'Item Code': r.itemCode ?? '',
    Warehouse: r.warehouseName ?? '',
    'Expected Balance Qty': (r.expectedBalanceQty ?? 0).toFixed(2),
    'Actual Balance Qty': (r.actualBalanceQty ?? 0).toFixed(2),
    Difference: (r.difference ?? 0).toFixed(2),
  }));
}

@Component({
  selector: 'app-incorrect-balance-qty-after-transaction',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './incorrect-balance-qty-after-transaction.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class IncorrectBalanceQtyAfterTransactionComponent implements OnInit {
  private fb = inject(FormBuilder);
  private diagnosticsService = inject(StockDiagnosticsService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  filters = this.fb.group({
    companyId: [''],
    warehouseId: [''],
    itemId: [''],
  });

  companies = signal<CompanyDto[]>([]);
  warehouses = signal<WarehouseDto[]>([]);
  items = signal<ItemDto[]>([]);
  report = signal<IncorrectBalanceQtyReportDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe(res => {
      this.companies.set(res.items ?? []);
      const defaultId = this.companyContext.currentCompanyId();
      if (defaultId && !this.filters.get('companyId')?.value) {
        this.filters.patchValue({ companyId: defaultId });
      }
      this.loadLookups();
      this.generate();
    });
  }

  onCompanyChange(): void {
    this.filters.patchValue({ warehouseId: '', itemId: '' });
    this.loadLookups();
  }

  loadLookups(): void {
    const companyId = this.filters.get('companyId')?.value || undefined;
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'name' }).subscribe(res => {
      const list = res.items ?? [];
      this.warehouses.set(companyId ? list.filter(w => w.companyId === companyId) : list);
    });
    this.itemService
      .getList({ companyId, skipCount: 0, maxResultCount: 500, sorting: 'itemCode' })
      .subscribe(res => {
        this.items.set(res.items ?? []);
      });
  }

  generate(): void {
    this.isLoading.set(true);
    const { companyId, warehouseId, itemId } = this.filters.getRawValue();
    this.diagnosticsService
      .getIncorrectBalanceQty({
        companyId: companyId || null,
        warehouseId: warehouseId || null,
        itemId: itemId || null,
        precision: 2,
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
    exportToCsv('incorrect-balance-qty-after-transaction.csv', buildIncorrectBalanceQtyCsvRows(r.rows), [
      'Posting Date',
      'Voucher Type',
      'Voucher No',
      'Item Code',
      'Warehouse',
      'Expected Balance Qty',
      'Actual Balance Qty',
      'Difference',
    ]);
  }
}
