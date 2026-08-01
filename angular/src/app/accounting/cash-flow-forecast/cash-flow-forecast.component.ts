import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { CashFlowForecastService } from '../../proxy/accounting/cash-flow-forecast.service';
import { exportToCsv } from '../../shared/utils/csv-export';

interface ForecastPeriod {
  label: string;
  periodStart: string;
  periodEnd: string;
  inflows: number;
  outflows: number;
  netFlow: number;
  cumulativeBalance: number;
}

interface ForecastEntry {
  documentId: string;
  documentNumber: string;
  documentType: string;
  partyName: string;
  dueDate: string;
  amount: number;
  daysUntilDue: number;
  isOverdue: boolean;
}

interface ForecastSummary {
  overdueReceivablesCount: number;
  overdueReceivablesAmount: number;
  overduePayablesCount: number;
  overduePayablesAmount: number;
  cashRunwayDays: number;
  projectedCashCrunchDate: string | null;
}

interface CashFlowForecast {
  asOfDate: string;
  forecastDays: number;
  currentCashBalance: number;
  totalExpectedInflows: number;
  totalExpectedOutflows: number;
  netCashFlow: number;
  projectedClosingBalance: number;
  periods: ForecastPeriod[];
  upcomingInflows: ForecastEntry[];
  upcomingOutflows: ForecastEntry[];
  summary: ForecastSummary;
}

@Component({
  selector: 'app-cash-flow-forecast',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'CashFlowForecast' | abpLocalization">
      <!-- Filters -->
      <div class="card mb-3">
        <div class="card-body py-2">
          <div class="row align-items-end g-2">
            <div class="col-auto">
              <label class="form-label small mb-0">{{ 'ForecastDays' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="forecastDays" (change)="loadForecast()">
                <option [value]="30">30 {{ 'Days' | abpLocalization }}</option>
                <option [value]="60">60 {{ 'Days' | abpLocalization }}</option>
                <option [value]="90">90 {{ 'Days' | abpLocalization }}</option>
                <option [value]="180">180 {{ 'Days' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-auto ms-auto">
              <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()" [disabled]="!forecast()">
                <i class="fa fa-download me-1"></i>{{ 'ExportCSV' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      @if (loading()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (forecast()) {
        <!-- KPI Cards -->
        <div class="row g-3 mb-4">
          <div class="col-md-3">
            <div class="card border-primary h-100">
              <div class="card-body text-center">
                <div class="text-muted small">{{ 'CurrentCashBalance' | abpLocalization }}</div>
                <div class="fs-4 fw-bold text-primary">{{ forecast()!.currentCashBalance | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-success h-100">
              <div class="card-body text-center">
                <div class="text-muted small">{{ 'ExpectedInflows' | abpLocalization }}</div>
                <div class="fs-4 fw-bold text-success">{{ forecast()!.totalExpectedInflows | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-danger h-100">
              <div class="card-body text-center">
                <div class="text-muted small">{{ 'ExpectedOutflows' | abpLocalization }}</div>
                <div class="fs-4 fw-bold text-danger">{{ forecast()!.totalExpectedOutflows | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card h-100" [class.border-success]="forecast()!.projectedClosingBalance >= 0"
                 [class.border-danger]="forecast()!.projectedClosingBalance < 0">
              <div class="card-body text-center">
                <div class="text-muted small">{{ 'ProjectedBalance' | abpLocalization }}</div>
                <div class="fs-4 fw-bold" [class.text-success]="forecast()!.projectedClosingBalance >= 0"
                     [class.text-danger]="forecast()!.projectedClosingBalance < 0">
                  {{ forecast()!.projectedClosingBalance | number:'1.2-2' }}
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Alert: Cash Crunch Warning -->
        @if (forecast()!.summary.projectedCashCrunchDate) {
          <div class="alert alert-danger d-flex align-items-center mb-4">
            <i class="fa fa-triangle-exclamation me-2 fs-5"></i>
            <div>
              <strong>{{ 'CashCrunchWarning' | abpLocalization }}</strong>
              <span class="ms-1">{{ forecast()!.summary.projectedCashCrunchDate | date:'dd/MM/yyyy' }}</span>
            </div>
          </div>
        }

        <!-- Cash Runway + Overdue Summary -->
        <div class="row g-3 mb-4">
          <div class="col-md-4">
            <div class="card bg-light">
              <div class="card-body py-2">
                <div class="d-flex justify-content-between align-items-center">
                  <span class="text-muted small">{{ 'CashRunway' | abpLocalization }}</span>
                  <span class="badge" [class.bg-success]="forecast()!.summary.cashRunwayDays > 60"
                        [class.bg-warning]="forecast()!.summary.cashRunwayDays > 30 && forecast()!.summary.cashRunwayDays <= 60"
                        [class.bg-danger]="forecast()!.summary.cashRunwayDays <= 30">
                    {{ forecast()!.summary.cashRunwayDays > 365 ? '365+' : (forecast()!.summary.cashRunwayDays | number:'1.0-0') }} {{ 'Days' | abpLocalization }}
                  </span>
                </div>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card bg-light">
              <div class="card-body py-2">
                <div class="d-flex justify-content-between align-items-center">
                  <span class="text-muted small">{{ 'OverdueReceivables' | abpLocalization }}</span>
                  <span class="badge bg-warning text-dark">
                    {{ forecast()!.summary.overdueReceivablesCount }} ({{ forecast()!.summary.overdueReceivablesAmount | number:'1.2-2' }})
                  </span>
                </div>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card bg-light">
              <div class="card-body py-2">
                <div class="d-flex justify-content-between align-items-center">
                  <span class="text-muted small">{{ 'OverduePayables' | abpLocalization }}</span>
                  <span class="badge bg-danger">
                    {{ forecast()!.summary.overduePayablesCount }} ({{ forecast()!.summary.overduePayablesAmount | number:'1.2-2' }})
                  </span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Period Chart (CSS bar chart) -->
        <div class="card mb-4">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h6 class="mb-0">{{ 'WeeklyCashFlowProjection' | abpLocalization }}</h6>
          </div>
          <div class="card-body">
            <div class="d-flex align-items-end gap-1" style="height: 200px; overflow-x: auto;">
              @for (period of forecast()!.periods; track period.label) {
                <div class="d-flex flex-column align-items-center flex-shrink-0" style="min-width: 80px;">
                  <div class="d-flex flex-column align-items-center" style="height: 160px; justify-content: flex-end;">
                    @if (period.netFlow >= 0) {
                      <div class="bg-success rounded-top" style="width: 40px; transition: height 0.3s;"
                           [style.height.px]="getBarHeight(period.netFlow)"></div>
                    } @else {
                      <div class="bg-danger rounded-top" style="width: 40px; transition: height 0.3s;"
                           [style.height.px]="getBarHeight(Math.abs(period.netFlow))"></div>
                    }
                  </div>
                  <div class="text-muted mt-1" style="font-size: 10px;">{{ period.label }}</div>
                  <div class="fw-bold" style="font-size: 11px;"
                       [class.text-success]="period.cumulativeBalance >= 0"
                       [class.text-danger]="period.cumulativeBalance < 0">
                    {{ period.cumulativeBalance | number:'1.0-0' }}
                  </div>
                </div>
              }
            </div>
          </div>
        </div>

        <!-- Upcoming Inflows + Outflows Tables -->
        <div class="row g-3">
          <div class="col-md-6">
            <div class="card">
              <div class="card-header bg-success bg-opacity-10">
                <h6 class="mb-0 text-success"><i class="fa fa-arrow-down me-1"></i>{{ 'UpcomingInflows' | abpLocalization }}</h6>
              </div>
              <div class="card-body p-0">
                <div class="table-responsive">
                  <table class="table table-sm table-hover mb-0">
                    <thead class="table-light">
                      <tr>
                        <th>{{ 'Invoice' | abpLocalization }}</th>
                        <th>{{ 'Customer' | abpLocalization }}</th>
                        <th>{{ 'DueDate' | abpLocalization }}</th>
                        <th class="text-end">{{ 'Amount' | abpLocalization }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (entry of forecast()!.upcomingInflows; track entry.documentId) {
                        <tr [class.table-warning]="entry.isOverdue">
                          <td>
                            <a [routerLink]="['/sales/invoices', entry.documentId]" class="text-decoration-none">
                              {{ entry.documentNumber }}
                            </a>
                          </td>
                          <td class="text-truncate" style="max-width: 120px;">{{ entry.partyName }}</td>
                          <td>
                            <span [class.text-danger]="entry.isOverdue" [class.fw-bold]="entry.isOverdue">
                              {{ entry.dueDate | date:'dd/MM/yyyy' }}
                            </span>
                            @if (entry.isOverdue) {
                              <span class="badge bg-danger ms-1" style="font-size: 9px;">{{ 'Overdue' | abpLocalization }}</span>
                            }
                          </td>
                          <td class="text-end fw-semibold text-success">{{ entry.amount | number:'1.2-2' }}</td>
                        </tr>
                      }
                      @if (!forecast()!.upcomingInflows.length) {
                        <tr><td colspan="4" class="text-center text-muted py-3">{{ 'NoUpcomingInflows' | abpLocalization }}</td></tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          </div>
          <div class="col-md-6">
            <div class="card">
              <div class="card-header bg-danger bg-opacity-10">
                <h6 class="mb-0 text-danger"><i class="fa fa-arrow-up me-1"></i>{{ 'UpcomingOutflows' | abpLocalization }}</h6>
              </div>
              <div class="card-body p-0">
                <div class="table-responsive">
                  <table class="table table-sm table-hover mb-0">
                    <thead class="table-light">
                      <tr>
                        <th>{{ 'Invoice' | abpLocalization }}</th>
                        <th>{{ 'Supplier' | abpLocalization }}</th>
                        <th>{{ 'DueDate' | abpLocalization }}</th>
                        <th class="text-end">{{ 'Amount' | abpLocalization }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (entry of forecast()!.upcomingOutflows; track entry.documentId) {
                        <tr [class.table-warning]="entry.isOverdue">
                          <td>
                            <a [routerLink]="['/purchasing/invoices', entry.documentId]" class="text-decoration-none">
                              {{ entry.documentNumber }}
                            </a>
                          </td>
                          <td class="text-truncate" style="max-width: 120px;">{{ entry.partyName }}</td>
                          <td>
                            <span [class.text-danger]="entry.isOverdue" [class.fw-bold]="entry.isOverdue">
                              {{ entry.dueDate | date:'dd/MM/yyyy' }}
                            </span>
                            @if (entry.isOverdue) {
                              <span class="badge bg-danger ms-1" style="font-size: 9px;">{{ 'Overdue' | abpLocalization }}</span>
                            }
                          </td>
                          <td class="text-end fw-semibold text-danger">{{ entry.amount | number:'1.2-2' }}</td>
                        </tr>
                      }
                      @if (!forecast()!.upcomingOutflows.length) {
                        <tr><td colspan="4" class="text-center text-muted py-3">{{ 'NoUpcomingOutflows' | abpLocalization }}</td></tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          </div>
        </div>
      } @else {
        <div class="text-center py-5 text-muted">
          <i class="fa fa-chart-line fa-3x mb-3 opacity-25"></i>
          <p>{{ 'SelectCompanyToViewForecast' | abpLocalization }}</p>
        </div>
      }
    </abp-page>
  `
})
export class CashFlowForecastComponent implements OnInit {
  private forecastService = inject(CashFlowForecastService);
  private companyContext = inject(CompanyContextService);

  forecast = signal<CashFlowForecast | null>(null);
  loading = signal(false);
  forecastDays = 90;
  Math = Math;

  ngOnInit() {
    this.loadForecast();
  }

  loadForecast() {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.loading.set(true);
    this.forecastService.getForecast({ companyId, forecastDays: this.forecastDays } as any).subscribe({
      next: (data) => {
        this.forecast.set(data as any);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  getBarHeight(value: number): number {
    if (!this.forecast()?.periods.length) return 0;
    const maxNetFlow = Math.max(...this.forecast()!.periods.map(p => Math.abs(p.netFlow)), 1);
    return Math.max(4, (value / maxNetFlow) * 140);
  }

  exportCsv() {
    if (!this.forecast()) return;
    const rows = this.forecast()!.periods.map(p => ({
      Period: p.label,
      Inflows: p.inflows,
      Outflows: p.outflows,
      'Net Flow': p.netFlow,
      'Cumulative Balance': p.cumulativeBalance
    }));
    exportToCsv('cash-flow-forecast', rows, ['Period', 'Inflows', 'Outflows', 'Net Flow', 'Cumulative Balance']);
  }
}
