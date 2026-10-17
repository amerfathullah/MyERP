import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { JobCardSummaryService } from '../../proxy/manufacturing/job-card-summary.service';
import type { JobCardSummaryDto } from '../../proxy/manufacturing/models';
import { JobCardStatus, jobCardStatusOptions } from '../../proxy/manufacturing/job-card-status.enum';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-job-card-summary',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-id-card me-2 text-primary"></i>{{ '::JobCardSummary' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::JobCardSummaryDescription' | abpLocalization }}</small>
        </div>
        @if (data() && data()!.rows.length > 0) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-3">
            <label class="form-label small text-muted">{{ '::From' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" />
          </div>
          <div class="col-md-3">
            <label class="form-label small text-muted">{{ '::To' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" />
          </div>
          <div class="col-md-3">
            <label class="form-label small text-muted">{{ '::Status' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedStatus">
              <option [ngValue]="null">-- {{ '::All' | abpLocalization }} --</option>
              @for (opt of statusOptions; track opt.value) {
                <option [ngValue]="opt.value">{{ opt.key }}</option>
              }
            </select>
          </div>
          <div class="col-md-3">
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
                  <div class="text-muted small">{{ '::TotalJobCards' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-primary">{{ d.totalJobCards }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-success text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">{{ '::CompletedQty' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-success">{{ d.totalCompletedQty | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-warning text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">{{ '::TimeInMins' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-warning">{{ d.totalTimeInMins | number:'1.0-0' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-info text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">Total Hours</div>
                  <div class="fs-4 fw-bold text-info">{{ (d.totalTimeInMins / 60) | number:'1.1-1' }}h</div>
                </div>
              </div>
            </div>
          </div>

          <!-- Periodic Breakdown -->
          @if (d.chart && d.chart.labels && d.chart.labels.length > 0) {
            <div class="card border mb-4">
              <div class="card-header bg-light py-2">
                <strong class="small text-muted">Monthly Execution Breakdown</strong>
              </div>
              <div class="card-body p-2">
                <div class="table-responsive">
                  <table class="table table-sm table-bordered text-center mb-0">
                    <thead class="table-light">
                      <tr>
                        <th>Status</th>
                        @for (lbl of d.chart.labels; track lbl) {
                          <th>{{ lbl }}</th>
                        }
                      </tr>
                    </thead>
                    <tbody>
                      @for (ds of d.chart.datasets; track ds.name) {
                        <tr>
                          <td class="fw-bold text-start">
                            <span
                              class="badge"
                              [class.bg-success]="ds.name === 'Completed'"
                              [class.bg-primary]="ds.name !== 'Completed'"
                            >
                              {{ ds.name }}
                            </span>
                          </td>
                          @for (val of ds.values; track $index) {
                            <td>{{ val }}</td>
                          }
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          }

          <!-- Detailed Grid -->
          <div class="table-responsive">
            <table class="table table-striped table-hover align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Date' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th>{{ '::WorkOrder' | abpLocalization }}</th>
                  <th>{{ '::ProductionItem' | abpLocalization }}</th>
                  <th>{{ '::Operation' | abpLocalization }}</th>
                  <th>{{ '::Workstation' | abpLocalization }}</th>
                  <th class="text-end">{{ '::CompletedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::TimeInMins' | abpLocalization }}</th>
                  <th>Time Log</th>
                </tr>
              </thead>
              <tbody>
                @if (d.rows.length === 0) {
                  <tr>
                    <td colspan="9" class="text-center text-muted py-4">
                      {{ '::NoDataAvailable' | abpLocalization }}
                    </td>
                  </tr>
                }
                @for (row of d.rows; track row.id) {
                  <tr>
                    <td>{{ row.postingDate | date:'yyyy-MM-dd' }}</td>
                    <td>
                      <span
                        class="badge"
                        [class.bg-success]="row.status === 'Completed'"
                        [class.bg-info]="row.status === 'Work In Progress' || row.status === 'WorkInProgress'"
                        [class.bg-warning]="row.status === 'On Hold' || row.status === 'OnHold'"
                        [class.bg-danger]="row.status === 'Cancelled'"
                        [class.bg-secondary]="row.status === 'Open'"
                      >
                        {{ row.status }}
                      </span>
                    </td>
                    <td>
                      <strong>{{ row.workOrderNumber }}</strong>
                    </td>
                    <td>
                      <div><strong>{{ row.productionItemCode }}</strong></div>
                      @if (row.productionItemName && row.productionItemName !== row.productionItemCode) {
                        <div class="small text-muted">{{ row.productionItemName }}</div>
                      }
                    </td>
                    <td>{{ row.operationName || '-' }}</td>
                    <td>{{ row.workstationName || '-' }}</td>
                    <td class="text-end fw-bold">{{ row.totalCompletedQty | number:'1.2-2' }}</td>
                    <td class="text-end">{{ row.totalTimeInMins | number:'1.0-0' }}</td>
                    <td class="small text-muted">
                      @if (row.fromTime && row.toTime) {
                        {{ row.fromTime | date:'HH:mm' }} - {{ row.toTime | date:'HH:mm' }}
                      } @else if (row.fromTime) {
                        From: {{ row.fromTime | date:'HH:mm' }}
                      } @else {
                        -
                      }
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
export class JobCardSummaryComponent implements OnInit {
  private service = inject(JobCardSummaryService);
  private companyContext = inject(CompanyContextService);

  statusOptions = jobCardStatusOptions;

  fromDate = '';
  toDate = '';
  selectedStatus: number | null = null;

  isLoading = signal(false);
  data = signal<JobCardSummaryDto | null>(null);

  ngOnInit(): void {
    const today = new Date();
    const startOfMonth = new Date(today.getFullYear(), today.getMonth(), 1);

    this.fromDate = startOfMonth.toISOString().substring(0, 10);
    this.toDate = today.toISOString().substring(0, 10);

    this.loadData();
  }

  loadData(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.service
      .getSummary({
        companyId,
        fromDate: this.fromDate,
        toDate: this.toDate,
        status: this.selectedStatus,
      })
      .subscribe({
        next: res => {
          this.data.set(res);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false),
      });
  }

  exportCsv(): void {
    const d = this.data();
    if (!d || !d.rows.length) return;

    exportToCsv('job_card_summary.csv', d.rows, [
      'postingDate',
      'status',
      'workOrderNumber',
      'productionItemCode',
      'productionItemName',
      'operationName',
      'workstationName',
      'totalCompletedQty',
      'totalTimeInMins',
      'fromTime',
      'toTime',
    ]);
  }
}
