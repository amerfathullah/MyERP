import { Component, OnInit, computed, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { StockValuationSummaryService } from '../../../proxy/inventory/stock-valuation-summary.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyService } from '../../../proxy/core/company.service';
import type { StockValuationRowDto, StockValuationSummaryDto, WarehouseDto } from '../../../proxy/inventory/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function filterValuationRows(rows: StockValuationRowDto[], term: string): StockValuationRowDto[] {
  const q = term.trim().toLowerCase();
  if (!q) return rows;
  return rows.filter(
    r =>
      (r.itemCode ?? '').toLowerCase().includes(q) ||
      (r.itemName ?? '').toLowerCase().includes(q) ||
      (r.warehouseName ?? '').toLowerCase().includes(q)
  );
}

@Component({
  selector: 'app-stock-valuation-summary',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-coins me-2 text-primary"></i>{{ '::StockValuationSummary' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::StockValuationSummaryDescription' | abpLocalization }}</small>
        </div>
        @if (filteredRows().length > 0) {
          <button type="button" class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fa fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <div class="card-body">
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-4 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Warehouse' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedWarehouseId" (change)="loadData()">
              <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
              @for (w of warehouses(); track w.id) {
                <option [value]="w.id">{{ w.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-5 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Search' | abpLocalization }}</label>
            <input type="text" class="form-control form-control-sm"
              [placeholder]="'::Search' | abpLocalization"
              [ngModel]="searchTerm()"
              (ngModelChange)="searchTerm.set($event)" />
          </div>
          <div class="col-md-3 col-sm-6">
            <button type="button" class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Refresh' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (summary()) {
          <div class="row g-3 mb-4">
            <div class="col-md-4 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalStockValue' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-success mt-1">{{ (summary()!.totalStockValue ?? 0) | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-4 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalItems' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-primary mt-1">{{ summary()!.totalItems ?? 0 }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-4 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalWarehouses' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-info mt-1">{{ summary()!.totalWarehouses ?? 0 }}</div>
                </div>
              </div>
            </div>
          </div>

          <div class="table-responsive">
            <table class="table table-sm table-hover align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::ItemCode' | abpLocalization }}</th>
                  <th>{{ '::ItemName' | abpLocalization }}</th>
                  <th>{{ '::Warehouse' | abpLocalization }}</th>
                  <th>{{ '::UOM' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Qty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ValuationRate' | abpLocalization }}</th>
                  <th class="text-end">{{ '::StockValue' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track (row.itemId ?? '') + ':' + (row.warehouseId ?? '')) {
                  <tr>
                    <td class="font-monospace small">{{ row.itemCode }}</td>
                    <td class="small">{{ row.itemName }}</td>
                    <td class="small">{{ row.warehouseName }}</td>
                    <td class="small">{{ row.uom }}</td>
                    <td class="text-end font-monospace small">{{ (row.quantity ?? 0) | number:'1.2-4' }}</td>
                    <td class="text-end font-monospace small">{{ (row.valuationRate ?? 0) | number:'1.2-4' }}</td>
                    <td class="text-end font-monospace small fw-semibold">{{ (row.stockValue ?? 0) | number:'1.2-2' }}</td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="7" class="text-center text-muted py-4">{{ '::NoDataAvailable' | abpLocalization }}</td>
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
export class StockValuationSummaryComponent implements OnInit {
  private service = inject(StockValuationSummaryService);
  private warehouseService = inject(WarehouseService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);

  summary = signal<StockValuationSummaryDto | null>(null);
  warehouses = signal<WarehouseDto[]>([]);
  isLoading = signal(false);
  selectedWarehouseId = '';
  searchTerm = signal('');
  private companyId = '';

  filteredRows = computed(() => filterValuationRows(this.summary()?.rows ?? [], this.searchTerm()));

  ngOnInit(): void {
    this.warehouseService.getList({ maxResultCount: 100 } as any).subscribe({
      next: res => this.warehouses.set(res.items ?? []),
      error: () => {},
    });

    const ctxId = this.companyContext.currentCompanyId() ?? '';
    if (ctxId) {
      this.companyId = ctxId;
      this.loadData();
    } else {
      this.companyService.getList({ skipCount: 0, maxResultCount: 10, sorting: '' }).subscribe({
        next: res => {
          this.companyId = res.items?.[0]?.id ?? '';
          if (this.companyId) this.loadData();
        },
        error: () => {},
      });
    }
  }

  loadData(): void {
    const cid = this.companyContext.currentCompanyId() ?? this.companyId;
    if (!cid) return;
    this.isLoading.set(true);
    this.service.getSummary(cid, this.selectedWarehouseId || undefined).subscribe({
      next: res => {
        this.summary.set(res);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (rows.length === 0) return;
    exportToCsv(
      `Stock-Valuation-Summary-${new Date().toISOString().substring(0, 10)}`,
      rows.map(r => ({
        ItemCode: r.itemCode ?? '',
        ItemName: r.itemName ?? '',
        Warehouse: r.warehouseName ?? '',
        UOM: r.uom ?? '',
        Quantity: r.quantity ?? 0,
        ValuationRate: r.valuationRate ?? 0,
        StockValue: r.stockValue ?? 0,
      })),
      ['ItemCode', 'ItemName', 'Warehouse', 'UOM', 'Quantity', 'ValuationRate', 'StockValue']
    );
  }
}
