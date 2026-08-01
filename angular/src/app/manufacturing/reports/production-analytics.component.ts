import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ProductionAnalyticsService } from '../../proxy/manufacturing/production-analytics.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-production-analytics',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0"><i class="fa fa-chart-bar me-2"></i>{{ 'ProductionAnalytics' | abpLocalization }}</h5>
        @if (data()) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ 'ExportCSV' | abpLocalization }}
          </button>
        }
      </div>
      <div class="card-body">
        <!-- Date Filters -->
        <div class="row g-2 mb-4">
          <div class="col-md-3">
            <label class="form-label">{{ 'From' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate">
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'To' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate">
          </div>
          <div class="col-md-2 d-flex align-items-end">
            <button class="btn btn-primary btn-sm" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ 'Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (data(); as d) {
          <!-- KPI Cards -->
          <div class="row g-3 mb-4">
            <div class="col-md-3">
              <div class="border rounded p-3 text-center">
                <div class="fs-3 fw-bold text-primary">{{ d.totalWorkOrders }}</div>
                <small class="text-muted">{{ 'TotalWorkOrders' | abpLocalization }}</small>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-3 text-center">
                <div class="fs-3 fw-bold text-success">{{ d.completionRate }}%</div>
                <small class="text-muted">{{ 'CompletionRate' | abpLocalization }}</small>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-3 text-center">
                <div class="fs-3 fw-bold text-info">{{ d.productionEfficiency }}%</div>
                <small class="text-muted">{{ 'PlannedVsActual' | abpLocalization }}</small>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-3 text-center" [class.border-danger]="d.overdueCount > 0">
                <div class="fs-3 fw-bold" [class.text-danger]="d.overdueCount > 0">{{ d.overdueCount }}</div>
                <small class="text-muted">{{ 'OverdueWorkOrders' | abpLocalization }}</small>
              </div>
            </div>
          </div>

          <div class="row g-3">
            <!-- Status Breakdown -->
            <div class="col-md-6">
              <div class="card h-100">
                <div class="card-header"><small class="fw-bold">{{ 'ByStatus' | abpLocalization }}</small></div>
                <div class="card-body">
                  @for (s of d.statusBreakdown; track s.status) {
                    @if (s.count > 0) {
                      <div class="d-flex justify-content-between align-items-center mb-2">
                        <span class="badge bg-{{ s.color }}">{{ s.status | abpLocalization }}</span>
                        <div class="d-flex align-items-center" style="width: 60%">
                          <div class="progress flex-grow-1 me-2" style="height: 8px">
                            <div class="progress-bar bg-{{ s.color }}" [style.width.%]="getStatusPct(s.count, d.totalWorkOrders)"></div>
                          </div>
                          <small class="text-muted">{{ s.count }}</small>
                        </div>
                      </div>
                    }
                  }
                </div>
              </div>
            </div>

            <!-- Qty Comparison -->
            <div class="col-md-6">
              <div class="card h-100">
                <div class="card-header"><small class="fw-bold">{{ 'ByQuantity' | abpLocalization }}</small></div>
                <div class="card-body">
                  <div class="mb-3">
                    <div class="d-flex justify-content-between mb-1">
                      <small>{{ 'PlannedQty' | abpLocalization }}</small>
                      <small class="fw-bold">{{ d.totalPlannedQty | number:'1.0-0' }}</small>
                    </div>
                    <div class="progress" style="height: 12px">
                      <div class="progress-bar bg-info" style="width: 100%"></div>
                    </div>
                  </div>
                  <div class="mb-3">
                    <div class="d-flex justify-content-between mb-1">
                      <small>{{ 'ProducedQty' | abpLocalization }}</small>
                      <small class="fw-bold">{{ d.totalProducedQty | number:'1.0-0' }}</small>
                    </div>
                    <div class="progress" style="height: 12px">
                      <div class="progress-bar bg-success" [style.width.%]="d.productionEfficiency"></div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Top Produced Items -->
          @if (d.topProducedItems.length > 0) {
            <div class="card mt-3">
              <div class="card-header"><small class="fw-bold">Top Produced Items</small></div>
              <div class="card-body p-0">
                <table class="table table-sm table-hover mb-0">
                  <thead class="table-light">
                    <tr>
                      <th>{{ 'Item' | abpLocalization }}</th>
                      <th class="text-end">{{ 'ProducedQty' | abpLocalization }}</th>
                      <th class="text-end">Work Orders</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (item of d.topProducedItems; track item.itemId) {
                      <tr>
                        <td>{{ item.itemName }}</td>
                        <td class="text-end fw-bold">{{ item.totalProduced | number:'1.0-2' }}</td>
                        <td class="text-end">{{ item.workOrderCount }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }
        } @else if (!isLoading()) {
          <p class="text-muted text-center py-4">{{ 'NoWorkOrdersInPeriod' | abpLocalization }}</p>
        }
      </div>
    </div>
  `
})
export class ProductionAnalyticsComponent implements OnInit {
  private analyticsService = inject(ProductionAnalyticsService);
  private companyContext = inject(CompanyContextService);

  data = signal<any>(null);
  isLoading = signal(false);

  fromDate = new Date(new Date().getFullYear(), new Date().getMonth() - 2, 1).toISOString().substring(0, 10);
  toDate = new Date().toISOString().substring(0, 10);

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.analyticsService.getAnalytics(companyId, this.fromDate, this.toDate).subscribe({
      next: (r: any) => { this.data.set(r); this.isLoading.set(false); },
      error: () => { this.isLoading.set(false); }
    });
  }

  getStatusPct(count: number, total: number): number {
    return total > 0 ? (count / total) * 100 : 0;
  }

  exportCsv() {
    const d = this.data();
    if (!d) return;
    const rows = d.statusBreakdown.map((s: any) => ({ Status: s.status, Count: s.count }));
    rows.push({ Status: 'TOTAL', Count: d.totalWorkOrders });
    exportToCsv('production-analytics.csv', rows, ['Status', 'Count']);
  }
}
