import { Component, inject, OnInit, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { StockBalanceService } from '../../../proxy/inventory/stock-balance.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../../shared/components/breadcrumb/breadcrumb.component';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import type { StockBalanceReportDto, StockBalanceReportRowDto } from '../../../proxy/inventory/models';

export function filterMovementRows(
  rows: StockBalanceReportRowDto[] | undefined,
  searchText: string,
): StockBalanceReportRowDto[] {
  const list = rows ?? [];
  const term = searchText.trim().toLowerCase();
  if (!term) return list;
  return list.filter(
    r =>
      (r.itemCode ?? '').toLowerCase().includes(term) ||
      (r.itemName ?? '').toLowerCase().includes(term) ||
      (r.batchNo ?? '').toLowerCase().includes(term),
  );
}

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
        <div class="card-header d-flex justify-content-between align-items-center flex-wrap gap-2">
          <h5 class="mb-0"><i class="fas fa-boxes-stacked me-2"></i>{{ '::Inventory:StockBalance' | abpLocalization }}</h5>
          <div class="d-flex gap-2 align-items-center">
            <div class="btn-group btn-group-sm">
              <button type="button" class="btn"
                      [class.btn-primary]="reportMode === 'snapshot'"
                      [class.btn-outline-primary]="reportMode !== 'snapshot'"
                      (click)="switchMode('snapshot')">
                {{ '::CurrentSnapshot' | abpLocalization }}
              </button>
              <button type="button" class="btn"
                      [class.btn-primary]="reportMode === 'movement'"
                      [class.btn-outline-primary]="reportMode !== 'movement'"
                      (click)="switchMode('movement')">
                {{ '::PeriodMovement' | abpLocalization }}
              </button>
            </div>
            <button class="btn btn-outline-secondary btn-sm" (click)="exportCsv()" [disabled]="reportMode === 'snapshot' ? !data().length : !filteredMovementData().length">
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
            @if (reportMode === 'snapshot') {
              <div class="col-md-3">
                <label class="form-label small">{{ '::ItemGroup' | abpLocalization }}</label>
                <select class="form-select form-select-sm" [(ngModel)]="filterItemGroup">
                  <option value="">All Groups</option>
                  @for (g of itemGroups(); track g) {
                    <option [value]="g">{{ g }}</option>
                  }
                </select>
              </div>
            } @else {
              <div class="col-md-2">
                <label class="form-label small">{{ '::FromDate' | abpLocalization }}</label>
                <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" />
              </div>
              <div class="col-md-2">
                <label class="form-label small">{{ '::ToDate' | abpLocalization }}</label>
                <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" />
              </div>
            }
            <div class="col-md-3">
              <label class="form-label small">{{ '::Search' | abpLocalization }}</label>
              <input class="form-control form-control-sm" [(ngModel)]="filterText" [placeholder]="'::Placeholder:ItemCodeOrName' | abpLocalization" (keyup.enter)="loadReport()" />
            </div>
            <div class="col-md-3 d-flex align-items-end gap-3 flex-wrap">
              <div class="form-check">
                <input class="form-check-input" type="checkbox" id="showZero" [(ngModel)]="includeZeroStock" />
                <label class="form-check-label small" for="showZero">Show Zero Stock</label>
              </div>
              @if (reportMode === 'movement') {
                <div class="form-check">
                  <input class="form-check-input" type="checkbox" id="showDimWise" [(ngModel)]="showDimensionWiseStock" />
                  <label class="form-check-label small" for="showDimWise">{{ '::BatchWise' | abpLocalization }}</label>
                </div>
              }
            </div>
          </div>
        </div>

        <!-- Summary Cards -->
        @if (reportMode === 'snapshot' && data().length > 0) {
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
        @if (reportMode === 'movement' && movementReport(); as mr) {
          <div class="card-body border-bottom">
            <div class="row text-center">
              <div class="col-md">
                <div class="fw-bold text-secondary fs-5">{{ (mr.totalOpeningQty ?? 0) | number:'1.0-2' }}</div>
                <small class="text-muted">{{ '::OpeningQty' | abpLocalization }}</small>
              </div>
              <div class="col-md">
                <div class="fw-bold text-success fs-5">{{ (mr.totalInQty ?? 0) | number:'1.0-2' }}</div>
                <small class="text-muted">{{ '::InQty' | abpLocalization }}</small>
              </div>
              <div class="col-md">
                <div class="fw-bold text-danger fs-5">{{ (mr.totalOutQty ?? 0) | number:'1.0-2' }}</div>
                <small class="text-muted">{{ '::OutQty' | abpLocalization }}</small>
              </div>
              <div class="col-md">
                <div class="fw-bold text-primary fs-5">{{ (mr.totalBalanceQty ?? 0) | number:'1.0-2' }}</div>
                <small class="text-muted">{{ '::BalanceQty' | abpLocalization }}</small>
              </div>
              <div class="col-md">
                <div class="fw-bold text-info fs-5">{{ (mr.totalBalanceValue ?? 0) | number:'1.2-2' }}</div>
                <small class="text-muted">{{ '::BalanceValue' | abpLocalization }}</small>
              </div>
            </div>
          </div>
        }

        <!-- Data Table -->
        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
          } @else if (reportMode === 'movement') {
            @if (filteredMovementData().length === 0) {
              <div class="text-center py-5 text-muted">
                <i class="fas fa-box-open fa-3x mb-3 d-block opacity-50"></i>
                <p>No stock movement data. Click Refresh to load.</p>
              </div>
            } @else {
              <div class="table-responsive">
                <table class="table table-sm table-hover mb-0">
                  <thead class="table-dark sticky-top">
                    <tr>
                      <th>{{ '::ItemCode' | abpLocalization }}</th>
                      <th>{{ '::ItemName' | abpLocalization }}</th>
                      <th>{{ '::Warehouse' | abpLocalization }}</th>
                      @if (showDimensionWiseStock) {
                        <th>{{ '::BatchNo' | abpLocalization }}</th>
                      }
                      <th class="text-end">{{ '::OpeningQty' | abpLocalization }}</th>
                      <th class="text-end">{{ '::OpeningValue' | abpLocalization }}</th>
                      <th class="text-end">{{ '::InQty' | abpLocalization }}</th>
                      <th class="text-end">{{ '::InValue' | abpLocalization }}</th>
                      <th class="text-end">{{ '::OutQty' | abpLocalization }}</th>
                      <th class="text-end">{{ '::OutValue' | abpLocalization }}</th>
                      <th class="text-end">{{ '::BalanceQty' | abpLocalization }}</th>
                      <th class="text-end">{{ '::BalanceValue' | abpLocalization }}</th>
                      <th class="text-end">{{ '::ValuationRate' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (row of filteredMovementData(); track (row.itemId ?? '') + (row.warehouseId ?? '') + (row.batchId ?? '')) {
                      <tr [class.table-danger]="(row.balanceQty ?? 0) < 0">
                        <td class="font-monospace small">
                          <a [routerLink]="['/inventory/items', row.itemId]" class="text-primary">{{ row.itemCode }}</a>
                        </td>
                        <td>{{ row.itemName }}</td>
                        <td><span class="badge bg-light text-dark">{{ row.warehouseName }}</span></td>
                        @if (showDimensionWiseStock) {
                          <td class="font-monospace small">{{ row.batchNo || '-' }}</td>
                        }
                        <td class="text-end font-monospace">{{ (row.openingQty ?? 0) | number:'1.0-2' }}</td>
                        <td class="text-end font-monospace">{{ (row.openingValue ?? 0) | number:'1.2-2' }}</td>
                        <td class="text-end font-monospace text-success">{{ (row.inQty ?? 0) | number:'1.0-2' }}</td>
                        <td class="text-end font-monospace text-success">{{ (row.inValue ?? 0) | number:'1.2-2' }}</td>
                        <td class="text-end font-monospace text-danger">{{ (row.outQty ?? 0) | number:'1.0-2' }}</td>
                        <td class="text-end font-monospace text-danger">{{ (row.outValue ?? 0) | number:'1.2-2' }}</td>
                        <td class="text-end font-monospace fw-bold">{{ (row.balanceQty ?? 0) | number:'1.0-2' }}</td>
                        <td class="text-end font-monospace fw-semibold">{{ (row.balanceValue ?? 0) | number:'1.2-2' }}</td>
                        <td class="text-end font-monospace">{{ (row.valuationRate ?? 0) | number:'1.2-4' }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            }
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
  changeDetection: ChangeDetectionStrategy.Eager,
  styles: [`
    .sortable { cursor: pointer; user-select: none; }
    .sortable:hover { background: rgba(255,255,255,0.1); }
    .sticky-top { position: sticky; top: 0; z-index: 1; }
  `]
})
export class StockBalanceReportComponent implements OnInit {
  private stockBalanceService = inject(StockBalanceService);
  private warehouseService = inject(WarehouseService);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  data = signal<StockBalanceRow[]>([]);
  movementReport = signal<StockBalanceReportDto | null>(null);
  warehouses = signal<{ id: string; name: string }[]>([]);
  itemGroups = signal<string[]>([]);
  loading = signal(false);

  reportMode: 'snapshot' | 'movement' = 'snapshot';
  fromDate = new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0];
  toDate = new Date().toISOString().split('T')[0];
  showDimensionWiseStock = false;
  filterWarehouseId = '';
  filterItemGroup = '';
  filterText = '';
  includeZeroStock = false;
  sortField = 'itemCode';
  sortDir: 'asc' | 'desc' = 'asc';

  filteredMovementData = computed(() => filterMovementRows(this.movementReport()?.rows, this.filterText));

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
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any)
      .subscribe({ next: res => this.warehouses.set((res.items ?? []).map((w: any) => ({ id: w.id, name: w.name }))), error: () => {} });
  }

  switchMode(mode: 'snapshot' | 'movement'): void {
    this.reportMode = mode;
    this.loadReport();
  }

  loadReport(): void {
    this.loading.set(true);
    const companyId = this.companyContext.currentCompanyId();

    if (this.reportMode === 'movement') {
      this.stockBalanceService.getStockBalanceReport({
        companyId: companyId || undefined,
        warehouseId: this.filterWarehouseId || undefined,
        fromDate: this.fromDate || undefined,
        toDate: this.toDate || undefined,
        showDimensionWiseStock: this.showDimensionWiseStock,
        excludeZeroStock: !this.includeZeroStock,
      }).subscribe({
        next: res => {
          this.movementReport.set(res);
          this.loading.set(false);
        },
        error: () => { this.loading.set(false); this.toaster.error('::FailedToLoad'); },
      });
      return;
    }

    const params: any = { skipCount: '0', maxResultCount: '5000' };
    if (this.filterWarehouseId) params.warehouseId = this.filterWarehouseId;
    if (companyId) params.companyId = companyId;

    this.stockBalanceService.getStockBalance({ skipCount: 0, maxResultCount: 5000, warehouseId: this.filterWarehouseId || undefined, companyId } as any).subscribe({
      next: (res: any) => {
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
    if (this.reportMode === 'movement') {
      const mRows = this.filteredMovementData();
      if (!mRows.length) return;
      const headers = ['Item Code', 'Item Name', 'Warehouse', 'Batch No', 'Opening Qty', 'Opening Value', 'In Qty', 'In Value', 'Out Qty', 'Out Value', 'Balance Qty', 'Balance Value', 'Valuation Rate'];
      const csvRows = [headers.join(',')];
      for (const r of mRows) {
        csvRows.push([
          `"${r.itemCode ?? ''}"`, `"${r.itemName ?? ''}"`, `"${r.warehouseName ?? ''}"`, `"${r.batchNo ?? ''}"`,
          r.openingQty ?? 0, r.openingValue ?? 0, r.inQty ?? 0, r.inValue ?? 0,
          r.outQty ?? 0, r.outValue ?? 0, r.balanceQty ?? 0, r.balanceValue ?? 0, r.valuationRate ?? 0,
        ].join(','));
      }
      const blob = new Blob([csvRows.join('\n')], { type: 'text/csv' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `stock-balance-movement-${this.fromDate}-to-${this.toDate}.csv`;
      a.click();
      URL.revokeObjectURL(url);
      return;
    }

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
