import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { SalesPersonWiseTransactionSummaryService } from '../../../proxy/sales/sales-person-wise-transaction-summary.service';
import type {
  SalesPersonWiseTransactionSummaryReportDto,
  SalesPersonWiseTransactionRowDto,
} from '../../../proxy/sales/models';
import { SalesPersonService } from '../../../proxy/sales/sales-person.service';
import type { SalesPersonDto } from '../../../proxy/sales/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-sales-person-wise-transaction-summary',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-users text-primary me-2"></i>
            {{ '::SalesPersonWiseTransactionSummary' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::SalesPersonWiseTransactionSummaryDescription' | abpLocalization }}
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
                <option value="Delivery Note">{{ '::DeliveryNote' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::FromDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="fromDate"
                (ngModelChange)="loadReport()"
              />
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::ToDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="toDate"
                (ngModelChange)="loadReport()"
              />
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
              <label class="form-label small mb-1">{{ '::ItemCode' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                [(ngModel)]="itemCodeFilter"
                (keyup.enter)="loadReport()"
                placeholder="Search code..."
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
                  <i class="fa fa-sync me-1"></i>
                }
                {{ '::Refresh' | abpLocalization }}
              </button>
            </div>
          </div>
          <div class="row g-2 mt-1">
            <div class="col-md-12">
              <div class="form-check form-check-inline small">
                <input
                  class="form-check-input"
                  type="checkbox"
                  id="showReturnEntriesCheck"
                  [(ngModel)]="showReturnEntries"
                  (ngModelChange)="loadReport()"
                />
                <label class="form-check-label" for="showReturnEntriesCheck">
                  {{ '::ShowReturnEntries' | abpLocalization }}
                </label>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Highlights Summary -->
      @if (report()) {
        <div class="row g-2 mb-3">
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-primary-subtle text-primary py-2 px-3">
              <div class="small fw-bold">{{ '::TotalContributionQty' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ report()!.totalContributionQuantity | number:'1.2-2' }}
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-success-subtle text-success py-2 px-3">
              <div class="small fw-bold">{{ '::TotalContributionAmount' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ report()!.totalContributionAmount | number:'1.2-2' }} {{ report()!.currency }}
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-info-subtle text-info-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::TotalNetAmount' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ report()!.totalNetAmount | number:'1.2-2' }} {{ report()!.currency }}
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-secondary-subtle text-secondary-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::Transactions' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ report()!.transactionCount }}
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
                    <th style="min-width: 140px;">{{ selectedDocType }}</th>
                    <th style="min-width: 150px;">{{ '::Customer' | abpLocalization }}</th>
                    <th style="min-width: 100px;">{{ '::Date' | abpLocalization }}</th>
                    <th style="min-width: 160px;">{{ '::Item' | abpLocalization }}</th>
                    <th class="text-end" style="min-width: 80px;">{{ '::Qty' | abpLocalization }}</th>
                    <th class="text-end" style="min-width: 90px;">{{ '::Rate' | abpLocalization }}</th>
                    <th class="text-end" style="min-width: 110px;">{{ '::Amount' | abpLocalization }}</th>
                    <th style="min-width: 140px;">{{ '::SalesPerson' | abpLocalization }}</th>
                    <th class="text-end" style="min-width: 80px;">%</th>
                    <th class="text-end" style="min-width: 100px;">{{ '::ContributionQty' | abpLocalization }}</th>
                    <th class="text-end" style="min-width: 120px;">{{ '::ContributionAmount' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of report()!.rows; track (row.transactionId + '-' + row.itemId + '-' + row.salesPersonId)) {
                    <tr>
                      <td class="fw-semibold">
                        <a [routerLink]="getTransactionRoute(row)" class="text-decoration-none">
                          {{ row.transactionNumber }}
                        </a>
                        @if (row.warehouseName) {
                          <div>
                            <span class="badge bg-secondary-subtle text-secondary border">
                              <i class="fa fa-warehouse me-1"></i>{{ row.warehouseName }}
                            </span>
                          </div>
                        }
                      </td>
                      <td>
                        <div class="fw-bold text-dark">{{ row.customerName }}</div>
                        @if (row.territoryName) {
                          <span class="badge bg-light text-secondary border">
                            <i class="fa fa-map-marker-alt me-1"></i>{{ row.territoryName }}
                          </span>
                        }
                      </td>
                      <td>{{ row.postingDate | date:'shortDate' }}</td>
                      <td>
                        <div class="fw-semibold">{{ row.itemCode }}</div>
                        <div class="text-muted small text-truncate" style="max-width: 180px;">
                          {{ row.itemName }}
                        </div>
                        @if (row.itemGroup) {
                          <span class="badge bg-info-subtle text-info border">
                            {{ row.itemGroup }}
                          </span>
                        }
                      </td>
                      <td class="text-end">{{ row.quantity | number:'1.2-2' }}</td>
                      <td class="text-end">{{ row.rate | number:'1.2-2' }}</td>
                      <td class="text-end fw-semibold">{{ row.baseNetAmount | number:'1.2-2' }}</td>
                      <td>
                        <a [routerLink]="['/sales/sales-persons', row.salesPersonId]" class="text-decoration-none">
                          <i class="fa fa-user text-secondary me-1"></i>{{ row.salesPersonName }}
                        </a>
                      </td>
                      <td class="text-end text-muted">{{ row.allocatedPercentage | number:'1.1-2' }}%</td>
                      <td class="text-end fw-bold text-primary">{{ row.contributionQuantity | number:'1.2-2' }}</td>
                      <td class="text-end text-success fw-bold">{{ row.contributionAmount | number:'1.2-2' }}</td>
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
export class SalesPersonWiseTransactionSummaryComponent implements OnInit {
  private reportService = inject(SalesPersonWiseTransactionSummaryService);
  private salesPersonService = inject(SalesPersonService);
  private companyContext = inject(CompanyContextService);

  report = signal<SalesPersonWiseTransactionSummaryReportDto | null>(null);
  salesPersons = signal<SalesPersonDto[]>([]);
  loading = signal(false);

  selectedDocType = 'Sales Invoice';
  selectedSalesPersonId = '';
  itemCodeFilter = '';
  showReturnEntries = false;
  fromDate = '';
  toDate = '';

  ngOnInit(): void {
    const now = new Date();
    const firstDay = new Date(now.getFullYear(), now.getMonth(), 1);
    this.fromDate = firstDay.toISOString().substring(0, 10);
    this.toDate = now.toISOString().substring(0, 10);

    this.loadSalesPersons();
    this.loadReport();
  }

  loadSalesPersons(): void {
    this.salesPersonService.getList({ maxResultCount: 200 }).subscribe({
      next: res => this.salesPersons.set(res.items || []),
      error: () => this.salesPersons.set([]),
    });
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.loading.set(true);

    this.reportService
      .getReport({
        companyId,
        docType: this.selectedDocType,
        fromDate: this.fromDate ? `${this.fromDate}T00:00:00Z` : null,
        toDate: this.toDate ? `${this.toDate}T23:59:59Z` : null,
        salesPersonId: this.selectedSalesPersonId || null,
        itemCode: this.itemCodeFilter?.trim() || null,
        showReturnEntries: this.showReturnEntries,
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

  getTransactionRoute(row: SalesPersonWiseTransactionRowDto): string[] {
    switch (row.docType) {
      case 'Sales Order':
        return ['/sales/orders', row.transactionId];
      case 'Delivery Note':
        return ['/sales/delivery-notes', row.transactionId];
      default:
        return ['/sales/invoices', row.transactionId];
    }
  }

  exportCsv(): void {
    const data = this.report();
    if (!data || !data.rows || data.rows.length === 0) return;

    const headers = [
      'Document Type',
      'Transaction Number',
      'Posting Date',
      'Customer',
      'Territory',
      'Warehouse',
      'Item Code',
      'Item Name',
      'Item Group',
      'Quantity',
      'Rate',
      'Net Amount',
      'Sales Person',
      'Allocated (%)',
      'Contribution Qty',
      'Contribution Amount',
    ];

    const dataRows = data.rows.map(r => ({
      'Document Type': r.docType,
      'Transaction Number': r.transactionNumber,
      'Posting Date': r.postingDate ? r.postingDate.substring(0, 10) : '',
      Customer: r.customerName,
      Territory: r.territoryName ?? '',
      Warehouse: r.warehouseName ?? '',
      'Item Code': r.itemCode,
      'Item Name': r.itemName,
      'Item Group': r.itemGroup ?? '',
      Quantity: r.quantity,
      Rate: r.rate,
      'Net Amount': r.baseNetAmount,
      'Sales Person': r.salesPersonName,
      'Allocated (%)': `${r.allocatedPercentage}%`,
      'Contribution Qty': r.contributionQuantity,
      'Contribution Amount': r.contributionAmount,
    }));

    exportToCsv(
      `sales_person_wise_transaction_summary_${new Date().toISOString().substring(0, 10)}.csv`,
      dataRows,
      headers
    );
  }
}
