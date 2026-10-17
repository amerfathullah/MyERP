import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { QualityInspectionSummaryService } from '../../proxy/inventory/quality-inspection-summary.service';
import type { QualityInspectionSummaryReportDto } from '../../proxy/inventory/models';
import { InspectionStatus, inspectionStatusOptions } from '../../proxy/inventory/inspection-status.enum';
import { InspectionType, inspectionTypeOptions } from '../../proxy/inventory/inspection-type.enum';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-quality-inspection-summary',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-clipboard-check me-2 text-primary"></i>{{ '::QualityInspectionSummary' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::QualityInspectionSummaryDescription' | abpLocalization }}</small>
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
          <div class="col-md-2">
            <label class="form-label small text-muted">{{ '::Status' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedStatus">
              <option [ngValue]="null">-- {{ '::All' | abpLocalization }} --</option>
              @for (opt of statusOptions; track opt.value) {
                <option [ngValue]="opt.value">{{ opt.key }}</option>
              }
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label small text-muted">{{ '::InspectionType' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedType">
              <option [ngValue]="null">-- {{ '::All' | abpLocalization }} --</option>
              @for (opt of typeOptions; track opt.value) {
                <option [ngValue]="opt.value">{{ opt.key }}</option>
              }
            </select>
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
                  <div class="text-muted small">{{ '::TotalInspections' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-primary">{{ d.totalInspections }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-success text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">{{ '::AcceptedCount' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-success">{{ d.acceptedCount }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-danger text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">{{ '::RejectedCount' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-danger">{{ d.rejectedCount }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-info text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">{{ '::AcceptanceRate' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-info">{{ d.acceptanceRate | number:'1.1-1' }}%</div>
                </div>
              </div>
            </div>
          </div>

          <!-- Breakdown summaries -->
          <div class="row g-3 mb-4">
            <div class="col-md-5">
              <div class="card border h-100">
                <div class="card-header bg-light py-2">
                  <strong class="small text-muted">{{ '::Status' | abpLocalization }} {{ '::Breakdown' | abpLocalization }}</strong>
                </div>
                <div class="card-body p-2">
                  <ul class="list-group list-group-flush">
                    @for (s of d.statusSummaries; track s.status) {
                      <li class="list-group-item d-flex justify-content-between align-items-center py-2 px-1">
                        <div>
                          <span
                            class="badge me-2"
                            [class.bg-success]="s.status === 'Accepted'"
                            [class.bg-danger]="s.status === 'Rejected'"
                            [class.bg-secondary]="s.status !== 'Accepted' && s.status !== 'Rejected'"
                          >
                            {{ s.status }}
                          </span>
                          <span class="small text-muted">({{ s.percentage }}%)</span>
                        </div>
                        <span class="fw-bold">{{ s.count }}</span>
                      </li>
                    }
                  </ul>
                </div>
              </div>
            </div>

            <div class="col-md-7">
              <div class="card border h-100">
                <div class="card-header bg-light py-2">
                  <strong class="small text-muted">{{ '::Item' | abpLocalization }} {{ '::Summary' | abpLocalization }}</strong>
                </div>
                <div class="card-body p-0">
                  <div class="table-responsive" style="max-height: 200px;">
                    <table class="table table-sm table-hover mb-0">
                      <thead class="table-light sticky-top">
                        <tr>
                          <th>{{ '::Item' | abpLocalization }}</th>
                          <th class="text-center">{{ '::TotalInspections' | abpLocalization }}</th>
                          <th class="text-center">{{ '::Accepted' | abpLocalization }}</th>
                          <th class="text-center">{{ '::Rejected' | abpLocalization }}</th>
                        </tr>
                      </thead>
                      <tbody>
                        @for (item of d.itemSummaries; track item.itemId) {
                          <tr>
                            <td>
                              <strong>{{ item.itemCode }}</strong>
                              @if (item.itemName && item.itemName !== item.itemCode) {
                                <div class="small text-muted">{{ item.itemName }}</div>
                              }
                            </td>
                            <td class="text-center fw-bold">{{ item.totalInspected }}</td>
                            <td class="text-center text-success">{{ item.acceptedCount }}</td>
                            <td class="text-center text-danger">{{ item.rejectedCount }}</td>
                          </tr>
                        }
                      </tbody>
                    </table>
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
                  <th>{{ '::InspectionNumber' | abpLocalization }}</th>
                  <th>{{ '::InspectionDate' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th class="text-end">{{ '::SampleSize' | abpLocalization }}</th>
                  <th>{{ '::InspectionType' | abpLocalization }}</th>
                  <th>{{ '::Document' | abpLocalization }}</th>
                  <th>{{ '::InspectedBy' | abpLocalization }}</th>
                  <th>{{ '::Remarks' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @if (d.rows.length === 0) {
                  <tr>
                    <td colspan="9" class="text-center text-muted py-4">
                      {{ '::NoQualityInspectionsYet' | abpLocalization }}
                    </td>
                  </tr>
                }
                @for (row of d.rows; track row.id) {
                  <tr>
                    <td class="fw-bold">{{ row.inspectionNumber || row.id.substring(0, 8) }}</td>
                    <td>{{ row.inspectionDate | date:'yyyy-MM-dd' }}</td>
                    <td>
                      <span
                        class="badge"
                        [class.bg-success]="row.statusName === 'Accepted'"
                        [class.bg-danger]="row.statusName === 'Rejected'"
                        [class.bg-secondary]="row.statusName !== 'Accepted' && row.statusName !== 'Rejected'"
                      >
                        {{ row.statusName }}
                      </span>
                    </td>
                    <td>
                      <div><strong>{{ row.itemCode }}</strong></div>
                      @if (row.itemName && row.itemName !== row.itemCode) {
                        <div class="small text-muted">{{ row.itemName }}</div>
                      }
                    </td>
                    <td class="text-end">{{ row.sampleSize }}</td>
                    <td>{{ row.inspectionTypeName }}</td>
                    <td>
                      @if (row.referenceType) {
                        <span class="badge bg-light text-dark border">{{ row.referenceType }}</span>
                      } @else {
                        <span class="text-muted">-</span>
                      }
                    </td>
                    <td>{{ row.inspectedByName || '-' }}</td>
                    <td class="small text-muted text-truncate" style="max-width: 180px;">{{ row.remarks || '-' }}</td>
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
export class QualityInspectionSummaryComponent implements OnInit {
  private service = inject(QualityInspectionSummaryService);
  private companyContext = inject(CompanyContextService);

  statusOptions = inspectionStatusOptions;
  typeOptions = inspectionTypeOptions;

  fromDate = '';
  toDate = '';
  selectedStatus: number | null = null;
  selectedType: number | null = null;

  isLoading = signal(false);
  data = signal<QualityInspectionSummaryReportDto | null>(null);

  ngOnInit(): void {
    const today = new Date();
    const twelveMonthsAgo = new Date();
    twelveMonthsAgo.setMonth(twelveMonthsAgo.getMonth() - 12);

    this.fromDate = twelveMonthsAgo.toISOString().substring(0, 10);
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
        fromDate: this.fromDate || null,
        toDate: this.toDate || null,
        status: this.selectedStatus,
        inspectionType: this.selectedType,
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

    exportToCsv('quality_inspection_summary.csv', d.rows, [
      'inspectionNumber',
      'inspectionDate',
      'statusName',
      'itemCode',
      'itemName',
      'sampleSize',
      'inspectionTypeName',
      'referenceType',
      'inspectedByName',
      'remarks',
    ]);
  }
}
