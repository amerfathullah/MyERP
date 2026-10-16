import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { SalesPersonCommissionSummaryService } from '../../../proxy/sales/sales-person-commission-summary.service';
import type {
  SalesPersonCommissionSummaryReportDto,
  SalesPersonCommissionSummaryRowDto,
} from '../../../proxy/sales/models';
import { SalesPersonService } from '../../../proxy/sales/sales-person.service';
import type { SalesPersonDto } from '../../../proxy/sales/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-sales-person-commission-summary',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-handshake text-primary me-2"></i>
            {{ '::SalesPersonCommissionSummary' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::SalesPersonCommissionSummaryDescription' | abpLocalization }}
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
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::DocumentType' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedDocType"
                (ngModelChange)="loadReport()"
              >
                <option value="Sales Invoice">{{ '::SalesInvoice' | abpLocalization }}</option>
                <option value="Sales Order">{{ '::SalesOrder' | abpLocalization }}</option>
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
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::SalesPerson' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedSalesPersonId"
                (ngModelChange)="loadReport()"
              >
                <option value="">-- {{ '::AllSalesPersons' | abpLocalization }} --</option>
                @for (sp of salesPersons(); track sp.id) {
                  <option [value]="sp.id">{{ sp.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-3 d-flex align-items-end">
              <button
                class="btn btn-sm btn-primary w-100"
                (click)="loadReport()"
                [disabled]="loading()"
              >
                @if (loading()) {
                  <span class="spinner-border spinner-border-sm me-1"></span>
                } @else {
                  <i class="fa fa-sync me-1"></i>
                }
                {{ '::Refresh' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Highlights Summary -->
      @if (report()) {
        <div class="row g-2 mb-3">
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-primary-subtle text-primary py-2 px-3">
              <div class="small fw-bold">{{ '::TotalNetAmount' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ report()!.totalNetAmount | number:'1.2-2' }} {{ report()!.currency }}
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-info-subtle text-info-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::TotalAllocatedAmount' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ report()!.totalAllocatedAmount | number:'1.2-2' }} {{ report()!.currency }}
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-success-subtle text-success py-2 px-3">
              <div class="small fw-bold">{{ '::TotalIncentives' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ report()!.totalIncentives | number:'1.2-2' }} {{ report()!.currency }}
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-secondary-subtle text-secondary-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::Transactions' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ report()!.transactionCount }}
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Table -->
      <div class="card shadow-sm border-0">
        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5">
              <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Loading...</span>
              </div>
            </div>
          } @else if (!report() || !report()!.rows || report()!.rows.length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-inbox fa-3x mb-3 text-secondary"></i>
              <p class="mb-0">{{ '::NoRecordsFound' | abpLocalization }}</p>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-hover table-striped align-middle mb-0" style="font-size: 0.85rem;">
                <thead class="table-light">
                  <tr>
                    <th style="min-width: 140px;">{{ selectedDocType }}</th>
                    <th style="min-width: 170px;">{{ '::Customer' | abpLocalization }}</th>
                    <th style="min-width: 110px;">{{ '::Date' | abpLocalization }}</th>
                    <th class="text-end" style="min-width: 120px;">{{ '::NetAmount' | abpLocalization }}</th>
                    <th style="min-width: 150px;">{{ '::SalesPerson' | abpLocalization }}</th>
                    <th class="text-end" style="min-width: 100px;">{{ '::Contribution' | abpLocalization }} %</th>
                    <th class="text-end" style="min-width: 120px;">{{ '::AllocatedAmount' | abpLocalization }}</th>
                    <th class="text-end" style="min-width: 100px;">{{ '::CommissionRate' | abpLocalization }} %</th>
                    <th class="text-end" style="min-width: 120px;">{{ '::Incentives' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of report()!.rows; track (row.transactionId + '-' + row.salesPersonId)) {
                    <tr>
                      <td class="fw-semibold">
                        <a [routerLink]="getTransactionRoute(row)" class="text-decoration-none">
                          {{ row.transactionNumber }}
                        </a>
                      </td>
                      <td>
                        <div class="fw-bold text-dark">{{ row.customerName }}</div>
                        @if (row.territoryName) {
                          <span class="badge bg-light text-secondary border">
                            <i class="fa fa-map-marker-alt me-1"></i>{{ row.territoryName }}
                          </span>
                        }
                      </td>
                      <td>{{ row.postingDate | date:'shortDate' }}</td>
                      <td class="text-end fw-bold">{{ row.netAmount | number:'1.2-2' }}</td>
                      <td>
                        <a [routerLink]="['/sales/sales-persons', row.salesPersonId]" class="text-decoration-none">
                          <i class="fa fa-user text-secondary me-1"></i>{{ row.salesPersonName }}
                        </a>
                      </td>
                      <td class="text-end">{{ row.allocatedPercentage | number:'1.1-2' }}%</td>
                      <td class="text-end text-primary fw-semibold">{{ row.allocatedAmount | number:'1.2-2' }}</td>
                      <td class="text-end">{{ row.commissionRate | number:'1.1-2' }}%</td>
                      <td class="text-end text-success fw-bold">{{ row.incentives | number:'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-light fw-bold">
                  <tr class="table-info">
                    <td colspan="3">{{ '::Total' | abpLocalization }}</td>
                    <td class="text-end">{{ report()!.totalNetAmount | number:'1.2-2' }}</td>
                    <td colspan="2"></td>
                    <td class="text-end text-primary">{{ report()!.totalAllocatedAmount | number:'1.2-2' }}</td>
                    <td></td>
                    <td class="text-end text-success">{{ report()!.totalIncentives | number:'1.2-2' }}</td>
                  </tr>
                </tfoot>
              </table>
            </div>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .table td, .table th {
      padding: 0.6rem 0.75rem;
    }
  `],
})
export class SalesPersonCommissionSummaryComponent implements OnInit {
  private commissionSummaryService = inject(SalesPersonCommissionSummaryService);
  private salesPersonService = inject(SalesPersonService);
  private companyContext = inject(CompanyContextService);

  report = signal<SalesPersonCommissionSummaryReportDto | null>(null);
  salesPersons = signal<SalesPersonDto[]>([]);
  loading = signal(false);

  selectedDocType = 'Sales Invoice';
  fromDate = '';
  toDate = '';
  selectedSalesPersonId = '';

  ngOnInit(): void {
    const now = new Date();
    const oneMonthAgo = new Date();
    oneMonthAgo.setMonth(now.getMonth() - 1);

    this.toDate = now.toISOString().slice(0, 10);
    this.fromDate = oneMonthAgo.toISOString().slice(0, 10);

    this.loadSalesPersons();
    this.loadReport();
  }

  loadSalesPersons(): void {
    this.salesPersonService
      .getList({
        maxResultCount: 200,
        skipCount: 0,
      })
      .subscribe({
        next: (res) => {
          this.salesPersons.set(res.items || []);
        },
      });
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.loading.set(true);

    this.commissionSummaryService
      .getReport({
        companyId,
        docType: this.selectedDocType,
        fromDate: this.fromDate ? new Date(this.fromDate).toISOString() : undefined,
        toDate: this.toDate ? new Date(this.toDate + 'T23:59:59Z').toISOString() : undefined,
        salesPersonId: this.selectedSalesPersonId || undefined,
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

  getTransactionRoute(row: SalesPersonCommissionSummaryRowDto): any[] {
    if (this.selectedDocType === 'Sales Order') {
      return ['/sales/orders', row.transactionId];
    }
    return ['/sales/invoices', row.transactionId];
  }

  exportCsv(): void {
    const rep = this.report();
    if (!rep || !rep.rows || rep.rows.length === 0) return;

    const headers = [
      this.selectedDocType,
      'Customer',
      'Territory',
      'Posting Date',
      'Net Amount',
      'Sales Person',
      'Contribution (%)',
      'Allocated Amount',
      'Commission Rate (%)',
      'Incentives',
    ];

    const dataRows = rep.rows.map((r) => ({
      [this.selectedDocType]: r.transactionNumber,
      Customer: r.customerName,
      Territory: r.territoryName || '',
      'Posting Date': r.postingDate?.slice(0, 10) || '',
      'Net Amount': r.netAmount,
      'Sales Person': r.salesPersonName,
      'Contribution (%)': `${r.allocatedPercentage}%`,
      'Allocated Amount': r.allocatedAmount,
      'Commission Rate (%)': `${r.commissionRate}%`,
      Incentives: r.incentives,
    }));

    // Grand total row
    dataRows.push({
      [this.selectedDocType]: 'Total',
      Customer: '',
      Territory: '',
      'Posting Date': '',
      'Net Amount': rep.totalNetAmount,
      'Sales Person': '',
      'Contribution (%)': '',
      'Allocated Amount': rep.totalAllocatedAmount,
      'Commission Rate (%)': '',
      Incentives: rep.totalIncentives,
    });

    const fileSuffix = this.selectedDocType.toLowerCase().replace(/\s+/g, '_');
    exportToCsv(`Sales_Person_Commission_Summary_${fileSuffix}.csv`, dataRows, headers);
  }
}
