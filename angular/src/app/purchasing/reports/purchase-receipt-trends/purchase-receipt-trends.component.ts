import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { PurchaseReceiptTrendsService } from '../../../proxy/purchasing/purchase-receipt-trends.service';
import type {
  PurchaseReceiptTrendsReportDto,
  PurchaseReceiptTrendsRowDto,
} from '../../../proxy/purchasing/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-purchase-receipt-trends',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-boxes-packing text-primary me-2"></i>
            {{ '::PurchaseReceiptTrends' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::PurchaseReceiptTrendsDescription' | abpLocalization }}
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
                {{ '::Refresh' | abpLocalization }}
              </button>
            </div>
          </div>

          <div class="row g-2 align-items-center mt-2 pt-2 border-top">
            <div class="col-auto">
              <span class="small text-muted me-2">{{ '::View' | abpLocalization }}:</span>
              <div class="btn-group btn-group-sm" role="group">
                <button
                  type="button"
                  class="btn"
                  [ngClass]="valueView === 'Amount' ? 'btn-primary' : 'btn-outline-secondary'"
                  (click)="valueView = 'Amount'"
                >
                  {{ '::Amount' | abpLocalization }}
                </button>
                <button
                  type="button"
                  class="btn"
                  [ngClass]="valueView === 'Qty' ? 'btn-primary' : 'btn-outline-secondary'"
                  (click)="valueView = 'Qty'"
                >
                  {{ '::Quantity' | abpLocalization }}
                </button>
                <button
                  type="button"
                  class="btn"
                  [ngClass]="valueView === 'Both' ? 'btn-primary' : 'btn-outline-secondary'"
                  (click)="valueView = 'Both'"
                >
                  {{ '::Both' | abpLocalization }}
                </button>
              </div>
            </div>
            <div class="col-auto ms-auto">
              <div class="form-check form-switch mb-0">
                <input
                  class="form-check-input"
                  type="checkbox"
                  id="includeReturnsSwitch"
                  [(ngModel)]="includeReturns"
                  (ngModelChange)="loadReport()"
                />
                <label class="form-check-label small" for="includeReturnsSwitch">
                  {{ '::IncludeReturns' | abpLocalization }}
                </label>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- KPI Summary Cards -->
      @if (report()) {
        <div class="row g-2 mb-3">
          <div class="col-12 col-md-3">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::TotalReceivedAmount' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-success">
                {{ report()!.totalRow.totalAmount | number:'1.2-2' }} {{ report()!.companyCurrency }}
              </div>
            </div>
          </div>
          <div class="col-6 col-md-3">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::TotalReceivedQty' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-primary">
                {{ report()!.totalRow.totalQty | number:'1.0-2' }}
              </div>
            </div>
          </div>
          <div class="col-6 col-md-3">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::Periods' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-dark">
                {{ report()!.periodColumns.length }}
              </div>
            </div>
          </div>
          <div class="col-12 col-md-3">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::Rows' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-dark">
                {{ report()!.rows.length }}
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
              <i class="fa fa-chart-column text-info me-2"></i>{{ '::DeliveryTrendsSummary' | abpLocalization }}
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
                    class="bg-info rounded-top w-100"
                    [style.height.px]="getBarHeight(point)"
                    style="min-height: 6px; max-width: 48px; opacity: 0.85;"
                  ></div>
                  <span class="small text-muted mt-2 text-truncate" style="max-width: 80px;">
                    {{ report()!.chart.labels[$index] }}
                  </span>
                </div>
              }
            </div>
          </div>
        </div>
      }

      <!-- Main Data Table -->
      <div class="card shadow-sm border-0">
        <div class="card-body p-0">
          @if (isLoading()) {
            <div class="text-center py-5">
              <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">{{ '::Loading' | abpLocalization }}...</span>
              </div>
            </div>
          } @else if (!report() || report()!.rows.length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-chart-line fa-3x mb-2 text-secondary"></i>
              <p>{{ '::NoPurchaseReceiptTrendsFound' | abpLocalization }}</p>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-sm table-hover align-middle mb-0 font-monospace">
                <thead class="table-light">
                  <tr>
                    <th style="min-width: 220px;" class="font-sans-serif">
                      {{ selectedBasedOn === 'Supplier' ? ('::Supplier' | abpLocalization) : selectedBasedOn === 'Item' ? ('::Item' | abpLocalization) : ('::ItemGroup' | abpLocalization) }}
                    </th>
                    @if (selectedGroupBy) {
                      <th style="min-width: 140px;" class="font-sans-serif">
                        {{ selectedGroupBy === 'Item' ? ('::Item' | abpLocalization) : ('::Supplier' | abpLocalization) }}
                      </th>
                    }
                    @for (col of report()!.periodColumns; track col.key) {
                      <th class="text-end font-sans-serif" style="min-width: 100px;">
                        {{ col.label }}
                      </th>
                    }
                    <th class="text-end bg-light fw-bold font-sans-serif" style="min-width: 120px;">
                      {{ '::Total' | abpLocalization }}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of filteredRows(); track $index) {
                    <tr [class.table-light]="!row.groupByValue && selectedGroupBy" [class.fw-bold]="!row.groupByValue && selectedGroupBy">
                      <td class="font-sans-serif">
                        @if (row.groupByValue) {
                          <span class="ps-3 text-muted"><i class="fa fa-angle-right me-1"></i></span>
                        }
                        <span>{{ row.entityName }}</span>
                        @if (row.secondaryInfo && !row.groupByValue) {
                          <small class="text-muted d-block font-sans-serif">{{ row.secondaryInfo }}</small>
                        }
                      </td>
                      @if (selectedGroupBy) {
                        <td class="font-sans-serif">
                          <span class="text-muted small">{{ row.groupByValue || '-' }}</span>
                        </td>
                      }
                      @for (col of report()!.periodColumns; track col.key) {
                        <td class="text-end">
                          @if (valueView === 'Amount' || valueView === 'Both') {
                            <div>{{ row.periodAmounts[col.key] | number:'1.2-2' }}</div>
                          }
                          @if (valueView === 'Qty' || valueView === 'Both') {
                            <div class="text-muted small">({{ row.periodQuantities[col.key] | number:'1.0-2' }})</div>
                          }
                        </td>
                      }
                      <td class="text-end bg-light fw-bold">
                        @if (valueView === 'Amount' || valueView === 'Both') {
                          <div class="text-success">{{ row.totalAmount | number:'1.2-2' }}</div>
                        }
                        @if (valueView === 'Qty' || valueView === 'Both') {
                          <div class="text-muted small">({{ row.totalQty | number:'1.0-2' }})</div>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-dark">
                  <tr>
                    <td class="fw-bold font-sans-serif" [attr.colspan]="selectedGroupBy ? 2 : 1">
                      {{ '::GrandTotal' | abpLocalization }}
                    </td>
                    @for (col of report()!.periodColumns; track col.key) {
                      <td class="text-end">
                        @if (valueView === 'Amount' || valueView === 'Both') {
                          <div>{{ report()!.totalRow.periodAmounts[col.key] | number:'1.2-2' }}</div>
                        }
                        @if (valueView === 'Qty' || valueView === 'Both') {
                          <div class="text-light small">({{ report()!.totalRow.periodQuantities[col.key] | number:'1.0-2' }})</div>
                        }
                      </td>
                    }
                    <td class="text-end fw-bold text-warning">
                      @if (valueView === 'Amount' || valueView === 'Both') {
                        <div>{{ report()!.totalRow.totalAmount | number:'1.2-2' }}</div>
                      }
                      @if (valueView === 'Qty' || valueView === 'Both') {
                        <div class="small">({{ report()!.totalRow.totalQty | number:'1.0-2' }})</div>
                      }
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
  styles: [
    `
      .font-sans-serif {
        font-family: var(--bs-body-font-family, system-ui, -apple-system, sans-serif);
      }
    `,
  ],
})
export class PurchaseReceiptTrendsComponent implements OnInit {
  private trendsService = inject(PurchaseReceiptTrendsService);
  private companyContext = inject(CompanyContextService);

  report = signal<PurchaseReceiptTrendsReportDto | null>(null);
  isLoading = signal<boolean>(false);

  selectedPeriod = 'Monthly';
  selectedBasedOn = 'Supplier';
  selectedGroupBy = '';
  fromDate = '';
  toDate = '';
  includeReturns = false;
  valueView: 'Amount' | 'Qty' | 'Both' = 'Amount';

  filteredRows = computed(() => {
    return this.report()?.rows ?? [];
  });

  ngOnInit() {
    this.initDefaultDates();
    this.loadReport();
  }

  initDefaultDates() {
    const today = new Date();
    const startOfYear = new Date(today.getFullYear(), 0, 1);
    this.fromDate = startOfYear.toISOString().substring(0, 10);
    this.toDate = today.toISOString().substring(0, 10);
  }

  onBasedOnChange() {
    if (this.selectedGroupBy === this.selectedBasedOn) {
      this.selectedGroupBy = '';
    }
    this.loadReport();
  }

  loadReport() {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);

    this.trendsService
      .getReport({
        companyId,
        period: this.selectedPeriod,
        basedOn: this.selectedBasedOn,
        groupBy: this.selectedGroupBy || undefined,
        fromDate: this.fromDate ? new Date(this.fromDate).toISOString() : undefined,
        toDate: this.toDate ? new Date(this.toDate).toISOString() : undefined,
        includeReturns: this.includeReturns,
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

  exportCsv() {
    const r = this.report();
    if (!r || r.rows.length === 0) return;

    const baseHeaders = [r.basedOn];
    if (r.groupBy) baseHeaders.push(r.groupBy);

    const periodHeaders = r.periodColumns.map(p => p.label);
    const columns = [...baseHeaders, ...periodHeaders, 'Total Amount', 'Total Qty'];

    const exportData = r.rows.map(row => {
      const obj: Record<string, any> = {
        [r.basedOn]: row.entityName,
      };
      if (r.groupBy) {
        obj[r.groupBy] = row.groupByValue || '';
      }
      r.periodColumns.forEach(p => {
        obj[p.label] = `${row.periodAmounts[p.key] || 0} (${row.periodQuantities[p.key] || 0})`;
      });
      obj['Total Amount'] = row.totalAmount;
      obj['Total Qty'] = row.totalQty;
      return obj;
    });

    exportToCsv(`purchase_receipt_trends_${r.period.toLowerCase()}.csv`, exportData, columns);
  }
}
