import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { SalesPipelineService } from '../../../proxy/crm/sales-pipeline.service';
import type {
  FirstResponseTimeReportDto,
  FirstResponseTimeRowDto,
} from '../../../proxy/crm/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function computeOverallAverageResponseSeconds(
  rows: FirstResponseTimeRowDto[] | undefined,
): number {
  if (!rows || rows.length === 0) {
    return 0;
  }
  const sum = rows.reduce((acc, r) => acc + (r.avgResponseTimeSeconds ?? 0), 0);
  return Math.round((sum / rows.length) * 100) / 100;
}

@Component({
  selector: 'app-first-response-time',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fas fa-stopwatch text-primary me-2"></i>
            {{ '::FirstResponseTimeReport' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::FirstResponseTimeReportDescription' | abpLocalization }}
          </p>
        </div>
        @if ((report()?.data?.length ?? 0) > 0) {
          <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fas fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <!-- Filters -->
      <div class="card mb-3 shadow-sm border-0">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::FromDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="fromDate"
                (change)="loadReport()"
              />
            </div>
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::ToDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="toDate"
                (change)="loadReport()"
              />
            </div>
            <div class="col-md-2 d-flex align-items-end">
              <button
                class="btn btn-sm btn-primary w-100"
                (click)="loadReport()"
                [disabled]="loading()"
              >
                @if (loading()) {
                  <span class="spinner-border spinner-border-sm me-1"></span>
                } @else {
                  <i class="fas fa-sync me-1"></i>
                }
                {{ '::Refresh' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- KPI -->
      @if (report()) {
        <div class="row g-2 mb-3">
          <div class="col-md-4">
            <div class="card border-0 shadow-sm bg-primary-subtle text-primary py-2 px-3">
              <div class="small fw-bold">{{ '::AvgResponseTime' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ overallAvgSeconds() | number: '1.2-2' }}s</div>
            </div>
          </div>
        </div>
      }

      <!-- Table -->
      <div class="card border-0 shadow-sm">
        <div class="table-responsive">
          <table class="table table-hover table-sm align-middle mb-0">
            <thead class="table-light">
              <tr>
                <th>{{ '::CreatedDate' | abpLocalization }}</th>
                <th>{{ '::AvgResponseTime' | abpLocalization }}</th>
                <th class="text-end">{{ '::AvgResponseTime' | abpLocalization }} (s)</th>
              </tr>
            </thead>
            <tbody>
              @if (loading()) {
                <tr>
                  <td colspan="3" class="text-center py-4 text-muted">
                    <span class="spinner-border spinner-border-sm me-2"></span>
                    {{ '::Loading' | abpLocalization }}
                  </td>
                </tr>
              } @else if ((report()?.data?.length ?? 0) === 0) {
                <tr>
                  <td colspan="3" class="text-center py-4 text-muted">
                    {{ '::NoDataAvailable' | abpLocalization }}
                  </td>
                </tr>
              } @else {
                @for (row of report()!.data ?? []; track row.creationDate) {
                  <tr>
                    <td class="fw-semibold">{{ row.creationDate | date: 'yyyy-MM-dd' }}</td>
                    <td>{{ row.formattedResponseTime }}</td>
                    <td class="text-end">{{ (row.avgResponseTimeSeconds ?? 0) | number: '1.2-2' }}</td>
                  </tr>
                }
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `,
})
export class FirstResponseTimeComponent implements OnInit {
  private pipelineService = inject(SalesPipelineService);
  private companyContext = inject(CompanyContextService);

  report = signal<FirstResponseTimeReportDto | null>(null);
  loading = signal(false);

  fromDate = '';
  toDate = '';

  overallAvgSeconds = computed(() =>
    computeOverallAverageResponseSeconds(this.report()?.data),
  );

  ngOnInit(): void {
    this.companyContext.load();
    const now = new Date();
    const monthStart = new Date(now.getFullYear(), now.getMonth(), 1);
    this.fromDate = monthStart.toISOString().slice(0, 10);
    this.toDate = now.toISOString().slice(0, 10);
    this.loadReport();
  }

  loadReport(): void {
    this.loading.set(true);
    const companyId = this.companyContext.currentCompanyId() || null;

    this.pipelineService
      .getFirstResponseTimeReport({
        companyId,
        fromDate: this.fromDate || null,
        toDate: this.toDate || null,
      })
      .subscribe({
        next: res => {
          this.report.set(res);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }

  exportCsv(): void {
    exportToCsv('first-response-time-opportunity.csv', this.report()?.data ?? [], [
      'creationDate',
      'formattedResponseTime',
      'avgResponseTimeSeconds',
    ]);
  }
}
