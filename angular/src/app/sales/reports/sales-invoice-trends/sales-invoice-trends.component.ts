import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { SalesInvoiceTrendsService } from '../../../proxy/sales/sales-invoice-trends.service';
import type {
  SalesInvoiceTrendsReportDto,
  SalesInvoiceTrendsRowDto,
} from '../../../proxy/sales/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-sales-invoice-trends',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-chart-line text-primary me-2"></i>
            {{ '::SalesInvoiceTrends' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::SalesInvoiceTrendsDescription' | abpLocalization }}
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
              [class.btn-primary]="valueType() === 'amount'"
              [class.btn-outline-primary]="valueType() !== 'amount'"
              (click)="valueType.set('amount')"
            >
              <i class="fa fa-money-bill-wave me-1"></i>{{ '::Amount' | abpLocalization }} ({{ report()!.companyCurrency }})
            </button>
            <button
              type="button"
              class="btn"
              [class.btn-primary]="valueType() === 'qty'"
              [class.btn-outline-primary]="valueType() !== 'qty'"
              (click)="valueType.set('qty')"
            >
              <i class="fa fa-cubes me-1"></i>{{ '::Quantity' | abpLocalization }}
            </button>
          </div>
          <span class="text-muted small">
            {{ report()!.rows.length }} {{ '::Records' | abpLocalization }}
          </span>
        </div>

        <!-- Mini Visual Trend Bar Chart -->
        <div class="card mb-3 shadow-sm border-0">
          <div class="card-body py-3">
            <div class="d-flex align-items-end gap-2" style="height: 100px;">
              @for (col of report()!.periodColumns; track col.key) {
                <div class="flex-fill d-flex flex-column align-items-center h-100 justify-content-end">
                  <div class="small fw-semibold text-primary mb-1">
                    @if (valueType() === 'amount') {
                      {{ report()!.totalRow.periodAmounts[col.key] || 0 | number: '1.0-0' }}
                    } @else {
                      {{ report()!.totalRow.periodQuantities[col.key] || 0 | number: '1.0-0' }}
                    }
                  </div>
                  <div
                    class="w-100 bg-primary bg-opacity-75 rounded-top"
                    [style.height.%]="getBarHeightPercent(col.key)"
                    style="min-height: 4px;"
                  ></div>
                  <div class="text-muted text-truncate w-100 text-center mt-1" style="font-size: 0.7rem;">
                    {{ col.label }}
                  </div>
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
              <div class="spinner-border text-primary" role="status"></div>
              <div class="text-muted small mt-2">{{ '::Loading' | abpLocalization }}...</div>
            </div>
          } @else if (!report() || report()!.rows.length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-folder-open fa-2x mb-2 text-secondary opacity-50"></i>
              <p class="mb-0">{{ '::NoSalesInvoiceTrendsFound' | abpLocalization }}</p>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-sm table-hover align-middle mb-0">
                <thead class="table-light text-muted small text-uppercase">
                  <tr>
                    <th>{{ selectedBasedOnLabel() }}</th>
                    @if (selectedBasedOn === 'Customer') {
                      <th>{{ '::Territory' | abpLocalization }}</th>
                    } @else if (selectedBasedOn === 'Item') {
                      <th>{{ '::ItemGroup' | abpLocalization }}</th>
                    }
                    @if (selectedGroupBy) {
                      <th>{{ selectedGroupByLabel() }}</th>
                    }
                    @for (col of report()!.periodColumns; track col.key) {
                      <th class="text-end">{{ col.label }}</th>
                    }
                    <th class="text-end fw-bold">{{ '::Total' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody class="small">
                  @for (row of report()!.rows; track row.entityId + '-' + (row.groupByValue || '')) {
                    <tr [class.table-light]="row.groupByValue">
                      <td [class.ps-4]="row.groupByValue" class="fw-semibold">
                        {{ row.entityName }}
                        @if (row.entityCode && row.entityCode !== row.entityName && !row.groupByValue) {
                          <span class="text-muted small ms-1">({{ row.entityCode }})</span>
                        }
                      </td>
                      @if (selectedBasedOn === 'Customer' || selectedBasedOn === 'Item') {
                        <td class="text-muted">{{ row.secondaryInfo || '—' }}</td>
                      }
                      @if (selectedGroupBy) {
                        <td>{{ row.groupByValue || '—' }}</td>
                      }
                      @for (col of report()!.periodColumns; track col.key) {
                        <td class="text-end">
                          @if (valueType() === 'amount') {
                            {{ row.periodAmounts[col.key] || 0 | number: '1.2-2' }}
                          } @else {
                            {{ row.periodQuantities[col.key] || 0 | number: '1.2-2' }}
                          }
                        </td>
                      }
                      <td class="text-end fw-bold text-primary">
                        @if (valueType() === 'amount') {
                          {{ row.totalAmount | number: '1.2-2' }}
                        } @else {
                          {{ row.totalQty | number: '1.2-2' }}
                        }
                      </td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-light fw-bold small">
                  <tr>
                    <td>{{ '::Total' | abpLocalization }}</td>
                    @if (selectedBasedOn === 'Customer' || selectedBasedOn === 'Item') {
                      <td></td>
                    }
                    @if (selectedGroupBy) {
                      <td></td>
                    }
                    @for (col of report()!.periodColumns; track col.key) {
                      <td class="text-end">
                        @if (valueType() === 'amount') {
                          {{ report()!.totalRow.periodAmounts[col.key] || 0 | number: '1.2-2' }}
                        } @else {
                          {{ report()!.totalRow.periodQuantities[col.key] || 0 | number: '1.2-2' }}
                        }
                      </td>
                    }
                    <td class="text-end text-primary">
                      @if (valueType() === 'amount') {
                        {{ report()!.totalRow.totalAmount | number: '1.2-2' }}
                      } @else {
                        {{ report()!.totalRow.totalQty | number: '1.2-2' }}
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
})
export class SalesInvoiceTrendsComponent implements OnInit {
  private reportService = inject(SalesInvoiceTrendsService);
  private companyContext = inject(CompanyContextService);

  report = signal<SalesInvoiceTrendsReportDto | null>(null);
  loading = signal<boolean>(false);
  valueType = signal<'amount' | 'qty'>('amount');

  selectedPeriod = 'Monthly';
  selectedBasedOn = 'Customer';
  selectedGroupBy = '';
  includeReturns = false;
  fromDate = '';
  toDate = '';

  selectedBasedOnLabel = computed(() => {
    switch (this.selectedBasedOn) {
      case 'Item': return 'Item';
      case 'ItemGroup': return 'Item Group';
      default: return 'Customer';
    }
  });

  selectedGroupByLabel = computed(() => {
    switch (this.selectedGroupBy) {
      case 'Item': return 'Item';
      case 'Customer': return 'Customer';
      default: return '';
    }
  });

  ngOnInit(): void {
    const now = new Date();
    const y = now.getFullYear();
    this.fromDate = `${y}-01-01`;
    this.toDate = `${y}-12-31`;
    this.loadReport();
  }

  onBasedOnChange(): void {
    if (this.selectedGroupBy === this.selectedBasedOn) {
      this.selectedGroupBy = '';
    }
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.loading.set(true);
    this.reportService
      .getReport({
        companyId,
        period: this.selectedPeriod,
        basedOn: this.selectedBasedOn,
        groupBy: this.selectedGroupBy || null,
        includeReturns: this.includeReturns,
        fromDate: this.fromDate ? `${this.fromDate}T00:00:00Z` : null,
        toDate: this.toDate ? `${this.toDate}T23:59:59Z` : null,
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

  getBarHeightPercent(colKey: string): number {
    const rep = this.report();
    if (!rep) return 0;
    const isAmount = this.valueType() === 'amount';
    const values = rep.periodColumns.map((c) =>
      isAmount ? rep.totalRow.periodAmounts[c.key] || 0 : rep.totalRow.periodQuantities[c.key] || 0
    );
    const maxVal = Math.max(...values, 0);
    if (maxVal <= 0) return 4;
    const current = isAmount ? rep.totalRow.periodAmounts[colKey] || 0 : rep.totalRow.periodQuantities[colKey] || 0;
    return Math.max(4, Math.round((current / maxVal) * 100));
  }

  exportCsv(): void {
    const rep = this.report();
    if (!rep || rep.rows.length === 0) return;

    const columns: (keyof SalesInvoiceTrendsRowDto)[] = [
      'entityCode',
      'entityName',
      'secondaryInfo',
      'groupByValue',
      'totalQty',
      'totalAmount',
    ];

    exportToCsv(`sales_invoice_trends_${this.selectedPeriod.toLowerCase()}_${this.selectedBasedOn.toLowerCase()}.csv`, rep.rows, columns as string[]);
  }
}
