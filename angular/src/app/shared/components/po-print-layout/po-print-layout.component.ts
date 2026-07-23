import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Purchase Order print layout — professional A4 format for sending to suppliers.
 * Includes: company header with TIN/SST, supplier billing info, items table,
 * delivery details, terms & conditions, and total breakdown.
 * Hidden on screen, shown only in print media.
 */
@Component({
  selector: 'app-po-print-layout',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (order) {
      <div class="po-print-layout">
        <!-- Company Header -->
        <div class="print-header d-flex justify-content-between align-items-start mb-4">
          <div>
            <h3 class="fw-bold mb-1">{{ companyName }}</h3>
            @if (companyTin) {
              <p class="text-muted mb-0">TIN: {{ companyTin }}</p>
            }
            @if (companySst) {
              <p class="text-muted mb-0">SST No: {{ companySst }}</p>
            }
            @if (companyAddress) {
              <p class="text-muted mb-0">{{ companyAddress }}</p>
            }
          </div>
          <div class="text-end">
            <h4 class="text-uppercase text-primary fw-bold mb-1">PURCHASE ORDER</h4>
            <p class="mb-0"><strong>PO #:</strong> {{ order.orderNumber }}</p>
            <p class="mb-0"><strong>Date:</strong> {{ order.orderDate | date:'dd/MM/yyyy' }}</p>
            @if (order.deliveryDate) {
              <p class="mb-0"><strong>Delivery By:</strong> {{ order.deliveryDate | date:'dd/MM/yyyy' }}</p>
            }
          </div>
        </div>
        <hr class="mb-4">
        <!-- Supplier Info -->
        <div class="row mb-4">
          <div class="col-6">
            <h6 class="text-uppercase text-muted fw-bold mb-2">Supplier</h6>
            <p class="fw-bold mb-1">{{ order.supplierName || 'N/A' }}</p>
            @if (supplierAddress) {
              <p class="text-muted mb-0">{{ supplierAddress }}</p>
            }
          </div>
          <div class="col-6">
            <h6 class="text-uppercase text-muted fw-bold mb-2">Ship To</h6>
            <p class="mb-1">{{ companyName }}</p>
            @if (shipToAddress) {
              <p class="text-muted mb-0">{{ shipToAddress }}</p>
            }
          </div>
        </div>
        <!-- Items Table -->
        <table class="table table-bordered mb-4">
          <thead class="table-light">
            <tr>
              <th style="width: 40px" class="text-center">S/No</th>
              <th>Item Description</th>
              <th class="text-end" style="width: 80px">Qty</th>
              <th class="text-end" style="width: 80px">UOM</th>
              <th class="text-end" style="width: 120px">Unit Price</th>
              <th class="text-end" style="width: 120px">Amount</th>
            </tr>
          </thead>
          <tbody>
            @for (item of order.items; track item; let i = $index) {
              <tr>
                <td class="text-center">{{ i + 1 }}</td>
                <td>{{ item.description || item.itemName || '—' }}</td>
                <td class="text-end font-monospace">{{ item.quantity | number:'1.0-2' }}</td>
                <td class="text-end">{{ item.uom || 'Unit' }}</td>
                <td class="text-end font-monospace">{{ item.unitPrice | number:'1.2-2' }}</td>
                <td class="text-end font-monospace">{{ (item.quantity || 0) * (item.unitPrice || 0) | number:'1.2-2' }}</td>
              </tr>
            }
          </tbody>
        </table>
        <!-- Totals -->
        <div class="row">
          <div class="col-6">
            @if (order.notes) {
              <div class="border rounded p-3">
                <h6 class="text-uppercase text-muted fw-bold mb-2">Notes / Terms</h6>
                <p class="mb-0 small">{{ order.notes }}</p>
              </div>
            }
          </div>
          <div class="col-6">
            <table class="table table-sm mb-0">
              <tbody>
                <tr>
                  <td class="text-end border-0"><strong>Subtotal:</strong></td>
                  <td class="text-end border-0 font-monospace" style="width: 140px">{{ order.currency || 'MYR' }} {{ order.netTotal | number:'1.2-2' }}</td>
                </tr>
                @if (order.taxAmount) {
                  <tr>
                    <td class="text-end border-0"><strong>Tax:</strong></td>
                    <td class="text-end border-0 font-monospace">{{ order.currency || 'MYR' }} {{ order.taxAmount | number:'1.2-2' }}</td>
                  </tr>
                }
                <tr class="border-top">
                  <td class="text-end"><strong class="fs-5">Grand Total:</strong></td>
                  <td class="text-end font-monospace fs-5 fw-bold">{{ order.currency || 'MYR' }} {{ order.grandTotal | number:'1.2-2' }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
        <!-- Footer -->
        <div class="mt-5 pt-4 border-top">
          <div class="row">
            <div class="col-6">
              <p class="mb-4">Authorized Signature:</p>
              <div class="border-bottom" style="width: 200px; margin-top: 40px;"></div>
              <p class="text-muted small mt-1">Name & Date</p>
            </div>
            <div class="col-6 text-end">
              <p class="text-muted small">This is a computer-generated document.</p>
              <p class="text-muted small">No signature required.</p>
            </div>
          </div>
        </div>
      </div>
    }
    `,
  styles: [`
    :host { display: none; }
    @media print {
      :host { display: block !important; }
      .po-print-layout {
        font-size: 12px;
        padding: 20mm;
      }
      .print-header h3 { font-size: 18px; }
      .table th, .table td { padding: 6px 8px; }
      .table-bordered { border: 1px solid #000 !important; }
      .table-bordered th, .table-bordered td { border: 1px solid #000 !important; }
    }
  `]
})
export class PurchaseOrderPrintLayoutComponent {
  @Input() order: any;
  @Input() companyName = '';
  @Input() companyTin = '';
  @Input() companySst = '';
  @Input() companyAddress = '';
  @Input() supplierAddress = '';
  @Input() shipToAddress = '';
}
