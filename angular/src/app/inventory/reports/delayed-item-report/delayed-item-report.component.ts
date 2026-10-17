import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { DelayedItemReportService } from '../../../proxy/inventory/delayed-item-report.service';
import type { DelayedItemReportDto, DelayedItemRowDto } from '../../../proxy/inventory/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-delayed-item-report',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-clock me-2 text-warning"></i>{{ '::DelayedItemReport' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::DelayedItemReportDescription' | abpLocalization }}</small>
        </div>
        <div class="d-flex gap-2">
          @if (data() && data()!.rows && data()!.rows.length > 0) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
      </div>

      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::BasedOn' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="basedOn" (change)="loadData()">
              <option value="Delivery Note">Delivery Note</option>
              <option value="Sales Invoice">Sales Invoice</option>
            </select>
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::FromDate' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" />
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::ToDate' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" />
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Search' | abpLocalization }}</label>
            <input
              type="text"
              class="form-control form-control-sm"
              [placeholder]="'::Search' | abpLocalization"
              [(ngModel)]="searchTerm"
            />
          </div>
          <div class="col-md-2 col-sm-6">
            <div class="form-check mb-1">
              <input
                class="form-check-input"
                type="checkbox"
                id="delayedOnlyCheck"
                [(ngModel)]="delayedOnly"
                (change)="loadData()"
              />
              <label class="form-check-label small fw-semibold" for="delayedOnlyCheck">
                {{ '::DelayedOnly' | abpLocalization }}
              </label>
            </div>
          </div>
          <div class="col-md-2 col-sm-6">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Summary KPI Cards -->
        @if (data()) {
          <div class="row g-3 mb-4">
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalItems' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-dark mt-1">
                    {{ data()!.totalItems }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::DelayedItems' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-danger mt-1">
                    {{ data()!.delayedItems }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::AverageDelayDays' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-warning mt-1">
                    {{ data()!.averageDelayDays | number: '1.1-2' }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalDelayedAmount' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-danger mt-1">
                    {{ data()!.totalDelayedAmount | number: '1.2-2' }}
                  </div>
                </div>
              </div>
            </div>
          </div>
        }

        <!-- Table -->
        @if (isLoading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">Loading...</span>
            </div>
          </div>
        } @else if (filteredRows().length === 0) {
          <div class="text-center py-5 text-muted">
            <i class="fa fa-info-circle fa-2x mb-2 d-block"></i>
            {{ '::NoDelayedItemsFound' | abpLocalization }}
          </div>
        } @else {
          <div class="table-responsive">
            <table class="table table-hover align-middle table-sm border">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Voucher' | abpLocalization }}</th>
                  <th>{{ '::PostingDate' | abpLocalization }}</th>
                  <th>{{ '::Customer' | abpLocalization }}</th>
                  <th>{{ '::SalesOrder' | abpLocalization }}</th>
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Quantity' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Rate' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Amount' | abpLocalization }}</th>
                  <th>{{ '::ExpectedDeliveryDate' | abpLocalization }}</th>
                  <th>{{ '::ActualDeliveryDate' | abpLocalization }}</th>
                  <th class="text-center">{{ '::DelayedDays' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track $index) {
                  <tr>
                    <td>
                      <span class="badge bg-secondary me-1">{{ row.voucherType }}</span>
                      <span class="fw-semibold">{{ row.voucherNumber }}</span>
                    </td>
                    <td>{{ row.postingDate | date: 'yyyy-MM-dd' }}</td>
                    <td>{{ row.customerName }}</td>
                    <td>
                      @if (row.salesOrderNumber) {
                        <span class="text-primary">{{ row.salesOrderNumber }}</span>
                        @if (row.customerPoNumber) {
                          <br /><small class="text-muted">PO: {{ row.customerPoNumber }}</small>
                        }
                      } @else {
                        <span class="text-muted">-</span>
                      }
                    </td>
                    <td>
                      <div class="fw-semibold">{{ row.itemCode }}</div>
                      <small class="text-muted">{{ row.itemName }}</small>
                    </td>
                    <td class="text-end">{{ row.quantity | number: '1.2-4' }} {{ row.uom }}</td>
                    <td class="text-end">{{ row.rate | number: '1.2-2' }}</td>
                    <td class="text-end">{{ row.amount | number: '1.2-2' }}</td>
                    <td>{{ row.expectedDeliveryDate | date: 'yyyy-MM-dd' }}</td>
                    <td>{{ row.actualDeliveryDate | date: 'yyyy-MM-dd' }}</td>
                    <td class="text-center">
                      @if (row.delayedDays > 0) {
                        <span class="badge bg-danger">+{{ row.delayedDays }} d</span>
                      } @else {
                        <span class="badge bg-success">{{ row.delayedDays }} d</span>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>
  `,
})
export class DelayedItemReportComponent implements OnInit {
  private delayedItemReportService = inject(DelayedItemReportService);
  private companyContextService = inject(CompanyContextService);

  data = signal<DelayedItemReportDto | null>(null);
  isLoading = signal(false);

  basedOn = 'Delivery Note';
  fromDate: string | null = null;
  toDate: string | null = null;
  delayedOnly = true;
  searchTerm = '';

  filteredRows = computed(() => {
    const report = this.data();
    if (!report || !report.rows) return [];
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return report.rows;
    return report.rows.filter(
      r =>
        r.voucherNumber.toLowerCase().includes(term) ||
        r.customerName.toLowerCase().includes(term) ||
        r.itemCode.toLowerCase().includes(term) ||
        r.itemName.toLowerCase().includes(term) ||
        (r.salesOrderNumber && r.salesOrderNumber.toLowerCase().includes(term))
    );
  });

  ngOnInit(): void {
    const today = new Date();
    const firstDay = new Date(today.getFullYear(), today.getMonth(), 1);
    this.fromDate = firstDay.toISOString().split('T')[0];
    this.toDate = today.toISOString().split('T')[0];
    this.loadData();
  }

  loadData(): void {
    const companyId = this.companyContextService.selectedCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.delayedItemReportService
      .getReport({
        companyId,
        basedOn: this.basedOn,
        fromDate: this.fromDate,
        toDate: this.toDate,
        delayedOnly: this.delayedOnly,
      })
      .subscribe({
        next: res => {
          this.data.set(res);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
        },
      });
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (!rows || rows.length === 0) return;

    const exportData = rows.map(r => ({
      voucherType: r.voucherType,
      voucherNumber: r.voucherNumber,
      postingDate: r.postingDate,
      customerName: r.customerName,
      salesOrderNumber: r.salesOrderNumber || '',
      customerPoNumber: r.customerPoNumber || '',
      itemCode: r.itemCode,
      itemName: r.itemName,
      quantity: r.quantity,
      uom: r.uom,
      rate: r.rate,
      amount: r.amount,
      expectedDeliveryDate: r.expectedDeliveryDate,
      actualDeliveryDate: r.actualDeliveryDate,
      delayedDays: r.delayedDays,
    }));

    exportToCsv(
      `delayed_item_report_${new Date().toISOString().split('T')[0]}.csv`,
      exportData,
      [
        'voucherType',
        'voucherNumber',
        'postingDate',
        'customerName',
        'salesOrderNumber',
        'customerPoNumber',
        'itemCode',
        'itemName',
        'quantity',
        'uom',
        'rate',
        'amount',
        'expectedDeliveryDate',
        'actualDeliveryDate',
        'delayedDays',
      ]
    );
  }
}
