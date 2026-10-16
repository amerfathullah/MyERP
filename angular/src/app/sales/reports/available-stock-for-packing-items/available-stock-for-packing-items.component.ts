import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { SalesAnalyticsService } from '../../../proxy/sales/sales-analytics.service';
import { ProductBundleService } from '../../../proxy/sales/product-bundle.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import type {
  AvailableStockForPackingItemDto,
  ProductBundleDto,
} from '../../../proxy/sales/models';
import type { WarehouseDto } from '../../../proxy/inventory/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-available-stock-for-packing-items',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-boxes text-primary me-2"></i>
            {{ '::AvailableStockForPackingItems' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::AvailableStockForPackingItemsDescription' | abpLocalization }}
          </p>
        </div>
        @if (filteredRows().length > 0) {
          <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fa fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <!-- Filters -->
      <div class="card mb-3 shadow-sm border-0">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::Warehouse' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedWarehouseId"
                (ngModelChange)="loadReport()"
              >
                <option value="">-- {{ '::All' | abpLocalization }} --</option>
                @for (wh of warehouses(); track wh.id) {
                  <option [value]="wh.id">{{ wh.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-4">
              <label class="form-label small mb-1">{{ '::BundleItem' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedBundleItemId"
                (ngModelChange)="loadReport()"
              >
                <option value="">-- {{ '::All' | abpLocalization }} --</option>
                @for (bundle of productBundles(); track bundle.id) {
                  <option [value]="bundle.itemId">
                    {{ bundle.itemName || bundle.itemId }}
                  </option>
                }
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::Search' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                placeholder="Search code, name, warehouse..."
                [(ngModel)]="searchTerm"
              />
            </div>
            <div class="col-md-2 d-flex align-items-end">
              <button
                class="btn btn-primary btn-sm ms-auto px-4 w-100"
                (click)="loadReport()"
                [disabled]="isLoading()"
              >
                @if (isLoading()) {
                  <span class="spinner-border spinner-border-sm me-1"></span>
                } @else {
                  <i class="fa fa-sync-alt me-1"></i>
                }
                {{ '::Refresh' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- KPI Summary Cards -->
      <div class="row g-2 mb-3">
        <div class="col-12 col-md-4">
          <div class="card border-0 bg-light shadow-sm text-center p-2">
            <div class="text-muted small">{{ '::AvailablePackableBundles' | abpLocalization }}</div>
            <div class="h5 mb-0 fw-bold text-primary">
              {{ totalPackableBundles() | number:'1.0-2' }}
            </div>
          </div>
        </div>
        <div class="col-6 col-md-4">
          <div class="card border-0 bg-light shadow-sm text-center p-2">
            <div class="text-muted small">{{ '::BundleItem' | abpLocalization }}s</div>
            <div class="h5 mb-0 fw-bold text-dark">
              {{ distinctBundleCount() }}
            </div>
          </div>
        </div>
        <div class="col-6 col-md-4">
          <div class="card border-0 bg-light shadow-sm text-center p-2">
            <div class="text-muted small">{{ '::Warehouse' | abpLocalization }}s</div>
            <div class="h5 mb-0 fw-bold text-dark">
              {{ distinctWarehouseCount() }}
            </div>
          </div>
        </div>
      </div>

      <!-- Loading State -->
      @if (isLoading()) {
        <div class="text-center py-5">
          <div class="spinner-border text-primary" role="status">
            <span class="visually-hidden">Loading...</span>
          </div>
          <div class="text-muted mt-2 small">{{ '::Loading' | abpLocalization }}...</div>
        </div>
      } @else if (filteredRows().length === 0) {
        <div class="alert alert-info text-center my-4 py-4 shadow-sm border-0">
          <i class="fa fa-info-circle fa-2x mb-2 text-info"></i>
          <p class="mb-0 fw-medium">{{ '::NoPackingStockFound' | abpLocalization }}</p>
        </div>
      } @else {
        <!-- Data Table -->
        <div class="card shadow-sm border-0">
          <div class="table-responsive">
            <table class="table table-hover table-striped align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th scope="col" style="width: 15%">{{ '::ItemCode' | abpLocalization }}</th>
                  <th scope="col" style="width: 25%">{{ '::ItemName' | abpLocalization }}</th>
                  <th scope="col" style="width: 20%">{{ '::Description' | abpLocalization }}</th>
                  <th scope="col" style="width: 10%">{{ '::Uom' | abpLocalization }}</th>
                  <th scope="col" style="width: 15%">{{ '::Warehouse' | abpLocalization }}</th>
                  <th scope="col" style="width: 15%" class="text-end">{{ '::Quantity' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (group of groupedRows(); track group.bundleItemId) {
                  @for (row of group.rows; track row.warehouseId) {
                    <tr>
                      <td class="fw-semibold text-primary">{{ row.bundleItemCode }}</td>
                      <td>{{ row.bundleItemName }}</td>
                      <td class="text-muted small">{{ row.description || '—' }}</td>
                      <td><span class="badge bg-secondary">{{ row.stockUom || 'Unit' }}</span></td>
                      <td>{{ row.warehouseName }}</td>
                      <td class="text-end fw-bold text-success">{{ row.availablePackableBundles | number:'1.0-2' }}</td>
                    </tr>
                  }
                  @if (group.rows.length > 1) {
                    <tr class="table-secondary fw-bold">
                      <td colspan="5" class="text-end pe-3">{{ '::Subtotal' | abpLocalization }} ({{ group.bundleItemName }}):</td>
                      <td class="text-end text-success">{{ group.totalQty | number:'1.0-2' }}</td>
                    </tr>
                  }
                }
              </tbody>
              <tfoot class="table-dark">
                <tr>
                  <td colspan="5" class="fw-bold text-end pe-3">{{ '::Total' | abpLocalization }}:</td>
                  <td class="text-end fw-bold">{{ totalPackableBundles() | number:'1.0-2' }}</td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>
      }
    </div>
  `,
})
export class AvailableStockForPackingItemsComponent implements OnInit {
  private salesAnalyticsService = inject(SalesAnalyticsService);
  private bundleService = inject(ProductBundleService);
  private warehouseService = inject(WarehouseService);
  private companyContext = inject(CompanyContextService);

  reportData = signal<AvailableStockForPackingItemDto[]>([]);
  productBundles = signal<ProductBundleDto[]>([]);
  warehouses = signal<WarehouseDto[]>([]);
  isLoading = signal(false);

  selectedWarehouseId = '';
  selectedBundleItemId = '';
  searchTerm = '';

  filteredRows = computed(() => {
    let rows = this.reportData();
    const term = this.searchTerm.trim().toLowerCase();
    if (term) {
      rows = rows.filter(
        r =>
          (r.bundleItemCode && r.bundleItemCode.toLowerCase().includes(term)) ||
          (r.bundleItemName && r.bundleItemName.toLowerCase().includes(term)) ||
          (r.warehouseName && r.warehouseName.toLowerCase().includes(term)) ||
          (r.description && r.description.toLowerCase().includes(term))
      );
    }
    return rows;
  });

  groupedRows = computed(() => {
    const rows = this.filteredRows();
    const groupsMap = new Map<string, { bundleItemId: string; bundleItemName: string; rows: AvailableStockForPackingItemDto[]; totalQty: number }>();

    for (const row of rows) {
      const key = row.bundleItemId || row.bundleItemCode || 'unknown';
      let grp = groupsMap.get(key);
      if (!grp) {
        grp = {
          bundleItemId: key,
          bundleItemName: row.bundleItemName || row.bundleItemCode || '—',
          rows: [],
          totalQty: 0,
        };
        groupsMap.set(key, grp);
      }
      grp.rows.push(row);
      grp.totalQty += row.availablePackableBundles || 0;
    }

    return Array.from(groupsMap.values());
  });

  totalPackableBundles = computed(() =>
    this.filteredRows().reduce((sum, r) => sum + (r.availablePackableBundles || 0), 0)
  );

  distinctBundleCount = computed(() => {
    const ids = new Set(this.filteredRows().map(r => r.bundleItemId));
    return ids.size;
  });

  distinctWarehouseCount = computed(() => {
    const ids = new Set(this.filteredRows().map(r => r.warehouseId));
    return ids.size;
  });

  ngOnInit(): void {
    this.loadDropdowns();
    this.loadReport();
  }

  loadDropdowns(): void {
    this.bundleService.getList({ maxResultCount: 200, skipCount: 0 }).subscribe({
      next: res => {
        this.productBundles.set(res.items || []);
      },
      error: () => {},
    });

    this.warehouseService.getList({ maxResultCount: 200, skipCount: 0 }).subscribe({
      next: res => {
        this.warehouses.set(res.items || []);
      },
      error: () => {},
    });
  }

  loadReport(): void {
    this.isLoading.set(true);
    const companyId = this.companyContext.selectedCompanyId() || undefined;

    this.salesAnalyticsService
      .getAvailableStockForPackingItemsReport({
        companyId: companyId || null,
        warehouseId: this.selectedWarehouseId || null,
        bundleItemId: this.selectedBundleItemId || null,
      })
      .subscribe({
        next: data => {
          this.reportData.set(data || []);
          this.isLoading.set(false);
        },
        error: () => {
          this.reportData.set([]);
          this.isLoading.set(false);
        },
      });
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (rows.length === 0) return;

    const data = rows.map(r => ({
      'Item Code': r.bundleItemCode || '',
      'Item Name': r.bundleItemName || '',
      'Description': r.description || '',
      'UOM': r.stockUom || 'Unit',
      'Warehouse': r.warehouseName || '',
      'Available Packable Bundles': r.availablePackableBundles,
    }));

    const columns = [
      'Item Code',
      'Item Name',
      'Description',
      'UOM',
      'Warehouse',
      'Available Packable Bundles',
    ];

    exportToCsv(
      `available-stock-for-packing-items-${new Date().toISOString().slice(0, 10)}.csv`,
      data,
      columns
    );
  }
}
