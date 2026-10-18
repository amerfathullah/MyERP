import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { SupplierLedgerSummaryService } from '../../../proxy/purchasing/supplier-ledger-summary.service';
import { SupplierService } from '../../../proxy/purchasing/supplier.service';
import type {
  SupplierLedgerSummaryReportDto,
  SupplierLedgerSummaryRowDto,
  SupplierDto,
} from '../../../proxy/purchasing/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-supplier-ledger-summary',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-book-open text-primary me-2"></i>
            {{ '::SupplierLedgerSummary' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::SupplierLedgerSummaryDescription' | abpLocalization }}
          </p>
        </div>
        <div class="d-flex gap-2">
          @if (report() && report()!.rows.length > 0) {
            <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
              <i class="fa fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
          <button class="btn btn-sm btn-primary" (click)="loadReport()" [disabled]="isLoading()">
            <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Refresh' | abpLocalization }}
          </button>
        </div>
      </div>

      <!-- Filters Card -->
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
            <div class="col-md-4">
              <label class="form-label small mb-1">{{ '::Supplier' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedSupplierId"
                (ngModelChange)="loadReport()"
              >
                <option value="">-- {{ '::All' | abpLocalization }} --</option>
                @for (s of suppliers(); track s.id) {
                  <option [value]="s.id">{{ s.name }} ({{ s.supplierCode }})</option>
                }
              </select>
            </div>
            <div class="col-md-2 d-flex align-items-end">
              <button
                class="btn btn-sm btn-outline-secondary w-100"
                (click)="resetFilters()"
              >
                <i class="fa fa-undo me-1"></i>{{ '::Reset' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      @if (isLoading()) {
        <div class="text-center py-5">
          <div class="spinner-border text-primary" role="status">
            <span class="visually-hidden">Loading...</span>
          </div>
        </div>
      } @else if (report()) {
        <!-- KPI Summary Cards -->
        <div class="row g-3 mb-3">
          <div class="col-6 col-md">
            <div class="card border-0 shadow-sm text-center py-2 bg-light">
              <div class="text-muted small">{{ '::OpeningBalance' | abpLocalization }}</div>
              <div class="fw-bold fs-5 text-dark">
                {{ report()!.companyCurrency }} {{ report()!.totalRow.openingBalance | number:'1.2-2' }}
              </div>
            </div>
          </div>
          <div class="col-6 col-md">
            <div class="card border-0 shadow-sm text-center py-2 bg-light">
              <div class="text-muted small">{{ '::InvoicedAmount' | abpLocalization }}</div>
              <div class="fw-bold fs-5 text-primary">
                {{ report()!.companyCurrency }} {{ report()!.totalRow.invoicedAmount | number:'1.2-2' }}
              </div>
            </div>
          </div>
          <div class="col-6 col-md">
            <div class="card border-0 shadow-sm text-center py-2 bg-light">
              <div class="text-muted small">{{ '::PaidAmount' | abpLocalization }}</div>
              <div class="fw-bold fs-5 text-success">
                {{ report()!.companyCurrency }} {{ report()!.totalRow.paidAmount | number:'1.2-2' }}
              </div>
            </div>
          </div>
          <div class="col-6 col-md">
            <div class="card border-0 shadow-sm text-center py-2 bg-light">
              <div class="text-muted small">{{ '::ReturnAmount' | abpLocalization }}</div>
              <div class="fw-bold fs-5 text-warning">
                {{ report()!.companyCurrency }} {{ report()!.totalRow.returnAmount | number:'1.2-2' }}
              </div>
            </div>
          </div>
          <div class="col-6 col-md">
            <div class="card border-0 shadow-sm text-center py-2 bg-light">
              <div class="text-muted small">{{ '::ClosingBalance' | abpLocalization }}</div>
              <div
                class="fw-bold fs-5"
                [class.text-danger]="report()!.totalRow.closingBalance > 0"
                [class.text-success]="report()!.totalRow.closingBalance <= 0"
              >
                {{ report()!.companyCurrency }} {{ report()!.totalRow.closingBalance | number:'1.2-2' }}
                <span class="badge ms-1" [class.bg-danger]="report()!.totalRow.drOrCr === 'Cr'" [class.bg-info]="report()!.totalRow.drOrCr === 'Dr'">
                  {{ report()!.totalRow.drOrCr }}
                </span>
              </div>
            </div>
          </div>
        </div>

        <!-- Ledger Summary Table -->
        <div class="card shadow-sm border-0">
          <div class="card-body p-0">
            <div class="table-responsive">
              <table class="table table-hover table-striped align-middle mb-0">
                <thead class="table-dark">
                  <tr>
                    <th>{{ '::Supplier' | abpLocalization }}</th>
                    <th>{{ '::SupplierGroup' | abpLocalization }}</th>
                    <th class="text-end">{{ '::OpeningBalance' | abpLocalization }}</th>
                    <th class="text-end">{{ '::InvoicedAmount' | abpLocalization }}</th>
                    <th class="text-end">{{ '::PaidAmount' | abpLocalization }}</th>
                    <th class="text-end">{{ '::ReturnAmount' | abpLocalization }}</th>
                    <th class="text-end">{{ '::AdjustmentAmount' | abpLocalization }}</th>
                    <th class="text-end">{{ '::ClosingBalance' | abpLocalization }}</th>
                    <th class="text-center">{{ '::DrCr' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @if (report()!.rows.length === 0) {
                    <tr>
                      <td colspan="9" class="text-center py-4 text-muted">
                        {{ '::NoSupplierLedgerSummaryFound' | abpLocalization }}
                      </td>
                    </tr>
                  } @else {
                    @for (row of report()!.rows; track row.supplierId) {
                      <tr>
                        <td>
                          <strong>{{ row.supplierName }}</strong>
                          <span class="text-muted small ms-1">({{ row.supplierCode }})</span>
                        </td>
                        <td>{{ row.supplierGroupName || '-' }}</td>
                        <td class="text-end">{{ row.openingBalance | number:'1.2-2' }}</td>
                        <td class="text-end text-primary">{{ row.invoicedAmount | number:'1.2-2' }}</td>
                        <td class="text-end text-success">{{ row.paidAmount | number:'1.2-2' }}</td>
                        <td class="text-end text-warning">{{ row.returnAmount | number:'1.2-2' }}</td>
                        <td class="text-end">{{ row.adjustmentAmount | number:'1.2-2' }}</td>
                        <td class="text-end fw-bold" [class.text-danger]="row.closingBalance > 0">
                          {{ row.closingBalance | number:'1.2-2' }}
                        </td>
                        <td class="text-center">
                          <span
                            class="badge"
                            [class.bg-danger]="row.drOrCr === 'Cr'"
                            [class.bg-info]="row.drOrCr === 'Dr'"
                          >
                            {{ row.drOrCr }}
                          </span>
                        </td>
                      </tr>
                    }
                  }
                </tbody>
                @if (report()!.rows.length > 0) {
                  <tfoot class="table-secondary fw-bold">
                    <tr>
                      <td colspan="2">{{ '::Total' | abpLocalization }}</td>
                      <td class="text-end">{{ report()!.totalRow.openingBalance | number:'1.2-2' }}</td>
                      <td class="text-end text-primary">{{ report()!.totalRow.invoicedAmount | number:'1.2-2' }}</td>
                      <td class="text-end text-success">{{ report()!.totalRow.paidAmount | number:'1.2-2' }}</td>
                      <td class="text-end text-warning">{{ report()!.totalRow.returnAmount | number:'1.2-2' }}</td>
                      <td class="text-end">{{ report()!.totalRow.adjustmentAmount | number:'1.2-2' }}</td>
                      <td class="text-end fw-bold" [class.text-danger]="report()!.totalRow.closingBalance > 0">
                        {{ report()!.totalRow.closingBalance | number:'1.2-2' }}
                      </td>
                      <td class="text-center">
                        <span
                          class="badge"
                          [class.bg-danger]="report()!.totalRow.drOrCr === 'Cr'"
                          [class.bg-info]="report()!.totalRow.drOrCr === 'Dr'"
                        >
                          {{ report()!.totalRow.drOrCr }}
                        </span>
                      </td>
                    </tr>
                  </tfoot>
                }
              </table>
            </div>
          </div>
        </div>
      }
    </div>
  `,
})
export class SupplierLedgerSummaryComponent implements OnInit {
  private ledgerService = inject(SupplierLedgerSummaryService);
  private supplierService = inject(SupplierService);
  private companyContext = inject(CompanyContextService);

  report = signal<SupplierLedgerSummaryReportDto | null>(null);
  suppliers = signal<SupplierDto[]>([]);
  isLoading = signal(false);

  fromDate: string = '';
  toDate: string = '';
  selectedSupplierId: string = '';

  ngOnInit(): void {
    const today = new Date();
    const firstDay = new Date(today.getFullYear(), today.getMonth(), 1);
    this.fromDate = firstDay.toISOString().split('T')[0];
    this.toDate = today.toISOString().split('T')[0];

    this.loadSuppliers();
    this.loadReport();
  }

  loadSuppliers(): void {
    this.supplierService
      .getList({ maxResultCount: 200 } as any)
      .subscribe({
        next: (res) => this.suppliers.set(res.items || []),
        error: () => {},
      });
  }

  resetFilters(): void {
    const today = new Date();
    const firstDay = new Date(today.getFullYear(), today.getMonth(), 1);
    this.fromDate = firstDay.toISOString().split('T')[0];
    this.toDate = today.toISOString().split('T')[0];
    this.selectedSupplierId = '';
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);

    this.ledgerService
      .getReport({
        companyId,
        fromDate: this.fromDate ? new Date(this.fromDate).toISOString() : undefined,
        toDate: this.toDate ? new Date(this.toDate).toISOString() : undefined,
        supplierId: this.selectedSupplierId ? this.selectedSupplierId : undefined,
      })
      .subscribe({
        next: (res) => {
          this.report.set(res);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
        },
      });
  }

  exportCsv(): void {
    const rep = this.report();
    if (!rep || !rep.rows.length) return;

    const data = rep.rows.map((r) => ({
      SupplierCode: r.supplierCode,
      SupplierName: r.supplierName,
      SupplierGroup: r.supplierGroupName ?? '',
      OpeningBalance: r.openingBalance,
      InvoicedAmount: r.invoicedAmount,
      PaidAmount: r.paidAmount,
      ReturnAmount: r.returnAmount,
      AdjustmentAmount: r.adjustmentAmount,
      ClosingBalance: r.closingBalance,
      DrOrCr: r.drOrCr,
      Currency: r.currency,
    }));

    // Add total row
    data.push({
      SupplierCode: 'TOTAL',
      SupplierName: 'Total',
      SupplierGroup: '',
      OpeningBalance: rep.totalRow.openingBalance,
      InvoicedAmount: rep.totalRow.invoicedAmount,
      PaidAmount: rep.totalRow.paidAmount,
      ReturnAmount: rep.totalRow.returnAmount,
      AdjustmentAmount: rep.totalRow.adjustmentAmount,
      ClosingBalance: rep.totalRow.closingBalance,
      DrOrCr: rep.totalRow.drOrCr,
      Currency: rep.companyCurrency,
    });

    exportToCsv(
      `Supplier_Ledger_Summary_${this.fromDate}_to_${this.toDate}.csv`,
      data,
      [
        'SupplierCode',
        'SupplierName',
        'SupplierGroup',
        'OpeningBalance',
        'InvoicedAmount',
        'PaidAmount',
        'ReturnAmount',
        'AdjustmentAmount',
        'ClosingBalance',
        'DrOrCr',
        'Currency',
      ]
    );
  }
}
