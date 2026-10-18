import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ReservedStockReportService } from '../../../proxy/inventory/reserved-stock-report.service';
import type {
  ReservedStockReportDto,
  ReservedStockRowDto,
  WarehouseDto,
} from '../../../proxy/inventory/models';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-reserved-stock',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-lock me-2 text-warning"></i>{{ '::ReservedStock' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::ReservedStockDescription' | abpLocalization }}</small>
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
            <label class="form-label small fw-semibold">{{ '::Warehouse' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedWarehouseId" (change)="loadData()">
              <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
              @for (w of warehouses(); track w.id) {
                <option [value]="w.id">{{ w.name }}</option>
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
            <label class="form-label small fw-semibold">{{ '::VoucherType' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="voucherType" (change)="loadData()">
              <option value="">{{ '::All' | abpLocalization }}</option>
              <option value="Sales Order">Sales Order</option>
              <option value="Work Order">Work Order</option>
              <option value="Production Plan">Production Plan</option>
            </select>
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
            <div class="col-md-2 col-sm-4">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalEntries' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-primary mt-1">
                    {{ data()!.totalEntries }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-2 col-sm-4">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalReservedStock' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-warning mt-1">
                    {{ data()!.totalReservedQty | number: '1.2-4' }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-2 col-sm-4">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalDeliveredQty' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-success mt-1">
                    {{ data()!.totalDeliveredQty | number: '1.2-4' }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-2 col-sm-4">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalTransferredQty' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-info mt-1">
                    {{ data()!.totalTransferredQty | number: '1.2-4' }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-2 col-sm-4">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalConsumedQty' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-secondary mt-1">
                    {{ data()!.totalConsumedQty | number: '1.2-4' }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-2 col-sm-4">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::AvailableQtyToReserve' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-dark mt-1">
                    {{ data()!.totalAvailableQty | number: '1.2-4' }}
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
            {{ '::NoReservedStockFound' | abpLocalization }}
          </div>
        } @else {
          <div class="table-responsive">
            <table class="table table-hover align-middle table-sm border">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Date' | abpLocalization }}</th>
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th>{{ '::Warehouse' | abpLocalization }}</th>
                  <th>{{ '::BatchNo' | abpLocalization }}</th>
                  <th>{{ '::Voucher' | abpLocalization }}</th>
                  <th class="text-end">{{ '::VoucherQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ReservedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::DeliveredQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::TransferredQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ConsumedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::AvailableQtyToReserve' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track row.stockReservationEntryId) {
                  <tr>
                    <td>{{ row.date | date: 'short' }}</td>
                    <td>
                      <span class="fw-semibold text-primary">{{ row.itemCode }}</span>
                      @if (row.itemName && row.itemName !== row.itemCode) {
                        <br /><small class="text-muted">{{ row.itemName }}</small>
                      }
                    </td>
                    <td>{{ row.warehouseName || '—' }}</td>
                    <td>
                      @if (row.batchNo) {
                        <span class="badge bg-secondary font-monospace">{{ row.batchNo }}</span>
                      } @else {
                        <span class="text-muted">—</span>
                      }
                    </td>
                    <td>
                      <span class="badge bg-light text-dark border">{{ row.voucherType }}</span>
                      @if (row.fromVoucherType) {
                        <br /><small class="text-muted">From: {{ row.fromVoucherType }}</small>
                      }
                    </td>
                    <td class="text-end">
                      {{ row.voucherQty | number: '1.2-4' }}
                      @if (row.stockUom) {
                        <small class="text-muted ms-1">{{ row.stockUom }}</small>
                      }
                    </td>
                    <td class="text-end fw-bold text-warning">
                      {{ row.reservedQty | number: '1.2-4' }}
                    </td>
                    <td class="text-end text-success">
                      {{ row.deliveredQty | number: '1.2-4' }}
                    </td>
                    <td class="text-end text-info">
                      {{ row.transferredQty | number: '1.2-4' }}
                    </td>
                    <td class="text-end text-secondary">
                      {{ row.consumedQty | number: '1.2-4' }}
                    </td>
                    <td class="text-end fw-semibold">
                      {{ row.availableQtyToReserve | number: '1.2-4' }}
                    </td>
                    <td>
                      <span
                        class="badge"
                        [class.bg-success]="row.status === 'Submitted'"
                        [class.bg-secondary]="row.status === 'Draft'"
                        [class.bg-danger]="row.status === 'Cancelled'"
                      >
                        {{ row.status }}
                      </span>
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
export class ReservedStockComponent implements OnInit {
  private reservedStockService = inject(ReservedStockReportService);
  private warehouseService = inject(WarehouseService);
  private companyContextService = inject(CompanyContextService);

  data = signal<ReservedStockReportDto | null>(null);
  warehouses = signal<WarehouseDto[]>([]);
  isLoading = signal(false);

  selectedWarehouseId = '';
  voucherType = '';
  fromDate = new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString().split('T')[0];
  toDate = new Date().toISOString().split('T')[0];
  searchTerm = '';

  filteredRows = computed(() => {
    const report = this.data();
    if (!report || !report.rows) return [];
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return report.rows;
    return report.rows.filter(
      r =>
        r.itemCode.toLowerCase().includes(term) ||
        r.itemName.toLowerCase().includes(term) ||
        r.warehouseName.toLowerCase().includes(term) ||
        r.voucherType.toLowerCase().includes(term) ||
        (r.batchNo && r.batchNo.toLowerCase().includes(term))
    );
  });

  ngOnInit(): void {
    this.loadWarehouses();
    this.loadData();
  }

  loadWarehouses(): void {
    this.warehouseService.getList({ maxResultCount: 100 } as any).subscribe({
      next: res => {
        this.warehouses.set(res.items || []);
      },
    });
  }

  loadData(): void {
    const companyId = this.companyContextService.selectedCompanyId();

    this.isLoading.set(true);
    this.reservedStockService
      .getReport({
        companyId: companyId || null,
        warehouseId: this.selectedWarehouseId || null,
        voucherType: this.voucherType || null,
        fromDate: this.fromDate ? `${this.fromDate}T00:00:00Z` : null,
        toDate: this.toDate ? `${this.toDate}T23:59:59Z` : null,
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
    const rows = this.filteredRows();
    if (!rows || rows.length === 0) return;

    const exportData = rows.map(r => ({
      date: r.date,
      itemCode: r.itemCode,
      itemName: r.itemName,
      warehouseName: r.warehouseName,
      batchNo: r.batchNo || '',
      voucherType: r.voucherType,
      voucherQty: r.voucherQty,
      reservedQty: r.reservedQty,
      deliveredQty: r.deliveredQty,
      transferredQty: r.transferredQty,
      consumedQty: r.consumedQty,
      availableQtyToReserve: r.availableQtyToReserve,
      status: r.status,
    }));

    exportToCsv(
      `reserved_stock_${this.toDate || new Date().toISOString().split('T')[0]}.csv`,
      exportData,
      [
        'date',
        'itemCode',
        'itemName',
        'warehouseName',
        'batchNo',
        'voucherType',
        'voucherQty',
        'reservedQty',
        'deliveredQty',
        'transferredQty',
        'consumedQty',
        'availableQtyToReserve',
        'status',
      ]
    );
  }
}
