import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ProductionPlanningReportService } from '../../proxy/manufacturing/production-planning-report.service';
import type {
  ProductionPlanningReportDto,
  ProductionPlanningReportRowDto,
} from '../../proxy/manufacturing/models';
import type { ItemDto, WarehouseDto } from '../../proxy/inventory/models';
import { ItemService } from '../../proxy/inventory/item.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-production-planning-report',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-industry me-2 text-primary"></i>
            {{ '::ProductionPlanningReport' | abpLocalization }}
          </h5>
          <small class="text-muted">
            {{ '::ProductionPlanningReportDescription' | abpLocalization }}
          </small>
        </div>
        <div>
          @if (data() && data()!.rows && data()!.rows.length > 0) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
      </div>

      <div class="card-body">
        <!-- Filter Controls -->
        <div class="p-3 bg-light rounded mb-4">
          <div class="row g-2 mb-2">
            <!-- Based On -->
            <div class="col-md-3 col-sm-6">
              <label class="form-label small fw-semibold">Based On</label>
              <select class="form-select form-select-sm" [(ngModel)]="basedOn" (ngModelChange)="loadData()">
                <option value="WorkOrder">Work Order</option>
                <option value="SalesOrder">Sales Order</option>
                <option value="MaterialRequest">Material Request</option>
              </select>
            </div>

            <!-- Warehouse -->
            <div class="col-md-3 col-sm-6">
              <label class="form-label small fw-semibold">{{ '::RawMaterialWarehouse' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="rawMaterialWarehouseId" (ngModelChange)="loadData()">
                <option [ngValue]="null">-- All Warehouses --</option>
                @for (w of warehouses(); track w.id) {
                  <option [value]="w.id">{{ w.name }}</option>
                }
              </select>
            </div>

            <!-- Item -->
            <div class="col-md-3 col-sm-6">
              <label class="form-label small fw-semibold">{{ '::Item' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="itemId" (ngModelChange)="loadData()">
                <option [ngValue]="null">-- All Items --</option>
                @for (item of items(); track item.id) {
                  <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
                }
              </select>
            </div>

            <!-- Search Term -->
            <div class="col-md-3 col-sm-6">
              <label class="form-label small fw-semibold">{{ '::Search' | abpLocalization }}</label>
              <div class="input-group input-group-sm">
                <input
                  type="text"
                  class="form-control form-control-sm"
                  [(ngModel)]="searchTerm"
                  (keyup.enter)="loadData()"
                  placeholder="Order No, Item, Material..."
                />
                <button class="btn btn-outline-secondary" type="button" (click)="loadData()">
                  <i class="fa fa-search"></i>
                </button>
              </div>
            </div>
          </div>

          <div class="d-flex justify-content-between align-items-center mt-3 pt-2 border-top">
            <div class="form-check form-switch mb-0">
              <input
                type="checkbox"
                class="form-check-input"
                id="shortageOnlyCheck"
                [(ngModel)]="shortageOnly"
              />
              <label class="form-check-label small" for="shortageOnlyCheck">
                Only Show Shortages
              </label>
            </div>
            <button class="btn btn-primary btn-sm px-4" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (isLoading()) {
          <div class="text-center py-5">
            <i class="fa fa-spinner fa-spin fa-2x text-primary"></i>
          </div>
        } @else if (data(); as d) {
          <!-- KPI Summary Cards -->
          <div class="row g-3 mb-4">
            <div class="col-md-2 col-sm-4">
              <div class="card border-primary text-center h-100 bg-light">
                <div class="card-body py-2">
                  <div class="text-muted small">Orders</div>
                  <div class="fs-5 fw-bold text-primary">{{ d.totalOrdersCount }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-2 col-sm-4">
              <div class="card border-info text-center h-100 bg-light">
                <div class="card-body py-2">
                  <div class="text-muted small">Materials</div>
                  <div class="fs-5 fw-bold text-info">{{ d.totalRawMaterialsCount }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-2 col-sm-4">
              <div class="card border-secondary text-center h-100 bg-light">
                <div class="card-body py-2">
                  <div class="text-muted small">Req Qty</div>
                  <div class="fs-5 fw-bold text-secondary">{{ d.totalRequiredQty | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-2 col-sm-4">
              <div class="card border-success text-center h-100 bg-light">
                <div class="card-body py-2">
                  <div class="text-muted small">Allotted</div>
                  <div class="fs-5 fw-bold text-success">{{ d.totalAllottedQty | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-2 col-sm-4">
              <div class="card border-danger text-center h-100 bg-light">
                <div class="card-body py-2">
                  <div class="text-muted small">Shortage</div>
                  <div class="fs-5 fw-bold text-danger">{{ d.totalShortageQty | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-2 col-sm-4">
              <div class="card border-warning text-center h-100 bg-light">
                <div class="card-body py-2">
                  <div class="text-muted small">Expected PO</div>
                  <div class="fs-5 fw-bold text-warning">{{ d.totalExpectedArrivalQty | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
          </div>

          <!-- Table -->
          @if (displayedRows().length > 0) {
            <div class="table-responsive">
              <table class="table table-sm table-striped table-hover align-middle mb-0">
                <thead class="table-light">
                  <tr>
                    <th>Order No</th>
                    <th>Type</th>
                    <th>FG Item</th>
                    <th>FG Wh</th>
                    <th class="text-end">To Mfg</th>
                    <th class="text-end">FG Avail</th>
                    <th>Planned Date</th>
                    <th>Raw Material</th>
                    <th>RM Wh</th>
                    <th class="text-end">Req Qty</th>
                    <th class="text-end">Allotted</th>
                    <th class="text-end">Shortage</th>
                    <th>PO Arrival</th>
                  </tr>
                </thead>
                <tbody>
                  @for (r of displayedRows(); track r.rowId) {
                    <tr>
                      <td class="fw-semibold">{{ r.orderNo }}</td>
                      <td>
                        <span class="badge" [ngClass]="getOrderBadgeClass(r.orderType)">
                          {{ r.orderType }}
                        </span>
                      </td>
                      <td>
                        <div>{{ r.productionItemName }}</div>
                        <small class="text-muted font-monospace">{{ r.productionItemCode }}</small>
                      </td>
                      <td>{{ r.forWarehouse || '-' }}</td>
                      <td class="text-end">{{ r.qtyToManufacture | number:'1.2-2' }}</td>
                      <td class="text-end">
                        <span [class.text-success]="r.availableProductionQty > 0">
                          {{ r.availableProductionQty | number:'1.2-2' }}
                        </span>
                      </td>
                      <td>{{ r.plannedDate ? (r.plannedDate | date:'yyyy-MM-dd') : '-' }}</td>
                      <td>
                        <div>{{ r.rawMaterialName }}</div>
                        <small class="text-muted font-monospace">{{ r.rawMaterialCode }}</small>
                      </td>
                      <td>{{ r.rawMaterialWarehouse || '-' }}</td>
                      <td class="text-end fw-semibold">{{ r.requiredQty | number:'1.2-2' }}</td>
                      <td class="text-end text-success">{{ r.allottedQty | number:'1.2-2' }}</td>
                      <td class="text-end">
                        @if (r.shortageQty > 0) {
                          <span class="badge bg-danger">
                            {{ r.shortageQty | number:'1.2-2' }}
                          </span>
                        } @else {
                          <span class="badge bg-success">0.00</span>
                        }
                      </td>
                      <td>
                        @if (r.expectedArrivalQty > 0) {
                          <span class="badge bg-warning text-dark">
                            {{ r.expectedArrivalQty | number:'1.2-2' }} on {{ r.expectedArrivalDate | date:'yyyy-MM-dd' }}
                          </span>
                        } @else {
                          <span class="text-muted">-</span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          } @else {
            <div class="alert alert-info text-center my-4">
              <i class="fa fa-info-circle me-2"></i>
              {{ '::NoProductionPlanningRecordsFound' | abpLocalization }}
            </div>
          }
        }
      </div>
    </div>
  `,
})
export class ProductionPlanningReportComponent implements OnInit {
  private reportService = inject(ProductionPlanningReportService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);
  private companyContext = inject(CompanyContextService);

  basedOn = 'WorkOrder';
  rawMaterialWarehouseId: string | null = null;
  itemId: string | null = null;
  searchTerm = '';
  shortageOnly = false;

  items = signal<ItemDto[]>([]);
  warehouses = signal<WarehouseDto[]>([]);
  data = signal<ProductionPlanningReportDto | null>(null);
  isLoading = signal(false);

  displayedRows = computed(() => {
    const d = this.data();
    if (!d || !d.rows) return [];
    if (this.shortageOnly) {
      return d.rows.filter(r => r.shortageQty > 0);
    }
    return d.rows;
  });

  ngOnInit() {
    this.loadLookups();
    this.loadData();
  }

  loadLookups() {
    this.warehouseService.getList({ maxResultCount: 500 }).subscribe(res => {
      this.warehouses.set(res.items ?? []);
    });

    this.itemService.getList({ maxResultCount: 500 }).subscribe(res => {
      this.items.set(res.items ?? []);
    });
  }

  loadData() {
    this.isLoading.set(true);
    const companyId = this.companyContext.selectedCompanyId();

    this.reportService
      .getReport({
        companyId: companyId || undefined,
        basedOn: this.basedOn,
        rawMaterialWarehouseId: this.rawMaterialWarehouseId || undefined,
        itemId: this.itemId || undefined,
        searchTerm: this.searchTerm ? this.searchTerm.trim() : undefined,
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

  getOrderBadgeClass(orderType: string): string {
    if (orderType === 'Work Order') return 'bg-primary';
    if (orderType === 'Sales Order') return 'bg-success';
    if (orderType === 'Material Request') return 'bg-info text-dark';
    return 'bg-secondary';
  }

  exportCsv() {
    const rows = this.displayedRows();
    if (!rows.length) return;

    const columns: (keyof ProductionPlanningReportRowDto)[] = [
      'orderNo',
      'orderType',
      'productionItemCode',
      'productionItemName',
      'forWarehouse',
      'qtyToManufacture',
      'availableProductionQty',
      'plannedDate',
      'rawMaterialCode',
      'rawMaterialName',
      'rawMaterialWarehouse',
      'requiredQty',
      'allottedQty',
      'shortageQty',
      'expectedArrivalDate',
      'expectedArrivalQty',
    ];

    exportToCsv(
      `production-planning-report-${new Date().toISOString().slice(0, 10)}.csv`,
      rows,
      columns
    );
  }
}
