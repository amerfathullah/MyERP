import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { SalesPipelineService } from '../../../proxy/crm/sales-pipeline.service';
import type {
  OpportunitySummaryBySalesStageDto,
  OpportunitySummaryRowDto,
} from '../../../proxy/crm/models';
import { CompanyService } from '../../../proxy/core/company.service';
import type { CompanyDto } from '../../../proxy/core/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function filterOpportunitySummaryRows(
  rows: OpportunitySummaryRowDto[] | undefined,
  searchTerm: string,
): OpportunitySummaryRowDto[] {
  if (!rows || rows.length === 0) {
    return [];
  }
  const term = searchTerm.trim().toLowerCase();
  if (!term) {
    return rows;
  }
  return rows.filter(r => (r.key ?? '').toLowerCase().includes(term));
}

export function sumSummaryStageValues(row: OpportunitySummaryRowDto, stageKeys: string[]): number {
  const map = row.stageValues ?? {};
  return stageKeys.reduce((acc, k) => acc + (map[k] ?? 0), 0);
}

@Component({
  selector: 'app-opportunity-summary',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fas fa-layer-group text-primary me-2"></i>
            {{ '::OpportunitySummary' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::OpportunitySummaryDescription' | abpLocalization }}
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
              <label class="form-label small mb-1">{{ '::Company' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="companyId"
                (ngModelChange)="loadReport()"
              >
                @for (company of companies(); track company.id) {
                  <option [value]="company.id">{{ company.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::BasedOn' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="basedOn"
                (ngModelChange)="loadReport()"
              >
                <option value="Opportunity Type">{{ '::OpportunityType' | abpLocalization }}</option>
                <option value="Opportunity Owner">{{ '::OpportunityOwner' | abpLocalization }}</option>
                <option value="Source">{{ '::Source' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::DataBasedOn' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="dataBasedOn"
                (ngModelChange)="loadReport()"
              >
                <option value="Number">{{ '::Count' | abpLocalization }}</option>
                <option value="Amount">{{ '::Amount' | abpLocalization }}</option>
              </select>
            </div>
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
          <div class="row g-2 mt-1">
            <div class="col-md-4">
              <label class="form-label small mb-1">{{ '::Search' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                [placeholder]="basedOn"
                [ngModel]="searchTerm()"
                (ngModelChange)="searchTerm.set($event)"
              />
            </div>
          </div>
        </div>
      </div>

      <!-- Matrix Table -->
      <div class="card border-0 shadow-sm">
        <div class="table-responsive">
          <table class="table table-hover table-sm align-middle mb-0">
            <thead class="table-light">
              <tr>
                @for (col of report()?.columns ?? []; track col.fieldname; let i = $index) {
                  <th [class.text-end]="i > 0">{{ col.label }}</th>
                }
                @if ((report()?.columns?.length ?? 0) > 1) {
                  <th class="text-end">{{ '::Total' | abpLocalization }}</th>
                }
              </tr>
            </thead>
            <tbody>
              @if (loading()) {
                <tr>
                  <td [attr.colspan]="stageColumns().length + 2" class="text-center py-4 text-muted">
                    <span class="spinner-border spinner-border-sm me-2"></span>
                    {{ '::Loading' | abpLocalization }}
                  </td>
                </tr>
              } @else if (filteredRows().length === 0) {
                <tr>
                  <td [attr.colspan]="stageColumns().length + 2" class="text-center py-4 text-muted">
                    {{ '::NoDataAvailable' | abpLocalization }}
                  </td>
                </tr>
              } @else {
                @for (row of filteredRows(); track row.key) {
                  <tr>
                    <td class="fw-semibold">{{ row.key }}</td>
                    @for (col of stageColumns(); track col.fieldname) {
                      <td class="text-end">
                        @if (dataBasedOn === 'Amount') {
                          {{ (row.stageValues?.[col.fieldname!] ?? 0) | number: '1.2-2' }}
                        } @else {
                          {{ row.stageValues?.[col.fieldname!] ?? 0 }}
                        }
                      </td>
                    }
                    <td class="text-end fw-bold">
                      @if (dataBasedOn === 'Amount') {
                        {{ rowTotal(row) | number: '1.2-2' }}
                      } @else {
                        {{ rowTotal(row) }}
                      }
                    </td>
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
export class OpportunitySummaryComponent implements OnInit {
  private pipelineService = inject(SalesPipelineService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);

  companies = signal<CompanyDto[]>([]);
  report = signal<OpportunitySummaryBySalesStageDto | null>(null);
  loading = signal(false);
  searchTerm = signal('');

  companyId = '';
  basedOn = 'Opportunity Type';
  dataBasedOn = 'Number';
  fromDate = '';
  toDate = '';

  stageColumns = computed(() => (this.report()?.columns ?? []).slice(1));
  filteredRows = computed(() =>
    filterOpportunitySummaryRows(this.report()?.data, this.searchTerm()),
  );

  ngOnInit(): void {
    this.companyContext.load();
    const now = new Date();
    const yearStart = new Date(now.getFullYear(), 0, 1);
    this.fromDate = yearStart.toISOString().slice(0, 10);
    this.toDate = now.toISOString().slice(0, 10);

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => {
        const items = res.items ?? [];
        this.companies.set(items);
        this.companyId = this.companyContext.currentCompanyId() || items[0]?.id || '';
        this.loadReport();
      },
      error: () => this.loadReport(),
    });
  }

  loadReport(): void {
    this.loading.set(true);
    this.pipelineService
      .getOpportunitySummaryBySalesStage({
        companyId: this.companyId || null,
        basedOn: this.basedOn,
        dataBasedOn: this.dataBasedOn,
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

  rowTotal(row: OpportunitySummaryRowDto): number {
    const keys = this.stageColumns()
      .map(c => c.fieldname)
      .filter((k): k is string => Boolean(k));
    return sumSummaryStageValues(row, keys);
  }

  exportCsv(): void {
    const stages = this.stageColumns();
    const headers = [this.basedOn, ...stages.map(c => c.label ?? c.fieldname ?? ''), 'Total'];
    const exportRows = this.filteredRows().map(row => {
      const entry: Record<string, string | number> = { [this.basedOn]: row.key ?? '' };
      for (const col of stages) {
        const label = col.label ?? col.fieldname ?? '';
        entry[label] = row.stageValues?.[col.fieldname!] ?? 0;
      }
      entry['Total'] = this.rowTotal(row);
      return entry;
    });
    exportToCsv('opportunity-summary-by-sales-stage.csv', exportRows, headers);
  }
}
