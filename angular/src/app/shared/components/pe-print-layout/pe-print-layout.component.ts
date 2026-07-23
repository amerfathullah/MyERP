import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

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
  imports: [CommonModule],
  template: `
    <div class="pe-print-layout" *ngIf="payment">
      <!-- Company Header -->
      <div class="print-header d-flex justify-content-between align-items-start mb-4">
        <div>
          <h3 class="fw-bold mb-1">{{ companyName }}</h3>
          <p class="text-muted mb-0" *ngIf="companyTin">TIN: {{ companyTin }}</p>
          <p class="text-muted mb-0" *ngIf="companySst">SST Reg No: {{ companySst }}</p>
          <p class="text-muted mb-0" *ngIf="companyAddress">{{ companyAddress }}</p>
          <p class="text-muted mb-0" *ngIf="companyPhone">Tel: {{ companyPhone }}</p>
        </div>
        <div class="text-end">
          <h4 class="text-uppercase fw-bold mb-1" [class.text-success]="isReceive" [class.text-primary]="!isReceive">
            {{ isReceive ? 'OFFICIAL RECEIPT' : 'PAYMENT VOUCHER' }}
          </h4>
          <p class="mb-0"><strong>Receipt #:</strong> {{ payment.paymentNumber }}</p>
          <p class="mb-0"><strong>Date:</strong> {{ payment.postingDate | date:'dd/MM/yyyy' }}</p>
          <p class="mb-0" *ngIf="payment.referenceNumber"><strong>Reference:</strong> {{ payment.referenceNumber }}</p>
        </div>
      </div>

      <hr class="mb-4">

      <!-- Party Info -->
      <div class="row mb-4">
        <div class="col-6">
          <h6 class="text-uppercase text-muted fw-bold mb-2">{{ isReceive ? 'Received From' : 'Paid To' }}</h6>
          <p class="fw-bold mb-1">{{ payment.partyName || 'N/A' }}</p>
          <p class="text-muted mb-0" *ngIf="partyAddress">{{ partyAddress }}</p>
        </div>
        <div class="col-6">
          <h6 class="text-uppercase text-muted fw-bold mb-2">Payment Method</h6>
          <p class="fw-bold mb-1">{{ modeOfPayment || 'Bank Transfer' }}</p>
          <p class="text-muted mb-0" *ngIf="payment.referenceNumber">Ref: {{ payment.referenceNumber }}</p>
          <p class="text-muted mb-0" *ngIf="payment.referenceDate">Cheque Date: {{ payment.referenceDate | date:'dd/MM/yyyy' }}</p>
        </div>
      </div>

      <!-- Amount Hero -->
      <div class="text-center border rounded p-3 mb-4 bg-light">
        <p class="text-muted mb-1">{{ isReceive ? 'Amount Received' : 'Amount Paid' }}</p>
        <h2 class="fw-bold mb-1">{{ payment.currency || 'MYR' }} {{ payment.paidAmount | number:'1.2-2' }}</h2>
        <p class="text-muted mb-0 fst-italic" *ngIf="amountInWords">{{ amountInWords }}</p>
      </div>

      <!-- Allocation References Table (if multi-invoice) -->
      <div *ngIf="payment.references && payment.references.length > 0" class="mb-4">
        <h6 class="text-uppercase text-muted fw-bold mb-2">Against Invoices</h6>
        <table class="table table-bordered table-sm">
          <thead class="table-light">
            <tr>
              <th>#</th>
              <th>Invoice</th>
              <th>Type</th>
              <th class="text-end">Invoice Amount</th>
              <th class="text-end">Allocated</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let ref of payment.references; let i = index">
              <td>{{ i + 1 }}</td>
              <td>{{ ref.referenceNumber || ref.referenceId }}</td>
              <td>{{ ref.referenceType }}</td>
              <td class="text-end">{{ ref.totalAmount | number:'1.2-2' }}</td>
              <td class="text-end fw-bold">{{ ref.allocatedAmount | number:'1.2-2' }}</td>
            </tr>
          </tbody>
          <tfoot class="table-light">
            <tr>
              <td colspan="4" class="text-end fw-bold">Total Allocated:</td>
              <td class="text-end fw-bold">{{ totalAllocated | number:'1.2-2' }}</td>
            </tr>
          </tfoot>
        </table>
      </div>

      <!-- Single Invoice Reference (legacy path) -->
      <div *ngIf="!payment.references?.length && payment.againstInvoiceId" class="mb-4">
        <h6 class="text-uppercase text-muted fw-bold mb-2">Against</h6>
        <p class="mb-0">Invoice: <strong>{{ againstInvoiceNumber || payment.againstInvoiceId }}</strong></p>
      </div>

      <!-- Account Details -->
      <div class="row mb-4">
        <div class="col-6">
          <h6 class="text-uppercase text-muted fw-bold mb-2">From Account</h6>
          <p class="mb-0">{{ paidFromAccountName || 'Company Account' }}</p>
        </div>
        <div class="col-6">
          <h6 class="text-uppercase text-muted fw-bold mb-2">To Account</h6>
          <p class="mb-0">{{ paidToAccountName || 'Party Account' }}</p>
        </div>
      </div>

      <!-- Exchange Rate (multi-currency) -->
      <div *ngIf="payment.exchangeRate && payment.exchangeRate !== 1" class="mb-4">
        <h6 class="text-uppercase text-muted fw-bold mb-2">Exchange Rate</h6>
        <p class="mb-0">1 {{ payment.currency }} = {{ payment.exchangeRate | number:'1.4-4' }} MYR</p>
        <p class="mb-0">Base Amount: MYR {{ baseAmount | number:'1.2-2' }}</p>
      </div>

      <!-- Remarks -->
      <div *ngIf="payment.notes" class="mb-4">
        <h6 class="text-uppercase text-muted fw-bold mb-2">Remarks</h6>
        <p class="mb-0">{{ payment.notes }}</p>
      </div>

      <!-- Signature Block -->
      <div class="row mt-5 pt-4">
        <div class="col-4 text-center">
          <div class="border-top pt-2 mx-3">
            <p class="text-muted mb-0 small">Prepared By</p>
          </div>
        </div>
        <div class="col-4 text-center">
          <div class="border-top pt-2 mx-3">
            <p class="text-muted mb-0 small">Authorized Signatory</p>
          </div>
        </div>
        <div class="col-4 text-center">
          <div class="border-top pt-2 mx-3">
            <p class="text-muted mb-0 small">{{ isReceive ? 'Received By' : 'Acknowledgement' }}</p>
          </div>
        </div>
      </div>

      <!-- Footer -->
      <div class="text-center mt-4 pt-3 border-top">
        <p class="text-muted small mb-0">This is a computer-generated document. No signature is required.</p>
        <p class="text-muted small mb-0">Thank you for your {{ isReceive ? 'payment' : 'business' }}.</p>
      </div>
    </div>
  `,
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
