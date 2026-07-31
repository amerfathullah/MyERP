import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../../shared/components/breadcrumb/breadcrumb.component';
import { CompanyContextService } from '../../../shared/services/company-context.service';

interface StockBalanceRow {
  itemId: string;
  itemCode: string;
  itemName: string;
  itemGroup: string;
  warehouseId: string;
  warehouseName: string;
  uom: string;
  actualQty: number;
  reservedQty: number;
  orderedQty: number;
  plannedQty: number;
  projectedQty: number;
  valuationRate: number;
  stockValue: number;
}

/**
 * Stock Balance Report — the most used inventory report.
 * Shows warehouse-wise quantity and value for all items.
 * 
 * Per ERPNext: stock_balance.py report with 20+ filters.
 * Features:
 * - Filter by warehouse, item group, item code
 * - Group by warehouse or item
 * - Show valuation rate and stock value
 * - Export to CSV
 * - Drill-down to stock ledger
 * - Include zero-stock items toggle
 * - Projected qty (actual - reserved + ordered + planned)
 */
@Component({
  selector: 'app-stock-balance-report',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe, BreadcrumbComponent, RouterLink],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-boxes-stacked me-2"></i>{{ 'Inventory:StockBalance' | abpLocalization }}</h5>
          <div class="d-flex gap-2">
            <button class="btn btn-outline-secondary btn-sm" (click)="exportCsv()" [disabled]="!data().length">
              <i class="fas fa-download me-1"></i>Export CSV
            </button>
            <button class="btn btn-primary btn-sm" (click)="loadReport()" [disabled]="loading()">
              <i class="fas fa-sync me-1"></i>{{ '::Refresh' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Filters -->
        <div class="card-body border-bottom bg-light">
          <div class="row g-2 align-items-end">
            <div class="col-md-3">
              <label class="form-label small">{{ '::Warehouse' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="filterWarehouseId">
                <option value="">All Warehouses</option>
                @for (wh of warehouses(); track wh.id) {
                  <option [value]="wh.id">{{ wh.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label small">{{ '::ItemGroup' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="filterItemGroup">
                <option value="">All Groups</option>
                @for (g of itemGroups(); track g) {
                  <option [value]="g">{{ g }}</option>
                }
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label small">{{ '::Search' | abpLocalization }}</label>
              <input class="form-control form-control-sm" [(ngModel)]="filterText" [placeholder]="'::Placeholder:ItemCodeOrName' | abpLocalization" (keyup.enter)="loadReport()" />
            </div>
            <div class="col-md-3 d-flex align-items-end gap-2">
              <div class="form-check">
                <input class="form-check-input" type="checkbox" id="showZero" [(ngModel)]="includeZeroStock" />
                <label class="form-check-label small" for="showZero">Show Zero Stock</label>
              </div>
            </div>
          </div>
        </div>

        <!-- Summary Cards -->
        @if (data().length > 0) {
          <div class="card-body border-bottom">
            <div class="row text-center">
              <div class="col-md-3">
                <div class="fw-bold text-primary fs-5">{{ totalItems() }}</div>
                <small class="text-muted">Items</small>
              </div>
              <div class="col-md-3">
                <div class="fw-bold text-success fs-5">{{ totalQty() | number:'1.0-0' }}</div>
                <small class="text-muted">Total Qty</small>
              </div>
              <div class="col-md-3">
                <div class="fw-bold text-info fs-5">{{ totalValue() | number:'1.0-0' }}</div>
                <small class="text-muted">Total Value</small>
              </div>
              <div class="col-md-3">
                <div class="fw-bold text-warning fs-5">{{ negativeStockCount() }}</div>
                <small class="text-muted">Negative Stock</small>
              </div>
            </div>
          </div>
        }

        <!-- Data Table -->
        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
          } @else if (data().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fas fa-box-open fa-3x mb-3 d-block opacity-50"></i>
              <p>No stock balance data. Click Refresh to load.</p>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-sm table-hover mb-0">
                <thead class="table-dark sticky-top">
                  <tr>
                    <th class="sortable" (click)="sort('itemCode')">
                      {{ '::ItemCode' | abpLocalization }}
                      @if (sortField === 'itemCode') { <i class="fas fa-sort-{{ sortDir === 'asc' ? 'up' : 'down' }} ms-1"></i> }
                    </th>
                    <th>{{ '::ItemName' | abpLocalization }}</th>
                    <th>{{ '::Warehouse' | abpLocalization }}</th>
                    <th>{{ '::UOM' | abpLocalization }}</th>
                    <th class="text-end sortable" (click)="sort('actualQty')">
                      {{ '::ActualQty' | abpLocalization }}
                      @if (sortField === 'actualQty') { <i class="fas fa-sort-{{ sortDir === 'asc' ? 'up' : 'down' }} ms-1"></i> }
                    </th>
                    <th class="text-end">{{ '::Reserved' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Ordered' | abpLocalization }}</th>
                    <th class="text-end sortable" (click)="sort('projectedQty')">
                      {{ '::Projected' | abpLocalization }}
                      @if (sortField === 'projectedQty') { <i class="fas fa-sort-{{ sortDir === 'asc' ? 'up' : 'down' }} ms-1"></i> }
                    </th>
                    <th class="text-end">{{ '::Rate' | abpLocalization }}</th>
                    <th class="text-end sortable" (click)="sort('stockValue')">
                      {{ '::Value' | abpLocalization }}
                      @if (sortField === 'stockValue') { <i class="fas fa-sort-{{ sortDir === 'asc' ? 'up' : 'down' }} ms-1"></i> }
                    </th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of filteredData(); track row.itemId + row.warehouseId) {
                    <tr [class.table-danger]="row.actualQty < 0">
                      <td class="font-monospace small">
                        <a [routerLink]="['/inventory/items', row.itemId]" class="text-primary">{{ row.itemCode }}</a>
                      </td>
                      <td>{{ row.itemName }}</td>
                      <td>
                        <span class="badge bg-light text-dark">{{ row.warehouseName }}</span>
                      </td>
                      <td>{{ row.uom }}</td>
                      <td class="text-end fw-bold" [class.text-danger]="row.actualQty < 0" [class.text-success]="row.actualQty > 0">
                        {{ row.actualQty | number:'1.0-2' }}
                      </td>
                      <td class="text-end text-muted">{{ row.reservedQty | number:'1.0-2' }}</td>
                      <td class="text-end text-info">{{ row.orderedQty | number:'1.0-2' }}</td>
                      <td class="text-end" [class.text-danger]="row.projectedQty < 0">
                        {{ row.projectedQty | number:'1.0-2' }}
                      </td>
                      <td class="text-end">{{ row.valuationRate | number:'1.2-4' }}</td>
                      <td class="text-end fw-medium">{{ row.stockValue | number:'1.0-0' }}</td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-light fw-bold">
                  <tr>
                    <td colspan="4">Total</td>
                    <td class="text-end">{{ totalQty() | number:'1.0-2' }}</td>
                    <td class="text-end">{{ totalReserved() | number:'1.0-2' }}</td>
                    <td class="text-end">{{ totalOrdered() | number:'1.0-2' }}</td>
                    <td class="text-end">{{ totalProjected() | number:'1.0-2' }}</td>
                    <td></td>
                    <td class="text-end">{{ totalValue() | number:'1.0-0' }}</td>
                  </tr>
                </tfoot>
              </table>
            </div>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .sortable { cursor: pointer; user-select: none; }
    .sortable:hover { background: rgba(255,255,255,0.1); }
    .sticky-top { position: sticky; top: 0; z-index: 1; }
  `]
})
export class StockBalanceReportComponent implements OnInit {
  private http = inject(HttpClient);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  data = signal<StockBalanceRow[]>([]);
  warehouses = signal<{ id: string; name: string }[]>([]);
  itemGroups = signal<string[]>([]);
  loading = signal(false);

  filterWarehouseId = '';
  filterItemGroup = '';
  filterText = '';
  includeZeroStock = false;
  sortField = 'itemCode';
  sortDir: 'asc' | 'desc' = 'asc';

  // Computed summaries
  totalItems = computed(() => new Set(this.filteredData().map(r => r.itemId)).size);
  totalQty = computed(() => this.filteredData().reduce((s, r) => s + r.actualQty, 0));
  totalReserved = computed(() => this.filteredData().reduce((s, r) => s + r.reservedQty, 0));
  totalOrdered = computed(() => this.filteredData().reduce((s, r) => s + r.orderedQty, 0));
  totalProjected = computed(() => this.filteredData().reduce((s, r) => s + r.projectedQty, 0));
  totalValue = computed(() => this.filteredData().reduce((s, r) => s + r.stockValue, 0));
  negativeStockCount = computed(() => this.filteredData().filter(r => r.actualQty < 0).length);

  filteredData = computed(() => {
    let rows = this.data();
    if (!this.includeZeroStock) rows = rows.filter(r => r.actualQty !== 0 || r.reservedQty !== 0 || r.orderedQty !== 0);
    if (this.filterItemGroup) rows = rows.filter(r => r.itemGroup === this.filterItemGroup);
    if (this.filterText) {
      const txt = this.filterText.toLowerCase();
      rows = rows.filter(r => r.itemCode.toLowerCase().includes(txt) || r.itemName.toLowerCase().includes(txt));
    }
    // Sort
    rows = [...rows].sort((a, b) => {
      const key = this.sortField as keyof StockBalanceRow;
      const av = a[key], bv = b[key];
      const cmp = typeof av === 'number' ? (av as number) - (bv as number) : String(av).localeCompare(String(bv));
      return this.sortDir === 'asc' ? cmp : -cmp;
    });
    return rows;
  });

  ngOnInit(): void {
    this.loadWarehouses();
    this.loadReport();

    // Pre-filter by warehouse if query param provided
    const whId = this.route.snapshot.queryParamMap.get('warehouseId');
    if (whId) this.filterWarehouseId = whId;
  }

  loadWarehouses(): void {
    this.http.get<any>('/api/app/warehouse', { params: { skipCount: '0', maxResultCount: '500' } })
      .subscribe({ next: res => this.warehouses.set((res.items ?? []).map((w: any) => ({ id: w.id, name: w.name }))), error: () => {} });
  }

  loadReport(): void {
    this.loading.set(true);
    const params: any = { skipCount: '0', maxResultCount: '5000' };
    if (this.filterWarehouseId) params.warehouseId = this.filterWarehouseId;
    const companyId = this.companyContext.currentCompanyId();
    if (companyId) params.companyId = companyId;

    this.http.get<any>('/api/app/stock-balance/stock-balance', { params }).subscribe({
      next: res => {
        const items: StockBalanceRow[] = (res.items ?? []).map((r: any) => ({
          itemId: r.itemId,
          itemCode: r.itemCode ?? '',
          itemName: r.itemName ?? '',
          itemGroup: r.itemGroup ?? '',
          warehouseId: r.warehouseId,
          warehouseName: r.warehouseName ?? '',
          uom: r.uom ?? 'Unit',
          actualQty: r.actualQty ?? 0,
          reservedQty: r.reservedQty ?? 0,
          orderedQty: r.orderedQty ?? 0,
          plannedQty: r.plannedQty ?? 0,
          projectedQty: r.projectedQty ?? 0,
          valuationRate: r.valuationRate ?? 0,
          stockValue: r.stockValue ?? 0,
        }));
        this.data.set(items);

        // Extract unique item groups for filter dropdown
        const groups = [...new Set(items.map(i => i.itemGroup).filter(g => !!g))].sort();
        this.itemGroups.set(groups);

        this.loading.set(false);
      },
      error: () => { this.loading.set(false); this.toaster.error('::FailedToLoad'); }
    });
  }

  sort(field: string): void {
    if (this.sortField === field) {
      this.sortDir = this.sortDir === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortField = field;
      this.sortDir = 'asc';
    }
    // Trigger reactivity
    this.data.set([...this.data()]);
  }

  exportCsv(): void {
    const rows = this.filteredData();
    if (!rows.length) return;
    const headers = ['Item Code', 'Item Name', 'Warehouse', 'UOM', 'Actual Qty', 'Reserved', 'Ordered', 'Projected', 'Rate', 'Value'];
    const csvRows = [headers.join(',')];
    for (const r of rows) {
      csvRows.push([
        `"${r.itemCode}"`, `"${r.itemName}"`, `"${r.warehouseName}"`, r.uom,
        r.actualQty, r.reservedQty, r.orderedQty, r.projectedQty,
        r.valuationRate, r.stockValue
      ].join(','));
    }
    const blob = new Blob([csvRows.join('\n')], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `stock-balance-${new Date().toISOString().split('T')[0]}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }
}
