import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { LostQuotationsService } from '../../../proxy/sales/lost-quotations.service';
import type {
  LostQuotationsReportDto,
  LostQuotationReportRowDto,
} from '../../../proxy/sales/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-lost-quotations',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-chart-pie text-danger me-2"></i>
            {{ '::LostQuotations' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::LostQuotationsDescription' | abpLocalization }}
          </p>
        </div>
        @if (report() && report()!.rows && report()!.rows.length > 0) {
          <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fa fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <!-- Filters -->
      <div class="card mb-3 shadow-sm border-0">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::GroupBy' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="groupBy"
                (ngModelChange)="loadReport()"
              >
                <option value="Lost Reason">{{ '::LostReason' | abpLocalization }}</option>
                <option value="Competitor">{{ '::Competitor' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::Timespan' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="timespan"
                (ngModelChange)="onTimespanChange()"
              >
                <option value="This Year">{{ '::ThisYear' | abpLocalization }}</option>
                <option value="Last Year">{{ '::LastYear' | abpLocalization }}</option>
                <option value="This Month">{{ '::ThisMonth' | abpLocalization }}</option>
                <option value="Last Month">{{ '::LastMonth' | abpLocalization }}</option>
                <option value="This Quarter">{{ '::ThisQuarter' | abpLocalization }}</option>
                <option value="Last Quarter">{{ '::LastQuarter' | abpLocalization }}</option>
                <option value="This Week">{{ '::ThisWeek' | abpLocalization }}</option>
                <option value="Last Week">{{ '::LastWeek' | abpLocalization }}</option>
                <option value="Last 6 months">{{ '::Last6Months' | abpLocalization }}</option>
                <option value="Custom">{{ '::Custom' | abpLocalization }}</option>
              </select>
            </div>
            @if (timespan === 'Custom') {
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
            }
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::Search' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                placeholder="Search..."
                [(ngModel)]="searchTerm"
              />
            </div>
            <div class="col-md d-flex align-items-end">
              <button
                class="btn btn-primary btn-sm ms-auto px-4"
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
        </div>
      </div>

      <!-- KPI Summary Cards -->
      @if (report()) {
        <div class="row g-2 mb-3">
          <div class="col-12 col-md-6">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::TotalLostQuotations' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-primary">
                {{ report()!.summary.totalLostQuotations | number }}
              </div>
            </div>
          </div>
          <div class="col-12 col-md-6">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::TotalLostValue' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-danger">
                {{ report()!.summary.currency }} {{ report()!.summary.totalLostValue | number:'1.2-2' }}
              </div>
            </div>
          </div>
        </div>

        <!-- Top Dimensions Breakdown Bar Chart -->
        @if (report()!.rows.length > 0) {
          <div class="card mb-3 shadow-sm border-0">
            <div class="card-header bg-white py-2">
              <h6 class="mb-0 fw-bold text-muted small">
                {{ (groupBy === 'Lost Reason' ? '::LostReason' : '::Competitor') | abpLocalization }} {{ '::Breakdown' | abpLocalization }}
              </h6>
            </div>
            <div class="card-body py-2">
              @for (row of topRows; track row.dimension) {
                <div class="mb-2">
                  <div class="d-flex justify-content-between small mb-1">
                    <span class="fw-semibold">{{ row.dimension }}</span>
                    <span>
                      <strong class="text-danger">{{ row.currency }} {{ row.lostValue | number:'1.2-2' }}</strong>
                      <span class="text-muted ms-2">({{ row.lostValuePct | number:'1.1-2' }}%)</span>
                    </span>
                  </div>
                  <div class="progress" style="height: 8px;">
                    <div
                      class="progress-bar bg-danger"
                      role="progressbar"
                      [style.width.%]="row.lostValuePct"
                      [attr.aria-valuenow]="row.lostValuePct"
                      aria-valuemin="0"
                      aria-valuemax="100"
                    ></div>
                  </div>
                </div>
              }
            </div>
          </div>
        }
      }

      <!-- Data Table -->
      <div class="card shadow-sm border-0">
        <div class="table-responsive">
          <table class="table table-hover table-striped table-sm mb-0 align-middle">
            <thead class="table-light">
              <tr>
                <th>{{ (groupBy === 'Lost Reason' ? '::LostReason' : '::Competitor') | abpLocalization }}</th>
                <th class="text-end">{{ '::LostQuotationsCount' | abpLocalization }}</th>
                <th class="text-end">{{ '::LostQuotationsPct' | abpLocalization }}</th>
                <th class="text-end">{{ '::LostValue' | abpLocalization }}</th>
                <th class="text-end">{{ '::LostValuePct' | abpLocalization }}</th>
              </tr>
            </thead>
            <tbody>
              @if (isLoading()) {
                <tr>
                  <td colspan="5" class="text-center py-4 text-muted">
                    <span class="spinner-border spinner-border-sm me-2"></span>
                    {{ '::Loading' | abpLocalization }}...
                  </td>
                </tr>
              } @else if (filteredRows.length === 0) {
                <tr>
                  <td colspan="5" class="text-center py-4 text-muted">
                    {{ '::NoLostQuotationsFound' | abpLocalization }}
                  </td>
                </tr>
              } @else {
                @for (row of filteredRows; track row.dimension) {
                  <tr>
                    <td class="fw-semibold">{{ row.dimension }}</td>
                    <td class="text-end">{{ row.lostQuotations | number }}</td>
                    <td class="text-end">{{ row.lostQuotationsPct | number:'1.2-2' }}%</td>
                    <td class="text-end text-danger fw-semibold">{{ row.currency }} {{ row.lostValue | number:'1.2-2' }}</td>
                    <td class="text-end">{{ row.lostValuePct | number:'1.2-2' }}%</td>
                  </tr>
                }
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `,
})
export class LostQuotationsComponent implements OnInit {
  private service = inject(LostQuotationsService);
  private companyContext = inject(CompanyContextService);

  groupBy = 'Lost Reason';
  timespan = 'This Year';
  fromDate = '';
  toDate = '';
  searchTerm = '';

  isLoading = signal(false);
  report = signal<LostQuotationsReportDto | null>(null);

  ngOnInit(): void {
    this.loadReport();
  }

  onTimespanChange(): void {
    if (this.timespan !== 'Custom') {
      this.fromDate = '';
      this.toDate = '';
      this.loadReport();
    }
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);

    this.service
      .getReport({
        companyId,
        groupBy: this.groupBy,
        timespan: this.timespan,
        fromDate: this.timespan === 'Custom' && this.fromDate ? this.fromDate : undefined,
        toDate: this.timespan === 'Custom' && this.toDate ? this.toDate : undefined,
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

  get filteredRows(): LostQuotationReportRowDto[] {
    const r = this.report();
    if (!r || !r.rows) return [];
    if (!this.searchTerm.trim()) return r.rows;

    const term = this.searchTerm.toLowerCase().trim();
    return r.rows.filter((row) =>
      row.dimension?.toLowerCase().includes(term)
    );
  }

  get topRows(): LostQuotationReportRowDto[] {
    return this.filteredRows.slice(0, 5);
  }

  exportCsv(): void {
    const rows = this.filteredRows;
    if (rows.length === 0) return;

    const exportData = rows.map((r) => ({
      Dimension: r.dimension,
      LostQuotations: r.lostQuotations,
      LostQuotationsPct: `${r.lostQuotationsPct}%`,
      LostValue: r.lostValue,
      LostValuePct: `${r.lostValuePct}%`,
      Currency: r.currency,
    }));

    exportToCsv(
      `Lost-Quotations-${this.groupBy.replace(/\s+/g, '-')}-${new Date().toISOString().substring(0, 10)}`,
      exportData,
      ['Dimension', 'LostQuotations', 'LostQuotationsPct', 'LostValue', 'LostValuePct', 'Currency']
    );
  }
}
