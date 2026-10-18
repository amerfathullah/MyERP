import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { NegativeBatchReportService } from '../../../proxy/inventory/negative-batch-report.service';
import type {
  NegativeBatchReportDto,
  NegativeBatchRowDto,
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
  selector: 'app-negative-batch',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-exclamation-triangle me-2 text-danger"></i>{{ '::NegativeBatchReport' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::NegativeBatchReportDescription' | abpLocalization }}</small>
        </div>
        <div class="d-flex gap-2">
          @if (filteredRows().length > 0) {
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
              @for (item of batchItems(); track item.id) {
                <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
              }
            </select>
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::FromDate' | abpLocalization }}</label>
            <input
              type="date"
              class="form-control form-control-sm"
              [(ngModel)]="fromDate"
              (change)="loadData()"
            />
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::ToDate' | abpLocalization }}</label>
            <input
              type="date"
              class="form-control form-control-sm"
              [(ngModel)]="toDate"
              (change)="loadData()"
            />
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
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Summary KPI Cards -->
        @if (data()) {
          <div class="row g-3 mb-4">
            <div class="col-md-6 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalIncidents' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-danger mt-1">
                    {{ data()!.totalIncidents }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-6 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::DistinctNegativeBatches' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-warning mt-1">
                    {{ data()!.distinctNegativeBatches }}
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
              <span class="visually-hidden">{{ '::Loading' | abpLocalization }}</span>
            </div>
          </div>
        } @else if (filteredRows().length === 0) {
          <div class="text-center py-5 text-muted">
            <i class="fa fa-check-circle fa-3x mb-3 text-success opacity-50"></i>
            <p class="mb-0">{{ '::NoNegativeBatchesFound' | abpLocalization }}</p>
          </div>
        } @else {
          <div class="table-responsive">
            <table class="table table-sm table-hover table-striped align-middle border">
              <thead class="table-light">
                <tr>
                  <th>#</th>
                  <th>{{ '::Date' | abpLocalization }}</th>
                  <th>{{ '::BatchNo' | abpLocalization }}</th>
                  <th>{{ '::ItemCode' | abpLocalization }}</th>
                  <th>{{ '::ItemName' | abpLocalization }}</th>
                  <th>{{ '::Warehouse' | abpLocalization }}</th>
                  <th class="text-end">{{ '::PreviousQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::TransactionQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::QtyAfterTransaction' | abpLocalization }}</th>
                  <th>{{ '::VoucherType' | abpLocalization }}</th>
                  <th>{{ '::VoucherNo' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track row.batchId + '-' + row.postingDateTime; let idx = $index) {
                  <tr>
                    <td class="text-muted small">{{ idx + 1 }}</td>
                    <td class="small">{{ row.postingDateTime | date: 'yyyy-MM-dd HH:mm' }}</td>
                    <td>
                      <span class="badge bg-secondary-subtle text-secondary fw-semibold">
                        {{ row.batchNo }}
                      </span>
                    </td>
                    <td class="fw-medium">{{ row.itemCode }}</td>
                    <td>{{ row.itemName }}</td>
                    <td>{{ row.warehouseName }}</td>
                    <td class="text-end">{{ row.previousQty | number: '1.2-2' }}</td>
                    <td class="text-end text-danger fw-semibold">{{ row.transactionQty | number: '1.2-2' }}</td>
                    <td class="text-end">
                      <span class="badge bg-danger text-white">
                        {{ row.qtyAfterTransaction | number: '1.2-2' }}
                      </span>
                    </td>
                    <td>
                      <span class="badge bg-light text-dark border">{{ row.voucherType }}</span>
                    </td>
                    <td class="small text-muted font-monospace">{{ row.voucherNo || '-' }}</td>
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
export class NegativeBatchComponent implements OnInit {
  private negativeBatchReportService = inject(NegativeBatchReportService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);
  private companyContext = inject(CompanyContextService);

  batchItems = signal<ItemDto[]>([]);
  warehouses = signal<WarehouseDto[]>([]);
  data = signal<NegativeBatchReportDto | null>(null);
  isLoading = signal(false);

  selectedWarehouseId = '';
  selectedItemId = '';
  fromDate = '';
  toDate = new Date().toISOString().slice(0, 10);
  searchTerm = '';

  filteredRows = computed(() => {
    const report = this.data();
    if (!report || !report.rows) return [];
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return report.rows;

    return report.rows.filter(
      r =>
        r.batchNo.toLowerCase().includes(term) ||
        r.itemCode.toLowerCase().includes(term) ||
        r.itemName.toLowerCase().includes(term) ||
        r.warehouseName.toLowerCase().includes(term) ||
        r.voucherType.toLowerCase().includes(term)
    );
  });

  ngOnInit(): void {
    this.loadDropdowns();
    this.loadData();
  }

  loadDropdowns(): void {
    // Fetch batch-tracked items
    this.itemService.getList({ hasBatchNo: true, maxResultCount: 500 } as any).subscribe({
      next: res => {
        this.batchItems.set(res.items ?? []);
      },
      error: () => {},
    });

    // Fetch active warehouses
    this.warehouseService.getList({ maxResultCount: 500 } as any).subscribe({
      next: res => {
        this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup && w.isActive));
      },
      error: () => {},
    });
  }

  loadData(): void {
    this.isLoading.set(true);
    const companyId = this.companyContext.selectedCompanyId() || null;

    this.negativeBatchReportService
      .getReport({
        companyId,
        warehouseId: this.selectedWarehouseId || null,
        itemId: this.selectedItemId || null,
        fromDate: this.fromDate ? `${this.fromDate}T00:00:00` : null,
        toDate: this.toDate ? `${this.toDate}T23:59:59` : null,
        searchTerm: this.searchTerm || null,
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
    const report = this.data();
    if (!report || !report.rows.length) return;

    const exportRows = this.filteredRows().map(r => ({
      PostingDateTime: r.postingDateTime,
      BatchNo: r.batchNo,
      ItemCode: r.itemCode,
      ItemName: r.itemName,
      Warehouse: r.warehouseName,
      PreviousQty: r.previousQty,
      TransactionQty: r.transactionQty,
      QtyAfterTransaction: r.qtyAfterTransaction,
      VoucherType: r.voucherType,
      VoucherNo: r.voucherNo,
    }));

    const columns = [
      'PostingDateTime',
      'BatchNo',
      'ItemCode',
      'ItemName',
      'Warehouse',
      'PreviousQty',
      'TransactionQty',
      'QtyAfterTransaction',
      'VoucherType',
      'VoucherNo',
    ];

    const filename = `Negative_Batch_Report_${new Date().toISOString().slice(0, 10)}.csv`;
    exportToCsv(filename, exportRows, columns);
  }
}
