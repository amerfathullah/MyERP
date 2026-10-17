import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { TotalStockSummaryService } from '../../../proxy/inventory/total-stock-summary.service';
import type { TotalStockSummaryReportDto, TotalStockSummaryRowDto, WarehouseDto } from '../../../proxy/inventory/models';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-total-stock-summary',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-boxes-stacked me-2 text-primary"></i>{{ '::TotalStockSummary' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::TotalStockSummaryDescription' | abpLocalization }}</small>
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
            <label class="form-label small fw-semibold">{{ '::GroupBy' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="groupBy" (change)="loadData()">
              <option value="Warehouse">{{ '::Warehouse' | abpLocalization }}</option>
              <option value="Company">{{ '::Company' | abpLocalization }}</option>
            </select>
          </div>
          @if (groupBy === 'Warehouse') {
            <div class="col-md-3 col-sm-6">
              <label class="form-label small fw-semibold">{{ '::Warehouse' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="selectedWarehouseId" (change)="loadData()">
                <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
                @for (w of warehouses(); track w.id) {
                  <option [value]="w.id">{{ w.name }}</option>
                }
              </select>
            </div>
          }
          <div class="col-md-4 col-sm-6">
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

        <!-- Summary KPI Cards -->
        @if (data()) {
          <div class="row g-3 mb-4">
            <div class="col-md-4 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalItems' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-primary mt-1">
                    {{ data()!.totalItems }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-4 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalQuantity' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-success mt-1">
                    {{ data()!.totalQuantity | number: '1.2-4' }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-4 col-sm-12">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalStockValue' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-dark mt-1">
                    {{ data()!.totalStockValue | number: '1.2-2' }}
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
            {{ '::NoStockSummaryRecordsFound' | abpLocalization }}
          </div>
        } @else {
          <div class="table-responsive">
            <table class="table table-hover align-middle table-sm border">
              <thead class="table-light">
                <tr>
                  <th>
                    @if (groupBy === 'Warehouse') {
                      {{ '::Warehouse' | abpLocalization }}
                    } @else {
                      {{ '::Company' | abpLocalization }}
                    }
                  </th>
                  <th>{{ '::ItemCode' | abpLocalization }}</th>
                  <th>{{ '::ItemName' | abpLocalization }}</th>
                  <th>{{ '::Description' | abpLocalization }}</th>
                  <th class="text-end">{{ '::CurrentQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ValuationRate' | abpLocalization }}</th>
                  <th class="text-end">{{ '::StockValue' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track $index) {
                  <tr>
                    <td class="fw-semibold text-secondary">{{ row.groupName }}</td>
                    <td class="fw-semibold text-primary">{{ row.itemCode }}</td>
                    <td>{{ row.itemName }}</td>
                    <td class="text-muted small">{{ row.description || '-' }}</td>
                    <td class="text-end fw-semibold">
                      {{ row.currentQty | number: '1.2-4' }}
                      @if (row.uom) {
                        <small class="text-muted ms-1">{{ row.uom }}</small>
                      }
                    </td>
                    <td class="text-end">{{ row.valuationRate | number: '1.2-4' }}</td>
                    <td class="text-end fw-semibold">{{ row.stockValue | number: '1.2-2' }}</td>
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
export class TotalStockSummaryComponent implements OnInit {
  private totalStockSummaryService = inject(TotalStockSummaryService);
  private warehouseService = inject(WarehouseService);
  private companyContextService = inject(CompanyContextService);

  data = signal<TotalStockSummaryReportDto | null>(null);
  warehouses = signal<WarehouseDto[]>([]);
  isLoading = signal(false);

  groupBy = 'Warehouse';
  selectedWarehouseId = '';
  searchTerm = '';

  filteredRows = computed(() => {
    const report = this.data();
    if (!report || !report.rows) return [];
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return report.rows;
    return report.rows.filter(
      r =>
        r.groupName.toLowerCase().includes(term) ||
        r.itemCode.toLowerCase().includes(term) ||
        r.itemName.toLowerCase().includes(term) ||
        (r.description && r.description.toLowerCase().includes(term))
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
    this.totalStockSummaryService
      .getReport({
        companyId: companyId || null,
        groupBy: this.groupBy,
        warehouseId: this.selectedWarehouseId || null,
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
      groupName: r.groupName,
      itemCode: r.itemCode,
      itemName: r.itemName,
      description: r.description || '',
      currentQty: r.currentQty,
      uom: r.uom || '',
      valuationRate: r.valuationRate,
      stockValue: r.stockValue,
    }));

    exportToCsv(
      `total_stock_summary_${new Date().toISOString().split('T')[0]}.csv`,
      exportData,
      [
        'groupName',
        'itemCode',
        'itemName',
        'description',
        'currentQty',
        'uom',
        'valuationRate',
        'stockValue',
      ]
    );
  }
}
