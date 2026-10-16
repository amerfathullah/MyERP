import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { SalesAnalyticsService } from '../../../proxy/sales/sales-analytics.service';
import type {
  CustomerAcquisitionAndLoyaltyReportDto,
  CustomerAcquisitionAndLoyaltyRowDto,
} from '../../../proxy/sales/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-customer-acquisition-and-loyalty',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-users text-primary me-2"></i>
            {{ '::CustomerAcquisitionAndLoyalty' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::CustomerAcquisitionAndLoyaltyDescription' | abpLocalization }}
          </p>
        </div>
        @if (report() && report()!.rows && report()!.rows!.length > 0) {
          <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fa fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <!-- Filters -->
      <div class="card mb-3 shadow-sm border-0">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::From' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="fromDate"
                (change)="loadReport()"
              />
            </div>
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::To' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="toDate"
                (change)="loadReport()"
              />
            </div>
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::View' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="viewType"
                (change)="onViewTypeChange()"
              >
                <option value="Monthly">{{ '::ViewByTime' | abpLocalization }}</option>
                <option value="Territory">{{ '::ViewByTerritory' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-3 d-flex align-items-end">
              <button
                class="btn btn-primary btn-sm w-100"
                (click)="loadReport()"
                [disabled]="isLoading()"
              >
                <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>
                {{ '::Refresh' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- KPI Summary Cards -->
      @if (report(); as rep) {
        <div class="row g-3 mb-3">
          <div class="col-md">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::NewCustomers' | abpLocalization }}</div>
                <h4 class="fw-bold text-success mb-0">{{ rep.totalNewCustomers }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::RepeatCustomers' | abpLocalization }}</div>
                <h4 class="fw-bold text-info mb-0">{{ rep.totalRepeatCustomers }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::Total' | abpLocalization }} Customers</div>
                <h4 class="fw-bold text-dark mb-0">{{ rep.totalNewCustomers + rep.totalRepeatCustomers }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::NewCustomerRevenue' | abpLocalization }}</div>
                <h4 class="fw-bold text-success mb-0">{{ rep.totalNewRevenue | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::RepeatCustomerRevenue' | abpLocalization }}</div>
                <h4 class="fw-bold text-info mb-0">{{ rep.totalRepeatRevenue | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::TotalRevenue' | abpLocalization }}</div>
                <h4 class="fw-bold text-primary mb-0">{{ rep.totalRevenue | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
        </div>

        <!-- Table View -->
        <div class="card shadow-sm border-0">
          <div class="card-body p-0">
            @if (isLoading()) {
              <div class="text-center py-5">
                <i class="fa fa-spinner fa-spin fa-2x text-primary"></i>
              </div>
            } @else if (!rep.rows || rep.rows.length === 0) {
              <div class="text-center py-5 text-muted">
                <i class="fa fa-info-circle fa-2x mb-2 d-block"></i>
                {{ '::NoAcquisitionDataFound' | abpLocalization }}
              </div>
            } @else {
              <div class="table-responsive">
                <table class="table table-hover table-striped align-middle mb-0 small">
                  <thead class="table-light">
                    <tr>
                      <th>
                        {{ (viewType === 'Monthly' ? '::Month' : '::Territory') | abpLocalization }}
                      </th>
                      <th class="text-end">{{ '::NewCustomers' | abpLocalization }}</th>
                      <th class="text-end">{{ '::RepeatCustomers' | abpLocalization }}</th>
                      <th class="text-end">{{ '::Total' | abpLocalization }}</th>
                      <th class="text-end">{{ '::NewCustomerRevenue' | abpLocalization }}</th>
                      <th class="text-end">{{ '::RepeatCustomerRevenue' | abpLocalization }}</th>
                      <th class="text-end">{{ '::TotalRevenue' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (row of rep.rows; track row.key) {
                      <tr>
                        <td class="fw-semibold text-primary">{{ row.key || '-' }}</td>
                        <td class="text-end text-success fw-semibold">{{ row.newCustomerCount }}</td>
                        <td class="text-end text-info fw-semibold">{{ row.repeatCustomerCount }}</td>
                        <td class="text-end fw-bold">{{ row.newCustomerCount + row.repeatCustomerCount }}</td>
                        <td class="text-end text-success">{{ row.newCustomerRevenue | number:'1.2-2' }}</td>
                        <td class="text-end text-info">{{ row.repeatCustomerRevenue | number:'1.2-2' }}</td>
                        <td class="text-end fw-bold text-dark">{{ row.totalRevenue | number:'1.2-2' }}</td>
                      </tr>
                    }
                  </tbody>
                  <tfoot class="table-light fw-bold">
                    <tr>
                      <td>{{ '::Total' | abpLocalization }}</td>
                      <td class="text-end text-success">{{ rep.totalNewCustomers }}</td>
                      <td class="text-end text-info">{{ rep.totalRepeatCustomers }}</td>
                      <td class="text-end">{{ rep.totalNewCustomers + rep.totalRepeatCustomers }}</td>
                      <td class="text-end text-success">{{ rep.totalNewRevenue | number:'1.2-2' }}</td>
                      <td class="text-end text-info">{{ rep.totalRepeatRevenue | number:'1.2-2' }}</td>
                      <td class="text-end text-primary">{{ rep.totalRevenue | number:'1.2-2' }}</td>
                    </tr>
                  </tfoot>
                </table>
              </div>
            }
          </div>
        </div>
      }
    </div>
  `,
})
export class CustomerAcquisitionAndLoyaltyComponent implements OnInit {
  private service = inject(SalesAnalyticsService);
  private companyContext = inject(CompanyContextService);

  report = signal<CustomerAcquisitionAndLoyaltyReportDto | null>(null);
  isLoading = signal(false);

  fromDate = '';
  toDate = '';
  viewType: 'Monthly' | 'Territory' = 'Monthly';

  ngOnInit(): void {
    const now = new Date();
    const startOfYear = new Date(now.getFullYear(), 0, 1);
    this.fromDate = startOfYear.toISOString().split('T')[0];
    this.toDate = now.toISOString().split('T')[0];
    this.loadReport();
  }

  onViewTypeChange(): void {
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.service
      .getCustomerAcquisitionAndLoyaltyReport({
        companyId,
        fromDate: this.fromDate ? new Date(this.fromDate).toISOString() : new Date().toISOString(),
        toDate: this.toDate ? new Date(this.toDate).toISOString() : new Date().toISOString(),
        treeView: this.viewType === 'Territory',
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
    if (!rep || !rep.rows || !rep.rows.length) return;

    const keyHeader = this.viewType === 'Monthly' ? 'Month' : 'Territory';
    const exportRows = rep.rows.map((r) => ({
      key: r.key || '',
      newCustomerCount: r.newCustomerCount,
      repeatCustomerCount: r.repeatCustomerCount,
      totalCustomers: r.newCustomerCount + r.repeatCustomerCount,
      newCustomerRevenue: r.newCustomerRevenue,
      repeatCustomerRevenue: r.repeatCustomerRevenue,
      totalRevenue: r.totalRevenue,
    }));

    exportToCsv(
      `Customer_Acquisition_Loyalty_${new Date().toISOString().split('T')[0]}.csv`,
      exportRows,
      [
        'key',
        'newCustomerCount',
        'repeatCustomerCount',
        'totalCustomers',
        'newCustomerRevenue',
        'repeatCustomerRevenue',
        'totalRevenue',
      ]
    );
  }
}
