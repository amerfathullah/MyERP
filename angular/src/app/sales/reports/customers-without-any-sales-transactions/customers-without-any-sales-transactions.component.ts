import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { CustomersWithoutAnySalesTransactionsService } from '../../../proxy/sales/customers-without-any-sales-transactions.service';
import type {
  CustomersWithoutAnySalesTransactionsReportDto,
  CustomerWithoutSalesItemDto,
} from '../../../proxy/sales/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-customers-without-any-sales-transactions',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-user-slash text-danger me-2"></i>
            {{ '::CustomersWithoutAnySalesTransactions' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::CustomersWithoutAnySalesTransactionsDescription' | abpLocalization }}
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
            <div class="col-md-6">
              <label class="form-label small mb-1">{{ '::Customer' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                placeholder="Filter by customer name, code or territory..."
                [(ngModel)]="customerSearch"
              />
            </div>
            <div class="col-md-6 d-flex align-items-end">
              <button
                class="btn btn-primary btn-sm ms-auto px-4"
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

      <!-- KPI Summary Card -->
      @if (report()) {
        <div class="row g-2 mb-3">
          <div class="col-12 col-md-4">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::CustomersWithoutAnySalesTransactions' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-danger">
                {{ filteredItems.length }}
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
                <th>{{ '::CustomerSince' | abpLocalization }}</th>
              </tr>
            </thead>
            <tbody>
              @if (isLoading()) {
                <tr>
                  <td colspan="4" class="text-center py-4 text-muted">
                    <span class="spinner-border spinner-border-sm me-2"></span>
                    {{ '::Loading' | abpLocalization }}...
                  </td>
                </tr>
              } @else if (filteredItems.length === 0) {
                <tr>
                  <td colspan="4" class="text-center py-4 text-muted">
                    {{ '::NoCustomersWithoutSalesFound' | abpLocalization }}
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
                    <td>{{ item.creationTime | date:'yyyy-MM-dd' }}</td>
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
export class CustomersWithoutAnySalesTransactionsComponent implements OnInit {
  private service = inject(CustomersWithoutAnySalesTransactionsService);
  private companyContext = inject(CompanyContextService);

  customerSearch = '';
  isLoading = signal(false);
  report = signal<CustomersWithoutAnySalesTransactionsReportDto | null>(null);

  ngOnInit(): void {
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    this.isLoading.set(true);

    this.service
      .getReport({
        companyId: companyId || undefined,
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

  get filteredItems(): CustomerWithoutSalesItemDto[] {
    const r = this.report();
    if (!r || !r.items) return [];
    if (!this.customerSearch.trim()) return r.items;

    const term = this.customerSearch.toLowerCase().trim();
    return r.items.filter(
      (item) =>
        item.customerName?.toLowerCase().includes(term) ||
        item.customerCode?.toLowerCase().includes(term) ||
        item.territory?.toLowerCase().includes(term) ||
        item.customerGroup?.toLowerCase().includes(term)
    );
  }

  exportCsv(): void {
    const items = this.filteredItems;
    if (items.length === 0) return;

    const rows = items.map((r) => ({
      Customer: r.customerName,
      Code: r.customerCode ?? '',
      Territory: r.territory ?? '',
      CustomerGroup: r.customerGroup ?? '',
      CustomerSince: r.creationTime ? r.creationTime.substring(0, 10) : '',
    }));

    exportToCsv(
      `Customers-Without-Sales-${new Date().toISOString().substring(0, 10)}`,
      rows,
      ['Customer', 'Code', 'Territory', 'CustomerGroup', 'CustomerSince']
    );
  }
}
