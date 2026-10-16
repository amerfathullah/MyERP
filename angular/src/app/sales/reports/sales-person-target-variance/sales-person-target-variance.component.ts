import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { SalesPersonTargetVarianceService } from '../../../proxy/sales/sales-person-target-variance.service';
import type {
  SalesPersonTargetVarianceReportDto,
  SalesPersonTargetVarianceRowDto,
} from '../../../proxy/sales/models';
import { SalesPersonService } from '../../../proxy/sales/sales-person.service';
import type { SalesPersonDto } from '../../../proxy/sales/models';
import { FiscalYearService } from '../../../proxy/accounting/fiscal-year.service';
import type { FiscalYearDto } from '../../../proxy/accounting/models';
import { ItemGroupService } from '../../../proxy/inventory/item-group.service';
import type { ItemGroupDto } from '../../../proxy/inventory/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-sales-person-target-variance',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-bullseye text-primary me-2"></i>
            {{ '::SalesPersonTargetVariance' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::SalesPersonTargetVarianceDescription' | abpLocalization }}
          </p>
        </div>
        @if (report() && report()!.rows && report()!.rows.length > 0) {
          <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fa fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
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
              <label class="form-label small mb-1">{{ '::SalesPerson' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedSalesPersonId"
                (ngModelChange)="loadReport()"
              >
                <option value="">-- {{ '::AllSalesPersons' | abpLocalization }} --</option>
                @for (sp of salesPersons(); track sp.id) {
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
                [disabled]="loading()"
              >
                @if (loading()) {
                  <span class="spinner-border spinner-border-sm me-1"></span>
                } @else {
                  <i class="fa fa-sync me-1"></i>
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
          </div>
        </div>
      </div>

      <!-- Highlights Summary -->
      @if (report()) {
        <div class="row g-2 mb-3">
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-primary-subtle text-primary py-2 px-3">
              <div class="small fw-bold">{{ '::TotalTarget' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ report()!.totalTarget | number:'1.2-2' }}
                @if (selectedTargetOn === 'Amount') { {{ report()!.currency }} }
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-info-subtle text-info-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::TotalAchieved' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ report()!.totalAchieved | number:'1.2-2' }}
                @if (selectedTargetOn === 'Amount') { {{ report()!.currency }} }
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div
              class="card border-0 shadow-sm py-2 px-3"
              [class.bg-success-subtle]="report()!.totalVariance >= 0"
              [class.text-success]="report()!.totalVariance >= 0"
              [class.bg-danger-subtle]="report()!.totalVariance < 0"
              [class.text-danger]="report()!.totalVariance < 0"
            >
              <div class="small fw-bold">{{ '::TotalVariance' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ report()!.totalVariance >= 0 ? '+' : '' }}{{ report()!.totalVariance | number:'1.2-2' }}
                @if (selectedTargetOn === 'Amount') { {{ report()!.currency }} }
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-secondary-subtle text-secondary-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::OverallAchievement' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ report()!.overallAchievementPercentage | number:'1.1-2' }}%
              </div>
              <div class="progress mt-1" style="height: 4px;">
                <div
                  class="progress-bar"
                  [class.bg-success]="report()!.overallAchievementPercentage >= 100"
                  [class.bg-primary]="report()!.overallAchievementPercentage >= 50 && report()!.overallAchievementPercentage < 100"
                  [class.bg-warning]="report()!.overallAchievementPercentage < 50"
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
          } @else if (!report() || !report()!.rows || report()!.rows.length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-bullseye fa-3x mb-3 text-secondary"></i>
              <p class="mb-0">{{ '::NoRecordsFound' | abpLocalization }}</p>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-hover table-striped align-middle mb-0" style="font-size: 0.85rem;">
                <thead class="table-light">
                  <tr>
                    <th style="min-width: 170px;">{{ '::SalesPerson' | abpLocalization }}</th>
                    <th style="min-width: 150px;">{{ '::ItemGroup' | abpLocalization }}</th>
                    <th class="text-end" style="min-width: 120px;">
                      {{ '::Target' | abpLocalization }}
                      @if (selectedTargetOn === 'Amount') { ({{ report()!.currency }}) }
                    </th>
                    <th class="text-end" style="min-width: 120px;">
                      {{ '::Achieved' | abpLocalization }}
                      @if (selectedTargetOn === 'Amount') { ({{ report()!.currency }}) }
                    </th>
                    <th class="text-end" style="min-width: 120px;">{{ '::Variance' | abpLocalization }}</th>
                    <th style="min-width: 160px;">{{ '::Achievement' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of report()!.rows; track (row.salesPersonId + '-' + (row.itemGroupId || 'all'))) {
                    <tr>
                      <td class="fw-semibold">
                        <a [routerLink]="['/sales/sales-persons', row.salesPersonId]" class="text-decoration-none">
                          <i class="fa fa-user text-secondary me-1"></i>{{ row.salesPersonName }}
                        </a>
                      </td>
                      <td>
                        <span class="badge bg-light text-dark border">
                          {{ row.itemGroupName }}
                        </span>
                      </td>
                      <td class="text-end fw-semibold">{{ row.target | number:'1.2-2' }}</td>
                      <td class="text-end fw-bold text-primary">{{ row.achieved | number:'1.2-2' }}</td>
                      <td class="text-end fw-bold" [class.text-success]="row.variance >= 0" [class.text-danger]="row.variance < 0">
                        {{ row.variance >= 0 ? '+' : '' }}{{ row.variance | number:'1.2-2' }}
                      </td>
                      <td>
                        <div class="d-flex align-items-center">
                          <div class="progress flex-grow-1 me-2" style="height: 6px;">
                            <div
                              class="progress-bar"
                              [class.bg-success]="row.achievementPercentage >= 100"
                              [class.bg-primary]="row.achievementPercentage >= 50 && row.achievementPercentage < 100"
                              [class.bg-warning]="row.achievementPercentage < 50"
                              [style.width.%]="getSafeProgress(row.achievementPercentage)"
                            ></div>
                          </div>
                          <span class="small fw-semibold" style="min-width: 48px;">
                            {{ row.achievementPercentage | number:'1.1-1' }}%
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
export class SalesPersonTargetVarianceComponent implements OnInit {
  private reportService = inject(SalesPersonTargetVarianceService);
  private salesPersonService = inject(SalesPersonService);
  private fiscalYearService = inject(FiscalYearService);
  private itemGroupService = inject(ItemGroupService);
  private companyContext = inject(CompanyContextService);

  report = signal<SalesPersonTargetVarianceReportDto | null>(null);
  salesPersons = signal<SalesPersonDto[]>([]);
  fiscalYears = signal<FiscalYearDto[]>([]);
  itemGroups = signal<ItemGroupDto[]>([]);
  loading = signal(false);

  selectedDocType = 'Sales Invoice';
  selectedTargetOn = 'Amount';
  selectedFiscalYearId = '';
  selectedSalesPersonId = '';
  selectedItemGroupId = '';
  fromDate = '';
  toDate = '';

  ngOnInit(): void {
    const now = new Date();
    const firstDay = new Date(now.getFullYear(), 0, 1);
    const lastDay = new Date(now.getFullYear(), 11, 31);
    this.fromDate = firstDay.toISOString().substring(0, 10);
    this.toDate = lastDay.toISOString().substring(0, 10);

    this.loadLookups();
    this.loadReport();
  }

  loadLookups(): void {
    this.salesPersonService.getList({ maxResultCount: 200 }).subscribe({
      next: res => this.salesPersons.set(res.items || []),
      error: () => this.salesPersons.set([]),
    });

    this.fiscalYearService.getList({ maxResultCount: 50 }).subscribe({
      next: res => {
        const items = res.items || [];
        this.fiscalYears.set(items);
        if (items.length > 0 && !this.selectedFiscalYearId) {
          const currentYear = new Date().getFullYear().toString();
          const match = items.find(fy => fy.name.includes(currentYear));
          if (match) {
            this.selectedFiscalYearId = match.id;
            this.fromDate = match.startDate ? match.startDate.substring(0, 10) : this.fromDate;
            this.toDate = match.endDate ? match.endDate.substring(0, 10) : this.toDate;
          }
        }
      },
      error: () => this.fiscalYears.set([]),
    });

    this.itemGroupService.getList({ maxResultCount: 200 }).subscribe({
      next: res => this.itemGroups.set(res.items || []),
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
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.loading.set(true);

    this.reportService
      .getReport({
        companyId,
        docType: this.selectedDocType,
        targetOn: this.selectedTargetOn,
        fiscalYearId: this.selectedFiscalYearId || null,
        fromDate: this.fromDate ? `${this.fromDate}T00:00:00Z` : null,
        toDate: this.toDate ? `${this.toDate}T23:59:59Z` : null,
        salesPersonId: this.selectedSalesPersonId || null,
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

  getSafeProgress(percentage: number): number {
    return Math.min(Math.max(percentage, 0), 100);
  }

  exportCsv(): void {
    const data = this.report();
    if (!data || !data.rows || data.rows.length === 0) return;

    const headers = [
      'Sales Person',
      'Item Group',
      `Target (${data.targetOn})`,
      `Achieved (${data.targetOn})`,
      'Variance',
      'Achievement (%)',
    ];

    const dataRows = data.rows.map(r => ({
      'Sales Person': r.salesPersonName,
      'Item Group': r.itemGroupName,
      [`Target (${data.targetOn})`]: r.target.toFixed(2),
      [`Achieved (${data.targetOn})`]: r.achieved.toFixed(2),
      Variance: r.variance.toFixed(2),
      'Achievement (%)': `${r.achievementPercentage.toFixed(2)}%`,
    }));

    exportToCsv(
      `sales_person_target_variance_${new Date().toISOString().substring(0, 10)}.csv`,
      dataRows,
      headers
    );
  }
}
