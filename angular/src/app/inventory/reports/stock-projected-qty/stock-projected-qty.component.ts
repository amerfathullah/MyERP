import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { StockProjectedQtyService } from '../../../proxy/inventory/stock-projected-qty.service';
import type {
  StockProjectedQtyReportDto,
  StockProjectedQtyRowDto,
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
  selector: 'app-stock-projected-qty',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-chart-line me-2 text-primary"></i>{{ '::ProjectedStockQty' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::StockProjectedQtyDescription' | abpLocalization }}</small>
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
              @for (item of items(); track item.id) {
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

        <!-- Filter Checkbox -->
        <div class="row g-2 mb-4 align-items-center">
          <div class="col-auto">
            <div class="form-check form-check-inline">
              <input
                type="checkbox"
                class="form-check-input"
                id="showOnlyShortage"
                [(ngModel)]="showOnlyShortage"
                (change)="loadData()"
              />
              <label class="form-check-label small" for="showOnlyShortage">
                {{ '::ShowOnlyShortage' | abpLocalization }}
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
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::ShortageQty' | abpLocalization }}</div>
                  <div class="h3 mb-0 mt-1" [ngClass]="data()!.shortageCount > 0 ? 'text-danger' : 'text-success'">
                    {{ data()!.shortageCount }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalActualQty' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-primary mt-1">
                    {{ data()!.totalActualQty | number: '1.2-2' }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalProjectedQty' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-info mt-1">
                    {{ data()!.totalProjectedQty | number: '1.2-2' }}
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
            <i class="fa fa-info-circle fa-3x mb-3 text-secondary opacity-50"></i>
            <p class="mb-0">{{ '::NoStockProjectedQtyFound' | abpLocalization }}</p>
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
                  <th>{{ '::Uom' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ActualQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::PlannedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::OrderedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::TotalReservedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ProjectedStockQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ReorderLevel' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ShortageQty' | abpLocalization }}</th>
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
                    <td class="small text-muted">{{ row.uom }}</td>
                    <td class="text-end">{{ row.actualQty | number: '1.2-2' }}</td>
                    <td class="text-end text-muted">{{ row.plannedQty | number: '1.2-2' }}</td>
                    <td class="text-end text-muted">{{ row.orderedQty | number: '1.2-2' }}</td>
                    <td class="text-end text-muted">{{ row.totalReservedQty | number: '1.2-2' }}</td>
                    <td class="text-end fw-bold">
                      <span [ngClass]="row.projectedQty < 0 ? 'text-danger' : 'text-dark'">
                        {{ row.projectedQty | number: '1.2-2' }}
                      </span>
                    </td>
                    <td class="text-end text-muted">{{ row.reorderLevel | number: '1.2-2' }}</td>
                    <td class="text-end">
                      @if (row.hasShortage) {
                        <span class="badge bg-danger text-white">
                          -{{ row.shortageQty | number: '1.2-2' }}
                        </span>
                      } @else {
                        <span class="text-muted">-</span>
                      }
                    </td>
                    <td class="text-center">
                      @if (row.hasShortage) {
                        <span class="badge bg-danger-subtle text-danger border border-danger-subtle">
                          <i class="fa fa-exclamation-circle me-1"></i>Shortage
                        </span>
                      } @else {
                        <span class="badge bg-success-subtle text-success border border-success-subtle">
                          <i class="fa fa-check me-1"></i>OK
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
export class StockProjectedQtyComponent implements OnInit {
  private service = inject(StockProjectedQtyService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);
  private companyContext = inject(CompanyContextService);

  items = signal<ItemDto[]>([]);
  warehouses = signal<WarehouseDto[]>([]);
  data = signal<StockProjectedQtyReportDto | null>(null);
  isLoading = signal(false);

  selectedWarehouseId = '';
  selectedItemId = '';
  showOnlyShortage = false;
  searchTerm = '';

  filteredRows = computed<StockProjectedQtyRowDto[]>(() => {
    const report = this.data();
    if (!report || !report.rows) return [];
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return report.rows;

    return report.rows.filter(
      r =>
        r.itemCode.toLowerCase().includes(term) ||
        r.itemName.toLowerCase().includes(term) ||
        r.warehouseName.toLowerCase().includes(term) ||
        (r.brand && r.brand.toLowerCase().includes(term)) ||
        (r.itemGroup && r.itemGroup.toLowerCase().includes(term))
    );
  });

  ngOnInit(): void {
    this.loadWarehouses();
    this.loadItems();
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

  private loadItems(): void {
    const companyId = this.companyContext.selectedCompanyId();
    this.itemService.getList({ maxResultCount: 500 }).subscribe({
      next: res => {
        const items = res.items || [];
        this.items.set(
          items.filter(i => (!companyId || i.companyId === companyId) && i.maintainStock)
        );
      },
      error: () => this.items.set([]),
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
        showOnlyShortage: this.showOnlyShortage,
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

  exportCsv(): void {
    const rows = this.filteredRows();
    if (rows.length === 0) return;

    const exportRows = rows.map(r => ({
      ItemCode: r.itemCode,
      ItemName: r.itemName,
      ItemGroup: r.itemGroup || '',
      Brand: r.brand || '',
      Warehouse: r.warehouseName,
      Uom: r.uom,
      ActualQty: r.actualQty,
      PlannedQty: r.plannedQty,
      IndentedQty: r.indentedQty,
      OrderedQty: r.orderedQty,
      TotalReservedQty: r.totalReservedQty,
      ProjectedQty: r.projectedQty,
      ReorderLevel: r.reorderLevel,
      ReorderQty: r.reorderQty,
      ShortageQty: r.shortageQty,
      Status: r.hasShortage ? 'Shortage' : 'OK',
    }));

    const columns = [
      'ItemCode',
      'ItemName',
      'ItemGroup',
      'Brand',
      'Warehouse',
      'Uom',
      'ActualQty',
      'PlannedQty',
      'IndentedQty',
      'OrderedQty',
      'TotalReservedQty',
      'ProjectedQty',
      'ReorderLevel',
      'ReorderQty',
      'ShortageQty',
      'Status',
    ];

    const filename = `Stock_Projected_Qty_${new Date().toISOString().slice(0, 10)}.csv`;
    exportToCsv(filename, exportRows, columns);
  }
}
