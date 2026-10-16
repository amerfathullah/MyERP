import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { SalesAnalyticsService } from '../../../proxy/sales/sales-analytics.service';
import type { CustomerCreditBalanceDto } from '../../../proxy/sales/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-customer-credit-balance',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0"><i class="fas fa-hand-holding-usd me-2 text-primary"></i>{{ '::CustomerCreditBalance' | abpLocalization }}</h5>
          <small class="text-muted">{{ '::CustomerCreditBalanceDescription' | abpLocalization }}</small>
        </div>
        <div class="d-flex gap-2">
          @if (rows().length > 0) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="fas fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
          <button class="btn btn-sm btn-primary" (click)="loadReport()" [disabled]="isLoading()">
            <i class="fas fa-sync-alt me-1" [class.fa-spin]="isLoading()"></i>{{ '::Refresh' | abpLocalization }}
          </button>
        </div>
      </div>
      <div class="card-body">
        @if (isLoading()) {
          <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
        } @else {
          <!-- KPI Cards -->
          <div class="row g-3 mb-4">
            <div class="col-md-3">
              <div class="card text-center border-primary h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">Total Customers</div>
                  <div class="fs-4 fw-bold text-primary">{{ rows().length }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card text-center border-info h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">Total Credit Limit</div>
                  <div class="fs-4 fw-bold text-info">{{ totalCreditLimit() | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card text-center border-warning h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">Total Outstanding</div>
                  <div class="fs-4 fw-bold text-warning">{{ totalOutstanding() | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card text-center h-100 bg-light" [class.border-danger]="totalCreditBalance() < 0" [class.border-success]="totalCreditBalance() >= 0">
                <div class="card-body py-3">
                  <div class="text-muted small">Total Credit Balance</div>
                  <div class="fs-4 fw-bold" [class.text-danger]="totalCreditBalance() < 0" [class.text-success]="totalCreditBalance() >= 0">
                    {{ totalCreditBalance() | number:'1.2-2' }}
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
                  <th>{{ '::Customer' | abpLocalization }}</th>
                  <th class="text-end">{{ '::CreditLimit' | abpLocalization }}</th>
                  <th class="text-end">{{ '::OutstandingAmount' | abpLocalization }}</th>
                  <th class="text-end">{{ '::CreditBalance' | abpLocalization }}</th>
                  <th class="text-center">{{ '::BypassCreditCheck' | abpLocalization }}</th>
                  <th class="text-center">Frozen</th>
                  <th class="text-center">Status</th>
                </tr>
              </thead>
              <tbody>
                @for (row of rows(); track row.customerId) {
                  <tr>
                    <td><strong>{{ row.customerName }}</strong></td>
                    <td class="text-end">{{ row.creditLimit | number:'1.2-2' }}</td>
                    <td class="text-end">{{ row.outstandingAmount | number:'1.2-2' }}</td>
                    <td class="text-end fw-bold">
                      <span class="badge" [class.bg-danger]="row.creditBalance <= 0" [class.bg-success]="row.creditBalance > 0">
                        {{ row.creditBalance | number:'1.2-2' }}
                      </span>
                    </td>
                    <td class="text-center">
                      @if (row.bypassCreditLimitCheckAtSalesOrder) {
                        <span class="badge bg-secondary">Yes</span>
                      } @else {
                        <span class="text-muted">No</span>
                      }
                    </td>
                    <td class="text-center">
                      @if (row.isFrozen) {
                        <span class="badge bg-danger"><i class="fas fa-snowflake me-1"></i>Frozen</span>
                      } @else {
                        <span class="text-muted">-</span>
                      }
                    </td>
                    <td class="text-center">
                      @if (row.disabled) {
                        <span class="badge bg-secondary">Disabled</span>
                      } @else {
                        <span class="badge bg-success">Active</span>
                      }
                    </td>
                  </tr>
                }
                @if (rows().length === 0) {
                  <tr>
                    <td colspan="7" class="text-center py-4 text-muted">
                      No customer credit records found for the current company.
                    </td>
                  </tr>
                }
              </tbody>
              @if (rows().length > 0) {
                <tfoot class="table-light fw-bold">
                  <tr>
                    <td>{{ '::Total' | abpLocalization }}</td>
                    <td class="text-end">{{ totalCreditLimit() | number:'1.2-2' }}</td>
                    <td class="text-end text-warning">{{ totalOutstanding() | number:'1.2-2' }}</td>
                    <td class="text-end" [class.text-danger]="totalCreditBalance() < 0" [class.text-success]="totalCreditBalance() >= 0">
                      {{ totalCreditBalance() | number:'1.2-2' }}
                    </td>
                    <td colspan="3"></td>
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
export class CustomerCreditBalanceComponent implements OnInit {
  private service = inject(SalesAnalyticsService);
  private companyContext = inject(CompanyContextService);

  rows = signal<CustomerCreditBalanceDto[]>([]);
  isLoading = signal(false);

  totalCreditLimit = signal(0);
  totalOutstanding = signal(0);
  totalCreditBalance = signal(0);

  ngOnInit(): void {
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.service.getCustomerCreditBalanceReport({ companyId }).subscribe({
      next: (data) => {
        const list = data ?? [];
        this.rows.set(list);
        this.totalCreditLimit.set(list.reduce((acc, r) => acc + (r.creditLimit || 0), 0));
        this.totalOutstanding.set(list.reduce((acc, r) => acc + (r.outstandingAmount || 0), 0));
        this.totalCreditBalance.set(list.reduce((acc, r) => acc + (r.creditBalance || 0), 0));
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      },
    });
  }

  exportCsv(): void {
    const list = this.rows();
    if (!list.length) return;

    const exportRows = list.map((r) => ({
      customerName: r.customerName ?? '',
      creditLimit: r.creditLimit,
      outstandingAmount: r.outstandingAmount,
      creditBalance: r.creditBalance,
      bypassAtSalesOrder: r.bypassCreditLimitCheckAtSalesOrder ? 'Yes' : 'No',
      isFrozen: r.isFrozen ? 'Yes' : 'No',
      status: r.disabled ? 'Disabled' : 'Active',
    }));

    exportToCsv(
      `Customer_Credit_Balance_${new Date().toISOString().split('T')[0]}.csv`,
      exportRows,
      [
        'customerName',
        'creditLimit',
        'outstandingAmount',
        'creditBalance',
        'bypassAtSalesOrder',
        'isFrozen',
        'status',
      ]
    );
  }
}
