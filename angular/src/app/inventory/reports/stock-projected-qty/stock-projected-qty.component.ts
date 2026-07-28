import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { StockBalanceService } from '../../../proxy/inventory/stock-balance.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { PaginationComponent, PageEvent } from '../../../shared/components/pagination/pagination.component';
import { exportToCsv } from '../../../shared/utils/csv-export';

interface ProjectedQtyRow {
  itemId: string;
  itemCode: string;
  itemName: string;
  warehouseName: string;
  actualQty: number;
  plannedQty: number;
  orderedQty: number;
  reservedQty: number;
  projectedQty: number;
  reorderLevel: number;
  reorderQty: number;
  shortageQty: number;
}

@Component({
  selector: 'app-stock-projected-qty',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe, PaginationComponent],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0"><i class="bi bi-graph-up-arrow me-2"></i>{{ 'MyERP::ProjectedStockQty' | abpLocalization }}</h5>
        <div class="btn-group btn-group-sm">
          <button class="btn" [class.btn-primary]="!showShortageOnly" [class.btn-outline-primary]="showShortageOnly"
            (click)="showShortageOnly = false; filterData()">{{ 'MyERP::AllItems' | abpLocalization }}</button>
          <button class="btn" [class.btn-danger]="showShortageOnly" [class.btn-outline-danger]="!showShortageOnly"
            (click)="showShortageOnly = true; filterData()">
            <i class="bi bi-exclamation-triangle me-1"></i>{{ 'MyERP::ShortageOnly' | abpLocalization }} ({{ shortageCount }})
          </button>
        </div>
      </div>
      <div class="card-body">
        <!-- Filters -->
        <div class="row mb-3 g-2">
          <div class="col-md-3">
            <select class="form-select form-select-sm" [(ngModel)]="filterWarehouse" (change)="loadData()">
              <option value="">{{ 'MyERP::AllWarehouses' | abpLocalization }}</option>
              @for (w of warehouses(); track w.id) {
                <option [value]="w.id">{{ w.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-3">
            <input type="text" class="form-control form-control-sm" [(ngModel)]="searchTerm"
              [placeholder]="'MyERP::Placeholder:SearchItem' | abpLocalization" (keyup.enter)="filterData()" />
          </div>
          <div class="col-md-2">
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="bi bi-download me-1"></i>Export
            </button>
          </div>
        </div>

        @if (loading()) {
          <div class="text-center py-4"><div class="spinner-border text-primary"></div></div>
        } @else {
          <div class="table-responsive">
            <table class="table table-sm table-hover align-middle">
              <thead class="table-light">
                <tr>
                  <th>{{ 'MyERP::Item' | abpLocalization }}</th>
                  <th>{{ 'MyERP::Warehouse' | abpLocalization }}</th>
                  <th class="text-end">{{ 'MyERP::ActualQty' | abpLocalization }}</th>
                  <th class="text-end">{{ 'MyERP::PlannedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ 'MyERP::OrderedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ 'MyERP::ReservedQty' | abpLocalization }}</th>
                  <th class="text-end fw-bold">{{ 'MyERP::ProjectedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ 'MyERP::ReorderLevel' | abpLocalization }}</th>
                  <th class="text-center">{{ 'MyERP::Status' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track row.itemId + row.warehouseName) {
                  <tr [class.table-danger]="row.shortageQty > 0" [class.table-warning]="row.projectedQty > 0 && row.projectedQty <= row.reorderLevel">
                    <td>
                      <div class="fw-medium small">{{ row.itemCode }}</div>
                      <div class="text-muted" style="font-size: 0.75rem;">{{ row.itemName }}</div>
                    </td>
                    <td class="small">{{ row.warehouseName }}</td>
                    <td class="text-end font-monospace">{{ row.actualQty | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace text-info">{{ row.plannedQty | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace text-success">{{ row.orderedQty | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace text-warning">{{ row.reservedQty | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace fw-bold" [class.text-danger]="row.projectedQty < 0">
                      {{ row.projectedQty | number:'1.2-2' }}
                    </td>
                    <td class="text-end font-monospace">{{ row.reorderLevel | number:'1.2-2' }}</td>
                    <td class="text-center">
                      @if (row.shortageQty > 0) {
                        <span class="badge bg-danger">{{ 'MyERP::Shortage' | abpLocalization }}</span>
                      } @else if (row.projectedQty <= row.reorderLevel && row.reorderLevel > 0) {
                        <span class="badge bg-warning">{{ 'MyERP::Reorder' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-success">{{ 'MyERP::OK' | abpLocalization }}</span>
                      }
                    </td>
                  </tr>
                } @empty {
                  <tr><td colspan="9" class="text-center text-muted py-4">{{ 'MyERP::NoRecordsFound' | abpLocalization }}</td></tr>
                }
              </tbody>
            </table>
          </div>
          <app-pagination [totalCount]="filteredRows().length" [pageSize]="pageSize" [currentPage]="currentPage"
            (pageChange)="onPageChange($event)" />
        }
      </div>
    </div>
  `,
})
export class StockProjectedQtyComponent implements OnInit {
  private stockService = inject(StockBalanceService);
  private warehouseService = inject(WarehouseService);

  allRows = signal<ProjectedQtyRow[]>([]);
  filteredRows = signal<ProjectedQtyRow[]>([]);
  warehouses = signal<{ id: string; name: string }[]>([]);
  loading = signal(true);

  filterWarehouse = '';
  searchTerm = '';
  showShortageOnly = false;
  shortageCount = 0;
  currentPage = 0;
  pageSize = 50;

  ngOnInit() {
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(
      res => this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup)
        .map((w: any) => ({ id: w.id, name: w.warehouseName ?? w.name ?? w.id }))));
    this.loadData();
  }

  loadData() {
    this.loading.set(true);
    this.stockService.getStockBalance({
      skipCount: 0, maxResultCount: 2000,
      warehouseId: this.filterWarehouse || undefined,
    } as any).subscribe({
      next: (res) => {
        const items = (res.items ?? []).map((b: any) => {
          const actual = b.actualQty ?? b.qty ?? 0;
          const planned = b.plannedQty ?? 0;
          const ordered = b.orderedQty ?? 0;
          const reserved = b.reservedQty ?? 0;
          const projected = b.projectedQty ?? (actual + ordered + planned - reserved);
          const reorderLevel = b.reorderLevel ?? 0;
          const shortage = Math.max(0, reorderLevel - projected);

          return {
            itemId: b.itemId ?? '',
            itemCode: b.itemCode ?? '',
            itemName: b.itemName ?? '',
            warehouseName: b.warehouseName ?? '',
            actualQty: actual,
            plannedQty: planned,
            orderedQty: ordered,
            reservedQty: reserved,
            projectedQty: projected,
            reorderLevel,
            reorderQty: b.reorderQty ?? 0,
            shortageQty: shortage,
          } as ProjectedQtyRow;
        });

        this.allRows.set(items);
        this.shortageCount = items.filter(r => r.shortageQty > 0).length;
        this.filterData();
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  filterData() {
    let rows = this.allRows();
    if (this.showShortageOnly) {
      rows = rows.filter(r => r.shortageQty > 0);
    }
    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      rows = rows.filter(r => r.itemCode.toLowerCase().includes(term) || r.itemName.toLowerCase().includes(term));
    }
    this.filteredRows.set(rows);
  }

  onPageChange(event: PageEvent) {
    this.currentPage = event.pageIndex;
  }

  exportCsv() {
    const columns = ['itemCode', 'itemName', 'warehouseName', 'actualQty', 'plannedQty', 'orderedQty', 'reservedQty', 'projectedQty', 'reorderLevel', 'shortageQty'];
    exportToCsv('projected-stock-qty', this.filteredRows(), columns);
  }
}
