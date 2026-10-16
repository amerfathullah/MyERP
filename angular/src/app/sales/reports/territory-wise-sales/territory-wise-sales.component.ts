import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { TerritoryWiseSalesService } from '../../../proxy/sales/territory-wise-sales.service';
import type {
  TerritoryWiseSalesReportDto,
  TerritoryWiseSalesRowDto,
} from '../../../proxy/sales/models';
import { TerritoryService } from '../../../proxy/core/territory.service';
import type { TerritoryDto } from '../../../proxy/core/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-territory-wise-sales',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-map-marked-alt text-primary me-2"></i>
            {{ '::TerritoryWiseSales' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::TerritoryWiseSalesDescription' | abpLocalization }}
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
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::Territory' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedTerritoryId"
                (ngModelChange)="loadReport()"
              >
                <option value="">-- {{ '::AllTerritories' | abpLocalization }} --</option>
                @for (terr of territories(); track terr.id) {
                  <option [value]="terr.id">{{ terr.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-3 d-flex align-items-end">
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
        </div>
      </div>

      <!-- Highlights Summary -->
      @if (report()) {
        <div class="row g-2 mb-3">
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-primary-subtle text-primary py-2 px-3">
              <div class="small fw-bold">{{ '::TotalOpportunityAmount' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ report()!.totalOpportunityAmount | number:'1.2-2' }} {{ report()!.currency }}
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-info-subtle text-info-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::TotalQuotationAmount' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ report()!.totalQuotationAmount | number:'1.2-2' }} {{ report()!.currency }}
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-warning-subtle text-warning-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::TotalOrderAmount' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ report()!.totalOrderAmount | number:'1.2-2' }} {{ report()!.currency }}
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-success-subtle text-success py-2 px-3">
              <div class="small fw-bold">{{ '::TotalBillingAmount' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ report()!.totalBillingAmount | number:'1.2-2' }} {{ report()!.currency }}
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
              <i class="fa fa-inbox fa-3x mb-3 text-secondary"></i>
              <p class="mb-0">{{ '::NoRecordsFound' | abpLocalization }}</p>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-hover table-striped align-middle mb-0" style="font-size: 0.85rem;">
                <thead class="table-light">
                  <tr>
                    <th style="min-width: 170px;">{{ '::Territory' | abpLocalization }}</th>
                    <th class="text-end" style="min-width: 140px;">{{ '::OpportunityAmount' | abpLocalization }}</th>
                    <th class="text-end" style="min-width: 140px;">{{ '::QuotationAmount' | abpLocalization }}</th>
                    <th class="text-end" style="min-width: 140px;">{{ '::OrderAmount' | abpLocalization }}</th>
                    <th class="text-end" style="min-width: 140px;">{{ '::BillingAmount' | abpLocalization }}</th>
                    <th class="text-center" style="min-width: 120px;">{{ '::ConversionRate' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of report()!.rows; track (row.territoryId || row.territoryName)) {
                    <tr>
                      <td class="fw-semibold">
                        <i class="fa fa-map-marker-alt text-secondary me-1"></i>
                        {{ row.territoryName }}
                      </td>
                      <td class="text-end">
                        <div class="fw-bold">{{ row.opportunityAmount | number:'1.2-2' }}</div>
                        <div class="small text-muted">{{ row.opportunityCount }} {{ '::Opportunities' | abpLocalization }}</div>
                      </td>
                      <td class="text-end">
                        <div class="fw-bold text-info">{{ row.quotationAmount | number:'1.2-2' }}</div>
                        <div class="small text-muted">{{ row.quotationCount }} {{ '::Quotations' | abpLocalization }}</div>
                      </td>
                      <td class="text-end">
                        <div class="fw-bold text-primary">{{ row.orderAmount | number:'1.2-2' }}</div>
                        <div class="small text-muted">{{ row.orderCount }} {{ '::Orders' | abpLocalization }}</div>
                      </td>
                      <td class="text-end">
                        <div class="fw-bold text-success">{{ row.billingAmount | number:'1.2-2' }}</div>
                        <div class="small text-muted">{{ row.billingCount }} {{ '::Invoices' | abpLocalization }}</div>
                      </td>
                      <td class="text-center">
                        <span
                          class="badge"
                          [class.bg-success]="getConversionRate(row) >= 50"
                          [class.bg-warning]="getConversionRate(row) > 0 && getConversionRate(row) < 50"
                          [class.bg-light]="getConversionRate(row) === 0"
                          [class.text-dark]="getConversionRate(row) === 0"
                        >
                          {{ formatConversion(row) }}
                        </span>
                      </td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-light fw-bold">
                  <tr class="table-info">
                    <td>{{ '::Total' | abpLocalization }}</td>
                    <td class="text-end">{{ report()!.totalOpportunityAmount | number:'1.2-2' }}</td>
                    <td class="text-end text-info">{{ report()!.totalQuotationAmount | number:'1.2-2' }}</td>
                    <td class="text-end text-primary">{{ report()!.totalOrderAmount | number:'1.2-2' }}</td>
                    <td class="text-end text-success">{{ report()!.totalBillingAmount | number:'1.2-2' }}</td>
                    <td class="text-center">
                      {{ formatTotalConversion() }}
                    </td>
                  </tr>
                </tfoot>
              </table>
            </div>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .table td, .table th {
      padding: 0.6rem 0.75rem;
    }
  `],
})
export class TerritoryWiseSalesComponent implements OnInit {
  private territoryWiseSalesService = inject(TerritoryWiseSalesService);
  private territoryService = inject(TerritoryService);
  private companyContext = inject(CompanyContextService);

  report = signal<TerritoryWiseSalesReportDto | null>(null);
  territories = signal<TerritoryDto[]>([]);
  loading = signal(false);

  fromDate = '';
  toDate = '';
  selectedTerritoryId = '';

  ngOnInit(): void {
    const now = new Date();
    const oneMonthAgo = new Date();
    oneMonthAgo.setMonth(now.getMonth() - 1);

    this.toDate = now.toISOString().slice(0, 10);
    this.fromDate = oneMonthAgo.toISOString().slice(0, 10);

    this.loadTerritories();
    this.loadReport();
  }

  loadTerritories(): void {
    this.territoryService
      .getList({
        maxResultCount: 200,
        skipCount: 0,
      })
      .subscribe({
        next: (res) => {
          this.territories.set(res.items || []);
        },
      });
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.loading.set(true);

    this.territoryWiseSalesService
      .getReport({
        companyId,
        fromDate: this.fromDate ? new Date(this.fromDate).toISOString() : undefined,
        toDate: this.toDate ? new Date(this.toDate + 'T23:59:59Z').toISOString() : undefined,
        territoryId: this.selectedTerritoryId || undefined,
      })
      .subscribe({
        next: (res) => {
          this.report.set(res);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }

  getConversionRate(row: TerritoryWiseSalesRowDto): number {
    if (!row.opportunityAmount || row.opportunityAmount <= 0) {
      return row.billingAmount > 0 ? 100 : 0;
    }
    return Math.round((row.billingAmount / row.opportunityAmount) * 100);
  }

  formatConversion(row: TerritoryWiseSalesRowDto): string {
    if (!row.opportunityAmount || row.opportunityAmount <= 0) {
      return row.billingAmount > 0 ? '100%' : '—';
    }
    const pct = ((row.billingAmount / row.opportunityAmount) * 100).toFixed(1);
    return `${pct}%`;
  }

  formatTotalConversion(): string {
    const rep = this.report();
    if (!rep || !rep.totalOpportunityAmount || rep.totalOpportunityAmount <= 0) {
      return rep && rep.totalBillingAmount > 0 ? '100%' : '—';
    }
    const pct = ((rep.totalBillingAmount / rep.totalOpportunityAmount) * 100).toFixed(1);
    return `${pct}%`;
  }

  exportCsv(): void {
    const rep = this.report();
    if (!rep || !rep.rows || rep.rows.length === 0) return;

    const headers = [
      'Territory',
      'Opportunity Amount',
      'Opportunity Count',
      'Quotation Amount',
      'Quotation Count',
      'Order Amount',
      'Order Count',
      'Billing Amount',
      'Billing Count',
      'Conversion Rate (%)',
    ];

    const dataRows = rep.rows.map((r) => ({
      Territory: r.territoryName,
      'Opportunity Amount': r.opportunityAmount,
      'Opportunity Count': r.opportunityCount,
      'Quotation Amount': r.quotationAmount,
      'Quotation Count': r.quotationCount,
      'Order Amount': r.orderAmount,
      'Order Count': r.orderCount,
      'Billing Amount': r.billingAmount,
      'Billing Count': r.billingCount,
      'Conversion Rate (%)': this.formatConversion(r),
    }));

    // Add total row
    dataRows.push({
      Territory: 'Total',
      'Opportunity Amount': rep.totalOpportunityAmount,
      'Opportunity Count': rep.rows.reduce((sum, r) => sum + r.opportunityCount, 0),
      'Quotation Amount': rep.totalQuotationAmount,
      'Quotation Count': rep.rows.reduce((sum, r) => sum + r.quotationCount, 0),
      'Order Amount': rep.totalOrderAmount,
      'Order Count': rep.rows.reduce((sum, r) => sum + r.orderCount, 0),
      'Billing Amount': rep.totalBillingAmount,
      'Billing Count': rep.rows.reduce((sum, r) => sum + r.billingCount, 0),
      'Conversion Rate (%)': this.formatTotalConversion(),
    });

    exportToCsv('Territory_Wise_Sales.csv', dataRows, headers);
  }
}
