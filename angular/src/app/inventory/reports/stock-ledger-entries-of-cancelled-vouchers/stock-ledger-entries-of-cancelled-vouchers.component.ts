import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { StockLedgerEntriesOfCancelledVouchersService } from '../../../proxy/inventory/stock-ledger-entries-of-cancelled-vouchers.service';
import type {
  StockLedgerEntriesOfCancelledVouchersReportDto,
  StockLedgerEntryOfCancelledVoucherRowDto,
  WarehouseDto,
  ItemDto,
  CancelledVoucherKeyDto,
} from '../../../proxy/inventory/models';
import { ItemService } from '../../../proxy/inventory/item.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-stock-ledger-entries-of-cancelled-vouchers',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-exclamation-triangle me-2 text-danger"></i>
            {{ '::StockLedgerEntriesOfCancelledVouchers' | abpLocalization }}
          </h5>
          <small class="text-muted">
            {{ '::StockLedgerEntriesOfCancelledVouchersDescription' | abpLocalization }}
          </small>
        </div>
        <div class="d-flex gap-2">
          @if (selectedVouchersCount() > 0) {
            <button
              class="btn btn-sm btn-danger"
              (click)="fixSelectedEntries()"
              [disabled]="isFixing()"
            >
              <i class="fa fa-wrench me-1" [class.fa-spin]="isFixing()"></i>
              {{ '::FixSelectedEntries' | abpLocalization }} ({{ selectedVouchersCount() }})
            </button>
          }
          @if (filteredRows().length > 0) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
      </div>

      <div class="card-body">
        <!-- Filters -->
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

        <div class="row g-2 mb-4">
          <div class="col-md-4 col-sm-6">
            <input
              type="text"
              class="form-control form-control-sm"
              [placeholder]="'Search voucher #, item, warehouse...'"
              [(ngModel)]="searchTerm"
            />
          </div>
          <div class="col-md-2 col-sm-6">
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" (change)="loadData()" />
          </div>
        </div>

        <!-- Alert Banner if Repairs Performed -->
        @if (repairMessages().length > 0) {
          <div class="alert alert-success alert-dismissible fade show" role="alert">
            <div class="fw-bold mb-1">
              <i class="fa fa-check-circle me-1"></i>
              {{ '::FixSelectedEntriesSuccess' | abpLocalization }}
            </div>
            <ul class="mb-0 small ps-3">
              @for (msg of repairMessages(); track msg) {
                <li>{{ msg }}</li>
              }
            </ul>
            <button type="button" class="btn-close" (click)="clearMessages()"></button>
          </div>
        }

        <!-- Summary KPI Cards -->
        @if (report()) {
          <div class="row g-3 mb-4">
            <div class="col-md-3 col-sm-6 col-12">
              <div class="card bg-danger-subtle border-0 shadow-sm p-3 text-center">
                <span class="text-danger small fw-semibold">Uncancelled SLEs</span>
                <span class="h4 mb-0 fw-bold text-danger">{{ report()!.totalUncancelledEntries }}</span>
              </div>
            </div>
            <div class="col-md-3 col-sm-6 col-12">
              <div class="card bg-warning-subtle border-0 shadow-sm p-3 text-center">
                <span class="text-warning-emphasis small fw-semibold">Cancelled Vouchers Affected</span>
                <span class="h4 mb-0 fw-bold text-warning-emphasis">{{ report()!.distinctCancelledVouchersCount }}</span>
              </div>
            </div>
            <div class="col-md-3 col-sm-6 col-12">
              <div class="card bg-info-subtle border-0 shadow-sm p-3 text-center">
                <span class="text-info small fw-semibold">Discrepancy Quantity</span>
                <span class="h4 mb-0 fw-bold text-info">{{ report()!.totalDiscrepancyQty | number: '1.2-2' }}</span>
              </div>
            </div>
            <div class="col-md-3 col-sm-6 col-12">
              <div class="card bg-primary-subtle border-0 shadow-sm p-3 text-center">
                <span class="text-primary small fw-semibold">Discrepancy Stock Value</span>
                <span class="h4 mb-0 fw-bold text-primary">{{ report()!.totalDiscrepancyValue | currency: 'MYR' }}</span>
              </div>
            </div>
          </div>
        }

        <!-- Content Area -->
        @if (isLoading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status"></div>
            <div class="small text-muted mt-2">{{ '::Loading' | abpLocalization }}</div>
          </div>
        } @else if (filteredRows().length === 0) {
          <div class="text-center py-5 text-success">
            <i class="fa fa-check-circle fa-3x mb-3 text-success"></i>
            <h6 class="fw-bold">{{ '::NoOrphanedStockLedgerEntriesFound' | abpLocalization }}</h6>
          </div>
        } @else {
          <div class="table-responsive">
            <table class="table table-sm table-hover table-bordered align-middle">
              <thead class="table-light">
                <tr>
                  <th style="width: 40px;" class="text-center">
                    <input
                      type="checkbox"
                      class="form-check-input"
                      [checked]="isAllSelected()"
                      (change)="toggleSelectAll($event)"
                    />
                  </th>
                  <th>#</th>
                  <th>Voucher Type</th>
                  <th>Voucher No</th>
                  <th>Posting Date</th>
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th>{{ '::Warehouse' | abpLocalization }}</th>
                  <th class="text-end">Quantity</th>
                  <th class="text-end">Value Difference</th>
                  <th>Bundle</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track row.sleId; let idx = $index) {
                  <tr>
                    <td class="text-center">
                      <input
                        type="checkbox"
                        class="form-check-input"
                        [checked]="isRowSelected(row)"
                        (change)="toggleSelectRow(row)"
                      />
                    </td>
                    <td class="text-muted small">{{ idx + 1 }}</td>
                    <td>
                      <span class="badge bg-secondary">{{ row.voucherType }}</span>
                    </td>
                    <td>
                      <code class="fw-semibold text-danger">{{ row.voucherNo }}</code>
                    </td>
                    <td class="small">
                      <div>{{ row.postingDate | date: 'mediumDate' }}</div>
                      <small class="text-muted">{{ row.postingTime }}</small>
                    </td>
                    <td>
                      <div class="fw-semibold small">{{ row.itemCode }}</div>
                      <div class="text-muted small">{{ row.itemName }}</div>
                    </td>
                    <td class="small">{{ row.warehouseName }}</td>
                    <td
                      class="text-end small fw-semibold"
                      [class.text-success]="row.quantityChange > 0"
                      [class.text-danger]="row.quantityChange < 0"
                    >
                      {{ row.quantityChange > 0 ? '+' : '' }}{{ row.quantityChange | number: '1.2-2' }}
                    </td>
                    <td
                      class="text-end small fw-semibold"
                      [class.text-danger]="row.stockValueDifference < 0"
                      [class.text-success]="row.stockValueDifference > 0"
                    >
                      {{ row.stockValueDifference | currency: 'MYR' }}
                    </td>
                    <td>
                      @if (row.serialAndBatchBundleId) {
                        @if (row.isBundleActive) {
                          <span class="badge bg-warning text-dark">
                            <i class="fa fa-exclamation-triangle me-1"></i>Active Bundle
                          </span>
                        } @else {
                          <span class="badge bg-light text-muted">Bundle Cancelled</span>
                        }
                      } @else {
                        <span class="text-muted">-</span>
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
export class StockLedgerEntriesOfCancelledVouchersComponent implements OnInit {
  private readonly reportService = inject(StockLedgerEntriesOfCancelledVouchersService);
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

  // State
  isLoading = signal<boolean>(false);
  isFixing = signal<boolean>(false);
  report = signal<StockLedgerEntriesOfCancelledVouchersReportDto | null>(null);
  warehouses = signal<WarehouseDto[]>([]);
  items = signal<ItemDto[]>([]);
  selectedVoucherKeys = signal<CancelledVoucherKeyDto[]>([]);
  repairMessages = signal<string[]>([]);

  filteredRows = computed<StockLedgerEntryOfCancelledVoucherRowDto[]>(() => {
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
          r.warehouseName.toLowerCase().includes(term)
      );
    }

    return rows;
  });

  selectedVouchersCount = computed<number>(() => {
    return this.selectedVoucherKeys().length;
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
      this.items.set(res.items || []);
    });
  }

  loadData(): void {
    this.isLoading.set(true);
    this.selectedVoucherKeys.set([]);

    this.reportService
      .getReport({
        companyId: this.companyContext.selectedCompanyId() || undefined,
        voucherType: this.selectedVoucherType !== 'All' ? this.selectedVoucherType : undefined,
        warehouseId: this.selectedWarehouseId || undefined,
        itemId: this.selectedItemId || undefined,
        fromDate: this.fromDate || undefined,
        toDate: this.toDate || undefined,
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

  isRowSelected(row: StockLedgerEntryOfCancelledVoucherRowDto): boolean {
    return this.selectedVoucherKeys().some(
      k => k.voucherType === row.voucherType && k.voucherId === row.voucherId
    );
  }

  toggleSelectRow(row: StockLedgerEntryOfCancelledVoucherRowDto): void {
    const keys = [...this.selectedVoucherKeys()];
    const idx = keys.findIndex(k => k.voucherType === row.voucherType && k.voucherId === row.voucherId);
    if (idx >= 0) {
      keys.splice(idx, 1);
    } else {
      keys.push({ voucherType: row.voucherType, voucherId: row.voucherId });
    }
    this.selectedVoucherKeys.set(keys);
  }

  isAllSelected(): boolean {
    const rows = this.filteredRows();
    if (!rows.length) return false;
    const distinctRows = this.getDistinctVoucherKeys(rows);
    return distinctRows.length > 0 && distinctRows.every(dr =>
      this.selectedVoucherKeys().some(k => k.voucherType === dr.voucherType && k.voucherId === dr.voucherId)
    );
  }

  toggleSelectAll(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    if (checked) {
      const distinct = this.getDistinctVoucherKeys(this.filteredRows());
      this.selectedVoucherKeys.set(distinct);
    } else {
      this.selectedVoucherKeys.set([]);
    }
  }

  private getDistinctVoucherKeys(rows: StockLedgerEntryOfCancelledVoucherRowDto[]): CancelledVoucherKeyDto[] {
    const map = new Map<string, CancelledVoucherKeyDto>();
    for (const r of rows) {
      const key = `${r.voucherType}_${r.voucherId}`;
      if (!map.has(key)) {
        map.set(key, { voucherType: r.voucherType, voucherId: r.voucherId });
      }
    }
    return Array.from(map.values());
  }

  fixSelectedEntries(): void {
    const keys = this.selectedVoucherKeys();
    if (!keys.length) return;

    if (!confirm('This will mark stock ledger entries for the selected cancelled vouchers as cancelled and queue valuation reposting. Proceed?')) {
      return;
    }

    this.isFixing.set(true);
    this.reportService
      .fixUncancelledEntries({ voucherKeys: keys })
      .subscribe({
        next: res => {
          this.isFixing.set(false);
          this.repairMessages.set(res.messages || []);
          this.loadData();
        },
        error: () => {
          this.isFixing.set(false);
        },
      });
  }

  clearMessages(): void {
    this.repairMessages.set([]);
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (!rows.length) return;

    const exportRows = rows.map(r => ({
      'Voucher Type': r.voucherType,
      'Voucher No': r.voucherNo,
      'Posting Date': r.postingDate.substring(0, 10),
      'Posting Time': r.postingTime,
      'Item Code': r.itemCode,
      'Item Name': r.itemName,
      'Warehouse': r.warehouseName,
      'Quantity Change': r.quantityChange,
      'Stock Value Difference': r.stockValueDifference,
      'Bundle ID': r.serialAndBatchBundleId || '',
      'Is Bundle Active': r.isBundleActive ? 'Yes' : 'No',
    }));

    const columns = [
      'Voucher Type',
      'Voucher No',
      'Posting Date',
      'Posting Time',
      'Item Code',
      'Item Name',
      'Warehouse',
      'Quantity Change',
      'Stock Value Difference',
      'Bundle ID',
      'Is Bundle Active',
    ];

    exportToCsv('stock_ledger_entries_of_cancelled_vouchers.csv', exportRows, columns);
  }
}
