import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { BatchWiseBalanceHistoryService } from '../../../proxy/inventory/batch-wise-balance-history.service';
import type {
  BatchWiseBalanceHistoryReportDto,
  BatchWiseBalanceHistoryRowDto,
  ItemDto,
  WarehouseDto,
} from '../../../proxy/inventory/models';
import { ItemService } from '../../../proxy/inventory/item.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-batch-wise-balance-history',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-history me-2 text-primary"></i>
            {{ '::BatchWiseBalanceHistory' | abpLocalization }}
          </h5>
          <small class="text-muted">
            {{ '::BatchWiseBalanceHistoryDescription' | abpLocalization }}
          </small>
        </div>
        <div>
          @if (filteredRows().length > 0) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
      </div>

      <div class="card-body">
        <!-- Filters Row 1 -->
        <div class="row g-2 mb-3 align-items-end">
          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Warehouse' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedWarehouseId">
              <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
              @for (wh of warehouses(); track wh.id) {
                <option [value]="wh.id">{{ wh.name }}</option>
              }
            </select>
          </div>

          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Item' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedItemId">
              <option value="">{{ '::AllItems' | abpLocalization }}</option>
              @for (item of items(); track item.id) {
                <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
              }
            </select>
          </div>

          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::FromDate' | abpLocalization }}</label>
            <input
              type="date"
              class="form-control form-control-sm"
              [(ngModel)]="fromDate"
            />
          </div>

          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::ToDate' | abpLocalization }}</label>
            <input
              type="date"
              class="form-control form-control-sm"
              [(ngModel)]="toDate"
            />
          </div>

          <div class="col-md-2 col-sm-12">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Apply' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Filters Row 2 -->
        <div class="row g-2 mb-4 align-items-center">
          <div class="col-md-6 col-sm-12">
            <label class="form-label small fw-semibold">{{ '::Search' | abpLocalization }}</label>
            <input
              type="text"
              class="form-control form-control-sm"
              [placeholder]="'Search item, batch number, warehouse, description...'"
              [(ngModel)]="searchTerm"
            />
          </div>

          <div class="col-md-6 col-sm-12 pt-3">
            <div class="form-check form-switch">
              <input
                class="form-check-input"
                type="checkbox"
                id="includeZeroStockBatchesSwitch"
                [(ngModel)]="includeZeroStockBatches"
                (change)="loadData()"
              />
              <label class="form-check-label small" for="includeZeroStockBatchesSwitch">
                {{ '::IncludeZeroStockItems' | abpLocalization }}
              </label>
            </div>
          </div>
        </div>

        <!-- Summary KPIs -->
        @if (report(); as rep) {
          <div class="row g-3 mb-4">
            <div class="col-md-2 col-sm-4 col-6">
              <div class="card bg-light border-0 shadow-none">
                <div class="card-body p-3 text-center">
                  <div class="text-muted small mb-1">{{ '::Items' | abpLocalization }}</div>
                  <h5 class="mb-0 text-primary">{{ rep.totalItemsCount | number }}</h5>
                </div>
              </div>
            </div>
            <div class="col-md-2 col-sm-4 col-6">
              <div class="card bg-light border-0 shadow-none">
                <div class="card-body p-3 text-center">
                  <div class="text-muted small mb-1">{{ '::Batches' | abpLocalization }}</div>
                  <h5 class="mb-0 text-info">{{ rep.totalBatchesCount | number }}</h5>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-4 col-6">
              <div class="card bg-light border-0 shadow-none">
                <div class="card-body p-3 text-center">
                  <div class="text-muted small mb-1">{{ '::TotalBalanceQty' | abpLocalization }}</div>
                  <h5 class="mb-0 text-success">{{ rep.totalBalanceQty | number: '1.2-2' }}</h5>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6 col-6">
              <div class="card bg-light border-0 shadow-none">
                <div class="card-body p-3 text-center">
                  <div class="text-muted small mb-1">{{ '::TotalBalanceValue' | abpLocalization }}</div>
                  <h5 class="mb-0 text-dark">{{ rep.totalBalanceValue | number: '1.2-2' }}</h5>
                </div>
              </div>
            </div>
            <div class="col-md-2 col-sm-6 col-12">
              <div class="card bg-light border-0 shadow-none">
                <div class="card-body p-3 text-center">
                  <div class="text-muted small mb-1">{{ '::ReservedQty' | abpLocalization }}</div>
                  <h5 class="mb-0 text-warning">{{ rep.totalReservedQty | number: '1.2-2' }}</h5>
                </div>
              </div>
            </div>
          </div>
        }

        <!-- Results Table -->
        <div class="table-responsive">
          <table class="table table-sm table-hover align-middle">
            <thead class="table-light text-nowrap">
              <tr>
                <th>{{ '::Item' | abpLocalization }}</th>
                <th>{{ '::Warehouse' | abpLocalization }}</th>
                <th>{{ '::Batch' | abpLocalization }}</th>
                <th>{{ '::StockUom' | abpLocalization }}</th>
                <th class="text-end">{{ '::OpeningQty' | abpLocalization }}</th>
                <th class="text-end">{{ '::InQty' | abpLocalization }}</th>
                <th class="text-end">{{ '::OutQty' | abpLocalization }}</th>
                <th class="text-end">{{ '::BalanceQty' | abpLocalization }}</th>
                <th class="text-end">{{ '::ValuationRate' | abpLocalization }}</th>
                <th class="text-end">{{ '::BalanceValue' | abpLocalization }}</th>
                <th class="text-end">{{ '::ReservedQty' | abpLocalization }}</th>
                <th class="text-end">{{ '::AvailableQty' | abpLocalization }}</th>
              </tr>
            </thead>
            <tbody>
              @if (isLoading()) {
                <tr>
                  <td colspan="12" class="text-center py-4">
                    <i class="fa fa-spinner fa-spin fa-2x text-primary"></i>
                    <div class="mt-2 text-muted small">{{ '::Loading' | abpLocalization }}...</div>
                  </td>
                </tr>
              } @else if (filteredRows().length === 0) {
                <tr>
                  <td colspan="12" class="text-center py-4 text-muted">
                    <i class="fa fa-info-circle me-1"></i>
                    {{ '::NoBatchWiseBalanceRecordsFound' | abpLocalization }}
                  </td>
                </tr>
              } @else {
                @for (row of filteredRows(); track row.rowId) {
                  <tr>
                    <td>
                      <div class="fw-semibold text-primary">{{ row.itemCode }}</div>
                      <div class="text-muted small">{{ row.itemName }}</div>
                    </td>
                    <td>
                      <span class="text-dark small">{{ row.warehouseName }}</span>
                    </td>
                    <td>
                      <span class="badge bg-secondary-subtle text-secondary border font-monospace">
                        <i class="fa fa-layer-group me-1"></i>{{ row.batchNo }}
                      </span>
                    </td>
                    <td>
                      <small class="text-muted">{{ row.stockUom }}</small>
                    </td>
                    <td class="text-end font-monospace small">
                      {{ row.openingQty | number: '1.2-2' }}
                    </td>
                    <td class="text-end font-monospace small text-success">
                      {{ row.inQty > 0 ? '+' : '' }}{{ row.inQty | number: '1.2-2' }}
                    </td>
                    <td class="text-end font-monospace small text-danger">
                      {{ row.outQty > 0 ? '-' : '' }}{{ row.outQty | number: '1.2-2' }}
                    </td>
                    <td class="text-end font-monospace small fw-bold" [class.text-danger]="row.balanceQty < 0">
                      {{ row.balanceQty | number: '1.2-2' }}
                    </td>
                    <td class="text-end font-monospace small">
                      {{ row.valuationRate | number: '1.2-2' }}
                    </td>
                    <td class="text-end font-monospace small">
                      {{ row.balanceValue | number: '1.2-2' }}
                    </td>
                    <td class="text-end font-monospace small">
                      @if (row.reservedQty > 0) {
                        <span class="badge bg-warning-subtle text-warning-emphasis">
                          {{ row.reservedQty | number: '1.2-2' }}
                        </span>
                      } @else {
                        <span class="text-muted">0</span>
                      }
                    </td>
                    <td class="text-end font-monospace small">
                      <span class="badge bg-success-subtle text-success">
                        {{ row.availableQty | number: '1.2-2' }}
                      </span>
                    </td>
                  </tr>
                }
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `,
})
export class BatchWiseBalanceHistoryComponent implements OnInit {
  private service = inject(BatchWiseBalanceHistoryService);
  private companyContext = inject(CompanyContextService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);
  private toaster = inject(ToasterService);

  report = signal<BatchWiseBalanceHistoryReportDto | null>(null);
  isLoading = signal<boolean>(false);
  warehouses = signal<WarehouseDto[]>([]);
  items = signal<ItemDto[]>([]);

  selectedWarehouseId = '';
  selectedItemId = '';
  fromDate = '';
  toDate = '';
  includeZeroStockBatches = false;
  searchTerm = '';

  filteredRows = computed(() => {
    const rep = this.report();
    if (!rep) return [];
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return rep.rows;

    return rep.rows.filter(
      r =>
        r.itemCode.toLowerCase().includes(term) ||
        r.itemName.toLowerCase().includes(term) ||
        r.warehouseName.toLowerCase().includes(term) ||
        r.batchNo.toLowerCase().includes(term) ||
        (r.description && r.description.toLowerCase().includes(term))
    );
  });

  ngOnInit(): void {
    this.loadMasters();
    this.loadData();
  }

  loadMasters(): void {
    const companyId = this.companyContext.selectedCompanyId();
    this.itemService.getList({ maxResultCount: 1000 }).subscribe({
      next: res => this.items.set(res.items || []),
    });

    this.warehouseService.getList({ maxResultCount: 500 }).subscribe({
      next: res => {
        let whs = res.items || [];
        if (companyId) {
          whs = whs.filter(w => w.companyId === companyId);
        }
        this.warehouses.set(whs);
      },
    });
  }

  loadData(): void {
    this.isLoading.set(true);
    const companyId = this.companyContext.selectedCompanyId();

    this.service
      .getReport({
        companyId: companyId || undefined,
        warehouseId: this.selectedWarehouseId || undefined,
        itemId: this.selectedItemId || undefined,
        fromDate: this.fromDate ? this.fromDate : undefined,
        toDate: this.toDate ? this.toDate : undefined,
        includeZeroStockBatches: this.includeZeroStockBatches,
        searchTerm: this.searchTerm ? this.searchTerm : undefined,
      })
      .subscribe({
        next: res => {
          this.report.set(res);
          this.isLoading.set(false);
        },
        error: err => {
          this.toaster.error(err.message || 'Failed to load batch balance history');
          this.isLoading.set(false);
        },
      });
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (!rows.length) return;

    const exportData = rows.map(r => ({
      ItemCode: r.itemCode,
      ItemName: r.itemName,
      Description: r.description || '',
      Warehouse: r.warehouseName,
      BatchNo: r.batchNo,
      StockUom: r.stockUom,
      OpeningQty: r.openingQty,
      InQty: r.inQty,
      OutQty: r.outQty,
      BalanceQty: r.balanceQty,
      ValuationRate: r.valuationRate,
      BalanceValue: r.balanceValue,
      ReservedQty: r.reservedQty,
      AvailableQty: r.availableQty,
    }));

    exportToCsv(
      `batch-wise-balance-history-${new Date().toISOString().slice(0, 10)}.csv`,
      exportData,
      [
        'ItemCode',
        'ItemName',
        'Description',
        'Warehouse',
        'BatchNo',
        'StockUom',
        'OpeningQty',
        'InQty',
        'OutQty',
        'BalanceQty',
        'ValuationRate',
        'BalanceValue',
        'ReservedQty',
        'AvailableQty',
      ]
    );
  }
}
