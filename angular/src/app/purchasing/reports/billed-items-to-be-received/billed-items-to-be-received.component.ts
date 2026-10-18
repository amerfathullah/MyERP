import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { BilledItemsToBeReceivedService } from '../../../proxy/purchasing/billed-items-to-be-received.service';
import type {
  BilledItemsToBeReceivedReportDto,
  BilledItemToBeReceivedRowDto,
} from '../../../proxy/purchasing/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-billed-items-to-be-received',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-boxes-packing text-primary me-2"></i>
            {{ '::BilledItemsToBeReceived' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::BilledItemsToBeReceivedDescription' | abpLocalization }}
          </p>
        </div>
        <div class="d-flex gap-2">
          @if (report() && report()!.rows.length > 0) {
            <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
              <i class="fa fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
          <button class="btn btn-sm btn-primary" (click)="loadReport()" [disabled]="loading()">
            <i class="fa fa-sync me-1" [class.fa-spin]="loading()"></i>{{ '::Refresh' | abpLocalization }}
          </button>
        </div>
      </div>

      <!-- Filters -->
      <div class="card mb-3 shadow-sm border-0">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::PostingDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="postingDate"
                (ngModelChange)="loadReport()"
              />
            </div>
            <div class="col-md-4">
              <label class="form-label small mb-1">{{ '::Search' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                placeholder="Filter by invoice, supplier, item..."
                [(ngModel)]="searchTerm"
              />
            </div>
            <div class="col-md-2">
              <button
                class="btn btn-sm btn-outline-secondary w-100"
                (click)="resetFilters()"
              >
                <i class="fa fa-undo me-1"></i>{{ '::Reset' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Summary KPI Cards -->
      @if (report()) {
        <div class="row g-3 mb-3">
          <div class="col-sm-4">
            <div class="card border-0 shadow-sm bg-primary bg-opacity-10 py-2">
              <div class="card-body p-3">
                <div class="text-muted small mb-1">{{ '::TotalAmount' | abpLocalization }}</div>
                <h5 class="mb-0 fw-bold text-primary">
                  {{ report()!.totalAmount | number: '1.2-2' }} {{ report()!.currency }}
                </h5>
              </div>
            </div>
          </div>
          <div class="col-sm-4">
            <div class="card border-0 shadow-sm bg-info bg-opacity-10 py-2">
              <div class="card-body p-3">
                <div class="text-muted small mb-1">{{ '::ReceivedAmount' | abpLocalization }}</div>
                <h5 class="mb-0 fw-bold text-info">
                  {{ report()!.totalReceivedAmount | number: '1.2-2' }} {{ report()!.currency }}
                </h5>
              </div>
            </div>
          </div>
          <div class="col-sm-4">
            <div class="card border-0 shadow-sm bg-danger bg-opacity-10 py-2">
              <div class="card-body p-3">
                <div class="text-muted small mb-1">{{ '::PendingAmount' | abpLocalization }}</div>
                <h5 class="mb-0 fw-bold text-danger">
                  {{ report()!.totalPendingAmount | number: '1.2-2' }} {{ report()!.currency }}
                </h5>
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Data Table -->
      <div class="card shadow-sm border-0">
        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5">
              <div class="spinner-border text-primary" role="status"></div>
              <div class="text-muted small mt-2">{{ '::Loading' | abpLocalization }}...</div>
            </div>
          } @else if (!report() || filteredRows().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-folder-open fa-2x mb-2 text-secondary opacity-50"></i>
              <p class="mb-0">{{ '::NoBilledItemsToBeReceivedFound' | abpLocalization }}</p>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-sm table-hover align-middle mb-0">
                <thead class="table-dark">
                  <tr>
                    <th>{{ '::PurchaseInvoice' | abpLocalization }}</th>
                    <th>{{ '::PostingDate' | abpLocalization }}</th>
                    <th>{{ '::Supplier' | abpLocalization }}</th>
                    <th>{{ '::ItemCode' | abpLocalization }}</th>
                    <th>{{ '::UOM' | abpLocalization }}</th>
                    <th class="text-end">{{ '::InvoicedQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::ReceivedQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::PendingQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Rate' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Amount' | abpLocalization }}</th>
                    <th class="text-end">{{ '::ReceivedAmount' | abpLocalization }}</th>
                    <th class="text-end">{{ '::PendingAmount' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of filteredRows(); track row.purchaseInvoiceId + '-' + row.itemId) {
                    <tr>
                      <td class="fw-semibold text-primary">{{ row.invoiceNumber }}</td>
                      <td>{{ row.postingDate | date: 'mediumDate' }}</td>
                      <td>{{ row.supplierName }}</td>
                      <td>
                        <span class="fw-medium">{{ row.itemCode }}</span>
                        @if (row.itemName && row.itemName !== row.itemCode) {
                          <span class="text-muted small d-block">{{ row.itemName }}</span>
                        }
                      </td>
                      <td>{{ row.uom }}</td>
                      <td class="text-end">{{ row.invoicedQty | number: '1.2-2' }}</td>
                      <td class="text-end text-success">{{ row.receivedQty | number: '1.2-2' }}</td>
                      <td class="text-end fw-semibold text-warning">{{ row.pendingQty | number: '1.2-2' }}</td>
                      <td class="text-end">{{ row.rate | number: '1.2-2' }}</td>
                      <td class="text-end">{{ row.amount | number: '1.2-2' }}</td>
                      <td class="text-end text-info">{{ row.receivedAmount | number: '1.2-2' }}</td>
                      <td class="text-end fw-bold text-danger">{{ row.pendingAmount | number: '1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-secondary fw-bold">
                  <tr>
                    <td colspan="5">{{ '::Total' | abpLocalization }}</td>
                    <td class="text-end">{{ sumInvoicedQty() | number: '1.2-2' }}</td>
                    <td class="text-end text-success">{{ sumReceivedQty() | number: '1.2-2' }}</td>
                    <td class="text-end text-warning">{{ sumPendingQty() | number: '1.2-2' }}</td>
                    <td></td>
                    <td class="text-end">{{ report()!.totalAmount | number: '1.2-2' }}</td>
                    <td class="text-end text-info">{{ report()!.totalReceivedAmount | number: '1.2-2' }}</td>
                    <td class="text-end text-danger">{{ report()!.totalPendingAmount | number: '1.2-2' }}</td>
                  </tr>
                </tfoot>
              </table>
            </div>
          }
        </div>
      </div>
    </div>
  `,
})
export class BilledItemsToBeReceivedComponent implements OnInit {
  private service = inject(BilledItemsToBeReceivedService);
  private companyContext = inject(CompanyContextService);

  report = signal<BilledItemsToBeReceivedReportDto | null>(null);
  loading = signal(false);
  postingDate = '';
  searchTerm = '';

  filteredRows = computed(() => {
    const rep = this.report();
    if (!rep) return [];
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return rep.rows;
    return rep.rows.filter(
      (r) =>
        r.invoiceNumber.toLowerCase().includes(term) ||
        r.supplierName.toLowerCase().includes(term) ||
        r.itemCode.toLowerCase().includes(term) ||
        r.itemName.toLowerCase().includes(term)
    );
  });

  sumInvoicedQty = computed(() => {
    return this.filteredRows().reduce((acc, r) => acc + (r.invoicedQty || 0), 0);
  });

  sumReceivedQty = computed(() => {
    return this.filteredRows().reduce((acc, r) => acc + (r.receivedQty || 0), 0);
  });

  sumPendingQty = computed(() => {
    return this.filteredRows().reduce((acc, r) => acc + (r.pendingQty || 0), 0);
  });

  ngOnInit(): void {
    const today = new Date();
    this.postingDate = today.toISOString().split('T')[0];
    this.loadReport();
  }

  resetFilters(): void {
    const today = new Date();
    this.postingDate = today.toISOString().split('T')[0];
    this.searchTerm = '';
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.loading.set(true);

    this.service
      .getReport({
        companyId,
        postingDate: this.postingDate ? new Date(this.postingDate).toISOString() : undefined,
      })
      .subscribe({
        next: (data) => {
          this.report.set(data);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }

  exportCsv(): void {
    const rep = this.report();
    if (!rep || !rep.rows.length) return;

    const data = rep.rows.map((r) => ({
      PurchaseInvoice: r.invoiceNumber,
      PostingDate: r.postingDate ? r.postingDate.toString().split('T')[0] : '',
      Supplier: r.supplierName,
      ItemCode: r.itemCode,
      ItemName: r.itemName,
      UOM: r.uom,
      InvoicedQty: r.invoicedQty,
      ReceivedQty: r.receivedQty,
      PendingQty: r.pendingQty,
      Rate: r.rate,
      Amount: r.amount,
      ReceivedAmount: r.receivedAmount,
      PendingAmount: r.pendingAmount,
      Currency: r.currency,
    }));

    exportToCsv(
      `Billed_Items_To_Be_Received_${this.postingDate}.csv`,
      data,
      [
        'PurchaseInvoice',
        'PostingDate',
        'Supplier',
        'ItemCode',
        'ItemName',
        'UOM',
        'InvoicedQty',
        'ReceivedQty',
        'PendingQty',
        'Rate',
        'Amount',
        'ReceivedAmount',
        'PendingAmount',
        'Currency',
      ]
    );
  }
}
