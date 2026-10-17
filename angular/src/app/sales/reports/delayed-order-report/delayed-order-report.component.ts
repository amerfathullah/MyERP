import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { DelayedOrderReportService } from '../../../proxy/sales/delayed-order-report.service';
import type { DelayedOrderReportDto, DelayedOrderRowDto } from '../../../proxy/sales/models';
import { CustomerService } from '../../../proxy/sales/customer.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-delayed-order-report',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-clock me-2 text-danger"></i>{{ '::DelayedOrderReport' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::DelayedOrderReportDescription' | abpLocalization }}</small>
        </div>
        @if (data() && data()!.rows && data()!.rows.length > 0) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-3 align-items-end">
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::FromDate' | abpLocalization }} *</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" />
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::ToDate' | abpLocalization }} *</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" />
          </div>
          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Customer' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedCustomerId" (change)="loadData()">
              <option value="">{{ '::AllCustomers' | abpLocalization }}</option>
              @for (c of customers(); track c.id) {
                <option [value]="c.id">{{ c.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-3 col-sm-6 d-flex align-items-center gap-3 pt-4">
            <div class="form-check form-switch mb-0">
              <input
                class="form-check-input"
                type="checkbox"
                id="delayedOnlySwitch"
                [(ngModel)]="delayedOnly"
                (change)="loadData()"
              />
              <label class="form-check-label small" for="delayedOnlySwitch">Delayed Only</label>
            </div>
            <div class="form-check form-switch mb-0">
              <input
                class="form-check-input"
                type="checkbox"
                id="itemDetailsSwitch"
                [(ngModel)]="showItemDetails"
              />
              <label class="form-check-label small" for="itemDetailsSwitch">Item Details</label>
            </div>
          </div>
          <div class="col-md-2">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Search Bar -->
        <div class="row g-2 mb-4 align-items-center">
          <div class="col-md-6">
            <div class="input-group input-group-sm">
              <span class="input-group-text"><i class="fa fa-search"></i></span>
              <input
                type="text"
                class="form-control"
                [placeholder]="'::Search' | abpLocalization"
                [(ngModel)]="searchTerm"
              />
            </div>
          </div>
          <div class="col-md-6 text-end text-muted small">
            @if (data()) {
              <span>{{ '::Showing' | abpLocalization }} {{ filteredRows().length }} {{ '::Entries' | abpLocalization }}</span>
            }
          </div>
        </div>

        <!-- KPI Cards -->
        @if (data() && data()!.totalDeliveries > 0) {
          <div class="row g-3 mb-4">
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalDeliveries' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-dark mt-1">
                    {{ data()!.totalDeliveries }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::DelayedDeliveries' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-danger mt-1">
                    {{ data()!.delayedDeliveries }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::OnTimeDeliveryRate' | abpLocalization }}</div>
                  <div class="h3 mb-0 mt-1" [class.text-success]="data()!.onTimeDeliveryRate >= 90" [class.text-warning]="data()!.onTimeDeliveryRate < 90 && data()!.onTimeDeliveryRate >= 75" [class.text-danger]="data()!.onTimeDeliveryRate < 75">
                    {{ data()!.onTimeDeliveryRate | number: '1.1-1' }}%
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::AverageDelayDays' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-warning mt-1">
                    {{ data()!.averageDelayDays | number: '1.1-1' }}d
                  </div>
                </div>
              </div>
            </div>
          </div>
        }

        <!-- Loading State -->
        @if (isLoading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">Loading...</span>
            </div>
            <div class="mt-2 text-muted small">{{ '::Loading' | abpLocalization }}...</div>
          </div>
        }

        <!-- Table State -->
        @if (!isLoading() && filteredRows().length > 0) {
          <div class="table-responsive">
            <table class="table table-hover table-bordered table-sm align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th style="min-width: 130px;">{{ '::DeliveryNote' | abpLocalization }}</th>
                  <th style="min-width: 140px;">{{ '::SalesOrder' | abpLocalization }}</th>
                  <th style="min-width: 180px;">{{ '::Customer' | abpLocalization }}</th>
                  <th style="width: 130px;">{{ '::ExpectedDeliveryDate' | abpLocalization }}</th>
                  <th style="width: 130px;">{{ '::ActualDeliveryDate' | abpLocalization }}</th>
                  <th class="text-center" style="width: 110px;">{{ '::DelayedDays' | abpLocalization }}</th>
                  <th class="text-end" style="width: 120px;">{{ '::Amount' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track row.deliveryNoteId) {
                  <tr>
                    <td>
                      <span class="fw-semibold text-primary font-monospace">{{ row.deliveryNumber }}</span>
                    </td>
                    <td>
                      <div class="fw-semibold font-monospace">{{ row.orderNumber }}</div>
                      @if (row.customerPoNumber) {
                        <small class="text-muted">PO: {{ row.customerPoNumber }}</small>
                      }
                    </td>
                    <td>
                      <div class="fw-semibold">{{ row.customerName }}</div>
                      @if (row.shippingAddress) {
                        <small class="text-muted text-truncate d-block" style="max-width: 200px;">{{ row.shippingAddress }}</small>
                      }
                    </td>
                    <td class="text-nowrap small font-monospace">
                      {{ row.expectedDeliveryDate | date: 'mediumDate' }}
                    </td>
                    <td class="text-nowrap small font-monospace">
                      {{ row.actualDeliveryDate | date: 'mediumDate' }}
                    </td>
                    <td class="text-center">
                      @if (row.delayedDays > 0) {
                        <span class="badge bg-danger">+{{ row.delayedDays }}d late</span>
                      } @else {
                        <span class="badge bg-success">On-time</span>
                      }
                    </td>
                    <td class="text-end font-monospace fw-semibold">
                      {{ row.grandTotal | number: '1.2-2' }}
                    </td>
                  </tr>
                  @if (showItemDetails && row.items && row.items.length > 0) {
                    <tr class="table-light">
                      <td colspan="7" class="p-2 ps-4">
                        <table class="table table-sm table-bordered bg-white mb-0">
                          <thead>
                            <tr class="small text-muted">
                              <th>{{ '::Item' | abpLocalization }}</th>
                              <th class="text-end">{{ '::Quantity' | abpLocalization }}</th>
                              <th class="text-end">{{ '::UnitPrice' | abpLocalization }}</th>
                              <th class="text-end">{{ '::Total' | abpLocalization }}</th>
                            </tr>
                          </thead>
                          <tbody>
                            @for (item of row.items; track item.itemId) {
                              <tr class="small">
                                <td>{{ item.itemCode }} — {{ item.itemName }}</td>
                                <td class="text-end font-monospace">{{ item.quantity | number: '1.2-2' }} {{ item.uom }}</td>
                                <td class="text-end font-monospace">{{ item.unitPrice | number: '1.2-2' }}</td>
                                <td class="text-end font-monospace">{{ item.lineTotal | number: '1.2-2' }}</td>
                              </tr>
                            }
                          </tbody>
                        </table>
                      </td>
                    </tr>
                  }
                }
              </tbody>
              <tfoot class="table-light fw-bold">
                <tr>
                  <td colspan="5">{{ '::Total' | abpLocalization }} ({{ filteredRows().length }} deliveries)</td>
                  <td class="text-center text-danger">{{ data()!.delayedDeliveries }} delayed</td>
                  <td class="text-end font-monospace">{{ data()!.totalDelayedAmount | number: '1.2-2' }}</td>
                </tr>
              </tfoot>
            </table>
          </div>
        }

        <!-- Empty State -->
        @if (!isLoading() && (!data() || filteredRows().length === 0)) {
          <div class="text-center py-5 text-muted">
            <i class="fa fa-truck fa-3x mb-3 text-secondary"></i>
            <p class="mb-0">{{ '::NoDataAvailable' | abpLocalization }}</p>
          </div>
        }
      </div>
    </div>
  `,
})
export class DelayedOrderReportComponent implements OnInit {
  private delayedOrderService = inject(DelayedOrderReportService);
  private customerService = inject(CustomerService);
  private companyContext = inject(CompanyContextService);

  fromDate = '';
  toDate = '';
  selectedCustomerId = '';
  delayedOnly = true;
  showItemDetails = false;
  searchTerm = '';

  customers = signal<{ id: string; name: string }[]>([]);
  data = signal<DelayedOrderReportDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    const today = new Date();
    const yearStart = new Date(today.getFullYear(), 0, 1);

    this.fromDate = yearStart.toISOString().split('T')[0];
    this.toDate = today.toISOString().split('T')[0];

    this.loadCustomers();
    this.loadData();
  }

  loadCustomers(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.customerService.getList({ maxResultCount: 200 } as any).subscribe({
      next: (res) => {
        const list = res.items || [];
        this.customers.set(list.map((c: any) => ({ id: c.id, name: c.name })));
      },
    });
  }

  loadData(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId || !this.fromDate || !this.toDate) return;

    this.isLoading.set(true);

    this.delayedOrderService
      .getReport({
        companyId,
        fromDate: this.fromDate,
        toDate: this.toDate,
        customerId: this.selectedCustomerId || null,
        delayedOnly: this.delayedOnly,
        searchText: this.searchTerm || null,
      })
      .subscribe({
        next: (result) => {
          this.data.set(result);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
        },
      });
  }

  filteredRows(): DelayedOrderRowDto[] {
    const report = this.data();
    if (!report || !report.rows) return [];

    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return report.rows;

    return report.rows.filter(
      (r) =>
        r.customerName.toLowerCase().includes(term) ||
        r.deliveryNumber.toLowerCase().includes(term) ||
        r.orderNumber.toLowerCase().includes(term) ||
        (r.customerPoNumber && r.customerPoNumber.toLowerCase().includes(term))
    );
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (!rows || rows.length === 0) return;

    const exportData = rows.map((r) => ({
      'Delivery Note': r.deliveryNumber,
      'Sales Order': r.orderNumber,
      'Customer': r.customerName,
      'Customer PO': r.customerPoNumber || '',
      'Expected Delivery Date': r.expectedDeliveryDate.split('T')[0],
      'Actual Delivery Date': r.actualDeliveryDate.split('T')[0],
      'Delayed Days': r.delayedDays,
      'Grand Total': r.grandTotal,
    }));

    const columns = [
      'Delivery Note',
      'Sales Order',
      'Customer',
      'Customer PO',
      'Expected Delivery Date',
      'Actual Delivery Date',
      'Delayed Days',
      'Grand Total',
    ];

    exportToCsv(`delayed_order_report_${this.fromDate}_${this.toDate}.csv`, exportData, columns);
  }
}
