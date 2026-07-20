import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';

@Component({
  selector: 'app-settings-overview',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'Settings' | abpLocalization">
      <div class="row g-3">
        @for (section of sections; track section.title) {
          <div class="col-md-4">
            <div class="card h-100">
              <div class="card-body">
                <h6 class="card-title"><i class="fa {{ section.icon }} me-2 text-primary"></i>{{ section.title }}</h6>
                <p class="text-muted small mb-3">{{ section.description }}</p>
                <div class="d-grid gap-1">
                  @for (link of section.links; track link.label) {
                    <a [routerLink]="link.path" class="btn btn-sm btn-outline-secondary text-start">
                      <i class="fa {{ link.icon }} me-2"></i>{{ link.label }}
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
      title: 'Company', icon: 'fa-building', description: 'Company defaults, accounts, and frozen dates.',
      links: [
        { path: '/settings/company', label: 'Company Settings', icon: 'fa-sliders' },
        { path: '/accounting/fiscal-years', label: 'Fiscal Years', icon: 'fa-calendar-days' },
        { path: '/accounting/currency-exchange', label: 'Exchange Rates', icon: 'fa-exchange-alt' },
      ]
    },
    {
      title: 'Manufacturing', icon: 'fa-industry', description: 'Production, scheduling, and capacity settings.',
      links: [
        { path: '/manufacturing/settings', label: 'Manufacturing Settings', icon: 'fa-sliders' },
        { path: '/manufacturing/workstations', label: 'Workstations', icon: 'fa-desktop' },
      ]
    },
    {
      title: 'Sales & Pricing', icon: 'fa-tags', description: 'Pricing rules, shipping, loyalty, and commissions.',
      links: [
        { path: '/sales/pricing-rules', label: 'Pricing Rules', icon: 'fa-percent' },
        { path: '/sales/shipping-rules', label: 'Shipping Rules', icon: 'fa-truck-fast' },
        { path: '/sales/loyalty-programs', label: 'Loyalty Programs', icon: 'fa-gift' },
        { path: '/sales/sales-persons', label: 'Sales Persons', icon: 'fa-user-tie' },
      ]
    },
    {
      title: 'Inventory', icon: 'fa-boxes-stacked', description: 'Item attributes, batches, and stock settings.',
      links: [
        { path: '/inventory/item-attributes', label: 'Item Attributes', icon: 'fa-palette' },
        { path: '/inventory/batches', label: 'Batches', icon: 'fa-layer-group' },
        { path: '/inventory/serial-numbers', label: 'Serial Numbers', icon: 'fa-barcode' },
      ]
    },
    {
      title: 'Purchasing', icon: 'fa-cart-shopping', description: 'Supplier scorecards and procurement settings.',
      links: [
        { path: '/purchasing/scorecards', label: 'Supplier Scorecards', icon: 'fa-star-half-stroke' },
      ]
    },
    {
      title: 'Automation & Security', icon: 'fa-robot', description: 'Rules, templates, logs, and approvals.',
      links: [
        { path: '/settings/authorization-rules', label: 'Authorization Rules', icon: 'fa-shield-halved' },
        { path: '/settings/email-templates', label: 'Email Templates', icon: 'fa-envelope' },
        { path: '/settings/notification-logs', label: 'Notification Logs', icon: 'fa-bell' },
        { path: '/automation', label: 'Automation Rules', icon: 'fa-robot' },
        { path: '/settings/einvoice', label: 'E-Invoice (LHDN)', icon: 'fa-file-shield' },
      ]
    },
  ];
}
