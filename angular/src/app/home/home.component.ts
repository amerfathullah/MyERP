import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService, LocalizationPipe } from '@abp/ng.core';
import { DashboardService } from '../proxy/core/dashboard.service';
import { DocumentActivityLogService } from '../proxy/core/document-activity-log.service';
import type { DashboardSummaryDto } from '../proxy/core/models';
import { CompanyContextService } from '../shared/services/company-context.service';

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
  private activityLogService = inject(DocumentActivityLogService);
  private companyContext = inject(CompanyContextService);

  summary = signal<DashboardSummaryDto | null>(null);
  lowStockItems = signal<any[]>([]);
  revenueTrend = signal<{ month: string; amount: number; heightPct: number }[]>([]);
  recentActivity = signal<any[]>([]);
  financialKpis = signal<any | null>(null);

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
      this.dashboardService.getLowStockItems()
        .subscribe({ next: items => this.lowStockItems.set(items ?? []), error: () => {} });
      this.dashboardService.getRevenueTrend()
        .subscribe({
          next: data => {
            if (!data?.length) return;
            const maxAmount = Math.max(...data.map(d => d.amount), 1);
            this.revenueTrend.set(data.map(d => ({
              month: d.month,
              amount: d.amount,
              heightPct: (d.amount / maxAmount) * 100,
            })));
          },
          error: () => {},
        });
      const companyId = this.companyContext.currentCompanyId();
      if (companyId) {
        this.activityLogService.getRecent(companyId, 0, 10)
          .subscribe({ next: res => this.recentActivity.set(res?.items ?? []), error: () => {} });
        this.dashboardService.getFinancialKpis(companyId)
          .subscribe({ next: kpis => this.financialKpis.set(kpis), error: () => {} });
      }
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
