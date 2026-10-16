import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { InactiveCustomersService } from '../../../proxy/sales/inactive-customers.service';
import type {
  InactiveCustomersReportDto,
  InactiveCustomerItemDto,
} from '../../../proxy/sales/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-inactive-customers',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-user-clock text-warning me-2"></i>
            {{ '::InactiveCustomers' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::InactiveCustomersDescription' | abpLocalization }}
          </p>
        </div>
        @if (report() && report()!.items && report()!.items.length > 0) {
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
              <label class="form-label small mb-1">{{ '::DocType' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="docType"
                (change)="loadReport()"
              >
                <option value="Sales Order">Sales Order</option>
                <option value="Sales Invoice">Sales Invoice</option>
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::DaysSinceLastOrder' | abpLocalization }}</label>
              <input
                type="number"
                min="1"
                class="form-control form-control-sm"
                [(ngModel)]="daysSinceLastOrder"
                (change)="loadReport()"
              />
            </div>
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::Customer' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                placeholder="Filter by customer name or code"
                [(ngModel)]="customerSearch"
              />
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::AsOfDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="asOfDate"
                (change)="loadReport()"
              />
            </div>
            <div class="col-md-3 d-flex align-items-end">
              <button
                class="btn btn-primary btn-sm w-100"
                (click)="loadReport()"
                [disabled]="isLoading()"
              >
                @if (isLoading()) {
                  <span class="spinner-border spinner-border-sm me-1"></span>
                } @else {
                  <i class="fa fa-sync-alt me-1"></i>
                }
                {{ '::Refresh' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- KPI Summary Cards -->
      @if (report()) {
        <div class="row g-2 mb-3">
          <div class="col-6 col-md-3">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::InactiveCustomers' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-danger">
                {{ filteredItems.length }}
              </div>
            </div>
          </div>
          <div class="col-6 col-md-3">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::TotalOrders' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-secondary">
                {{ totalOrdersFiltered }}
              </div>
            </div>
          </div>
          <div class="col-6 col-md-3">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::TotalOrderValue' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-primary">
                {{ totalOrderValueFiltered | number:'1.2-2' }}
              </div>
            </div>
          </div>
          <div class="col-6 col-md-3">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::TotalOrderConsidered' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-success">
                {{ totalOrderConsideredFiltered | number:'1.2-2' }}
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Data Table -->
      <div class="card shadow-sm border-0">
        <div class="table-responsive">
          <table class="table table-hover table-striped table-sm mb-0 align-middle">
            <thead class="table-light">
              <tr>
                <th>{{ '::Customer' | abpLocalization }}</th>
                <th>{{ '::Territory' | abpLocalization }}</th>
                <th>{{ '::CustomerGroup' | abpLocalization }}</th>
                <th class="text-end">{{ '::NumOfOrder' | abpLocalization }}</th>
                <th class="text-end">{{ '::TotalOrderValue' | abpLocalization }}</th>
                <th class="text-end">{{ '::TotalOrderConsidered' | abpLocalization }}</th>
                <th class="text-end">{{ '::LastOrderAmount' | abpLocalization }}</th>
                <th>{{ '::LastOrderDate' | abpLocalization }}</th>
                <th class="text-end">{{ '::DaysSinceLastOrder' | abpLocalization }}</th>
              </tr>
            </thead>
            <tbody>
              @if (isLoading()) {
                <tr>
                  <td colspan="9" class="text-center py-4 text-muted">
                    <span class="spinner-border spinner-border-sm me-2"></span>
                    {{ '::Loading' | abpLocalization }}...
                  </td>
                </tr>
              } @else if (filteredItems.length === 0) {
                <tr>
                  <td colspan="9" class="text-center py-4 text-muted">
                    {{ '::NoInactiveCustomersFound' | abpLocalization }}
                  </td>
                </tr>
              } @else {
                @for (item of filteredItems; track item.customerId) {
                  <tr>
                    <td>
                      <div class="fw-semibold">{{ item.customerName }}</div>
                      @if (item.customerCode) {
                        <small class="text-muted">{{ item.customerCode }}</small>
                      }
                    </td>
                    <td>{{ item.territory || '-' }}</td>
                    <td>{{ item.customerGroup || '-' }}</td>
                    <td class="text-end">{{ item.numOfOrder }}</td>
                    <td class="text-end">{{ item.totalOrderValue | number:'1.2-2' }}</td>
                    <td class="text-end">{{ item.totalOrderConsidered | number:'1.2-2' }}</td>
                    <td class="text-end">{{ item.lastOrderAmount | number:'1.2-2' }}</td>
                    <td>{{ item.lastOrderDate | date:'yyyy-MM-dd' }}</td>
                    <td class="text-end">
                      <span
                        class="badge"
                        [ngClass]="item.daysSinceLastOrder > 120 ? 'bg-danger' : item.daysSinceLastOrder > 60 ? 'bg-warning text-dark' : 'bg-info text-dark'"
                      >
                        {{ item.daysSinceLastOrder }} d
                      </span>
                    </td>
                  </tr>
                }
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `,
})
export class InactiveCustomersComponent implements OnInit {
  private service = inject(InactiveCustomersService);
  private companyContext = inject(CompanyContextService);

  docType = 'Sales Order';
  daysSinceLastOrder = 60;
  customerSearch = '';
  asOfDate = new Date().toISOString().substring(0, 10);

  isLoading = signal(false);
  report = signal<InactiveCustomersReportDto | null>(null);

  ngOnInit(): void {
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    this.isLoading.set(true);

    this.service
      .getReport({
        companyId: companyId || undefined,
        docType: this.docType,
        daysSinceLastOrder: this.daysSinceLastOrder > 0 ? this.daysSinceLastOrder : 60,
        asOfDate: this.asOfDate ? new Date(this.asOfDate).toISOString() : undefined,
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

  get filteredItems(): InactiveCustomerItemDto[] {
    const r = this.report();
    if (!r || !r.items) return [];
    if (!this.customerSearch.trim()) return r.items;

    const term = this.customerSearch.toLowerCase().trim();
    return r.items.filter(
      (item) =>
        item.customerName?.toLowerCase().includes(term) ||
        item.customerCode?.toLowerCase().includes(term) ||
        item.territory?.toLowerCase().includes(term)
    );
  }

  get totalOrdersFiltered(): number {
    return this.filteredItems.reduce((acc, x) => acc + (x.numOfOrder || 0), 0);
  }

  get totalOrderValueFiltered(): number {
    return this.filteredItems.reduce((acc, x) => acc + (x.totalOrderValue || 0), 0);
  }

  get totalOrderConsideredFiltered(): number {
    return this.filteredItems.reduce((acc, x) => acc + (x.totalOrderConsidered || 0), 0);
  }

  exportCsv(): void {
    const items = this.filteredItems;
    if (items.length === 0) return;

    const rows = items.map((r) => ({
      Customer: r.customerName,
      Code: r.customerCode ?? '',
      Territory: r.territory ?? '',
      CustomerGroup: r.customerGroup ?? '',
      Orders: r.numOfOrder,
      TotalOrderValue: r.totalOrderValue,
      TotalOrderConsidered: r.totalOrderConsidered,
      LastOrderAmount: r.lastOrderAmount,
      LastOrderDate: r.lastOrderDate ? r.lastOrderDate.substring(0, 10) : '',
      DaysSinceLastOrder: r.daysSinceLastOrder,
    }));

    exportToCsv(
      `Inactive-Customers-${this.docType.replace(/\s+/g, '_')}-${new Date().toISOString().substring(0, 10)}`,
      rows,
      [
        'Customer',
        'Code',
        'Territory',
        'CustomerGroup',
        'Orders',
        'TotalOrderValue',
        'TotalOrderConsidered',
        'LastOrderAmount',
        'LastOrderDate',
        'DaysSinceLastOrder',
      ]
    );
  }
}
