import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { PaymentTermsStatusForSalesOrderService } from '../../../proxy/sales/payment-terms-status-for-sales-order.service';
import type { PaymentTermsStatusForSalesOrderReportDto } from '../../../proxy/sales/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-payment-terms-status-for-sales-order',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0"><i class="fas fa-file-invoice-dollar me-2 text-primary"></i>{{ '::PaymentTermsStatusForSalesOrder' | abpLocalization }}</h5>
          <small class="text-muted">{{ '::PaymentTermsStatusForSalesOrderDescription' | abpLocalization }}</small>
        </div>
        <div class="d-flex gap-2">
          @if (data()) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="fas fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
      </div>
      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4">
          <div class="col-md-2">
            <label class="form-label small">{{ '::From' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate">
          </div>
          <div class="col-md-2">
            <label class="form-label small">{{ '::To' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate">
          </div>
          <div class="col-md-3">
            <label class="form-label small">{{ '::Status' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="status">
              <option value="">All Statuses</option>
              <option value="Completed">Completed</option>
              <option value="Partly Paid">Partly Paid</option>
              <option value="Unpaid">Unpaid</option>
              <option value="Overdue">Overdue</option>
            </select>
          </div>
          <div class="col-md-3 d-flex align-items-center pt-3">
            <div class="form-check">
              <input class="form-check-input" type="checkbox" id="onlyUpcoming" [(ngModel)]="onlyImmediateUpcomingTerm">
              <label class="form-check-label small" for="onlyUpcoming">
                {{ '::OnlyImmediateUpcomingTerm' | abpLocalization }}
              </label>
            </div>
          </div>
          <div class="col-md-2 d-flex align-items-end">
            <button class="btn btn-primary btn-sm w-100" (click)="loadReport()" [disabled]="isLoading()">
              <i class="fas fa-sync-alt me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (isLoading()) {
          <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
        } @else if (data(); as d) {
          <!-- KPI Cards -->
          <div class="row g-3 mb-4">
            <div class="col-md-2">
              <div class="card text-center border-primary h-100 bg-light"><div class="card-body py-3">
                <div class="text-muted small">Sales Orders</div>
                <div class="fs-4 fw-bold text-primary">{{ d.totalSalesOrders }}</div>
              </div></div>
            </div>
            <div class="col-md-2">
              <div class="card text-center border-info h-100 bg-light"><div class="card-body py-3">
                <div class="text-muted small">Total Terms</div>
                <div class="fs-4 fw-bold text-info">{{ d.totalPaymentTerms }}</div>
              </div></div>
            </div>
            <div class="col-md-3">
              <div class="card text-center border-secondary h-100 bg-light"><div class="card-body py-3">
                <div class="text-muted small">Scheduled Amount</div>
                <div class="fs-4 fw-bold text-secondary">{{ d.totalPaymentAmount | number:'1.2-2' }}</div>
              </div></div>
            </div>
            <div class="col-md-3">
              <div class="card text-center border-success h-100 bg-light"><div class="card-body py-3">
                <div class="text-muted small">Paid / Invoiced</div>
                <div class="fs-4 fw-bold text-success">{{ d.totalPaidAmount | number:'1.2-2' }}</div>
              </div></div>
            </div>
            <div class="col-md-2">
              <div class="card text-center h-100 bg-light" [class.border-danger]="d.overdueTermsCount > 0">
                <div class="card-body py-3">
                  <div class="text-muted small">Overdue Terms</div>
                  <div class="fs-4 fw-bold" [class.text-danger]="d.overdueTermsCount > 0" [class.text-muted]="d.overdueTermsCount === 0">
                    {{ d.overdueTermsCount }}
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Table -->
          <div class="table-responsive">
            <table class="table table-sm table-hover align-middle">
              <thead class="table-light">
                <tr>
                  <th>{{ '::SalesOrder' | abpLocalization }}</th>
                  <th>{{ '::Customer' | abpLocalization }}</th>
                  <th>Order Date</th>
                  <th>Payment Term</th>
                  <th>Due Date</th>
                  <th class="text-end">Portion</th>
                  <th class="text-end">Amount</th>
                  <th class="text-end">Paid</th>
                  <th>Invoices</th>
                  <th class="text-center">{{ '::Status' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of d.rows; track row.salesOrderId + '-' + row.dueDate + '-' + row.paymentTermName) {
                  <tr>
                    <td><strong>{{ row.salesOrderNumber }}</strong></td>
                    <td>{{ row.customerName }}</td>
                    <td><small class="text-muted">{{ row.orderDate | date:'shortDate' }}</small></td>
                    <td>
                      <div>{{ row.paymentTermName }}</div>
                      @if (row.description && row.description !== row.paymentTermName) {
                        <small class="text-muted">{{ row.description }}</small>
                      }
                    </td>
                    <td><small class="fw-medium">{{ row.dueDate | date:'shortDate' }}</small></td>
                    <td class="text-end">{{ row.invoicePortion | number:'1.0-1' }}%</td>
                    <td class="text-end">{{ row.paymentAmount | number:'1.2-2' }}</td>
                    <td class="text-end text-success">{{ row.paidAmount | number:'1.2-2' }}</td>
                    <td>
                      @if (row.invoices) {
                        <small class="badge bg-light text-dark border">{{ row.invoices }}</small>
                      } @else {
                        <span class="text-muted">-</span>
                      }
                    </td>
                    <td class="text-center">
                      @if (row.status === 'Completed') {
                        <span class="badge bg-success">Completed</span>
                      } @else if (row.status === 'Partly Paid') {
                        <span class="badge bg-warning text-dark">Partly Paid</span>
                      } @else if (row.status === 'Overdue') {
                        <span class="badge bg-danger">Overdue</span>
                      } @else {
                        <span class="badge bg-secondary">Unpaid</span>
                      }
                    </td>
                  </tr>
                }
                @if (d.rows.length === 0) {
                  <tr>
                    <td colspan="10" class="text-center py-4 text-muted">
                      No payment terms schedule records match the criteria.
                    </td>
                  </tr>
                }
              </tbody>
              @if (d.rows.length > 0) {
                <tfoot class="table-light fw-bold">
                  <tr>
                    <td colspan="6">{{ '::Total' | abpLocalization }}</td>
                    <td class="text-end">{{ d.totalPaymentAmount | number:'1.2-2' }}</td>
                    <td class="text-end text-success">{{ d.totalPaidAmount | number:'1.2-2' }}</td>
                    <td colspan="2"></td>
                  </tr>
                </tfoot>
              }
            </table>
          </div>
        }
      </div>
    </div>
  `,
})
export class PaymentTermsStatusForSalesOrderComponent implements OnInit {
  private service = inject(PaymentTermsStatusForSalesOrderService);
  private companyContext = inject(CompanyContextService);

  data = signal<PaymentTermsStatusForSalesOrderReportDto | null>(null);
  isLoading = signal(false);

  fromDate: string = '';
  toDate: string = '';
  status: string = '';
  onlyImmediateUpcomingTerm: boolean = false;

  ngOnInit(): void {
    const today = new Date();
    const firstDayOfYear = new Date(today.getFullYear(), 0, 1);
    this.fromDate = firstDayOfYear.toISOString().split('T')[0];
    this.toDate = today.toISOString().split('T')[0];
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.service
      .getReport({
        companyId,
        fromDate: this.fromDate ? new Date(this.fromDate).toISOString() : null,
        toDate: this.toDate ? new Date(this.toDate).toISOString() : null,
        status: this.status || null,
        onlyImmediateUpcomingTerm: this.onlyImmediateUpcomingTerm || null,
      })
      .subscribe({
        next: (res) => {
          this.data.set(res);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
        },
      });
  }

  exportCsv(): void {
    const d = this.data();
    if (!d || !d.rows.length) return;

    const exportRows = d.rows.map((r) => ({
      salesOrderNumber: r.salesOrderNumber,
      customerName: r.customerName,
      orderDate: r.orderDate,
      paymentTermName: r.paymentTermName,
      dueDate: r.dueDate,
      invoicePortion: r.invoicePortion,
      paymentAmount: r.paymentAmount,
      paidAmount: r.paidAmount,
      invoices: r.invoices,
      status: r.status,
    }));

    exportToCsv(
      `Payment_Terms_Status_SO_${new Date().toISOString().split('T')[0]}.csv`,
      exportRows,
      [
        'salesOrderNumber',
        'customerName',
        'orderDate',
        'paymentTermName',
        'dueDate',
        'invoicePortion',
        'paymentAmount',
        'paidAmount',
        'invoices',
        'status',
      ]
    );
  }
}
