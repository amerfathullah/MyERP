import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Purchase Receipt print layout — professional A4 Goods Receiving Note (GRN).
 * Used by warehouse teams when receiving goods from suppliers.
 * Includes: company header, supplier info, PO reference, items table with
 * accepted/rejected qty, warehouse location, and receiving inspector signature.
 * Per ERPNext: PR is the primary goods receiving document in the procure-to-pay flow.
 */
@Component({
  selector: 'app-pr-print-layout',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="pr-print-layout" *ngIf="receipt">
      <!-- Company Header -->
      <div class="print-header d-flex justify-content-between align-items-start mb-4">
        <div>
          <h3 class="fw-bold mb-1">{{ companyName }}</h3>
          <p class="text-muted mb-0" *ngIf="companyTin">TIN: {{ companyTin }}</p>
          <p class="text-muted mb-0" *ngIf="companySst">SST Reg No: {{ companySst }}</p>
          <p class="text-muted mb-0" *ngIf="companyAddress">{{ companyAddress }}</p>
        </div>
        <div class="text-end">
          <h4 class="text-uppercase text-primary fw-bold mb-1">GOODS RECEIVING NOTE</h4>
          <p class="mb-0"><strong>GRN #:</strong> {{ receipt.receiptNumber }}</p>
          <p class="mb-0"><strong>Date:</strong> {{ receipt.postingDate | date:'dd/MM/yyyy' }}</p>
          <p class="mb-0" *ngIf="receipt.isReturn"><span class="badge bg-warning">RETURN</span></p>
        </div>
      </div>

      <hr class="my-3" />

      <!-- Supplier + Reference Info -->
      <div class="row mb-4">
        <div class="col-6">
          <h6 class="fw-bold text-uppercase mb-2">Received From</h6>
          <p class="mb-0 fw-bold">{{ receipt.supplierName || receipt.supplierId }}</p>
          <p class="mb-0" *ngIf="supplierAddress">{{ supplierAddress }}</p>
        </div>
        <div class="col-6">
          <h6 class="fw-bold text-uppercase mb-2">Reference Details</h6>
          <table class="table table-sm table-borderless mb-0">
            <tbody>
              <tr *ngIf="receipt.purchaseOrderId">
                <td class="text-muted pe-2">Purchase Order:</td>
                <td class="fw-bold">{{ poNumber || '—' }}</td>
              </tr>
              <tr>
                <td class="text-muted pe-2">Warehouse:</td>
                <td class="fw-bold">{{ receipt.warehouseName || '—' }}</td>
              </tr>
              <tr *ngIf="receipt.supplierDeliveryNote">
                <td class="text-muted pe-2">Supplier DN:</td>
                <td class="fw-bold">{{ receipt.supplierDeliveryNote }}</td>
              </tr>
              <tr>
                <td class="text-muted pe-2">Currency:</td>
                <td>{{ receipt.currency || 'MYR' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Items Table -->
      <table class="table table-bordered table-sm mb-4">
        <thead class="table-light">
          <tr>
            <th style="width:5%">#</th>
            <th style="width:35%">Item Description</th>
            <th class="text-center" style="width:10%">Ordered</th>
            <th class="text-center" style="width:10%">Received</th>
            <th class="text-center" style="width:10%">Rejected</th>
            <th class="text-end" style="width:12%">Rate</th>
            <th class="text-end" style="width:15%">Amount</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let item of receipt.items; let i = index">
            <td>{{ i + 1 }}</td>
            <td>{{ item.description || item.itemName || '—' }}</td>
            <td class="text-center">{{ item.orderedQty || '—' }}</td>
            <td class="text-center fw-bold">{{ getAbsQty(item) }}</td>
            <td class="text-center" [class.text-danger]="item.rejectedQty > 0">{{ item.rejectedQty || '—' }}</td>
            <td class="text-end">{{ item.unitPrice | number:'1.2-2' }}</td>
            <td class="text-end">{{ item.lineTotal | number:'1.2-2' }}</td>
          </tr>
        </tbody>
        <tfoot>
          <tr class="table-light fw-bold">
            <td colspan="6" class="text-end">Net Total:</td>
            <td class="text-end">{{ receipt.netTotal | number:'1.2-2' }}</td>
          </tr>
          <tr *ngIf="receipt.taxAmount > 0">
            <td colspan="6" class="text-end">Tax (SST):</td>
            <td class="text-end">{{ receipt.taxAmount | number:'1.2-2' }}</td>
          </tr>
          <tr class="fw-bold">
            <td colspan="6" class="text-end">Grand Total:</td>
            <td class="text-end">{{ receipt.grandTotal | number:'1.2-2' }}</td>
          </tr>
        </tfoot>
      </table>

      <!-- Quality Inspection Notes -->
      <div class="row mb-4" *ngIf="receipt.notes">
        <div class="col-12">
          <h6 class="fw-bold">Inspection Notes / Remarks</h6>
          <p class="border rounded p-2 bg-light" style="min-height:40px">{{ receipt.notes }}</p>
        </div>
      </div>

      <!-- Signatures -->
      <div class="row mt-5 pt-3">
        <div class="col-4 text-center">
          <div class="border-bottom border-dark mb-1" style="height:40px;"></div>
          <small class="text-muted">Received By</small>
        </div>
        <div class="col-4 text-center">
          <div class="border-bottom border-dark mb-1" style="height:40px;"></div>
          <small class="text-muted">Quality Inspector</small>
        </div>
        <div class="col-4 text-center">
          <div class="border-bottom border-dark mb-1" style="height:40px;"></div>
          <small class="text-muted">Store Manager</small>
        </div>
      </div>

      <!-- Footer -->
      <div class="text-center mt-4 text-muted small">
        <p class="mb-0">This is a computer-generated document. Signature required for acceptance.</p>
        <p class="mb-0">Printed on: {{ today | date:'dd/MM/yyyy HH:mm' }}</p>
      </div>
    </div>
  `,
  styles: [`
    .pr-print-layout { padding: 20mm; font-size: 12px; }
    @media screen { .pr-print-layout { display: none; } }
    @media print {
      .pr-print-layout { display: block !important; page-break-after: always; }
      .pr-print-layout .table { font-size: 11px; }
      .pr-print-layout .badge { border: 1px solid #333; color: #333 !important; background: transparent !important; }
    }
  `]
})
export class PurchaseReceiptPrintLayoutComponent {
  @Input() receipt: any;
  @Input() companyName = '';
  @Input() companyTin = '';
  @Input() companySst = '';
  @Input() companyAddress = '';
  @Input() supplierAddress = '';
  @Input() poNumber = '';

  today = new Date();

  getAbsQty(item: any): string {
    const qty = item.quantity ?? item.receivedQty ?? 0;
    return Math.abs(qty).toString();
  }
}
