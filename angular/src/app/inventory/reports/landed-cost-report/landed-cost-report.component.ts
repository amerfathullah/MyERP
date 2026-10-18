import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { LandedCostReportService } from '../../../proxy/inventory/landed-cost-report.service';
import type { LandedCostReportDto, LandedCostReportRowDto } from '../../../proxy/inventory/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-landed-cost-report',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-file-invoice-dollar me-2 text-primary"></i>
            {{ '::LandedCostReport' | abpLocalization }}
          </h5>
          <small class="text-muted">
            {{ '::LandedCostReportDescription' | abpLocalization }}
          </small>
        </div>
        <div>
          @if (filteredRows().length > 0) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
      </div>

      <div class="card-body">
        <!-- Filters Row 1 -->
        <div class="row g-2 mb-3 align-items-end">
          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::ReceiptDocumentType' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="receiptDocumentType" (change)="loadData()">
              <option value="All">All Types</option>
              <option value="PurchaseReceipt">Purchase Receipt</option>
              <option value="PurchaseInvoice">Purchase Invoice</option>
              <option value="StockEntry">Stock Entry</option>
            </select>
          </div>

          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::ReceiptDocumentNo' | abpLocalization }}</label>
            <input
              type="text"
              class="form-control form-control-sm"
              placeholder="e.g. PR-2026-001..."
              [(ngModel)]="receiptDocumentNo"
              (keyup.enter)="loadData()"
            />
          </div>

          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::FromDate' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" (change)="loadData()" />
          </div>

          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::ToDate' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" (change)="loadData()" />
          </div>

          <div class="col-md-2 col-sm-12">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Filters Row 2 -->
        <div class="row g-2 mb-4 align-items-center">
          <div class="col-md-6 col-sm-12">
            <label class="form-label small fw-semibold">{{ '::Search' | abpLocalization }}</label>
            <input
              type="text"
              class="form-control form-control-sm"
              [placeholder]="'Search voucher #, receipt #, supplier, charge description...'"
              [(ngModel)]="searchTerm"
            />
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">Status</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedStatus" (change)="loadData()">
              <option [ngValue]="null">All Statuses</option>
              <option [ngValue]="1">Submitted</option>
              <option [ngValue]="0">Draft</option>
              <option [ngValue]="4">Cancelled</option>
            </select>
          </div>
          <div class="col-md-2 col-sm-6 mt-auto">
            <button class="btn btn-outline-secondary btn-sm w-100" (click)="resetFilters()">
              <i class="fa fa-times me-1"></i>Reset
            </button>
          </div>
        </div>

        <!-- KPI Metrics -->
        @if (reportData(); as data) {
          <div class="row g-3 mb-4">
            <div class="col-md-4 col-sm-6">
              <div class="card border-0 bg-light p-3 text-center">
                <small class="text-muted fw-semibold">Landed Cost Vouchers</small>
                <span class="fs-4 fw-bold text-primary">{{ data.totalVouchersCount }}</span>
              </div>
            </div>
            <div class="col-md-4 col-sm-6">
              <div class="card border-0 bg-light p-3 text-center">
                <small class="text-muted fw-semibold">Total Landed Costs</small>
                <span class="fs-4 fw-bold text-success">{{ data.totalLandedCost | currency:'USD':'symbol':'1.2-2' }}</span>
              </div>
            </div>
            <div class="col-md-4 col-sm-12">
              <div class="card border-0 bg-light p-3 text-center">
                <small class="text-muted fw-semibold">Receipt Documents Affected</small>
                <span class="fs-4 fw-bold text-info">{{ data.totalReceiptsAffected }}</span>
              </div>
            </div>
          </div>
        }

        <!-- Report Data Table -->
        <div class="table-responsive border rounded">
          <table class="table table-sm table-hover align-middle mb-0">
            <thead class="table-light text-nowrap">
              <tr>
                <th>Landed Cost Voucher</th>
                <th>Posting Date</th>
                <th>Status</th>
                <th>Receipt Voucher</th>
                <th>Supplier</th>
                <th class="text-end">Receipt Value</th>
                <th class="text-end">{{ '::AllocatedCharges' | abpLocalization }}</th>
                <th>Distribution Method</th>
                <th>Charges Breakdown</th>
                <th>Items Covered</th>
              </tr>
            </thead>
            <tbody>
              @if (isLoading()) {
                <tr>
                  <td colspan="10" class="text-center py-4 text-muted">
                    <i class="fa fa-spinner fa-spin me-2"></i>Loading landed cost report...
                  </td>
                </tr>
              } @else if (filteredRows().length === 0) {
                <tr>
                  <td colspan="10" class="text-center py-4 text-muted">
                    <i class="fa fa-info-circle me-1"></i>
                    {{ '::NoLandedCostRecordsFound' | abpLocalization }}
                  </td>
                </tr>
              } @else {
                @for (row of filteredRows(); track row.landedCostVoucherId + '-' + row.receiptDocumentId) {
                  <tr>
                    <td>
                      <span class="fw-bold">{{ row.landedCostVoucherNo }}</span>
                    </td>
                    <td class="text-nowrap small text-muted">
                      {{ row.postingDate | date:'mediumDate' }}
                    </td>
                    <td>
                      @if (row.status === 1) {
                        <span class="badge bg-success">Submitted</span>
                      } @else if (row.status === 0) {
                        <span class="badge bg-secondary">Draft</span>
                      } @else if (row.status === 4) {
                        <span class="badge bg-danger">Cancelled</span>
                      } @else {
                        <span class="badge bg-light text-dark border">{{ row.status }}</span>
                      }
                    </td>
                    <td>
                      <span class="badge bg-light text-dark border me-1">{{ row.receiptDocumentType }}</span>
                      <span class="fw-semibold">{{ row.receiptDocumentNo }}</span>
                    </td>
                    <td>
                      @if (row.supplierName) {
                        <i class="fa fa-truck me-1 text-info small"></i>{{ row.supplierName }}
                      } @else {
                        <span class="text-muted">-</span>
                      }
                    </td>
                    <td class="text-end">
                      {{ row.receiptTotalAmount | currency:'USD':'symbol':'1.2-2' }}
                    </td>
                    <td class="text-end fw-bold text-success">
                      {{ row.allocatedCharges | currency:'USD':'symbol':'1.2-2' }}
                    </td>
                    <td>
                      <span class="badge bg-light text-muted border">
                        {{ formatMethod(row.distributionMethod) }}
                      </span>
                    </td>
                    <td>
                      <small class="text-muted text-truncate" style="max-width: 220px; display: inline-block;">
                        {{ row.chargesSummary }}
                      </small>
                    </td>
                    <td>
                      <small class="text-muted text-truncate" style="max-width: 200px; display: inline-block;">
                        {{ row.itemsSummary }} ({{ row.itemCount }} item{{ row.itemCount === 1 ? '' : 's' }})
                      </small>
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
export class LandedCostReportComponent implements OnInit {
  private landedCostReportService = inject(LandedCostReportService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  reportData = signal<LandedCostReportDto | null>(null);
  isLoading = signal(false);

  receiptDocumentType = 'All';
  receiptDocumentNo = '';
  fromDate = '';
  toDate = '';
  selectedStatus: number | null = 1; // Submitted by default
  searchTerm = '';

  filteredRows = computed(() => {
    const data = this.reportData();
    if (!data?.rows) return [];

    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return data.rows;

    return data.rows.filter(r =>
      r.landedCostVoucherNo.toLowerCase().includes(term) ||
      r.receiptDocumentNo.toLowerCase().includes(term) ||
      r.receiptDocumentType.toLowerCase().includes(term) ||
      (r.supplierName && r.supplierName.toLowerCase().includes(term)) ||
      r.chargesSummary.toLowerCase().includes(term) ||
      r.itemsSummary.toLowerCase().includes(term)
    );
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading.set(true);
    const companyId = this.companyContext.selectedCompanyId() || undefined;

    this.landedCostReportService
      .getReport({
        companyId,
        receiptDocumentType: this.receiptDocumentType !== 'All' ? this.receiptDocumentType : undefined,
        receiptDocumentNo: this.receiptDocumentNo.trim() || undefined,
        fromDate: this.fromDate || undefined,
        toDate: this.toDate || undefined,
        status: this.selectedStatus ?? undefined,
        searchTerm: this.searchTerm.trim() || undefined,
      })
      .subscribe({
        next: res => {
          this.reportData.set(res);
          this.isLoading.set(false);
        },
        error: err => {
          this.isLoading.set(false);
          this.toaster.error(err.message || 'Failed to load landed cost report');
        },
      });
  }

  resetFilters(): void {
    this.receiptDocumentType = 'All';
    this.receiptDocumentNo = '';
    this.fromDate = '';
    this.toDate = '';
    this.selectedStatus = 1;
    this.searchTerm = '';
    this.loadData();
  }

  formatMethod(method: number): string {
    switch (method) {
      case 0: return 'Based On Quantity';
      case 1: return 'Based On Amount';
      case 2: return 'Manual';
      default: return String(method);
    }
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (!rows.length) return;

    const columns = [
      'LandedCostVoucherNo',
      'PostingDate',
      'Status',
      'ReceiptDocumentType',
      'ReceiptDocumentNo',
      'Supplier',
      'ReceiptTotalAmount',
      'AllocatedCharges',
      'DistributionMethod',
      'ChargesSummary',
      'ItemsCovered',
    ];

    const exportRows = rows.map(r => ({
      LandedCostVoucherNo: r.landedCostVoucherNo,
      PostingDate: r.postingDate,
      Status: r.status === 1 ? 'Submitted' : (r.status === 0 ? 'Draft' : 'Cancelled'),
      ReceiptDocumentType: r.receiptDocumentType,
      ReceiptDocumentNo: r.receiptDocumentNo,
      Supplier: r.supplierName || '',
      ReceiptTotalAmount: r.receiptTotalAmount,
      AllocatedCharges: r.allocatedCharges,
      DistributionMethod: this.formatMethod(r.distributionMethod),
      ChargesSummary: r.chargesSummary,
      ItemsCovered: r.itemsSummary,
    }));

    exportToCsv('landed_cost_report.csv', exportRows, columns);
  }
}
