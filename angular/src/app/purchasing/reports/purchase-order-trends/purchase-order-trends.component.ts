import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { PurchaseOrderTrendsService } from '../../../proxy/purchasing/purchase-order-trends.service';
import type {
  PurchaseOrderTrendsReportDto,
  PurchaseOrderTrendsRowDto,
} from '../../../proxy/purchasing/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-purchase-order-trends',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-chart-line text-primary me-2"></i>
            {{ '::PurchaseOrderTrends' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::PurchaseOrderTrendsDescription' | abpLocalization }}
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
            <div class="col-md-2 d-flex align-items-center gap-2">
              <div class="form-check form-check-inline mb-0 mt-3">
                <input
                  type="checkbox"
                  class="form-check-input"
                  id="includeClosedOrdersCheck"
                  [(ngModel)]="includeClosedOrders"
                  (ngModelChange)="loadReport()"
                />
                <label class="form-check-label small" for="includeClosedOrdersCheck">
                  {{ '::IncludeClosedOrders' | abpLocalization }}
                </label>
              </div>
              <button
                class="btn btn-sm btn-primary mt-3"
                (click)="loadReport()"
                [disabled]="loading()"
              >
                @if (loading()) {
                  <span class="spinner-border spinner-border-sm me-1"></span>
                } @else {
                  <i class="fa fa-sync me-1"></i>
                }
                {{ '::Refresh' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Trend Chart Visualization -->
      @if (report() && report()!.chart && report()!.chart.labels.length > 0) {
        <div class="card mb-3 shadow-sm border-0">
          <div class="card-header bg-transparent py-2">
            <h6 class="mb-0 text-muted small fw-bold">
              <i class="fa fa-chart-bar me-1"></i>
              {{ '::TrendVisualization' | abpLocalization }} ({{ report()!.companyCurrency }})
            </h6>
          </div>
          <div class="card-body py-3">
            <div class="d-flex align-items-end gap-2" style="height: 140px; overflow-x: auto;">
              @for (lbl of report()!.chart.labels; track lbl; let idx = $index) {
                <div class="d-flex flex-column align-items-center flex-grow-1 text-center" style="min-width: 55px;">
                  <span class="small fw-bold text-primary mb-1">
                    {{ report()!.chart.datapoints[idx] | number:'1.0-0' }}
                  </span>
                  <div
                    class="w-100 rounded-top bg-primary"
                    [style.height.px]="getBarHeight(report()!.chart.datapoints[idx])"
                    [style.opacity]="report()!.chart.datapoints[idx] > 0 ? '0.85' : '0.2'"
                  ></div>
                  <span class="text-muted text-truncate w-100 mt-1" style="font-size: 0.72rem;">
                    {{ lbl }}
                  </span>
                </div>
              }
            </div>
          </div>
        </div>
      }

      <!-- Data Table -->
      <div class="card shadow-sm border-0">
        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5">
              <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Loading...</span>
              </div>
            </div>
          } @else if (!report() || report()!.rows.length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-inbox fa-3x mb-3 text-secondary"></i>
              <p class="mb-0">{{ '::NoPurchaseOrderTrendsDataFound' | abpLocalization }}</p>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-hover table-striped table-bordered align-middle mb-0" style="font-size: 0.85rem;">
                <thead class="table-light text-nowrap">
                  <tr>
                    <th style="min-width: 180px;">{{ getFirstColHeader() }}</th>
                    @if (selectedGroupBy) {
                      <th style="min-width: 140px;">{{ selectedGroupBy }}</th>
                    }
                    @if (selectedBasedOn === 'Supplier') {
                      <th>{{ '::SupplierGroup' | abpLocalization }}</th>
                    } @else if (selectedBasedOn === 'Item') {
                      <th>{{ '::ItemGroup' | abpLocalization }}</th>
                    }
                    @for (col of report()!.periodColumns; track col.key) {
                      <th class="text-end" colspan="2">
                        <div>{{ col.label }}</div>
                        <div class="small fw-normal text-muted d-flex justify-content-between px-1">
                          <span>{{ '::Qty' | abpLocalization }}</span>
                          <span>{{ '::Amt' | abpLocalization }}</span>
                        </div>
                      </th>
                    }
                    <th class="text-end bg-secondary-subtle" colspan="2">
                      <div>{{ '::Total' | abpLocalization }}</div>
                      <div class="small fw-normal text-muted d-flex justify-content-between px-1">
                        <span>{{ '::Qty' | abpLocalization }}</span>
                        <span>{{ '::Amt' | abpLocalization }}</span>
                      </div>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of report()!.rows; track (row.entityId + (row.groupByValue || ''))) {
                    <tr [class.fw-bold]="!row.groupByValue" [class.table-light]="row.groupByValue">
                      <td>
                        @if (!row.groupByValue) {
                          <span>{{ row.entityName }}</span>
                          @if (row.entityCode && row.entityCode !== row.entityName) {
                            <span class="badge bg-light text-muted ms-1">{{ row.entityCode }}</span>
                          }
                        } @else {
                          <span class="text-muted ms-3">—</span>
                        }
                      </td>
                      @if (selectedGroupBy) {
                        <td>
                          @if (row.groupByValue) {
                            <span class="ps-2 text-primary">{{ row.groupByValue }}</span>
                          } @else {
                            <span class="text-muted small">({{ '::All' | abpLocalization }})</span>
                          }
                        </td>
                      }
                      @if (selectedBasedOn === 'Supplier' || selectedBasedOn === 'Item') {
                        <td>
                          @if (!row.groupByValue) {
                            <span class="badge bg-secondary-subtle text-dark">{{ row.secondaryInfo || '—' }}</span>
                          }
                        </td>
                      }
                      @for (col of report()!.periodColumns; track col.key) {
                        <td class="text-end" style="min-width: 70px;">
                          {{ (row.periodQuantities[col.key] || 0) | number:'1.0-2' }}
                        </td>
                        <td class="text-end" style="min-width: 85px;">
                          {{ (row.periodAmounts[col.key] || 0) | number:'1.2-2' }}
                        </td>
                      }
                      <td class="text-end fw-bold bg-secondary-subtle" style="min-width: 75px;">
                        {{ row.totalQty | number:'1.0-2' }}
                      </td>
                      <td class="text-end fw-bold bg-secondary-subtle" style="min-width: 90px;">
                        {{ row.totalAmount | number:'1.2-2' }}
                      </td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-light fw-bold">
                  <tr class="table-info">
                    <td>{{ '::Total' | abpLocalization }}</td>
                    @if (selectedGroupBy) {
                      <td></td>
                    }
                    @if (selectedBasedOn === 'Supplier' || selectedBasedOn === 'Item') {
                      <td></td>
                    }
                    @for (col of report()!.periodColumns; track col.key) {
                      <td class="text-end">
                        {{ (report()!.totalRow.periodQuantities[col.key] || 0) | number:'1.0-2' }}
                      </td>
                      <td class="text-end">
                        {{ (report()!.totalRow.periodAmounts[col.key] || 0) | number:'1.2-2' }}
                      </td>
                    }
                    <td class="text-end bg-secondary-subtle">
                      {{ report()!.totalRow.totalQty | number:'1.0-2' }}
                    </td>
                    <td class="text-end bg-secondary-subtle">
                      {{ report()!.totalRow.totalAmount | number:'1.2-2' }}
                    </td>
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
    .table td, .table th {
      padding: 0.45rem 0.6rem;
    }
  `],
})
export class PurchaseOrderTrendsComponent implements OnInit {
  private trendsService = inject(PurchaseOrderTrendsService);
  private companyContext = inject(CompanyContextService);

  report = signal<PurchaseOrderTrendsReportDto | null>(null);
  loading = signal(false);

  selectedPeriod = 'Monthly';
  selectedBasedOn = 'Supplier';
  selectedGroupBy = '';
  fromDate: string | null = null;
  toDate: string | null = null;
  includeClosedOrders = false;

  ngOnInit(): void {
    const today = new Date();
    const curYear = today.getFullYear();
    this.fromDate = `${curYear}-01-01`;
    this.toDate = `${curYear}-12-31`;

    this.loadReport();
  }

  onBasedOnChange(): void {
    if (this.selectedBasedOn === this.selectedGroupBy) {
      this.selectedGroupBy = '';
    }
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.loading.set(true);

    this.trendsService
      .getReport({
        companyId,
        period: this.selectedPeriod,
        basedOn: this.selectedBasedOn,
        groupBy: this.selectedGroupBy || undefined,
        fromDate: this.fromDate ? `${this.fromDate}T00:00:00Z` : undefined,
        toDate: this.toDate ? `${this.toDate}T23:59:59Z` : undefined,
        includeClosedOrders: this.includeClosedOrders,
      })
      .subscribe({
        next: (res) => {
          this.report.set(res);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }

  getFirstColHeader(): string {
    switch (this.selectedBasedOn) {
      case 'Item':
        return 'Item';
      case 'ItemGroup':
        return 'Item Group';
      default:
        return 'Supplier';
    }
  }

  getBarHeight(val: number): number {
    const chart = this.report()?.chart;
    if (!chart || !chart.datapoints.length) return 4;
    const max = Math.max(...chart.datapoints, 1);
    const height = Math.round((val / max) * 110);
    return Math.max(height, 4);
  }

  exportCsv(): void {
    const r = this.report();
    if (!r || r.rows.length === 0) return;

    const periodCols = r.periodColumns;
    const headers: string[] = [this.getFirstColHeader()];

    if (this.selectedGroupBy) {
      headers.push(this.selectedGroupBy);
    }
    if (this.selectedBasedOn === 'Supplier') {
      headers.push('Supplier Group');
    } else if (this.selectedBasedOn === 'Item') {
      headers.push('Item Group');
    }

    for (const p of periodCols) {
      headers.push(`${p.label} (Qty)`, `${p.label} (Amt)`);
    }
    headers.push('Total (Qty)', 'Total (Amt)');

    const dataRows = r.rows.map((row) => {
      const rowData: Record<string, any> = {};
      rowData[headers[0]] = row.entityName;
      let colIdx = 1;

      if (this.selectedGroupBy) {
        rowData[headers[colIdx++]] = row.groupByValue || '(All)';
      }
      if (this.selectedBasedOn === 'Supplier' || this.selectedBasedOn === 'Item') {
        rowData[headers[colIdx++]] = row.secondaryInfo || '';
      }

      for (const p of periodCols) {
        rowData[`${p.label} (Qty)`] = row.periodQuantities[p.key] || 0;
        rowData[`${p.label} (Amt)`] = row.periodAmounts[p.key] || 0;
      }
      rowData['Total (Qty)'] = row.totalQty;
      rowData['Total (Amt)'] = row.totalAmount;
      return rowData;
    });

    exportToCsv('Purchase_Order_Trends.csv', dataRows, headers);
  }
}
