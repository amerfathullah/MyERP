import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { SalesCommissionReportService } from '../../../proxy/sales/sales-commission-report.service';
import type { SalesCommissionReportDto } from '../../../proxy/sales/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { CompanyCurrencyPipe } from '../../../shared/pipes/company-currency.pipe';
import { exportToCsv } from '../../../shared/utils/csv-export';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-sales-commission-report',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe, CompanyCurrencyPipe, RouterModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0"><i class="fas fa-coins me-2"></i>{{ '::SalesCommissionReport' | abpLocalization }}</h5>
        <div class="d-flex gap-2">
          @if (report()) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="fas fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
      </div>
      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4">
          <div class="col-md-3">
            <label class="form-label small">{{ '::From' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" (change)="loadReport()">
          </div>
          <div class="col-md-3">
            <label class="form-label small">{{ '::To' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" (change)="loadReport()">
          </div>
        </div>

        @if (isLoading()) {
          <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
        } @else if (report(); as r) {
          <!-- KPI Cards -->
          <div class="row g-3 mb-4">
            <div class="col-md-3">
              <div class="card text-center border-primary h-100"><div class="card-body py-3">
                <div class="text-muted small">{{ '::TotalRevenue' | abpLocalization }}</div>
                <div class="fs-4 fw-bold text-primary">{{ '' | companyCurrency }} {{ r.totalRevenue | number:'1.2-2' }}</div>
              </div></div>
            </div>
            <div class="col-md-3">
              <div class="card text-center border-success h-100"><div class="card-body py-3">
                <div class="text-muted small">{{ '::TotalCommission' | abpLocalization }}</div>
                <div class="fs-4 fw-bold text-success">{{ '' | companyCurrency }} {{ r.totalCommission | number:'1.2-2' }}</div>
              </div></div>
            </div>
            <div class="col-md-3">
              <div class="card text-center h-100"><div class="card-body py-3">
                <div class="text-muted small">{{ '::Invoices' | abpLocalization }}</div>
                <div class="fs-3 fw-bold">{{ r.invoiceCount }}</div>
              </div></div>
            </div>
            <div class="col-md-3">
              <div class="card text-center h-100"><div class="card-body py-3">
                <div class="text-muted small">{{ '::SalesPersons' | abpLocalization }}</div>
                <div class="fs-3 fw-bold">{{ r.salesPersonCount }}</div>
              </div></div>
            </div>
          </div>

          <!-- Commission Table -->
          @if (r.rows && r.rows.length > 0) {
            <div class="table-responsive">
              <table class="table table-hover">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::SalesPerson' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Invoices' | abpLocalization }}</th>
                    <th class="text-end">{{ '::AllocatedAmount' | abpLocalization }}</th>
                    <th class="text-end">{{ '::CommissionRate' | abpLocalization }} %</th>
                    <th class="text-end">{{ '::Commission' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of r.rows; track row.salesPersonId) {
                    <tr>
                      <td>
                        <a [routerLink]="['/sales/sales-persons', row.salesPersonId]" class="text-decoration-none">
                          {{ row.salesPersonName }}
                        </a>
                      </td>
                      <td class="text-end">{{ row.invoiceCount }}</td>
                      <td class="text-end">{{ row.totalAllocatedAmount | number:'1.2-2' }}</td>
                      <td class="text-end">{{ row.commissionRate | number:'1.1-1' }}%</td>
                      <td class="text-end fw-bold text-success">{{ row.totalCommission | number:'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-light fw-bold">
                  <tr>
                    <td>{{ '::Total' | abpLocalization }}</td>
                    <td class="text-end">{{ r.invoiceCount }}</td>
                    <td class="text-end">{{ getTotalAllocated() | number:'1.2-2' }}</td>
                    <td class="text-end">—</td>
                    <td class="text-end text-success">{{ r.totalCommission | number:'1.2-2' }}</td>
                  </tr>
                </tfoot>
              </table>
            </div>
          } @else {
            <div class="text-center text-muted py-4">
              <i class="fas fa-users-slash fa-3x mb-3 text-secondary"></i>
              <p>{{ '::NoCommissionDataForPeriod' | abpLocalization }}</p>
            </div>
          }
        }
      </div>
    </div>
  `,
})
export class SalesCommissionReportComponent implements OnInit {
  private reportService = inject(SalesCommissionReportService);
  private companyContext = inject(CompanyContextService);

  report = signal<SalesCommissionReportDto | null>(null);
  isLoading = signal(false);
  fromDate = '';
  toDate = '';

  ngOnInit(): void {
    const now = new Date();
    this.toDate = now.toISOString().slice(0, 10);
    const from = new Date(now.getFullYear(), now.getMonth() - 2, 1);
    this.fromDate = from.toISOString().slice(0, 10);
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId || !this.fromDate || !this.toDate) return;

    this.isLoading.set(true);
    this.reportService.getReport(companyId, this.fromDate, this.toDate).subscribe({
      next: data => {
        this.report.set(data);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  getTotalAllocated(): number {
    return this.report()?.rows?.reduce((sum, r) => sum + (r.totalAllocatedAmount ?? 0), 0) ?? 0;
  }

  exportCsv(): void {
    const r = this.report();
    if (!r || !r.rows) return;
    const columns = ['Sales Person', 'Invoices', 'Allocated Amount', 'Commission Rate (%)', 'Commission Amount'];
    const rows = r.rows.map(row => ({
      'Sales Person': row.salesPersonName,
      'Invoices': row.invoiceCount,
      'Allocated Amount': row.totalAllocatedAmount,
      'Commission Rate (%)': row.commissionRate,
      'Commission Amount': row.totalCommission,
    }));
    exportToCsv('sales-commission-report.csv', rows, columns);
  }
}
