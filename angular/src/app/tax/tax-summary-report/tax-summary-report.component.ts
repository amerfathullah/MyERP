import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { TaxSummaryReportService } from '../../proxy/tax/tax-summary-report.service';
import { exportToCsv } from '../../shared/utils/csv-export';

interface TaxRateBreakdown {
  taxRate: string;
  taxableAmount: number;
  taxAmount: number;
  invoiceCount: number;
}

interface TaxSummary {
  fromDate: string;
  toDate: string;
  totalSalesAmount: number;
  outputTax: number;
  creditNoteTaxAdjustment: number;
  netOutputTax: number;
  salesInvoiceCount: number;
  creditNoteCount: number;
  totalPurchaseAmount: number;
  inputTax: number;
  debitNoteTaxAdjustment: number;
  netInputTax: number;
  purchaseInvoiceCount: number;
  debitNoteCount: number;
  netTaxPayable: number;
  isRefundable: boolean;
  outputTaxBreakdown: TaxRateBreakdown[];
  inputTaxBreakdown: TaxRateBreakdown[];
}

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-tax-summary-report',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0">{{ 'TaxSummaryReport' | abpLocalization }}</h5>
        @if (result()) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ 'ExportCSV' | abpLocalization }}
          </button>
        }
      </div>
      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'From' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate">
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'To' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate">
          </div>
          <div class="col-md-4 d-flex align-items-end">
            <button class="btn btn-primary btn-sm w-100" (click)="generate()">
              {{ 'GenerateReport' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (result(); as r) {
          <!-- Net Position Card -->
          <div class="alert mb-3" [class.alert-danger]="!r.isRefundable" [class.alert-success]="r.isRefundable">
            <div class="d-flex justify-content-between align-items-center">
              <div>
                <strong>{{ r.isRefundable ? 'Tax Refundable' : 'Tax Payable' }}</strong>
                <small class="d-block text-muted">{{ 'NetTaxPosition' | abpLocalization }}</small>
              </div>
              <h4 class="mb-0">MYR {{ (r.isRefundable ? -r.netTaxPayable : r.netTaxPayable) | number:'1.2-2' }}</h4>
            </div>
          </div>

          <div class="row g-3">
            <!-- Output Tax (Sales) -->
            <div class="col-md-6">
              <div class="card h-100">
                <div class="card-header bg-primary bg-opacity-10">
                  <strong>{{ 'OutputTax' | abpLocalization }} ({{ 'Sales' | abpLocalization }})</strong>
                </div>
                <div class="card-body">
                  <table class="table table-sm mb-2">
                    <tr><td>{{ 'TotalSales' | abpLocalization }}</td><td class="text-end">{{ r.totalSalesAmount | number:'1.2-2' }}</td></tr>
                    <tr><td>{{ 'TaxCollected' | abpLocalization }} ({{ r.salesInvoiceCount }} invoices)</td><td class="text-end">{{ r.outputTax | number:'1.2-2' }}</td></tr>
                    <tr><td>{{ 'CreditNoteAdjustment' | abpLocalization }} ({{ r.creditNoteCount }})</td><td class="text-end text-danger">-{{ r.creditNoteTaxAdjustment | number:'1.2-2' }}</td></tr>
                    <tr class="table-primary"><td><strong>{{ 'NetOutputTax' | abpLocalization }}</strong></td><td class="text-end"><strong>{{ r.netOutputTax | number:'1.2-2' }}</strong></td></tr>
                  </table>
                  @if (r.outputTaxBreakdown.length) {
                    <small class="text-muted">{{ 'ByRate' | abpLocalization }}:</small>
                    @for (b of r.outputTaxBreakdown; track b.taxRate) {
                      <div class="d-flex justify-content-between small">
                        <span>{{ b.taxRate }} ({{ b.invoiceCount }})</span>
                        <span>{{ b.taxAmount | number:'1.2-2' }}</span>
                      </div>
                    }
                  }
                </div>
              </div>
            </div>

            <!-- Input Tax (Purchases) -->
            <div class="col-md-6">
              <div class="card h-100">
                <div class="card-header bg-success bg-opacity-10">
                  <strong>{{ 'InputTax' | abpLocalization }} ({{ 'Purchases' | abpLocalization }})</strong>
                </div>
                <div class="card-body">
                  <table class="table table-sm mb-2">
                    <tr><td>{{ 'TotalPurchases' | abpLocalization }}</td><td class="text-end">{{ r.totalPurchaseAmount | number:'1.2-2' }}</td></tr>
                    <tr><td>{{ 'TaxPaid' | abpLocalization }} ({{ r.purchaseInvoiceCount }} invoices)</td><td class="text-end">{{ r.inputTax | number:'1.2-2' }}</td></tr>
                    <tr><td>{{ 'DebitNoteAdjustment' | abpLocalization }} ({{ r.debitNoteCount }})</td><td class="text-end text-danger">-{{ r.debitNoteTaxAdjustment | number:'1.2-2' }}</td></tr>
                    <tr class="table-success"><td><strong>{{ 'NetInputTax' | abpLocalization }}</strong></td><td class="text-end"><strong>{{ r.netInputTax | number:'1.2-2' }}</strong></td></tr>
                  </table>
                  @if (r.inputTaxBreakdown.length) {
                    <small class="text-muted">{{ 'ByRate' | abpLocalization }}:</small>
                    @for (b of r.inputTaxBreakdown; track b.taxRate) {
                      <div class="d-flex justify-content-between small">
                        <span>{{ b.taxRate }} ({{ b.invoiceCount }})</span>
                        <span>{{ b.taxAmount | number:'1.2-2' }}</span>
                      </div>
                    }
                  }
                </div>
              </div>
            </div>
          </div>
        } @else {
          <p class="text-muted text-center py-4">{{ 'SelectPeriodToGenerateTaxSummary' | abpLocalization }}</p>
        }
      </div>
    </div>
  `
})
export class TaxSummaryReportComponent implements OnInit {
  private taxSummaryReportService = inject(TaxSummaryReportService);
  private companyContext = inject(CompanyContextService);

  result = signal<TaxSummary | null>(null);
  fromDate = new Date(new Date().getFullYear(), new Date().getMonth() - 2, 1).toISOString().substring(0, 10);
  toDate = new Date().toISOString().substring(0, 10);

  ngOnInit() { this.generate(); }

  generate() {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;
    this.taxSummaryReportService.getTaxSummary(companyId, this.fromDate, this.toDate).subscribe({ next: data => this.result.set(data as any), error: () => {} });
  }

  exportCsv() {
    const r = this.result();
    if (!r) return;
    const rows = [
      { Category: 'Output Tax (Sales)', Amount: r.outputTax },
      { Category: 'Credit Note Adjustment', Amount: -r.creditNoteTaxAdjustment },
      { Category: 'Net Output Tax', Amount: r.netOutputTax },
      { Category: 'Input Tax (Purchases)', Amount: r.inputTax },
      { Category: 'Debit Note Adjustment', Amount: -r.debitNoteTaxAdjustment },
      { Category: 'Net Input Tax', Amount: r.netInputTax },
      { Category: 'Net Tax Payable', Amount: r.netTaxPayable },
    ];
    exportToCsv(`tax-summary-${this.fromDate}-to-${this.toDate}.csv`, rows, ['Category', 'Amount']);
  }
}
