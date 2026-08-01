import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { StockBalanceService } from '../../../proxy/inventory/stock-balance.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { ItemService } from '../../../proxy/inventory/item.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

interface BatchBalanceRow {
  itemId: string;
  itemName: string;
  batchId: string;
  batchNo: string;
  warehouseId: string;
  warehouseName: string;
  balance: number;
  stockValue: number;
  expiryDate: string | null;
  isExpired: boolean;
  isDisabled: boolean;
}

interface BatchBalanceReport {
  rows: BatchBalanceRow[];
  totalBatches: number;
  totalQuantity: number;
  totalStockValue: number;
  expiredBatchCount: number;
}

@Component({
  standalone: true,
  selector: 'app-batch-wise-balance',
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <h4><i class="fa fa-cubes me-2"></i>{{ '::BatchWiseBalance' | abpLocalization }}</h4>
        @if (report()) {
          <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fa fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <!-- Filters -->
      <div class="card mb-3">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-3">
              <label class="form-label small">{{ '::Warehouse' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="warehouseId" (change)="loadReport()">
                <option value="">{{ '::All' | abpLocalization }}</option>
                @for (w of warehouses(); track w.id) {
                  <option [value]="w.id">{{ w.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label small">{{ '::Item' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="itemId" (change)="loadReport()">
                <option value="">{{ '::All' | abpLocalization }}</option>
                @for (item of items(); track item.id) {
                  <option [value]="item.id">{{ item.itemCode }} — {{ item.itemName }}</option>
                }
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small">{{ '::FromDate' | abpLocalization }}</label>
              <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" (change)="loadReport()">
            </div>
            <div class="col-md-2">
              <label class="form-label small">{{ '::ToDate' | abpLocalization }}</label>
              <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" (change)="loadReport()">
            </div>
            <div class="col-md-2">
              <div class="form-check mt-3">
                <input type="checkbox" class="form-check-input" id="includeZero" [(ngModel)]="includeZeroBalance" (change)="loadReport()">
                <label class="form-check-label small" for="includeZero">{{ '::IncludeZeroBalance' | abpLocalization }}</label>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- KPI Cards -->
      @if (report()) {
        <div class="row g-3 mb-3">
          <div class="col-md-3">
            <div class="card border-start border-primary border-3">
              <div class="card-body py-2">
                <div class="text-muted small">{{ '::TotalBatches' | abpLocalization }}</div>
                <div class="h5 mb-0">{{ report()!.totalBatches }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-start border-success border-3">
              <div class="card-body py-2">
                <div class="text-muted small">{{ '::TotalQuantity' | abpLocalization }}</div>
                <div class="h5 mb-0">{{ report()!.totalQuantity | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-start border-info border-3">
              <div class="card-body py-2">
                <div class="text-muted small">{{ '::TotalStockValue' | abpLocalization }}</div>
                <div class="h5 mb-0">{{ report()!.totalStockValue | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-start border-danger border-3">
              <div class="card-body py-2">
                <div class="text-muted small">{{ '::ExpiredBatches' | abpLocalization }}</div>
                <div class="h5 mb-0 text-danger">{{ report()!.expiredBatchCount }}</div>
              </div>
            </div>
          </div>
        </div>

        <!-- Results Table -->
        <div class="card">
          <div class="card-body p-0">
            <div class="table-responsive">
              <table class="table table-hover table-sm mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::Item' | abpLocalization }}</th>
                    <th>{{ '::BatchNo' | abpLocalization }}</th>
                    <th>{{ '::Warehouse' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Balance' | abpLocalization }}</th>
                    <th class="text-end">{{ '::StockValue' | abpLocalization }}</th>
                    <th>{{ '::ExpiryDate' | abpLocalization }}</th>
                    <th>{{ '::Status' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of report()!.rows; track row.batchId + row.warehouseId) {
                    <tr [class.table-danger]="row.isExpired" [class.table-warning]="row.isDisabled && !row.isExpired">
                      <td>{{ row.itemName }}</td>
                      <td><code>{{ row.batchNo }}</code></td>
                      <td>{{ row.warehouseName }}</td>
                      <td class="text-end fw-bold">{{ row.balance | number:'1.2-2' }}</td>
                      <td class="text-end">{{ row.stockValue | number:'1.2-2' }}</td>
                      <td>
                        @if (row.expiryDate) {
                          <span [class.text-danger]="row.isExpired">{{ row.expiryDate | date:'dd/MM/yyyy' }}</span>
                        } @else {
                          <span class="text-muted">—</span>
                        }
                      </td>
                      <td>
                        @if (row.isExpired) {
                          <span class="badge bg-danger">{{ '::Expired' | abpLocalization }}</span>
                        } @else if (row.isDisabled) {
                          <span class="badge bg-warning">{{ '::Disabled' | abpLocalization }}</span>
                        } @else {
                          <span class="badge bg-success">{{ '::Active' | abpLocalization }}</span>
                        }
                      </td>
                    </tr>
                  }
                  @if (report()!.rows.length === 0) {
                    <tr><td colspan="7" class="text-center text-muted py-4">{{ '::NoBatchDataFound' | abpLocalization }}</td></tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      }

      @if (loading()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x text-muted"></i></div>
      }
    </div>
  `
})
export class BatchWiseBalanceComponent implements OnInit {
  private stockBalanceService = inject(StockBalanceService);
  private warehouseService = inject(WarehouseService);
  private itemService = inject(ItemService);
  private companyContext = inject(CompanyContextService);

  report = signal<BatchBalanceReport | null>(null);
  loading = signal(false);
  warehouses = signal<{ id: string; name: string }[]>([]);
  items = signal<{ id: string; itemCode: string; itemName: string }[]>([]);

  warehouseId = '';
  itemId = '';
  fromDate = '';
  toDate = '';
  includeZeroBalance = false;

  ngOnInit(): void {
    this.loadWarehouses();
    this.loadItems();
    this.loadReport();
  }

  loadWarehouses(): void {
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any).subscribe({
      next: (r) => this.warehouses.set((r.items ?? []).map((w: any) => ({ id: w.id, name: w.name }))),
      error: () => {}
    });
  }

  loadItems(): void {
    this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any).subscribe({
      next: (r) => this.items.set((r.items ?? []).map((i: any) => ({ id: i.id, itemCode: i.itemCode, itemName: i.itemName }))),
      error: () => {}
    });
  }

  loadReport(): void {
    this.loading.set(true);
    const params: any = {};
    if (this.warehouseId) params.warehouseId = this.warehouseId;
    if (this.itemId) params.itemId = this.itemId;
    if (this.fromDate) params.fromDate = this.fromDate;
    if (this.toDate) params.toDate = this.toDate;
    if (this.includeZeroBalance) params.includeZeroBalance = 'true';

    this.stockBalanceService.getBatchWiseBalance(params as any).subscribe({
      next: (data: any) => { this.report.set(data); this.loading.set(false); },
      error: () => { this.loading.set(false); }
    });
  }

  exportCsv(): void {
    const r = this.report();
    if (!r) return;
    const mapped = r.rows.map(row => ({
      Item: row.itemName,
      'Batch No': row.batchNo,
      Warehouse: row.warehouseName,
      'Balance Qty': row.balance,
      'Stock Value': row.stockValue,
      'Expiry Date': row.expiryDate ?? '',
      Expired: row.isExpired ? 'Yes' : 'No',
      Disabled: row.isDisabled ? 'Yes' : 'No',
    }));
    exportToCsv('batch-wise-balance.csv', mapped, ['Item', 'Batch No', 'Warehouse', 'Balance Qty', 'Stock Value', 'Expiry Date', 'Expired', 'Disabled']);
  }
}
