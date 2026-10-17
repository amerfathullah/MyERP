import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ItemWiseConsumptionService } from '../../../proxy/inventory/item-wise-consumption.service';
import type { ItemWiseConsumptionReportDto, ItemWiseConsumptionRowDto } from '../../../proxy/inventory/models';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { ItemService } from '../../../proxy/inventory/item.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-item-wise-consumption',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-chart-pie me-2 text-primary"></i>{{ '::ItemWiseConsumption' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::ItemWiseConsumptionDescription' | abpLocalization }}</small>
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

        <div class="row g-2 mb-4">
          <div class="col-md-6">
            <input
              type="text"
              class="form-control form-control-sm"
              [(ngModel)]="searchText"
              placeholder="Search by item code, name, description, supplier..."
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
                  <div class="text-muted small">Total Active Items</div>
                  <div class="h4 mb-0 fw-bold">{{ d.totalItems }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-info-subtle border-0 shadow-none h-100">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::TotalConsumedQty' | abpLocalization }}</div>
                  <div class="h4 mb-0 fw-bold text-info font-monospace">
                    {{ d.grandTotalConsumedQty | number:'1.2-2' }}
                  </div>
                  <div class="small text-muted">{{ d.grandTotalConsumedAmount | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-success-subtle border-0 shadow-none h-100">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::DeliveredQty' | abpLocalization }}</div>
                  <div class="h4 mb-0 fw-bold text-success font-monospace">
                    {{ d.grandTotalDeliveredQty | number:'1.2-2' }}
                  </div>
                  <div class="small text-muted">{{ d.grandTotalDeliveredAmount | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-primary-subtle border-0 shadow-none h-100">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::TotalAmount' | abpLocalization }}</div>
                  <div class="h4 mb-0 fw-bold text-primary font-monospace">
                    {{ d.grandTotalAmount | number:'1.2-2' }}
                  </div>
                  <div class="small text-muted">Total Qty: {{ d.grandTotalQty | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
          </div>

          <!-- Grid Table -->
          <div class="table-responsive">
            <table class="table table-hover align-middle table-sm border">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th>{{ '::Description' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ConsumedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ConsumedAmount' | abpLocalization }}</th>
                  <th class="text-end">{{ '::DeliveredQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::DeliveredAmount' | abpLocalization }}</th>
                  <th class="text-end">{{ '::TotalQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::TotalAmount' | abpLocalization }}</th>
                  <th>{{ '::Suppliers' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of d.rows; track row.itemId) {
                  <tr>
                    <td>
                      <div class="fw-semibold font-monospace small">{{ row.itemCode }}</div>
                      <div class="text-muted small">{{ row.itemName }}</div>
                      <span class="badge bg-light text-secondary border mt-1">{{ row.stockUom }}</span>
                    </td>
                    <td class="small text-muted text-truncate" style="max-width: 180px;">
                      {{ row.description ?? '—' }}
                    </td>
                    <td class="text-end font-monospace text-info">
                      {{ row.consumedQty | number:'1.2-2' }}
                    </td>
                    <td class="text-end font-monospace text-info">
                      {{ row.consumedAmount | number:'1.2-2' }}
                    </td>
                    <td class="text-end font-monospace text-success">
                      {{ row.deliveredQty | number:'1.2-2' }}
                    </td>
                    <td class="text-end font-monospace text-success">
                      {{ row.deliveredAmount | number:'1.2-2' }}
                    </td>
                    <td class="text-end font-monospace fw-bold">
                      {{ row.totalQty | number:'1.2-2' }}
                    </td>
                    <td class="text-end font-monospace fw-bold text-primary">
                      {{ row.totalAmount | number:'1.2-2' }}
                    </td>
                    <td class="small">
                      @if (row.suppliers) {
                        <span class="badge bg-light text-dark border">{{ row.suppliers }}</span>
                      } @else {
                        <span class="text-muted">—</span>
                      }
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="9" class="text-center text-muted py-5">
                      <i class="fa fa-box-open fa-2x text-muted mb-2 d-block"></i>
                      No consumption or delivery records found for the specified period.
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
export class ItemWiseConsumptionComponent implements OnInit {
  private service = inject(ItemWiseConsumptionService);
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
  data = signal<ItemWiseConsumptionReportDto | null>(null);
  isLoading = signal<boolean>(false);

  ngOnInit(): void {
    const now = new Date();
    const firstDay = new Date(now.getFullYear(), now.getMonth(), 1);

    this.fromDate = firstDay.toISOString().substring(0, 10);
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
        const list = (res.items ?? []).map((i: any) => ({
          id: i.id,
          itemCode: i.itemCode,
          itemName: i.itemName,
        }));
        this.items.set(list);
      },
    });
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
    const currentData = this.data();
    if (!currentData || !currentData.rows || currentData.rows.length === 0) return;

    exportToCsv(
      'item-wise-consumption',
      currentData.rows,
      [
        'itemCode',
        'itemName',
        'description',
        'stockUom',
        'consumedQty',
        'consumedAmount',
        'deliveredQty',
        'deliveredAmount',
        'totalQty',
        'totalAmount',
        'suppliers',
      ]
    );
  }
}
