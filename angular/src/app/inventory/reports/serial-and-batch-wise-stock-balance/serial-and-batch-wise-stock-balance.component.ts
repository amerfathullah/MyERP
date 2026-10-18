import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { SerialAndBatchWiseStockBalanceService } from '../../../proxy/inventory/serial-and-batch-wise-stock-balance.service';
import type {
  SerialAndBatchWiseStockBalanceReportDto,
  SerialAndBatchWiseStockBalanceRowDto,
  ItemDto,
  WarehouseDto,
} from '../../../proxy/inventory/models';
import { ItemService } from '../../../proxy/inventory/item.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-serial-and-batch-wise-stock-balance',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-boxes-stacked me-2 text-primary"></i>
            {{ '::SerialAndBatchWiseStockBalance' | abpLocalization }}
          </h5>
          <small class="text-muted">
            {{ '::SerialAndBatchWiseStockBalanceDescription' | abpLocalization }}
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
            <label class="form-label small fw-semibold">{{ '::Warehouse' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedWarehouseId">
              <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
              @for (wh of warehouses(); track wh.id) {
                <option [value]="wh.id">{{ wh.name }}</option>
              }
            </select>
          </div>

          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Item' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedItemId">
              <option value="">{{ '::AllItems' | abpLocalization }}</option>
              @for (item of items(); track item.id) {
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
            />
          </div>

          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::ToDate' | abpLocalization }}</label>
            <input
              type="date"
              class="form-control form-control-sm"
              [(ngModel)]="toDate"
            />
          </div>

          <div class="col-md-2 col-sm-12">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Apply' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Filters Row 2 -->
        <div class="row g-2 mb-4 align-items-center">
          <div class="col-md-6 col-sm-12">
            <label class="form-label small fw-semibold">{{ '::Search' | abpLocalization }}</label>
            <input
              type="text"
              class="form-control form-control-sm"
              [placeholder]="'Search item, batch, serial number, warehouse...'"
              [(ngModel)]="searchTerm"
            />
          </div>

          <div class="col-md-6 col-sm-12 pt-3">
            <div class="form-check form-switch">
              <input
                class="form-check-input"
                type="checkbox"
                id="includeZeroStockSwitch"
                [(ngModel)]="includeZeroStockItems"
                (change)="loadData()"
              />
              <label class="form-check-label small" for="includeZeroStockSwitch">
                {{ '::IncludeZeroStockItems' | abpLocalization }}
              </label>
            </div>
          </div>
        </div>

        <!-- Summary KPIs -->
        @if (report(); as rep) {
          <div class="row g-3 mb-4">
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-none">
                <div class="card-body p-3 text-center">
                  <div class="text-muted small mb-1">{{ '::Items' | abpLocalization }}</div>
                  <h5 class="mb-0 text-primary">{{ rep.totalItemsCount | number }}</h5>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-none">
                <div class="card-body p-3 text-center">
                  <div class="text-muted small mb-1">{{ '::Batches' | abpLocalization }}</div>
                  <h5 class="mb-0 text-info">{{ rep.totalBatchesCount | number }}</h5>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-none">
                <div class="card-body p-3 text-center">
                  <div class="text-muted small mb-1">{{ '::TotalBalanceQty' | abpLocalization }}</div>
                  <h5 class="mb-0 text-success">{{ rep.totalBalanceQty | number: '1.2-2' }}</h5>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-none">
                <div class="card-body p-3 text-center">
                  <div class="text-muted small mb-1">{{ '::TotalBalanceValue' | abpLocalization }}</div>
                  <h5 class="mb-0 text-dark">{{ rep.totalBalanceValue | number: '1.2-2' }}</h5>
                </div>
              </div>
            </div>
          </div>
        }

        <!-- Results Table -->
        <div class="table-responsive">
          <table class="table table-sm table-hover align-middle">
            <thead class="table-light text-nowrap">
              <tr>
                <th style="min-width: 260px;">{{ '::Item' | abpLocalization }} / {{ '::Batch' | abpLocalization }}</th>
                <th>{{ '::Warehouse' | abpLocalization }}</th>
                <th>{{ '::StockUom' | abpLocalization }}</th>
                <th class="text-end">{{ '::OpeningQty' | abpLocalization }}</th>
                <th class="text-end">{{ '::InQty' | abpLocalization }}</th>
                <th class="text-end">{{ '::OutQty' | abpLocalization }}</th>
                <th class="text-end">{{ '::BalanceQty' | abpLocalization }}</th>
                <th class="text-end">{{ '::ValuationRate' | abpLocalization }}</th>
                <th class="text-end">{{ '::BalanceValue' | abpLocalization }}</th>
                <th style="min-width: 200px;">{{ '::SerialNos' | abpLocalization }}</th>
              </tr>
            </thead>
            <tbody>
              @if (isLoading()) {
                <tr>
                  <td colspan="10" class="text-center py-4">
                    <i class="fa fa-spinner fa-spin fa-2x text-primary"></i>
                    <div class="mt-2 text-muted small">{{ '::Loading' | abpLocalization }}...</div>
                  </td>
                </tr>
              } @else if (filteredRows().length === 0) {
                <tr>
                  <td colspan="10" class="text-center py-4 text-muted">
                    <i class="fa fa-info-circle me-1"></i>
                    {{ '::NoSerialBatchStockBalanceRecordsFound' | abpLocalization }}
                  </td>
                </tr>
              } @else {
                @for (row of filteredRows(); track row.rowId) {
                  <tr [class.table-light]="row.indent === 0" [class.fw-semibold]="row.indent === 0">
                    <td [style.paddingLeft.px]="row.indent === 0 ? 8 : 32">
                      @if (row.indent === 0) {
                        <i class="fa fa-box me-1 text-primary"></i>
                        <span>{{ row.itemCode }} - {{ row.itemName }}</span>
                      } @else {
                        <i class="fa fa-layer-group me-1 text-secondary"></i>
                        <span class="badge bg-secondary-subtle text-secondary border">
                          {{ row.batchNo || 'Batch' }}
                        </span>
                      }
                    </td>
                    <td>
                      <small class="text-muted">{{ row.warehouseName }}</small>
                    </td>
                    <td>
                      <small class="text-muted">{{ row.stockUom }}</small>
                    </td>
                    <td class="text-end font-monospace small">
                      {{ row.openingQty | number: '1.2-2' }}
                    </td>
                    <td class="text-end font-monospace small text-success">
                      {{ row.inQty > 0 ? '+' : '' }}{{ row.inQty | number: '1.2-2' }}
                    </td>
                    <td class="text-end font-monospace small text-danger">
                      {{ row.outQty > 0 ? '-' : '' }}{{ row.outQty | number: '1.2-2' }}
                    </td>
                    <td class="text-end font-monospace small" [class.text-danger]="row.balanceQty < 0">
                      {{ row.balanceQty | number: '1.2-2' }}
                    </td>
                    <td class="text-end font-monospace small">
                      {{ row.valuationRate | number: '1.2-2' }}
                    </td>
                    <td class="text-end font-monospace small">
                      {{ row.balanceValue | number: '1.2-2' }}
                    </td>
                    <td>
                      @if (row.serialNos) {
                        <div class="d-flex flex-wrap gap-1">
                          @for (sn of getSerialList(row.serialNos); track sn) {
                            <span class="badge bg-light text-dark border font-monospace" style="font-size: 0.75rem;">
                              {{ sn }}
                            </span>
                          }
                        </div>
                      } @else {
                        <span class="text-muted small">-</span>
                      }
                    </td>
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
export class SerialAndBatchWiseStockBalanceComponent implements OnInit {
  private service = inject(SerialAndBatchWiseStockBalanceService);
  private companyContext = inject(CompanyContextService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);
  private toaster = inject(ToasterService);

  report = signal<SerialAndBatchWiseStockBalanceReportDto | null>(null);
  isLoading = signal<boolean>(false);
  warehouses = signal<WarehouseDto[]>([]);
  items = signal<ItemDto[]>([]);

  selectedWarehouseId = '';
  selectedItemId = '';
  fromDate = '';
  toDate = '';
  includeZeroStockItems = false;
  searchTerm = '';

  filteredRows = computed(() => {
    const rep = this.report();
    if (!rep) return [];
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return rep.rows;

    return rep.rows.filter(
      r =>
        r.itemCode.toLowerCase().includes(term) ||
        r.itemName.toLowerCase().includes(term) ||
        r.warehouseName.toLowerCase().includes(term) ||
        (r.batchNo && r.batchNo.toLowerCase().includes(term)) ||
        (r.serialNos && r.serialNos.toLowerCase().includes(term))
    );
  });

  ngOnInit(): void {
    this.loadMasters();
    this.loadData();
  }

  loadMasters(): void {
    const companyId = this.companyContext.selectedCompanyId();
    this.itemService.getList({ maxResultCount: 1000 }).subscribe({
      next: res => this.items.set(res.items || []),
    });

    this.warehouseService.getList({ maxResultCount: 500 }).subscribe({
      next: res => {
        let whs = res.items || [];
        if (companyId) {
          whs = whs.filter(w => w.companyId === companyId);
        }
        this.warehouses.set(whs);
      },
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
        fromDate: this.fromDate ? this.fromDate : undefined,
        toDate: this.toDate ? this.toDate : undefined,
        includeZeroStockItems: this.includeZeroStockItems,
        searchTerm: this.searchTerm ? this.searchTerm : undefined,
      })
      .subscribe({
        next: res => {
          this.report.set(res);
          this.isLoading.set(false);
        },
        error: err => {
          this.toaster.error(err.message || 'Failed to load stock balance report');
          this.isLoading.set(false);
        },
      });
  }

  getSerialList(serialNosString: string | null | undefined): string[] {
    if (!serialNosString) return [];
    return serialNosString
      .split('\n')
      .map(s => s.trim())
      .filter(s => s.length > 0);
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (!rows.length) return;

    const exportData = rows.map(r => ({
      Indent: r.indent,
      ItemCode: r.itemCode,
      ItemName: r.itemName,
      Warehouse: r.warehouseName,
      BatchNo: r.batchNo || '',
      StockUom: r.stockUom,
      OpeningQty: r.openingQty,
      OpeningValue: r.openingValue,
      InQty: r.inQty,
      InValue: r.inValue,
      OutQty: r.outQty,
      OutValue: r.outValue,
      BalanceQty: r.balanceQty,
      ValuationRate: r.valuationRate,
      BalanceValue: r.balanceValue,
      SerialNos: r.serialNos ? r.serialNos.replace(/\n/g, '; ') : '',
    }));

    exportToCsv(
      `serial-and-batch-wise-stock-balance-${new Date().toISOString().slice(0, 10)}.csv`,
      exportData,
      [
        'Indent',
        'ItemCode',
        'ItemName',
        'Warehouse',
        'BatchNo',
        'StockUom',
        'OpeningQty',
        'OpeningValue',
        'InQty',
        'InValue',
        'OutQty',
        'OutValue',
        'BalanceQty',
        'ValuationRate',
        'BalanceValue',
        'SerialNos',
      ]
    );
  }
}
