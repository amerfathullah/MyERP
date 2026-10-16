import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { CustomerWiseItemPriceService } from '../../../proxy/sales/customer-wise-item-price.service';
import { CustomerService } from '../../../proxy/sales/customer.service';
import type {
  CustomerWiseItemPriceReportDto,
  CustomerWiseItemPriceRowDto,
  CustomerDto,
} from '../../../proxy/sales/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-customer-wise-item-price',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-tags text-primary me-2"></i>
            {{ '::CustomerWiseItemPrice' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::CustomerWiseItemPriceDescription' | abpLocalization }}
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
            <div class="col-md-5">
              <label class="form-label small mb-1">{{ '::Customer' | abpLocalization }} *</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedCustomerId"
                (ngModelChange)="onCustomerChange()"
              >
                <option value="">-- {{ '::SelectCustomer' | abpLocalization }} --</option>
                @for (cust of customers(); track cust.id) {
                  <option [value]="cust.id">
                    {{ cust.name }} @if (cust.customerCode) { ({{ cust.customerCode }}) }
                  </option>
                }
              </select>
            </div>
            <div class="col-md-4">
              <label class="form-label small mb-1">{{ '::Item' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                placeholder="Search item code or name..."
                [(ngModel)]="searchTerm"
              />
            </div>
            <div class="col-md d-flex align-items-end">
              <button
                class="btn btn-primary btn-sm ms-auto px-4"
                (click)="loadReport()"
                [disabled]="isLoading() || !selectedCustomerId"
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
          <div class="col-12 col-md-4">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::Customer' | abpLocalization }}</div>
              <div class="h6 mb-0 fw-bold text-dark">
                {{ report()!.customerName }}
              </div>
              @if (report()!.defaultPriceListName) {
                <small class="text-muted">
                  {{ '::PriceList' | abpLocalization }}:
                  <span class="badge bg-info text-dark">{{ report()!.defaultPriceListName }}</span>
                </small>
              }
            </div>
          </div>
          <div class="col-12 col-md-4">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::TotalItems' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-primary">
                {{ filteredRows.length }}
              </div>
            </div>
          </div>
          <div class="col-12 col-md-4">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::AvailableStock' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-success">
                {{ report()!.totalAvailableStock | number:'1.2-2' }}
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
                <th>{{ '::ItemCode' | abpLocalization }}</th>
                <th>{{ '::ItemName' | abpLocalization }}</th>
                <th class="text-end">{{ '::SellingRate' | abpLocalization }}</th>
                <th>{{ '::PriceList' | abpLocalization }}</th>
                <th class="text-end">{{ '::AvailableStock' | abpLocalization }}</th>
                <th>{{ '::Uom' | abpLocalization }}</th>
              </tr>
            </thead>
            <tbody>
              @if (!selectedCustomerId) {
                <tr>
                  <td colspan="6" class="text-center py-4 text-muted">
                    <i class="fa fa-info-circle me-1"></i>
                    {{ '::SelectCustomer' | abpLocalization }}
                  </td>
                </tr>
              } @else if (isLoading()) {
                <tr>
                  <td colspan="6" class="text-center py-4 text-muted">
                    <span class="spinner-border spinner-border-sm me-2"></span>
                    {{ '::Loading' | abpLocalization }}...
                  </td>
                </tr>
              } @else if (filteredRows.length === 0) {
                <tr>
                  <td colspan="6" class="text-center py-4 text-muted">
                    {{ '::NoCustomerPricesFound' | abpLocalization }}
                  </td>
                </tr>
              } @else {
                @for (row of filteredRows; track row.itemId) {
                  <tr>
                    <td class="fw-semibold">{{ row.itemCode }}</td>
                    <td>{{ row.itemName }}</td>
                    <td class="text-end fw-semibold text-primary">
                      {{ row.sellingRate | number:'1.2-2' }}
                    </td>
                    <td>
                      @if (row.priceListName) {
                        <span class="badge bg-light text-dark border">
                          {{ row.priceListName }}
                        </span>
                      } @else {
                        <span class="text-muted">-</span>
                      }
                    </td>
                    <td class="text-end">
                      <span [class.text-success]="row.availableStock > 0" [class.text-muted]="row.availableStock <= 0">
                        {{ row.availableStock | number:'1.2-2' }}
                      </span>
                    </td>
                    <td><small class="text-muted">{{ row.stockUom }}</small></td>
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
export class CustomerWiseItemPriceComponent implements OnInit {
  private service = inject(CustomerWiseItemPriceService);
  private customerService = inject(CustomerService);
  private companyContext = inject(CompanyContextService);

  customers = signal<CustomerDto[]>([]);
  selectedCustomerId = '';
  searchTerm = '';

  isLoading = signal(false);
  report = signal<CustomerWiseItemPriceReportDto | null>(null);

  ngOnInit(): void {
    this.loadCustomers();
  }

  loadCustomers(): void {
    this.customerService
      .getList({
        maxResultCount: 1000,
        skipCount: 0,
      })
      .subscribe({
        next: (res) => {
          this.customers.set(res.items || []);
        },
      });
  }

  onCustomerChange(): void {
    if (this.selectedCustomerId) {
      this.loadReport();
    } else {
      this.report.set(null);
    }
  }

  loadReport(): void {
    if (!this.selectedCustomerId) return;
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);

    this.service
      .getReport({
        companyId,
        customerId: this.selectedCustomerId,
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

  get filteredRows(): CustomerWiseItemPriceRowDto[] {
    const r = this.report();
    if (!r || !r.rows) return [];
    if (!this.searchTerm.trim()) return r.rows;

    const term = this.searchTerm.toLowerCase().trim();
    return r.rows.filter(
      (row) =>
        row.itemCode?.toLowerCase().includes(term) ||
        row.itemName?.toLowerCase().includes(term) ||
        row.priceListName?.toLowerCase().includes(term)
    );
  }

  exportCsv(): void {
    const rows = this.filteredRows;
    if (rows.length === 0) return;

    const exportData = rows.map((r) => ({
      ItemCode: r.itemCode,
      ItemName: r.itemName,
      SellingRate: r.sellingRate,
      PriceList: r.priceListName ?? '',
      AvailableStock: r.availableStock,
      StockUom: r.stockUom,
    }));

    exportToCsv(
      `Customer-Wise-Item-Price-${new Date().toISOString().substring(0, 10)}`,
      exportData,
      ['ItemCode', 'ItemName', 'SellingRate', 'PriceList', 'AvailableStock', 'StockUom']
    );
  }
}
