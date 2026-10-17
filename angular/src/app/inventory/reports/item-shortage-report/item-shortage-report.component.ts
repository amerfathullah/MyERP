import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ItemShortageReportService } from '../../../proxy/inventory/item-shortage-report.service';
import type { ItemShortageReportDto, ItemShortageReportRowDto } from '../../../proxy/inventory/models';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-item-shortage-report',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-triangle-exclamation me-2 text-danger"></i>{{ '::ItemShortageReport' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::ItemShortageReportDescription' | abpLocalization }}</small>
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
          <div class="col-md-4">
            <label class="form-label small fw-semibold">{{ '::Warehouse' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedWarehouseId" (change)="loadData()">
              <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
              @for (w of warehouses(); track w.id) {
                <option [value]="w.id">{{ w.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-4">
            <label class="form-label small fw-semibold">{{ '::Search' | abpLocalization }}</label>
            <input
              type="text"
              class="form-control form-control-sm"
              [(ngModel)]="searchText"
              placeholder="Filter by item or warehouse..."
              (keyup.enter)="loadData()"
            />
          </div>
          <div class="col-md-2 ms-auto">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (isLoading()) {
          <div class="text-center py-5">
            <i class="fa fa-spinner fa-spin fa-2x text-primary"></i>
          </div>
        } @else if (data(); as d) {
          <!-- KPI Cards -->
          <div class="row g-3 mb-4">
            <div class="col-md-4 col-sm-6">
              <div class="card bg-danger-subtle border-0 shadow-none h-100">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::TotalShortageItems' | abpLocalization }}</div>
                  <div class="h4 mb-0 fw-bold text-danger">{{ d.totalShortageItems }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-4 col-sm-6">
              <div class="card bg-danger-subtle border-0 shadow-none h-100">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::TotalProjectedDeficit' | abpLocalization }}</div>
                  <div class="h4 mb-0 fw-bold text-danger font-monospace">
                    {{ d.totalProjectedDeficit | number:'1.2-2' }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-4 col-sm-6">
              <div class="card bg-body-tertiary border-0 shadow-none h-100">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::TotalActualQty' | abpLocalization }}</div>
                  <div class="h4 mb-0 fw-bold font-monospace">{{ d.totalActualQty | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-4 col-sm-6">
              <div class="card bg-body-tertiary border-0 shadow-none h-100">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::TotalOrderedQty' | abpLocalization }}</div>
                  <div class="h4 mb-0 fw-bold font-monospace text-success">{{ d.totalOrderedQty | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-4 col-sm-6">
              <div class="card bg-body-tertiary border-0 shadow-none h-100">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::TotalPlannedQty' | abpLocalization }}</div>
                  <div class="h4 mb-0 fw-bold font-monospace text-info">{{ d.totalPlannedQty | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-4 col-sm-6">
              <div class="card bg-body-tertiary border-0 shadow-none h-100">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::TotalReservedQty' | abpLocalization }}</div>
                  <div class="h4 mb-0 fw-bold font-monospace text-warning">{{ d.totalReservedQty | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
          </div>

          <!-- Top Shortages Visualizer -->
          @if (d.chartData && d.chartData.length > 0) {
            <div class="card mb-4 border">
              <div class="card-header bg-light py-2">
                <h6 class="mb-0 small fw-bold">Top Shortage Items by Deficit</h6>
              </div>
              <div class="card-body py-3">
                <div class="row g-2">
                  @for (c of d.chartData; track c.itemCode) {
                    <div class="col-md-6">
                      <div class="d-flex justify-content-between small mb-1">
                        <span class="fw-medium font-monospace">{{ c.itemCode }}</span>
                        <span class="text-danger fw-bold font-monospace">{{ c.projectedQty | number:'1.2-2' }}</span>
                      </div>
                      <div class="progress" style="height: 6px;">
                        <div
                          class="progress-bar bg-danger"
                          role="progressbar"
                          [style.width.%]="getDeficitBarWidth(c.projectedQty, d.totalProjectedDeficit)"
                        ></div>
                      </div>
                    </div>
                  }
                </div>
              </div>
            </div>
          }

          <!-- Grid Table -->
          <div class="table-responsive">
            <table class="table table-hover align-middle table-sm border">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Warehouse' | abpLocalization }}</th>
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th>{{ '::Description' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ActualQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::OrderedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::PlannedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ReservedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ProjectedQty' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of d.rows; track row.itemId + '-' + row.warehouseId) {
                  <tr class="table-danger">
                    <td>
                      <span class="badge bg-light text-dark border">{{ row.warehouseName }}</span>
                    </td>
                    <td>
                      <div class="fw-semibold font-monospace">{{ row.itemCode }}</div>
                      <div class="text-muted small">{{ row.itemName }}</div>
                    </td>
                    <td class="small text-muted text-truncate" style="max-width: 200px;">
                      {{ row.description ?? '—' }}
                    </td>
                    <td class="text-end font-monospace">{{ row.actualQty | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace text-success">{{ row.orderedQty | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace text-info">{{ row.plannedQty | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace text-warning">
                      {{ (row.reservedQty + row.reservedQtyForProduction) | number:'1.2-2' }}
                    </td>
                    <td class="text-end font-monospace fw-bold text-danger">
                      {{ row.projectedQty | number:'1.2-2' }}
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="8" class="text-center text-muted py-5">
                      <i class="fa fa-circle-check fa-2x text-success mb-2 d-block"></i>
                      No stock shortages found. All items have positive or zero projected quantities.
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
export class ItemShortageReportComponent implements OnInit {
  private service = inject(ItemShortageReportService);
  private warehouseService = inject(WarehouseService);
  private companyContext = inject(CompanyContextService);

  selectedWarehouseId = '';
  searchText = '';

  warehouses = signal<{ id: string; name: string }[]>([]);
  data = signal<ItemShortageReportDto | null>(null);
  isLoading = signal<boolean>(false);

  ngOnInit(): void {
    this.loadWarehouses();
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

  loadData(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.service
      .getReport({
        companyId,
        warehouseId: this.selectedWarehouseId ? this.selectedWarehouseId : null,
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

  getDeficitBarWidth(projectedQty: number, totalDeficit: number): number {
    if (totalDeficit === 0) return 0;
    const pct = Math.abs(projectedQty) / Math.abs(totalDeficit) * 100;
    return Math.min(100, Math.max(5, Math.round(pct)));
  }

  exportCsv(): void {
    const currentData = this.data();
    if (!currentData || !currentData.rows || currentData.rows.length === 0) return;

    exportToCsv(
      'item-shortage-report',
      currentData.rows,
      [
        'warehouseName',
        'itemCode',
        'itemName',
        'description',
        'actualQty',
        'orderedQty',
        'plannedQty',
        'reservedQty',
        'reservedQtyForProduction',
        'projectedQty',
        'companyName',
      ]
    );
  }
}
