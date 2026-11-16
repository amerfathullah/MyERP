import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { SalesPipelineService } from '../../../proxy/crm/sales-pipeline.service';
import type {
  LostOpportunityReportDto,
  LostOpportunityRowDto,
} from '../../../proxy/crm/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function filterLostOpportunityRows(
  rows: LostOpportunityRowDto[] | undefined,
  searchTerm: string,
): LostOpportunityRowDto[] {
  if (!rows || rows.length === 0) {
    return [];
  }
  const term = searchTerm.trim().toLowerCase();
  if (!term) {
    return rows;
  }
  return rows.filter(r =>
    [
      r.opportunityNumber,
      r.title,
      r.customerName,
      r.lostReasons,
      r.territory,
      r.salesStage,
    ].some(v => (v ?? '').toLowerCase().includes(term)),
  );
}

export function sumLostOpportunityAmount(rows: LostOpportunityRowDto[]): number {
  return rows.reduce((acc, r) => acc + (r.opportunityAmount ?? 0), 0);
}

@Component({
  selector: 'app-lost-opportunity',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fas fa-times-circle text-danger me-2"></i>
            {{ '::LostOpportunityReport' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::LostOpportunityReportDescription' | abpLocalization }}
          </p>
        </div>
        @if (filteredRows().length > 0) {
          <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fas fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <!-- Filters -->
      <div class="card mb-3 shadow-sm border-0">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::FromDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="fromDate"
                (change)="loadReport()"
              />
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::ToDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="toDate"
                (change)="loadReport()"
              />
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::OpportunityFrom' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="opportunityFrom"
                (ngModelChange)="loadReport()"
              >
                <option value="">{{ '::All' | abpLocalization }}</option>
                <option value="Lead">{{ '::Lead' | abpLocalization }}</option>
                <option value="Customer">{{ '::Customer' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::LostReasons' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                [(ngModel)]="lostReason"
                (keyup.enter)="loadReport()"
                (blur)="loadReport()"
              />
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::Search' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                [placeholder]="'::OpportunityNumber' | abpLocalization"
                [ngModel]="searchTerm()"
                (ngModelChange)="searchTerm.set($event)"
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

      <!-- KPI Summary -->
      @if (report()) {
        <div class="row g-2 mb-3">
          <div class="col-md-4">
            <div class="card border-0 shadow-sm bg-danger-subtle text-danger py-2 px-3">
              <div class="small fw-bold">{{ '::LostOpportunityReport' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ filteredRows().length }}</div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card border-0 shadow-sm bg-warning-subtle text-warning-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::OpportunityAmount' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ totalLostAmount() | number: '1.2-2' }}</div>
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
                <th>{{ '::OpportunityNumber' | abpLocalization }}</th>
                <th>{{ '::Title' | abpLocalization }}</th>
                <th>{{ '::OpportunityFrom' | abpLocalization }}</th>
                <th>{{ '::CustomerName' | abpLocalization }}</th>
                <th>{{ '::SalesStage' | abpLocalization }}</th>
                <th>{{ '::Territory' | abpLocalization }}</th>
                <th>{{ '::LostReasons' | abpLocalization }}</th>
                <th class="text-end">{{ '::OpportunityAmount' | abpLocalization }}</th>
                <th>{{ '::CreatedDate' | abpLocalization }}</th>
              </tr>
            </thead>
            <tbody>
              @if (loading()) {
                <tr>
                  <td colspan="9" class="text-center py-4 text-muted">
                    <span class="spinner-border spinner-border-sm me-2"></span>
                    {{ '::Loading' | abpLocalization }}
                  </td>
                </tr>
              } @else if (filteredRows().length === 0) {
                <tr>
                  <td colspan="9" class="text-center py-4 text-muted">
                    {{ '::NoDataAvailable' | abpLocalization }}
                  </td>
                </tr>
              } @else {
                @for (row of filteredRows(); track row.id) {
                  <tr>
                    <td class="fw-semibold">{{ row.opportunityNumber }}</td>
                    <td>{{ row.title }}</td>
                    <td>{{ row.opportunityFrom }}</td>
                    <td>{{ row.customerName || row.partyName || '-' }}</td>
                    <td>{{ row.salesStage || '-' }}</td>
                    <td>{{ row.territory || '-' }}</td>
                    <td>{{ row.lostReasons || '-' }}</td>
                    <td class="text-end fw-semibold">{{ (row.opportunityAmount ?? 0) | number: '1.2-2' }}</td>
                    <td>{{ row.creationTime | date: 'yyyy-MM-dd' }}</td>
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
export class LostOpportunityComponent implements OnInit {
  private pipelineService = inject(SalesPipelineService);
  private companyContext = inject(CompanyContextService);

  report = signal<LostOpportunityReportDto | null>(null);
  loading = signal(false);
  searchTerm = signal('');

  fromDate = '';
  toDate = '';
  opportunityFrom = '';
  lostReason = '';

  filteredRows = computed(() =>
    filterLostOpportunityRows(this.report()?.data, this.searchTerm()),
  );

  totalLostAmount = computed(() => sumLostOpportunityAmount(this.filteredRows()));

  ngOnInit(): void {
    this.companyContext.load();
    const now = new Date();
    const yearStart = new Date(now.getFullYear(), 0, 1);
    this.fromDate = yearStart.toISOString().slice(0, 10);
    this.toDate = now.toISOString().slice(0, 10);
    this.loadReport();
  }

  loadReport(): void {
    this.loading.set(true);
    const companyId = this.companyContext.currentCompanyId() || null;

    this.pipelineService
      .getLostOpportunityReport({
        companyId,
        fromDate: this.fromDate || null,
        toDate: this.toDate || null,
        opportunityFrom: this.opportunityFrom || null,
        lostReason: this.lostReason.trim() || null,
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
    exportToCsv('lost-opportunity-report.csv', this.filteredRows(), [
      'opportunityNumber',
      'title',
      'opportunityFrom',
      'customerName',
      'salesStage',
      'territory',
      'lostReasons',
      'opportunityAmount',
      'creationTime',
    ]);
  }
}
