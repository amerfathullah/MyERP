import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { SupplierPaymentSummaryService } from '../../../proxy/purchasing/supplier-payment-summary.service';
import type { SupplierPaymentSummaryReportDto, SupplierPaymentLineDto } from '../../../proxy/purchasing/models';
import { exportToCsv } from '../../../shared/utils/csv-export';
import { CompanyCurrencyPipe } from '../../../shared/pipes/company-currency.pipe';

@Component({
  selector: 'app-supplier-payment-summary',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe, CompanyCurrencyPipe],
  template: `
    <abp-page [title]="'::SupplierPaymentSummary' | abpLocalization">
      <!-- Filters -->
      <div class="card mb-3"><div class="card-body">
        <div class="row g-2 align-items-end">
          <div class="col-md-3">
            <label class="form-label">{{ '::From' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" (change)="loadReport()">
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ '::To' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" (change)="loadReport()">
          </div>
          <div class="col-md-3 d-flex gap-2">
            <button class="btn btn-sm btn-primary" (click)="loadReport()">
              <i class="fa fa-sync me-1"></i>{{ '::Generate' | abpLocalization }}
            </button>
            @if (report()) {
              <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
                <i class="fa fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
              </button>
            }
          </div>
        </div>
      </div></div>

      @if (loading()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      }

      @if (report(); as r) {
        <!-- KPI Cards -->
        <div class="row mb-3">
          <div class="col-md-3">
            <div class="card border-start border-primary border-3">
              <div class="card-body text-center">
                <div class="fs-4 fw-bold">{{ "" | companyCurrency }} {{ r.totalInvoiced | number:'1.2-2' }}</div>
                <small class="text-muted">{{ '::TotalInvoiced' | abpLocalization }}</small>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-start border-success border-3">
              <div class="card-body text-center">
                <div class="fs-4 fw-bold text-success">{{ "" | companyCurrency }} {{ r.totalPaid | number:'1.2-2' }}</div>
                <small class="text-muted">{{ '::TotalPaid' | abpLocalization }}</small>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-start border-warning border-3">
              <div class="card-body text-center">
                <div class="fs-4 fw-bold text-warning">{{ "" | companyCurrency }} {{ r.totalOutstanding | number:'1.2-2' }}</div>
                <small class="text-muted">{{ '::TotalOutstanding' | abpLocalization }}</small>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-start border-danger border-3">
              <div class="card-body text-center">
                <div class="fs-4 fw-bold text-danger">{{ "" | companyCurrency }} {{ r.totalOverdueAmount | number:'1.2-2' }}</div>
                <small class="text-muted">{{ '::OverdueAmount' | abpLocalization }}</small>
              </div>
            </div>
          </div>
        </div>

        <!-- Supplier Table -->
        <div class="card"><div class="card-body">
          <h6><i class="fa fa-building me-2"></i>{{ '::BySupplier' | abpLocalization }}
            <span class="badge bg-secondary ms-2">{{ r.supplierCount }}</span>
          </h6>
          @if ((r.items ?? []).length === 0) {
            <p class="text-muted text-center py-3">{{ '::NoDataForSelectedPeriod' | abpLocalization }}</p>
          } @else {
            <div class="table-responsive">
              <table class="table table-sm table-hover">
                <thead>
                  <tr>
                    <th>{{ '::Supplier' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Invoices' | abpLocalization }}</th>
                    <th class="text-end">{{ '::TotalInvoiced' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Paid' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Outstanding' | abpLocalization }}</th>
                    <th class="text-center">{{ '::Overdue' | abpLocalization }}</th>
                    <th class="text-center">{{ '::OnTimePayment' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of r.items ?? []; track item.supplierId) {
                    <tr [class.table-danger]="(item.overdueCount ?? 0) > 0">
                      <td>
                        <a [routerLink]="['/suppliers', item.supplierId]" class="text-decoration-none">
                          {{ item.supplierName }}
                        </a>
                      </td>
                      <td class="text-end">{{ item.invoiceCount }}</td>
                      <td class="text-end">{{ item.totalInvoiced | number:'1.2-2' }}</td>
                      <td class="text-end text-success">{{ item.totalPaid | number:'1.2-2' }}</td>
                      <td class="text-end fw-bold" [class.text-danger]="(item.totalOutstanding ?? 0) > 0">
                        {{ item.totalOutstanding | number:'1.2-2' }}
                      </td>
                      <td class="text-center">
                        @if ((item.overdueCount ?? 0) > 0) {
                          <span class="badge bg-danger">{{ item.overdueCount }}
                            ({{ item.overdueAmount ?? 0 | number:'1.0-0' }})
                          </span>
                        } @else {
                          <span class="badge bg-success"><i class="fa fa-check"></i></span>
                        }
                      </td>
                      <td class="text-center">
                        <span class="badge" [ngClass]="getTimelinessClass(item.paymentTimeliness ?? 0)">
                          {{ item.paymentTimeliness ?? 0 | number:'1.0-0' }}%
                        </span>
                      </td>
                    </tr>
                  }
                </tbody>
                <tfoot>
                  <tr class="table-light fw-bold">
                    <td>{{ '::Total' | abpLocalization }} ({{ r.supplierCount }} {{ '::Suppliers' | abpLocalization }})</td>
                    <td class="text-end">{{ getTotalInvoices(r.items ?? []) }}</td>
                    <td class="text-end">{{ r.totalInvoiced | number:'1.2-2' }}</td>
                    <td class="text-end text-success">{{ r.totalPaid | number:'1.2-2' }}</td>
                    <td class="text-end text-danger">{{ r.totalOutstanding | number:'1.2-2' }}</td>
                    <td></td>
                    <td></td>
                  </tr>
                </tfoot>
              </table>
            </div>
          }
        </div></div>
      }
    </abp-page>
  `,
})
export class SupplierPaymentSummaryComponent implements OnInit {
  private service = inject(SupplierPaymentSummaryService);
  private companyContext = inject(CompanyContextService);

  report = signal<SupplierPaymentSummaryReportDto | null>(null);
  loading = signal(false);

  fromDate = new Date(Date.now() - 90 * 86400000).toISOString().substring(0, 10);
  toDate = new Date().toISOString().substring(0, 10);

  ngOnInit(): void {
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;
    this.loading.set(true);
    this.service.getReport({ companyId, fromDate: this.fromDate, toDate: this.toDate }).subscribe({
      next: (r) => { this.report.set(r); this.loading.set(false); },
      error: () => { this.loading.set(false); },
    });
  }

  getTimelinessClass(pct: number): string {
    if (pct >= 80) return 'bg-success';
    if (pct >= 50) return 'bg-warning';
    return 'bg-danger';
  }

  getTotalInvoices(items: SupplierPaymentLineDto[]): number {
    return items.reduce((sum, i) => sum + (i.invoiceCount ?? 0), 0);
  }

  exportCsv(): void {
    const r = this.report();
    if (!r) return;
    const rows = (r.items ?? []).map(i => ({
      Supplier: i.supplierName,
      Invoices: i.invoiceCount,
      'Total Invoiced': i.totalInvoiced,
      'Total Paid': i.totalPaid,
      Outstanding: i.totalOutstanding,
      'Overdue Count': i.overdueCount,
      'Overdue Amount': i.overdueAmount,
      'On-Time %': i.paymentTimeliness,
    }));
    exportToCsv('supplier-payment-summary.csv', rows, [
      'Supplier', 'Invoices', 'Total Invoiced', 'Total Paid', 'Outstanding', 'Overdue Count', 'Overdue Amount', 'On-Time %'
    ]);
  }
}
