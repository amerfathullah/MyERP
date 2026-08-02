import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { PurchaseAnalyticsService } from '../../../proxy/purchasing/purchase-analytics.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { CompanyCurrencyPipe } from '../../../shared/pipes/company-currency.pipe';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type { PurchaseAnalyticsReportDto, PurchaseAnalyticsRowDto } from '../../../proxy/purchasing/models';

@Component({
  selector: 'app-purchase-analytics',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe, CompanyCurrencyPipe],
  template: `
    <div class="container-fluid">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-chart-bar me-2"></i>{{ '::PurchaseAnalytics' | abpLocalization }}</h5>
          @if (report()) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="fas fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
        <div class="card-body">
          <!-- Filters -->
          <div class="row g-2 mb-3">
            <div class="col-md-2">
              <label class="form-label small">{{ '::GroupBy' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="groupBy" (ngModelChange)="loadReport()">
                <option [value]="0">{{ '::Supplier' | abpLocalization }}</option>
                <option [value]="1">{{ '::Item' | abpLocalization }}</option>
                <option [value]="4">{{ '::ItemGroup' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small">{{ '::Period' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="periodType" (ngModelChange)="loadReport()">
                <option [value]="0">{{ '::Monthly' | abpLocalization }}</option>
                <option [value]="1">{{ '::Quarterly' | abpLocalization }}</option>
                <option [value]="2">{{ '::Yearly' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small">{{ '::From' | abpLocalization }}</label>
              <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" (change)="loadReport()" />
            </div>
            <div class="col-md-2">
              <label class="form-label small">{{ '::To' | abpLocalization }}</label>
              <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" (change)="loadReport()" />
            </div>
            <div class="col-md-2">
              <label class="form-label small">{{ '::Value' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="valueField" (ngModelChange)="loadReport()">
                <option value="Amount">{{ '::Amount' | abpLocalization }}</option>
                <option value="Quantity">{{ '::Quantity' | abpLocalization }}</option>
              </select>
            </div>
          </div>

          @if (isLoading()) {
            <div class="text-center py-4"><div class="spinner-border text-primary"></div></div>
          }

          @if (report(); as r) {
            <!-- KPI Cards -->
            <div class="row mb-3">
              <div class="col-md-3">
                <div class="card border-start border-primary border-3">
                  <div class="card-body py-2 text-center">
                    <small class="text-muted">{{ '::GrandTotal' | abpLocalization }}</small>
                    <div class="fw-bold text-primary">{{ "" | companyCurrency }} {{ r.grandTotal | number:'1.2-2' }}</div>
                  </div>
                </div>
              </div>
              <div class="col-md-3">
                <div class="card border-start border-info border-3">
                  <div class="card-body py-2 text-center">
                    <small class="text-muted">{{ '::Suppliers' | abpLocalization }}</small>
                    <div class="fw-bold">{{ r.rows?.length ?? 0 }}</div>
                  </div>
                </div>
              </div>
              <div class="col-md-3">
                <div class="card border-start border-success border-3">
                  <div class="card-body py-2 text-center">
                    <small class="text-muted">{{ '::Periods' | abpLocalization }}</small>
                    <div class="fw-bold">{{ r.periodLabels?.length ?? 0 }}</div>
                  </div>
                </div>
              </div>
              <div class="col-md-3">
                <div class="card border-start border-warning border-3">
                  <div class="card-body py-2 text-center">
                    <small class="text-muted">{{ '::TopSupplierShare' | abpLocalization }}</small>
                    <div class="fw-bold">{{ getTopShare(r) | number:'1.0-0' }}%</div>
                  </div>
                </div>
              </div>
            </div>

            <!-- Data Table -->
            <div class="table-responsive">
              <table class="table table-sm table-hover">
                <thead class="table-light">
                  <tr>
                    <th>{{ getGroupLabel() | abpLocalization }}</th>
                    @for (label of r.periodLabels ?? []; track label) {
                      <th class="text-end">{{ label }}</th>
                    }
                    <th class="text-end fw-bold">{{ '::Total' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Growth' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of r.rows ?? []; track row.entityId) {
                    <tr>
                      <td>{{ row.entityName }}</td>
                      @for (val of row.periodValues ?? []; track $index) {
                        <td class="text-end">{{ val | number:'1.2-2' }}</td>
                      }
                      <td class="text-end fw-bold">{{ row.total | number:'1.2-2' }}</td>
                      <td class="text-end">
                        @if ((row.growth ?? 0) > 0) {
                          <span class="text-danger"><i class="fas fa-arrow-up"></i> {{ row.growth }}%</span>
                        } @else if ((row.growth ?? 0) < 0) {
                          <span class="text-success"><i class="fas fa-arrow-down"></i> {{ row.growth }}%</span>
                        } @else {
                          <span class="text-muted">—</span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-light">
                  <tr class="fw-bold">
                    <td>{{ '::Total' | abpLocalization }}</td>
                    @for (pt of r.periodTotals ?? []; track $index) {
                      <td class="text-end">{{ pt | number:'1.2-2' }}</td>
                    }
                    <td class="text-end">{{ r.grandTotal | number:'1.2-2' }}</td>
                    <td></td>
                  </tr>
                </tfoot>
              </table>
            </div>
          }

          @if (!report() && !isLoading()) {
            <div class="text-center text-muted py-4">
              <i class="fas fa-chart-bar fa-2x mb-2"></i>
              <p>{{ '::ClickGenerateToViewReport' | abpLocalization }}</p>
            </div>
          }
        </div>
      </div>
    </div>
  `,
})
export class PurchaseAnalyticsComponent implements OnInit {
  private service = inject(PurchaseAnalyticsService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  report = signal<PurchaseAnalyticsReportDto | null>(null);
  isLoading = signal(false);

  groupBy = 0;
  periodType = 0;
  valueField = 'Amount';
  fromDate = '';
  toDate = '';

  ngOnInit(): void {
    const now = new Date();
    const threeMonthsAgo = new Date(now.getFullYear(), now.getMonth() - 3, 1);
    this.fromDate = threeMonthsAgo.toISOString().split('T')[0];
    this.toDate = now.toISOString().split('T')[0];
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId || !this.fromDate || !this.toDate) return;

    this.isLoading.set(true);
    this.service.getReport({
      companyId,
      fromDate: this.fromDate,
      toDate: this.toDate,
      groupBy: this.groupBy,
      periodType: this.periodType,
      valueField: this.valueField,
    }).subscribe({
      next: (r) => { this.report.set(r); this.isLoading.set(false); },
      error: () => { this.isLoading.set(false); this.toaster.error('::FailedToGenerateReport'); },
    });
  }

  getGroupLabel(): string {
    switch (this.groupBy) {
      case 1: return '::Item';
      case 4: return '::ItemGroup';
      default: return '::Supplier';
    }
  }

  getTopShare(r: PurchaseAnalyticsReportDto): number {
    if (!r.rows?.length || !r.grandTotal) return 0;
    return ((r.rows[0].total ?? 0) / r.grandTotal) * 100;
  }

  exportCsv(): void {
    const r = this.report();
    if (!r?.rows) return;
    const cols = [this.getGroupLabel(), ...(r.periodLabels ?? []), 'Total', 'Growth %'];
    const rows = (r.rows ?? []).map(row => [
      row.entityName, ...(row.periodValues ?? []).map(v => v.toString()), (row.total ?? 0).toString(), `${row.growth ?? 0}%`
    ]);
    exportToCsv('purchase-analytics.csv', rows, cols);
  }
}
