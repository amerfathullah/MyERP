import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LocalizationPipe } from '@abp/ng.core';

/**
 * Payment Entry print layout — professional A4 receipt/voucher format.
 * Includes: company header (TIN/SST), party info, payment details,
 * reference allocations table, mode of payment, and authorized signature.
 * Per ERPNext: PE receipt is the customer-facing proof of payment document.
 * Payment type determines layout: Receive = Receipt, Pay = Payment Voucher.
 */
@Component({
  selector: 'app-pe-print-layout',
  standalone: true,
  imports: [CommonModule, LocalizationPipe],
  template: `
    @if (payment) {
      <div class="pe-print-layout">
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
          </div>
          <div class="text-end">
            <h4 class="text-uppercase fw-bold mb-1" [class.text-success]="isReceive" [class.text-primary]="!isReceive">
              {{ (isReceive ? '::OfficialReceipt' : '::PaymentVoucher') | abpLocalization }}
            </h4>
            <p class="mb-0"><strong>{{ '::ReceiptNumberShort' | abpLocalization }}:</strong> {{ payment.paymentNumber }}</p>
            <p class="mb-0"><strong>{{ '::Date' | abpLocalization }}:</strong> {{ payment.postingDate | date:'dd/MM/yyyy' }}</p>
            @if (payment.referenceNumber) {
              <p class="mb-0"><strong>{{ '::Reference' | abpLocalization }}:</strong> {{ payment.referenceNumber }}</p>
            }
          </div>
        </div>
        <hr class="mb-4">
        <!-- Party Info -->
        <div class="row mb-4">
          <div class="col-6">
            <h6 class="text-uppercase text-muted fw-bold mb-2">{{ (isReceive ? '::ReceivedFrom' : '::PaidTo') | abpLocalization }}</h6>
            <p class="fw-bold mb-1">{{ payment.partyName || ('::NotAvailableShort' | abpLocalization) }}</p>
            @if (partyAddress) {
              <p class="text-muted mb-0">{{ partyAddress }}</p>
            }
          </div>
          <div class="col-6">
            <h6 class="text-uppercase text-muted fw-bold mb-2">{{ '::PaymentMethod' | abpLocalization }}</h6>
            <p class="fw-bold mb-1">{{ modeOfPayment || ('::BankTransfer' | abpLocalization) }}</p>
            @if (payment.referenceNumber) {
              <p class="text-muted mb-0">{{ '::RefShort' | abpLocalization }}: {{ payment.referenceNumber }}</p>
            }
            @if (payment.referenceDate) {
              <p class="text-muted mb-0">{{ '::ChequeDate' | abpLocalization }}: {{ payment.referenceDate | date:'dd/MM/yyyy' }}</p>
            }
          </div>
        </div>
        <!-- Amount Hero -->
        <div class="text-center border rounded p-3 mb-4 bg-light">
          <p class="text-muted mb-1">{{ (isReceive ? '::AmountReceived' : '::AmountPaid') | abpLocalization }}</p>
          <h2 class="fw-bold mb-1">{{ payment.currency || 'MYR' }} {{ payment.paidAmount | number:'1.2-2' }}</h2>
          @if (amountInWords) {
            <p class="text-muted mb-0 fst-italic">{{ amountInWords }}</p>
          }
        </div>
        <!-- Allocation References Table (if multi-invoice) -->
        @if (payment.references && payment.references.length > 0) {
          <div class="mb-4">
            <h6 class="text-uppercase text-muted fw-bold mb-2">{{ '::AgainstInvoices' | abpLocalization }}</h6>
            <table class="table table-bordered table-sm">
              <thead class="table-light">
                <tr>
                  <th>#</th>
                  <th>{{ '::Invoice' | abpLocalization }}</th>
                  <th>{{ '::Type' | abpLocalization }}</th>
                  <th class="text-end">{{ '::InvoiceAmount' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Allocated' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (ref of payment.references; track ref; let i = $index) {
                  <tr>
                    <td>{{ i + 1 }}</td>
                    <td>{{ ref.referenceNumber || ref.referenceId }}</td>
                    <td>{{ ref.referenceType }}</td>
                    <td class="text-end">{{ ref.totalAmount | number:'1.2-2' }}</td>
                    <td class="text-end fw-bold">{{ ref.allocatedAmount | number:'1.2-2' }}</td>
                  </tr>
                }
              </tbody>
              <tfoot class="table-light">
                <tr>
                  <td colspan="4" class="text-end fw-bold">{{ '::TotalAllocated' | abpLocalization }}:</td>
                  <td class="text-end fw-bold">{{ totalAllocated | number:'1.2-2' }}</td>
                </tr>
              </tfoot>
            </table>
          </div>
        }
        <!-- Single Invoice Reference (legacy path) -->
        @if (!payment.references?.length && payment.againstInvoiceId) {
          <div class="mb-4">
            <h6 class="text-uppercase text-muted fw-bold mb-2">{{ '::Against' | abpLocalization }}</h6>
            <p class="mb-0">{{ '::Invoice' | abpLocalization }}: <strong>{{ againstInvoiceNumber || payment.againstInvoiceId }}</strong></p>
          </div>
        }
        <!-- Account Details -->
        <div class="row mb-4">
          <div class="col-6">
            <h6 class="text-uppercase text-muted fw-bold mb-2">{{ '::FromAccount' | abpLocalization }}</h6>
            <p class="mb-0">{{ paidFromAccountName || ('::CompanyAccount' | abpLocalization) }}</p>
          </div>
          <div class="col-6">
            <h6 class="text-uppercase text-muted fw-bold mb-2">{{ '::ToAccount' | abpLocalization }}</h6>
            <p class="mb-0">{{ paidToAccountName || ('::PartyAccount' | abpLocalization) }}</p>
          </div>
        </div>
        <!-- Exchange Rate (multi-currency) -->
        @if (payment.exchangeRate && payment.exchangeRate !== 1) {
          <div class="mb-4">
            <h6 class="text-uppercase text-muted fw-bold mb-2">{{ '::ExchangeRate' | abpLocalization }}</h6>
            <p class="mb-0">1 {{ payment.currency }} = {{ payment.exchangeRate | number:'1.4-4' }} MYR</p>
            <p class="mb-0">{{ '::BaseAmount' | abpLocalization }}: MYR {{ baseAmount | number:'1.2-2' }}</p>
          </div>
        }
        <!-- Remarks -->
        @if (payment.notes) {
          <div class="mb-4">
            <h6 class="text-uppercase text-muted fw-bold mb-2">{{ '::Remarks' | abpLocalization }}</h6>
            <p class="mb-0">{{ payment.notes }}</p>
          </div>
        }
        <!-- Signature Block -->
        <div class="row mt-5 pt-4">
          <div class="col-4 text-center">
            <div class="border-top pt-2 mx-3">
              <p class="text-muted mb-0 small">{{ '::PreparedBy' | abpLocalization }}</p>
            </div>
          </div>
          <div class="col-4 text-center">
            <div class="border-top pt-2 mx-3">
              <p class="text-muted mb-0 small">{{ '::AuthorizedSignatory' | abpLocalization }}</p>
            </div>
          </div>
          <div class="col-4 text-center">
            <div class="border-top pt-2 mx-3">
              <p class="text-muted mb-0 small">{{ (isReceive ? '::ReceivedBy' : '::Acknowledgement') | abpLocalization }}</p>
            </div>
          </div>
        </div>
        <!-- Footer -->
        <div class="text-center mt-4 pt-3 border-top">
          <p class="text-muted small mb-0">{{ '::ComputerGeneratedDocument' | abpLocalization }} {{ '::NoSignatureRequired' | abpLocalization }}</p>
          <p class="text-muted small mb-0">{{ (isReceive ? '::ThankYouForPayment' : '::ThankYouForBusiness') | abpLocalization }}</p>
        </div>
      </div>
    }
    `,
  changeDetection: ChangeDetectionStrategy.Eager,
  styles: [`
    .pe-print-layout {
      font-family: 'Segoe UI', Tahoma, sans-serif;
      font-size: 12px;
      max-width: 210mm;
      margin: 0 auto;
      padding: 15mm;
    }
    @media screen {
      .pe-print-layout { display: none; }
    }
    @media print {
      .pe-print-layout { display: block !important; }
      .pe-print-layout table { page-break-inside: avoid; }
    }
  `]
})
export class PaymentEntryPrintLayoutComponent {
  @Input() payment: any;
  @Input() companyName = '';
  @Input() companyTin = '';
  @Input() companySst = '';
  @Input() companyAddress = '';
  @Input() companyPhone = '';
  @Input() partyAddress = '';
  @Input() modeOfPayment = '';
  @Input() paidFromAccountName = '';
  @Input() paidToAccountName = '';
  @Input() againstInvoiceNumber = '';
  @Input() amountInWords = '';
  @Input() companyEmail = '';

  get isReceive(): boolean {
    return this.payment?.paymentType === 'Receive' || this.payment?.paymentType === 0;
  }

  get totalAllocated(): number {
    if (!this.payment?.references?.length) return 0;
    return this.payment.references.reduce((sum: number, ref: any) => sum + (ref.allocatedAmount || 0), 0);
  }

  get baseAmount(): number {
    return (this.payment?.paidAmount || 0) * (this.payment?.exchangeRate || 1);
  }
}
