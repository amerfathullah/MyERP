import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { WorkOrderSummaryService } from '../../proxy/manufacturing/work-order-summary.service';
import { WorkOrderSummaryDto, WorkOrderSummaryRowDto } from '../../proxy/manufacturing/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-work-order-summary',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0"><i class="fa fa-clipboard-list me-2"></i>{{ 'WorkOrderSummary' | abpLocalization }}</h5>
        @if (data()) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ 'ExportCSV' | abpLocalization }}
          </button>
        }
      </div>
      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4">
          <div class="col-md-2">
            <label class="form-label">{{ 'From' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate">
          </div>
          <div class="col-md-2">
            <label class="form-label">{{ 'To' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate">
          </div>
          <div class="col-md-2">
            <label class="form-label">{{ 'BasedOn' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="basedOn">
              <option value="Creation Date">Creation Date</option>
              <option value="Planned Date">Planned Date</option>
              <option value="Actual Date">Actual Date</option>
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label">{{ 'MinAge' | abpLocalization }} (Days)</label>
            <input type="number" class="form-control form-control-sm" [(ngModel)]="minAge" min="0">
          </div>
          <div class="col-md-2">
            <label class="form-label">{{ 'ChartsBasedOn' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="chartsBasedOn">
              <option value="Quantity">Quantity</option>
              <option value="Status">Status</option>
              <option value="Age">Age</option>
            </select>
          </div>
          <div class="col-md-2 d-flex align-items-end">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ 'Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (data(); as d) {
          <!-- KPI Cards -->
          <div class="row g-3 mb-4">
            <div class="col-md-4">
              <div class="border rounded p-3 text-center">
                <div class="fs-3 fw-bold text-primary">{{ d.totalWorkOrders }}</div>
                <small class="text-muted">{{ 'TotalWorkOrders' | abpLocalization }}</small>
              </div>
            </div>
            <div class="col-md-4">
              <div class="border rounded p-3 text-center">
                <div class="fs-3 fw-bold text-info">{{ d.totalProduceQty | number:'1.2-2' }}</div>
                <small class="text-muted">{{ 'ProduceQty' | abpLocalization }}</small>
              </div>
            </div>
            <div class="col-md-4">
              <div class="border rounded p-3 text-center">
                <div class="fs-3 fw-bold text-success">{{ d.totalProducedQty | number:'1.2-2' }}</div>
                <small class="text-muted">{{ 'ProducedQty' | abpLocalization }}</small>
              </div>
            </div>
          </div>

          <!-- Chart representation -->
          @if (d.chart.labels && d.chart.labels.length > 0) {
            <div class="card mb-4">
              <div class="card-header py-2">
                <small class="fw-bold">{{ d.chart.chartType === 'bar' ? 'Monthly Production Overview' : 'Distribution Chart' }}</small>
              </div>
              <div class="card-body">
                <div class="row g-3">
                  @for (label of d.chart.labels; track label; let idx = $index) {
                    <div class="col-md-6 mb-2">
                      <div class="d-flex justify-content-between mb-1">
                        <small class="fw-medium">{{ label }}</small>
                        <small class="text-muted">
                          @for (ds of d.chart.datasets; track ds.name) {
                            <span class="ms-2">{{ ds.name }}: {{ ds.values[idx] | number:'1.0-2' }}</span>
                          }
                        </small>
                      </div>
                      <div class="progress" style="height: 10px">
                        @if (d.chart.chartType === 'bar') {
                          @let pending = d.chart.datasets[0]?.values[idx] || 0;
                          @let completed = d.chart.datasets[1]?.values[idx] || 0;
                          @let total = pending + completed;
                          @if (total > 0) {
                            <div class="progress-bar bg-success" [style.width.%]="(completed / total) * 100"></div>
                            <div class="progress-bar bg-warning" [style.width.%]="(pending / total) * 100"></div>
                          }
                        } @else {
                          @let val = d.chart.datasets[0]?.values[idx] || 0;
                          @let total = d.totalWorkOrders || 1;
                          <div class="progress-bar bg-primary" [style.width.%]="(val / total) * 100"></div>
                        }
                      </div>
                    </div>
                  }
                </div>
              </div>
            </div>
          }

          <!-- Details Table -->
          <div class="table-responsive">
            <table class="table table-sm table-hover align-middle">
              <thead class="table-light">
                <tr>
                  <th>{{ 'WorkOrder' | abpLocalization }}</th>
                  <th>{{ 'Status' | abpLocalization }}</th>
                  <th>{{ 'ProductionItem' | abpLocalization }}</th>
                  <th class="text-end">{{ 'ProduceQty' | abpLocalization }}</th>
                  <th class="text-end">{{ 'ProducedQty' | abpLocalization }}</th>
                  <th>{{ 'ActualFinishedGoods' | abpLocalization }}</th>
                  <th>{{ 'SalesOrder' | abpLocalization }}</th>
                  <th>{{ 'PlannedDates' | abpLocalization }}</th>
                  <th>{{ 'ActualDates' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Age' | abpLocalization }}</th>
                  <th class="text-end">{{ 'LeadTime' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of d.rows; track row.id) {
                  <tr>
                    <td><strong>{{ row.workOrderNumber }}</strong></td>
                    <td>
                      <span class="badge" [class.bg-success]="row.status === 'Completed'" [class.bg-warning]="row.status === 'InProcess'" [class.bg-secondary]="row.status === 'Draft'">
                        {{ row.status }}
                      </span>
                    </td>
                    <td>
                      <div>{{ row.productionItemName }}</div>
                      <small class="text-muted">{{ row.productionItemCode }}</small>
                    </td>
                    <td class="text-end">{{ row.qty | number:'1.2-2' }}</td>
                    <td class="text-end">{{ row.producedQty | number:'1.2-2' }}</td>
                    <td><small>{{ row.actualFinishedGoods || '—' }}</small></td>
                    <td>{{ row.salesOrderNumber || '—' }}</td>
                    <td>
                      <small>
                        {{ row.plannedStartDate ? (row.plannedStartDate | date:'shortDate') : '—' }}
                        to {{ row.plannedEndDate ? (row.plannedEndDate | date:'shortDate') : '—' }}
                      </small>
                    </td>
                    <td>
                      <small>
                        {{ row.actualStartDate ? (row.actualStartDate | date:'shortDate') : '—' }}
                        to {{ row.actualEndDate ? (row.actualEndDate | date:'shortDate') : '—' }}
                      </small>
                    </td>
                    <td class="text-end">{{ row.age }} d</td>
                    <td class="text-end">{{ row.leadTime ? (row.leadTime | number:'1.0-0') + ' min' : '—' }}</td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="11" class="text-center py-4 text-muted">{{ 'NoDataAvailable' | abpLocalization }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>
  `
})
export class WorkOrderSummaryComponent implements OnInit {
  private service = inject(WorkOrderSummaryService);
  private companyContext = inject(CompanyContextService);

  fromDate = '';
  toDate = '';
  basedOn = 'Creation Date';
  minAge = 0;
  chartsBasedOn = 'Quantity';

  isLoading = signal(false);
  data = signal<WorkOrderSummaryDto | null>(null);

  ngOnInit(): void {
    const now = new Date();
    const firstDay = new Date(now.getFullYear(), now.getMonth(), 1);
    this.fromDate = firstDay.toISOString().substring(0, 10);
    this.toDate = now.toISOString().substring(0, 10);
    this.loadData();
  }

  loadData(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.service.getSummary({
      companyId,
      fromDate: this.fromDate,
      toDate: this.toDate,
      basedOn: this.basedOn,
      age: this.minAge,
      chartsBasedOn: this.chartsBasedOn,
    }).subscribe({
      next: (res) => {
        this.data.set(res);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  exportCsv(): void {
    const d = this.data();
    if (!d || !d.rows.length) return;

    const exportRows = d.rows.map(r => ({
      WorkOrderNumber: r.workOrderNumber,
      Status: r.status,
      ItemCode: r.productionItemCode,
      ItemName: r.productionItemName,
      ProduceQty: r.qty,
      ProducedQty: r.producedQty,
      ActualFinishedGoods: r.actualFinishedGoods || '',
      SalesOrderNumber: r.salesOrderNumber || '',
      PlannedStartDate: r.plannedStartDate || '',
      PlannedEndDate: r.plannedEndDate || '',
      ActualStartDate: r.actualStartDate || '',
      ActualEndDate: r.actualEndDate || '',
      AgeDays: r.age,
      LeadTimeMinutes: r.leadTime || '',
    }));

    const columns = [
      'WorkOrderNumber',
      'Status',
      'ItemCode',
      'ItemName',
      'ProduceQty',
      'ProducedQty',
      'ActualFinishedGoods',
      'SalesOrderNumber',
      'PlannedStartDate',
      'PlannedEndDate',
      'ActualStartDate',
      'ActualEndDate',
      'AgeDays',
      'LeadTimeMinutes',
    ];

    exportToCsv(`work-order-summary-${this.fromDate}-to-${this.toDate}.csv`, exportRows, columns);
  }
}
