import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';

@Component({
  selector: 'app-settings-overview',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <abp-page [title]="'::Settings' | abpLocalization">
      <div class="row g-3">
        @for (section of sections; track section.titleKey) {
          <div class="col-md-4">
            <div class="card h-100">
              <div class="card-body">
                <h6 class="card-title"><i class="fa {{ section.icon }} me-2 text-primary"></i>{{ section.titleKey | abpLocalization }}</h6>
                <p class="text-muted small mb-3">{{ section.descriptionKey | abpLocalization }}</p>
                <div class="d-grid gap-1">
                  @for (link of section.links; track link.path) {
                    <a [routerLink]="link.path" class="btn btn-sm btn-outline-secondary text-start">
                      <i class="fa {{ link.icon }} me-2"></i>{{ link.labelKey | abpLocalization }}
                    </a>
                  }
                </div>
              </div>
            </div>
          </div>
        }
      </div>
    </abp-page>
  `
})
export class SettingsOverviewComponent {
  sections = [
    {
      titleKey: '::Company', icon: 'fa-building', descriptionKey: '::SettingsOverviewCompanyDesc',
      links: [
        { path: '/settings/company', labelKey: '::CompanySettings', icon: 'fa-sliders' },
        { path: '/settings/accounts', labelKey: '::AccountsSettings', icon: 'fa-calculator' },
        { path: '/settings/selling', labelKey: '::SellingSettings', icon: 'fa-chart-line' },
        { path: '/settings/buying', labelKey: '::BuyingSettings', icon: 'fa-cart-shopping' },
        { path: '/settings/stock', labelKey: '::StockSettings', icon: 'fa-boxes-stacked' },
        { path: '/accounting/fiscal-years', labelKey: '::FiscalYears', icon: 'fa-calendar-days' },
        { path: '/accounting/currency-exchange', labelKey: '::ExchangeRates', icon: 'fa-exchange-alt' },
        { path: '/accounting/period-closing', labelKey: '::PeriodClosing', icon: 'fa-lock' },
      ]
    },
    {
      titleKey: '::Accounting', icon: 'fa-calculator', descriptionKey: '::SettingsOverviewAccountingDesc',
      links: [
        { path: '/accounting/opening-balance', labelKey: '::OpeningBalance', icon: 'fa-scale-balanced' },
        { path: '/accounting/bank-transaction-rules', labelKey: '::BankTransactionRules', icon: 'fa-robot' },
        { path: '/accounting/invoice-discounting', labelKey: '::InvoiceDiscounting', icon: 'fa-percent' },
        { path: '/accounting/exchange-rate-revaluation', labelKey: '::ExchangeRateRevaluation', icon: 'fa-scale-unbalanced' },
        { path: '/accounting/finance-books', labelKey: '::FinanceBooks', icon: 'fa-book-bookmark' },
      ]
    },
    {
      titleKey: '::Manufacturing', icon: 'fa-industry', descriptionKey: '::SettingsOverviewManufacturingDesc',
      links: [
        { path: '/manufacturing/settings', labelKey: '::ManufacturingSettings', icon: 'fa-sliders' },
        { path: '/manufacturing/workstations', labelKey: '::Workstations', icon: 'fa-desktop' },
      ]
    },
    {
      titleKey: '::SalesAndPricing', icon: 'fa-tags', descriptionKey: '::SettingsOverviewSalesPricingDesc',
      links: [
        { path: '/sales/pricing-rules', labelKey: '::PricingRules', icon: 'fa-percent' },
        { path: '/sales/shipping-rules', labelKey: '::ShippingRules', icon: 'fa-truck-fast' },
        { path: '/sales/loyalty-programs', labelKey: '::LoyaltyPrograms', icon: 'fa-gift' },
        { path: '/sales/sales-persons', labelKey: '::SalesPersons', icon: 'fa-user-tie' },
      ]
    },
    {
      titleKey: '::InventoryAndStock', icon: 'fa-boxes-stacked', descriptionKey: '::SettingsOverviewInventoryDesc',
      links: [
        { path: '/inventory/item-attributes', labelKey: '::ItemAttributes', icon: 'fa-palette' },
        { path: '/inventory/batches', labelKey: '::Batches', icon: 'fa-layer-group' },
        { path: '/inventory/serial-numbers', labelKey: '::SerialNumbers', icon: 'fa-barcode' },
        { path: '/inventory/standard-costs', labelKey: '::StandardCosts', icon: 'fa-tag' },
        { path: '/inventory/stock-closing', labelKey: '::StockClosing', icon: 'fa-box-archive' },
      ]
    },
    {
      titleKey: '::Purchasing', icon: 'fa-cart-shopping', descriptionKey: '::SettingsOverviewPurchasingDesc',
      links: [
        { path: '/purchasing/scorecards', labelKey: '::SupplierScorecards', icon: 'fa-star-half-stroke' },
      ]
    },
    {
      titleKey: '::HrAndPayroll', icon: 'fa-users', descriptionKey: '::SettingsOverviewHrPayrollDesc',
      links: [
        { path: '/hr/leave-allocations', labelKey: '::LeaveAllocations', icon: 'fa-calendar-check' },
        { path: '/hr/holiday-lists', labelKey: '::HolidayLists', icon: 'fa-umbrella-beach' },
        { path: '/hr/salary-structures', labelKey: '::SalaryStructures', icon: 'fa-money-bill-wave' },
        { path: '/hr/loans', labelKey: '::Loans', icon: 'fa-hand-holding-dollar' },
      ]
    },
    {
      titleKey: '::Tax', icon: 'fa-receipt', descriptionKey: '::SettingsOverviewTaxDesc',
      links: [
        { path: '/tax/categories', labelKey: '::TaxCategories', icon: 'fa-percent' },
        { path: '/tax/summary', labelKey: '::SstTaxSummary', icon: 'fa-file-invoice-dollar' },
      ]
    },
    {
      titleKey: '::AutomationAndSecurity', icon: 'fa-robot', descriptionKey: '::SettingsOverviewAutomationDesc',
      links: [
        { path: '/settings/authorization-rules', labelKey: '::AuthorizationRules', icon: 'fa-shield-halved' },
        { path: '/settings/email-templates', labelKey: '::EmailTemplates', icon: 'fa-envelope' },
        { path: '/settings/notification-logs', labelKey: '::NotificationLogs', icon: 'fa-bell' },
        { path: '/automation', labelKey: '::AutomationRules', icon: 'fa-robot' },
        { path: '/settings/einvoice', labelKey: '::EInvoiceLhdn', icon: 'fa-file-shield' },
      ]
    },
  ];
}
