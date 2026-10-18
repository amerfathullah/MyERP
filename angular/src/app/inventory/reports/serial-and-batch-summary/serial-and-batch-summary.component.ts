import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { SerialAndBatchSummaryService } from '../../../proxy/inventory/serial-and-batch-summary.service';
import type {
  SerialAndBatchSummaryReportDto,
  SerialAndBatchSummaryRowDto,
  WarehouseDto,
  ItemDto,
} from '../../../proxy/inventory/models';
import { ItemService } from '../../../proxy/inventory/item.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-serial-and-batch-summary',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-boxes-stacked me-2 text-primary"></i>
            {{ '::SerialAndBatchSummary' | abpLocalization }}
          </h5>
          <small class="text-muted">
            {{ '::SerialAndBatchSummaryDescription' | abpLocalization }}
          </small>
        </div>
        <div>
          @if (filteredRows().length > 0) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
      </div>

      <div class="card-body">
        <!-- Filters Row 1 -->
        <div class="row g-2 mb-3 align-items-end">
          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">Voucher Type</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedVoucherType" (change)="loadData()">
              <option value="All">All Voucher Types</option>
              <option value="StockEntry">Stock Entry</option>
              <option value="PurchaseReceipt">Purchase Receipt</option>
              <option value="DeliveryNote">Delivery Note</option>
              <option value="SalesInvoice">Sales Invoice</option>
              <option value="PurchaseInvoice">Purchase Invoice</option>
              <option value="StockReconciliation">Stock Reconciliation</option>
            </select>
          </div>
          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Warehouse' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedWarehouseId" (change)="loadData()">
              <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
              @for (w of warehouses(); track w.id) {
                <option [value]="w.id">{{ w.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Item' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedItemId" (change)="loadData()">
              <option value="">{{ '::AllItems' | abpLocalization }}</option>
              @for (item of items(); track item.id) {
                <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
              }
            </select>
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::FromDate' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" (change)="loadData()" />
          </div>
          <div class="col-md-2 col-sm-12">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Filters Row 2 -->
        <div class="row g-2 mb-4 align-items-center">
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::ToDate' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" (change)="loadData()" />
          </div>
          <div class="col-md-4 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Search' | abpLocalization }}</label>
            <input
              type="text"
              class="form-control form-control-sm"
              [placeholder]="'Search serial #, batch #, voucher #, item...'"
              [(ngModel)]="searchTerm"
            />
          </div>
          <div class="col-md-3 col-sm-6 pt-md-3">
            <div class="form-check">
              <input
                type="checkbox"
                class="form-check-input"
                id="includeCancelled"
                [(ngModel)]="includeCancelled"
                (change)="loadData()"
              />
              <label class="form-check-label small" for="includeCancelled">
                Include Cancelled Bundles
              </label>
            </div>
          </div>
        </div>

        <!-- Summary KPI Cards -->
        @if (report()) {
          <div class="row g-3 mb-4">
            <div class="col-md-2 col-sm-4 col-6">
              <div class="card bg-light border-0 shadow-sm p-3 text-center">
                <span class="text-muted small">Total Bundles</span>
                <span class="h5 mb-0 fw-bold">{{ report()!.totalBundles }}</span>
              </div>
            </div>
            <div class="col-md-2 col-sm-4 col-6">
              <div class="card bg-info-subtle border-0 shadow-sm p-3 text-center">
                <span class="text-info small fw-semibold">Total Entries</span>
                <span class="h5 mb-0 fw-bold text-info">{{ report()!.totalEntries }}</span>
              </div>
            </div>
            <div class="col-md-3 col-sm-4 col-6">
              <div class="card bg-success-subtle border-0 shadow-sm p-3 text-center">
                <span class="text-success small fw-semibold">Inward Quantity</span>
                <span class="h5 mb-0 fw-bold text-success">{{ report()!.totalInwardQty | number: '1.2-2' }}</span>
              </div>
            </div>
            <div class="col-md-2 col-sm-6 col-6">
              <div class="card bg-warning-subtle border-0 shadow-sm p-3 text-center">
                <span class="text-warning-emphasis small fw-semibold">Outward Quantity</span>
                <span class="h5 mb-0 fw-bold text-warning-emphasis">{{ report()!.totalOutwardQty | number: '1.2-2' }}</span>
              </div>
            </div>
            <div class="col-md-3 col-sm-6 col-12">
              <div class="card bg-primary-subtle border-0 shadow-sm p-3 text-center">
                <span class="text-primary small fw-semibold">Total Stock Value</span>
                <span class="h5 mb-0 fw-bold text-primary">{{ report()!.totalStockValue | currency: 'MYR' }}</span>
              </div>
            </div>
          </div>
        }

        <!-- Report Table -->
        @if (isLoading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status"></div>
            <div class="small text-muted mt-2">{{ '::Loading' | abpLocalization }}</div>
          </div>
        } @else if (filteredRows().length === 0) {
          <div class="text-center py-5 text-muted">
            <i class="fa fa-info-circle fa-2x mb-2 d-block"></i>
            {{ '::NoSerialAndBatchRecordsFound' | abpLocalization }}
          </div>
        } @else {
          <div class="table-responsive">
            <table class="table table-sm table-hover table-bordered align-middle">
              <thead class="table-light">
                <tr>
                  <th>#</th>
                  <th>Bundle</th>
                  <th>Voucher Type</th>
                  <th>Voucher No</th>
                  <th>Posting Date</th>
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th>{{ '::Warehouse' | abpLocalization }}</th>
                  <th>Type</th>
                  <th>Serial / Batch</th>
                  <th class="text-end">Qty</th>
                  <th class="text-end">Rate</th>
                  <th class="text-end">Value Diff</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track row.bundleId + '_' + $index; let idx = $index) {
                  <tr>
                    <td class="text-muted small">{{ idx + 1 }}</td>
                    <td>
                      <code class="fw-semibold text-primary">{{ row.bundleId.substring(0, 8) }}</code>
                    </td>
                    <td>
                      <span class="badge bg-secondary">{{ row.voucherType }}</span>
                    </td>
                    <td>
                      <code class="fw-semibold text-dark">{{ row.voucherNo }}</code>
                    </td>
                    <td class="small">{{ row.postingDate | date: 'mediumDate' }}</td>
                    <td>
                      <div class="fw-semibold small">{{ row.itemCode }}</div>
                      <div class="text-muted small">{{ row.itemName }}</div>
                    </td>
                    <td class="small">{{ row.warehouseName }}</td>
                    <td>
                      <span
                        class="badge"
                        [class.bg-success]="row.typeOfTransaction === 'Inward'"
                        [class.bg-warning]="row.typeOfTransaction === 'Outward'"
                        [class.text-dark]="row.typeOfTransaction === 'Outward'"
                      >
                        {{ row.typeOfTransaction }}
                      </span>
                    </td>
                    <td>
                      @if (row.serialNo) {
                        <div><code class="badge bg-primary-subtle text-primary border">SN: {{ row.serialNo }}</code></div>
                      }
                      @if (row.batchNo) {
                        <div><code class="badge bg-info-subtle text-info border">Batch: {{ row.batchNo }}</code></div>
                      }
                      @if (!row.serialNo && !row.batchNo) {
                        <span class="text-muted">-</span>
                      }
                    </td>
                    <td
                      class="text-end small fw-semibold"
                      [class.text-success]="row.quantity > 0"
                      [class.text-danger]="row.quantity < 0"
                    >
                      {{ row.quantity | number: '1.2-2' }}
                    </td>
                    <td class="text-end small">
                      {{ row.incomingRate | currency: 'MYR' }}
                    </td>
                    <td
                      class="text-end small fw-semibold"
                      [class.text-success]="row.stockValueDifference > 0"
                      [class.text-danger]="row.stockValueDifference < 0"
                    >
                      {{ row.stockValueDifference | currency: 'MYR' }}
                    </td>
                    <td>
                      @if (row.isCancelled) {
                        <span class="badge bg-danger">Cancelled</span>
                      } @else {
                        <span class="badge bg-success">Active</span>
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
export class SerialAndBatchSummaryComponent implements OnInit {
  private readonly reportService = inject(SerialAndBatchSummaryService);
  private readonly itemService = inject(ItemService);
  private readonly warehouseService = inject(WarehouseService);
  private readonly companyContext = inject(CompanyContextService);

  // Filters
  selectedVoucherType = 'All';
  selectedWarehouseId = '';
  selectedItemId = '';
  fromDate = '';
  toDate = '';
  searchTerm = '';
  includeCancelled = false;

  // State
  isLoading = signal<boolean>(false);
  report = signal<SerialAndBatchSummaryReportDto | null>(null);
  warehouses = signal<WarehouseDto[]>([]);
  items = signal<ItemDto[]>([]);

  filteredRows = computed<SerialAndBatchSummaryRowDto[]>(() => {
    const data = this.report();
    if (!data?.rows) return [];

    let rows = data.rows;
    if (this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      rows = rows.filter(
        r =>
          r.voucherNo.toLowerCase().includes(term) ||
          r.voucherType.toLowerCase().includes(term) ||
          r.itemCode.toLowerCase().includes(term) ||
          r.itemName.toLowerCase().includes(term) ||
          (r.serialNo && r.serialNo.toLowerCase().includes(term)) ||
          (r.batchNo && r.batchNo.toLowerCase().includes(term)) ||
          r.warehouseName.toLowerCase().includes(term)
      );
    }

    return rows;
  });

  ngOnInit(): void {
    this.loadLookups();
    this.loadData();
  }

  loadLookups(): void {
    this.warehouseService.getList({ maxResultCount: 200 } as any).subscribe(res => {
      this.warehouses.set(res.items || []);
    });

    this.itemService.getList({ maxResultCount: 500 } as any).subscribe(res => {
      const filtered = (res.items || []).filter(i => i.hasSerialNo || i.hasBatchNo);
      this.items.set(filtered);
    });
  }

  loadData(): void {
    this.isLoading.set(true);

    this.reportService
      .getReport({
        companyId: this.companyContext.selectedCompanyId() || undefined,
        voucherType: this.selectedVoucherType !== 'All' ? this.selectedVoucherType : undefined,
        warehouseId: this.selectedWarehouseId || undefined,
        itemId: this.selectedItemId || undefined,
        fromDate: this.fromDate || undefined,
        toDate: this.toDate || undefined,
        includeCancelled: this.includeCancelled,
        searchTerm: this.searchTerm || undefined,
      })
      .subscribe({
        next: data => {
          this.report.set(data);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
        },
      });
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (!rows.length) return;

    const exportRows = rows.map(r => ({
      'Bundle ID': r.bundleId,
      'Voucher Type': r.voucherType,
      'Voucher No': r.voucherNo,
      'Posting Date': r.postingDate.substring(0, 10),
      'Item Code': r.itemCode,
      'Item Name': r.itemName,
      'Warehouse': r.warehouseName,
      'Type': r.typeOfTransaction,
      'Serial No': r.serialNo || '',
      'Batch No': r.batchNo || '',
      'Quantity': r.quantity,
      'Rate': r.incomingRate,
      'Value Difference': r.stockValueDifference,
      'Status': r.isCancelled ? 'Cancelled' : 'Active',
    }));

    const columns = [
      'Bundle ID',
      'Voucher Type',
      'Voucher No',
      'Posting Date',
      'Item Code',
      'Item Name',
      'Warehouse',
      'Type',
      'Serial No',
      'Batch No',
      'Quantity',
      'Rate',
      'Value Difference',
      'Status',
    ];

    exportToCsv('serial_and_batch_summary.csv', exportRows, columns);
  }
}
