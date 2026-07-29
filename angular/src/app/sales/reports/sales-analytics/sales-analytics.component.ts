import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

interface AnalyticsRow {
  entityId: string;
  entityName: string;
  periodValues: number[];
  total: number;
  growth: number;
}

interface AnalyticsReport {
  periodLabels: string[];
  rows: AnalyticsRow[];
  grandTotal: number;
  periodTotals: number[];
}

@Component({
  selector: 'app-sales-analytics',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0"><i class="fas fa-chart-bar me-2"></i>{{ '::SalesAnalytics' | abpLocalization }}</h5>
        <div class="d-flex gap-2">
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()" [disabled]="!report()">
            <i class="fas fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        </div>
      </div>
      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4">
          <div class="col-md-2">
            <label class="form-label small text-muted">{{ '::GroupBy' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="groupBy" (change)="generate()">
              <option value="0">{{ '::Customer' | abpLocalization }}</option>
              <option value="1">{{ '::Item' | abpLocalization }}</option>
              <option value="4">{{ '::ItemGroup' | abpLocalization }}</option>
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label small text-muted">{{ '::Period' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="periodType" (change)="generate()">
              <option value="0">{{ '::Monthly' | abpLocalization }}</option>
              <option value="1">{{ '::Quarterly' | abpLocalization }}</option>
              <option value="2">{{ '::Yearly' | abpLocalization }}</option>
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label small text-muted">{{ '::Value' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="valueField" (change)="generate()">
              <option value="Amount">{{ '::Amount' | abpLocalization }}</option>
              <option value="Quantity">{{ '::Quantity' | abpLocalization }}</option>
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label small text-muted">{{ '::From' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" (change)="generate()" />
          </div>
          <div class="col-md-2">
            <label class="form-label small text-muted">{{ '::To' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" (change)="generate()" />
          </div>
        </div>

        <!-- KPI Cards -->
        @if (report(); as r) {
          <div class="row g-3 mb-4">
            <div class="col-md-4">
              <div class="card border-start border-primary border-4">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::GrandTotal' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold">{{ r.grandTotal | number:'1.0-0' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-4">
              <div class="card border-start border-success border-4">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::Entities' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold">{{ r.rows.length }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-4">
              <div class="card border-start border-info border-4">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::Periods' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold">{{ r.periodLabels.length }}</div>
                </div>
              </div>
            </div>
          </div>

          <!-- Pivot Table -->
          <div class="table-responsive">
            <table class="table table-sm table-hover table-bordered align-middle">
              <thead class="table-light">
                <tr>
                  <th class="sticky-start">{{ '::Entity' | abpLocalization }}</th>
                  @for (label of r.periodLabels; track label) {
                    <th class="text-end">{{ label }}</th>
                  }
                  <th class="text-end fw-bold">{{ '::Total' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Growth' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of r.rows; track row.entityId) {
                  <tr>
                    <td class="sticky-start fw-medium">{{ row.entityName }}</td>
                    @for (val of row.periodValues; track $index) {
                      <td class="text-end" [class.text-danger]="val < 0">
                        {{ val | number:'1.0-0' }}
                      </td>
                    }
                    <td class="text-end fw-bold">{{ row.total | number:'1.0-0' }}</td>
                    <td class="text-end">
                      @if (row.growth > 0) {
                        <span class="badge bg-success"><i class="fas fa-arrow-up me-1"></i>{{ row.growth }}%</span>
                      } @else if (row.growth < 0) {
                        <span class="badge bg-danger"><i class="fas fa-arrow-down me-1"></i>{{ row.growth }}%</span>
                      } @else {
                        <span class="text-muted">—</span>
                      }
                    </td>
                  </tr>
                }
              </tbody>
              <tfoot class="table-light fw-bold">
                <tr>
                  <td class="sticky-start">{{ '::Total' | abpLocalization }}</td>
                  @for (pt of r.periodTotals; track $index) {
                    <td class="text-end">{{ pt | number:'1.0-0' }}</td>
                  }
                  <td class="text-end">{{ r.grandTotal | number:'1.0-0' }}</td>
                  <td></td>
                </tr>
              </tfoot>
            </table>
          </div>
        }

        @if (loading()) {
          <div class="text-center py-4"><div class="spinner-border text-primary"></div></div>
        }

        @if (!loading() && !report()) {
          <div class="text-center py-5 text-muted">
            <i class="fas fa-chart-bar fa-3x mb-3"></i>
            <p>{{ '::ClickGenerateToViewReport' | abpLocalization }}</p>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .sticky-start { position: sticky; left: 0; background: inherit; z-index: 1; }
    thead .sticky-start { background: var(--bs-table-bg, #f8f9fa); }
    tfoot .sticky-start { background: var(--bs-table-bg, #f8f9fa); }
  `]
})
export class SalesAnalyticsComponent implements OnInit {
  private http = inject(HttpClient);
  private companyContext = inject(CompanyContextService);
  private l = inject(LocalizationService);

  report = signal<AnalyticsReport | null>(null);
  loading = signal(false);

  groupBy = '0';
  periodType = '0';
  valueField = 'Amount';
  fromDate = '';
  toDate = '';

  ngOnInit() {
    const now = new Date();
    const yearStart = new Date(now.getFullYear(), 0, 1);
    this.fromDate = yearStart.toISOString().split('T')[0];
    this.toDate = now.toISOString().split('T')[0];
    this.generate();
  }

  generate() {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId || !this.fromDate || !this.toDate) return;

    this.loading.set(true);
    this.http.get<AnalyticsReport>('/api/app/sales-analytics/report', {
      params: {
        companyId,
        fromDate: this.fromDate,
        toDate: this.toDate,
        groupBy: this.groupBy,
        periodType: this.periodType,
        valueField: this.valueField,
      }
    }).subscribe({
      next: data => { this.report.set(data); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  exportCsv() {
    const r = this.report();
    if (!r) return;
    const rows = r.rows.map(row => {
      const obj: any = { Entity: row.entityName };
      r.periodLabels.forEach((label, i) => obj[label] = row.periodValues[i]);
      obj['Total'] = row.total;
      obj['Growth %'] = row.growth;
      return obj;
    });
    const cols = ['Entity', ...r.periodLabels, 'Total', 'Growth %'];
    exportToCsv('sales-analytics.csv', rows, cols);
  }
}
