import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { StockAgeingService } from '../../../proxy/inventory/stock-ageing.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { ItemGroupService } from '../../../proxy/inventory/item-group.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import {
  StockAgeingReportDto,
  StockAgeingRowDto,
  StockAgeingBucketDefinitionDto
} from '../../../proxy/inventory/models';

@Component({
  selector: 'app-stock-ageing',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0"><i class="bi bi-clock-history me-2"></i>{{ 'StockAgeing' | abpLocalization }}</h5>
        <div class="d-flex gap-2">
          <div class="btn-group btn-group-sm" role="group">
            <button
              type="button"
              class="btn"
              [class.btn-primary]="displayMode === 'qty'"
              [class.btn-outline-primary]="displayMode !== 'qty'"
              (click)="displayMode = 'qty'"
            >
              Qty
            </button>
            <button
              type="button"
              class="btn"
              [class.btn-primary]="displayMode === 'value'"
              [class.btn-outline-primary]="displayMode !== 'value'"
              (click)="displayMode = 'value'"
            >
              Value
            </button>
          </div>
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()" [disabled]="!rows().length">
            <i class="bi bi-download me-1"></i>Export CSV
          </button>
          <button class="btn btn-sm btn-primary" (click)="loadData()" [disabled]="loading()">
            <i class="bi bi-arrow-clockwise me-1"></i>Refresh
          </button>
        </div>
      </div>
      <div class="card-body">
        <!-- Filters -->
        <div class="row mb-3 g-2">
          <div class="col-md-2">
            <label class="form-label small">{{ 'Warehouse' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="filterWarehouse" (change)="loadData()">
              <option value="">All Warehouses</option>
              @for (w of warehouses(); track w.id) {
                <option [value]="w.id">{{ w.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label small">{{ 'ItemGroup' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="filterItemGroup" (change)="loadData()">
              <option value="">All Item Groups</option>
              @for (g of itemGroups(); track g.id) {
                <option [value]="g.id">{{ g.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label small">As of Date</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="asOfDate" (change)="loadData()" />
          </div>
          <div class="col-md-2">
            <label class="form-label small">Age Ranges (days)</label>
            <input
              type="text"
              class="form-control form-control-sm"
              [(ngModel)]="ranges"
              placeholder="30, 60, 90, 120"
              (change)="loadData()"
            />
          </div>
          <div class="col-md-2">
            <label class="form-label small">Search Item</label>
            <input
              type="text"
              class="form-control form-control-sm"
              [(ngModel)]="filterText"
              placeholder="Code or name..."
              (keyup.enter)="loadData()"
            />
          </div>
          <div class="col-md-2 d-flex flex-column justify-content-end">
            <div class="form-check form-check-sm">
              <input
                type="checkbox"
                class="form-check-input"
                id="showWhWise"
                [(ngModel)]="showWarehouseWise"
                (change)="loadData()"
              />
              <label class="form-check-label small" for="showWhWise">Warehouse-wise</label>
            </div>
            <div class="form-check form-check-sm">
              <input
                type="checkbox"
                class="form-check-input"
                id="showZero"
                [(ngModel)]="includeZeroStock"
                (change)="loadData()"
              />
              <label class="form-check-label small" for="showZero">Zero Stock</label>
            </div>
          </div>
        </div>

        @if (loading()) {
          <div class="text-center py-4"><div class="spinner-border text-primary"></div></div>
        } @else {
          <!-- Summary row -->
          <div class="row g-3 mb-3">
            <div class="col-md-3">
              <div class="border rounded p-2 text-center">
                <div class="small text-muted">Total Items</div>
                <div class="fw-bold fs-5">{{ report()?.totalItems ?? 0 }}</div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-2 text-center">
                <div class="small text-muted">Total Stock Value</div>
                <div class="fw-bold fs-5">{{ report()?.totalStockValue ?? 0 | number:'1.2-2' }}</div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-2 text-center bg-warning bg-opacity-10">
                <div class="small text-muted">Aged &gt; 90 days</div>
                <div class="fw-bold fs-5 text-warning">{{ report()?.agedOver90Count ?? 0 }}</div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-2 text-center">
                <div class="small text-muted">Overall Avg Age (days)</div>
                <div class="fw-bold fs-5">{{ report()?.overallAverageAgeDays ?? 0 | number:'1.0-0' }}</div>
              </div>
            </div>
          </div>

          <!-- Table -->
          <div class="table-responsive">
            <table class="table table-sm table-hover align-middle">
              <thead class="table-light">
                <tr>
                  <th>{{ 'Item' | abpLocalization }}</th>
                  @if (showWarehouseWise) {
                    <th>{{ 'Warehouse' | abpLocalization }}</th>
                  }
                  <th class="text-end">Total Qty</th>
                  <th class="text-end">Valuation Rate</th>
                  <th class="text-end">Total Value</th>
                  @for (b of buckets(); track b.bucketIndex) {
                    <th class="text-end">{{ b.label }}</th>
                  }
                  <th class="text-end">Avg Age</th>
                  <th class="text-end">Oldest</th>
                  <th class="text-end">Newest</th>
                </tr>
              </thead>
              <tbody>
                @for (row of rows(); track row.itemId + (row.warehouseId ?? '')) {
                  <tr>
                    <td>
                      <div class="fw-medium small">{{ row.itemCode }}</div>
                      <div class="text-muted" style="font-size: 0.75rem;">{{ row.itemName }}</div>
                    </td>
                    @if (showWarehouseWise) {
                      <td class="small">{{ row.warehouseName ?? '—' }}</td>
                    }
                    <td class="text-end font-monospace">{{ row.totalQty | number:'1.2-2' }} <span class="text-muted small">{{ row.stockUom }}</span></td>
                    <td class="text-end font-monospace">{{ row.valuationRate | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace">{{ row.totalStockValue | number:'1.2-2' }}</td>
                    @for (b of row.buckets; track b.bucketIndex) {
                      <td class="text-end font-monospace" [class.fw-bold]="b.qty > 0">
                        @if (displayMode === 'qty') {
                          {{ b.qty | number:'1.2-2' }}
                        } @else {
                          {{ b.stockValue | number:'1.2-2' }}
                        }
                      </td>
                    }
                    <td class="text-end">
                      <span
                        class="badge"
                        [class.bg-success]="row.averageAgeDays <= 30"
                        [class.bg-warning]="row.averageAgeDays > 30 && row.averageAgeDays <= 90"
                        [class.bg-danger]="row.averageAgeDays > 90"
                      >
                        {{ row.averageAgeDays | number:'1.0-0' }}d
                      </span>
                    </td>
                    <td class="text-end small font-monospace">{{ row.oldestDays }}d</td>
                    <td class="text-end small font-monospace">{{ row.newestDays }}d</td>
                  </tr>
                } @empty {
                  <tr>
                    <td [attr.colspan]="showWarehouseWise ? (7 + buckets().length) : (6 + buckets().length)" class="text-center text-muted py-4">
                      {{ 'NoDataAvailable' | abpLocalization }}
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
export class StockAgeingComponent implements OnInit {
  private stockAgeingService = inject(StockAgeingService);
  private warehouseService = inject(WarehouseService);
  private itemGroupService = inject(ItemGroupService);
  private companyContext = inject(CompanyContextService);

  report = signal<StockAgeingReportDto | null>(null);
  rows = signal<StockAgeingRowDto[]>([]);
  buckets = signal<StockAgeingBucketDefinitionDto[]>([]);
  warehouses = signal<{ id: string; name: string }[]>([]);
  itemGroups = signal<{ id: string; name: string }[]>([]);
  loading = signal(true);

  displayMode: 'qty' | 'value' = 'qty';
  filterWarehouse = '';
  filterItemGroup = '';
  filterText = '';
  ranges = '30, 60, 90, 120';
  showWarehouseWise = true;
  includeZeroStock = false;
  asOfDate = new Date().toISOString().substring(0, 10);

  ngOnInit() {
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe({
      next: res => this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup).map((w: any) => ({ id: w.id, name: w.warehouseName ?? w.name ?? w.id })))
    });
    this.itemGroupService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe({
      next: res => this.itemGroups.set((res.items ?? []).map((g: any) => ({ id: g.id, name: g.name ?? g.id })))
    });
    this.loadData();
  }

  loadData() {
    this.loading.set(true);
    const companyId = this.companyContext.currentCompanyId();

    this.stockAgeingService.getReport({
      companyId: companyId ?? '00000000-0000-0000-0000-000000000000',
      warehouseId: this.filterWarehouse || null,
      itemGroupId: this.filterItemGroup || null,
      filterText: this.filterText || null,
      toDate: this.asOfDate ? new Date(this.asOfDate).toISOString() : null,
      ranges: this.ranges || '30, 60, 90, 120',
      showWarehouseWiseStock: this.showWarehouseWise,
      includeZeroStock: this.includeZeroStock,
    }).subscribe({
      next: (res) => {
        this.report.set(res);
        this.rows.set(res.rows ?? []);
        this.buckets.set(res.buckets ?? []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  exportCsv() {
    const r = this.rows();
    if (!r.length) return;

    const b = this.buckets();
    const rowsForExport = r.map(item => {
      const record: Record<string, any> = {
        'Item Code': item.itemCode,
        'Item Name': item.itemName,
        'Warehouse': item.warehouseName ?? '—',
        'Stock UOM': item.stockUom,
        'Total Qty': item.totalQty,
        'Valuation Rate': item.valuationRate,
        'Total Stock Value': item.totalStockValue,
      };

      for (const bucket of item.buckets) {
        record[`${bucket.label} (Qty)`] = bucket.qty;
        record[`${bucket.label} (Value)`] = bucket.stockValue;
      }

      record['Average Age (days)'] = item.averageAgeDays;
      record['Oldest Age (days)'] = item.oldestDays;
      record['Newest Age (days)'] = item.newestDays;

      return record;
    });

    const columns = Object.keys(rowsForExport[0]);
    exportToCsv('stock-ageing-report', rowsForExport, columns);
  }
}
