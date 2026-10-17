import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { RequestedItemsToTransferService } from '../../../proxy/inventory/requested-items-to-transfer.service';
import type { RequestedItemsToTransferReportDto, RequestedItemsToTransferRowDto } from '../../../proxy/inventory/models';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { ItemService } from '../../../proxy/inventory/item.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-requested-items-to-transfer',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-truck-moving me-2 text-info"></i>{{ '::RequestedItemsToTransfer' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::RequestedItemsToTransferDescription' | abpLocalization }}</small>
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
            <label class="form-label small fw-semibold">{{ '::FromDate' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" />
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::ToDate' | abpLocalization }}</label>
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

        <!-- Search Bar -->
        <div class="row g-2 mb-4 align-items-center">
          <div class="col-md-6">
            <div class="input-group input-group-sm">
              <span class="input-group-text"><i class="fa fa-search"></i></span>
              <input
                type="text"
                class="form-control"
                [placeholder]="'::Search' | abpLocalization"
                [(ngModel)]="searchText"
                (ngModelChange)="onSearchChange()"
              />
            </div>
          </div>
          <div class="col-md-6 text-end text-muted small">
            @if (data()) {
              <span>{{ '::Showing' | abpLocalization }} {{ data()!.rows.length }} {{ '::Entries' | abpLocalization }}</span>
            }
          </div>
        </div>

        <!-- Summary KPI Cards -->
        @if (data() && data()!.rows && data()!.rows.length > 0) {
          <div class="row g-3 mb-4">
            <div class="col-md-4">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalRequests' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-primary mt-1">
                    {{ data()!.totalRequests }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-4">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalItems' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-info mt-1">
                    {{ data()!.totalItems }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-4">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalQtyToTransfer' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-danger mt-1">
                    {{ data()!.totalQtyToTransfer | number: '1.2-2' }}
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
        @if (!isLoading() && data() && data()!.rows && data()!.rows.length > 0) {
          <div class="table-responsive">
            <table class="table table-hover table-bordered table-sm align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th style="min-width: 130px;">{{ '::MaterialRequest' | abpLocalization }}</th>
                  <th style="min-width: 90px;">{{ '::Date' | abpLocalization }}</th>
                  <th style="min-width: 100px;">{{ '::Type' | abpLocalization }}</th>
                  <th style="min-width: 200px;">{{ '::Item' | abpLocalization }}</th>
                  <th class="text-end" style="width: 110px;">{{ '::RequestedQty' | abpLocalization }}</th>
                  <th class="text-end" style="width: 110px;">{{ '::TransferredQty' | abpLocalization }}</th>
                  <th class="text-end" style="width: 120px;">{{ '::QtyToTransfer' | abpLocalization }}</th>
                  <th style="width: 140px;">{{ '::CompletionPercent' | abpLocalization }}</th>
                  <th style="min-width: 180px;">{{ '::Warehouse' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of data()!.rows; track $index) {
                  <tr>
                    <td>
                      <span class="fw-semibold text-primary font-monospace">{{ row.requestNumber }}</span>
                    </td>
                    <td class="text-nowrap small">
                      {{ row.requestDate | date: 'mediumDate' }}
                      @if (row.requiredByDate) {
                        <div class="text-muted small">Req: {{ row.requiredByDate | date: 'shortDate' }}</div>
                      }
                    </td>
                    <td>
                      @if (row.requestType === 'MaterialTransfer') {
                        <span class="badge bg-info text-dark">Transfer</span>
                      } @else if (row.requestType === 'MaterialIssue') {
                        <span class="badge bg-warning text-dark">Issue</span>
                      } @else {
                        <span class="badge bg-secondary">{{ row.requestType }}</span>
                      }
                    </td>
                    <td>
                      <div class="fw-semibold">{{ row.itemCode }}</div>
                      <div class="text-muted small text-truncate" style="max-width: 240px;">{{ row.itemName }}</div>
                    </td>
                    <td class="text-end font-monospace">
                      {{ row.requestedQty | number: '1.2-2' }} <small class="text-muted">{{ row.uom }}</small>
                    </td>
                    <td class="text-end font-monospace">
                      {{ row.transferredQty | number: '1.2-2' }}
                    </td>
                    <td class="text-end font-monospace fw-bold text-danger">
                      {{ row.qtyToTransfer | number: '1.2-2' }}
                    </td>
                    <td>
                      <div class="d-flex align-items-center">
                        <div class="progress flex-grow-1 me-2" style="height: 6px;">
                          <div
                            class="progress-bar"
                            [class.bg-success]="row.completionPercent >= 100"
                            [class.bg-warning]="row.completionPercent > 0 && row.completionPercent < 100"
                            [class.bg-secondary]="row.completionPercent === 0"
                            role="progressbar"
                            [style.width.%]="row.completionPercent"
                          ></div>
                        </div>
                        <span class="small text-muted font-monospace" style="min-width: 40px;">
                          {{ row.completionPercent | number: '1.0-0' }}%
                        </span>
                      </div>
                    </td>
                    <td class="small">
                      @if (row.sourceWarehouseName && row.targetWarehouseName) {
                        <div><i class="fa fa-arrow-right text-muted me-1"></i>{{ row.sourceWarehouseName }} &rarr; {{ row.targetWarehouseName }}</div>
                      } @else if (row.sourceWarehouseName) {
                        <div><span class="text-muted">From:</span> {{ row.sourceWarehouseName }}</div>
                      } @else if (row.targetWarehouseName) {
                        <div><span class="text-muted">To:</span> {{ row.targetWarehouseName }}</div>
                      } @else {
                        <span class="text-muted">-</span>
                      }
                    </td>
                  </tr>
                }
              </tbody>
              <tfoot class="table-light fw-bold">
                <tr>
                  <td colspan="6">{{ '::Total' | abpLocalization }}</td>
                  <td class="text-end font-monospace text-danger">{{ data()!.totalQtyToTransfer | number: '1.2-2' }}</td>
                  <td colspan="2"></td>
                </tr>
              </tfoot>
            </table>
          </div>
        }

        <!-- Empty State -->
        @if (!isLoading() && (!data() || !data()!.rows || data()!.rows.length === 0)) {
          <div class="text-center py-5 text-muted">
            <i class="fa fa-clipboard-check fa-3x mb-3 text-success"></i>
            <p class="mb-0">{{ '::NoDataAvailable' | abpLocalization }}</p>
          </div>
        }
      </div>
    </div>
  `,
})
export class RequestedItemsToTransferComponent implements OnInit {
  private transferService = inject(RequestedItemsToTransferService);
  private warehouseService = inject(WarehouseService);
  private itemService = inject(ItemService);
  private companyContext = inject(CompanyContextService);

  fromDate = '';
  toDate = '';
  selectedWarehouseId = '';
  selectedItemId = '';
  searchText = '';

  warehouses = signal<{ id: string; name: string }[]>([]);
  items = signal<{ id: string; itemCode: string; itemName: string }[]>([]);
  data = signal<RequestedItemsToTransferReportDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    this.loadWarehouses();
    this.loadItems();
    this.loadData();
  }

  loadWarehouses(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.warehouseService.getList({ maxResultCount: 100 } as any).subscribe({
      next: (res) => {
        const list = res.items || [];
        this.warehouses.set(
          list
            .filter((w: any) => !w.companyId || w.companyId === companyId)
            .map((w: any) => ({ id: w.id, name: w.warehouseName || w.name }))
        );
      },
    });
  }

  loadItems(): void {
    this.itemService.getList({ maxResultCount: 200 } as any).subscribe({
      next: (res) => {
        const list = res.items || [];
        this.items.set(
          list.map((i: any) => ({ id: i.id, itemCode: i.itemCode, itemName: i.itemName }))
        );
      },
    });
  }

  loadData(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);

    this.transferService
      .getReport({
        companyId,
        fromDate: this.fromDate ? this.fromDate : null,
        toDate: this.toDate ? this.toDate : null,
        warehouseId: this.selectedWarehouseId || null,
        itemId: this.selectedItemId || null,
        searchText: this.searchText || null,
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

  onSearchChange(): void {
    this.loadData();
  }

  exportCsv(): void {
    const report = this.data();
    if (!report || !report.rows || report.rows.length === 0) return;

    const exportData = report.rows.map((r) => ({
      'Material Request': r.requestNumber,
      'Date': r.requestDate.split('T')[0],
      'Type': r.requestType,
      'Item Code': r.itemCode,
      'Item Name': r.itemName,
      'Requested Qty': r.requestedQty,
      'Transferred Qty': r.transferredQty,
      'Qty to Transfer': r.qtyToTransfer,
      'Completion %': r.completionPercent,
      'UOM': r.uom,
      'Source Warehouse': r.sourceWarehouseName || '',
      'Target Warehouse': r.targetWarehouseName || '',
    }));

    const columns = [
      'Material Request',
      'Date',
      'Type',
      'Item Code',
      'Item Name',
      'Requested Qty',
      'Transferred Qty',
      'Qty to Transfer',
      'Completion %',
      'UOM',
      'Source Warehouse',
      'Target Warehouse',
    ];

    exportToCsv(`requested_items_to_be_transferred_${new Date().toISOString().split('T')[0]}.csv`, exportData, columns);
  }
}
