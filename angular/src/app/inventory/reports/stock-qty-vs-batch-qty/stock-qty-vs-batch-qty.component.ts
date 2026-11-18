import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { StockDiagnosticsService } from '../../../proxy/inventory/stock-diagnostics.service';
import { ItemService } from '../../../proxy/inventory/item.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import type {
  ItemDto,
  StockQtyVsBatchQtyReportDto,
  StockQtyVsBatchQtyRowDto,
  WarehouseDto,
} from '../../../proxy/inventory/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildStockQtyVsBatchCsvRows(
  rows: StockQtyVsBatchQtyRowDto[] | undefined,
): Record<string, string | number>[] {
  return (rows ?? []).map(r => ({
    'Item Code': r.itemCode ?? '',
    'Item Name': r.itemName ?? '',
    'Warehouse': r.warehouseName ?? '',
    'Batches': r.batchNumbers ?? '',
    'Batch Qty': (r.batchQty ?? 0).toFixed(4),
    'Stock Qty': (r.stockQty ?? 0).toFixed(4),
    'Difference': (r.difference ?? 0).toFixed(4),
  }));
}

@Component({
  selector: 'app-stock-qty-vs-batch-qty',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <abp-page [title]="'::StockQtyVsBatchQty' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <div>
            <h5 class="mb-0">
              <i class="fa fa-layer-group me-2 text-primary"></i>{{ '::StockQtyVsBatchQty' | abpLocalization }}
            </h5>
            <small class="text-muted">{{ '::StockQtyVsBatchQtyDescription' | abpLocalization }}</small>
          </div>
          <div class="d-flex gap-2">
            @if (selectedMismatchRows().length > 0) {
              <button
                type="button"
                class="btn btn-sm btn-outline-warning"
                (click)="reconcileSelected()"
                [disabled]="isReconciling() || isLoading()"
              >
                <i class="fa fa-wrench me-1" [class.fa-spin]="isReconciling()"></i>
                {{ '::UpdateBatchStockQty' | abpLocalization }} ({{ selectedMismatchRows().length }})
              </button>
            }
            @if ((report()?.rows?.length ?? 0) > 0) {
              <button type="button" class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
                <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
              </button>
            }
          </div>
        </div>

        <div class="card-body">
          <div class="row g-2 mb-3 align-items-end">
            <div class="col-md-3">
              <label class="form-label small fw-semibold">{{ '::Warehouse' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="selectedWarehouseId" (change)="loadData()">
                <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
                @for (w of warehouses(); track w.id) {
                  <option [value]="w.id">{{ w.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label small fw-semibold">{{ '::Item' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="selectedItemId" (change)="loadData()">
                <option value="">{{ '::AllItems' | abpLocalization }}</option>
                @for (it of batchItems(); track it.id) {
                  <option [value]="it.id">{{ it.itemCode }} - {{ it.itemName }}</option>
                }
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label small fw-semibold">{{ '::Search' | abpLocalization }}</label>
              <input type="text" class="form-control form-control-sm" [(ngModel)]="searchTerm" (keyup.enter)="loadData()" />
            </div>
            <div class="col-md-3">
              <button type="button" class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
                <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
              </button>
            </div>
          </div>

          <div class="row g-2 mb-3">
            <div class="col-auto">
              <div class="form-check form-check-inline">
                <input type="checkbox" class="form-check-input" id="onlyDiff" [(ngModel)]="showOnlyDifferences" (change)="loadData()" />
                <label class="form-check-label small" for="onlyDiff">{{ '::ShowOnlyDifferences' | abpLocalization }}</label>
              </div>
            </div>
          </div>

          @if (report(); as rep) {
            <div class="row g-2 mb-3">
              <div class="col-md-3">
                <div class="card border-start border-primary border-4">
                  <div class="card-body py-2">
                    <div class="small text-muted">{{ '::BatchQty' | abpLocalization }}</div>
                    <div class="h5 mb-0 font-monospace">{{ (rep.totalBatchQty ?? 0) | number:'1.2-4' }}</div>
                  </div>
                </div>
              </div>
              <div class="col-md-3">
                <div class="card border-start border-info border-4">
                  <div class="card-body py-2">
                    <div class="small text-muted">{{ '::StockQty' | abpLocalization }}</div>
                    <div class="h5 mb-0 font-monospace">{{ (rep.totalStockQty ?? 0) | number:'1.2-4' }}</div>
                  </div>
                </div>
              </div>
              <div class="col-md-3">
                <div class="card border-start border-danger border-4">
                  <div class="card-body py-2">
                    <div class="small text-muted">{{ '::Difference' | abpLocalization }}</div>
                    <div class="h5 mb-0 font-monospace text-danger">{{ (rep.totalDifference ?? 0) | number:'1.2-4' }}</div>
                  </div>
                </div>
              </div>
              <div class="col-md-3">
                <div class="card border-start border-warning border-4">
                  <div class="card-body py-2">
                    <div class="small text-muted">{{ '::Mismatch' | abpLocalization }}</div>
                    <div class="h5 mb-0 font-monospace">{{ rep.totalMismatches ?? 0 }}</div>
                  </div>
                </div>
              </div>
            </div>

            <div class="table-responsive">
              <table class="table table-hover table-sm mb-0">
                <thead class="table-light">
                  <tr>
                    <th style="width:36px"></th>
                    <th>{{ '::ItemCode' | abpLocalization }}</th>
                    <th>{{ '::ItemName' | abpLocalization }}</th>
                    <th>{{ '::Warehouse' | abpLocalization }}</th>
                    <th>{{ '::BatchCount' | abpLocalization }}</th>
                    <th class="text-end">{{ '::BatchQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::StockQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Difference' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of rep.rows; track (row.itemId ?? '') + '_' + (row.warehouseId ?? '')) {
                    <tr>
                      <td>
                        @if (row.isMismatch) {
                          <input type="checkbox" class="form-check-input" [checked]="isSelected(row)" (change)="toggleSelect(row)" />
                        }
                      </td>
                      <td class="font-monospace small">{{ row.itemCode }}</td>
                      <td>{{ row.itemName }}</td>
                      <td>{{ row.warehouseName }}</td>
                      <td class="small text-muted">{{ row.batchCount }} ({{ row.batchNumbers || '—' }})</td>
                      <td class="text-end font-monospace">{{ (row.batchQty ?? 0) | number:'1.2-4' }}</td>
                      <td class="text-end font-monospace">{{ (row.stockQty ?? 0) | number:'1.2-4' }}</td>
                      <td class="text-end font-monospace fw-bold" [class.text-danger]="row.isMismatch">
                        {{ (row.difference ?? 0) | number:'1.2-4' }}
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      </div>
    </abp-page>
  `,
})
export class StockQtyVsBatchQtyComponent implements OnInit {
  private diagnosticsService = inject(StockDiagnosticsService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  warehouses = signal<WarehouseDto[]>([]);
  batchItems = signal<ItemDto[]>([]);
  report = signal<StockQtyVsBatchQtyReportDto | null>(null);
  isLoading = signal(false);
  isReconciling = signal(false);
  selectedKeys = signal<Set<string>>(new Set());

  selectedWarehouseId = '';
  selectedItemId = '';
  searchTerm = '';
  showOnlyDifferences = true;

  selectedMismatchRows = computed(() => {
    const keys = this.selectedKeys();
    return (this.report()?.rows ?? []).filter(
      r => r.isMismatch && r.itemId && r.warehouseId && keys.has(`${r.itemId}:${r.warehouseId}`),
    );
  });

  ngOnInit(): void {
    const companyId = this.companyContext.currentCompanyId() ?? undefined;
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200 }).subscribe({
      next: res => this.warehouses.set((res.items ?? []).filter(w => !w.isGroup && (!companyId || w.companyId === companyId))),
      error: () => {},
    });
    this.itemService.getList({ skipCount: 0, maxResultCount: 500, companyId, hasBatchNo: true }).subscribe({
      next: res => this.batchItems.set((res.items ?? []).filter(i => i.hasBatchNo)),
      error: () => {},
    });
    this.loadData();
  }

  loadData(): void {
    this.isLoading.set(true);
    this.selectedKeys.set(new Set());
    this.diagnosticsService
      .getStockQtyVsBatchQty({
        companyId: this.companyContext.currentCompanyId() ?? null,
        warehouseId: this.selectedWarehouseId || null,
        itemId: this.selectedItemId || null,
        showOnlyDifferences: this.showOnlyDifferences,
        searchTerm: this.searchTerm.trim() || null,
      })
      .subscribe({
        next: res => {
          this.report.set(res);
          this.isLoading.set(false);
        },
        error: () => {
          this.toaster.error('Failed to load Stock Qty vs Batch Qty report.');
          this.isLoading.set(false);
        },
      });
  }

  isSelected(row: StockQtyVsBatchQtyRowDto): boolean {
    if (!row.itemId || !row.warehouseId) return false;
    return this.selectedKeys().has(`${row.itemId}:${row.warehouseId}`);
  }

  toggleSelect(row: StockQtyVsBatchQtyRowDto): void {
    if (!row.itemId || !row.warehouseId) return;
    const key = `${row.itemId}:${row.warehouseId}`;
    const next = new Set(this.selectedKeys());
    if (next.has(key)) next.delete(key);
    else next.add(key);
    this.selectedKeys.set(next);
  }

  reconcileSelected(): void {
    const items = this.selectedMismatchRows().map(r => ({
      itemId: r.itemId!,
      warehouseId: r.warehouseId!,
    }));
    if (items.length === 0) return;

    this.isReconciling.set(true);
    this.diagnosticsService.reconcileBatchStockQty({ items }).subscribe({
      next: res => {
        this.isReconciling.set(false);
        this.toaster.success(res.message || `Reconciled ${res.updatedCount ?? 0} bin(s).`);
        this.loadData();
      },
      error: () => {
        this.isReconciling.set(false);
        this.toaster.error('Failed to reconcile batch stock quantities.');
      },
    });
  }

  exportCsv(): void {
    const rows = buildStockQtyVsBatchCsvRows(this.report()?.rows);
    if (rows.length === 0) return;
    exportToCsv('stock-qty-vs-batch-qty.csv', rows, Object.keys(rows[0]));
  }
}
