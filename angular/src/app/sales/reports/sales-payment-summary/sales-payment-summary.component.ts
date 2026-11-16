import { Component, OnInit, computed, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { SalesPaymentSummaryService } from '../../../proxy/sales/sales-payment-summary.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyService } from '../../../proxy/core/company.service';
import type { SalesPaymentSummaryReportDto, SalesPaymentSummaryRowDto } from '../../../proxy/sales/models';
import type { WarehouseDto } from '../../../proxy/inventory/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function filterPaymentSummaryRows(rows: SalesPaymentSummaryRowDto[], term: string): SalesPaymentSummaryRowDto[] {
  const q = term.trim().toLowerCase();
  if (!q) return rows;
  return rows.filter(
    r =>
      (r.cashier ?? '').toLowerCase().includes(q) ||
      (r.warehouseName ?? '').toLowerCase().includes(q) ||
      (r.modeOfPayment ?? '').toLowerCase().includes(q) ||
      (r.costCenter ?? '').toLowerCase().includes(q)
  );
}

@Component({
  selector: 'app-sales-payment-summary',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="container-fluid py-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-money-check-dollar text-success me-2"></i>{{ '::SalesPaymentSummary' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">{{ '::SalesPaymentSummaryDescription' | abpLocalization }}</p>
        </div>
        @if (filteredRows().length > 0) {
          <button type="button" class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fa fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <div class="card mb-3 shadow-sm border-0">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::FromDate' | abpLocalization }}</label>
              <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" (change)="loadReport()" />
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::ToDate' | abpLocalization }}</label>
              <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" (change)="loadReport()" />
            </div>
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::Warehouse' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="selectedWarehouseId" (change)="loadReport()">
                <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
                @for (w of warehouses(); track w.id) {
                  <option [value]="w.id">{{ w.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::Search' | abpLocalization }}</label>
              <input type="text" class="form-control form-control-sm"
                [placeholder]="'::Search' | abpLocalization"
                [ngModel]="searchTerm()"
                (ngModelChange)="searchTerm.set($event)" />
            </div>
            <div class="col-md-2 d-flex align-items-end">
              <button type="button" class="btn btn-primary btn-sm w-100" (click)="loadReport()" [disabled]="isLoading()">
                <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Refresh' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      @if (report()) {
        <div class="row g-3 mb-3">
          <div class="col-md-3 col-sm-6">
            <div class="card bg-light border-0 shadow-sm h-100">
              <div class="card-body p-3">
                <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalNet' | abpLocalization }}</div>
                <div class="h4 mb-0 text-primary mt-1">{{ (report()!.totalNet ?? 0) | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3 col-sm-6">
            <div class="card bg-light border-0 shadow-sm h-100">
              <div class="card-body p-3">
                <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalTaxes' | abpLocalization }}</div>
                <div class="h4 mb-0 text-info mt-1">{{ (report()!.totalTaxes ?? 0) | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3 col-sm-6">
            <div class="card bg-light border-0 shadow-sm h-100">
              <div class="card-body p-3">
                <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalPaid' | abpLocalization }}</div>
                <div class="h4 mb-0 text-success mt-1">{{ (report()!.totalPaid ?? 0) | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3 col-sm-6">
            <div class="card bg-light border-0 shadow-sm h-100">
              <div class="card-body p-3">
                <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalOutstanding' | abpLocalization }}</div>
                <div class="h4 mb-0 text-warning mt-1">{{ (report()!.totalOutstanding ?? 0) | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
        </div>

        <div class="card shadow-sm border-0">
          <div class="table-responsive">
            <table class="table table-sm table-hover align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::PostingDate' | abpLocalization }}</th>
                  <th>{{ '::Warehouse' | abpLocalization }}</th>
                  <th>{{ '::Cashier' | abpLocalization }}</th>
                  <th>{{ '::ModeOfPayment' | abpLocalization }}</th>
                  <th>{{ '::CostCenter' | abpLocalization }}</th>
                  <th class="text-end">{{ '::TotalNet' | abpLocalization }}</th>
                  <th class="text-end">{{ '::TotalTaxes' | abpLocalization }}</th>
                  <th class="text-end">{{ '::PaidAmount' | abpLocalization }}</th>
                  <th class="text-end">{{ '::OutstandingAmount' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track $index) {
                  <tr>
                    <td class="small">{{ row.postingDate ? (row.postingDate | date:'yyyy-MM-dd') : '&mdash;' }}</td>
                    <td class="small">{{ row.warehouseName || '&mdash;' }}</td>
                    <td class="small">{{ row.cashier || '&mdash;' }}</td>
                    <td class="small">{{ row.modeOfPayment || '&mdash;' }}</td>
                    <td class="small">{{ row.costCenter || '&mdash;' }}</td>
                    <td class="text-end font-monospace small">{{ (row.netTotal ?? 0) | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace small">{{ (row.totalTaxes ?? 0) | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace small text-success fw-semibold">{{ (row.paidAmount ?? 0) | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace small">{{ (row.outstandingAmount ?? 0) | number:'1.2-2' }}</td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="9" class="text-center text-muted py-4">{{ '::NoDataAvailable' | abpLocalization }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    </div>
  `,
})
export class SalesPaymentSummaryComponent implements OnInit {
  private service = inject(SalesPaymentSummaryService);
  private warehouseService = inject(WarehouseService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);

  fromDate = new Date(Date.now() - 30 * 86400000).toISOString().substring(0, 10);
  toDate = new Date().toISOString().substring(0, 10);
  selectedWarehouseId = '';
  searchTerm = signal('');
  warehouses = signal<WarehouseDto[]>([]);
  isLoading = signal(false);
  report = signal<SalesPaymentSummaryReportDto | null>(null);
  private companyId = '';

  filteredRows = computed(() => filterPaymentSummaryRows(this.report()?.rows ?? [], this.searchTerm()));

  ngOnInit(): void {
    this.warehouseService.getList({ maxResultCount: 100 } as any).subscribe({
      next: res => this.warehouses.set(res.items ?? []),
      error: () => {},
    });

    const ctxId = this.companyContext.currentCompanyId() ?? '';
    if (ctxId) {
      this.companyId = ctxId;
      this.loadReport();
    } else {
      this.companyService.getList({ skipCount: 0, maxResultCount: 10, sorting: '' }).subscribe({
        next: res => {
          this.companyId = res.items?.[0]?.id ?? '';
          if (this.companyId) this.loadReport();
        },
        error: () => {},
      });
    }
  }

  loadReport(): void {
    const cid = this.companyContext.currentCompanyId() ?? this.companyId;
    if (!cid) return;
    this.isLoading.set(true);
    this.service.getReport({
      companyId: cid,
      fromDate: this.fromDate || undefined,
      toDate: this.toDate || undefined,
      warehouseId: this.selectedWarehouseId || undefined,
    }).subscribe({
      next: res => {
        this.report.set(res);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (rows.length === 0) return;
    exportToCsv(
      `Sales-Payment-Summary-${this.fromDate}-${this.toDate}`,
      rows.map(r => ({
        PostingDate: r.postingDate ? r.postingDate.substring(0, 10) : '',
        Warehouse: r.warehouseName ?? '',
        Cashier: r.cashier ?? '',
        ModeOfPayment: r.modeOfPayment ?? '',
        CostCenter: r.costCenter ?? '',
        NetTotal: r.netTotal ?? 0,
        TotalTaxes: r.totalTaxes ?? 0,
        PaidAmount: r.paidAmount ?? 0,
        OutstandingAmount: r.outstandingAmount ?? 0,
      })),
      ['PostingDate', 'Warehouse', 'Cashier', 'ModeOfPayment', 'CostCenter', 'NetTotal', 'TotalTaxes', 'PaidAmount', 'OutstandingAmount']
    );
  }
}
