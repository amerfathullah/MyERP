import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LocalizationPipe } from '@abp/ng.core';

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
  imports: [CommonModule, LocalizationPipe],
  template: `
    @if (salesOrder) {
      <div class="so-print-layout">
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
            <h4 class="text-uppercase text-primary fw-bold mb-1">{{ '::SalesOrder' | abpLocalization }}</h4>
            <p class="mb-0"><strong>{{ '::OrderNumberShort' | abpLocalization }}:</strong> {{ salesOrder.orderNumber }}</p>
            <p class="mb-0"><strong>{{ '::Date' | abpLocalization }}:</strong> {{ salesOrder.orderDate | date:'dd/MM/yyyy' }}</p>
            @if (salesOrder.customerPo) {
              <p class="mb-0"><strong>{{ '::YourPoNumberShort' | abpLocalization }}:</strong> {{ salesOrder.customerPo }}</p>
            }
            @if (salesOrder.deliveryDate) {
              <p class="mb-0"><strong>{{ '::DeliveryBy' | abpLocalization }}:</strong> {{ salesOrder.deliveryDate | date:'dd/MM/yyyy' }}</p>
            }
          </div>
        </div>
        <hr class="mb-4">
        <!-- Customer & Delivery Info -->
        <div class="row mb-4">
          <div class="col-6">
            <h6 class="text-uppercase text-muted fw-bold mb-2">{{ '::Customer' | abpLocalization }}</h6>
            <p class="fw-bold mb-1">{{ customerName || ('::NotAvailableShort' | abpLocalization) }}</p>
            @if (billingAddress) {
              <p class="text-muted mb-0">{{ billingAddress }}</p>
            }
            @if (customerTin) {
              <p class="text-muted mb-0">{{ '::Tin' | abpLocalization }}: {{ customerTin }}</p>
            }
            @if (contactPerson) {
              <p class="text-muted mb-0">{{ '::Attn' | abpLocalization }}: {{ contactPerson }}</p>
            }
            @if (contactPhone) {
              <p class="text-muted mb-0">{{ '::Tel' | abpLocalization }}: {{ contactPhone }}</p>
            }
          </div>
          <div class="col-6">
            <h6 class="text-uppercase text-muted fw-bold mb-2">{{ '::DeliverTo' | abpLocalization }}</h6>
            <p class="fw-bold mb-1">{{ shippingName || customerName || ('::NotAvailableShort' | abpLocalization) }}</p>
            @if (shippingAddress) {
              <p class="text-muted mb-0">{{ shippingAddress }}</p>
            }
            @if (shippingPhone) {
              <p class="text-muted mb-0">{{ '::Tel' | abpLocalization }}: {{ shippingPhone }}</p>
            }
          </div>
        </div>
        <!-- Order References -->
        @if (salesPerson || paymentTerms) {
          <div class="row mb-3">
            @if (salesPerson) {
              <div class="col-4">
                <small class="text-muted">{{ '::SalesPerson' | abpLocalization }}</small>
                <p class="fw-bold mb-0">{{ salesPerson }}</p>
              </div>
            }
            @if (paymentTerms) {
              <div class="col-4">
                <small class="text-muted">{{ '::PaymentTerms' | abpLocalization }}</small>
                <p class="fw-bold mb-0">{{ paymentTerms }}</p>
              </div>
            }
            @if (currency && currency !== 'MYR') {
              <div class="col-4">
                <small class="text-muted">{{ '::Currency' | abpLocalization }}</small>
                <p class="fw-bold mb-0">{{ currency }} ({{ '::Rate' | abpLocalization }}: {{ salesOrder.exchangeRate | number:'1.4-4' }})</p>
              </div>
            }
          </div>
        }
        <!-- Items Table -->
        <table class="table table-bordered mb-4">
          <thead class="table-light">
            <tr>
              <th style="width: 40px" class="text-center">{{ '::SerialNoShort' | abpLocalization }}</th>
              <th>{{ '::ItemDescription' | abpLocalization }}</th>
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
            @for (item of salesOrder.items; track item; let i = $index) {
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
                    @if (item.deliveryDate) {
                      <br>
                      }
                      @if (item.deliveryDate) {
                        <small class="text-muted">{{ '::Delivery' | abpLocalization }}: {{ item.deliveryDate | date:'dd/MM/yyyy' }}</small>
                      }
                    </td>
                    <td class="text-end font-monospace">{{ item.quantity | number:'1.0-2' }}</td>
                    <td class="text-end">{{ item.uom || item.stockUom || 'Unit' }}</td>
                    <td class="text-end font-monospace">{{ item.unitPrice | number:'1.2-2' }}</td>
                    @if (hasDiscounts) {
                      <td class="text-end font-monospace">{{ item.discountPercentage ? (item.discountPercentage | number:'1.0-1') + '%' : '—' }}</td>
                    }
                    <td class="text-end font-monospace fw-semibold">{{ (item.quantity * item.unitPrice) | number:'1.2-2' }}</td>
                  </tr>
                }
              </tbody>
            </table>
            <!-- Totals Section -->
            <div class="row">
              <div class="col-7">
                <!-- Terms & Conditions -->
                @if (termsAndConditions) {
                  <div>
                    <h6 class="text-uppercase text-muted fw-bold mb-2">{{ '::TermsAndConditions' | abpLocalization }}</h6>
                    <p class="small" style="white-space: pre-line">{{ termsAndConditions }}</p>
                  </div>
                }
                @if (salesOrder.notes) {
                  <div>
                    <h6 class="text-uppercase text-muted fw-bold mb-2">{{ '::Notes' | abpLocalization }}</h6>
                    <p class="small" style="white-space: pre-line">{{ salesOrder.notes }}</p>
                  </div>
                }
              </div>
              <div class="col-5">
                <table class="table table-sm table-borderless">
                  <tbody>
                    <tr>
                      <td class="text-muted">{{ '::NetTotal' | abpLocalization }}:</td>
                      <td class="text-end font-monospace">{{ currency }} {{ salesOrder.netTotal | number:'1.2-2' }}</td>
                    </tr>
                    @if (salesOrder.taxAmount) {
                      <tr>
                        <td class="text-muted">{{ '::TaxSst' | abpLocalization }}:</td>
                        <td class="text-end font-monospace">{{ currency }} {{ salesOrder.taxAmount | number:'1.2-2' }}</td>
                      </tr>
                    }
                    @if (salesOrder.shippingCharge) {
                      <tr>
                        <td class="text-muted">{{ '::Shipping' | abpLocalization }}:</td>
                        <td class="text-end font-monospace">{{ currency }} {{ salesOrder.shippingCharge | number:'1.2-2' }}</td>
                      </tr>
                    }
                    @if (salesOrder.discountAmount) {
                      <tr>
                        <td class="text-muted">{{ '::Discount' | abpLocalization }}:</td>
                        <td class="text-end font-monospace text-danger">({{ currency }} {{ salesOrder.discountAmount | number:'1.2-2' }})</td>
                      </tr>
                    }
                    <tr class="border-top">
                      <td class="fw-bold">{{ '::GrandTotal' | abpLocalization }}:</td>
                      <td class="text-end font-monospace fw-bold fs-5">{{ currency }} {{ salesOrder.grandTotal | number:'1.2-2' }}</td>
                    </tr>
                    @if (salesOrder.advancePaid) {
                      <tr>
                        <td class="text-muted">{{ '::AdvancePaid' | abpLocalization }}:</td>
                        <td class="text-end font-monospace text-success">{{ currency }} {{ salesOrder.advancePaid | number:'1.2-2' }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
            <!-- Acceptance Signature -->
            <div class="row mt-5 pt-4">
              <div class="col-6">
                <div class="border-bottom mb-2" style="height: 60px;"></div>
                <small class="text-muted">{{ '::AuthorizedSignatureSeller' | abpLocalization }}</small>
                <br><small class="text-muted">{{ '::Date' | abpLocalization }}: _______________</small>
              </div>
              <div class="col-6">
                <div class="border-bottom mb-2" style="height: 60px;"></div>
                <small class="text-muted">{{ '::AcceptedByCustomer' | abpLocalization }}</small>
                <br><small class="text-muted">{{ '::Date' | abpLocalization }}: _______________</small>
              </div>
            </div>
            <!-- Footer -->
            <div class="text-center mt-4 pt-3 border-top">
              <small class="text-muted">{{ '::ComputerGeneratedDocument' | abpLocalization }} {{ '::PleaseSignAndReturnOrder' | abpLocalization }}</small>
            </div>
          </div>
        }
    `,
  changeDetection: ChangeDetectionStrategy.Eager,
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
