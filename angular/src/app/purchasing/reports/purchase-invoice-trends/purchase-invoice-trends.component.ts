import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { PurchaseInvoiceTrendsService } from '../../../proxy/purchasing/purchase-invoice-trends.service';
import type {
  PurchaseInvoiceTrendsReportDto,
  PurchaseInvoiceTrendsRowDto,
} from '../../../proxy/purchasing/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-purchase-invoice-trends',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-chart-line text-primary me-2"></i>
            {{ '::PurchaseInvoiceTrends' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::PurchaseInvoiceTrendsDescription' | abpLocalization }}
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
                [(ngModel)]="selectedPeriod"
                (ngModelChange)="loadReport()"
              >
                <option value="Monthly">{{ '::Monthly' | abpLocalization }}</option>
                <option value="Quarterly">{{ '::Quarterly' | abpLocalization }}</option>
                <option value="Half-Yearly">{{ '::HalfYearly' | abpLocalization }}</option>
                <option value="Yearly">{{ '::Yearly' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::BasedOn' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedBasedOn"
                (ngModelChange)="onBasedOnChange()"
              >
                <option value="Supplier">{{ '::Supplier' | abpLocalization }}</option>
                <option value="Item">{{ '::Item' | abpLocalization }}</option>
                <option value="ItemGroup">{{ '::ItemGroup' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::GroupBy' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedGroupBy"
                (ngModelChange)="loadReport()"
              >
                <option value="">-- {{ '::None' | abpLocalization }} --</option>
                @if (selectedBasedOn !== 'Supplier') {
                  <option value="Supplier">{{ '::Supplier' | abpLocalization }}</option>
                }
                @if (selectedBasedOn !== 'Item') {
                  <option value="Item">{{ '::Item' | abpLocalization }}</option>
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
              <div class="form-check form-switch mb-1">
                <input
                  class="form-check-input"
                  type="checkbox"
                  id="includeReturnsCheck"
                  [(ngModel)]="includeReturns"
                  (ngModelChange)="loadReport()"
                />
                <label class="form-check-label small" for="includeReturnsCheck">
                  {{ '::IncludeReturns' | abpLocalization }}
                </label>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Metric Toggle & Chart Summary -->
      @if (report() && report()!.rows.length > 0) {
        <div class="d-flex justify-content-between align-items-center mb-2">
          <div class="btn-group btn-group-sm" role="group">
            <button
              type="button"
              class="btn"
              [class.btn-primary]="displayMetric === 'amount'"
              [class.btn-outline-primary]="displayMetric !== 'amount'"
              (click)="displayMetric = 'amount'"
            >
              <i class="fa fa-coins me-1"></i>{{ '::Amount' | abpLocalization }}
            </button>
            <button
              type="button"
              class="btn"
              [class.btn-primary]="displayMetric === 'quantity'"
              [class.btn-outline-primary]="displayMetric !== 'quantity'"
              (click)="displayMetric = 'quantity'"
            >
              <i class="fa fa-boxes-stacked me-1"></i>{{ '::Quantity' | abpLocalization }}
            </button>
          </div>

          <div class="badge bg-light text-dark border p-2">
            <span class="text-muted me-1">{{ '::TotalSpend' | abpLocalization }}:</span>
            <span class="fw-bold fs-6">
              {{ report()!.totalRow.totalAmount | number: '1.2-2' }}
              {{ report()!.companyCurrency }}
            </span>
            <span class="text-muted ms-3 me-1">{{ '::TotalQty' | abpLocalization }}:</span>
            <span class="fw-bold fs-6">
              {{ report()!.totalRow.totalQty | number: '1.0-2' }}
            </span>
          </div>
        </div>

        <!-- Trend Chart Bars -->
        @if (report()!.chart.datapoints.length > 0) {
          <div class="card mb-3 border-0 shadow-sm">
            <div class="card-body py-3">
              <div class="small fw-bold text-muted mb-2">
                <i class="fa fa-chart-column me-1"></i>{{ '::PeriodSummary' | abpLocalization }} ({{ report()!.companyCurrency }})
              </div>
              <div class="d-flex align-items-end gap-2 pt-2" style="height: 120px;">
                @for (val of report()!.chart.datapoints; track $index) {
                  <div class="d-flex flex-column align-items-center flex-fill h-100 justify-content-end">
                    <span class="small text-muted mb-1" style="font-size: 0.75rem;">
                      {{ val | number: '1.0-0' }}
                    </span>
                    <div
                      class="w-100 rounded-top bg-primary"
                      [style.height.%]="getBarHeight(val)"
                      [style.opacity]="val > 0 ? '0.85' : '0.2'"
                      [title]="report()!.chart.labels[$index] + ': ' + (val | number: '1.2-2')"
                    ></div>
                    <span class="text-truncate text-center small mt-1 w-100" style="font-size: 0.7rem;">
                      {{ report()!.chart.labels[$index] }}
                    </span>
                  </div>
                }
              </div>
            </div>
          </div>
        }

        <!-- Data Table -->
        <div class="card border-0 shadow-sm">
          <div class="table-responsive">
            <table class="table table-hover align-middle mb-0 table-sm">
              <thead class="table-light">
                <tr>
                  <th style="min-width: 180px;">
                    @if (report()!.basedOn === 'Supplier') {
                      {{ '::Supplier' | abpLocalization }}
                    } @else if (report()!.basedOn === 'Item') {
                      {{ '::Item' | abpLocalization }}
                    } @else {
                      {{ '::ItemGroup' | abpLocalization }}
                    }
                  </th>
                  @if (report()!.rows.some(hasSecondaryInfo)) {
                    <th style="min-width: 120px;">
                      @if (report()!.basedOn === 'Supplier') {
                        {{ '::SupplierGroup' | abpLocalization }}
                      } @else {
                        {{ '::Group' | abpLocalization }}
                      }
                    </th>
                  }
                  @for (col of report()!.periodColumns; track col.key) {
                    <th class="text-end" style="min-width: 100px;">
                      {{ col.label }}
                    </th>
                  }
                  <th class="text-end bg-light fw-bold" style="min-width: 120px;">
                    {{ '::Total' | abpLocalization }}
                  </th>
                </tr>
              </thead>
              <tbody>
                @for (row of report()!.rows; track row.entityId + (row.groupByValue ?? '')) {
                  <tr [class.table-light]="row.groupByValue">
                    <td>
                      @if (row.groupByValue) {
                        <span class="ms-3 text-muted">
                          <i class="fa fa-angle-right me-1"></i>{{ row.groupByValue }}
                        </span>
                      } @else {
                        <span class="fw-semibold">{{ row.entityName }}</span>
                        @if (row.entityCode && row.entityCode !== row.entityName) {
                          <span class="badge bg-light text-secondary ms-1">{{ row.entityCode }}</span>
                        }
                      }
                    </td>
                    @if (report()!.rows.some(hasSecondaryInfo)) {
                      <td class="text-muted small">
                        {{ row.secondaryInfo || '—' }}
                      </td>
                    }
                    @for (col of report()!.periodColumns; track col.key) {
                      <td class="text-end font-monospace">
                        @if (displayMetric === 'amount') {
                          {{ (row.periodAmounts[col.key] || 0) | number: '1.2-2' }}
                        } @else {
                          {{ (row.periodQuantities[col.key] || 0) | number: '1.0-2' }}
                        }
                      </td>
                    }
                    <td class="text-end fw-bold font-monospace bg-light">
                      @if (displayMetric === 'amount') {
                        {{ row.totalAmount | number: '1.2-2' }}
                      } @else {
                        {{ row.totalQty | number: '1.0-2' }}
                      }
                    </td>
                  </tr>
                }
              </tbody>
              <tfoot class="table-light fw-bold border-top-2">
                <tr>
                  <td>{{ '::Total' | abpLocalization }}</td>
                  @if (report()!.rows.some(hasSecondaryInfo)) {
                    <td></td>
                  }
                  @for (col of report()!.periodColumns; track col.key) {
                    <td class="text-end font-monospace">
                      @if (displayMetric === 'amount') {
                        {{ (report()!.totalRow.periodAmounts[col.key] || 0) | number: '1.2-2' }}
                      } @else {
                        {{ (report()!.totalRow.periodQuantities[col.key] || 0) | number: '1.0-2' }}
                      }
                    </td>
                  }
                  <td class="text-end font-monospace text-primary">
                    @if (displayMetric === 'amount') {
                      {{ report()!.totalRow.totalAmount | number: '1.2-2' }}
                    } @else {
                      {{ report()!.totalRow.totalQty | number: '1.0-2' }}
                    }
                  </td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>
      } @else if (isLoading()) {
        <div class="card border-0 shadow-sm p-5 text-center">
          <div class="spinner-border text-primary mx-auto mb-2" role="status"></div>
          <div class="text-muted small">{{ '::LoadingReport' | abpLocalization }}...</div>
        </div>
      } @else {
        <div class="card border-0 shadow-sm p-5 text-center text-muted">
          <i class="fa fa-chart-line fa-3x mb-3 text-secondary opacity-50"></i>
          <h5>{{ '::NoDataAvailable' | abpLocalization }}</h5>
          <p class="small mb-0">{{ '::NoRecordsFoundForSelectedCriteria' | abpLocalization }}</p>
        </div>
      }
    </div>
  `,
  styles: [
    `
      .font-monospace {
        font-family: var(--bs-font-monospace);
      }
      .table > :not(caption) > * > * {
        padding: 0.5rem 0.75rem;
      }
    `,
  ],
})
export class PurchaseInvoiceTrendsComponent implements OnInit {
  private trendsService = inject(PurchaseInvoiceTrendsService);
  private companyContext = inject(CompanyContextService);

  report = signal<PurchaseInvoiceTrendsReportDto | null>(null);
  isLoading = signal<boolean>(false);

  selectedPeriod = 'Monthly';
  selectedBasedOn = 'Supplier';
  selectedGroupBy = '';
  fromDate = '';
  toDate = '';
  includeReturns = false;
  displayMetric: 'amount' | 'quantity' = 'amount';

  hasSecondaryInfo = (r: PurchaseInvoiceTrendsRowDto) => Boolean(r.secondaryInfo);

  ngOnInit(): void {
    const today = new Date();
    const curYear = today.getFullYear();
    this.fromDate = `${curYear}-01-01`;
    this.toDate = today.toISOString().split('T')[0];

    this.loadReport();
  }

  onBasedOnChange(): void {
    if (this.selectedBasedOn === 'Supplier' && this.selectedGroupBy === 'Supplier') {
      this.selectedGroupBy = '';
    } else if (this.selectedBasedOn === 'Item' && this.selectedGroupBy === 'Item') {
      this.selectedGroupBy = '';
    }
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.trendsService
      .getReport({
        companyId,
        period: this.selectedPeriod,
        basedOn: this.selectedBasedOn,
        groupBy: this.selectedGroupBy || undefined,
        fromDate: this.fromDate || undefined,
        toDate: this.toDate || undefined,
        includeReturns: this.includeReturns,
      })
      .subscribe({
        next: (res) => {
          this.report.set(res);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
        },
      });
  }

  getBarHeight(val: number): number {
    const r = this.report();
    if (!r || !r.chart.datapoints.length) return 0;
    const max = Math.max(...r.chart.datapoints.map((d) => Math.abs(d)), 1);
    const pct = Math.round((Math.abs(val) / max) * 100);
    return Math.max(pct, 4);
  }

  exportCsv(): void {
    const r = this.report();
    if (!r || !r.rows.length) return;

    const headers = [
      r.basedOn,
      'Group',
      ...r.periodColumns.map((c) => c.label),
      'Total',
    ];

    const data = r.rows.map((row) => {
      const item: Record<string, any> = {
        [r.basedOn]: row.groupByValue
          ? `  -> ${row.groupByValue}`
          : `${row.entityName} (${row.entityCode})`,
        Group: row.secondaryInfo || '',
      };

      for (const col of r.periodColumns) {
        item[col.label] =
          this.displayMetric === 'amount'
            ? row.periodAmounts[col.key] || 0
            : row.periodQuantities[col.key] || 0;
      }

      item['Total'] =
        this.displayMetric === 'amount' ? row.totalAmount : row.totalQty;
      return item;
    });

    exportToCsv(
      `purchase-invoice-trends-${this.selectedPeriod.toLowerCase()}-${new Date().toISOString().slice(0, 10)}.csv`,
      data,
      headers
    );
  }
}
