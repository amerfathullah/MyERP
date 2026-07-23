import { Component, Input } from '@angular/core';
import { DecimalPipe, DatePipe } from '@angular/common';

/**
 * Delivery Note print layout — professional A4 format for warehouse/shipping operations.
 * Includes: company header, customer billing + shipping address, items table with
 * serial/batch columns, weight summary, delivery instructions, and signature line.
 * Hidden on screen, shown only in print media.
 * Per ERPNext: DN is the key document for goods dispatch and proof of delivery.
 */
@Component({
  selector: 'app-dn-print-layout',
  standalone: true,
  imports: [DecimalPipe, DatePipe],
  template: `
    @if (deliveryNote) {
    <div class="dn-print-layout">
      <!-- Company Header -->
      <div class="print-header d-flex justify-content-between align-items-start mb-4">
        <div>
          <h3 class="fw-bold mb-1">{{ companyName }}</h3>
          @if (companyTin) { <p class="text-muted mb-0">TIN: {{ companyTin }}</p> }
          @if (companySst) { <p class="text-muted mb-0">SST No: {{ companySst }}</p> }
          @if (companyAddress) { <p class="text-muted mb-0">{{ companyAddress }}</p> }
          @if (companyPhone) { <p class="text-muted mb-0">Tel: {{ companyPhone }}</p> }
        </div>
        <div class="text-end">
          <h4 class="text-uppercase fw-bold mb-1" [class.text-danger]="deliveryNote.isReturn" [class.text-primary]="!deliveryNote.isReturn">
            {{ deliveryNote.isReturn ? 'RETURN NOTE' : 'DELIVERY NOTE' }}
          </h4>
          <p class="mb-0"><strong>DN #:</strong> {{ deliveryNote.deliveryNumber }}</p>
          <p class="mb-0"><strong>Date:</strong> {{ deliveryNote.postingDate | date:'dd/MM/yyyy' }}</p>
          @if (deliveryNote.salesOrderNumber) { <p class="mb-0"><strong>SO Ref:</strong> {{ deliveryNote.salesOrderNumber }}</p> }
          @if (deliveryNote.customerPo) { <p class="mb-0"><strong>Customer PO:</strong> {{ deliveryNote.customerPo }}</p> }
        </div>
      </div>

      <hr class="mb-4">

      <!-- Customer & Shipping Info -->
      <div class="row mb-4">
        <div class="col-6">
          <h6 class="text-uppercase text-muted fw-bold mb-2">Bill To</h6>
          <p class="fw-bold mb-1">{{ deliveryNote.customerName || 'N/A' }}</p>
          @if (billingAddress) { <p class="text-muted mb-0">{{ billingAddress }}</p> }
          @if (customerTin) { <p class="text-muted mb-0">TIN: {{ customerTin }}</p> }
        </div>
        <div class="col-6">
          <h6 class="text-uppercase text-muted fw-bold mb-2">Ship To</h6>
          <p class="fw-bold mb-1">{{ shippingContactName || deliveryNote.customerName || 'N/A' }}</p>
          @if (shippingAddress) { <p class="text-muted mb-0">{{ shippingAddress }}</p> }
          @if (shippingPhone) { <p class="text-muted mb-0">Tel: {{ shippingPhone }}</p> }
        </div>
      </div>

      <!-- Warehouse / Vehicle Info -->
      @if (warehouseName || vehicleNo || driverName) {
      <div class="row mb-3">
        @if (warehouseName) {
        <div class="col-4">
          <small class="text-muted">Source Warehouse</small>
          <p class="fw-bold mb-0">{{ warehouseName }}</p>
        </div>
        }
        @if (vehicleNo) {
        <div class="col-4">
          <small class="text-muted">Vehicle No.</small>
          <p class="fw-bold mb-0">{{ vehicleNo }}</p>
        </div>
        }
        @if (driverName) {
        <div class="col-4">
          <small class="text-muted">Driver</small>
          <p class="fw-bold mb-0">{{ driverName }}</p>
        </div>
        }
      </div>
      }

      <!-- Items Table -->
      <table class="table table-bordered mb-4">
        <thead class="table-light">
          <tr>
            <th style="width: 40px" class="text-center">S/No</th>
            <th>Item Code / Description</th>
            <th class="text-end" style="width: 70px">Qty</th>
            <th class="text-end" style="width: 70px">UOM</th>
            @if (showSerialBatch) { <th class="text-center" style="width: 120px">Serial / Batch</th> }
            @if (!hideAmounts) { <th class="text-end" style="width: 100px">Rate</th> }
            @if (!hideAmounts) { <th class="text-end" style="width: 110px">Amount</th> }
          </tr>
        </thead>
        <tbody>
          @for (item of deliveryNote.items; track $index) {
          <tr>
            <td class="text-center">{{ $index + 1 }}</td>
            <td>
              <span class="fw-semibold">{{ item.itemCode || item.itemName || '—' }}</span>
              @if (item.description && item.description !== item.itemCode) { <br><small class="text-muted">{{ item.description }}</small> }
            </td>
            <td class="text-end font-monospace">{{ getDisplayQty(item) | number:'1.0-2' }}</td>
            <td class="text-end">{{ item.uom || item.stockUom || 'Unit' }}</td>
            @if (showSerialBatch) { <td class="text-center small">{{ item.serialNo || item.batchNo || '—' }}</td> }
            @if (!hideAmounts) { <td class="text-end font-monospace">{{ item.unitPrice | number:'1.2-2' }}</td> }
            @if (!hideAmounts) { <td class="text-end font-monospace">{{ getLineTotal(item) | number:'1.2-2' }}</td> }
          </tr>
          }
        </tbody>
        @if (!hideAmounts) {
        <tfoot>
          <tr class="fw-bold">
            <td [attr.colspan]="showSerialBatch ? 5 : 4" class="text-end border-0">Total Qty:</td>
            <td class="text-end font-monospace border-0" [attr.colspan]="2">{{ totalQty | number:'1.0-2' }}</td>
          </tr>
        </tfoot>
        }
      </table>

      <!-- Totals (hidden in packing-slip mode) -->
      @if (!hideAmounts) {
      <div class="row">
        <div class="col-6">
          @if (deliveryNote.notes || deliveryInstructions) {
          <div class="border rounded p-3">
            <h6 class="text-uppercase text-muted fw-bold mb-2">Delivery Instructions</h6>
            <p class="mb-0 small">{{ deliveryInstructions || deliveryNote.notes }}</p>
          </div>
          }
        </div>
        <div class="col-6">
          <table class="table table-sm mb-0">
            <tbody>
              <tr>
                <td class="text-end border-0"><strong>Subtotal:</strong></td>
                <td class="text-end border-0 font-monospace" style="width: 140px">{{ currency }} {{ deliveryNote.netTotal | number:'1.2-2' }}</td>
              </tr>
              @if (deliveryNote.taxAmount) {
              <tr>
                <td class="text-end border-0"><strong>Tax (SST):</strong></td>
                <td class="text-end border-0 font-monospace">{{ currency }} {{ deliveryNote.taxAmount | number:'1.2-2' }}</td>
              </tr>
              }
              <tr class="border-top">
                <td class="text-end"><strong class="fs-5">Grand Total:</strong></td>
                <td class="text-end font-monospace fs-5 fw-bold">{{ currency }} {{ deliveryNote.grandTotal | number:'1.2-2' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
      }

      <!-- Signature Section -->
      <div class="mt-5 pt-4 border-top">
        <div class="row">
          <div class="col-4">
            <p class="mb-1 small fw-bold">Prepared By:</p>
            <div class="border-bottom mt-4" style="width: 180px;"></div>
            <p class="text-muted small mt-1">Name / Date / Stamp</p>
          </div>
          <div class="col-4">
            <p class="mb-1 small fw-bold">Checked By:</p>
            <div class="border-bottom mt-4" style="width: 180px;"></div>
            <p class="text-muted small mt-1">Name / Date / Stamp</p>
          </div>
          <div class="col-4">
            <p class="mb-1 small fw-bold">Received By:</p>
            <div class="border-bottom mt-4" style="width: 180px;"></div>
            <p class="text-muted small mt-1">Name / Date / Stamp</p>
          </div>
        </div>
      </div>

      <!-- Footer -->
      <div class="mt-3 text-center">
        <p class="text-muted small mb-0">
          Goods received in good condition unless otherwise noted above.
        </p>
        @if (deliveryNote.isReturn) {
        <p class="text-muted small mb-0">
          <strong>Note:</strong> This is a return document. Items listed are being returned to the company.
        </p>
        }
      </div>
    </div>
    }
  `,
  styles: [`
    :host { display: none; }
    @media print {
      :host { display: block !important; }
      .dn-print-layout {
        font-size: 11px;
        padding: 15mm;
      }
      .print-header h3 { font-size: 16px; }
      .print-header h4 { font-size: 14px; }
      .table th, .table td { padding: 5px 8px; }
      .table-bordered { border: 1px solid #000 !important; }
      .table-bordered th, .table-bordered td { border: 1px solid #000 !important; }
      .text-danger { color: #dc3545 !important; }
      .text-primary { color: #0d6efd !important; }
    }
  `]
})
export class DeliveryNotePrintLayoutComponent {
  @Input() deliveryNote: any;
  @Input() companyName = '';
  @Input() companyTin = '';
  @Input() companySst = '';
  @Input() companyAddress = '';
  @Input() companyPhone = '';
  @Input() billingAddress = '';
  @Input() shippingAddress = '';
  @Input() shippingContactName = '';
  @Input() shippingPhone = '';
  @Input() customerTin = '';
  @Input() warehouseName = '';
  @Input() vehicleNo = '';
  @Input() driverName = '';
  @Input() deliveryInstructions = '';
  @Input() currency = 'MYR';
  /** Hide amounts — used for packing-slip mode (warehouse only sees qty) */
  @Input() hideAmounts = false;
  /** Show serial/batch column when items have tracking */
  @Input() showSerialBatch = false;

  get totalQty(): number {
    return (this.deliveryNote?.items || []).reduce(
      (sum: number, item: any) => sum + Math.abs(item.quantity || item.stockQty || 0), 0
    );
  }

  getDisplayQty(item: any): number {
    return Math.abs(item.quantity || item.stockQty || 0);
  }

  getLineTotal(item: any): number {
    return Math.abs((item.quantity || 0) * (item.unitPrice || 0));
  }
}
