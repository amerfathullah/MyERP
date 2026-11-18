import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { ItemService } from '../../../proxy/inventory/item.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { StockDiagnosticsService } from '../../../proxy/inventory/stock-diagnostics.service';
import type { CompanyDto } from '../../../proxy/core/models';
import type {
  IncorrectSerialAndBatchBundleReportDto,
  IncorrectSerialAndBatchBundleRowDto,
  ItemDto,
  WarehouseDto,
} from '../../../proxy/inventory/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildIncorrectBundleCsvRows(
  rows: IncorrectSerialAndBatchBundleRowDto[] | undefined,
): Record<string, string>[] {
  return (rows ?? []).map(r => ({
    'Item Code': r.itemCode ?? '',
    Warehouse: r.warehouseName ?? '',
    'Voucher Type': r.voucherType ?? '',
    'Voucher No': r.voucherNo ?? '',
    'Issue Type': r.issueType ?? '',
    'Is Cancelled': r.isCancelled ? 'Yes' : 'No',
  }));
}

@Component({
  selector: 'app-incorrect-serial-and-batch-bundle',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './incorrect-serial-and-batch-bundle.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class IncorrectSerialAndBatchBundleComponent implements OnInit {
  private fb = inject(FormBuilder);
  private diagnosticsService = inject(StockDiagnosticsService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  filters = this.fb.group({
    companyId: [''],
    itemId: [''],
    warehouseId: [''],
    voucherType: [''],
    postingDate: [''],
  });

  companies = signal<CompanyDto[]>([]);
  items = signal<ItemDto[]>([]);
  warehouses = signal<WarehouseDto[]>([]);
  report = signal<IncorrectSerialAndBatchBundleReportDto | null>(null);
  selectedBundleIds = signal<Set<string>>(new Set());
  isLoading = signal(false);
  isFixing = signal(false);

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
    this.isLoading.set(true);
    const { companyId, itemId, warehouseId, voucherType, postingDate } = this.filters.getRawValue();
    this.diagnosticsService
      .getIncorrectSerialAndBatchBundles({
        companyId: companyId || null,
        itemId: itemId || null,
        warehouseId: warehouseId || null,
        voucherType: voucherType?.trim() || null,
        postingDate: postingDate || null,
      })
      .subscribe({
        next: res => {
          this.report.set(res);
          this.selectedBundleIds.set(new Set());
          this.isLoading.set(false);
        },
        error: err => {
          this.toaster.error(err?.error?.error?.message || 'Error generating report');
          this.isLoading.set(false);
        },
      });
  }

  toggleSelection(bundleId: string | undefined): void {
    if (!bundleId) return;
    const next = new Set(this.selectedBundleIds());
    if (next.has(bundleId)) {
      next.delete(bundleId);
    } else {
      next.add(bundleId);
    }
    this.selectedBundleIds.set(next);
  }

  fixSelected(): void {
    const ids = Array.from(this.selectedBundleIds());
    if (ids.length === 0) return;
    this.isFixing.set(true);
    this.diagnosticsService.fixIncorrectSerialAndBatchBundles({ bundleIds: ids }).subscribe({
      next: res => {
        this.isFixing.set(false);
        this.toaster.success(
          `Cancelled: ${res.cancelledCount ?? 0}, Restored: ${res.restoredCount ?? 0}, Skipped: ${res.skippedCount ?? 0}`,
        );
        this.generate();
      },
      error: err => {
        this.isFixing.set(false);
        this.toaster.error(err?.error?.error?.message || 'Error fixing bundles');
      },
    });
  }

  exportCsv(): void {
    const r = this.report();
    if (!r?.rows?.length) return;
    exportToCsv('incorrect-serial-and-batch-bundle.csv', buildIncorrectBundleCsvRows(r.rows), [
      'Item Code',
      'Warehouse',
      'Voucher Type',
      'Voucher No',
      'Issue Type',
      'Is Cancelled',
    ]);
  }
}
