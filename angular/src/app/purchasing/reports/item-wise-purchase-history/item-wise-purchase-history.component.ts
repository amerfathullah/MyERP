import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ItemWisePurchaseHistoryService } from '../../../proxy/purchasing/item-wise-purchase-history.service';
import type {
  ItemWisePurchaseHistoryReportDto,
  ItemWisePurchaseHistoryRowDto,
} from '../../../proxy/purchasing/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-item-wise-purchase-history',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-boxes-stacked text-primary me-2"></i>
            {{ '::ItemWisePurchaseHistory' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::ItemWisePurchaseHistoryDescription' | abpLocalization }}
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
            <div class="col-md-4">
              <label class="form-label small mb-1">{{ '::Search' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                placeholder="Search by item code, name, supplier..."
                [(ngModel)]="searchTerm"
              />
            </div>
            <div class="col-md-2 d-flex align-items-end">
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
              <div class="text-muted small">{{ '::Quantity' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-primary">
                {{ totalQtyFiltered | number:'1.2-2' }}
              </div>
            </div>
          </div>
          <div class="col-6 col-md-3">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::TotalAmount' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-success">
                {{ totalAmountFiltered | number:'1.2-2' }}
              </div>
            </div>
          </div>
          <div class="col-6 col-md-3">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::ReceivedQuantity' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-info">
                {{ totalReceivedFiltered | number:'1.2-2' }}
              </div>
            </div>
          </div>
          <div class="col-6 col-md-3">
            <div class="card border-0 bg-light shadow-sm text-center p-2">
              <div class="text-muted small">{{ '::BilledAmount' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold text-secondary">
                {{ totalBilledFiltered | number:'1.2-2' }}
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
                <th>{{ '::Item' | abpLocalization }}</th>
                <th>{{ '::ItemGroup' | abpLocalization }}</th>
                <th class="text-end">{{ '::Quantity' | abpLocalization }}</th>
                <th class="text-end">{{ '::Rate' | abpLocalization }}</th>
                <th class="text-end">{{ '::Amount' | abpLocalization }}</th>
                <th>{{ '::PurchaseOrder' | abpLocalization }}</th>
                <th>{{ '::Date' | abpLocalization }}</th>
                <th>{{ '::Supplier' | abpLocalization }}</th>
                <th class="text-end">{{ '::ReceivedQuantity' | abpLocalization }}</th>
                <th class="text-end">{{ '::BilledAmount' | abpLocalization }}</th>
              </tr>
            </thead>
            <tbody>
              @if (isLoading()) {
                <tr>
                  <td colspan="10" class="text-center py-4 text-muted">
                    <span class="spinner-border spinner-border-sm me-2"></span>
                    {{ '::Loading' | abpLocalization }}...
                  </td>
                </tr>
              } @else if (filteredRows.length === 0) {
                <tr>
                  <td colspan="10" class="text-center py-4 text-muted">
                    {{ '::NoItemWisePurchasesFound' | abpLocalization }}
                  </td>
                </tr>
              } @else {
                @for (row of filteredRows; track row.purchaseOrderId + row.itemId) {
                  <tr>
                    <td>
                      <div class="fw-semibold">{{ row.itemCode }}</div>
                      <small class="text-muted">{{ row.itemName }}</small>
                    </td>
                    <td>{{ row.itemGroup || '-' }}</td>
                    <td class="text-end fw-semibold">
                      {{ row.quantity | number:'1.2-2' }} <small class="text-muted">{{ row.uom }}</small>
                    </td>
                    <td class="text-end">{{ row.rate | number:'1.2-2' }}</td>
                    <td class="text-end fw-bold">{{ row.amount | number:'1.2-2' }}</td>
                    <td>{{ row.purchaseOrderNumber }}</td>
                    <td>{{ row.transactionDate | date:'yyyy-MM-dd' }}</td>
                    <td>
                      <div>{{ row.supplierName }}</div>
                      @if (row.supplierGroup) {
                        <small class="text-muted">{{ row.supplierGroup }}</small>
                      }
                    </td>
                    <td class="text-end">
                      <span [ngClass]="row.receivedQuantity >= row.quantity ? 'text-success' : 'text-warning'">
                        {{ row.receivedQuantity | number:'1.2-2' }}
                      </span>
                    </td>
                    <td class="text-end">{{ row.billedAmount | number:'1.2-2' }}</td>
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
export class ItemWisePurchaseHistoryComponent implements OnInit {
  private service = inject(ItemWisePurchaseHistoryService);
  private companyContext = inject(CompanyContextService);

  fromDate = new Date(Date.now() - 30 * 86400000).toISOString().substring(0, 10);
  toDate = new Date().toISOString().substring(0, 10);
  searchTerm = '';

  isLoading = signal(false);
  report = signal<ItemWisePurchaseHistoryReportDto | null>(null);

  ngOnInit(): void {
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    this.isLoading.set(true);

    this.service
      .getHistory({
        companyId: companyId || undefined,
        fromDate: this.fromDate ? new Date(this.fromDate).toISOString() : undefined,
        toDate: this.toDate ? new Date(this.toDate).toISOString() : undefined,
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

  get filteredRows(): ItemWisePurchaseHistoryRowDto[] {
    const r = this.report();
    if (!r || !r.rows) return [];
    if (!this.searchTerm.trim()) return r.rows;

    const term = this.searchTerm.toLowerCase().trim();
    return r.rows.filter(
      (row) =>
        row.itemCode?.toLowerCase().includes(term) ||
        row.itemName?.toLowerCase().includes(term) ||
        row.supplierName?.toLowerCase().includes(term) ||
        row.purchaseOrderNumber?.toLowerCase().includes(term) ||
        row.itemGroup?.toLowerCase().includes(term)
    );
  }

  get totalQtyFiltered(): number {
    return this.filteredRows.reduce((acc, r) => acc + (r.quantity || 0), 0);
  }

  get totalAmountFiltered(): number {
    return this.filteredRows.reduce((acc, r) => acc + (r.amount || 0), 0);
  }

  get totalReceivedFiltered(): number {
    return this.filteredRows.reduce((acc, r) => acc + (r.receivedQuantity || 0), 0);
  }

  get totalBilledFiltered(): number {
    return this.filteredRows.reduce((acc, r) => acc + (r.billedAmount || 0), 0);
  }

  exportCsv(): void {
    const rows = this.filteredRows;
    if (rows.length === 0) return;

    const data = rows.map((r) => ({
      ItemCode: r.itemCode,
      ItemName: r.itemName,
      ItemGroup: r.itemGroup ?? '',
      Description: r.description ?? '',
      Quantity: r.quantity,
      UOM: r.uom,
      Rate: r.rate,
      Amount: r.amount,
      PurchaseOrder: r.purchaseOrderNumber,
      Date: r.transactionDate ? r.transactionDate.substring(0, 10) : '',
      Supplier: r.supplierName,
      SupplierGroup: r.supplierGroup ?? '',
      ReceivedQty: r.receivedQuantity,
      BilledAmount: r.billedAmount,
    }));

    exportToCsv(
      `Item-Wise-Purchase-History-${new Date().toISOString().substring(0, 10)}`,
      data,
      [
        'ItemCode',
        'ItemName',
        'ItemGroup',
        'Description',
        'Quantity',
        'UOM',
        'Rate',
        'Amount',
        'PurchaseOrder',
        'Date',
        'Supplier',
        'SupplierGroup',
        'ReceivedQty',
        'BilledAmount',
      ]
    );
  }
}
