import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ProcessLossReportService } from '../../proxy/manufacturing/process-loss-report.service';
import type { ProcessLossReportDto } from '../../proxy/manufacturing/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-process-loss-report',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-recycle me-2 text-warning"></i>{{ '::ProcessLossReport' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::ProcessLossReportDescription' | abpLocalization }}</small>
        </div>
        @if (data() && data()!.rows && data()!.rows!.length > 0) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <div class="card-body">
        <!-- Date Filters -->
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-4">
            <label class="form-label small text-muted">{{ '::From' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" />
          </div>
          <div class="col-md-4">
            <label class="form-label small text-muted">{{ '::To' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" />
          </div>
          <div class="col-md-4">
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
                  <div class="text-muted small">{{ '::TotalProducedQty' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-primary">{{ d.totalProducedQty || 0 | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-danger text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">{{ '::TotalProcessLossQty' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-danger">{{ d.totalProcessLossQty || 0 | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-warning text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">{{ '::TotalProcessLossValue' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-warning">{{ d.totalProcessLossValue || 0 | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-info text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">{{ '::ProcessLossPercent' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-info">
                    @if ((d.totalProducedQty || 0) + (d.totalProcessLossQty || 0) > 0) {
                      {{ ((d.totalProcessLossQty || 0) / ((d.totalProducedQty || 0) + (d.totalProcessLossQty || 0)) * 100) | number:'1.1-1' }}%
                    } @else {
                      0.0%
                    }
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Detailed Grid -->
          <div class="table-responsive">
            <table class="table table-striped table-hover align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::WorkOrder' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th class="text-end">{{ '::QtyToManufacture' | abpLocalization }}</th>
                  <th class="text-end">{{ '::CompletedQty' | abpLocalization }}</th>
                  <th class="text-end text-danger">{{ '::ProcessLossQty' | abpLocalization }}</th>
                  <th class="text-end text-warning">{{ '::TotalProcessLossValue' | abpLocalization }}</th>
                  <th class="text-end">{{ '::TotalFgValue' | abpLocalization }}</th>
                  <th class="text-end">{{ '::TotalRmValue' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @if (!d.rows || d.rows.length === 0) {
                  <tr>
                    <td colspan="9" class="text-center text-muted py-4">
                      {{ '::NoDataAvailable' | abpLocalization }}
                    </td>
                  </tr>
                }
                @for (row of d.rows; track row.workOrderId) {
                  <tr>
                    <td class="fw-bold">{{ row.workOrderNumber }}</td>
                    <td>
                      <span class="badge bg-light text-dark border">{{ row.status }}</span>
                    </td>
                    <td>
                      <div><strong>{{ row.itemCode }}</strong></div>
                      @if (row.itemName && row.itemName !== row.itemCode) {
                        <div class="small text-muted">{{ row.itemName }}</div>
                      }
                    </td>
                    <td class="text-end">{{ row.qtyToManufacture | number:'1.2-2' }}</td>
                    <td class="text-end fw-bold text-success">{{ row.producedQty | number:'1.2-2' }}</td>
                    <td class="text-end fw-bold text-danger">{{ row.processLossQty | number:'1.2-2' }}</td>
                    <td class="text-end text-warning">{{ row.totalProcessLossValue | number:'1.2-2' }}</td>
                    <td class="text-end">{{ row.totalFgValue | number:'1.2-2' }}</td>
                    <td class="text-end">{{ row.totalRmValue | number:'1.2-2' }}</td>
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
export class ProcessLossReportComponent implements OnInit {
  private service = inject(ProcessLossReportService);
  private companyContext = inject(CompanyContextService);

  fromDate = '';
  toDate = '';

  isLoading = signal(false);
  data = signal<ProcessLossReportDto | null>(null);

  ngOnInit(): void {
    const today = new Date();
    const firstDay = new Date(today.getFullYear(), today.getMonth(), 1);

    this.fromDate = firstDay.toISOString().substring(0, 10);
    this.toDate = today.toISOString().substring(0, 10);

    this.loadData();
  }

  loadData(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.service
      .getReport({
        companyId,
        fromDate: this.fromDate,
        toDate: this.toDate,
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
    if (!d || !d.rows || !d.rows.length) return;

    exportToCsv('process_loss_report.csv', d.rows, [
      'workOrderNumber',
      'status',
      'itemCode',
      'itemName',
      'qtyToManufacture',
      'producedQty',
      'processLossQty',
      'totalProcessLossValue',
      'totalFgValue',
      'totalRmValue',
    ]);
  }
}
