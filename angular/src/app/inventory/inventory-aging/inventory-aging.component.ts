import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { InventoryAgingService } from '../../proxy/inventory/inventory-aging.service';
import { exportToCsv } from '../../shared/utils/csv-export';

interface AgingBucket {
  label: string;
  itemCount: number;
  stockValue: number;
  percentage: number;
}

interface AgingItem {
  itemCode: string;
  itemName: string;
  warehouseName: string;
  quantity: number;
  valuationRate: number;
  stockValue: number;
  lastMovementDate: string | null;
  ageDays: number;
  ageBucket: string;
}

interface InventoryAgingReport {
  asOfDate: string;
  totalItems: number;
  totalStockValue: number;
  slowMovingValue: number;
  slowMovingCount: number;
  deadStockValue: number;
  deadStockCount: number;
  buckets: AgingBucket[];
  items: AgingItem[];
}

@Component({
  selector: 'app-inventory-aging',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'InventoryAging' | abpLocalization">
      <!-- Settings -->
      <div class="card mb-3">
        <div class="card-body py-2">
          <div class="row align-items-end g-2">
            <div class="col-auto">
              <label class="form-label small mb-0">{{ 'SlowMovingThreshold' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="slowMovingDays" (change)="loadReport()">
                <option [value]="60">60 {{ 'Days' | abpLocalization }}</option>
                <option [value]="90">90 {{ 'Days' | abpLocalization }}</option>
                <option [value]="120">120 {{ 'Days' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-auto">
              <label class="form-label small mb-0">{{ 'DeadStockThreshold' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="deadStockDays" (change)="loadReport()">
                <option [value]="120">120 {{ 'Days' | abpLocalization }}</option>
                <option [value]="180">180 {{ 'Days' | abpLocalization }}</option>
                <option [value]="365">365 {{ 'Days' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-auto ms-auto">
              <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()" [disabled]="!report()">
                <i class="fa fa-download me-1"></i>{{ 'ExportCSV' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      @if (loading()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (report()) {
        <!-- KPI Cards -->
        <div class="row g-3 mb-4">
          <div class="col-md-3">
            <div class="card border-primary h-100">
              <div class="card-body text-center">
                <div class="text-muted small">{{ 'TotalStockValue' | abpLocalization }}</div>
                <div class="fs-4 fw-bold text-primary">{{ report()!.totalStockValue | number:'1.2-2' }}</div>
                <div class="small text-muted">{{ report()!.totalItems }} items</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-warning h-100">
              <div class="card-body text-center">
                <div class="text-muted small">{{ 'SlowMoving' | abpLocalization }}</div>
                <div class="fs-4 fw-bold text-warning">{{ report()!.slowMovingValue | number:'1.2-2' }}</div>
                <div class="small text-muted">{{ report()!.slowMovingCount }} items</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-danger h-100">
              <div class="card-body text-center">
                <div class="text-muted small">{{ 'DeadStock' | abpLocalization }}</div>
                <div class="fs-4 fw-bold text-danger">{{ report()!.deadStockValue | number:'1.2-2' }}</div>
                <div class="small text-muted">{{ report()!.deadStockCount }} items</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-success h-100">
              <div class="card-body text-center">
                <div class="text-muted small">{{ 'HealthyStock' | abpLocalization }}</div>
                <div class="fs-4 fw-bold text-success">
                  {{ report()!.totalStockValue - report()!.slowMovingValue - report()!.deadStockValue | number:'1.2-2' }}
                </div>
                <div class="small text-muted">{{ report()!.totalItems - report()!.slowMovingCount - report()!.deadStockCount }} items</div>
              </div>
            </div>
          </div>
        </div>

        <!-- Bucket Summary -->
        <div class="card mb-4">
          <div class="card-header"><h6 class="mb-0">{{ 'AgingDistribution' | abpLocalization }}</h6></div>
          <div class="card-body">
            @for (bucket of report()!.buckets; track bucket.label) {
              <div class="mb-2">
                <div class="d-flex justify-content-between mb-1">
                  <span class="small">{{ bucket.label }} ({{ bucket.itemCount }} items)</span>
                  <span class="small fw-semibold">{{ bucket.stockValue | number:'1.2-2' }} ({{ bucket.percentage }}%)</span>
                </div>
                <div class="progress" style="height: 8px;">
                  <div class="progress-bar" [class.bg-success]="bucket.percentage < 30"
                       [class.bg-warning]="bucket.percentage >= 30 && bucket.percentage < 60"
                       [class.bg-danger]="bucket.percentage >= 60"
                       [style.width.%]="bucket.percentage" role="progressbar"></div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Items Table -->
        <div class="card">
          <div class="card-header"><h6 class="mb-0">{{ 'OldestStockItems' | abpLocalization }}</h6></div>
          <div class="card-body p-0">
            <div class="table-responsive">
              <table class="table table-sm table-hover mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ 'Item' | abpLocalization }}</th>
                    <th>{{ 'Warehouse' | abpLocalization }}</th>
                    <th class="text-end">{{ 'Quantity' | abpLocalization }}</th>
                    <th class="text-end">{{ 'StockValue' | abpLocalization }}</th>
                    <th>{{ 'LastMovement' | abpLocalization }}</th>
                    <th>{{ 'Age' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of report()!.items; track item.itemCode + item.warehouseName) {
                    <tr>
                      <td>
                        <span class="fw-semibold">{{ item.itemCode }}</span>
                        <br><small class="text-muted">{{ item.itemName }}</small>
                      </td>
                      <td>{{ item.warehouseName }}</td>
                      <td class="text-end">{{ item.quantity | number:'1.0-2' }}</td>
                      <td class="text-end fw-semibold">{{ item.stockValue | number:'1.2-2' }}</td>
                      <td>
                        @if (item.lastMovementDate) {
                          {{ item.lastMovementDate | date:'dd/MM/yyyy' }}
                        } @else {
                          <span class="text-muted">—</span>
                        }
                      </td>
                      <td>
                        <span class="badge"
                              [class.bg-success]="item.ageDays < 30"
                              [class.bg-info]="item.ageDays >= 30 && item.ageDays < 60"
                              [class.bg-warning]="item.ageDays >= 60 && item.ageDays < slowMovingDays"
                              [class.bg-orange]="item.ageDays >= slowMovingDays && item.ageDays < deadStockDays"
                              [class.bg-danger]="item.ageDays >= deadStockDays">
                          {{ item.ageDays }}d
                        </span>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      }
    </abp-page>
  `
})
export class InventoryAgingComponent implements OnInit {
  private agingService = inject(InventoryAgingService);
  private companyContext = inject(CompanyContextService);

  report = signal<InventoryAgingReport | null>(null);
  loading = signal(false);
  slowMovingDays = 90;
  deadStockDays = 180;

  ngOnInit() {
    this.loadReport();
  }

  loadReport() {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.loading.set(true);
    this.agingService.getReport({
      companyId,
      slowMovingDays: this.slowMovingDays,
      deadStockDays: this.deadStockDays
    } as any).subscribe({
      next: (data) => {
        this.report.set(data as any);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  exportCsv() {
    if (!this.report()) return;
    const rows = this.report()!.items.map(i => ({
      'Item Code': i.itemCode,
      'Item Name': i.itemName,
      'Warehouse': i.warehouseName,
      'Quantity': i.quantity,
      'Valuation Rate': i.valuationRate,
      'Stock Value': i.stockValue,
      'Last Movement': i.lastMovementDate || 'N/A',
      'Age (Days)': i.ageDays,
      'Category': i.ageBucket
    }));
    exportToCsv('inventory-aging', rows, ['Item Code', 'Item Name', 'Warehouse', 'Quantity', 'Valuation Rate', 'Stock Value', 'Last Movement', 'Age (Days)', 'Category']);
  }
}
