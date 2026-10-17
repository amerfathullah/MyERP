import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { WarehouseWiseStockBalanceService } from '../../../proxy/inventory/warehouse-wise-stock-balance.service';
import type { WarehouseWiseStockBalanceReportDto, WarehouseStockBalanceItemDto } from '../../../proxy/inventory/models';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-warehouse-wise-stock-balance',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-sitemap me-2 text-primary"></i>{{ '::WarehouseWiseStockBalance' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::WarehouseWiseStockBalanceDescription' | abpLocalization }}</small>
        </div>
        @if (data() && data()!.items && data()!.items.length > 0) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::AsOfDate' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="asOfDate" />
          </div>
          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Warehouse' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedWarehouseId" (change)="loadData()">
              <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
              @for (w of warehouses(); track w.id) {
                <option [value]="w.id">{{ w.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-3 col-sm-6">
            <div class="form-check mt-4">
              <input
                type="checkbox"
                class="form-check-input"
                id="showDisabledWarehouses"
                [(ngModel)]="showDisabledWarehouses"
                (change)="loadData()"
              />
              <label class="form-check-label small" for="showDisabledWarehouses">
                {{ '::ShowDisabledWarehouses' | abpLocalization }}
              </label>
            </div>
          </div>
          <div class="col-md-3 col-sm-6">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Filter & Search Sub-bar -->
        <div class="row g-2 mb-4 align-items-center">
          <div class="col-md-6">
            <div class="input-group input-group-sm">
              <span class="input-group-text"><i class="fa fa-search"></i></span>
              <input
                type="text"
                class="form-control"
                [placeholder]="'::Search' | abpLocalization"
                [(ngModel)]="searchTerm"
              />
            </div>
          </div>
          <div class="col-md-6 text-end text-muted small">
            @if (data()) {
              <span>{{ '::Showing' | abpLocalization }} {{ filteredItems().length }} {{ '::Entries' | abpLocalization }}</span>
            }
          </div>
        </div>

        <!-- Summary KPI Cards -->
        @if (data() && data()!.items && data()!.items.length > 0) {
          <div class="row g-3 mb-4">
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalStockBalance' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-primary mt-1">
                    {{ data()!.totalStockBalance | number: '1.2-2' }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalQuantity' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-success mt-1">
                    {{ data()!.totalQuantity | number: '1.2-4' }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalWarehouses' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-dark mt-1">
                    {{ data()!.totalWarehouseCount }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::ActiveWarehouses' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-info mt-1">
                    {{ data()!.activeWarehouseCount }}
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
        } @else if (!data() || filteredItems().length === 0) {
          <div class="text-center text-muted py-5">
            <i class="fa fa-boxes-stacked fa-3x mb-3 text-secondary"></i>
            <p>{{ '::NoWarehouseBalanceRecordsFound' | abpLocalization }}</p>
          </div>
        } @else {
          <div class="table-responsive shadow-sm border rounded">
            <table class="table table-sm table-hover align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th style="min-width: 250px;">{{ '::Warehouse' | abpLocalization }}</th>
                  <th style="width: 120px;">{{ '::Code' | abpLocalization }}</th>
                  <th style="width: 100px;">{{ '::Type' | abpLocalization }}</th>
                  <th class="text-end" style="width: 140px;">{{ '::StockBalance' | abpLocalization }}</th>
                  <th class="text-end" style="width: 140px;">{{ '::DirectStockBalance' | abpLocalization }}</th>
                  <th class="text-end" style="width: 120px;">{{ '::TotalQuantity' | abpLocalization }}</th>
                  <th class="text-end" style="width: 120px;">{{ '::DirectQuantity' | abpLocalization }}</th>
                  <th class="text-end" style="width: 100px;">{{ '::Items' | abpLocalization }}</th>
                  <th style="width: 90px;">{{ '::Status' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (item of filteredItems(); track item.warehouseId) {
                  <tr [class.table-light]="item.isGroup" [class.fw-bold]="item.isGroup">
                    <td [style.padding-left.rem]="(item.indent * 1.5) + 0.75">
                      @if (item.isGroup) {
                        <i class="fa fa-folder text-warning me-2"></i>
                      } @else {
                        <i class="fa fa-warehouse text-primary me-2"></i>
                      }
                      <span>{{ item.warehouseName }}</span>
                      @if (item.isGroup) {
                        <span class="badge bg-secondary ms-2">{{ '::Group' | abpLocalization }}</span>
                      }
                    </td>
                    <td>
                      <span class="text-muted font-monospace small">{{ item.warehouseCode || '-' }}</span>
                    </td>
                    <td>
                      <span class="small">{{ item.isGroup ? ('::Group' | abpLocalization) : ('::Leaf' | abpLocalization) }}</span>
                    </td>
                    <td class="text-end font-monospace">
                      <span [class.text-danger]="item.stockBalance < 0" [class.text-primary]="item.isGroup">
                        {{ item.stockBalance | number: '1.2-2' }}
                      </span>
                    </td>
                    <td class="text-end font-monospace">
                      <span [class.text-danger]="item.directStockBalance < 0">
                        {{ item.directStockBalance | number: '1.2-2' }}
                      </span>
                    </td>
                    <td class="text-end font-monospace">
                      <span [class.text-danger]="item.totalQuantity < 0">
                        {{ item.totalQuantity | number: '1.2-4' }}
                      </span>
                    </td>
                    <td class="text-end font-monospace">
                      <span [class.text-danger]="item.directQuantity < 0">
                        {{ item.directQuantity | number: '1.2-4' }}
                      </span>
                    </td>
                    <td class="text-end font-monospace">
                      {{ item.itemCount }}
                    </td>
                    <td>
                      @if (item.isActive) {
                        <span class="badge bg-success-subtle text-success border border-success-subtle">
                          {{ '::Active' | abpLocalization }}
                        </span>
                      } @else {
                        <span class="badge bg-danger-subtle text-danger border border-danger-subtle">
                          {{ '::Disabled' | abpLocalization }}
                        </span>
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
export class WarehouseWiseStockBalanceComponent implements OnInit {
  private service = inject(WarehouseWiseStockBalanceService);
  private warehouseService = inject(WarehouseService);
  private companyContext = inject(CompanyContextService);

  data = signal<WarehouseWiseStockBalanceReportDto | null>(null);
  warehouses = signal<any[]>([]);
  isLoading = signal(false);

  asOfDate = '';
  selectedWarehouseId = '';
  showDisabledWarehouses = false;
  searchTerm = '';

  filteredItems = computed(() => {
    const report = this.data();
    if (!report || !report.items) return [];

    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return report.items;

    return report.items.filter(
      i =>
        i.warehouseName.toLowerCase().includes(term) ||
        (i.warehouseCode && i.warehouseCode.toLowerCase().includes(term)) ||
        (i.parentWarehouseName && i.parentWarehouseName.toLowerCase().includes(term))
    );
  });

  ngOnInit() {
    this.loadWarehouses();
    this.loadData();
  }

  loadWarehouses() {
    this.warehouseService.getList({ maxResultCount: 200 } as any).subscribe({
      next: res => {
        this.warehouses.set(res.items || []);
      },
      error: () => {
        this.warehouses.set([]);
      },
    });
  }

  loadData() {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.service
      .getReport({
        companyId,
        warehouseId: this.selectedWarehouseId || undefined,
        showDisabledWarehouses: this.showDisabledWarehouses,
        asOfDate: this.asOfDate ? new Date(this.asOfDate).toISOString() : undefined,
      })
      .subscribe({
        next: res => {
          this.data.set(res);
          this.isLoading.set(false);
        },
        error: () => {
          this.data.set(null);
          this.isLoading.set(false);
        },
      });
  }

  exportCsv() {
    const items = this.filteredItems();
    if (!items || items.length === 0) return;

    const rows = items.map(i => ({
      Warehouse: '  '.repeat(i.indent) + i.warehouseName,
      Code: i.warehouseCode || '',
      Parent: i.parentWarehouseName || '',
      IsGroup: i.isGroup ? 'Yes' : 'No',
      StockBalance: i.stockBalance,
      DirectStockBalance: i.directStockBalance,
      TotalQuantity: i.totalQuantity,
      DirectQuantity: i.directQuantity,
      ItemCount: i.itemCount,
      IsActive: i.isActive ? 'Active' : 'Disabled',
    }));

    const columns = [
      'Warehouse',
      'Code',
      'Parent',
      'IsGroup',
      'StockBalance',
      'DirectStockBalance',
      'TotalQuantity',
      'DirectQuantity',
      'ItemCount',
      'IsActive',
    ];

    exportToCsv('warehouse_wise_stock_balance.csv', rows, columns);
  }
}
