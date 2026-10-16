import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { CostOfPoorQualityReportService } from '../../proxy/manufacturing/cost-of-poor-quality-report.service';
import { CostOfPoorQualityReportDto } from '../../proxy/manufacturing/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-cost-of-poor-quality-report',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0"><i class="fa fa-exclamation-triangle me-2 text-warning"></i>{{ 'CostOfPoorQualityReport' | abpLocalization }}</h5>
          <small class="text-muted">{{ 'CostOfPoorQualityReportDescription' | abpLocalization }}</small>
        </div>
        @if (data()) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ 'ExportCSV' | abpLocalization }}
          </button>
        }
      </div>
      <div class="card-body">
        <!-- Filters -->
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
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ 'Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (data(); as d) {
          <!-- KPI Cards -->
          <div class="row g-3 mb-4">
            <div class="col-md-3">
              <div class="border rounded p-3 text-center bg-light">
                <div class="fs-3 fw-bold text-primary">{{ d.totalJobCards }}</div>
                <small class="text-muted">Corrective Job Cards</small>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-3 text-center bg-light">
                <div class="fs-3 fw-bold text-info">{{ d.totalTimeInMins | number:'1.1-1' }} <span class="fs-6 text-muted">mins</span></div>
                <small class="text-muted">Total Time Spent</small>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-3 text-center bg-light border-warning">
                <div class="fs-3 fw-bold text-danger">{{ d.totalOperatingCost | number:'1.2-2' }}</div>
                <small class="text-muted">Total Operating Cost</small>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-3 text-center bg-light">
                <div class="fs-3 fw-bold text-secondary">
                  {{ (d.totalJobCards > 0 ? (d.totalOperatingCost / d.totalJobCards) : 0) | number:'1.2-2' }}
                </div>
                <small class="text-muted">Avg Cost / Job Card</small>
              </div>
            </div>
          </div>

          <!-- Table -->
          <div class="table-responsive">
            <table class="table table-sm table-hover align-middle">
              <thead class="table-light">
                <tr>
                  <th>{{ 'WorkOrder' | abpLocalization }}</th>
                  <th>{{ 'Item' | abpLocalization }}</th>
                  <th>{{ 'Operation' | abpLocalization }}</th>
                  <th>{{ 'Workstation' | abpLocalization }}</th>
                  <th class="text-end">Hourly Rate</th>
                  <th class="text-end">Time (Mins)</th>
                  <th class="text-end">Operating Cost</th>
                  <th>Started</th>
                  <th>Completed</th>
                </tr>
              </thead>
              <tbody>
                @for (row of d.rows; track row.jobCardId) {
                  <tr>
                    <td><strong>{{ row.workOrderNumber }}</strong></td>
                    <td>
                      <div>{{ row.productionItemName }}</div>
                      <small class="text-muted">{{ row.productionItemCode }}</small>
                    </td>
                    <td>
                      <span class="badge bg-warning text-dark me-1">Rework</span>
                      {{ row.operationName }}
                    </td>
                    <td>{{ row.workstationName || '-' }}</td>
                    <td class="text-end">{{ row.hourRate | number:'1.2-2' }}</td>
                    <td class="text-end">{{ row.totalTimeInMins | number:'1.1-1' }}</td>
                    <td class="text-end fw-bold text-danger">{{ row.operatingCost | number:'1.2-2' }}</td>
                    <td><small class="text-muted">{{ row.startedAt | date:'short' }}</small></td>
                    <td><small class="text-muted">{{ row.completedAt | date:'short' }}</small></td>
                  </tr>
                }
                @if (d.rows.length === 0) {
                  <tr>
                    <td colspan="9" class="text-center py-4 text-muted">
                      <i class="fa fa-check-circle text-success me-1"></i>
                      No corrective or rework job cards recorded for the selected period.
                    </td>
                  </tr>
                }
              </tbody>
              @if (d.rows.length > 0) {
                <tfoot class="table-light fw-bold">
                  <tr>
                    <td colspan="5">{{ 'Total' | abpLocalization }}</td>
                    <td class="text-end">{{ d.totalTimeInMins | number:'1.1-1' }}</td>
                    <td class="text-end text-danger">{{ d.totalOperatingCost | number:'1.2-2' }}</td>
                    <td colspan="2"></td>
                  </tr>
                </tfoot>
              }
            </table>
          </div>
        }
      </div>
    </div>
  `
})
export class CostOfPoorQualityReportComponent implements OnInit {
  private service = inject(CostOfPoorQualityReportService);
  private companyContext = inject(CompanyContextService);

  data = signal<CostOfPoorQualityReportDto | null>(null);
  isLoading = signal(false);

  fromDate: string = '';
  toDate: string = '';

  ngOnInit(): void {
    const today = new Date();
    const firstDayOfYear = new Date(today.getFullYear(), 0, 1);
    this.fromDate = firstDayOfYear.toISOString().split('T')[0];
    this.toDate = today.toISOString().split('T')[0];
    this.loadData();
  }

  loadData(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.service.getReport({
      companyId,
      fromDate: this.fromDate ? new Date(this.fromDate).toISOString() : null,
      toDate: this.toDate ? new Date(this.toDate).toISOString() : null,
    }).subscribe({
      next: (res) => {
        this.data.set(res);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      }
    });
  }

  exportCsv(): void {
    const d = this.data();
    if (!d || !d.rows.length) return;

    const exportRows = d.rows.map(r => ({
      workOrderNumber: r.workOrderNumber,
      productionItemCode: r.productionItemCode,
      productionItemName: r.productionItemName,
      operationName: r.operationName,
      workstationName: r.workstationName ?? '',
      hourRate: r.hourRate,
      totalTimeInMins: r.totalTimeInMins,
      operatingCost: r.operatingCost,
      startedAt: r.startedAt ?? '',
      completedAt: r.completedAt ?? '',
    }));

    exportToCsv(
      `Cost_of_Poor_Quality_${new Date().toISOString().split('T')[0]}.csv`,
      exportRows,
      [
        'workOrderNumber',
        'productionItemCode',
        'productionItemName',
        'operationName',
        'workstationName',
        'hourRate',
        'totalTimeInMins',
        'operatingCost',
        'startedAt',
        'completedAt',
      ]
    );
  }
}
