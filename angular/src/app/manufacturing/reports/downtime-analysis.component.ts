import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { DowntimeAnalysisService } from '../../proxy/manufacturing/downtime-analysis.service';
import type { DowntimeAnalysisReportDto } from '../../proxy/manufacturing/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-downtime-analysis',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0"><i class="fa fa-exclamation-circle me-2 text-danger"></i>{{ '::DowntimeAnalysis' | abpLocalization }}</h5>
          <small class="text-muted">{{ '::DowntimeAnalysisDescription' | abpLocalization }}</small>
        </div>
        @if (data() && data()!.rows.length > 0) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>
      <div class="card-body">
        <!-- Date Filters -->
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-3">
            <label class="form-label small text-muted">{{ '::From' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate">
          </div>
          <div class="col-md-3">
            <label class="form-label small text-muted">{{ '::To' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate">
          </div>
          <div class="col-md-2">
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
            <div class="col-md-3">
              <div class="card border-primary text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">Total Stoppages</div>
                  <div class="fs-4 fw-bold text-primary">{{ d.totalEntries }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-danger text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">{{ '::DowntimeInHours' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-danger">{{ d.totalDowntimeHours | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-warning text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">Total Downtime (Mins)</div>
                  <div class="fs-4 fw-bold text-warning">{{ d.totalDowntimeMinutes | number:'1.0-0' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-secondary text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">Top Stoppage Reason</div>
                  <div class="fs-6 fw-bold text-truncate text-secondary">
                    {{ d.reasonSummaries.length > 0 ? d.reasonSummaries[0].label : '-' }}
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Breakdown summaries -->
          <div class="row g-3 mb-4">
            <div class="col-md-6">
              <div class="card border h-100">
                <div class="card-header bg-light py-2">
                  <strong class="small text-muted">{{ '::MachineDowntime' | abpLocalization }}</strong>
                </div>
                <div class="card-body p-2">
                  <ul class="list-group list-group-flush">
                    @for (ws of d.workstationSummaries; track ws.label) {
                      <li class="list-group-item d-flex justify-content-between align-items-center py-2 px-1">
                        <div>
                          <strong>{{ ws.label }}</strong>
                          <span class="text-muted small ms-2">({{ ws.count }} stoppages)</span>
                        </div>
                        <span class="badge bg-danger">{{ ws.hours | number:'1.2-2' }} hrs</span>
                      </li>
                    } @empty {
                      <li class="list-group-item text-center text-muted py-2">No machine downtime</li>
                    }
                  </ul>
                </div>
              </div>
            </div>
            <div class="col-md-6">
              <div class="card border h-100">
                <div class="card-header bg-light py-2">
                  <strong class="small text-muted">Stop Reason Breakdown</strong>
                </div>
                <div class="card-body p-2">
                  <ul class="list-group list-group-flush">
                    @for (r of d.reasonSummaries; track r.label) {
                      <li class="list-group-item d-flex justify-content-between align-items-center py-2 px-1">
                        <div>
                          <strong>{{ r.label }}</strong>
                          <span class="text-muted small ms-2">({{ r.count }} occurrences)</span>
                        </div>
                        <span class="badge bg-warning text-dark">{{ r.hours | number:'1.2-2' }} hrs</span>
                      </li>
                    } @empty {
                      <li class="list-group-item text-center text-muted py-2">No stop reasons</li>
                    }
                  </ul>
                </div>
              </div>
            </div>
          </div>

          <!-- Table -->
          <div class="table-responsive">
            <table class="table table-sm table-hover align-middle">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Workstation' | abpLocalization }}</th>
                  <th>Operator</th>
                  <th>{{ '::From' | abpLocalization }}</th>
                  <th>{{ '::To' | abpLocalization }}</th>
                  <th class="text-end">{{ '::DowntimeInHours' | abpLocalization }}</th>
                  <th>{{ '::StopReason' | abpLocalization }}</th>
                  <th>{{ '::Remarks' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of d.rows; track row.id) {
                  <tr>
                    <td><strong>{{ row.workstationName }}</strong></td>
                    <td>{{ row.operatorName }}</td>
                    <td>{{ row.fromTime | date:'short' }}</td>
                    <td>{{ row.toTime | date:'short' }}</td>
                    <td class="text-end fw-bold text-danger">{{ row.downtimeHours | number:'1.2-2' }}</td>
                    <td><span class="badge bg-secondary">{{ row.stopReason }}</span></td>
                    <td><small class="text-muted">{{ row.remarks || '-' }}</small></td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="7" class="text-center py-4 text-muted">{{ '::NoDataAvailable' | abpLocalization }}</td>
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
export class DowntimeAnalysisComponent implements OnInit {
  private service = inject(DowntimeAnalysisService);
  private companyContext = inject(CompanyContextService);

  fromDate = '';
  toDate = '';

  isLoading = signal(false);
  data = signal<DowntimeAnalysisReportDto | null>(null);

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
    this.service.getReport({
      companyId,
      fromDate: this.fromDate ? this.fromDate : null,
      toDate: this.toDate ? this.toDate : null,
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
      Workstation: r.workstationName,
      Operator: r.operatorName,
      FromTime: r.fromTime,
      ToTime: r.toTime,
      DowntimeHours: r.downtimeHours,
      DowntimeMinutes: r.downtimeMinutes,
      StopReason: r.stopReason,
      Remarks: r.remarks ?? '',
    }));

    const columns = [
      'Workstation',
      'Operator',
      'FromTime',
      'ToTime',
      'DowntimeHours',
      'DowntimeMinutes',
      'StopReason',
      'Remarks',
    ];

    exportToCsv(`downtime-analysis-${this.fromDate}-to-${this.toDate}.csv`, exportRows, columns);
  }
}
