import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ItemSalesService } from '../../../proxy/sales/item-sales.service';
import type {
  ItemWiseSalesHistoryReportDto,
  ItemWiseSalesHistoryRowDto,
} from '../../../proxy/sales/models';
import { CustomerService } from '../../../proxy/sales/customer.service';
import type { CustomerDto } from '../../../proxy/sales/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-item-wise-sales-history',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-history text-primary me-2"></i>
            {{ '::ItemWiseSalesHistory' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::ItemWiseSalesHistoryDescription' | abpLocalization }}
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
              <label class="form-label small mb-1">{{ '::Customer' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedCustomerId"
                (ngModelChange)="loadReport()"
              >
                <option value="">-- {{ '::AllCustomers' | abpLocalization }} --</option>
                @for (c of customers(); track c.id) {
                  <option [value]="c.id">{{ c.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::ItemCode' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                placeholder="{{ '::FilterByItemCode' | abpLocalization }}"
                [(ngModel)]="itemCodeFilter"
                (keyup.enter)="loadReport()"
              />
            </div>
            <div class="col-md-2 d-flex align-items-end">
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
              <div class="small fw-bold">{{ '::TotalQty' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ report()!.totalQty | number:'1.0-2' }}
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-info-subtle text-info-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::TotalAmount' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ report()!.totalAmount | number:'1.2-2' }}
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-success-subtle text-success py-2 px-3">
              <div class="small fw-bold">{{ '::TotalDeliveredQty' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ report()!.totalDeliveredQty | number:'1.0-2' }}
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-warning-subtle text-warning-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::TotalBilledAmount' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ report()!.totalBilledAmount | number:'1.2-2' }}
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
          } @else if (!report() || !report()!.rows || report()!.rows!.length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-inbox fa-3x mb-3 text-secondary"></i>
              <p class="mb-0">{{ '::NoRecordsFound' | abpLocalization }}</p>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-hover table-striped align-middle mb-0" style="font-size: 0.85rem;">
                <thead class="table-light">
                  <tr>
                    <th style="min-width: 180px;">{{ '::Item' | abpLocalization }}</th>
                    <th style="min-width: 140px;">{{ '::SalesOrder' | abpLocalization }}</th>
                    <th style="min-width: 160px;">{{ '::Customer' | abpLocalization }}</th>
                    <th class="text-end" style="min-width: 90px;">{{ '::Quantity' | abpLocalization }}</th>
                    <th class="text-end" style="min-width: 100px;">{{ '::Rate' | abpLocalization }}</th>
                    <th class="text-end" style="min-width: 110px;">{{ '::Amount' | abpLocalization }}</th>
                    <th class="text-end" style="min-width: 100px;">{{ '::DeliveredQty' | abpLocalization }}</th>
                    <th class="text-end" style="min-width: 110px;">{{ '::BilledAmount' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of report()!.rows; track (row.salesOrderId + '-' + row.itemId + '-' + $index)) {
                    <tr>
                      <td>
                        <div class="fw-bold text-dark">{{ row.itemName || row.itemCode }}</div>
                        <div class="small text-muted d-flex gap-1 align-items-center">
                          <span>{{ row.itemCode }}</span>
                          @if (row.itemGroup) {
                            <span class="badge bg-light text-secondary border">{{ row.itemGroup }}</span>
                          }
                        </div>
                      </td>
                      <td>
                        <a [routerLink]="['/sales/orders', row.salesOrderId]" class="text-decoration-none fw-semibold">
                          {{ row.salesOrderNumber }}
                        </a>
                        <div class="small text-muted">{{ row.transactionDate | date:'shortDate' }}</div>
                      </td>
                      <td>
                        <div class="fw-semibold text-dark">{{ row.customerName }}</div>
                        @if (row.customerGroup) {
                          <span class="badge bg-secondary-subtle text-dark border">{{ row.customerGroup }}</span>
                        }
                      </td>
                      <td class="text-end fw-semibold">
                        {{ row.quantity | number:'1.0-2' }}
                        <span class="small text-muted ms-1">{{ row.uom }}</span>
                      </td>
                      <td class="text-end">{{ row.rate | number:'1.2-2' }}</td>
                      <td class="text-end fw-bold text-primary">{{ row.amount | number:'1.2-2' }}</td>
                      <td class="text-end text-success">
                        {{ row.deliveredQuantity | number:'1.0-2' }}
                      </td>
                      <td class="text-end text-info fw-semibold">
                        {{ row.billedAmount | number:'1.2-2' }}
                      </td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-light fw-bold">
                  <tr class="table-info">
                    <td colspan="3">{{ '::Total' | abpLocalization }}</td>
                    <td class="text-end">{{ report()!.totalQty | number:'1.0-2' }}</td>
                    <td></td>
                    <td class="text-end text-primary">{{ report()!.totalAmount | number:'1.2-2' }}</td>
                    <td class="text-end text-success">{{ report()!.totalDeliveredQty | number:'1.0-2' }}</td>
                    <td class="text-end text-info">{{ report()!.totalBilledAmount | number:'1.2-2' }}</td>
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
export class ItemWiseSalesHistoryComponent implements OnInit {
  private itemSalesService = inject(ItemSalesService);
  private customerService = inject(CustomerService);
  private companyContext = inject(CompanyContextService);

  report = signal<ItemWiseSalesHistoryReportDto | null>(null);
  customers = signal<CustomerDto[]>([]);
  loading = signal(false);

  fromDate = '';
  toDate = '';
  selectedCustomerId = '';
  itemCodeFilter = '';

  ngOnInit(): void {
    const now = new Date();
    const threeMonthsAgo = new Date();
    threeMonthsAgo.setMonth(now.getMonth() - 3);

    this.toDate = now.toISOString().slice(0, 10);
    this.fromDate = threeMonthsAgo.toISOString().slice(0, 10);

    this.loadCustomers();
    this.loadReport();
  }

  loadCustomers(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.customerService
      .getList({
        maxResultCount: 200,
        skipCount: 0,
      })
      .subscribe({
        next: (res) => {
          this.customers.set(res.items || []);
        },
      });
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.loading.set(true);

    this.itemSalesService
      .getHistoryReport({
        companyId,
        fromDate: this.fromDate ? new Date(this.fromDate).toISOString() : undefined,
        toDate: this.toDate ? new Date(this.toDate + 'T23:59:59Z').toISOString() : undefined,
        customerId: this.selectedCustomerId || undefined,
        itemCode: this.itemCodeFilter.trim() || undefined,
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

  exportCsv(): void {
    const rep = this.report();
    if (!rep || !rep.rows || rep.rows.length === 0) return;

    const headers = [
      'Item Code',
      'Item Name',
      'Item Group',
      'Sales Order',
      'Transaction Date',
      'Customer Name',
      'Customer Group',
      'Quantity',
      'UOM',
      'Rate',
      'Amount',
      'Delivered Quantity',
      'Billed Amount',
    ];

    const dataRows = rep.rows.map((r) => ({
      'Item Code': r.itemCode,
      'Item Name': r.itemName,
      'Item Group': r.itemGroup,
      'Sales Order': r.salesOrderNumber,
      'Transaction Date': r.transactionDate?.slice(0, 10) || '',
      'Customer Name': r.customerName,
      'Customer Group': r.customerGroup,
      'Quantity': r.quantity,
      'UOM': r.uom,
      'Rate': r.rate,
      'Amount': r.amount,
      'Delivered Quantity': r.deliveredQuantity,
      'Billed Amount': r.billedAmount,
    }));

    // Grand total row
    dataRows.push({
      'Item Code': 'Total',
      'Item Name': '',
      'Item Group': '',
      'Sales Order': '',
      'Transaction Date': '',
      'Customer Name': '',
      'Customer Group': '',
      'Quantity': rep.totalQty ?? 0,
      'UOM': '',
      'Rate': 0,
      'Amount': rep.totalAmount ?? 0,
      'Delivered Quantity': rep.totalDeliveredQty ?? 0,
      'Billed Amount': rep.totalBilledAmount ?? 0,
    });

    exportToCsv('Item_Wise_Sales_History.csv', dataRows, headers);
  }
}
