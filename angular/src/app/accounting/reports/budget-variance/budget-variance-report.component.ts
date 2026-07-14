import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

interface BudgetVarianceRow {
  accountCode: string;
  accountName: string;
  accountType: string;
  budgetAmount: number;
  actualAmount: number;
  variance: number;
  variancePercent: number;
  isOverBudget: boolean;
}

interface BudgetVarianceReport {
  rows: BudgetVarianceRow[];
  totalBudget: number;
  totalActual: number;
  totalVariance: number;
  overBudgetCount: number;
}

@Component({
  selector: 'app-budget-variance-report',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'BudgetVarianceReport' | abpLocalization">
      <div class="card mb-4">
        <div class="card-body">
          <div class="row g-3 align-items-end">
            <div class="col-md-3">
              <label class="form-label">{{ 'FiscalYear' | abpLocalization }}</label>
              <select class="form-select" [(ngModel)]="selectedFiscalYearId" (ngModelChange)="loadReport()">
                <option value="">Select...</option>
                @for (fy of fiscalYears(); track fy.id) { <option [value]="fy.id">{{ fy.name }}</option> }
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label">{{ 'From' | abpLocalization }}</label>
              <input type="date" class="form-control" [(ngModel)]="fromDate" />
            </div>
            <div class="col-md-2">
              <label class="form-label">{{ 'To' | abpLocalization }}</label>
              <input type="date" class="form-control" [(ngModel)]="toDate" />
            </div>
            <div class="col-md-2">
              <button class="btn btn-primary" (click)="loadReport()" [disabled]="!selectedFiscalYearId || loading()">
                <i class="fa fa-sync me-1"></i>{{ 'GenerateReport' | abpLocalization }}
              </button>
            </div>
            <div class="col-md-3 text-end">
              @if (report()) {
                <button class="btn btn-outline-secondary btn-sm" (click)="exportReport()">
                  <i class="fa fa-download me-1"></i>{{ 'ExportCSV' | abpLocalization }}
                </button>
              }
            </div>
          </div>
        </div>
      </div>

      @if (loading()) {
        <div class="text-center p-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (report()) {
        <!-- KPI Cards -->
        <div class="row mb-4">
          <div class="col-md-3">
            <div class="card text-center">
              <div class="card-body">
                <h6 class="text-muted mb-1">{{ 'TotalBudget' | abpLocalization }}</h6>
                <h4 class="text-primary">{{ report()!.totalBudget | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card text-center">
              <div class="card-body">
                <h6 class="text-muted mb-1">{{ 'TotalActual' | abpLocalization }}</h6>
                <h4>{{ report()!.totalActual | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card text-center">
              <div class="card-body">
                <h6 class="text-muted mb-1">{{ 'TotalVariance' | abpLocalization }}</h6>
                <h4 [class]="report()!.totalVariance >= 0 ? 'text-success' : 'text-danger'">
                  {{ report()!.totalVariance | number:'1.2-2' }}
                </h4>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card text-center">
              <div class="card-body">
                <h6 class="text-muted mb-1">{{ 'OverBudget' | abpLocalization }}</h6>
                <h4 [class]="report()!.overBudgetCount > 0 ? 'text-danger' : 'text-success'">
                  {{ report()!.overBudgetCount }} / {{ report()!.rows.length }}
                </h4>
              </div>
            </div>
          </div>
        </div>

        <!-- Report Table -->
        <div class="card">
          <div class="card-body">
            @if (report()!.rows.length === 0) {
              <div class="text-center text-muted p-4">
                <i class="fa fa-chart-bar fa-2x mb-2 d-block"></i>
                No budgets found for this fiscal year.
              </div>
            } @else {
              <table class="table table-hover">
                <thead>
                  <tr>
                    <th>{{ 'AccountCode' | abpLocalization }}</th>
                    <th>{{ 'AccountName' | abpLocalization }}</th>
                    <th class="text-end">{{ 'Budget' | abpLocalization }}</th>
                    <th class="text-end">{{ 'Actual' | abpLocalization }}</th>
                    <th class="text-end">{{ 'Variance' | abpLocalization }}</th>
                    <th class="text-end">%</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of report()!.rows; track row.accountCode) {
                    <tr [class.table-danger]="row.isOverBudget">
                      <td><code>{{ row.accountCode }}</code></td>
                      <td>{{ row.accountName }}</td>
                      <td class="text-end">{{ row.budgetAmount | number:'1.2-2' }}</td>
                      <td class="text-end">{{ row.actualAmount | number:'1.2-2' }}</td>
                      <td class="text-end" [class.text-danger]="row.variance < 0" [class.text-success]="row.variance > 0">
                        {{ row.variance | number:'1.2-2' }}
                      </td>
                      <td class="text-end" [class.text-danger]="row.variancePercent < 0">
                        {{ row.variancePercent }}%
                      </td>
                      <td>
                        @if (row.isOverBudget) {
                          <span class="badge bg-danger"><i class="fa fa-exclamation-triangle"></i></span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
                <tfoot>
                  <tr class="fw-bold table-secondary">
                    <td colspan="2">{{ 'Total' | abpLocalization }}</td>
                    <td class="text-end">{{ report()!.totalBudget | number:'1.2-2' }}</td>
                    <td class="text-end">{{ report()!.totalActual | number:'1.2-2' }}</td>
                    <td class="text-end" [class.text-danger]="report()!.totalVariance < 0">{{ report()!.totalVariance | number:'1.2-2' }}</td>
                    <td colspan="2"></td>
                  </tr>
                </tfoot>
              </table>
            }
          </div>
        </div>
      }
    </abp-page>
  `
})
export class BudgetVarianceReportComponent implements OnInit {
  private http = inject(HttpClient);
  private companyContext = inject(CompanyContextService);

  selectedFiscalYearId = '';
  fromDate = '';
  toDate = '';
  loading = signal(false);
  report = signal<BudgetVarianceReport | null>(null);
  fiscalYears = signal<{ id: string; name: string }[]>([]);

  ngOnInit() {
    this.loadFiscalYears();
  }

  loadFiscalYears() {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;
    this.http.get<any>('/api/app/fiscal-year', { params: { companyId, maxResultCount: '20' } }).subscribe({
      next: (res) => {
        const items = res.items ?? res ?? [];
        this.fiscalYears.set(items.map((fy: any) => ({ id: fy.id, name: fy.name })));
        if (items.length > 0) {
          this.selectedFiscalYearId = items[0].id;
          this.loadReport();
        }
      }
    });
  }

  loadReport() {
    if (!this.selectedFiscalYearId) return;
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.loading.set(true);
    const params: any = { companyId, fiscalYearId: this.selectedFiscalYearId };
    if (this.fromDate) params.fromDate = this.fromDate;
    if (this.toDate) params.toDate = this.toDate;

    this.http.post<BudgetVarianceReport>('/api/app/budget-variance-report/report', params).subscribe({
      next: (res) => {
        this.report.set(res);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  exportReport() {
    const r = this.report();
    if (!r) return;
    const rows = r.rows.map(row => ({
      Code: row.accountCode,
      Account: row.accountName,
      Budget: row.budgetAmount,
      Actual: row.actualAmount,
      Variance: row.variance,
      'Variance%': row.variancePercent,
      Status: row.isOverBudget ? 'OVER' : 'OK'
    }));
    exportToCsv('budget-variance.csv', rows, ['Code', 'Account', 'Budget', 'Actual', 'Variance', 'Variance%', 'Status']);
  }
}
