import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LocalizationPipe } from '@abp/ng.core';

/**
 * Invoice Print Layout — Professional A4-format invoice for printing/PDF.
 * Follows ERPNext Standard Print Format patterns:
 * - Company header with logo placeholder, address, TIN/SST
 * - Invoice metadata (number, date, due date, status)
 * - Customer/Supplier billing details
 * - Line items table with S/No, Description, Qty, Rate, Amount
 * - Tax summary section
 * - Grand Total with words
 * - Payment terms schedule (if applicable)
 * - Footer with bank details + notes
 *
 * Usage: <app-invoice-print-layout [invoice]="invoiceData" [company]="companyData" />
 * Hidden in screen mode, visible only in @media print or when explicitly shown.
 */
@Component({
  selector: 'app-invoice-print-layout',
  standalone: true,
  imports: [CommonModule, LocalizationPipe],
  styles: [`
    :host { display: block; }
    .print-invoice { font-family: 'Segoe UI', sans-serif; color: #333; max-width: 210mm; margin: 0 auto; padding: 15mm; position: relative; overflow: hidden; }
    .print-header { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 2px solid #1976d2; padding-bottom: 12px; margin-bottom: 20px; }
    .company-info h1 { font-size: 20pt; color: #1976d2; margin: 0 0 4px; }
    .company-info p { margin: 2px 0; font-size: 9pt; color: #555; }
    .invoice-title { text-align: right; }
    .invoice-title h2 { font-size: 16pt; color: #333; margin: 0 0 8px; text-transform: uppercase; }
    .invoice-title .inv-number { font-size: 11pt; font-weight: 600; }
    .invoice-title .inv-date { font-size: 9pt; color: #666; }
    .parties { display: flex; justify-content: space-between; margin-bottom: 20px; }
    .party-box { width: 48%; }
    .party-box h4 { font-size: 9pt; text-transform: uppercase; color: #888; margin: 0 0 4px; letter-spacing: 0.5px; }
    .party-box p { margin: 2px 0; font-size: 10pt; }
    .items-table { width: 100%; border-collapse: collapse; margin-bottom: 16px; }
    .items-table th { background: #f5f7fa; border: 1px solid #ddd; padding: 8px 10px; font-size: 8pt; text-transform: uppercase; text-align: left; }
    .items-table td { border: 1px solid #ddd; padding: 7px 10px; font-size: 9.5pt; }
    .items-table .text-end { text-align: right; }
    .items-table .sno { width: 30px; text-align: center; }
    .totals-section { display: flex; justify-content: flex-end; margin-bottom: 20px; }
    .totals-table { width: 280px; }
    .totals-table tr td { padding: 5px 10px; font-size: 10pt; }
    .totals-table tr td:last-child { text-align: right; font-weight: 500; }
    .totals-table .grand-total td { font-size: 12pt; font-weight: 700; border-top: 2px solid #333; padding-top: 8px; }
    .payment-terms { margin-bottom: 16px; }
    .payment-terms h4 { font-size: 9pt; text-transform: uppercase; color: #888; margin: 0 0 6px; }
    .payment-terms table { width: 100%; border-collapse: collapse; font-size: 9pt; }
    .payment-terms th, .payment-terms td { border: 1px solid #eee; padding: 5px 8px; }
    .footer { border-top: 1px solid #ddd; padding-top: 12px; font-size: 8.5pt; color: #666; }
    .footer .notes { margin-bottom: 8px; }
    .footer .bank-details { margin-bottom: 8px; }
    .footer .thank-you { text-align: center; font-style: italic; margin-top: 16px; }
    .footer .computer-generated { text-align: center; font-size: 7.5pt; color: #999; margin-top: 4px; }
    .badge-doc-type { display: inline-block; padding: 2px 8px; border-radius: 3px; font-size: 8pt; font-weight: 600; text-transform: uppercase; }
    .badge-invoice { background: #e3f2fd; color: #1565c0; }
    .badge-credit-note { background: #fff3e0; color: #e65100; }

    /* LHDN e-Invoice QR Code */
    .einvoice-section { display: flex; align-items: center; gap: 16px; margin: 20px 0; padding: 12px; border: 1px dashed #1976d2; border-radius: 4px; background: #f8fbff; }
    .einvoice-qr { flex-shrink: 0; }
    .qr-image { width: 80px; height: 80px; }
    .einvoice-info { font-size: 8.5pt; color: #555; }
    .einvoice-label { font-weight: 600; color: #1976d2; margin: 0 0 4px; font-size: 9pt; }
    .einvoice-uuid { margin: 2px 0; font-family: monospace; font-size: 7.5pt; color: #666; word-break: break-all; }
    .einvoice-link a { color: #1976d2; text-decoration: underline; font-size: 8pt; }

    /* Document Status Watermark */
    .watermark { position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%) rotate(-35deg); font-size: 72pt; font-weight: 900; color: rgba(200, 50, 50, 0.08); text-transform: uppercase; pointer-events: none; z-index: 0; letter-spacing: 8px; white-space: nowrap; }

    @media screen {
      :host { display: none; }
      :host(.show-preview) { display: block; border: 1px solid #ddd; border-radius: 4px; background: white; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }
    }
    @media print {
      :host { display: block !important; }
      .print-invoice { padding: 10mm; }
    }
  `],
  template: `
    @if (invoice) {
      <div class="print-invoice">
        <!-- Header: Company Info + Invoice Title -->
        <div class="print-header">
          <div class="company-info">
            <h1>{{ company?.name || 'Company Name' }}</h1>
            @if (company?.registrationNumber) {
              <p>{{ 'RegistrationNumber' | abpLocalization }}: {{ company.registrationNumber }}</p>
            }
            @if (company?.taxId) {
              <p>TIN: {{ company.taxId }}</p>
            }
            @if (company?.sstRegistrationNumber) {
              <p>SST: {{ company.sstRegistrationNumber }}</p>
            }
            @if (company?.address) {
              <p>{{ company.address }}</p>
            }
            @if (company?.city || company?.state) {
              <p>{{ company.city }}@if (company?.state) {
                <span>, {{ company.state }}</span>
              } {{ company.postalCode }}</p>
            }
            @if (company?.phone) {
              <p>Tel: {{ company.phone }}</p>
            }
            @if (company?.email) {
              <p>{{ company.email }}</p>
            }
          </div>
          <div class="invoice-title">
            <h2>
              @if (!invoice.isReturn) {
                <span>{{ 'Tax Invoice' }}</span>
              }
              @if (invoice.isReturn) {
                <span>{{ 'Credit Note' }}</span>
              }
            </h2>
            <div class="inv-number">{{ invoice.invoiceNumber }}</div>
            <div class="inv-date">{{ 'Date' | abpLocalization }}: {{ invoice.issueDate | date:'dd/MM/yyyy' }}</div>
            @if (invoice.dueDate) {
              <div class="inv-date">{{ 'DueDate' | abpLocalization }}: {{ invoice.dueDate | date:'dd/MM/yyyy' }}</div>
            }
          </div>
        </div>
        <!-- Parties: Bill To + Ship To -->
        <div class="parties">
          <div class="party-box">
            <h4>{{ 'BillTo' | abpLocalization }}</h4>
            <p><strong>{{ invoice.customerName || '—' }}</strong></p>
            @if (invoice.customerTin) {
              <p>TIN: {{ invoice.customerTin }}</p>
            }
            @if (invoice.billingAddress) {
              <p>{{ invoice.billingAddress }}</p>
            }
          </div>
          @if (invoice.shippingAddress) {
            <div class="party-box">
              <h4>{{ 'ShipTo' | abpLocalization }}</h4>
              <p>{{ invoice.shippingAddress }}</p>
            </div>
          }
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
            @for (item of invoice.items; track item; let i = $index) {
              <tr>
                <td class="sno">{{ i + 1 }}</td>
                <td>{{ item.description || item.itemName || '—' }}</td>
                <td class="text-end">{{ formatNumber(item.quantity) }}</td>
                <td class="text-end">{{ item.uom || item.stockUom || 'Unit' }}</td>
                <td class="text-end">{{ formatCurrency(item.unitPrice) }}</td>
                <td class="text-end">{{ formatCurrency(item.quantity * item.unitPrice) }}</td>
              </tr>
            }
          </tbody>
        </table>
        <!-- Totals -->
        <div class="totals-section">
          <table class="totals-table">
            <tr>
              <td>{{ 'NetTotal' | abpLocalization }}</td>
              <td>{{ formatCurrency(invoice.netTotal) }}</td>
            </tr>
            @if (invoice.discountAmount > 0) {
              <tr>
                <td>{{ 'Discount' | abpLocalization }}</td>
                <td>- {{ formatCurrency(invoice.discountAmount) }}</td>
              </tr>
            }
            @if (invoice.taxAmount > 0) {
              <tr>
                <td>{{ taxLabel }}</td>
                <td>{{ formatCurrency(invoice.taxAmount) }}</td>
              </tr>
            }
            <tr class="grand-total">
              <td>{{ 'GrandTotal' | abpLocalization }}</td>
              <td>{{ invoice.currencyCode || 'MYR' }} {{ formatCurrency(invoice.grandTotal) }}</td>
            </tr>
            @if (invoice.amountPaid > 0) {
              <tr>
                <td>{{ 'Paid' | abpLocalization }}</td>
                <td>{{ formatCurrency(invoice.amountPaid) }}</td>
              </tr>
            }
            @if (invoice.outstandingAmount > 0) {
              <tr>
                <td><strong>{{ 'Outstanding' | abpLocalization }}</strong></td>
                <td><strong>{{ formatCurrency(invoice.outstandingAmount) }}</strong></td>
              </tr>
            }
          </table>
        </div>
        <!-- Payment Terms -->
        @if (paymentSchedule?.length) {
          <div class="payment-terms">
            <h4>{{ 'PaymentTerms' | abpLocalization }}</h4>
            <table>
              <thead>
                <tr><th>{{ 'DueDate' | abpLocalization }}</th><th>%</th><th>{{ 'Amount' | abpLocalization }}</th><th>{{ 'Status' | abpLocalization }}</th></tr>
              </thead>
              <tbody>
                @for (term of paymentSchedule; track term) {
                  <tr>
                    <td>{{ term.dueDate | date:'dd/MM/yyyy' }}</td>
                    <td>{{ term.invoicePortion }}%</td>
                    <td>{{ formatCurrency(term.paymentAmount) }}</td>
                    <td>{{ term.paidAmount >= term.paymentAmount ? 'Paid' : 'Pending' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
        <!-- LHDN e-Invoice QR Code Section (Malaysia tax compliance) -->
        @if (invoice.lhdnLongId || invoice.qrCodeUrl) {
          <div class="einvoice-section">
            <div class="einvoice-qr">
              <img [src]="getQrCodeUrl()" alt="LHDN e-Invoice QR" class="qr-image" />
            </div>
            <div class="einvoice-info">
              <p class="einvoice-label">{{ 'PrintLayout:LhdnVerification' | abpLocalization }}</p>
              @if (invoice.lhdnUuid) {
                <p class="einvoice-uuid">UUID: {{ invoice.lhdnUuid }}</p>
              }
              <p class="einvoice-link">
                <a [href]="getLhdnVerificationUrl()" target="_blank">{{ 'PrintLayout:VerifyOnLhdn' | abpLocalization }}</a>
              </p>
            </div>
          </div>
        }

        <!-- Document Status Watermark (Draft/Cancelled overlays) -->
        @if (showWatermark) {
          <div class="watermark">{{ watermarkText }}</div>
        }

        <!-- Footer -->
        <div class="footer">
          @if (invoice.notes) {
            <div class="notes">
              <strong>{{ 'Notes' | abpLocalization }}:</strong> {{ invoice.notes }}
            </div>
          }
          @if (bankDetails) {
            <div class="bank-details">
              <strong>{{ 'BankDetails' | abpLocalization }}:</strong><br/>
              {{ bankDetails }}
            </div>
          }
          <div class="thank-you">{{ 'PrintLayout:ThankYou' | abpLocalization }}</div>
          @if (invoice.lhdnLongId) {
            <div class="computer-generated">{{ 'PrintLayout:ComputerGenerated' | abpLocalization }}</div>
          }
        </div>
      </div>
    }
    `
})
export class InvoicePrintLayoutComponent {
  @Input() invoice: any;
  @Input() company: any;
  @Input() paymentSchedule: any[] = [];
  @Input() bankDetails: string = '';
  @Input() taxLabel: string = 'SST (6%)';

  /** Whether to show a status watermark (Draft/Cancelled) */
  get showWatermark(): boolean {
    if (!this.invoice) return false;
    const status = this.invoice.status;
    return status === 0 || status === 'Draft' || status === 4 || status === 'Cancelled';
  }

  get watermarkText(): string {
    if (!this.invoice) return '';
    const status = this.invoice.status;
    if (status === 0 || status === 'Draft') return 'DRAFT';
    if (status === 4 || status === 'Cancelled') return 'CANCELLED';
    return '';
  }

  /** QR code URL for LHDN e-Invoice verification (per myinvois get_status.py) */
  getQrCodeUrl(): string {
    if (this.invoice?.qrCodeUrl) return this.invoice.qrCodeUrl;
    if (this.invoice?.lhdnLongId) {
      // Generate QR via QR Server API (same approach as myinvois generate_qr_code)
      const verifyUrl = `https://myinvois.hasil.gov.my/${this.invoice.lhdnLongId}/share`;
      return `https://api.qrserver.com/v1/create-qr-code/?size=80x80&data=${encodeURIComponent(verifyUrl)}`;
    }
    return '';
  }

  /** LHDN verification portal URL */
  getLhdnVerificationUrl(): string {
    if (this.invoice?.lhdnLongId) {
      return `https://myinvois.hasil.gov.my/${this.invoice.lhdnLongId}/share`;
    }
    return '';
  }

  formatCurrency(amount: number | undefined): string {
    if (amount == null) return '0.00';
    return Math.abs(amount).toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
  }

  formatNumber(qty: number | undefined): string {
    if (qty == null) return '0';
    const abs = Math.abs(qty);
    return abs % 1 === 0 ? abs.toString() : abs.toFixed(2);
  }
}
