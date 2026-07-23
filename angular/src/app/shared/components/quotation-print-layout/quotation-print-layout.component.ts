import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Quotation print layout — professional A4 format for customer proposals.
 * Includes: company header (TIN/SST), customer info, validity period,
 * items table with discount column, terms & conditions, and total breakdown.
 * Per ERPNext: quotation is the first customer-facing document in the sales cycle.
 */
@Component({
  selector: 'app-quotation-print-layout',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="quotation-print-layout" *ngIf="quotation">
      <!-- Company Header -->
      <div class="print-header d-flex justify-content-between align-items-start mb-4">
        <div>
          <h3 class="fw-bold mb-1">{{ companyName }}</h3>
          <p class="text-muted mb-0" *ngIf="companyTin">TIN: {{ companyTin }}</p>
          <p class="text-muted mb-0" *ngIf="companySst">SST Reg No: {{ companySst }}</p>
          <p class="text-muted mb-0" *ngIf="companyAddress">{{ companyAddress }}</p>
          <p class="text-muted mb-0" *ngIf="companyPhone">Tel: {{ companyPhone }}</p>
          <p class="text-muted mb-0" *ngIf="companyEmail">Email: {{ companyEmail }}</p>
        </div>
        <div class="text-end">
          <h4 class="text-uppercase text-primary fw-bold mb-1">QUOTATION</h4>
          <p class="mb-0"><strong>Ref #:</strong> {{ quotation.quotationNumber }}</p>
          <p class="mb-0"><strong>Date:</strong> {{ quotation.issueDate | date:'dd/MM/yyyy' }}</p>
          <p class="mb-0" *ngIf="quotation.validUntil"><strong>Valid Until:</strong> {{ quotation.validUntil | date:'dd/MM/yyyy' }}</p>
        </div>
      </div>

      <hr class="mb-4">

      <!-- Customer Info -->
      <div class="row mb-4">
        <div class="col-7">
          <h6 class="text-uppercase text-muted fw-bold mb-2">Quotation To</h6>
          <p class="fw-bold mb-1">{{ quotation.customerName || customerName || 'N/A' }}</p>
          <p class="text-muted mb-0" *ngIf="customerAddress">{{ customerAddress }}</p>
          <p class="text-muted mb-0" *ngIf="customerTin">TIN: {{ customerTin }}</p>
          <p class="text-muted mb-0" *ngIf="contactPerson">Attn: {{ contactPerson }}</p>
        </div>
        <div class="col-5 text-end">
          <div class="border rounded p-3 d-inline-block">
            <small class="text-muted d-block">Payment Terms</small>
            <span class="fw-semibold">{{ paymentTerms || 'As per agreement' }}</span>
          </div>
        </div>
      </div>

      <!-- Subject / Introduction -->
      <div class="mb-4" *ngIf="quotation.notes || introText">
        <p class="mb-0">{{ introText || quotation.notes }}</p>
      </div>

      <!-- Items Table -->
      <table class="table table-bordered mb-4">
        <thead class="table-light">
          <tr>
            <th style="width: 40px" class="text-center">S/No</th>
            <th>Description</th>
            <th class="text-end" style="width: 70px">Qty</th>
            <th class="text-end" style="width: 70px">UOM</th>
            <th class="text-end" style="width: 110px">Unit Price</th>
            <th class="text-end" style="width: 80px" *ngIf="hasDiscounts">Disc %</th>
            <th class="text-end" style="width: 120px">Amount ({{ currency }})</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let item of quotation.items; let i = index">
            <td class="text-center">{{ i + 1 }}</td>
            <td>
              <span class="fw-semibold">{{ item.itemName || item.description || '—' }}</span>
              <br *ngIf="item.description && item.description !== item.itemName">
              <small class="text-muted" *ngIf="item.description && item.description !== item.itemName">{{ item.description }}</small>
            </td>
            <td class="text-end font-monospace">{{ item.quantity | number:'1.0-2' }}</td>
            <td class="text-end">{{ item.uom || 'Unit' }}</td>
            <td class="text-end font-monospace">{{ item.unitPrice | number:'1.2-2' }}</td>
            <td class="text-end font-monospace" *ngIf="hasDiscounts">{{ item.discountPercent ? (item.discountPercent | number:'1.0-1') + '%' : '—' }}</td>
            <td class="text-end font-monospace">{{ getLineTotal(item) | number:'1.2-2' }}</td>
          </tr>
        </tbody>
      </table>

      <!-- Totals -->
      <div class="row">
        <div class="col-6">
          <!-- Terms & Conditions -->
          <div *ngIf="termsAndConditions">
            <h6 class="text-uppercase text-muted fw-bold mb-2">Terms & Conditions</h6>
            <div class="small" [innerHTML]="termsAndConditions"></div>
          </div>
        </div>
        <div class="col-6">
          <table class="table table-sm mb-0">
            <tbody>
              <tr>
                <td class="text-end border-0"><strong>Subtotal:</strong></td>
                <td class="text-end border-0 font-monospace" style="width: 140px">{{ currency }} {{ quotation.netTotal | number:'1.2-2' }}</td>
              </tr>
              <tr *ngIf="quotation.discountAmount">
                <td class="text-end border-0"><strong>Discount:</strong></td>
                <td class="text-end border-0 font-monospace text-danger">- {{ currency }} {{ quotation.discountAmount | number:'1.2-2' }}</td>
              </tr>
              <tr *ngIf="quotation.taxAmount">
                <td class="text-end border-0"><strong>Tax (SST {{ taxRate }}%):</strong></td>
                <td class="text-end border-0 font-monospace">{{ currency }} {{ quotation.taxAmount | number:'1.2-2' }}</td>
              </tr>
              <tr class="border-top">
                <td class="text-end"><strong class="fs-5">Grand Total:</strong></td>
                <td class="text-end font-monospace fs-5 fw-bold">{{ currency }} {{ quotation.grandTotal | number:'1.2-2' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Validity Notice -->
      <div class="mt-4 p-3 border rounded bg-light" *ngIf="quotation.validUntil">
        <p class="mb-0 text-center small">
          <i class="fa fa-info-circle me-1"></i>
          This quotation is valid until <strong>{{ quotation.validUntil | date:'dd MMMM yyyy' }}</strong>.
          Prices may be subject to change after the validity period.
        </p>
      </div>

      <!-- Acceptance -->
      <div class="mt-5 pt-4 border-top">
        <div class="row">
          <div class="col-6">
            <h6 class="fw-bold mb-3">For {{ companyName }}</h6>
            <div class="border-bottom mt-4" style="width: 200px;"></div>
            <p class="text-muted small mt-1">Authorized Signatory</p>
          </div>
          <div class="col-6">
            <h6 class="fw-bold mb-3">Accepted By (Customer)</h6>
            <div class="border-bottom mt-4" style="width: 200px;"></div>
            <p class="text-muted small mt-1">Name / Signature / Date / Company Stamp</p>
          </div>
        </div>
      </div>

      <!-- Footer -->
      <div class="mt-3 text-center">
        <p class="text-muted small mb-0">Thank you for considering our proposal. We look forward to serving you.</p>
      </div>
    </div>
  `,
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
