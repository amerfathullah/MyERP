import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { StockBalanceService } from '../../../proxy/inventory/stock-balance.service';
import type {
  BatchWiseBalanceReportDto,
  BatchWiseBalanceRowDto,
  WarehouseDto,
} from '../../../proxy/inventory/models';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-batch-wise-balance',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-cubes-stacked me-2 text-info"></i>{{ '::BatchWiseBalance' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::BatchWiseBalanceDescription' | abpLocalization }}</small>
        </div>
        <div class="d-flex gap-2">
          @if (data() && data()!.rows && data()!.rows!.length > 0) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
      </div>

      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Warehouse' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedWarehouseId" (change)="loadData()">
              <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
              @for (w of warehouses(); track w.id) {
                <option [value]="w.id">{{ w.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::FromDate' | abpLocalization }}</label>
            <input
              type="date"
              class="form-control form-control-sm"
              [(ngModel)]="fromDate"
              (change)="loadData()"
            />
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::ToDate' | abpLocalization }}</label>
            <input
              type="date"
              class="form-control form-control-sm"
              [(ngModel)]="toDate"
              (change)="loadData()"
            />
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Search' | abpLocalization }}</label>
            <input
              type="text"
              class="form-control form-control-sm"
              [placeholder]="'::Search' | abpLocalization"
              [(ngModel)]="searchTerm"
            />
          </div>
          <div class="col-md-2 col-sm-6">
            <div class="form-check">
              <input
                class="form-check-input"
                type="checkbox"
                id="includeZeroCheck"
                [(ngModel)]="includeZeroBalance"
                (change)="loadData()"
              />
              <label class="form-check-label small fw-semibold" for="includeZeroCheck">
                {{ '::IncludeZeroBalance' | abpLocalization }}
              </label>
            </div>
            <div class="form-check">
              <input
                class="form-check-input"
                type="checkbox"
                id="includeExpiredCheck"
                [(ngModel)]="includeExpiredBatches"
                (change)="loadData()"
              />
              <label class="form-check-label small fw-semibold" for="includeExpiredCheck">
                {{ '::IncludeExpiredBatches' | abpLocalization }}
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
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalBatches' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-primary mt-1">
                    {{ data()!.totalBatches ?? 0 }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalStockAvailable' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-success mt-1">
                    {{ (data()!.totalQuantity ?? 0) | number: '1.2-4' }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalStockValue' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-dark mt-1">
                    {{ (data()!.totalStockValue ?? 0) | number: '1.2-2' }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalReservedStock' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-warning mt-1">
                    {{ (data()!.totalReservedStock ?? 0) | number: '1.2-4' }}
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
            {{ '::NoBatchDataFound' | abpLocalization }}
          </div>
        } @else {
          <div class="table-responsive">
            <table class="table table-hover align-middle table-sm border">
              <thead class="table-light">
                <tr>
                  <th>{{ '::ItemName' | abpLocalization }}</th>
                  <th>{{ '::Warehouse' | abpLocalization }}</th>
                  <th>{{ '::BatchNo' | abpLocalization }}</th>
                  <th>{{ '::ExpiryDate' | abpLocalization }}</th>
                  <th class="text-end">{{ '::BalanceQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ValuationRate' | abpLocalization }}</th>
                  <th class="text-end">{{ '::StockValue' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ReservedStockCurrent' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track $index) {
                  <tr>
                    <td class="fw-semibold text-primary">{{ row.itemName }}</td>
                    <td>{{ row.warehouseName || '—' }}</td>
                    <td>
                      <span class="badge bg-secondary font-monospace">{{ row.batchNo }}</span>
                    </td>
                    <td>
                      @if (row.expiryDate) {
                        <span>{{ row.expiryDate | date: 'mediumDate' }}</span>
                        @if (row.isExpired) {
                          <span class="badge bg-danger ms-2">{{ '::Expired' | abpLocalization }}</span>
                        }
                      } @else {
                        <span class="text-muted">—</span>
                      }
                    </td>
                    <td class="text-end fw-bold">
                      {{ (row.balance ?? 0) | number: '1.2-4' }}
                    </td>
                    <td class="text-end">
                      {{ (row.valuationRate ?? 0) | number: '1.2-4' }}
                    </td>
                    <td class="text-end fw-semibold">
                      {{ (row.stockValue ?? 0) | number: '1.2-2' }}
                    </td>
                    <td class="text-end text-warning fw-semibold">
                      {{ (row.reservedStockQty ?? 0) | number: '1.2-4' }}
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
export class BatchWiseBalanceComponent implements OnInit {
  private stockBalanceService = inject(StockBalanceService);
  private warehouseService = inject(WarehouseService);
  private companyContextService = inject(CompanyContextService);

  data = signal<BatchWiseBalanceReportDto | null>(null);
  warehouses = signal<WarehouseDto[]>([]);
  isLoading = signal(false);

  selectedWarehouseId = '';
  fromDate = new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0];
  toDate = new Date().toISOString().split('T')[0];
  includeZeroBalance = false;
  includeExpiredBatches = true;
  searchTerm = '';

  filteredRows = computed(() => {
    const report = this.data();
    if (!report || !report.rows) return [];
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return report.rows;
    return report.rows.filter(
      r =>
        (r.itemName && r.itemName.toLowerCase().includes(term)) ||
        (r.batchNo && r.batchNo.toLowerCase().includes(term)) ||
        (r.warehouseName && r.warehouseName.toLowerCase().includes(term))
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

    this.isLoading.set(true);
    this.stockBalanceService
      .getBatchWiseBalance({
        companyId: companyId || null,
        warehouseId: this.selectedWarehouseId || null,
        fromDate: this.fromDate ? `${this.fromDate}T00:00:00Z` : null,
        toDate: this.toDate ? `${this.toDate}T23:59:59Z` : null,
        includeZeroBalance: this.includeZeroBalance,
        includeExpiredBatches: this.includeExpiredBatches,
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
    const rows = this.filteredRows();
    if (!rows || rows.length === 0) return;

    const exportData = rows.map(r => ({
      itemName: r.itemName || '',
      warehouseName: r.warehouseName || '',
      batchNo: r.batchNo || '',
      expiryDate: r.expiryDate || '',
      balance: r.balance ?? 0,
      valuationRate: r.valuationRate ?? 0,
      stockValue: r.stockValue ?? 0,
      reservedStockQty: r.reservedStockQty ?? 0,
      isExpired: r.isExpired ? 'Yes' : 'No',
    }));

    exportToCsv(
      `batch_wise_balance_${this.toDate || new Date().toISOString().split('T')[0]}.csv`,
      exportData,
      [
        'itemName',
        'warehouseName',
        'batchNo',
        'expiryDate',
        'balance',
        'valuationRate',
        'stockValue',
        'reservedStockQty',
        'isExpired',
      ]
    );
  }
}
