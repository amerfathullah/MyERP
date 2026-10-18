import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ExponentialSmoothingForecastingService } from '../../proxy/manufacturing/exponential-smoothing-forecasting.service';
import type {
  ExponentialSmoothingForecastingDto,
  ExponentialSmoothingForecastingRowDto,
  ForecastingPeriodDto,
} from '../../proxy/manufacturing/models';
import type { ItemDto, WarehouseDto } from '../../proxy/inventory/models';
import { ItemService } from '../../proxy/inventory/item.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-exponential-smoothing-forecasting',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-chart-line me-2 text-primary"></i>
            {{ '::ExponentialSmoothingForecasting' | abpLocalization }}
          </h5>
          <small class="text-muted">
            {{ '::ExponentialSmoothingForecastingDescription' | abpLocalization }}
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
            <!-- Based On Document -->
            <div class="col-md-2 col-sm-6">
              <label class="form-label small fw-semibold">Document</label>
              <select class="form-select form-select-sm" [(ngModel)]="basedOnDocument">
                <option value="Sales Order">Sales Order</option>
                <option value="Delivery Note">Delivery Note</option>
                <option value="Sales Invoice">Sales Invoice</option>
              </select>
            </div>

            <!-- Based On Field -->
            <div class="col-md-2 col-sm-6">
              <label class="form-label small fw-semibold">Value Based On</label>
              <select class="form-select form-select-sm" [(ngModel)]="basedOnField">
                <option value="Qty">Quantity</option>
                <option value="Amount">Amount</option>
              </select>
            </div>

            <!-- Periodicity -->
            <div class="col-md-2 col-sm-6">
              <label class="form-label small fw-semibold">Periodicity</label>
              <select class="form-select form-select-sm" [(ngModel)]="periodicity">
                <option value="Monthly">Monthly</option>
                <option value="Quarterly">Quarterly</option>
                <option value="Half-Yearly">Half-Yearly</option>
                <option value="Yearly">Yearly</option>
              </select>
            </div>

            <!-- From Date (Start of Forecast) -->
            <div class="col-md-3 col-sm-6">
              <label class="form-label small fw-semibold">Forecast From</label>
              <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" />
            </div>

            <!-- To Date (End of Forecast) -->
            <div class="col-md-3 col-sm-6">
              <label class="form-label small fw-semibold">Forecast To</label>
              <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" />
            </div>
          </div>

          <div class="row g-2 mb-2">
            <!-- History Years -->
            <div class="col-md-2 col-sm-6">
              <label class="form-label small fw-semibold">History Years</label>
              <select class="form-select form-select-sm" [(ngModel)]="noOfYears">
                <option [ngValue]="1">1 Year</option>
                <option [ngValue]="2">2 Years</option>
                <option [ngValue]="3">3 Years</option>
                <option [ngValue]="5">5 Years</option>
              </select>
            </div>

            <!-- Smoothing Constant (alpha) -->
            <div class="col-md-2 col-sm-6">
              <label class="form-label small fw-semibold">
                {{ '::SmoothingConstant' | abpLocalization }}: {{ smoothingConstant }}
              </label>
              <input
                type="range"
                class="form-range"
                min="0.05"
                max="1.0"
                step="0.05"
                [(ngModel)]="smoothingConstant"
              />
            </div>

            <!-- Item -->
            <div class="col-md-3 col-sm-6">
              <label class="form-label small fw-semibold">{{ '::Item' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="itemId">
                <option [ngValue]="null">-- All Items --</option>
                @for (item of items(); track item.id) {
                  <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
                }
              </select>
            </div>

            <!-- Warehouse -->
            <div class="col-md-2 col-sm-6">
              <label class="form-label small fw-semibold">{{ '::Warehouse' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="warehouseId">
                <option [ngValue]="null">-- All Warehouses --</option>
                @for (w of warehouses(); track w.id) {
                  <option [value]="w.id">{{ w.name }}</option>
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
                  placeholder="Code, name, warehouse..."
                />
                <button class="btn btn-outline-secondary" type="button" (click)="loadData()">
                  <i class="fa fa-search"></i>
                </button>
              </div>
            </div>
          </div>

          <div class="d-flex justify-content-end mt-3 pt-2 border-top">
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
            <div class="col-md-3 col-sm-6">
              <div class="card border-primary text-center h-100 bg-light">
                <div class="card-body py-2">
                  <div class="text-muted small">Past Actual Demand</div>
                  <div class="fs-5 fw-bold text-primary">{{ d.totalDemandPastData | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card border-info text-center h-100 bg-light">
                <div class="card-body py-2">
                  <div class="text-muted small">Past Smoothed Forecast</div>
                  <div class="fs-5 fw-bold text-info">{{ d.totalForecastPastData | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card border-success text-center h-100 bg-light">
                <div class="card-body py-2">
                  <div class="text-muted small">Future Forecast Demand</div>
                  <div class="fs-5 fw-bold text-success">{{ d.totalForecastFutureData | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card border-warning text-center h-100 bg-light">
                <div class="card-body py-2">
                  <div class="text-muted small">Alpha (α)</div>
                  <div class="fs-5 fw-bold text-warning">{{ smoothingConstant }}</div>
                </div>
              </div>
            </div>
          </div>

          <!-- Dynamic Period Grid -->
          @if (d.rows && d.rows.length > 0) {
            <div class="table-responsive">
              <table class="table table-sm table-bordered table-hover align-middle mb-0">
                <thead class="table-light">
                  <tr>
                    <th style="min-width: 140px;">Item Code</th>
                    <th style="min-width: 160px;">Item Name</th>
                    <th style="min-width: 120px;">Warehouse</th>
                    <th style="min-width: 90px;" class="text-end">Avg Demand</th>
                    @for (p of d.periods; track p.key) {
                      <th
                        class="text-end"
                        [class.table-success]="p.isForecast"
                        style="min-width: 100px;"
                      >
                        <div>{{ p.label }}</div>
                        <small class="badge" [ngClass]="p.isForecast ? 'bg-success' : 'bg-secondary'">
                          {{ p.isForecast ? 'Forecast' : 'Actual' }}
                        </small>
                      </th>
                    }
                    <th class="text-end" style="min-width: 100px;">Total Demand</th>
                    <th class="text-end" style="min-width: 100px;">Total Forecast</th>
                  </tr>
                </thead>
                <tbody>
                  @for (r of d.rows; track r.itemId + (r.warehouseId || '')) {
                    <tr>
                      <td class="fw-semibold font-monospace">{{ r.itemCode }}</td>
                      <td>{{ r.itemName }}</td>
                      <td>{{ r.warehouseName || '-' }}</td>
                      <td class="text-end text-muted">{{ r.averageHistoricalDemand | number:'1.2-2' }}</td>
                      @for (p of d.periods; track p.key) {
                        <td
                          class="text-end"
                          [class.bg-success-subtle]="p.isForecast"
                          [class.fw-bold]="p.isForecast"
                        >
                          {{ (r.periodValues[p.key] || 0) | number:'1.2-2' }}
                        </td>
                      }
                      <td class="text-end fw-semibold text-primary">{{ r.totalDemand | number:'1.2-2' }}</td>
                      <td class="text-end fw-semibold text-success">{{ r.totalForecast | number:'1.2-2' }}</td>
                    </tr>
                  }
                  @if (d.totalRow; as tr) {
                    <tr class="table-dark fw-bold">
                      <td colspan="3">Total</td>
                      <td class="text-end">-</td>
                      @for (p of d.periods; track p.key) {
                        <td class="text-end">
                          {{ (tr.periodValues[p.key] || 0) | number:'1.2-2' }}
                        </td>
                      }
                      <td class="text-end">{{ tr.totalDemand | number:'1.2-2' }}</td>
                      <td class="text-end">{{ tr.totalForecast | number:'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          } @else {
            <div class="alert alert-info text-center my-4">
              <i class="fa fa-info-circle me-2"></i>
              {{ '::NoForecastingDataFound' | abpLocalization }}
            </div>
          }
        }
      </div>
    </div>
  `,
})
export class ExponentialSmoothingForecastingComponent implements OnInit {
  private forecastService = inject(ExponentialSmoothingForecastingService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);
  private companyContext = inject(CompanyContextService);

  basedOnDocument = 'Sales Order';
  basedOnField = 'Qty';
  periodicity = 'Monthly';
  fromDate = new Date().toISOString().slice(0, 10);
  toDate = new Date(Date.now() + 90 * 86400000).toISOString().slice(0, 10);
  noOfYears = 1;
  smoothingConstant = 0.5;
  itemId: string | null = null;
  warehouseId: string | null = null;
  searchTerm = '';

  items = signal<ItemDto[]>([]);
  warehouses = signal<WarehouseDto[]>([]);
  data = signal<ExponentialSmoothingForecastingDto | null>(null);
  isLoading = signal(false);

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

    this.forecastService
      .getForecast({
        companyId: companyId || undefined,
        basedOnDocument: this.basedOnDocument,
        basedOnField: this.basedOnField,
        periodicity: this.periodicity,
        fromDate: this.fromDate,
        toDate: this.toDate,
        noOfYears: this.noOfYears,
        smoothingConstant: Number(this.smoothingConstant),
        itemId: this.itemId || undefined,
        warehouseId: this.warehouseId || undefined,
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

  exportCsv() {
    const d = this.data();
    if (!d || !d.rows || d.rows.length === 0) return;

    const flattened = d.rows.map(r => {
      const rowObj: Record<string, any> = {
        ItemCode: r.itemCode,
        ItemName: r.itemName,
        Warehouse: r.warehouseName,
        AvgDemand: r.averageHistoricalDemand,
      };

      for (const p of d.periods) {
        rowObj[p.label] = r.periodValues[p.key] || 0;
      }

      rowObj['TotalDemand'] = r.totalDemand;
      rowObj['TotalForecast'] = r.totalForecast;
      return rowObj;
    });

    const headers = Object.keys(flattened[0]);
    exportToCsv(
      `exponential-smoothing-forecast-${new Date().toISOString().slice(0, 10)}.csv`,
      flattened,
      headers
    );
  }
}
