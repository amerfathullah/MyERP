import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type { FinancialRatiosReportDto, FinancialRatioRowDto } from '../../../proxy/accounting/models';

@Component({
  selector: 'app-financial-ratios',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">
            <i class="fas fa-percent me-2 text-primary"></i>
            {{ '::FinancialRatios' | abpLocalization }}
          </h5>
          @if (report()) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="fas fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
        <div class="card-body">
          <!-- Filters -->
          <div class="row g-2 mb-3 align-items-end">
            <div class="col-md-3">
              <label class="form-label small">{{ '::From' | abpLocalization }}</label>
              <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" (change)="generate()" />
            </div>
            <div class="col-md-3">
              <label class="form-label small">{{ '::To' | abpLocalization }}</label>
              <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" (change)="generate()" />
            </div>
            <div class="col-md-3">
              <div class="form-check mt-3">
                <input class="form-check-input" type="checkbox" id="chkComparison" [(ngModel)]="includeComparison" (change)="generate()" />
                <label class="form-check-label small" for="chkComparison">
                  {{ '::ComparePreviousPeriod' | abpLocalization }}
                </label>
              </div>
            </div>
            <div class="col-md-3 text-end">
              <button class="btn btn-sm btn-primary" (click)="generate()" [disabled]="isLoading()">
                @if (isLoading()) {
                  <span class="spinner-border spinner-border-sm me-1"></span>
                } @else {
                  <i class="fas fa-sync-alt me-1"></i>
                }
                {{ '::Generate' | abpLocalization }}
              </button>
            </div>
          </div>

          @if (isLoading()) {
            <div class="text-center py-5">
              <div class="spinner-border text-primary"></div>
            </div>
          }

          @if (report(); as r) {
            <!-- Top KPI Cards -->
            <div class="row g-3 mb-4">
              <div class="col-md-3">
                <div class="card border-start border-primary border-4 shadow-sm">
                  <div class="card-body py-2">
                    <small class="text-muted">{{ '::CurrentRatio' | abpLocalization }}</small>
                    <div class="fs-4 fw-bold text-primary">{{ formatRatio(getRatioValue('Current Ratio'), 'ratio') }}</div>
                    <small class="text-muted">{{ '::Benchmark' | abpLocalization }}: > 1.5</small>
                  </div>
                </div>
              </div>
              <div class="col-md-3">
                <div class="card border-start border-success border-4 shadow-sm">
                  <div class="card-body py-2">
                    <small class="text-muted">{{ '::NetProfitMargin' | abpLocalization }}</small>
                    <div class="fs-4 fw-bold text-success">{{ formatRatio(getRatioValue('Net Profit Margin'), '%') }}</div>
                    <small class="text-muted">{{ '::Benchmark' | abpLocalization }}: > 10%</small>
                  </div>
                </div>
              </div>
              <div class="col-md-3">
                <div class="card border-start border-warning border-4 shadow-sm">
                  <div class="card-body py-2">
                    <small class="text-muted">{{ '::DebtEquityRatio' | abpLocalization }}</small>
                    <div class="fs-4 fw-bold text-warning">{{ formatRatio(getRatioValue('Debt to Equity Ratio'), 'ratio') }}</div>
                    <small class="text-muted">{{ '::Benchmark' | abpLocalization }}: < 2.0</small>
                  </div>
                </div>
              </div>
              <div class="col-md-3">
                <div class="card border-start border-info border-4 shadow-sm">
                  <div class="card-body py-2">
                    <small class="text-muted">{{ '::ReturnOnEquity' | abpLocalization }}</small>
                    <div class="fs-4 fw-bold text-info">{{ formatRatio(getRatioValue('Return on Equity (ROE)'), '%') }}</div>
                    <small class="text-muted">{{ '::Benchmark' | abpLocalization }}: > 15%</small>
                  </div>
                </div>
              </div>
            </div>

            <!-- Ratio Category Tables -->
            @for (cat of categories; track cat) {
              <div class="card mb-3">
                <div class="card-header bg-light py-2">
                  <h6 class="mb-0 fw-bold">
                    <i [class]="getCategoryIcon(cat)" class="me-2 text-secondary"></i>
                    {{ getCategoryLabel(cat) | abpLocalization }}
                  </h6>
                </div>
                <div class="table-responsive">
                  <table class="table table-sm table-hover align-middle mb-0">
                    <thead class="table-light">
                      <tr>
                        <th style="width: 25%">{{ '::Ratio' | abpLocalization }}</th>
                        <th class="text-end" style="width: 15%">{{ '::CurrentPeriod' | abpLocalization }}</th>
                        @if (includeComparison) {
                          <th class="text-end" style="width: 15%">{{ '::PreviousPeriod' | abpLocalization }}</th>
                          <th class="text-end" style="width: 12%">{{ '::ChangePercent' | abpLocalization }}</th>
                        }
                        <th style="width: 20%">{{ '::RatioFormula' | abpLocalization }}</th>
                        <th style="width: 13%">{{ '::Interpretation' | abpLocalization }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (row of getRowsByCategory(cat); track row.ratioName) {
                        <tr>
                          <td class="fw-medium">{{ row.ratioName }}</td>
                          <td class="text-end fw-bold">
                            {{ formatRatio(row.value, row.unit) }}
                          </td>
                          @if (includeComparison) {
                            <td class="text-end text-muted">
                              {{ formatRatio(row.previousValue, row.unit) }}
                            </td>
                            <td class="text-end">
                              @if (row.changePercentage !== null) {
                                @if (row.changePercentage > 0) {
                                  <span class="badge bg-success-subtle text-success">
                                    <i class="fas fa-arrow-up me-1"></i>{{ row.changePercentage }}%
                                  </span>
                                } @else if (row.changePercentage < 0) {
                                  <span class="badge bg-danger-subtle text-danger">
                                    <i class="fas fa-arrow-down me-1"></i>{{ row.changePercentage }}%
                                  </span>
                                } @else {
                                  <span class="text-muted">0%</span>
                                }
                              } @else {
                                <span class="text-muted">—</span>
                              }
                            </td>
                          }
                          <td><small class="text-muted font-monospace">{{ row.formula }}</small></td>
                          <td><small class="text-muted">{{ row.description }}</small></td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            }
          }

          @if (!report() && !isLoading()) {
            <div class="text-center py-5 text-muted">
              <i class="fas fa-chart-pie fa-3x mb-3 text-secondary"></i>
              <p>{{ '::ClickGenerateToViewReport' | abpLocalization }}</p>
            </div>
          }
        </div>
      </div>
    </div>
  `,
})
export class FinancialRatiosComponent implements OnInit {
  private reportingService = inject(ReportingService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  report = signal<FinancialRatiosReportDto | null>(null);
  isLoading = signal(false);

  fromDate = '';
  toDate = '';
  includeComparison = true;

  categories = [
    'Liquidity Ratios',
    'Solvency & Profitability',
    'Turnover Ratios',
  ];

  ngOnInit(): void {
    const now = new Date();
    const yearStart = new Date(now.getFullYear(), 0, 1);
    this.fromDate = yearStart.toISOString().split('T')[0];
    this.toDate = now.toISOString().split('T')[0];

    this.companyContext.load();
    setTimeout(() => this.generate(), 200);
  }

  generate(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId || !this.fromDate || !this.toDate) return;

    this.isLoading.set(true);
    this.reportingService.getFinancialRatios({
      companyId,
      fromDate: this.fromDate,
      toDate: this.toDate,
      includeComparison: this.includeComparison,
    }).subscribe({
      next: (data) => {
        this.report.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.toaster.error('::FailedToGenerateReport');
      },
    });
  }

  getRowsByCategory(category: string): FinancialRatioRowDto[] {
    const r = this.report();
    if (!r?.rows) return [];
    return r.rows.filter(row => row.category === category);
  }

  getRatioValue(ratioName: string): number | null | undefined {
    const r = this.report();
    return r?.rows?.find(x => x.ratioName === ratioName)?.value;
  }

  formatRatio(val: number | null | undefined, unit?: string): string {
    if (val == null) return '—';
    if (unit === '%') return `${val.toFixed(2)}%`;
    if (unit === 'times') return `${val.toFixed(2)}x`;
    return val.toFixed(2);
  }

  getCategoryIcon(cat: string): string {
    switch (cat) {
      case 'Liquidity Ratios': return 'fas fa-tint';
      case 'Solvency & Profitability': return 'fas fa-landmark';
      case 'Turnover Ratios': return 'fas fa-arrows-rotate';
      default: return 'fas fa-table';
    }
  }

  getCategoryLabel(cat: string): string {
    switch (cat) {
      case 'Liquidity Ratios': return '::LiquidityRatios';
      case 'Solvency & Profitability': return '::SolvencyProfitabilityRatios';
      case 'Turnover Ratios': return '::TurnoverRatios';
      default: return cat;
    }
  }

  exportCsv(): void {
    const r = this.report();
    if (!r?.rows) return;

    const headers = ['Category', 'Ratio', 'Current Value', 'Previous Value', 'Change %', 'Formula', 'Interpretation'];
    const rows = r.rows.map(row => [
      row.category ?? '',
      row.ratioName ?? '',
      this.formatRatio(row.value, row.unit),
      this.formatRatio(row.previousValue, row.unit),
      row.changePercentage != null ? `${row.changePercentage}%` : '',
      row.formula ?? '',
      row.description ?? '',
    ]);

    exportToCsv(`financial-ratios-${this.fromDate}-to-${this.toDate}.csv`, rows, headers);
  }
}
