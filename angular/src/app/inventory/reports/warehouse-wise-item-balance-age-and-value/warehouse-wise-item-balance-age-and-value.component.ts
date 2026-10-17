import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { WarehouseWiseItemBalanceAgeAndValueService } from '../../../proxy/inventory/warehouse-wise-item-balance-age-and-value.service';
import type {
  WarehouseWiseItemBalanceAgeAndValueReportDto,
  WarehouseWiseItemBalanceAgeAndValueRowDto,
  WarehouseDto,
} from '../../../proxy/inventory/models';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-warehouse-wise-item-balance-age-and-value',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-cubes me-2 text-info"></i>{{ '::WarehouseWiseItemBalanceAgeAndValue' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::WarehouseWiseItemBalanceAgeAndValueDescription' | abpLocalization }}</small>
        </div>
        <div class="d-flex gap-2">
          @if (data() && data()!.rows && data()!.rows.length > 0) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
      </div>

      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Warehouse' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedWarehouseId" (change)="loadData()">
              <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
              @for (w of warehouses(); track w.id) {
                <option [value]="w.id">{{ w.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::ToDate' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" />
          </div>
          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Search' | abpLocalization }}</label>
            <input
              type="text"
              class="form-control form-control-sm"
              [placeholder]="'::Search' | abpLocalization"
              [(ngModel)]="searchTerm"
            />
          </div>
          <div class="col-md-2 col-sm-6">
            <div class="form-check mb-1">
              <input
                class="form-check-input"
                type="checkbox"
                id="filterZeroQtyCheck"
                [(ngModel)]="filterTotalZeroQty"
                (change)="loadData()"
              />
              <label class="form-check-label small fw-semibold" for="filterZeroQtyCheck">
                {{ '::FilterTotalZeroQty' | abpLocalization }}
              </label>
            </div>
          </div>
          <div class="col-md-2 col-sm-6">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Summary KPI Cards -->
        @if (data()) {
          <div class="row g-3 mb-4">
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalItems' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-primary mt-1">
                    {{ data()!.totalItems }}
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
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalStockValue' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-dark mt-1">
                    {{ data()!.totalStockValue | number: '1.2-2' }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::AverageAgeDays' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-warning mt-1">
                    {{ data()!.averageAgeDays | number: '1.1-2' }}
                  </div>
                </div>
              </div>
            </div>
          </div>
        }

        <!-- Table -->
        @if (isLoading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">Loading...</span>
            </div>
          </div>
        } @else if (filteredRows().length === 0) {
          <div class="text-center py-5 text-muted">
            <i class="fa fa-info-circle fa-2x mb-2 d-block"></i>
            {{ '::NoWarehouseWiseRecordsFound' | abpLocalization }}
          </div>
        } @else {
          <div class="table-responsive">
            <table class="table table-hover align-middle table-sm border">
              <thead class="table-light">
                <tr>
                  <th>{{ '::ItemCode' | abpLocalization }}</th>
                  <th>{{ '::ItemName' | abpLocalization }}</th>
                  <th>{{ '::ItemGroup' | abpLocalization }}</th>
                  <th class="text-end">{{ '::TotalStockValue' | abpLocalization }}</th>
                  <th class="text-center">{{ '::AverageAgeDays' | abpLocalization }}</th>
                  <th class="text-end">{{ '::TotalQty' | abpLocalization }}</th>
                  @for (col of data()!.warehouseColumns; track col.warehouseId) {
                    <th class="text-end">{{ col.warehouseName }}</th>
                  }
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track row.itemId) {
                  <tr>
                    <td class="fw-semibold text-primary">{{ row.itemCode }}</td>
                    <td>{{ row.itemName }}</td>
                    <td>
                      {{ row.itemGroup || '-' }}
                      @if (row.brand) {
                        <br /><small class="text-muted">Brand: {{ row.brand }}</small>
                      }
                    </td>
                    <td class="text-end fw-semibold">{{ row.totalStockValue | number: '1.2-2' }}</td>
                    <td class="text-center">
                      <span class="badge bg-secondary">{{ row.averageAgeDays | number: '1.0-1' }} d</span>
                    </td>
                    <td class="text-end fw-bold text-dark">
                      {{ row.totalQty | number: '1.2-4' }}
                      @if (row.stockUom) {
                        <small class="text-muted ms-1">{{ row.stockUom }}</small>
                      }
                    </td>
                    @for (col of data()!.warehouseColumns; track col.warehouseId) {
                      <td class="text-end">
                        {{ (row.warehouseBalances[col.warehouseId] || 0) | number: '1.2-4' }}
                      </td>
                    }
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
export class WarehouseWiseItemBalanceAgeAndValueComponent implements OnInit {
  private reportService = inject(WarehouseWiseItemBalanceAgeAndValueService);
  private warehouseService = inject(WarehouseService);
  private companyContextService = inject(CompanyContextService);

  data = signal<WarehouseWiseItemBalanceAgeAndValueReportDto | null>(null);
  warehouses = signal<WarehouseDto[]>([]);
  isLoading = signal(false);

  selectedWarehouseId = '';
  toDate = new Date().toISOString().split('T')[0];
  filterTotalZeroQty = true;
  searchTerm = '';

  filteredRows = computed(() => {
    const report = this.data();
    if (!report || !report.rows) return [];
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return report.rows;
    return report.rows.filter(
      r =>
        r.itemCode.toLowerCase().includes(term) ||
        r.itemName.toLowerCase().includes(term) ||
        (r.itemGroup && r.itemGroup.toLowerCase().includes(term)) ||
        (r.brand && r.brand.toLowerCase().includes(term))
    );
  });

  ngOnInit(): void {
    this.loadWarehouses();
    this.loadData();
  }

  loadWarehouses(): void {
    this.warehouseService.getList({ maxResultCount: 100 } as any).subscribe({
      next: res => {
        this.warehouses.set(res.items || []);
      },
    });
  }

  loadData(): void {
    const companyId = this.companyContextService.selectedCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.reportService
      .getReport({
        companyId,
        warehouseId: this.selectedWarehouseId || null,
        toDate: this.toDate,
        filterTotalZeroQty: this.filterTotalZeroQty,
        searchTerm: this.searchTerm || null,
      })
      .subscribe({
        next: res => {
          this.data.set(res);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
        },
      });
  }

  exportCsv(): void {
    const report = this.data();
    const rows = this.filteredRows();
    if (!report || !rows || rows.length === 0) return;

    const columns = [
      'itemCode',
      'itemName',
      'itemGroup',
      'brand',
      'stockUom',
      'totalStockValue',
      'averageAgeDays',
      'totalQty',
      ...report.warehouseColumns.map(c => `wh_${c.warehouseName}`),
    ];

    const exportData = rows.map(r => {
      const rowObj: any = {
        itemCode: r.itemCode,
        itemName: r.itemName,
        itemGroup: r.itemGroup || '',
        brand: r.brand || '',
        stockUom: r.stockUom || '',
        totalStockValue: r.totalStockValue,
        averageAgeDays: r.averageAgeDays,
        totalQty: r.totalQty,
      };

      for (const col of report.warehouseColumns) {
        rowObj[`wh_${col.warehouseName}`] = r.warehouseBalances[col.warehouseId] || 0;
      }

      return rowObj;
    });

    exportToCsv(
      `warehouse_wise_item_balance_age_and_value_${new Date().toISOString().split('T')[0]}.csv`,
      exportData,
      columns
    );
  }
}
