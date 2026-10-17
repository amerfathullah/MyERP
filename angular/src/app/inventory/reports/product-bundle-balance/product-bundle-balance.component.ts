import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { StockBalanceService } from '../../../proxy/inventory/stock-balance.service';
import type { ProductBundleBalanceReportDto, ProductBundleBalanceRowDto } from '../../../proxy/inventory/models';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-product-bundle-balance',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-boxes-packing me-2 text-primary"></i>{{ '::ProductBundleBalance' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::ProductBundleBalanceDescription' | abpLocalization }}</small>
        </div>
        @if (data() && data()!.rows && data()!.rows.length > 0) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-4 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Warehouse' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedWarehouseId" (change)="loadData()">
              <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
              @for (w of warehouses(); track w.id) {
                <option [value]="w.id">{{ w.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-5 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Search' | abpLocalization }}</label>
            <input
              type="text"
              class="form-control form-control-sm"
              [placeholder]="'::Search' | abpLocalization"
              [(ngModel)]="searchTerm"
            />
          </div>
          <div class="col-md-3 col-sm-12">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Summary KPI Cards -->
        @if (data() && data()!.rows && data()!.rows.length > 0) {
          <div class="row g-3 mb-4">
            <div class="col-md-4 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalBundles' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-primary mt-1">
                    {{ parentRowCount() }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-4 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::AvailableBundles' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-success mt-1">
                    {{ totalAvailableBundles() | number: '1.0-2' }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-4 col-sm-12">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::Showing' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-dark mt-1">
                    {{ filteredRows().length }} {{ '::Entries' | abpLocalization }}
                  </div>
                </div>
              </div>
            </div>
          </div>
        }

        <!-- Data Table -->
        @if (isLoading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">{{ '::Loading' | abpLocalization }}...</span>
            </div>
          </div>
        } @else if (!data() || filteredRows().length === 0) {
          <div class="text-center text-muted py-5">
            <i class="fa fa-boxes-stacked fa-3x mb-3 text-secondary"></i>
            <p>{{ '::NoProductBundleBalanceRecordsFound' | abpLocalization }}</p>
          </div>
        } @else {
          <div class="table-responsive shadow-sm border rounded">
            <table class="table table-sm table-hover align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th style="min-width: 250px;">{{ '::Item' | abpLocalization }}</th>
                  <th style="width: 150px;">{{ '::Warehouse' | abpLocalization }}</th>
                  <th style="width: 90px;">{{ '::UOM' | abpLocalization }}</th>
                  <th class="text-end" style="width: 130px;">{{ '::ActualQty' | abpLocalization }}</th>
                  <th class="text-end" style="width: 130px;">{{ '::MinimumQty' | abpLocalization }}</th>
                  <th class="text-end" style="width: 150px;">{{ '::BundleQty' | abpLocalization }}</th>
                  <th style="width: 120px;">{{ '::Status' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track $index) {
                  <tr [class.table-light]="row.indent === 0" [class.fw-bold]="row.indent === 0">
                    <td [style.padding-left.rem]="row.indent === 0 ? 0.75 : 2.25">
                      @if (row.indent === 0) {
                        <i class="fa fa-cube text-primary me-2"></i>
                        <span>{{ row.itemCode }} - {{ row.itemName }}</span>
                        <span class="badge bg-primary ms-2">{{ '::ParentItem' | abpLocalization }}</span>
                      } @else {
                        <i class="fa fa-caret-right text-muted me-2"></i>
                        <span>{{ row.itemCode }} - {{ row.itemName }}</span>
                      }
                    </td>
                    <td>
                      <span class="small text-muted">{{ row.warehouseName }}</span>
                    </td>
                    <td>
                      <span class="small">{{ row.uom || '-' }}</span>
                    </td>
                    <td class="text-end font-monospace">
                      @if (row.indent > 0) {
                        <span [class.text-danger]="row.actualQty <= 0">
                          {{ row.actualQty | number: '1.2-4' }}
                        </span>
                      } @else {
                        <span class="text-muted">-</span>
                      }
                    </td>
                    <td class="text-end font-monospace">
                      @if (row.indent > 0) {
                        <span>{{ row.minimumQty | number: '1.2-4' }}</span>
                      } @else {
                        <span class="text-muted">-</span>
                      }
                    </td>
                    <td class="text-end font-monospace">
                      <span [class.text-success]="row.bundleQty > 0" [class.text-danger]="row.bundleQty === 0">
                        {{ row.bundleQty | number: '1.0-2' }}
                      </span>
                    </td>
                    <td>
                      @if (row.indent === 0) {
                        @if (row.bundleQty > 0) {
                          <span class="badge bg-success">{{ '::Available' | abpLocalization }}</span>
                        } @else {
                          <span class="badge bg-danger">{{ '::OutOfStock' | abpLocalization }}</span>
                        }
                      } @else {
                        @if (row.actualQty < row.minimumQty) {
                          <span class="badge bg-warning text-dark">{{ '::Shortage' | abpLocalization }}</span>
                        } @else {
                          <span class="badge bg-secondary">{{ '::Component' | abpLocalization }}</span>
                        }
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>
  `,
})
export class ProductBundleBalanceComponent implements OnInit {
  private readonly stockBalanceService = inject(StockBalanceService);
  private readonly warehouseService = inject(WarehouseService);
  private readonly companyContext = inject(CompanyContextService);

  data = signal<ProductBundleBalanceReportDto | null>(null);
  isLoading = signal<boolean>(false);
  warehouses = signal<{ id: string; name: string }[]>([]);

  selectedWarehouseId = '';
  searchTerm = '';

  parentRowCount = computed(() => {
    return this.data()?.rows?.filter(r => r.indent === 0).length ?? 0;
  });

  totalAvailableBundles = computed(() => {
    return this.data()?.rows?.filter(r => r.indent === 0).reduce((sum, r) => sum + r.bundleQty, 0) ?? 0;
  });

  filteredRows = computed(() => {
    const d = this.data();
    if (!d || !d.rows) return [];
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return d.rows;

    return d.rows.filter(r =>
      (r.itemCode && r.itemCode.toLowerCase().includes(term)) ||
      (r.itemName && r.itemName.toLowerCase().includes(term)) ||
      (r.warehouseName && r.warehouseName.toLowerCase().includes(term))
    );
  });

  ngOnInit(): void {
    this.loadWarehouses();
    this.loadData();
  }

  loadWarehouses(): void {
    const companyId = this.companyContext.selectedCompanyId();
    this.warehouseService.getList({
      maxResultCount: 200,
      skipCount: 0,
      companyId: companyId || undefined,
      isActive: true,
      isGroup: false
    } as any).subscribe({
      next: res => {
        this.warehouses.set((res.items ?? []).map(w => ({ id: w.id!, name: w.name! })));
      },
      error: () => this.warehouses.set([])
    });
  }

  loadData(): void {
    const companyId = this.companyContext.selectedCompanyId();
    this.isLoading.set(true);

    this.stockBalanceService.getProductBundleBalance({
      companyId: companyId || undefined,
      warehouseId: this.selectedWarehouseId || undefined
    }).subscribe({
      next: result => {
        this.data.set(result);
        this.isLoading.set(false);
      },
      error: () => {
        this.data.set(null);
        this.isLoading.set(false);
      }
    });
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (!rows || rows.length === 0) return;

    const exportData = rows.map(r => ({
      Type: r.indent === 0 ? 'Bundle' : 'Component',
      ItemCode: r.itemCode,
      ItemName: r.itemName,
      Warehouse: r.warehouseName,
      UOM: r.uom || '',
      ActualQty: r.actualQty,
      MinimumQty: r.minimumQty,
      BundleQty: r.bundleQty
    }));

    const columns = ['Type', 'ItemCode', 'ItemName', 'Warehouse', 'UOM', 'ActualQty', 'MinimumQty', 'BundleQty'];
    exportToCsv('Product_Bundle_Balance_Report', exportData, columns);
  }
}
