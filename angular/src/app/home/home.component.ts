import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService, LocalizationPipe, LocalizationService } from '@abp/ng.core';
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
  stockValuation = signal<any | null>(null);
  overdueAlerts = signal<{ overdueReceivableCount: number; overdueReceivableAmount: number; overduePayableCount: number; overduePayableAmount: number; pendingApprovalCount: number } | null>(null);
  agingSummary = signal<any | null>(null);
  bankBalances = signal<any | null>(null);

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
        this.dashboardService.getStockValuationSummary(companyId)
          .subscribe({ next: data => this.stockValuation.set(data), error: () => {} });
        this.loadOverdueAlerts(companyId);
        this.dashboardService.getAgingSummaryWidget(companyId)
          .subscribe({ next: data => this.agingSummary.set(data), error: () => {} });
        this.dashboardService.getBankBalances(companyId)
          .subscribe({ next: data => this.bankBalances.set(data), error: () => {} });
      }
    }
  }

  private loadOverdueAlerts(companyId: string) {
    this.dashboardService.getOverdueAlerts(companyId).subscribe({
      next: (data: any) => this.overdueAlerts.set(data),
      error: () => this.overdueAlerts.set({ overdueReceivableCount: 0, overdueReceivableAmount: 0, overduePayableCount: 0, overduePayableAmount: 0, pendingApprovalCount: 0 }),
    });
  }

  isLoading = signal(false);

  quickLinks = [
    { labelKey: '::NewSalesInvoice', icon: 'fa-file-invoice', route: '/sales/invoices/new' },
    { labelKey: '::NewPurchaseOrder', icon: 'fa-cart-shopping', route: '/purchasing/orders/new' },
    { labelKey: '::NewJournalEntry', icon: 'fa-book', route: '/accounting/journal-entries/new' },
    { labelKey: '::LHDNDashboard', icon: 'fa-cloud-arrow-up', route: '/e-invoice/dashboard' },
    { labelKey: '::RunPayroll', icon: 'fa-money-bills', route: '/hr/payroll' },
    { labelKey: '::StockLedger', icon: 'fa-boxes-stacked', route: '/inventory/reports/stock-ledger' }];

  login() {
    this.authService.navigateToLogin();
  }
}
