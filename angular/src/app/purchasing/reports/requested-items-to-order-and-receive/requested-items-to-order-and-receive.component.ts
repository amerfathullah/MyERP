import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { RequestedItemsToOrderAndReceiveService } from '../../../proxy/purchasing/requested-items-to-order-and-receive.service';
import type {
  RequestedItemsToOrderAndReceiveReportDto,
  RequestedItemToOrderAndReceiveRowDto,
} from '../../../proxy/purchasing/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-requested-items-to-order-and-receive',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-boxes-packing text-primary me-2"></i>
            {{ '::RequestedItemsToOrderAndReceive' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::RequestedItemsToOrderAndReceiveDescription' | abpLocalization }}
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
              <label class="form-label small mb-1">{{ '::Search' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                placeholder="{{ '::SearchPlaceholder' | abpLocalization }}"
                [(ngModel)]="searchTerm"
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

      <!-- KPI Summary Cards -->
      @if (report()) {
        <div class="row g-2 mb-3">
          <div class="col-md-2">
            <div class="card border-0 shadow-sm bg-primary-subtle text-primary py-2 px-3">
              <div class="small fw-bold">{{ '::TotalQty' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold">{{ report()!.totalQty | number:'1.0-2' }}</div>
            </div>
          </div>
          <div class="col-md-2">
            <div class="card border-0 shadow-sm bg-info-subtle text-info-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::OrderedQty' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold">{{ report()!.totalOrderedQty | number:'1.0-2' }}</div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-warning-subtle text-warning-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::QtyToOrder' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold">{{ report()!.totalQtyToOrder | number:'1.0-2' }}</div>
            </div>
          </div>
          <div class="col-md-2">
            <div class="card border-0 shadow-sm bg-success-subtle text-success py-2 px-3">
              <div class="small fw-bold">{{ '::ReceivedQty' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold">{{ report()!.totalReceivedQty | number:'1.0-2' }}</div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-danger-subtle text-danger py-2 px-3">
              <div class="small fw-bold">{{ '::QtyToReceive' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold">{{ report()!.totalQtyToReceive | number:'1.0-2' }}</div>
            </div>
          </div>
        </div>
      }

      <!-- Data Table -->
      <div class="card shadow-sm border-0">
        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5">
              <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Loading...</span>
              </div>
            </div>
          } @else if (!report() || filteredRows().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-inbox fa-3x mb-3 text-secondary"></i>
              <p class="mb-0">{{ '::NoRequestedItemsFound' | abpLocalization }}</p>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-hover table-striped table-bordered align-middle mb-0" style="font-size: 0.85rem;">
                <thead class="table-light text-nowrap">
                  <tr>
                    <th>{{ '::MaterialRequest' | abpLocalization }}</th>
                    <th>{{ '::Date' | abpLocalization }}</th>
                    <th>{{ '::RequiredDate' | abpLocalization }}</th>
                    <th>{{ '::Item' | abpLocalization }}</th>
                    <th>{{ '::Warehouse' | abpLocalization }}</th>
                    <th class="text-end">{{ '::RequestedQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::OrderedQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::QtyToOrder' | abpLocalization }}</th>
                    <th class="text-end">{{ '::ReceivedQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::QtyToReceive' | abpLocalization }}</th>
                    <th class="text-center">{{ '::UOM' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of filteredRows(); track (row.materialRequestId + '-' + row.itemId)) {
                    <tr>
                      <td class="fw-bold">
                        <a [routerLink]="['/purchasing/material-requests', row.materialRequestId]" class="text-decoration-none">
                          {{ row.materialRequestNumber }}
                        </a>
                      </td>
                      <td>{{ row.transactionDate | date:'shortDate' }}</td>
                      <td>{{ row.requiredDate ? (row.requiredDate | date:'shortDate') : '—' }}</td>
                      <td>
                        <div class="fw-semibold">{{ row.itemName }}</div>
                        <span class="badge bg-light text-muted">{{ row.itemCode }}</span>
                      </td>
                      <td>{{ row.warehouseName || '—' }}</td>
                      <td class="text-end">{{ row.qty | number:'1.0-2' }}</td>
                      <td class="text-end">{{ row.orderedQty | number:'1.0-2' }}</td>
                      <td class="text-end">
                        @if (row.qtyToOrder > 0) {
                          <span class="badge bg-warning text-dark">{{ row.qtyToOrder | number:'1.0-2' }}</span>
                        } @else {
                          <span class="text-muted">0</span>
                        }
                      </td>
                      <td class="text-end">{{ row.receivedQty | number:'1.0-2' }}</td>
                      <td class="text-end">
                        @if (row.qtyToReceive > 0) {
                          <span class="badge bg-danger">{{ row.qtyToReceive | number:'1.0-2' }}</span>
                        } @else {
                          <span class="text-muted">0</span>
                        }
                      </td>
                      <td class="text-center">
                        <span class="badge bg-secondary-subtle text-dark">{{ row.uom }}</span>
                      </td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-light fw-bold">
                  <tr class="table-info">
                    <td colspan="5">{{ '::Total' | abpLocalization }}</td>
                    <td class="text-end">{{ report()!.totalQty | number:'1.0-2' }}</td>
                    <td class="text-end">{{ report()!.totalOrderedQty | number:'1.0-2' }}</td>
                    <td class="text-end">{{ report()!.totalQtyToOrder | number:'1.0-2' }}</td>
                    <td class="text-end">{{ report()!.totalReceivedQty | number:'1.0-2' }}</td>
                    <td class="text-end">{{ report()!.totalQtyToReceive | number:'1.0-2' }}</td>
                    <td></td>
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
      padding: 0.45rem 0.6rem;
    }
  `],
})
export class RequestedItemsToOrderAndReceiveComponent implements OnInit {
  private service = inject(RequestedItemsToOrderAndReceiveService);
  private companyContext = inject(CompanyContextService);

  report = signal<RequestedItemsToOrderAndReceiveReportDto | null>(null);
  loading = signal(false);

  fromDate: string | null = null;
  toDate: string | null = null;
  searchTerm = '';

  ngOnInit(): void {
    const today = new Date();
    const lastMonth = new Date(today);
    lastMonth.setMonth(today.getMonth() - 1);

    this.fromDate = lastMonth.toISOString().split('T')[0];
    this.toDate = today.toISOString().split('T')[0];

    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.loading.set(true);

    this.service
      .getReport({
        companyId,
        fromDate: this.fromDate ? `${this.fromDate}T00:00:00Z` : undefined,
        toDate: this.toDate ? `${this.toDate}T23:59:59Z` : undefined,
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

  filteredRows(): RequestedItemToOrderAndReceiveRowDto[] {
    const r = this.report();
    if (!r || !r.rows) return [];
    if (!this.searchTerm.trim()) return r.rows;

    const term = this.searchTerm.toLowerCase();
    return r.rows.filter(
      (row) =>
        (row.materialRequestNumber && row.materialRequestNumber.toLowerCase().includes(term)) ||
        (row.itemCode && row.itemCode.toLowerCase().includes(term)) ||
        (row.itemName && row.itemName.toLowerCase().includes(term)) ||
        (row.warehouseName && row.warehouseName.toLowerCase().includes(term))
    );
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (rows.length === 0) return;

    const headers: string[] = [
      'Material Request',
      'Date',
      'Required Date',
      'Item Code',
      'Item Name',
      'Warehouse',
      'Requested Qty',
      'Ordered Qty',
      'Qty To Order',
      'Received Qty',
      'Qty To Receive',
      'UOM',
    ];

    const dataRows = rows.map((row) => ({
      'Material Request': row.materialRequestNumber || '',
      Date: row.transactionDate ? row.transactionDate.split('T')[0] : '',
      'Required Date': row.requiredDate ? row.requiredDate.split('T')[0] : '',
      'Item Code': row.itemCode || '',
      'Item Name': row.itemName || '',
      Warehouse: row.warehouseName || '',
      'Requested Qty': row.qty || 0,
      'Ordered Qty': row.orderedQty || 0,
      'Qty To Order': row.qtyToOrder || 0,
      'Received Qty': row.receivedQty || 0,
      'Qty To Receive': row.qtyToReceive || 0,
      UOM: row.uom || '',
    }));

    exportToCsv('Requested_Items_To_Order_And_Receive.csv', dataRows, headers);
  }
}
