import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { CustomerLedgerSummaryService } from '../../../proxy/sales/customer-ledger-summary.service';
import { CustomerService } from '../../../proxy/sales/customer.service';
import type {
  CustomerLedgerSummaryReportDto,
  CustomerLedgerSummaryRowDto,
  CustomerDto,
} from '../../../proxy/sales/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-customer-ledger-summary',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-book-open text-primary me-2"></i>
            {{ '::CustomerLedgerSummary' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::CustomerLedgerSummaryDescription' | abpLocalization }}
          </p>
        </div>
        <div class="d-flex gap-2">
          @if (report() && report()!.rows.length > 0) {
            <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
              <i class="fa fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
          <button class="btn btn-sm btn-primary" (click)="loadReport()" [disabled]="isLoading()">
            <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Refresh' | abpLocalization }}
          </button>
        </div>
      </div>

      <!-- Filters Card -->
      <div class="card mb-3 shadow-sm border-0">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::FromDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="fromDate"
                (ngModelChange)="loadReport()"
              />
            </div>
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::ToDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="toDate"
                (ngModelChange)="loadReport()"
              />
            </div>
            <div class="col-md-4">
              <label class="form-label small mb-1">{{ '::Customer' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedCustomerId"
                (ngModelChange)="loadReport()"
              >
                <option value="">-- {{ '::All' | abpLocalization }} --</option>
                @for (c of customers(); track c.id) {
                  <option [value]="c.id">{{ c.name }} ({{ c.customerCode }})</option>
                }
              </select>
            </div>
            <div class="col-md-2 d-flex align-items-end">
              <button
                class="btn btn-sm btn-outline-secondary w-100"
                (click)="resetFilters()"
              >
                <i class="fa fa-undo me-1"></i>{{ '::Reset' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      @if (isLoading()) {
        <div class="text-center py-5">
          <div class="spinner-border text-primary" role="status">
            <span class="visually-hidden">Loading...</span>
          </div>
        </div>
      } @else if (report()) {
        <!-- KPI Summary Cards -->
        <div class="row g-3 mb-3">
          <div class="col-6 col-md">
            <div class="card border-0 shadow-sm text-center py-2 bg-light">
              <div class="text-muted small">{{ '::OpeningBalance' | abpLocalization }}</div>
              <div class="fw-bold fs-5 text-dark">
                {{ report()!.companyCurrency }} {{ report()!.totalRow.openingBalance | number:'1.2-2' }}
              </div>
            </div>
          </div>
          <div class="col-6 col-md">
            <div class="card border-0 shadow-sm text-center py-2 bg-light">
              <div class="text-muted small">{{ '::InvoicedAmount' | abpLocalization }}</div>
              <div class="fw-bold fs-5 text-primary">
                {{ report()!.companyCurrency }} {{ report()!.totalRow.invoicedAmount | number:'1.2-2' }}
              </div>
            </div>
          </div>
          <div class="col-6 col-md">
            <div class="card border-0 shadow-sm text-center py-2 bg-light">
              <div class="text-muted small">{{ '::PaidAmount' | abpLocalization }}</div>
              <div class="fw-bold fs-5 text-success">
                {{ report()!.companyCurrency }} {{ report()!.totalRow.paidAmount | number:'1.2-2' }}
              </div>
            </div>
          </div>
          <div class="col-6 col-md">
            <div class="card border-0 shadow-sm text-center py-2 bg-light">
              <div class="text-muted small">{{ '::ReturnAmount' | abpLocalization }}</div>
              <div class="fw-bold fs-5 text-warning">
                {{ report()!.companyCurrency }} {{ report()!.totalRow.returnAmount | number:'1.2-2' }}
              </div>
            </div>
          </div>
          <div class="col-6 col-md">
            <div class="card border-0 shadow-sm text-center py-2 bg-light">
              <div class="text-muted small">{{ '::ClosingBalance' | abpLocalization }}</div>
              <div
                class="fw-bold fs-5"
                [class.text-danger]="report()!.totalRow.closingBalance > 0"
                [class.text-success]="report()!.totalRow.closingBalance <= 0"
              >
                {{ report()!.companyCurrency }} {{ report()!.totalRow.closingBalance | number:'1.2-2' }}
                <span class="badge ms-1" [class.bg-danger]="report()!.totalRow.drOrCr === 'Dr'" [class.bg-info]="report()!.totalRow.drOrCr === 'Cr'">
                  {{ report()!.totalRow.drOrCr }}
                </span>
              </div>
            </div>
          </div>
        </div>

        <!-- Ledger Summary Table -->
        <div class="card shadow-sm border-0">
          <div class="card-body p-0">
            <div class="table-responsive">
              <table class="table table-hover table-striped align-middle mb-0">
                <thead class="table-dark">
                  <tr>
                    <th>{{ '::Customer' | abpLocalization }}</th>
                    <th>{{ '::CustomerGroup' | abpLocalization }}</th>
                    <th>{{ '::Territory' | abpLocalization }}</th>
                    <th class="text-end">{{ '::OpeningBalance' | abpLocalization }}</th>
                    <th class="text-end">{{ '::InvoicedAmount' | abpLocalization }}</th>
                    <th class="text-end">{{ '::PaidAmount' | abpLocalization }}</th>
                    <th class="text-end">{{ '::ReturnAmount' | abpLocalization }}</th>
                    <th class="text-end">{{ '::AdjustmentAmount' | abpLocalization }}</th>
                    <th class="text-end">{{ '::ClosingBalance' | abpLocalization }}</th>
                    <th class="text-center">{{ '::DrCr' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @if (report()!.rows.length === 0) {
                    <tr>
                      <td colspan="10" class="text-center py-4 text-muted">
                        {{ '::NoCustomerLedgerSummaryFound' | abpLocalization }}
                      </td>
                    </tr>
                  } @else {
                    @for (row of report()!.rows; track row.customerId) {
                      <tr>
                        <td>
                          <strong>{{ row.customerName }}</strong>
                          <span class="text-muted small ms-1">({{ row.customerCode }})</span>
                        </td>
                        <td>{{ row.customerGroupName || '-' }}</td>
                        <td>{{ row.territoryName || '-' }}</td>
                        <td class="text-end">{{ row.openingBalance | number:'1.2-2' }}</td>
                        <td class="text-end text-primary">{{ row.invoicedAmount | number:'1.2-2' }}</td>
                        <td class="text-end text-success">{{ row.paidAmount | number:'1.2-2' }}</td>
                        <td class="text-end text-warning">{{ row.returnAmount | number:'1.2-2' }}</td>
                        <td class="text-end">{{ row.adjustmentAmount | number:'1.2-2' }}</td>
                        <td class="text-end fw-bold" [class.text-danger]="row.closingBalance > 0">
                          {{ row.closingBalance | number:'1.2-2' }}
                        </td>
                        <td class="text-center">
                          <span
                            class="badge"
                            [class.bg-danger]="row.drOrCr === 'Dr'"
                            [class.bg-info]="row.drOrCr === 'Cr'"
                          >
                            {{ row.drOrCr }}
                          </span>
                        </td>
                      </tr>
                    }
                  }
                </tbody>
                @if (report()!.rows.length > 0) {
                  <tfoot class="table-secondary fw-bold">
                    <tr>
                      <td colspan="3">{{ '::Total' | abpLocalization }}</td>
                      <td class="text-end">{{ report()!.totalRow.openingBalance | number:'1.2-2' }}</td>
                      <td class="text-end text-primary">{{ report()!.totalRow.invoicedAmount | number:'1.2-2' }}</td>
                      <td class="text-end text-success">{{ report()!.totalRow.paidAmount | number:'1.2-2' }}</td>
                      <td class="text-end text-warning">{{ report()!.totalRow.returnAmount | number:'1.2-2' }}</td>
                      <td class="text-end">{{ report()!.totalRow.adjustmentAmount | number:'1.2-2' }}</td>
                      <td class="text-end fw-bold" [class.text-danger]="report()!.totalRow.closingBalance > 0">
                        {{ report()!.totalRow.closingBalance | number:'1.2-2' }}
                      </td>
                      <td class="text-center">
                        <span
                          class="badge"
                          [class.bg-danger]="report()!.totalRow.drOrCr === 'Dr'"
                          [class.bg-info]="report()!.totalRow.drOrCr === 'Cr'"
                        >
                          {{ report()!.totalRow.drOrCr }}
                        </span>
                      </td>
                    </tr>
                  </tfoot>
                }
              </table>
            </div>
          </div>
        </div>
      }
    </div>
  `,
})
export class CustomerLedgerSummaryComponent implements OnInit {
  private ledgerService = inject(CustomerLedgerSummaryService);
  private customerService = inject(CustomerService);
  private companyContext = inject(CompanyContextService);

  report = signal<CustomerLedgerSummaryReportDto | null>(null);
  customers = signal<CustomerDto[]>([]);
  isLoading = signal(false);

  fromDate: string = '';
  toDate: string = '';
  selectedCustomerId: string = '';

  ngOnInit(): void {
    const today = new Date();
    const firstDay = new Date(today.getFullYear(), today.getMonth(), 1);
    this.fromDate = firstDay.toISOString().split('T')[0];
    this.toDate = today.toISOString().split('T')[0];

    this.loadCustomers();
    this.loadReport();
  }

  loadCustomers(): void {
    this.customerService
      .getList({ maxResultCount: 200 } as any)
      .subscribe({
        next: (res) => this.customers.set(res.items || []),
        error: () => {},
      });
  }

  resetFilters(): void {
    const today = new Date();
    const firstDay = new Date(today.getFullYear(), today.getMonth(), 1);
    this.fromDate = firstDay.toISOString().split('T')[0];
    this.toDate = today.toISOString().split('T')[0];
    this.selectedCustomerId = '';
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);

    this.ledgerService
      .getReport({
        companyId,
        fromDate: this.fromDate ? new Date(this.fromDate).toISOString() : undefined,
        toDate: this.toDate ? new Date(this.toDate).toISOString() : undefined,
        customerId: this.selectedCustomerId ? this.selectedCustomerId : undefined,
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

  exportCsv(): void {
    const rep = this.report();
    if (!rep || !rep.rows.length) return;

    const data = rep.rows.map((r) => ({
      CustomerCode: r.customerCode,
      CustomerName: r.customerName,
      CustomerGroup: r.customerGroupName ?? '',
      Territory: r.territoryName ?? '',
      OpeningBalance: r.openingBalance,
      InvoicedAmount: r.invoicedAmount,
      PaidAmount: r.paidAmount,
      ReturnAmount: r.returnAmount,
      AdjustmentAmount: r.adjustmentAmount,
      ClosingBalance: r.closingBalance,
      DrOrCr: r.drOrCr,
      Currency: r.currency,
    }));

    // Add total row
    data.push({
      CustomerCode: 'TOTAL',
      CustomerName: 'Total',
      CustomerGroup: '',
      Territory: '',
      OpeningBalance: rep.totalRow.openingBalance,
      InvoicedAmount: rep.totalRow.invoicedAmount,
      PaidAmount: rep.totalRow.paidAmount,
      ReturnAmount: rep.totalRow.returnAmount,
      AdjustmentAmount: rep.totalRow.adjustmentAmount,
      ClosingBalance: rep.totalRow.closingBalance,
      DrOrCr: rep.totalRow.drOrCr,
      Currency: rep.companyCurrency,
    });

    exportToCsv(
      `Customer_Ledger_Summary_${this.fromDate}_to_${this.toDate}.csv`,
      data,
      [
        'CustomerCode',
        'CustomerName',
        'CustomerGroup',
        'Territory',
        'OpeningBalance',
        'InvoicedAmount',
        'PaidAmount',
        'ReturnAmount',
        'AdjustmentAmount',
        'ClosingBalance',
        'DrOrCr',
        'Currency',
      ]
    );
  }
}
