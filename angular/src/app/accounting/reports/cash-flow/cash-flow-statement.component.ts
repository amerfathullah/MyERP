import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { CashFlowStatementService } from '../../../proxy/accounting/cash-flow-statement.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type { CashFlowStatementDto } from '../../../proxy/accounting/models';

@Component({
  selector: 'app-cash-flow-statement',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'CashFlowStatement' | abpLocalization">
      <!-- Filters -->
      <div class="card mb-4">
        <div class="card-body">
          <div class="row g-3 align-items-end">
            <div class="col-md-3">
              <label class="form-label">{{ 'From' | abpLocalization }}</label>
              <input type="date" class="form-control" [(ngModel)]="fromDate" />
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ 'To' | abpLocalization }}</label>
              <input type="date" class="form-control" [(ngModel)]="toDate" />
            </div>
            <div class="col-md-3">
              <button class="btn btn-primary" (click)="loadReport()" [disabled]="loading()">
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
                <h6 class="text-muted mb-1">{{ 'OperatingCashFlow' | abpLocalization }}</h6>
                <h4 [class]="report()!.operatingTotal >= 0 ? 'text-success' : 'text-danger'">
                  {{ report()!.operatingTotal | number:'1.2-2' }}
                </h4>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card text-center">
              <div class="card-body">
                <h6 class="text-muted mb-1">{{ 'NetCashChange' | abpLocalization }}</h6>
                <h4 [class]="report()!.netCashChange >= 0 ? 'text-success' : 'text-danger'">
                  {{ report()!.netCashChange | number:'1.2-2' }}
                </h4>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card text-center">
              <div class="card-body">
                <h6 class="text-muted mb-1">{{ 'OpeningBalance' | abpLocalization }}</h6>
                <h4>{{ report()!.openingCashBalance | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card text-center">
              <div class="card-body">
                <h6 class="text-muted mb-1">{{ 'ClosingBalance' | abpLocalization }}</h6>
                <h4 class="text-primary">{{ report()!.closingCashBalance | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
        </div>

        <!-- Statement -->
        <div class="card">
          <div class="card-body">
            <!-- Operating Activities -->
            <h6 class="fw-bold text-primary mb-3"><i class="fa fa-industry me-2"></i>{{ 'OperatingActivities' | abpLocalization }}</h6>
            <table class="table table-sm mb-4">
              <tbody>
                @for (item of report()!.operatingActivities; track item.label) {
                  <tr>
                    <td class="ps-4">{{ item.label }}</td>
                    <td class="text-end" [class.text-danger]="item.amount < 0">{{ item.amount | number:'1.2-2' }}</td>
                  </tr>
                }
                <tr class="table-primary fw-bold">
                  <td>{{ 'NetCashFromOperations' | abpLocalization }}</td>
                  <td class="text-end">{{ report()!.operatingTotal | number:'1.2-2' }}</td>
                </tr>
              </tbody>
            </table>

            <!-- Investing Activities -->
            <h6 class="fw-bold text-info mb-3"><i class="fa fa-chart-line me-2"></i>{{ 'InvestingActivities' | abpLocalization }}</h6>
            <table class="table table-sm mb-4">
              <tbody>
                @for (item of report()!.investingActivities; track item.label) {
                  <tr>
                    <td class="ps-4">{{ item.label }}</td>
                    <td class="text-end" [class.text-danger]="item.amount < 0">{{ item.amount | number:'1.2-2' }}</td>
                  </tr>
                }
                @if (report()!.investingActivities.length === 0) {
                  <tr><td colspan="2" class="text-muted ps-4">No investing activities</td></tr>
                }
                <tr class="table-info fw-bold">
                  <td>{{ 'NetCashFromInvesting' | abpLocalization }}</td>
                  <td class="text-end">{{ report()!.investingTotal | number:'1.2-2' }}</td>
                </tr>
              </tbody>
            </table>

            <!-- Financing Activities -->
            <h6 class="fw-bold text-warning mb-3"><i class="fa fa-university me-2"></i>{{ 'FinancingActivities' | abpLocalization }}</h6>
            <table class="table table-sm mb-4">
              <tbody>
                @for (item of report()!.financingActivities; track item.label) {
                  <tr>
                    <td class="ps-4">{{ item.label }}</td>
                    <td class="text-end" [class.text-danger]="item.amount < 0">{{ item.amount | number:'1.2-2' }}</td>
                  </tr>
                }
                @if (report()!.financingActivities.length === 0) {
                  <tr><td colspan="2" class="text-muted ps-4">No financing activities</td></tr>
                }
                <tr class="table-warning fw-bold">
                  <td>{{ 'NetCashFromFinancing' | abpLocalization }}</td>
                  <td class="text-end">{{ report()!.financingTotal | number:'1.2-2' }}</td>
                </tr>
              </tbody>
            </table>

            <!-- Summary -->
            <hr />
            <table class="table table-sm">
              <tbody>
                <tr class="fw-bold">
                  <td>{{ 'NetCashChange' | abpLocalization }}</td>
                  <td class="text-end" [class.text-success]="report()!.netCashChange >= 0" [class.text-danger]="report()!.netCashChange < 0">
                    {{ report()!.netCashChange | number:'1.2-2' }}
                  </td>
                </tr>
                <tr>
                  <td>{{ 'OpeningBalance' | abpLocalization }}</td>
                  <td class="text-end">{{ report()!.openingCashBalance | number:'1.2-2' }}</td>
                </tr>
                <tr class="table-dark fw-bold fs-5">
                  <td>{{ 'ClosingBalance' | abpLocalization }}</td>
                  <td class="text-end">{{ report()!.closingCashBalance | number:'1.2-2' }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      }
    </abp-page>
  `
})
export class CashFlowStatementComponent implements OnInit {
  private cashFlowStatementService = inject(CashFlowStatementService);
  private companyContext = inject(CompanyContextService);

  fromDate = '';
  toDate = '';
  loading = signal(false);
  report = signal<CashFlowStatementDto | null>(null);

  ngOnInit() {
    const today = new Date();
    const firstOfMonth = new Date(today.getFullYear(), today.getMonth(), 1);
    this.fromDate = firstOfMonth.toISOString().split('T')[0];
    this.toDate = today.toISOString().split('T')[0];
    this.loadReport();
  }

  loadReport() {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.loading.set(true);
    this.cashFlowStatementService.getCashFlowStatement({
      companyId,
      fromDate: this.fromDate,
      toDate: this.toDate
    }).subscribe({
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

    const rows: any[] = [];
    rows.push({ Section: 'OPERATING ACTIVITIES', Label: '', Amount: '' });
    r.operatingActivities?.forEach(i => rows.push({ Section: '', Label: i.label, Amount: i.amount }));
    rows.push({ Section: '', Label: 'Net Cash from Operations', Amount: r.operatingTotal });
    rows.push({ Section: '', Label: '', Amount: '' });
    rows.push({ Section: 'INVESTING ACTIVITIES', Label: '', Amount: '' });
    r.investingActivities?.forEach(i => rows.push({ Section: '', Label: i.label, Amount: i.amount }));
    rows.push({ Section: '', Label: 'Net Cash from Investing', Amount: r.investingTotal });
    rows.push({ Section: '', Label: '', Amount: '' });
    rows.push({ Section: 'FINANCING ACTIVITIES', Label: '', Amount: '' });
    r.financingActivities?.forEach(i => rows.push({ Section: '', Label: i.label, Amount: i.amount }));
    rows.push({ Section: '', Label: 'Net Cash from Financing', Amount: r.financingTotal });
    rows.push({ Section: '', Label: '', Amount: '' });
    rows.push({ Section: 'SUMMARY', Label: 'Net Cash Change', Amount: r.netCashChange });
    rows.push({ Section: '', Label: 'Opening Cash Balance', Amount: r.openingCashBalance });
    rows.push({ Section: '', Label: 'Closing Cash Balance', Amount: r.closingCashBalance });

    exportToCsv(`cash-flow-${this.fromDate}-to-${this.toDate}.csv`, rows,
      ['Section', 'Label', 'Amount']);
  }
}
