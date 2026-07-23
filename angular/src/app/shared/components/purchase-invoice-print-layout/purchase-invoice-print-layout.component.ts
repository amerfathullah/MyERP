import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LocalizationPipe } from '@abp/ng.core';

/**
 * Purchase Invoice Print Layout — Professional A4-format for supplier payment processing.
 * Used for: vendor payment vouchers, supplier invoice verification, accounts payable processing.
 * Follows Malaysia compliance: company TIN/SST, supplier TIN, reference numbers.
 *
 * Differences from Sales Invoice print:
 * - Title: "Purchase Invoice" / "Debit Note" (not "Tax Invoice" / "Credit Note")
 * - Parties: "From" (supplier) + "To" (our company) instead of "Bill To"
 * - Shows supplier invoice number and supplier delivery note reference
 * - Payment terms and bank details oriented towards payable processing
 *
 * Usage: <app-purchase-invoice-print-layout [invoice]="piData" [company]="companyData" />
 */
@Component({
  selector: 'app-purchase-invoice-print-layout',
  standalone: true,
  imports: [CommonModule, LocalizationPipe],
  styles: [`
    :host { display: block; }
    .print-pi { font-family: 'Segoe UI', sans-serif; color: #333; max-width: 210mm; margin: 0 auto; padding: 15mm; }
    .print-header { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 2px solid #d32f2f; padding-bottom: 12px; margin-bottom: 20px; }
    .company-info h1 { font-size: 20pt; color: #d32f2f; margin: 0 0 4px; }
    .company-info p { margin: 2px 0; font-size: 9pt; color: #555; }
    .invoice-title { text-align: right; }
    .invoice-title h2 { font-size: 16pt; color: #333; margin: 0 0 8px; text-transform: uppercase; }
    .invoice-title .inv-number { font-size: 11pt; font-weight: 600; }
    .invoice-title .inv-date { font-size: 9pt; color: #666; }
    .parties { display: flex; justify-content: space-between; margin-bottom: 20px; }
    .party-box { width: 48%; }
    .party-box h4 { font-size: 9pt; text-transform: uppercase; color: #888; margin: 0 0 4px; letter-spacing: 0.5px; }
    .party-box p { margin: 2px 0; font-size: 10pt; }
    .ref-section { background: #fafafa; border: 1px solid #eee; border-radius: 4px; padding: 10px 14px; margin-bottom: 16px; display: flex; gap: 30px; flex-wrap: wrap; }
    .ref-item { font-size: 9pt; }
    .ref-item .label { color: #888; text-transform: uppercase; font-size: 8pt; }
    .ref-item .value { font-weight: 600; }
    .items-table { width: 100%; border-collapse: collapse; margin-bottom: 16px; }
    .items-table th { background: #fce4ec; border: 1px solid #ddd; padding: 8px 10px; font-size: 8pt; text-transform: uppercase; text-align: left; }
    .items-table td { border: 1px solid #ddd; padding: 7px 10px; font-size: 9.5pt; }
    .items-table .text-end { text-align: right; }
    .items-table .sno { width: 30px; text-align: center; }
    .totals-section { display: flex; justify-content: flex-end; margin-bottom: 20px; }
    .totals-table { width: 280px; }
    .totals-table tr td { padding: 5px 10px; font-size: 10pt; }
    .totals-table tr td:last-child { text-align: right; font-weight: 500; }
    .totals-table .grand-total td { font-size: 12pt; font-weight: 700; border-top: 2px solid #333; padding-top: 8px; }
    .payment-info { margin-bottom: 16px; padding: 10px 14px; background: #f5f5f5; border-radius: 4px; }
    .payment-info h4 { font-size: 9pt; text-transform: uppercase; color: #888; margin: 0 0 6px; }
    .payment-info p { margin: 2px 0; font-size: 9.5pt; }
    .footer { border-top: 1px solid #ddd; padding-top: 12px; font-size: 8.5pt; color: #666; }
    .footer .notes { margin-bottom: 8px; }
    .approval-box { margin-top: 24px; display: flex; justify-content: space-between; }
    .approval-box .sign-block { width: 200px; text-align: center; border-top: 1px solid #333; padding-top: 6px; font-size: 8pt; }

    @media screen {
      :host { display: none; }
      :host(.show-preview) { display: block; border: 1px solid #ddd; border-radius: 4px; background: white; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }
    }
    @media print {
      :host { display: block !important; }
      .print-pi { padding: 10mm; }
    }
  `],
  template: `
    <div class="print-pi" *ngIf="invoice">
      <!-- Header: Company Info + Invoice Title -->
      <div class="print-header">
        <div class="company-info">
          <h1>{{ company?.name || 'Company Name' }}</h1>
          <p *ngIf="company?.registrationNumber">Reg No: {{ company.registrationNumber }}</p>
          <p *ngIf="company?.taxId">TIN: {{ company.taxId }}</p>
          <p *ngIf="company?.sstRegistrationNumber">SST: {{ company.sstRegistrationNumber }}</p>
          <p *ngIf="company?.address">{{ company.address }}</p>
          <p *ngIf="company?.phone">Tel: {{ company.phone }}</p>
        </div>
        <div class="invoice-title">
          <h2>
            <span *ngIf="!invoice.isReturn">{{ 'PurchaseInvoice' | abpLocalization }}</span>
            <span *ngIf="invoice.isReturn">{{ 'DebitNote' | abpLocalization }}</span>
          </h2>
          <div class="inv-number">{{ invoice.invoiceNumber }}</div>
          <div class="inv-date">{{ 'Date' | abpLocalization }}: {{ invoice.issueDate | date:'dd/MM/yyyy' }}</div>
          <div class="inv-date" *ngIf="invoice.dueDate">{{ 'DueDate' | abpLocalization }}: {{ invoice.dueDate | date:'dd/MM/yyyy' }}</div>
        </div>
      </div>

      <!-- Parties: Supplier (From) + Company (To) -->
      <div class="parties">
        <div class="party-box">
          <h4>{{ 'From' | abpLocalization }} ({{ 'Supplier' | abpLocalization }})</h4>
          <p><strong>{{ invoice.supplierName || '—' }}</strong></p>
          <p *ngIf="invoice.supplierTin">TIN: {{ invoice.supplierTin }}</p>
          <p *ngIf="invoice.supplierAddress">{{ invoice.supplierAddress }}</p>
        </div>
        <div class="party-box">
          <h4>{{ 'To' | abpLocalization }} ({{ 'Company' | abpLocalization }})</h4>
          <p><strong>{{ company?.name }}</strong></p>
          <p *ngIf="company?.address">{{ company.address }}</p>
        </div>
      </div>

      <!-- Reference Numbers -->
      <div class="ref-section" *ngIf="invoice.supplierInvoiceNumber || invoice.purchaseOrderNumber || invoice.purchaseReceiptNumber">
        <div class="ref-item" *ngIf="invoice.supplierInvoiceNumber">
          <div class="label">Supplier Invoice No.</div>
          <div class="value">{{ invoice.supplierInvoiceNumber }}</div>
        </div>
        <div class="ref-item" *ngIf="invoice.purchaseOrderNumber">
          <div class="label">Purchase Order</div>
          <div class="value">{{ invoice.purchaseOrderNumber }}</div>
        </div>
        <div class="ref-item" *ngIf="invoice.purchaseReceiptNumber">
          <div class="label">Goods Receipt</div>
          <div class="value">{{ invoice.purchaseReceiptNumber }}</div>
        </div>
        <div class="ref-item" *ngIf="invoice.supplierDeliveryNote">
          <div class="label">Supplier DO</div>
          <div class="value">{{ invoice.supplierDeliveryNote }}</div>
        </div>
      </div>

      <!-- Line Items Table -->
      <table class="items-table">
        <thead>
          <tr>
            <th class="sno">#</th>
            <th>{{ 'Description' | abpLocalization }}</th>
            <th class="text-end" style="width:60px">{{ 'Qty' | abpLocalization }}</th>
            <th class="text-end" style="width:50px">UOM</th>
            <th class="text-end" style="width:100px">{{ 'Rate' | abpLocalization }} ({{ invoice.currencyCode || 'MYR' }})</th>
            <th class="text-end" style="width:120px">{{ 'Amount' | abpLocalization }} ({{ invoice.currencyCode || 'MYR' }})</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let item of invoice.items; let i = index">
            <td class="sno">{{ i + 1 }}</td>
            <td>{{ item.description || item.itemName || '—' }}</td>
            <td class="text-end">{{ formatNumber(item.quantity) }}</td>
            <td class="text-end">{{ item.uom || item.stockUom || 'Unit' }}</td>
            <td class="text-end">{{ formatCurrency(item.unitPrice) }}</td>
            <td class="text-end">{{ formatCurrency(item.quantity * item.unitPrice) }}</td>
          </tr>
        </tbody>
      </table>

      <!-- Totals -->
      <div class="totals-section">
        <table class="totals-table">
          <tr>
            <td>{{ 'NetTotal' | abpLocalization }}</td>
            <td>{{ formatCurrency(invoice.netTotal) }}</td>
          </tr>
          <tr *ngIf="invoice.discountAmount > 0">
            <td>{{ 'Discount' | abpLocalization }}</td>
            <td>- {{ formatCurrency(invoice.discountAmount) }}</td>
          </tr>
          <tr *ngIf="invoice.taxAmount > 0">
            <td>SST / Tax</td>
            <td>{{ formatCurrency(invoice.taxAmount) }}</td>
          </tr>
          <tr class="grand-total">
            <td>{{ 'GrandTotal' | abpLocalization }}</td>
            <td>{{ invoice.currencyCode || 'MYR' }} {{ formatCurrency(invoice.grandTotal) }}</td>
          </tr>
        </table>
      </div>

      <!-- Payment Information -->
      <div class="payment-info" *ngIf="invoice.paymentTerms">
        <h4>{{ 'PaymentTerms' | abpLocalization }}</h4>
        <p>{{ invoice.paymentTerms }}</p>
      </div>

      <!-- Footer with approval signature lines -->
      <div class="footer">
        <div class="notes" *ngIf="invoice.notes">
          <strong>{{ 'Notes' | abpLocalization }}:</strong> {{ invoice.notes }}
        </div>
        <div class="approval-box">
          <div class="sign-block">Prepared By</div>
          <div class="sign-block">Verified By</div>
          <div class="sign-block">Approved By</div>
        </div>
      </div>
    </div>
  `
})
export class PurchaseInvoicePrintLayoutComponent {
  @Input() invoice: any;
  @Input() company: any;

  formatCurrency(value: number | null | undefined): string {
    if (value == null) return '0.00';
    return value.toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
  }

  formatNumber(value: number | null | undefined): string {
    if (value == null) return '0';
    return Number.isInteger(value) ? value.toString() : value.toFixed(2);
  }
}
