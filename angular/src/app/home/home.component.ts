import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '@abp/ng.core';
import { DashboardService } from '../proxy/core/dashboard.service';
import type { DashboardSummaryDto } from '../proxy/core/models';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterModule],
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
      this.dashboardService.getSummary().subscribe(s => this.summary.set(s));
    }
  }

  quickLinks = [
    { label: 'New Sales Invoice', icon: 'add_circle', route: '/sales/invoices/new' },
    { label: 'New Purchase Order', icon: 'add_circle', route: '/purchasing/orders/new' },
    { label: 'Journal Entry', icon: 'book', route: '/accounting/journal-entries/new' },
    { label: 'LHDN Dashboard', icon: 'dashboard', route: '/e-invoice/dashboard' },
    { label: 'Run Payroll', icon: 'payments', route: '/hr/payroll' },
    { label: 'Stock Ledger', icon: 'inventory', route: '/inventory/reports/stock-ledger' }];

  login() {
    this.authService.navigateToLogin();
  }
}
