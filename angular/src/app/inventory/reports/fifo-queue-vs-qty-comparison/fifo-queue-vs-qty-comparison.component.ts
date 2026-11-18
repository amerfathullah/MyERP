import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { ItemService } from '../../../proxy/inventory/item.service';
import { ItemGroupService } from '../../../proxy/inventory/item-group.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { StockDiagnosticsService } from '../../../proxy/inventory/stock-diagnostics.service';
import type { CompanyDto } from '../../../proxy/core/models';
import type {
  FifoQueueVsQtyComparisonReportDto,
  FifoQueueVsQtyComparisonRowDto,
  ItemDto,
  ItemGroupDto,
  WarehouseDto,
} from '../../../proxy/inventory/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildFifoComparisonCsvRows(
  rows: FifoQueueVsQtyComparisonRowDto[] | undefined,
): Record<string, string | number>[] {
  return (rows ?? []).map(r => ({
    'Item Code': r.itemCode ?? '',
    Warehouse: r.warehouseName ?? '',
    'Posting Date': r.postingDate ? r.postingDate.split('T')[0] : '',
    'Voucher Type': r.voucherType ?? '',
    'Voucher No': r.voucherNo ?? '',
    'Qty Change': r.actualQty ?? 0,
    'Qty After Transaction': r.qtyAfterTransaction ?? 0,
    'FIFO Queue Qty': r.fifoQueueQty ?? 0,
    'Qty Diff': r.fifoQtyDiff ?? 0,
    'Stock Value': (r.stockValue ?? 0).toFixed(2),
    'FIFO Stock Value': (r.fifoStockValue ?? 0).toFixed(2),
    'Value Diff': (r.fifoValueDiff ?? 0).toFixed(2),
    'Valuation Rate': (r.valuationRate ?? 0).toFixed(4),
  }));
}

@Component({
  selector: 'app-fifo-queue-vs-qty-comparison',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './fifo-queue-vs-qty-comparison.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class FifoQueueVsQtyComparisonComponent implements OnInit {
  private fb = inject(FormBuilder);
  private diagnosticsService = inject(StockDiagnosticsService);
  private itemService = inject(ItemService);
  private itemGroupService = inject(ItemGroupService);
  private warehouseService = inject(WarehouseService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  filters = this.fb.group({
    companyId: [''],
    itemId: [''],
    itemGroupId: [''],
    warehouseId: [''],
    fromDate: [''],
    toDate: [''],
  });

  companies = signal<CompanyDto[]>([]);
  items = signal<ItemDto[]>([]);
  itemGroups = signal<ItemGroupDto[]>([]);
  warehouses = signal<WarehouseDto[]>([]);
  report = signal<FifoQueueVsQtyComparisonReportDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe(res => {
      this.companies.set(res.items ?? []);
      const defaultId = this.companyContext.currentCompanyId();
      if (defaultId && !this.filters.get('companyId')?.value) {
        this.filters.patchValue({ companyId: defaultId });
      }
      this.loadLookups();
    });
    this.itemGroupService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' }).subscribe(res => {
      this.itemGroups.set(res.items ?? []);
    });
  }

  onCompanyChange(): void {
    this.filters.patchValue({ itemId: '', warehouseId: '' });
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
    const { companyId, itemId, itemGroupId, warehouseId, fromDate, toDate } = this.filters.getRawValue();
    if (!warehouseId && !itemId && !itemGroupId) {
      this.toaster.warn('Select Warehouse, Item, or Item Group.');
      return;
    }
    this.isLoading.set(true);
    this.diagnosticsService
      .getFifoQueueVsQtyComparison({
        companyId: companyId || null,
        itemId: itemId || null,
        itemGroupId: itemGroupId || null,
        warehouseId: warehouseId || null,
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

  hasDiff(val: number | undefined): boolean {
    return Math.abs(val ?? 0) > 0.001;
  }

  exportCsv(): void {
    const r = this.report();
    if (!r?.rows?.length) return;
    exportToCsv('fifo-queue-vs-qty-comparison.csv', buildFifoComparisonCsvRows(r.rows), [
      'Item Code',
      'Warehouse',
      'Posting Date',
      'Voucher Type',
      'Voucher No',
      'Qty Change',
      'Qty After Transaction',
      'FIFO Queue Qty',
      'Qty Diff',
      'Stock Value',
      'FIFO Stock Value',
      'Value Diff',
      'Valuation Rate',
    ]);
  }
}
