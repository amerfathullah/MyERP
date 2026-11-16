import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { SalesPartnerTargetVarianceService } from '../../../proxy/sales/sales-partner-target-variance.service';
import type {
  SalesPartnerDto,
  SalesPartnerTargetVarianceReportDto,
  SalesPartnerTargetVarianceRowDto,
} from '../../../proxy/sales/models';
import { SalesPartnerService } from '../../../proxy/sales/sales-partner.service';
import { FiscalYearService } from '../../../proxy/accounting/fiscal-year.service';
import type { FiscalYearDto } from '../../../proxy/accounting/models';
import { ItemGroupService } from '../../../proxy/inventory/item-group.service';
import type { ItemGroupDto } from '../../../proxy/inventory/models';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function filterSalesPartnerTargetVarianceRows(
  rows: SalesPartnerTargetVarianceRowDto[] | undefined,
  searchTerm: string,
): SalesPartnerTargetVarianceRowDto[] {
  if (!rows || rows.length === 0) {
    return [];
  }
  const term = searchTerm.trim().toLowerCase();
  if (!term) {
    return rows;
  }
  return rows.filter(r =>
    [r.salesPartnerName, r.itemGroupName].some(v => (v ?? '').toLowerCase().includes(term)),
  );
}

export function clampAchievementProgress(percentage: number | undefined): number {
  return Math.min(Math.max(percentage ?? 0, 0), 100);
}

@Component({
  selector: 'app-sales-partner-target-variance',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fas fa-handshake text-primary me-2"></i>
            {{ '::SalesPartnerTargetVariance' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::SalesPartnerTargetVarianceDescription' | abpLocalization }}
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
              <label class="form-label small mb-1">{{ '::DocumentType' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedDocType"
                (ngModelChange)="loadReport()"
              >
                <option value="Sales Invoice">{{ '::SalesInvoice' | abpLocalization }}</option>
                <option value="Sales Order">{{ '::SalesOrder' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::TargetOn' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedTargetOn"
                (ngModelChange)="loadReport()"
              >
                <option value="Amount">{{ '::Amount' | abpLocalization }}</option>
                <option value="Quantity">{{ '::Quantity' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::FiscalYear' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedFiscalYearId"
                (ngModelChange)="onFiscalYearChange()"
              >
                <option value="">-- {{ '::AllFiscalYears' | abpLocalization }} --</option>
                @for (fy of fiscalYears(); track fy.id) {
                  <option [value]="fy.id">{{ fy.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::SalesPartner' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedSalesPartnerId"
                (ngModelChange)="loadReport()"
              >
                <option value="">-- {{ '::AllSalesPartners' | abpLocalization }} --</option>
                @for (sp of salesPartners(); track sp.id) {
                  <option [value]="sp.id">{{ sp.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::ItemGroup' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedItemGroupId"
                (ngModelChange)="loadReport()"
              >
                <option value="">-- {{ '::AllItemGroups' | abpLocalization }} --</option>
                @for (ig of itemGroups(); track ig.id) {
                  <option [value]="ig.id">{{ ig.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-2 d-flex align-items-end">
              <button
                class="btn btn-sm btn-primary w-100"
                (click)="loadReport()"
                [disabled]="loading() || !companyId"
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
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::FromDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="fromDate"
                (ngModelChange)="loadReport()"
              />
            </div>
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::ToDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="toDate"
                (ngModelChange)="loadReport()"
              />
            </div>
            <div class="col-md-4">
              <label class="form-label small mb-1">{{ '::Search' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                [placeholder]="'::SalesPartner' | abpLocalization"
                [ngModel]="searchTerm()"
                (ngModelChange)="searchTerm.set($event)"
              />
            </div>
          </div>
        </div>
      </div>

      <!-- Summary Cards -->
      @if (report()) {
        <div class="row g-2 mb-3">
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-primary-subtle text-primary py-2 px-3">
              <div class="small fw-bold">{{ '::TotalTarget' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ (report()!.totalTarget ?? 0) | number: '1.2-2' }}
                @if (selectedTargetOn === 'Amount') {
                  {{ report()!.currency }}
                }
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-info-subtle text-info-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::TotalAchieved' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ (report()!.totalAchieved ?? 0) | number: '1.2-2' }}
                @if (selectedTargetOn === 'Amount') {
                  {{ report()!.currency }}
                }
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div
              class="card border-0 shadow-sm py-2 px-3"
              [class.bg-success-subtle]="(report()!.totalVariance ?? 0) >= 0"
              [class.text-success]="(report()!.totalVariance ?? 0) >= 0"
              [class.bg-danger-subtle]="(report()!.totalVariance ?? 0) < 0"
              [class.text-danger]="(report()!.totalVariance ?? 0) < 0"
            >
              <div class="small fw-bold">{{ '::TotalVariance' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ (report()!.totalVariance ?? 0) >= 0 ? '+' : '' }}{{ (report()!.totalVariance ?? 0) | number: '1.2-2' }}
                @if (selectedTargetOn === 'Amount') {
                  {{ report()!.currency }}
                }
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-secondary-subtle text-secondary-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::OverallAchievement' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ (report()!.overallAchievementPercentage ?? 0) | number: '1.1-2' }}%
              </div>
              <div class="progress mt-1" style="height: 4px;">
                <div
                  class="progress-bar"
                  [class.bg-success]="(report()!.overallAchievementPercentage ?? 0) >= 100"
                  [class.bg-primary]="(report()!.overallAchievementPercentage ?? 0) >= 50 && (report()!.overallAchievementPercentage ?? 0) < 100"
                  [class.bg-warning]="(report()!.overallAchievementPercentage ?? 0) < 50"
                  [style.width.%]="getSafeProgress(report()!.overallAchievementPercentage)"
                ></div>
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Table -->
      <div class="card shadow-sm border-0">
        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5">
              <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Loading...</span>
              </div>
            </div>
          } @else if (filteredRows().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fas fa-handshake fa-3x mb-3 text-secondary"></i>
              <p class="mb-0">{{ '::NoRecordsFound' | abpLocalization }}</p>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-hover table-striped align-middle mb-0" style="font-size: 0.85rem;">
                <thead class="table-light">
                  <tr>
                    <th style="min-width: 170px;">{{ '::SalesPartner' | abpLocalization }}</th>
                    <th style="min-width: 150px;">{{ '::ItemGroup' | abpLocalization }}</th>
                    <th class="text-end" style="min-width: 120px;">
                      {{ '::Target' | abpLocalization }}
                      @if (selectedTargetOn === 'Amount') {
                        ({{ report()!.currency }})
                      }
                    </th>
                    <th class="text-end" style="min-width: 120px;">
                      {{ '::Achieved' | abpLocalization }}
                      @if (selectedTargetOn === 'Amount') {
                        ({{ report()!.currency }})
                      }
                    </th>
                    <th class="text-end" style="min-width: 120px;">{{ '::Variance' | abpLocalization }}</th>
                    <th style="min-width: 160px;">{{ '::Achievement' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of filteredRows(); track (row.salesPartnerId ?? '') + '-' + (row.itemGroupId ?? 'all')) {
                    <tr>
                      <td class="fw-semibold">{{ row.salesPartnerName }}</td>
                      <td>
                        <span class="badge bg-light text-dark border">
                          {{ row.itemGroupName }}
                        </span>
                      </td>
                      <td class="text-end fw-semibold">{{ (row.target ?? 0) | number: '1.2-2' }}</td>
                      <td class="text-end fw-bold text-primary">{{ (row.achieved ?? 0) | number: '1.2-2' }}</td>
                      <td
                        class="text-end fw-bold"
                        [class.text-success]="(row.variance ?? 0) >= 0"
                        [class.text-danger]="(row.variance ?? 0) < 0"
                      >
                        {{ (row.variance ?? 0) >= 0 ? '+' : '' }}{{ (row.variance ?? 0) | number: '1.2-2' }}
                      </td>
                      <td>
                        <div class="d-flex align-items-center">
                          <div class="progress flex-grow-1 me-2" style="height: 6px;">
                            <div
                              class="progress-bar"
                              [class.bg-success]="(row.achievementPercentage ?? 0) >= 100"
                              [class.bg-primary]="(row.achievementPercentage ?? 0) >= 50 && (row.achievementPercentage ?? 0) < 100"
                              [class.bg-warning]="(row.achievementPercentage ?? 0) < 50"
                              [style.width.%]="getSafeProgress(row.achievementPercentage)"
                            ></div>
                          </div>
                          <span class="small fw-semibold" style="min-width: 48px;">
                            {{ (row.achievementPercentage ?? 0) | number: '1.1-1' }}%
                          </span>
                        </div>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      </div>
    </div>
  `,
})
export class SalesPartnerTargetVarianceComponent implements OnInit {
  private reportService = inject(SalesPartnerTargetVarianceService);
  private salesPartnerService = inject(SalesPartnerService);
  private fiscalYearService = inject(FiscalYearService);
  private itemGroupService = inject(ItemGroupService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);

  report = signal<SalesPartnerTargetVarianceReportDto | null>(null);
  salesPartners = signal<SalesPartnerDto[]>([]);
  fiscalYears = signal<FiscalYearDto[]>([]);
  itemGroups = signal<ItemGroupDto[]>([]);
  loading = signal(false);
  searchTerm = signal('');

  companyId = '';
  selectedDocType = 'Sales Invoice';
  selectedTargetOn = 'Amount';
  selectedFiscalYearId = '';
  selectedSalesPartnerId = '';
  selectedItemGroupId = '';
  fromDate = '';
  toDate = '';

  filteredRows = computed(() =>
    filterSalesPartnerTargetVarianceRows(this.report()?.rows, this.searchTerm()),
  );

  ngOnInit(): void {
    this.companyContext.load();
    const now = new Date();
    const firstDay = new Date(now.getFullYear(), 0, 1);
    const lastDay = new Date(now.getFullYear(), 11, 31);
    this.fromDate = firstDay.toISOString().substring(0, 10);
    this.toDate = lastDay.toISOString().substring(0, 10);

    this.loadLookups();
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => {
        this.companyId = this.companyContext.currentCompanyId() || res.items?.[0]?.id || '';
        if (this.companyId) {
          this.loadReport();
        }
      },
    });
  }

  loadLookups(): void {
    this.salesPartnerService.getList({ maxResultCount: 200 }).subscribe({
      next: res => this.salesPartners.set(res.items ?? []),
      error: () => this.salesPartners.set([]),
    });

    this.fiscalYearService.getList({ maxResultCount: 50 }).subscribe({
      next: res => {
        const items = res.items ?? [];
        this.fiscalYears.set(items);
        if (items.length > 0 && !this.selectedFiscalYearId) {
          const currentYear = new Date().getFullYear().toString();
          const match = items.find(fy => fy.name?.includes(currentYear));
          if (match) {
            this.selectedFiscalYearId = match.id ?? '';
            this.fromDate = match.startDate ? match.startDate.substring(0, 10) : this.fromDate;
            this.toDate = match.endDate ? match.endDate.substring(0, 10) : this.toDate;
          }
        }
      },
      error: () => this.fiscalYears.set([]),
    });

    this.itemGroupService.getList({ maxResultCount: 200 }).subscribe({
      next: res => this.itemGroups.set(res.items ?? []),
      error: () => this.itemGroups.set([]),
    });
  }

  onFiscalYearChange(): void {
    if (this.selectedFiscalYearId) {
      const match = this.fiscalYears().find(fy => fy.id === this.selectedFiscalYearId);
      if (match) {
        this.fromDate = match.startDate ? match.startDate.substring(0, 10) : this.fromDate;
        this.toDate = match.endDate ? match.endDate.substring(0, 10) : this.toDate;
      }
    }
    this.loadReport();
  }

  loadReport(): void {
    if (!this.companyId) return;

    this.loading.set(true);

    this.reportService
      .getReport({
        companyId: this.companyId,
        docType: this.selectedDocType,
        targetOn: this.selectedTargetOn,
        fiscalYearId: this.selectedFiscalYearId || null,
        fromDate: this.fromDate ? `${this.fromDate}T00:00:00Z` : null,
        toDate: this.toDate ? `${this.toDate}T23:59:59Z` : null,
        salesPartnerId: this.selectedSalesPartnerId || null,
        itemGroupId: this.selectedItemGroupId || null,
      })
      .subscribe({
        next: data => {
          this.report.set(data);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }

  getSafeProgress(percentage: number | undefined): number {
    return clampAchievementProgress(percentage);
  }

  exportCsv(): void {
    const data = this.report();
    const rows = this.filteredRows();
    if (!data || rows.length === 0) return;

    const targetCol = `Target (${data.targetOn})`;
    const achievedCol = `Achieved (${data.targetOn})`;
    const headers = [
      'Sales Partner',
      'Item Group',
      targetCol,
      achievedCol,
      'Variance',
      'Achievement (%)',
    ];

    const dataRows = rows.map(r => ({
      'Sales Partner': r.salesPartnerName ?? '',
      'Item Group': r.itemGroupName ?? '',
      [targetCol]: (r.target ?? 0).toFixed(2),
      [achievedCol]: (r.achieved ?? 0).toFixed(2),
      Variance: (r.variance ?? 0).toFixed(2),
      'Achievement (%)': `${(r.achievementPercentage ?? 0).toFixed(2)}%`,
    }));

    exportToCsv(
      `sales_partner_target_variance_${new Date().toISOString().substring(0, 10)}.csv`,
      dataRows,
      headers,
    );
  }
}
