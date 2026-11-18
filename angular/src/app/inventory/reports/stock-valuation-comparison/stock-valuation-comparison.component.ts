import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
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
  ItemDto,
  ItemGroupDto,
  StockValuationComparisonReportDto,
  StockValuationComparisonRowDto,
  WarehouseDto,
} from '../../../proxy/inventory/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildStockValuationComparisonCsvRows(
  rows: StockValuationComparisonRowDto[] | undefined,
): Record<string, string | number>[] {
  return (rows ?? []).map(r => ({
    'Posting Date': r.postingDate ? r.postingDate.split('T')[0] : '',
    'Posting Time': r.postingTime ?? '',
    'Item Code': r.itemCode ?? '',
    Warehouse: r.warehouseName ?? '',
    'Voucher Type': r.voucherType ?? '',
    'Voucher No': r.voucherNo ?? '',
    'Qty Change': r.actualQty ?? 0,
    'Balance Qty': r.qtyAfterTransaction ?? 0,
    'Expected Balance Qty': r.expectedQtyAfterTransaction ?? 0,
    'Qty Diff': r.qtyDifference ?? 0,
    'Change in Stock Value': (r.stockValueDifference ?? 0).toFixed(2),
    'Expected Change in Stock Value': (r.expectedStockValueDifference ?? 0).toFixed(2),
    'Value Change Diff': (r.stockValueDifferenceDifference ?? 0).toFixed(2),
    'Valuation Rate': (r.valuationRate ?? 0).toFixed(4),
    'Expected Valuation Rate': (r.expectedValuationRate ?? 0).toFixed(4),
    'Rate Diff': (r.valuationRateDifference ?? 0).toFixed(4),
    'Balance Stock Value': (r.stockValue ?? 0).toFixed(2),
    'Expected Balance Stock Value': (r.expectedStockValue ?? 0).toFixed(2),
    'Balance Value Diff': (r.stockValueDifferenceInBalance ?? 0).toFixed(2),
    'Expected Value Based On': r.expectedBasis ?? '',
    'Adjusted By': r.adjustedBy ?? '',
  }));
}

export const SHOW_MODES = [
  'First Difference per Item-Warehouse',
  'All Differences',
  'All Entries',
] as const;

@Component({
  selector: 'app-stock-valuation-comparison',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './stock-valuation-comparison.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class StockValuationComparisonComponent implements OnInit {
  private fb = inject(FormBuilder);
  private diagnosticsService = inject(StockDiagnosticsService);
  private itemService = inject(ItemService);
  private itemGroupService = inject(ItemGroupService);
  private warehouseService = inject(WarehouseService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  readonly showModes = SHOW_MODES;

  filters = this.fb.group({
    companyId: [''],
    fromDate: [''],
    toDate: [''],
    itemId: [''],
    itemGroupId: [''],
    warehouseId: [''],
    show: ['First Difference per Item-Warehouse'],
    tolerance: [0.01],
    showAdjustedDifferences: [false],
  });

  companies = signal<CompanyDto[]>([]);
  items = signal<ItemDto[]>([]);
  itemGroups = signal<ItemGroupDto[]>([]);
  warehouses = signal<WarehouseDto[]>([]);
  report = signal<StockValuationComparisonReportDto | null>(null);
  selectedIds = signal<Set<string>>(new Set());
  isLoading = signal(false);
  isReposting = signal(false);

  diffRows = computed(() => (this.report()?.rows ?? []).filter(r => r.hasDifference));

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe(res => {
      const list = res.items ?? [];
      this.companies.set(list);
      const defaultId = this.companyContext.currentCompanyId() || list[0]?.id || '';
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
    const val = this.filters.getRawValue();
    if (!val.companyId) {
      this.toaster.warn('Please select a Company.');
      return;
    }

    this.isLoading.set(true);
    this.selectedIds.set(new Set());
    this.diagnosticsService
      .getStockValuationComparison({
        companyId: val.companyId,
        fromDate: val.fromDate || null,
        toDate: val.toDate || null,
        itemId: val.itemId || null,
        itemGroupId: val.itemGroupId || null,
        warehouseId: val.warehouseId || null,
        show: val.show || SHOW_MODES[0],
        tolerance: val.tolerance ?? 0.01,
        showAdjustedDifferences: Boolean(val.showAdjustedDifferences),
      })
      .subscribe({
        next: res => {
          this.report.set(res);
          this.isLoading.set(false);
        },
        error: () => {
          this.toaster.error('Failed to load Stock Valuation Comparison.');
          this.isLoading.set(false);
        },
      });
  }

  toggleRow(sleId: string | undefined, checked: boolean): void {
    if (!sleId) return;
    const next = new Set(this.selectedIds());
    if (checked) {
      next.add(sleId);
    } else {
      next.delete(sleId);
    }
    this.selectedIds.set(next);
  }

  toggleAll(checked: boolean): void {
    if (!checked) {
      this.selectedIds.set(new Set());
      return;
    }
    const ids = (this.report()?.rows ?? [])
      .filter(r => r.hasDifference && r.stockLedgerEntryId)
      .map(r => r.stockLedgerEntryId!);
    this.selectedIds.set(new Set(ids));
  }

  isSelected(sleId: string | undefined): boolean {
    return Boolean(sleId && this.selectedIds().has(sleId));
  }

  repostSelected(): void {
    const companyId = this.filters.get('companyId')?.value;
    if (!companyId) return;

    const selected = Array.from(this.selectedIds());
    const targetIds = selected.length
      ? selected
      : (this.report()?.rows ?? [])
          .filter(r => r.hasDifference && r.stockLedgerEntryId)
          .map(r => r.stockLedgerEntryId!);

    if (!targetIds.length) {
      this.toaster.info('There are no differences to fix.');
      return;
    }

    this.isReposting.set(true);
    this.diagnosticsService
      .createStockValuationReposts({
        companyId,
        stockLedgerEntryIds: targetIds,
      })
      .subscribe({
        next: res => {
          this.isReposting.set(false);
          const createdCount = res.created?.length ?? 0;
          const skippedCount = res.skipped?.length ?? 0;
          this.toaster.success(`Queued ${createdCount} repost(s), skipped ${skippedCount}.`);
          this.generate();
        },
        error: () => {
          this.isReposting.set(false);
          this.toaster.error('Failed to queue repost entries.');
        },
      });
  }

  hasNonZero(val: number | undefined | null): boolean {
    return (val ?? 0) !== 0;
  }

  exportCsv(): void {
    const r = this.report();
    if (!r?.rows?.length) return;
    const csvRows = buildStockValuationComparisonCsvRows(r.rows);
    exportToCsv('stock-valuation-comparison', csvRows, Object.keys(csvRows[0]));
  }
}
