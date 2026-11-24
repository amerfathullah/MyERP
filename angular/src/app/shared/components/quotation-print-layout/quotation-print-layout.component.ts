import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LocalizationPipe } from '@abp/ng.core';

/**
 * Quotation print layout — professional A4 format for customer proposals.
 * Includes: company header (TIN/SST), customer info, validity period,
 * items table with discount column, terms & conditions, and total breakdown.
 * Per ERPNext: quotation is the first customer-facing document in the sales cycle.
 */
@Component({
  selector: 'app-quotation-print-layout',
  standalone: true,
  imports: [CommonModule, LocalizationPipe],
  template: `
    @if (quotation) {
      <div class="quotation-print-layout">
        <!-- Company Header -->
        <div class="print-header d-flex justify-content-between align-items-start mb-4">
          <div>
            <h3 class="fw-bold mb-1">{{ companyName }}</h3>
            @if (companyTin) {
              <p class="text-muted mb-0">{{ '::Tin' | abpLocalization }}: {{ companyTin }}</p>
            }
            @if (companySst) {
              <p class="text-muted mb-0">{{ '::SstRegNo' | abpLocalization }}: {{ companySst }}</p>
            }
            @if (companyAddress) {
              <p class="text-muted mb-0">{{ companyAddress }}</p>
            }
            @if (companyPhone) {
              <p class="text-muted mb-0">{{ '::Tel' | abpLocalization }}: {{ companyPhone }}</p>
            }
            @if (companyEmail) {
              <p class="text-muted mb-0">{{ '::Email' | abpLocalization }}: {{ companyEmail }}</p>
            }
          </div>
          <div class="text-end">
            <h4 class="text-uppercase text-primary fw-bold mb-1">{{ '::Quotation' | abpLocalization }}</h4>
            <p class="mb-0"><strong>{{ '::RefNumberShort' | abpLocalization }}:</strong> {{ quotation.quotationNumber }}</p>
            <p class="mb-0"><strong>{{ '::Date' | abpLocalization }}:</strong> {{ quotation.issueDate | date:'dd/MM/yyyy' }}</p>
            @if (quotation.validUntil) {
              <p class="mb-0"><strong>{{ '::ValidUntil' | abpLocalization }}:</strong> {{ quotation.validUntil | date:'dd/MM/yyyy' }}</p>
            }
          </div>
        </div>
        <hr class="mb-4">
        <!-- Customer Info -->
        <div class="row mb-4">
          <div class="col-7">
            <h6 class="text-uppercase text-muted fw-bold mb-2">{{ '::QuotationTo' | abpLocalization }}</h6>
            <p class="fw-bold mb-1">{{ quotation.customerName || customerName || ('::NotAvailableShort' | abpLocalization) }}</p>
            @if (customerAddress) {
              <p class="text-muted mb-0">{{ customerAddress }}</p>
            }
            @if (customerTin) {
              <p class="text-muted mb-0">{{ '::Tin' | abpLocalization }}: {{ customerTin }}</p>
            }
            @if (contactPerson) {
              <p class="text-muted mb-0">{{ '::Attn' | abpLocalization }}: {{ contactPerson }}</p>
            }
          </div>
          <div class="col-5 text-end">
            <div class="border rounded p-3 d-inline-block">
              <small class="text-muted d-block">{{ '::PaymentTerms' | abpLocalization }}</small>
              <span class="fw-semibold">{{ paymentTerms || ('::AsPerAgreement' | abpLocalization) }}</span>
            </div>
          </div>
        </div>
        <!-- Subject / Introduction -->
        @if (quotation.notes || introText) {
          <div class="mb-4">
            <p class="mb-0">{{ introText || quotation.notes }}</p>
          </div>
        }
        <!-- Items Table -->
        <table class="table table-bordered mb-4">
          <thead class="table-light">
            <tr>
              <th style="width: 40px" class="text-center">{{ '::SerialNoShort' | abpLocalization }}</th>
              <th>{{ '::Description' | abpLocalization }}</th>
              <th class="text-end" style="width: 70px">{{ '::Qty' | abpLocalization }}</th>
              <th class="text-end" style="width: 70px">{{ '::Uom' | abpLocalization }}</th>
              <th class="text-end" style="width: 110px">{{ '::UnitPrice' | abpLocalization }}</th>
              @if (hasDiscounts) {
                <th class="text-end" style="width: 80px">{{ '::DiscPercent' | abpLocalization }}</th>
              }
              <th class="text-end" style="width: 120px">{{ '::Amount' | abpLocalization }} ({{ currency }})</th>
            </tr>
          </thead>
          <tbody>
            @for (item of quotation.items; track item; let i = $index) {
              <tr>
                <td class="text-center">{{ i + 1 }}</td>
                <td>
                  <span class="fw-semibold">{{ item.itemName || item.description || '—' }}</span>
                  @if (item.description && item.description !== item.itemName) {
                    <br>
                    }
                    @if (item.description && item.description !== item.itemName) {
                      <small class="text-muted">{{ item.description }}</small>
                    }
                  </td>
                  <td class="text-end font-monospace">{{ item.quantity | number:'1.0-2' }}</td>
                  <td class="text-end">{{ item.uom || 'Unit' }}</td>
                  <td class="text-end font-monospace">{{ item.unitPrice | number:'1.2-2' }}</td>
                  @if (hasDiscounts) {
                    <td class="text-end font-monospace">{{ item.discountPercent ? (item.discountPercent | number:'1.0-1') + '%' : '—' }}</td>
                  }
                  <td class="text-end font-monospace">{{ getLineTotal(item) | number:'1.2-2' }}</td>
                </tr>
              }
            </tbody>
          </table>
          <!-- Totals -->
          <div class="row">
            <div class="col-6">
              <!-- Terms & Conditions -->
              @if (termsAndConditions) {
                <div>
                  <h6 class="text-uppercase text-muted fw-bold mb-2">{{ '::TermsAndConditions' | abpLocalization }}</h6>
                  <div class="small" [innerHTML]="termsAndConditions"></div>
                </div>
              }
            </div>
            <div class="col-6">
              <table class="table table-sm mb-0">
                <tbody>
                  <tr>
                    <td class="text-end border-0"><strong>{{ '::Subtotal' | abpLocalization }}:</strong></td>
                    <td class="text-end border-0 font-monospace" style="width: 140px">{{ currency }} {{ quotation.netTotal | number:'1.2-2' }}</td>
                  </tr>
                  @if (quotation.discountAmount) {
                    <tr>
                      <td class="text-end border-0"><strong>{{ '::Discount' | abpLocalization }}:</strong></td>
                      <td class="text-end border-0 font-monospace text-danger">- {{ currency }} {{ quotation.discountAmount | number:'1.2-2' }}</td>
                    </tr>
                  }
                  @if (quotation.taxAmount) {
                    <tr>
                      <td class="text-end border-0"><strong>{{ '::Tax' | abpLocalization }} ({{ '::Sst' | abpLocalization }} {{ taxRate }}%):</strong></td>
                      <td class="text-end border-0 font-monospace">{{ currency }} {{ quotation.taxAmount | number:'1.2-2' }}</td>
                    </tr>
                  }
                  <tr class="border-top">
                    <td class="text-end"><strong class="fs-5">{{ '::GrandTotal' | abpLocalization }}:</strong></td>
                    <td class="text-end font-monospace fs-5 fw-bold">{{ currency }} {{ quotation.grandTotal | number:'1.2-2' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
          <!-- Validity Notice -->
          @if (quotation.validUntil) {
            <div class="mt-4 p-3 border rounded bg-light">
              <p class="mb-0 text-center small">
                <i class="fa fa-info-circle me-1"></i>
                {{ '::QuotationValidUntilPrefix' | abpLocalization }} <strong>{{ quotation.validUntil | date:'dd MMMM yyyy' }}</strong>.
                {{ '::QuotationPricesSubjectToChange' | abpLocalization }}
              </p>
            </div>
          }
          <!-- Acceptance -->
          <div class="mt-5 pt-4 border-top">
            <div class="row">
              <div class="col-6">
                <h6 class="fw-bold mb-3">{{ '::ForCompanyPrefix' | abpLocalization }} {{ companyName }}</h6>
                <div class="border-bottom mt-4" style="width: 200px;"></div>
                <p class="text-muted small mt-1">{{ '::AuthorizedSignatory' | abpLocalization }}</p>
              </div>
              <div class="col-6">
                <h6 class="fw-bold mb-3">{{ '::AcceptedByCustomer' | abpLocalization }}</h6>
                <div class="border-bottom mt-4" style="width: 200px;"></div>
                <p class="text-muted small mt-1">{{ '::NameSignatureDateStamp' | abpLocalization }}</p>
              </div>
            </div>
          </div>
          <!-- Footer -->
          <div class="mt-3 text-center">
            <p class="text-muted small mb-0">{{ '::ThankYouProposal' | abpLocalization }}</p>
          </div>
        </div>
      }
    `,
  changeDetection: ChangeDetectionStrategy.Eager,
  styles: [`
    :host { display: none; }
    @media print {
      :host { display: block !important; }
      .quotation-print-layout {
        font-size: 11px;
        padding: 15mm;
      }
      .print-header h3 { font-size: 16px; }
      .print-header h4 { font-size: 14px; }
      .table th, .table td { padding: 5px 8px; }
      .table-bordered { border: 1px solid #000 !important; }
      .table-bordered th, .table-bordered td { border: 1px solid #000 !important; }
      .bg-light { background-color: #f8f9fa !important; -webkit-print-color-adjust: exact; }
    }
  `]
})
export class QuotationPrintLayoutComponent {
  @Input() quotation: any;
  @Input() companyName = '';
  @Input() companyTin = '';
  @Input() companySst = '';
  @Input() companyAddress = '';
  @Input() companyPhone = '';
  @Input() companyEmail = '';
  @Input() customerName = '';
  @Input() customerAddress = '';
  @Input() customerTin = '';
  @Input() contactPerson = '';
  @Input() paymentTerms = '';
  @Input() termsAndConditions = '';
  @Input() introText = '';
  @Input() currency = 'MYR';
  @Input() taxRate = 6;

  get hasDiscounts(): boolean {
    return (this.quotation?.items || []).some((item: any) => item.discountPercent > 0);
  }

  getLineTotal(item: any): number {
    const qty = item.quantity || 0;
    const rate = item.unitPrice || 0;
    const disc = item.discountPercent || 0;
    return qty * rate * (1 - disc / 100);
  }
}
