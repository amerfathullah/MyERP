import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { StockQtyVsSerialNoCountService } from '../../../proxy/inventory/stock-qty-vs-serial-no-count.service';
import type {
  StockQtyVsSerialNoCountReportDto,
  StockQtyVsSerialNoCountRowDto,
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
  selector: 'app-stock-qty-vs-serial-no-count',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-balance-scale me-2 text-primary"></i>{{ '::StockQtyVsSerialNoCount' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::StockQtyVsSerialNoCountDescription' | abpLocalization }}</small>
        </div>
        <div class="d-flex gap-2">
          @if (selectedWarehouseId) {
            <button
              class="btn btn-sm btn-outline-warning"
              (click)="onSyncStatus()"
              [disabled]="isSyncing() || isLoading()"
            >
              <i class="fa fa-wrench me-1" [class.fa-spin]="isSyncing()"></i>{{ '::SyncSerialNoStatus' | abpLocalization }}
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
              @for (item of serialItems(); track item.id) {
                <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
              }
            </select>
          </div>
          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Search' | abpLocalization }}</label>
            <input
              type="text"
              class="form-control form-control-sm"
              [placeholder]="'::Search' | abpLocalization"
              [(ngModel)]="searchTerm"
            />
          </div>
          <div class="col-md-3 col-sm-6">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Filter Checkboxes -->
        <div class="row g-2 mb-4 align-items-center">
          <div class="col-auto">
            <div class="form-check form-check-inline">
              <input
                type="checkbox"
                class="form-check-input"
                id="showOnlyDiff"
                [(ngModel)]="showOnlyDifferences"
                (change)="loadData()"
              />
              <label class="form-check-label small" for="showOnlyDiff">
                {{ '::ShowOnlyDifferences' | abpLocalization }}
              </label>
            </div>
          </div>
          <div class="col-auto">
            <div class="form-check form-check-inline">
              <input
                type="checkbox"
                class="form-check-input"
                id="showDisabled"
                [(ngModel)]="showDisabledItems"
                (change)="loadData()"
              />
              <label class="form-check-label small" for="showDisabled">
                {{ '::ShowDisabledItems' | abpLocalization }}
              </label>
            </div>
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
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalMismatches' | abpLocalization }}</div>
                  <div class="h3 mb-0 mt-1" [ngClass]="data()!.totalMismatches > 0 ? 'text-danger' : 'text-success'">
                    {{ data()!.totalMismatches }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalSerialCount' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-primary mt-1">
                    {{ data()!.totalSerialCount | number: '1.2-2' }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalStockQty' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-info mt-1">
                    {{ data()!.totalStockQty | number: '1.2-2' }}
                  </div>
                </div>
              </div>
            </div>
          </div>
        }

        <!-- Status Alert Message -->
        @if (syncMessage()) {
          <div class="alert alert-info alert-dismissible fade show mb-4" role="alert">
            <i class="fa fa-info-circle me-2"></i>{{ syncMessage() }}
            <button type="button" class="btn-close" (click)="syncMessage.set('')"></button>
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
            <p class="mb-0">{{ '::NoStockQtyVsSerialNoCountFound' | abpLocalization }}</p>
          </div>
        } @else {
          <div class="table-responsive">
            <table class="table table-sm table-hover table-striped align-middle border">
              <thead class="table-light">
                <tr>
                  <th>#</th>
                  <th>{{ '::ItemCode' | abpLocalization }}</th>
                  <th>{{ '::ItemName' | abpLocalization }}</th>
                  <th>{{ '::Warehouse' | abpLocalization }}</th>
                  <th class="text-end">{{ '::SerialNoCount' | abpLocalization }}</th>
                  <th class="text-end">{{ '::StockQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Difference' | abpLocalization }}</th>
                  <th class="text-center">{{ '::Status' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track row.itemId + '-' + row.warehouseId; let idx = $index) {
                  <tr>
                    <td class="text-muted small">{{ idx + 1 }}</td>
                    <td class="fw-medium">{{ row.itemCode }}</td>
                    <td>{{ row.itemName }}</td>
                    <td>{{ row.warehouseName }}</td>
                    <td class="text-end fw-semibold">{{ row.serialNoCount | number: '1.2-2' }}</td>
                    <td class="text-end">{{ row.stockQty | number: '1.2-2' }}</td>
                    <td class="text-end">
                      <span [ngClass]="row.difference === 0 ? 'text-success' : 'text-danger fw-bold'">
                        {{ row.difference > 0 ? '+' : '' }}{{ row.difference | number: '1.2-2' }}
                      </span>
                    </td>
                    <td class="text-center">
                      @if (row.difference === 0) {
                        <span class="badge bg-success-subtle text-success border border-success-subtle">
                          <i class="fa fa-check me-1"></i>Matched
                        </span>
                      } @else {
                        <span class="badge bg-danger-subtle text-danger border border-danger-subtle">
                          <i class="fa fa-exclamation-triangle me-1"></i>Mismatch
                        </span>
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
export class StockQtyVsSerialNoCountComponent implements OnInit {
  private service = inject(StockQtyVsSerialNoCountService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);
  private companyContext = inject(CompanyContextService);

  serialItems = signal<ItemDto[]>([]);
  warehouses = signal<WarehouseDto[]>([]);
  data = signal<StockQtyVsSerialNoCountReportDto | null>(null);
  isLoading = signal(false);
  isSyncing = signal(false);
  syncMessage = signal('');

  selectedWarehouseId = '';
  selectedItemId = '';
  showDisabledItems = false;
  showOnlyDifferences = false;
  searchTerm = '';

  filteredRows = computed<StockQtyVsSerialNoCountRowDto[]>(() => {
    const report = this.data();
    if (!report || !report.rows) return [];
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return report.rows;

    return report.rows.filter(
      r =>
        r.itemCode.toLowerCase().includes(term) ||
        r.itemName.toLowerCase().includes(term) ||
        r.warehouseName.toLowerCase().includes(term)
    );
  });

  ngOnInit(): void {
    this.loadWarehouses();
    this.loadSerialItems();
    this.loadData();
  }

  private loadWarehouses(): void {
    const companyId = this.companyContext.selectedCompanyId();
    this.warehouseService.getList({ maxResultCount: 200 }).subscribe({
      next: res => {
        const items = res.items || [];
        this.warehouses.set(
          companyId ? items.filter(w => w.companyId === companyId) : items
        );
      },
      error: () => this.warehouses.set([]),
    });
  }

  private loadSerialItems(): void {
    const companyId = this.companyContext.selectedCompanyId();
    this.itemService.getList({ maxResultCount: 500 }).subscribe({
      next: res => {
        const items = res.items || [];
        this.serialItems.set(
          items.filter(i => (!companyId || i.companyId === companyId) && i.hasSerialNo)
        );
      },
      error: () => this.serialItems.set([]),
    });
  }

  loadData(): void {
    this.isLoading.set(true);
    const companyId = this.companyContext.selectedCompanyId();

    this.service
      .getReport({
        companyId: companyId || undefined,
        warehouseId: this.selectedWarehouseId || undefined,
        itemId: this.selectedItemId || undefined,
        showDisabledItems: this.showDisabledItems,
        showOnlyDifferences: this.showOnlyDifferences,
        searchTerm: this.searchTerm.trim() || undefined,
      })
      .subscribe({
        next: result => {
          this.data.set(result);
          this.isLoading.set(false);
        },
        error: () => {
          this.data.set(null);
          this.isLoading.set(false);
        },
      });
  }

  onSyncStatus(): void {
    if (!this.selectedWarehouseId) return;
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.isSyncing.set(true);
    this.service
      .syncSerialNoStatus({
        companyId,
        warehouseId: this.selectedWarehouseId,
        itemId: this.selectedItemId || undefined,
      })
      .subscribe({
        next: result => {
          this.isSyncing.set(false);
          this.syncMessage.set(result.message);
          this.loadData();
        },
        error: () => {
          this.isSyncing.set(false);
        },
      });
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (rows.length === 0) return;

    const exportRows = rows.map(r => ({
      ItemCode: r.itemCode,
      ItemName: r.itemName,
      Warehouse: r.warehouseName,
      SerialNoCount: r.serialNoCount,
      StockQty: r.stockQty,
      Difference: r.difference,
      Status: r.difference === 0 ? 'Matched' : 'Mismatch',
    }));

    const columns = [
      'ItemCode',
      'ItemName',
      'Warehouse',
      'SerialNoCount',
      'StockQty',
      'Difference',
      'Status',
    ];

    const filename = `Stock_Qty_Vs_Serial_No_Count_${new Date().toISOString().slice(0, 10)}.csv`;
    exportToCsv(filename, exportRows, columns);
  }
}
