import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Sales Order print layout — professional A4 format for order confirmations.
 * Includes: company header (TIN/SST), customer billing + shipping,
 * items table, totals, delivery date, payment terms, and acceptance signature.
 * Hidden on screen, shown only in print media.
 * Per ERPNext: SO is the customer's order confirmation and commitment document.
 */
@Component({
  selector: 'app-so-print-layout',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="so-print-layout" *ngIf="salesOrder">
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
          <h4 class="text-uppercase text-primary fw-bold mb-1">SALES ORDER</h4>
          <p class="mb-0"><strong>Order #:</strong> {{ salesOrder.orderNumber }}</p>
          <p class="mb-0"><strong>Date:</strong> {{ salesOrder.orderDate | date:'dd/MM/yyyy' }}</p>
          <p class="mb-0" *ngIf="salesOrder.customerPo"><strong>Your PO #:</strong> {{ salesOrder.customerPo }}</p>
          <p class="mb-0" *ngIf="salesOrder.deliveryDate"><strong>Delivery By:</strong> {{ salesOrder.deliveryDate | date:'dd/MM/yyyy' }}</p>
        </div>
      </div>

      <hr class="mb-4">

      <!-- Customer & Delivery Info -->
      <div class="row mb-4">
        <div class="col-6">
          <h6 class="text-uppercase text-muted fw-bold mb-2">Customer</h6>
          <p class="fw-bold mb-1">{{ customerName || 'N/A' }}</p>
          <p class="text-muted mb-0" *ngIf="billingAddress">{{ billingAddress }}</p>
          <p class="text-muted mb-0" *ngIf="customerTin">TIN: {{ customerTin }}</p>
          <p class="text-muted mb-0" *ngIf="contactPerson">Attn: {{ contactPerson }}</p>
          <p class="text-muted mb-0" *ngIf="contactPhone">Tel: {{ contactPhone }}</p>
        </div>
        <div class="col-6">
          <h6 class="text-uppercase text-muted fw-bold mb-2">Deliver To</h6>
          <p class="fw-bold mb-1">{{ shippingName || customerName || 'N/A' }}</p>
          <p class="text-muted mb-0" *ngIf="shippingAddress">{{ shippingAddress }}</p>
          <p class="text-muted mb-0" *ngIf="shippingPhone">Tel: {{ shippingPhone }}</p>
        </div>
      </div>

      <!-- Order References -->
      <div class="row mb-3" *ngIf="salesPerson || paymentTerms">
        <div class="col-4" *ngIf="salesPerson">
          <small class="text-muted">Sales Person</small>
          <p class="fw-bold mb-0">{{ salesPerson }}</p>
        </div>
        <div class="col-4" *ngIf="paymentTerms">
          <small class="text-muted">Payment Terms</small>
          <p class="fw-bold mb-0">{{ paymentTerms }}</p>
        </div>
        <div class="col-4" *ngIf="currency && currency !== 'MYR'">
          <small class="text-muted">Currency</small>
          <p class="fw-bold mb-0">{{ currency }} (Rate: {{ salesOrder.exchangeRate | number:'1.4-4' }})</p>
        </div>
      </div>

      <!-- Items Table -->
      <table class="table table-bordered mb-4">
        <thead class="table-light">
          <tr>
            <th style="width: 40px" class="text-center">S/No</th>
            <th>Item Description</th>
            <th class="text-end" style="width: 70px">Qty</th>
            <th class="text-end" style="width: 70px">UOM</th>
            <th class="text-end" style="width: 110px">Unit Price</th>
            <th class="text-end" style="width: 80px" *ngIf="hasDiscounts">Disc %</th>
            <th class="text-end" style="width: 120px">Amount ({{ currency }})</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let item of salesOrder.items; let i = index">
            <td class="text-center">{{ i + 1 }}</td>
            <td>
              <span class="fw-semibold">{{ item.itemName || item.description || '—' }}</span>
              <br *ngIf="item.description && item.description !== item.itemName">
              <small class="text-muted" *ngIf="item.description && item.description !== item.itemName">{{ item.description }}</small>
              <br *ngIf="item.deliveryDate">
              <small class="text-muted" *ngIf="item.deliveryDate">Delivery: {{ item.deliveryDate | date:'dd/MM/yyyy' }}</small>
            </td>
            <td class="text-end font-monospace">{{ item.quantity | number:'1.0-2' }}</td>
            <td class="text-end">{{ item.uom || item.stockUom || 'Unit' }}</td>
            <td class="text-end font-monospace">{{ item.unitPrice | number:'1.2-2' }}</td>
            <td class="text-end font-monospace" *ngIf="hasDiscounts">{{ item.discountPercentage ? (item.discountPercentage | number:'1.0-1') + '%' : '—' }}</td>
            <td class="text-end font-monospace fw-semibold">{{ (item.quantity * item.unitPrice) | number:'1.2-2' }}</td>
          </tr>
        </tbody>
      </table>

      <!-- Totals Section -->
      <div class="row">
        <div class="col-7">
          <!-- Terms & Conditions -->
          <div *ngIf="termsAndConditions">
            <h6 class="text-uppercase text-muted fw-bold mb-2">Terms & Conditions</h6>
            <p class="small" style="white-space: pre-line">{{ termsAndConditions }}</p>
          </div>
          <div *ngIf="salesOrder.notes">
            <h6 class="text-uppercase text-muted fw-bold mb-2">Notes</h6>
            <p class="small" style="white-space: pre-line">{{ salesOrder.notes }}</p>
          </div>
        </div>
        <div class="col-5">
          <table class="table table-sm table-borderless">
            <tbody>
              <tr>
                <td class="text-muted">Net Total:</td>
                <td class="text-end font-monospace">{{ currency }} {{ salesOrder.netTotal | number:'1.2-2' }}</td>
              </tr>
              <tr *ngIf="salesOrder.taxAmount">
                <td class="text-muted">Tax (SST):</td>
                <td class="text-end font-monospace">{{ currency }} {{ salesOrder.taxAmount | number:'1.2-2' }}</td>
              </tr>
              <tr *ngIf="salesOrder.shippingCharge">
                <td class="text-muted">Shipping:</td>
                <td class="text-end font-monospace">{{ currency }} {{ salesOrder.shippingCharge | number:'1.2-2' }}</td>
              </tr>
              <tr *ngIf="salesOrder.discountAmount">
                <td class="text-muted">Discount:</td>
                <td class="text-end font-monospace text-danger">({{ currency }} {{ salesOrder.discountAmount | number:'1.2-2' }})</td>
              </tr>
              <tr class="border-top">
                <td class="fw-bold">Grand Total:</td>
                <td class="text-end font-monospace fw-bold fs-5">{{ currency }} {{ salesOrder.grandTotal | number:'1.2-2' }}</td>
              </tr>
              <tr *ngIf="salesOrder.advancePaid">
                <td class="text-muted">Advance Paid:</td>
                <td class="text-end font-monospace text-success">{{ currency }} {{ salesOrder.advancePaid | number:'1.2-2' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Acceptance Signature -->
      <div class="row mt-5 pt-4">
        <div class="col-6">
          <div class="border-bottom mb-2" style="height: 60px;"></div>
          <small class="text-muted">Authorized Signature (Seller)</small>
          <br><small class="text-muted">Date: _______________</small>
        </div>
        <div class="col-6">
          <div class="border-bottom mb-2" style="height: 60px;"></div>
          <small class="text-muted">Accepted by (Customer)</small>
          <br><small class="text-muted">Date: _______________</small>
        </div>
      </div>

      <!-- Footer -->
      <div class="text-center mt-4 pt-3 border-top">
        <small class="text-muted">This is a computer-generated document. Please sign and return to confirm your order.</small>
      </div>
    </div>
  `,
  styles: [`
    :host { display: none; }
    @media print {
      :host { display: block; }
      .so-print-layout { font-size: 11px; padding: 15mm; }
      .print-header h3 { font-size: 18px; }
      .table { font-size: 10px; }
      .table th { background-color: #f8f9fa !important; -webkit-print-color-adjust: exact; }
    }
  `]
})
export class SalesOrderPrintLayoutComponent {
  @Input() salesOrder: any;
  @Input() companyName = '';
  @Input() companyTin = '';
  @Input() companySst = '';
  @Input() companyAddress = '';
  @Input() companyPhone = '';
  @Input() companyEmail = '';
  @Input() customerName = '';
  @Input() customerTin = '';
  @Input() billingAddress = '';
  @Input() shippingName = '';
  @Input() shippingAddress = '';
  @Input() shippingPhone = '';
  @Input() contactPerson = '';
  @Input() contactPhone = '';
  @Input() salesPerson = '';
  @Input() paymentTerms = '';
  @Input() termsAndConditions = '';
  @Input() currency = 'MYR';

  get hasDiscounts(): boolean {
    return this.salesOrder?.items?.some((i: any) => i.discountPercentage > 0) ?? false;
  }
}
