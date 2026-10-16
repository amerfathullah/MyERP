import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { SalesOrderTrendsService } from '../../../proxy/sales/sales-order-trends.service';
import type {
  SalesOrderTrendsReportDto,
  SalesOrderTrendsRowDto,
} from '../../../proxy/sales/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-sales-order-trends',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-chart-line text-primary me-2"></i>
            {{ '::SalesOrderTrends' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::SalesOrderTrendsDescription' | abpLocalization }}
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
                <option value="Customer">{{ '::Customer' | abpLocalization }}</option>
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
                @if (selectedBasedOn !== 'Customer') {
                  <option value="Customer">{{ '::Customer' | abpLocalization }}</option>
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
                  id="includeClosedOrdersSwitch"
                  [(ngModel)]="includeClosedOrders"
                  (ngModelChange)="loadReport()"
                />
                <label class="form-check-label small" for="includeClosedOrdersSwitch">
                  {{ '::IncludeClosedOrders' | abpLocalization }}
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
              <div class="text-muted small">{{ '::TotalAmount' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-success">
                {{ report()!.totalRow.totalAmount | number:'1.2-2' }} {{ report()!.companyCurrency }}
              </div>
            </div>
          </div>
          <div class="col-6 col-md-3">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::TotalQty' | abpLocalization }}</div>
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

      <!-- Loading State -->
      @if (isLoading()) {
        <div class="text-center py-5">
          <div class="spinner-border text-primary" role="status">
            <span class="visually-hidden">Loading...</span>
          </div>
          <div class="text-muted mt-2 small">{{ '::Loading' | abpLocalization }}...</div>
        </div>
      } @else if (!report() || report()!.rows.length === 0) {
        <div class="alert alert-info text-center my-4 py-4 shadow-sm border-0">
          <i class="fa fa-info-circle fa-2x mb-2 text-info"></i>
          <p class="mb-0 fw-medium">{{ '::NoTrendsDataFound' | abpLocalization }}</p>
        </div>
      } @else {
        <!-- Data Table -->
        <div class="card shadow-sm border-0">
          <div class="table-responsive" style="max-height: 600px;">
            <table class="table table-hover table-striped table-bordered align-middle mb-0 text-nowrap">
              <thead class="table-light sticky-top">
                <tr>
                  <th scope="col" style="min-width: 180px;">{{ getBasedOnHeader() }}</th>
                  <th scope="col" style="min-width: 140px;">{{ getSecondaryHeader() }}</th>
                  @if (report()!.groupBy) {
                    <th scope="col" style="min-width: 160px;">{{ report()!.groupBy }}</th>
                  }
                  @for (col of report()!.periodColumns; track col.key) {
                    @if (valueView === 'Amount' || valueView === 'Both') {
                      <th scope="col" class="text-end" style="min-width: 110px;">
                        {{ col.label }} @if (valueView === 'Both') { (Amt) }
                      </th>
                    }
                    @if (valueView === 'Qty' || valueView === 'Both') {
                      <th scope="col" class="text-end" style="min-width: 90px;">
                        {{ col.label }} @if (valueView === 'Both') { (Qty) }
                      </th>
                    }
                  }
                  <th scope="col" class="text-end fw-bold" style="min-width: 110px;">{{ '::TotalQty' | abpLocalization }}</th>
                  <th scope="col" class="text-end fw-bold" style="min-width: 130px;">{{ '::TotalAmount' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of report()!.rows; track (row.entityId + (row.groupByValue || ''))) {
                  <tr [ngClass]="row.groupByValue ? 'table-light' : ''">
                    <td [class.ps-4]="row.groupByValue">
                      @if (row.groupByValue) {
                        <i class="fa fa-angle-right text-muted me-1"></i>
                      }
                      <span class="fw-semibold">{{ row.entityName }}</span>
                      @if (row.entityCode && row.entityCode !== row.entityName) {
                        <small class="text-muted d-block">{{ row.entityCode }}</small>
                      }
                    </td>
                    <td class="text-muted small">{{ row.secondaryInfo || '—' }}</td>
                    @if (report()!.groupBy) {
                      <td>
                        @if (row.groupByValue) {
                          <span class="badge bg-secondary">{{ row.groupByValue }}</span>
                        } @else {
                          <span class="text-muted">—</span>
                        }
                      </td>
                    }
                    @for (col of report()!.periodColumns; track col.key) {
                      @if (valueView === 'Amount' || valueView === 'Both') {
                        <td class="text-end">
                          @if ((row.periodAmounts[col.key] || 0) > 0) {
                            {{ row.periodAmounts[col.key] | number:'1.2-2' }}
                          } @else {
                            <span class="text-muted">0.00</span>
                          }
                        </td>
                      }
                      @if (valueView === 'Qty' || valueView === 'Both') {
                        <td class="text-end">
                          @if ((row.periodQuantities[col.key] || 0) > 0) {
                            {{ row.periodQuantities[col.key] | number:'1.0-2' }}
                          } @else {
                            <span class="text-muted">0</span>
                          }
                        </td>
                      }
                    }
                    <td class="text-end fw-bold">{{ row.totalQty | number:'1.0-2' }}</td>
                    <td class="text-end fw-bold text-success">{{ row.totalAmount | number:'1.2-2' }}</td>
                  </tr>
                }
              </tbody>
              <tfoot class="table-dark sticky-bottom">
                <tr>
                  <td class="fw-bold">{{ '::Total' | abpLocalization }}</td>
                  <td></td>
                  @if (report()!.groupBy) {
                    <td></td>
                  }
                  @for (col of report()!.periodColumns; track col.key) {
                    @if (valueView === 'Amount' || valueView === 'Both') {
                      <td class="text-end fw-bold">
                        {{ report()!.totalRow.periodAmounts[col.key] | number:'1.2-2' }}
                      </td>
                    }
                    @if (valueView === 'Qty' || valueView === 'Both') {
                      <td class="text-end fw-bold">
                        {{ report()!.totalRow.periodQuantities[col.key] | number:'1.0-2' }}
                      </td>
                    }
                  }
                  <td class="text-end fw-bold">{{ report()!.totalRow.totalQty | number:'1.0-2' }}</td>
                  <td class="text-end fw-bold text-success">{{ report()!.totalRow.totalAmount | number:'1.2-2' }}</td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>
      }
    </div>
  `,
})
export class SalesOrderTrendsComponent implements OnInit {
  private trendsService = inject(SalesOrderTrendsService);
  private companyContext = inject(CompanyContextService);

  report = signal<SalesOrderTrendsReportDto | null>(null);
  isLoading = signal(false);

  selectedPeriod = 'Monthly';
  selectedBasedOn = 'Customer';
  selectedGroupBy = '';
  includeClosedOrders = false;
  valueView: 'Amount' | 'Qty' | 'Both' = 'Amount';

  fromDate = `${new Date().getFullYear()}-01-01`;
  toDate = `${new Date().getFullYear()}-12-31`;

  ngOnInit(): void {
    this.loadReport();
  }

  onBasedOnChange(): void {
    if (this.selectedGroupBy === this.selectedBasedOn) {
      this.selectedGroupBy = '';
    }
    this.loadReport();
  }

  getBasedOnHeader(): string {
    switch (this.selectedBasedOn) {
      case 'Item': return 'Item';
      case 'ItemGroup': return 'Item Group';
      default: return 'Customer';
    }
  }

  getSecondaryHeader(): string {
    switch (this.selectedBasedOn) {
      case 'Item': return 'Item Group';
      case 'ItemGroup': return '—';
      default: return 'Territory';
    }
  }

  loadReport(): void {
    this.isLoading.set(true);
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) {
      this.isLoading.set(false);
      return;
    }

    this.trendsService
      .getReport({
        companyId,
        period: this.selectedPeriod,
        basedOn: this.selectedBasedOn,
        groupBy: this.selectedGroupBy || null,
        includeClosedOrders: this.includeClosedOrders,
        fromDate: this.fromDate ? `${this.fromDate}T00:00:00Z` : null,
        toDate: this.toDate ? `${this.toDate}T23:59:59Z` : null,
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

  exportCsv(): void {
    const r = this.report();
    if (!r || !r.rows.length) return;

    const columns: string[] = [
      this.getBasedOnHeader(),
      this.getSecondaryHeader(),
    ];

    if (r.groupBy) {
      columns.push(r.groupBy);
    }

    for (const col of r.periodColumns) {
      if (this.valueView === 'Amount' || this.valueView === 'Both') {
        columns.push(`${col.label} (Amt)`);
      }
      if (this.valueView === 'Qty' || this.valueView === 'Both') {
        columns.push(`${col.label} (Qty)`);
      }
    }

    columns.push('Total Qty', 'Total Amount');

    const data = r.rows.map(row => {
      const obj: Record<string, any> = {
        [this.getBasedOnHeader()]: row.entityName,
        [this.getSecondaryHeader()]: row.secondaryInfo || '',
      };

      if (r.groupBy) {
        obj[r.groupBy] = row.groupByValue || '';
      }

      for (const col of r.periodColumns) {
        if (this.valueView === 'Amount' || this.valueView === 'Both') {
          obj[`${col.label} (Amt)`] = row.periodAmounts[col.key] || 0;
        }
        if (this.valueView === 'Qty' || this.valueView === 'Both') {
          obj[`${col.label} (Qty)`] = row.periodQuantities[col.key] || 0;
        }
      }

      obj['Total Qty'] = row.totalQty;
      obj['Total Amount'] = row.totalAmount;

      return obj;
    });

    exportToCsv(
      `sales-order-trends-${new Date().toISOString().slice(0, 10)}.csv`,
      data,
      columns
    );
  }
}
