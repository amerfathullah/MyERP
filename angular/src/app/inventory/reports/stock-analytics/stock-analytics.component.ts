import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { StockAnalyticsService } from '../../../proxy/inventory/stock-analytics.service';
import type {
  StockAnalyticsReportDto,
  StockAnalyticsRowDto,
  StockAnalyticsPeriodColumnDto,
} from '../../../proxy/inventory/models';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-stock-analytics',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-chart-line text-primary me-2"></i>
            {{ '::StockAnalytics' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::StockAnalyticsDescription' | abpLocalization }}
          </p>
        </div>
        @if (report() && report()!.rows.length > 0) {
          <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fa fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <!-- Filters -->
      <div class="card mb-3 shadow-sm border-0">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::Period' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedRange"
                (ngModelChange)="loadReport()"
              >
                <option value="Monthly">{{ '::Monthly' | abpLocalization }}</option>
                <option value="Quarterly">{{ '::Quarterly' | abpLocalization }}</option>
                <option value="HalfYearly">{{ '::HalfYearly' | abpLocalization }}</option>
                <option value="Yearly">{{ '::Yearly' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::ValueOrQuantity' | abpLocalization }}</label>
              <div class="btn-group btn-group-sm w-100" role="group">
                <button
                  type="button"
                  class="btn"
                  [ngClass]="selectedValueOrQty === 'Quantity' ? 'btn-primary' : 'btn-outline-secondary'"
                  (click)="setValueOrQty('Quantity')"
                >
                  {{ '::Quantity' | abpLocalization }}
                </button>
                <button
                  type="button"
                  class="btn"
                  [ngClass]="selectedValueOrQty === 'Value' ? 'btn-primary' : 'btn-outline-secondary'"
                  (click)="setValueOrQty('Value')"
                >
                  {{ '::Value' | abpLocalization }}
                </button>
              </div>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::Warehouse' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedWarehouseId"
                (ngModelChange)="loadReport()"
              >
                <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
                @for (w of warehouses(); track w.id) {
                  <option [value]="w.id">{{ w.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::FromDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="fromDate"
                (ngModelChange)="loadReport()"
              />
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::ToDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="toDate"
                (ngModelChange)="loadReport()"
              />
            </div>
            <div class="col-md-2 d-flex align-items-end">
              <button
                class="btn btn-primary btn-sm ms-auto px-4 w-100"
                (click)="loadReport()"
                [disabled]="isLoading()"
              >
                @if (isLoading()) {
                  <span class="spinner-border spinner-border-sm me-1"></span>
                } @else {
                  <i class="fa fa-sync-alt me-1"></i>
                }
                {{ '::Generate' | abpLocalization }}
              </button>
            </div>
          </div>

          <!-- Search Filter -->
          <div class="row g-2 mt-2 align-items-center">
            <div class="col-md-6">
              <div class="input-group input-group-sm">
                <span class="input-group-text"><i class="fa fa-search"></i></span>
                <input
                  type="text"
                  class="form-control"
                  [placeholder]="'::Search' | abpLocalization"
                  [(ngModel)]="searchTerm"
                />
              </div>
            </div>
            <div class="col-md-6 text-end text-muted small">
              @if (report()) {
                <span>{{ '::Showing' | abpLocalization }} {{ filteredRows().length }} {{ '::Entries' | abpLocalization }}</span>
              }
            </div>
          </div>
        </div>
      </div>

      <!-- KPI Summary Cards -->
      @if (report()) {
        <div class="row g-2 mb-3">
          <div class="col-12 col-md-4">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::TotalItems' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-dark">
                {{ report()!.totalItems }}
              </div>
            </div>
          </div>
          <div class="col-6 col-md-4">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::TotalClosingQuantity' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-primary">
                {{ report()!.totalClosingQuantity | number:'1.2-4' }}
              </div>
            </div>
          </div>
          <div class="col-6 col-md-4">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::TotalClosingValue' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-success">
                {{ report()!.totalClosingValue | number:'1.2-2' }} {{ report()!.companyCurrency }}
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Chart View -->
      @if (report() && report()!.chart && report()!.chart.datapoints.length > 0) {
        <div class="card mb-3 shadow-sm border-0">
          <div class="card-header bg-transparent py-2">
            <h6 class="mb-0 fw-bold text-dark">
              <i class="fa fa-chart-column text-primary me-2"></i>{{ '::StockAnalytics' | abpLocalization }} ({{ report()!.valueOrQuantity }})
            </h6>
          </div>
          <div class="card-body py-3">
            <div class="row g-2 align-items-end text-center" style="min-height: 120px;">
              @for (point of report()!.chart.datapoints; track $index) {
                <div class="col d-flex flex-column align-items-center justify-content-end h-100">
                  <span class="small fw-bold text-dark mb-1 font-monospace">
                    {{ point | number:'1.0-0' }}
                  </span>
                  <div
                    class="bg-primary rounded-top w-100"
                    [style.height.px]="getBarHeight(point)"
                    style="min-height: 6px; max-width: 48px; opacity: 0.85;"
                  ></div>
                  <span class="text-muted text-truncate mt-1" style="font-size: 11px; max-width: 60px;">
                    {{ report()!.chart.labels[$index] }}
                  </span>
                </div>
              }
            </div>
          </div>
        </div>
      }

      <!-- Data Table -->
      @if (isLoading()) {
        <div class="text-center py-5">
          <div class="spinner-border text-primary" role="status">
            <span class="visually-hidden">{{ '::Loading' | abpLocalization }}...</span>
          </div>
        </div>
      } @else if (!report() || filteredRows().length === 0) {
        <div class="card border-0 shadow-sm text-center py-5">
          <i class="fa fa-boxes-stacked fa-3x text-muted mb-2"></i>
          <p class="text-muted mb-0">{{ '::NoStockAnalyticsFound' | abpLocalization }}</p>
        </div>
      } @else {
        <div class="card border-0 shadow-sm">
          <div class="table-responsive">
            <table class="table table-hover table-sm align-middle mb-0 text-nowrap">
              <thead class="table-light">
                <tr>
                  <th style="min-width: 140px;">{{ '::ItemCode' | abpLocalization }}</th>
                  <th style="min-width: 180px;">{{ '::ItemName' | abpLocalization }}</th>
                  <th style="width: 120px;">{{ '::ItemGroup' | abpLocalization }}</th>
                  <th style="width: 80px;">{{ '::UOM' | abpLocalization }}</th>
                  <th class="text-end" style="width: 110px;">{{ '::OpeningBalance' | abpLocalization }}</th>
                  @for (col of report()!.periodColumns; track col.key) {
                    <th class="text-end" style="min-width: 110px;">{{ col.label }}</th>
                  }
                  <th class="text-end table-active" style="width: 120px;">{{ '::ClosingBalance' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track row.itemId) {
                  <tr>
                    <td class="fw-bold font-monospace">{{ row.itemCode }}</td>
                    <td>
                      <span class="text-truncate d-inline-block" style="max-width: 200px;">
                        {{ row.itemName }}
                      </span>
                    </td>
                    <td>
                      <span class="badge bg-secondary-subtle text-secondary border border-secondary-subtle">
                        {{ row.itemGroup || '-' }}
                      </span>
                    </td>
                    <td>
                      <span class="small text-muted">{{ row.uom || '-' }}</span>
                    </td>
                    <td class="text-end font-monospace text-muted">
                      {{ row.openingBalance | number:'1.2-2' }}
                    </td>
                    @for (col of report()!.periodColumns; track col.key) {
                      <td class="text-end font-monospace">
                        <span [class.text-danger]="(row.periodBalances[col.key] || 0) < 0">
                          {{ (row.periodBalances[col.key] || 0) | number:'1.2-2' }}
                        </span>
                      </td>
                    }
                    <td class="text-end font-monospace fw-bold table-active">
                      <span [class.text-danger]="row.closingBalance < 0" [class.text-primary]="row.closingBalance > 0">
                        {{ row.closingBalance | number:'1.2-2' }}
                      </span>
                    </td>
                  </tr>
                }
              </tbody>
              @if (report()!.totalRow) {
                <tfoot class="table-light border-top border-2">
                  <tr class="fw-bold">
                    <td colspan="4" class="text-end">{{ '::Total' | abpLocalization }}:</td>
                    <td class="text-end font-monospace">
                      {{ report()!.totalRow.openingBalance | number:'1.2-2' }}
                    </td>
                    @for (col of report()!.periodColumns; track col.key) {
                      <td class="text-end font-monospace">
                        {{ (report()!.totalRow.periodBalances[col.key] || 0) | number:'1.2-2' }}
                      </td>
                    }
                    <td class="text-end font-monospace table-active text-primary">
                      {{ report()!.totalRow.closingBalance | number:'1.2-2' }}
                    </td>
                  </tr>
                </tfoot>
              }
            </table>
          </div>
        </div>
      }
    </div>
  `,
})
export class StockAnalyticsComponent implements OnInit {
  private readonly stockAnalyticsService = inject(StockAnalyticsService);
  private readonly warehouseService = inject(WarehouseService);
  private readonly companyContext = inject(CompanyContextService);

  report = signal<StockAnalyticsReportDto | null>(null);
  isLoading = signal<boolean>(false);
  warehouses = signal<{ id: string; name: string }[]>([]);

  selectedRange = 'Monthly';
  selectedValueOrQty = 'Quantity';
  selectedWarehouseId = '';
  fromDate = '';
  toDate = '';
  searchTerm = '';

  filteredRows = computed(() => {
    const r = this.report();
    if (!r || !r.rows) return [];
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return r.rows;

    return r.rows.filter(row =>
      (row.itemCode && row.itemCode.toLowerCase().includes(term)) ||
      (row.itemName && row.itemName.toLowerCase().includes(term)) ||
      (row.itemGroup && row.itemGroup.toLowerCase().includes(term))
    );
  });

  ngOnInit(): void {
    const now = new Date();
    const startOfYear = new Date(now.getFullYear(), 0, 1);
    this.fromDate = startOfYear.toISOString().substring(0, 10);
    this.toDate = now.toISOString().substring(0, 10);

    this.loadWarehouses();
    this.loadReport();
  }

  loadWarehouses(): void {
    const companyId = this.companyContext.selectedCompanyId();
    this.warehouseService.getList({
      maxResultCount: 200,
      skipCount: 0,
      companyId: companyId || undefined,
      isActive: true,
      isGroup: false
    } as any).subscribe({
      next: res => {
        this.warehouses.set((res.items ?? []).map(w => ({ id: w.id!, name: w.name! })));
      },
      error: () => this.warehouses.set([])
    });
  }

  setValueOrQty(val: string): void {
    if (this.selectedValueOrQty !== val) {
      this.selectedValueOrQty = val;
      this.loadReport();
    }
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.stockAnalyticsService
      .getReport({
        companyId,
        fromDate: this.fromDate || undefined,
        toDate: this.toDate || undefined,
        range: this.selectedRange,
        valueOrQuantity: this.selectedValueOrQty,
        warehouseId: this.selectedWarehouseId || undefined,
        searchTerm: this.searchTerm || undefined,
      })
      .subscribe({
        next: res => {
          this.report.set(res);
          this.isLoading.set(false);
        },
        error: () => {
          this.report.set(null);
          this.isLoading.set(false);
        },
      });
  }

  getBarHeight(point: number): number {
    const max = Math.max(...(this.report()?.chart.datapoints ?? [1]), 1);
    if (max <= 0) return 6;
    return Math.max(Math.round((point / max) * 100), 6);
  }

  exportCsv(): void {
    const r = this.report();
    if (!r || r.rows.length === 0) return;

    const baseHeaders = ['ItemCode', 'ItemName', 'ItemGroup', 'Brand', 'UOM', 'OpeningBalance'];
    const periodHeaders = r.periodColumns.map(p => p.label);
    const columns = [...baseHeaders, ...periodHeaders, 'ClosingBalance'];

    const exportData = r.rows.map(row => {
      const obj: Record<string, any> = {
        ItemCode: row.itemCode,
        ItemName: row.itemName,
        ItemGroup: row.itemGroup,
        Brand: row.brand || '',
        UOM: row.uom || '',
        OpeningBalance: row.openingBalance,
      };
      r.periodColumns.forEach(p => {
        obj[p.label] = row.periodBalances[p.key] || 0;
      });
      obj['ClosingBalance'] = row.closingBalance;
      return obj;
    });

    exportToCsv(`stock_analytics_${r.range.toLowerCase()}_${r.valueOrQuantity.toLowerCase()}`, exportData, columns);
  }
}
