import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { TaxSummaryReportService } from '../../proxy/tax/tax-summary-report.service';
import { exportToCsv } from '../../shared/utils/csv-export';

interface Sst02FilingData {
  companyId: string;
  companyName: string;
  sstRegistrationNumber: string;
  taxPeriod: string;
  fromDate: string;
  toDate: string;
  taxableSupplies6Percent: number;
  taxableSupplies10Percent: number;
  taxableSupplies5Percent: number;
  taxableSuppliesOtherRate: number;
  exemptSupplies: number;
  zeroRatedSupplies: number;
  outputTax6Percent: number;
  outputTax10Percent: number;
  outputTax5Percent: number;
  outputTaxOther: number;
  totalOutputTax: number;
  inputTaxCredit: number;
  creditNoteAdjustment: number;
  debitNoteAdjustment: number;
  netAdjustment: number;
  netTaxPayable: number;
  isRefundable: boolean;
  totalSalesInvoices: number;
  totalPurchaseInvoices: number;
  totalCreditNotes: number;
  totalDebitNotes: number;
}

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-sst02-filing',
  template: `
    <div class="card mb-4">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0"><i class="fa fa-file-invoice me-2"></i>{{ 'SST02Filing' | abpLocalization }}</h5>
        @if (filing()) {
          <div>
            <button class="btn btn-sm btn-outline-secondary me-2" (click)="exportCsv()">
              <i class="fa fa-download me-1"></i>{{ 'ExportCSV' | abpLocalization }}
            </button>
            <button class="btn btn-sm btn-outline-primary" (click)="printFiling()">
              <i class="fa fa-print me-1"></i>{{ 'Print' | abpLocalization }}
            </button>
          </div>
        }
      </div>
      <div class="card-body">
        <!-- Period Selector -->
        <div class="row g-3 mb-4">
          <div class="col-md-3">
            <label class="form-label">{{ 'From' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="fromDate" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'To' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="toDate" />
          </div>
          <div class="col-md-3 d-flex align-items-end">
            <button class="btn btn-primary" (click)="generate()" [disabled]="loading()">
              @if (loading()) { <i class="fa fa-spinner fa-spin me-1"></i> }
              {{ 'GenerateReport' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (filing(); as f) {
          <!-- Net Position Alert -->
          <div class="alert" [class.alert-danger]="!f.isRefundable" [class.alert-success]="f.isRefundable">
            <div class="d-flex justify-content-between align-items-center">
              <div>
                <strong>{{ f.isRefundable ? 'Refundable' : 'Tax Payable' }}</strong>
                <span class="ms-2 text-muted">{{ f.taxPeriod }}</span>
              </div>
              <h4 class="mb-0">RM {{ (f.isRefundable ? -f.netTaxPayable : f.netTaxPayable) | number:'1.2-2' }}</h4>
            </div>
          </div>

          <!-- SST-02 Form Sections -->
          <div class="row g-4">
            <!-- Section A: Taxable Supplies -->
            <div class="col-md-6">
              <div class="card border">
                <div class="card-header bg-light">
                  <h6 class="mb-0">Section A: Taxable Supplies</h6>
                </div>
                <div class="card-body p-0">
                  <table class="table table-sm mb-0">
                    <thead><tr><th>Rate</th><th class="text-end">Taxable Value (RM)</th><th class="text-end">Tax (RM)</th></tr></thead>
                    <tbody>
                      @if (f.taxableSupplies6Percent > 0) {
                        <tr><td>Service Tax 6%</td><td class="text-end">{{ f.taxableSupplies6Percent | number:'1.2-2' }}</td><td class="text-end">{{ f.outputTax6Percent | number:'1.2-2' }}</td></tr>
                      }
                      @if (f.taxableSupplies10Percent > 0) {
                        <tr><td>Sales Tax 10%</td><td class="text-end">{{ f.taxableSupplies10Percent | number:'1.2-2' }}</td><td class="text-end">{{ f.outputTax10Percent | number:'1.2-2' }}</td></tr>
                      }
                      @if (f.taxableSupplies5Percent > 0) {
                        <tr><td>Sales Tax 5%</td><td class="text-end">{{ f.taxableSupplies5Percent | number:'1.2-2' }}</td><td class="text-end">{{ f.outputTax5Percent | number:'1.2-2' }}</td></tr>
                      }
                      @if (f.taxableSuppliesOtherRate > 0) {
                        <tr><td>Other Rates</td><td class="text-end">{{ f.taxableSuppliesOtherRate | number:'1.2-2' }}</td><td class="text-end">{{ f.outputTaxOther | number:'1.2-2' }}</td></tr>
                      }
                      <tr class="fw-bold table-light"><td>Total Output Tax (D)</td><td></td><td class="text-end">{{ f.totalOutputTax | number:'1.2-2' }}</td></tr>
                    </tbody>
                  </table>
                </div>
              </div>
            </div>

            <!-- Section B+C: Exempt & Zero-Rated -->
            <div class="col-md-6">
              <div class="card border">
                <div class="card-header bg-light">
                  <h6 class="mb-0">Section B & C: Non-Taxable Supplies</h6>
                </div>
                <div class="card-body p-0">
                  <table class="table table-sm mb-0">
                    <tbody>
                      <tr><td>Exempt Supplies (B)</td><td class="text-end">{{ f.exemptSupplies | number:'1.2-2' }}</td></tr>
                      <tr><td>Zero-Rated Supplies (C)</td><td class="text-end">{{ f.zeroRatedSupplies | number:'1.2-2' }}</td></tr>
                    </tbody>
                  </table>
                </div>
              </div>

              <!-- Section E: Input Tax -->
              <div class="card border mt-3">
                <div class="card-header bg-light">
                  <h6 class="mb-0">Section E: Input Tax Credit</h6>
                </div>
                <div class="card-body p-0">
                  <table class="table table-sm mb-0">
                    <tbody>
                      <tr><td>Input Tax on Purchases</td><td class="text-end">{{ f.inputTaxCredit | number:'1.2-2' }}</td></tr>
                    </tbody>
                  </table>
                </div>
              </div>
            </div>

            <!-- Section F: Adjustments -->
            <div class="col-md-6">
              <div class="card border">
                <div class="card-header bg-light">
                  <h6 class="mb-0">Section F: Adjustments</h6>
                </div>
                <div class="card-body p-0">
                  <table class="table table-sm mb-0">
                    <tbody>
                      <tr><td>Credit Note Adjustment</td><td class="text-end text-danger">-{{ f.creditNoteAdjustment | number:'1.2-2' }}</td></tr>
                      <tr><td>Debit Note Adjustment</td><td class="text-end text-success">+{{ f.debitNoteAdjustment | number:'1.2-2' }}</td></tr>
                      <tr class="fw-bold"><td>Net Adjustment</td><td class="text-end">{{ f.netAdjustment | number:'1.2-2' }}</td></tr>
                    </tbody>
                  </table>
                </div>
              </div>
            </div>

            <!-- Section G: Net Tax Payable -->
            <div class="col-md-6">
              <div class="card border">
                <div class="card-header bg-light">
                  <h6 class="mb-0">Section G: Net Tax Position</h6>
                </div>
                <div class="card-body p-0">
                  <table class="table table-sm mb-0">
                    <tbody>
                      <tr><td>Total Output Tax (D)</td><td class="text-end">{{ f.totalOutputTax | number:'1.2-2' }}</td></tr>
                      <tr><td>Less: Input Tax Credit (E)</td><td class="text-end">-{{ f.inputTaxCredit | number:'1.2-2' }}</td></tr>
                      <tr><td>Adjustments (F)</td><td class="text-end">{{ f.netAdjustment | number:'1.2-2' }}</td></tr>
                      <tr class="fw-bold table-warning"><td>Net Tax Payable/Refundable (G)</td><td class="text-end">{{ f.netTaxPayable | number:'1.2-2' }}</td></tr>
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          </div>

          <!-- Document Counts -->
          <div class="row mt-3">
            <div class="col-md-3"><small class="text-muted">Sales Invoices: {{ f.totalSalesInvoices }}</small></div>
            <div class="col-md-3"><small class="text-muted">Purchase Invoices: {{ f.totalPurchaseInvoices }}</small></div>
            <div class="col-md-3"><small class="text-muted">Credit Notes: {{ f.totalCreditNotes }}</small></div>
            <div class="col-md-3"><small class="text-muted">Debit Notes: {{ f.totalDebitNotes }}</small></div>
          </div>
        }
      </div>
    </div>
  `
})
export class Sst02FilingComponent implements OnInit {
  private taxSummaryReportService = inject(TaxSummaryReportService);
  private companyContext = inject(CompanyContextService);

  fromDate = '';
  toDate = '';
  loading = signal(false);
  filing = signal<Sst02FilingData | null>(null);

  ngOnInit() {
    // Default to current bimonthly period
    const now = new Date();
    const bimonthStart = now.getMonth() % 2 === 0 ? now.getMonth() : now.getMonth() - 1;
    this.fromDate = new Date(now.getFullYear(), bimonthStart, 1).toISOString().split('T')[0];
    this.toDate = new Date(now.getFullYear(), bimonthStart + 2, 0).toISOString().split('T')[0];
    this.generate();
  }

  generate() {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId || !this.fromDate || !this.toDate) return;

    this.loading.set(true);
    this.taxSummaryReportService.getSst02FilingData(companyId, this.fromDate, this.toDate).subscribe({
      next: (data) => { this.filing.set(data as any); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  exportCsv() {
    const f = this.filing();
    if (!f) return;
    exportToCsv(`SST02_${f.taxPeriod.replace(/\s/g, '_')}.csv`, [
      { section: 'A', description: 'Service Tax 6%', taxableValue: f.taxableSupplies6Percent, tax: f.outputTax6Percent },
      { section: 'A', description: 'Sales Tax 10%', taxableValue: f.taxableSupplies10Percent, tax: f.outputTax10Percent },
      { section: 'A', description: 'Sales Tax 5%', taxableValue: f.taxableSupplies5Percent, tax: f.outputTax5Percent },
      { section: 'B', description: 'Exempt Supplies', taxableValue: f.exemptSupplies, tax: 0 },
      { section: 'C', description: 'Zero-Rated Supplies', taxableValue: f.zeroRatedSupplies, tax: 0 },
      { section: 'D', description: 'Total Output Tax', taxableValue: 0, tax: f.totalOutputTax },
      { section: 'E', description: 'Input Tax Credit', taxableValue: 0, tax: f.inputTaxCredit },
      { section: 'F', description: 'Net Adjustment', taxableValue: 0, tax: f.netAdjustment },
      { section: 'G', description: 'Net Tax Payable', taxableValue: 0, tax: f.netTaxPayable },
    ], ['section', 'description', 'taxableValue', 'tax']);
  }

  printFiling() {
    window.print();
  }
}
