import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { BatchItemExpiryStatusService } from '../../../proxy/inventory/batch-item-expiry-status.service';
import type { BatchItemExpiryStatusReportDto, BatchItemExpiryStatusRowDto } from '../../../proxy/inventory/models';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { ItemService } from '../../../proxy/inventory/item.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-batch-item-expiry-status',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-calendar-xmark me-2 text-danger"></i>{{ '::BatchItemExpiryStatus' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::BatchItemExpiryStatusDescription' | abpLocalization }}</small>
        </div>
        @if (data() && data()!.rows && data()!.rows.length > 0) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::FromDate' | abpLocalization }} *</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" />
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::ToDate' | abpLocalization }} *</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" />
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
          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Item' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedItemId" (change)="loadData()">
              <option value="">{{ '::AllItems' | abpLocalization }}</option>
              @for (item of items(); track item.id) {
                <option [value]="item.id">{{ item.itemCode }} — {{ item.itemName }}</option>
              }
            </select>
          </div>
          <div class="col-md-2">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Filter Sub-bar: Status and Search -->
        <div class="row g-2 mb-4 align-items-center">
          <div class="col-md-6">
            <div class="btn-group btn-group-sm" role="group">
              <button
                type="button"
                class="btn"
                [class.btn-primary]="statusFilter === 'all'"
                [class.btn-outline-primary]="statusFilter !== 'all'"
                (click)="setStatusFilter('all')"
              >
                All
              </button>
              <button
                type="button"
                class="btn"
                [class.btn-danger]="statusFilter === 'expired'"
                [class.btn-outline-danger]="statusFilter !== 'expired'"
                (click)="setStatusFilter('expired')"
              >
                {{ '::ExpiredBatches' | abpLocalization }}
                @if (data()) {
                  ({{ data()!.expiredCount }})
                }
              </button>
              <button
                type="button"
                class="btn"
                [class.btn-warning]="statusFilter === 'soon'"
                [class.btn-outline-warning]="statusFilter !== 'soon'"
                (click)="setStatusFilter('soon')"
              >
                {{ '::ExpiringSoon' | abpLocalization }}
                @if (data()) {
                  ({{ data()!.expiringSoonCount }})
                }
              </button>
              <button
                type="button"
                class="btn"
                [class.btn-success]="statusFilter === 'valid'"
                [class.btn-outline-success]="statusFilter !== 'valid'"
                (click)="setStatusFilter('valid')"
              >
                Valid
              </button>
            </div>
          </div>
          <div class="col-md-6">
            <input
              type="text"
              class="form-control form-control-sm"
              [(ngModel)]="searchText"
              placeholder="Filter by batch number or item..."
              (keyup.enter)="loadData()"
            />
          </div>
        </div>

        @if (isLoading()) {
          <div class="text-center py-5">
            <i class="fa fa-spinner fa-spin fa-2x text-primary"></i>
          </div>
        } @else if (data(); as d) {
          <!-- KPI Summary Cards -->
          <div class="row g-3 mb-4">
            <div class="col-md-3 col-sm-6">
              <div class="card bg-body-tertiary border-0 shadow-none h-100">
                <div class="card-body py-2">
                  <div class="text-muted small">Total Active Batches</div>
                  <div class="h4 mb-0 fw-bold">{{ d.totalBatches }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-danger-subtle border-0 shadow-none h-100">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::ExpiredBatches' | abpLocalization }}</div>
                  <div class="h4 mb-0 fw-bold text-danger">{{ d.expiredCount }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-warning-subtle border-0 shadow-none h-100">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::ExpiringSoonBatches' | abpLocalization }}</div>
                  <div class="h4 mb-0 fw-bold text-warning">{{ d.expiringSoonCount }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-body-tertiary border-0 shadow-none h-100">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::TotalQuantity' | abpLocalization }}</div>
                  <div class="h4 mb-0 fw-bold font-monospace">{{ d.totalQuantity | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
          </div>

          <!-- Grid Table -->
          <div class="table-responsive">
            <table class="table table-hover align-middle table-sm border">
              <thead class="table-light">
                <tr>
                  <th style="width: 130px;">Status</th>
                  <th>Batch No</th>
                  <th>Item</th>
                  <th>Warehouse</th>
                  <th class="text-end">Quantity</th>
                  <th>Mfg Date</th>
                  <th>Expiry Date</th>
                  <th class="text-end">{{ '::ExpiryInDays' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track row.batchId + '-' + (row.warehouseId ?? '')) {
                  <tr [class.table-danger]="row.isExpired" [class.table-warning]="row.isExpiringSoon && !row.isExpired">
                    <td>
                      @if (row.isExpired) {
                        <span class="badge bg-danger">
                          <i class="fa fa-triangle-exclamation me-1"></i>Expired
                        </span>
                      } @else if (row.isExpiringSoon) {
                        <span class="badge bg-warning text-dark">
                          <i class="fa fa-clock me-1"></i>Expiring Soon
                        </span>
                      } @else {
                        <span class="badge bg-success-subtle text-success">Valid</span>
                      }
                    </td>
                    <td>
                      <span class="font-monospace fw-semibold small">{{ row.batchNo }}</span>
                    </td>
                    <td>
                      <div class="fw-medium font-monospace small">{{ row.itemCode }}</div>
                      <small class="text-muted">{{ row.itemName }}</small>
                    </td>
                    <td class="small">
                      {{ row.warehouseName ?? '—' }}
                    </td>
                    <td class="text-end font-monospace">
                      <span class="fw-semibold">{{ row.quantity | number:'1.2-2' }}</span>
                      <small class="text-muted ms-1">{{ row.stockUom }}</small>
                    </td>
                    <td class="small">
                      {{ row.manufacturingDate ? (row.manufacturingDate | date:'mediumDate') : '—' }}
                    </td>
                    <td class="small fw-medium">
                      {{ row.expiryDate ? (row.expiryDate | date:'mediumDate') : 'No Expiry' }}
                    </td>
                    <td class="text-end font-monospace small">
                      @if (row.expiryInDays !== null && row.expiryInDays !== undefined) {
                        @if (row.expiryInDays < 0) {
                          <span class="text-danger fw-bold">Expired ({{ -row.expiryInDays }}d ago)</span>
                        } @else if (row.expiryInDays === 0) {
                          <span class="text-danger fw-bold">Expires Today</span>
                        } @else if (row.expiryInDays <= 30) {
                          <span class="text-warning fw-bold">{{ row.expiryInDays }}d left</span>
                        } @else {
                          <span class="text-muted">{{ row.expiryInDays }}d</span>
                        }
                      } @else {
                        <span class="text-muted">—</span>
                      }
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="8" class="text-center text-muted py-5">
                      <i class="fa fa-circle-check fa-2x text-success mb-2 d-block"></i>
                      No batch expiry records found matching the filter criteria.
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
export class BatchItemExpiryStatusComponent implements OnInit {
  private service = inject(BatchItemExpiryStatusService);
  private warehouseService = inject(WarehouseService);
  private itemService = inject(ItemService);
  private companyContext = inject(CompanyContextService);

  fromDate = '';
  toDate = '';
  selectedWarehouseId = '';
  selectedItemId = '';
  searchText = '';
  statusFilter: 'all' | 'expired' | 'soon' | 'valid' = 'all';

  warehouses = signal<{ id: string; name: string }[]>([]);
  items = signal<{ id: string; itemCode: string; itemName: string }[]>([]);
  data = signal<BatchItemExpiryStatusReportDto | null>(null);
  isLoading = signal<boolean>(false);

  ngOnInit(): void {
    const now = new Date();
    const threeMonthsAgo = new Date();
    threeMonthsAgo.setMonth(now.getMonth() - 3);

    this.fromDate = threeMonthsAgo.toISOString().substring(0, 10);
    this.toDate = now.toISOString().substring(0, 10);

    this.loadWarehouses();
    this.loadItems();
    this.loadData();
  }

  loadWarehouses(): void {
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe({
      next: (res) => {
        const list = (res.items ?? [])
          .filter((w: any) => !w.isGroup)
          .map((w: any) => ({
            id: w.id,
            name: w.warehouseName ?? w.name ?? w.id,
          }));
        this.warehouses.set(list);
      },
    });
  }

  loadItems(): void {
    this.itemService.getList({ skipCount: 0, maxResultCount: 300, sorting: '' }).subscribe({
      next: (res) => {
        const list = (res.items ?? [])
          .filter((i: any) => i.hasBatchNo)
          .map((i: any) => ({
            id: i.id,
            itemCode: i.itemCode,
            itemName: i.itemName,
          }));
        this.items.set(list);
      },
    });
  }

  setStatusFilter(filter: 'all' | 'expired' | 'soon' | 'valid'): void {
    this.statusFilter = filter;
  }

  filteredRows(): BatchItemExpiryStatusRowDto[] {
    const rows = this.data()?.rows ?? [];
    if (this.statusFilter === 'all') return rows;
    if (this.statusFilter === 'expired') return rows.filter((r) => r.isExpired);
    if (this.statusFilter === 'soon') return rows.filter((r) => r.isExpiringSoon && !r.isExpired);
    if (this.statusFilter === 'valid') return rows.filter((r) => !r.isExpired && !r.isExpiringSoon);
    return rows;
  }

  loadData(): void {
    const companyId = this.companyContext.currentCompanyId();

    this.isLoading.set(true);
    this.service
      .getReport({
        fromDate: this.fromDate,
        toDate: this.toDate,
        companyId: companyId ? companyId : null,
        warehouseId: this.selectedWarehouseId ? this.selectedWarehouseId : null,
        itemId: this.selectedItemId ? this.selectedItemId : null,
        searchText: this.searchText ? this.searchText : null,
      })
      .subscribe({
        next: (res) => {
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
    if (rows.length === 0) return;

    exportToCsv(
      'batch-item-expiry-status',
      rows,
      [
        'batchNo',
        'itemCode',
        'itemName',
        'warehouseName',
        'quantity',
        'stockUom',
        'manufacturingDate',
        'expiryDate',
        'expiryInDays',
        'isExpired',
        'isExpiringSoon',
      ]
    );
  }
}
