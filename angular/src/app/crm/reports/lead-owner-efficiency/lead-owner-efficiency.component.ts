import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { CampaignEfficiencyService } from '../../../proxy/crm/campaign-efficiency.service';
import type {
  CampaignEfficiencyReportDto,
  CampaignEfficiencyRowDto,
} from '../../../proxy/crm/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function filterLeadOwnerRows(
  rows: CampaignEfficiencyRowDto[] | undefined,
  searchTerm: string,
): CampaignEfficiencyRowDto[] {
  if (!rows || rows.length === 0) {
    return [];
  }
  const term = searchTerm.trim().toLowerCase();
  if (!term) {
    return rows;
  }
  return rows.filter(r => (r.campaign ?? '').toLowerCase().includes(term));
}

@Component({
  selector: 'app-lead-owner-efficiency',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fas fa-user-check text-primary me-2"></i>
            {{ '::LeadOwnerEfficiency' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::LeadOwnerEfficiencyDescription' | abpLocalization }}
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
            <div class="col-md-4">
              <label class="form-label small mb-1">{{ '::Search' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                [placeholder]="'::LeadOwner' | abpLocalization"
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
          <div class="col-md">
            <div class="card border-0 shadow-sm bg-primary-subtle text-primary py-2 px-3">
              <div class="small fw-bold">{{ '::TotalLeads' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ report()!.totalLeads ?? 0 }}</div>
            </div>
          </div>
          <div class="col-md">
            <div class="card border-0 shadow-sm bg-info-subtle text-info-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::TotalOpportunities' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ report()!.totalOpps ?? 0 }}</div>
            </div>
          </div>
          <div class="col-md">
            <div class="card border-0 shadow-sm bg-warning-subtle text-warning-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::TotalQuotations' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ report()!.totalQuots ?? 0 }}</div>
            </div>
          </div>
          <div class="col-md">
            <div class="card border-0 shadow-sm bg-success-subtle text-success py-2 px-3">
              <div class="small fw-bold">{{ '::TotalOrders' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ report()!.totalOrders ?? 0 }}</div>
            </div>
          </div>
          <div class="col-md">
            <div class="card border-0 shadow-sm bg-secondary-subtle text-secondary-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::TotalOrderValue' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ (report()!.totalOrderValue ?? 0) | number: '1.2-2' }}</div>
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
                <th>{{ '::LeadOwner' | abpLocalization }}</th>
                <th class="text-end">{{ '::LeadCount' | abpLocalization }}</th>
                <th class="text-end">{{ '::OpportunityCount' | abpLocalization }}</th>
                <th class="text-end">{{ '::Quotations' | abpLocalization }}</th>
                <th class="text-end">{{ '::Orders' | abpLocalization }}</th>
                <th class="text-end">{{ '::OrderValue' | abpLocalization }}</th>
                <th class="text-end">{{ '::OppLeadRate' | abpLocalization }}</th>
                <th class="text-end">{{ '::QuotLeadRate' | abpLocalization }}</th>
                <th class="text-end">{{ '::OrderQuotRate' | abpLocalization }}</th>
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
                @for (row of filteredRows(); track row.campaign) {
                  <tr>
                    <td class="fw-semibold">{{ row.campaign }}</td>
                    <td class="text-end">{{ row.leadCount ?? 0 }}</td>
                    <td class="text-end">{{ row.oppCount ?? 0 }}</td>
                    <td class="text-end">{{ row.quotCount ?? 0 }}</td>
                    <td class="text-end">{{ row.orderCount ?? 0 }}</td>
                    <td class="text-end">{{ (row.orderValue ?? 0) | number: '1.2-2' }}</td>
                    <td class="text-end">{{ (row.oppLeadRate ?? 0) | number: '1.2-2' }}%</td>
                    <td class="text-end">{{ (row.quotLeadRate ?? 0) | number: '1.2-2' }}%</td>
                    <td class="text-end">{{ (row.orderQuotRate ?? 0) | number: '1.2-2' }}%</td>
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
export class LeadOwnerEfficiencyComponent implements OnInit {
  private campaignEfficiencyService = inject(CampaignEfficiencyService);
  private companyContext = inject(CompanyContextService);

  report = signal<CampaignEfficiencyReportDto | null>(null);
  loading = signal(false);
  searchTerm = signal('');

  fromDate = '';
  toDate = '';

  filteredRows = computed(() =>
    filterLeadOwnerRows(this.report()?.rows, this.searchTerm()),
  );

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

    this.campaignEfficiencyService
      .getReport({
        companyId,
        basedOn: 'LeadOwner',
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
    exportToCsv('lead-owner-efficiency.csv', this.filteredRows(), [
      'campaign',
      'leadCount',
      'oppCount',
      'quotCount',
      'orderCount',
      'orderValue',
      'oppLeadRate',
      'quotLeadRate',
      'orderQuotRate',
    ]);
  }
}
