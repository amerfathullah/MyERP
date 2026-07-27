import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { StockBalanceService } from '../../../proxy/inventory/stock-balance.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { ItemGroupService } from '../../../proxy/inventory/item-group.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

interface AgeingRow {
  itemId: string;
  itemCode: string;
  itemName: string;
  warehouseName: string;
  qty: number;
  valuationRate: number;
  stockValue: number;
  // Ageing buckets (days)
  bucket0to30: number;
  bucket31to60: number;
  bucket61to90: number;
  bucket91plus: number;
  averageAge: number;
}

@Component({
  selector: 'app-stock-ageing',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0"><i class="bi bi-clock-history me-2"></i>{{ 'MyERP::StockAgeing' | abpLocalization }}</h5>
        <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
          <i class="bi bi-download me-1"></i>Export CSV
        </button>
      </div>
      <div class="card-body">
        <!-- Filters -->
        <div class="row mb-3 g-2">
          <div class="col-md-3">
            <label class="form-label small">{{ 'MyERP::Warehouse' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="filterWarehouse" (change)="loadData()">
              <option value="">All Warehouses</option>
              @for (w of warehouses(); track w.id) {
                <option [value]="w.id">{{ w.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label small">{{ 'MyERP::ItemGroup' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="filterItemGroup" (change)="loadData()">
              <option value="">All Item Groups</option>
              @for (g of itemGroups(); track g.id) {
                <option [value]="g.id">{{ g.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label small">{{ 'MyERP::AsOfDate' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="asOfDate" (change)="loadData()" />
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
                <div class="fw-bold">{{ rows().length }}</div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-2 text-center">
                <div class="small text-muted">Total Value</div>
                <div class="fw-bold">{{ totalValue | number:'1.2-2' }}</div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-2 text-center bg-warning bg-opacity-10">
                <div class="small text-muted">Aged &gt; 90 days</div>
                <div class="fw-bold text-warning">{{ agedCount }}</div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-2 text-center">
                <div class="small text-muted">Avg Age (days)</div>
                <div class="fw-bold">{{ avgAge | number:'1.0-0' }}</div>
              </div>
            </div>
          </div>

          <!-- Table -->
          <div class="table-responsive">
            <table class="table table-sm table-hover align-middle">
              <thead class="table-light">
                <tr>
                  <th>{{ 'MyERP::Item' | abpLocalization }}</th>
                  <th>{{ 'MyERP::Warehouse' | abpLocalization }}</th>
                  <th class="text-end">Qty</th>
                  <th class="text-end">Value</th>
                  <th class="text-end">0-30d</th>
                  <th class="text-end">31-60d</th>
                  <th class="text-end">61-90d</th>
                  <th class="text-end">&gt;90d</th>
                  <th class="text-end">Avg Age</th>
                </tr>
              </thead>
              <tbody>
                @for (row of rows(); track row.itemId + row.warehouseName) {
                  <tr>
                    <td>
                      <div class="fw-medium small">{{ row.itemCode }}</div>
                      <div class="text-muted" style="font-size: 0.75rem;">{{ row.itemName }}</div>
                    </td>
                    <td class="small">{{ row.warehouseName }}</td>
                    <td class="text-end font-monospace">{{ row.qty | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace">{{ row.stockValue | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace">{{ row.bucket0to30 | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace">{{ row.bucket31to60 | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace">{{ row.bucket61to90 | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace" [class.text-danger]="row.bucket91plus > 0">
                      {{ row.bucket91plus | number:'1.2-2' }}
                    </td>
                    <td class="text-end">
                      <span class="badge" [class.bg-success]="row.averageAge <= 30"
                        [class.bg-warning]="row.averageAge > 30 && row.averageAge <= 90"
                        [class.bg-danger]="row.averageAge > 90">
                        {{ row.averageAge | number:'1.0-0' }}d
                      </span>
                    </td>
                  </tr>
                } @empty {
                  <tr><td colspan="9" class="text-center text-muted py-4">No stock data found</td></tr>
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
  private stockService = inject(StockBalanceService);
  private warehouseService = inject(WarehouseService);
  private itemGroupService = inject(ItemGroupService);

  rows = signal<AgeingRow[]>([]);
  warehouses = signal<{ id: string; name: string }[]>([]);
  itemGroups = signal<{ id: string; name: string }[]>([]);
  loading = signal(true);

  filterWarehouse = '';
  filterItemGroup = '';
  asOfDate = new Date().toISOString().substring(0, 10);

  totalValue = 0;
  agedCount = 0;
  avgAge = 0;

  ngOnInit() {
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(
      res => this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup).map((w: any) => ({ id: w.id, name: w.warehouseName ?? w.name ?? w.id }))));
    this.itemGroupService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(
      res => this.itemGroups.set((res.items ?? []).map((g: any) => ({ id: g.id, name: g.name ?? g.id }))));
    this.loadData();
  }

  loadData() {
    this.loading.set(true);
    // Use stock balance endpoint and compute ageing client-side
    this.stockService.getStockBalance({
      skipCount: 0, maxResultCount: 1000,
      warehouseId: this.filterWarehouse || undefined,
    } as any).subscribe({
      next: (res) => {
        const balances = res.items ?? [];
        const today = new Date(this.asOfDate);

        const ageingRows: AgeingRow[] = balances
          .filter((b: any) => (b.actualQty ?? b.qty ?? 0) > 0)
          .map((b: any) => {
            const qty = b.actualQty ?? b.qty ?? 0;
            const rate = b.valuationRate ?? 0;
            const stockValue = qty * rate;
            // Approximate average age from last stock entry (simplified)
            const lastDate = b.lastStockDate ? new Date(b.lastStockDate) : today;
            const daysSinceLastEntry = Math.max(0, Math.floor((today.getTime() - lastDate.getTime()) / 86400000));
            const avgDays = daysSinceLastEntry;

            return {
              itemId: b.itemId ?? '',
              itemCode: b.itemCode ?? b.itemId?.substring(0, 8) ?? '',
              itemName: b.itemName ?? '',
              warehouseName: b.warehouseName ?? '',
              qty,
              valuationRate: rate,
              stockValue,
              bucket0to30: avgDays <= 30 ? qty : 0,
              bucket31to60: avgDays > 30 && avgDays <= 60 ? qty : 0,
              bucket61to90: avgDays > 60 && avgDays <= 90 ? qty : 0,
              bucket91plus: avgDays > 90 ? qty : 0,
              averageAge: avgDays,
            } as AgeingRow;
          });

        this.rows.set(ageingRows);
        this.totalValue = ageingRows.reduce((s, r) => s + r.stockValue, 0);
        this.agedCount = ageingRows.filter(r => r.averageAge > 90).length;
        this.avgAge = ageingRows.length > 0
          ? ageingRows.reduce((s, r) => s + r.averageAge, 0) / ageingRows.length
          : 0;
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  exportCsv() {
    const columns = ['itemCode', 'itemName', 'warehouseName', 'qty', 'stockValue', 'bucket0to30', 'bucket31to60', 'bucket61to90', 'bucket91plus', 'averageAge'];
    exportToCsv('stock-ageing-report', this.rows(), columns);
  }
}
