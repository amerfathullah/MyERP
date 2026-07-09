import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService, LocalizationPipe } from '@abp/ng.core';
import { DashboardService } from '../proxy/core/dashboard.service';
import type { DashboardSummaryDto } from '../proxy/core/models';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe],
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss'],
})
export class HomeComponent implements OnInit {
  private authService = inject(AuthService);
  private dashboardService = inject(DashboardService);

  summary = signal<DashboardSummaryDto | null>(null);

  get hasLoggedIn(): boolean {
    return this.authService.isAuthenticated;
  }

  ngOnInit(): void {
    if (this.hasLoggedIn) {
      this.isLoading.set(true);
      this.dashboardService.getSummary().subscribe({
        next: s => {
          this.summary.set(s);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false),
      });
    }
  }

  isLoading = signal(false);

  quickLinks = [
    { label: 'New Sales Invoice', icon: 'fa-file-invoice', route: '/sales/invoices/new' },
    { label: 'New Purchase Order', icon: 'fa-cart-shopping', route: '/purchasing/orders/new' },
    { label: 'Journal Entry', icon: 'fa-book', route: '/accounting/journal-entries/new' },
    { label: 'LHDN Dashboard', icon: 'fa-cloud-arrow-up', route: '/e-invoice/dashboard' },
    { label: 'Run Payroll', icon: 'fa-money-bills', route: '/hr/payroll' },
    { label: 'Stock Ledger', icon: 'fa-boxes-stacked', route: '/inventory/reports/stock-ledger' }];

  login() {
    this.authService.navigateToLogin();
  }
}
