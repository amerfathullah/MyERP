import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { AuthService } from '@abp/ng.core';
import { of } from 'rxjs';
import { HomeComponent } from './home.component';
import { DashboardService } from '../proxy/core/dashboard.service';
import { DocumentActivityLogService } from '../proxy/core/document-activity-log.service';
import { CompanyContextService } from '../shared/services/company-context.service';

describe('HomeComponent Dashboard Widgets', () => {
  let dashboardMock: any;

  function createComponent(authenticated = true): HomeComponent {
    const injector = Injector.create({
      providers: [
        { provide: AuthService, useValue: { isAuthenticated: authenticated, navigateToLogin: vi.fn() } },
        { provide: DashboardService, useValue: dashboardMock },
        { provide: DocumentActivityLogService, useValue: { getRecent: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
      ],
    });
    return runInInjectionContext(injector, () => new HomeComponent());
  }

  beforeEach(() => {
    dashboardMock = {
      getSummary: vi.fn().mockReturnValue(of({})),
      getLowStockItems: vi.fn().mockReturnValue(of([])),
      getExpiringBatches: vi.fn().mockReturnValue(of([])),
      getTopDebtors: vi.fn().mockReturnValue(of([])),
      getUpcomingPaymentDues: vi.fn().mockReturnValue(of(null)),
      getPendingMaterialRequests: vi.fn().mockReturnValue(of([])),
      getSupplierPerformanceWidget: vi.fn().mockReturnValue(of(null)),
      getRevenueTrend: vi.fn().mockReturnValue(of([])),
      getRevenueVsExpenseTrend: vi.fn().mockReturnValue(of([])),
      getFinancialKpis: vi.fn().mockReturnValue(of(null)),
      getStockValuationSummary: vi.fn().mockReturnValue(of(null)),
      getProfitMarginTrend: vi.fn().mockReturnValue(of([])),
      getOverdueAlerts: vi.fn().mockReturnValue(of(null)),
      getAgingSummaryWidget: vi.fn().mockReturnValue(of(null)),
      getCashFlowSnapshot: vi.fn().mockReturnValue(of(null)),
      getBankBalances: vi.fn().mockReturnValue(of(null)),
      getExpiringQuotations: vi.fn().mockReturnValue(of([])),
      getTopCustomers: vi.fn().mockReturnValue(of([])),
      getPendingOrdersSummary: vi.fn().mockReturnValue(of(null)),
      getProductionSummary: vi.fn().mockReturnValue(of(null)),
      getTodaysActivity: vi.fn().mockReturnValue(of(null)),
      getOperationalMetrics: vi.fn().mockReturnValue(of({
        draftDocuments: 4,
        pendingApprovals: 2,
        openWorkOrders: 3,
        activeSubscriptions: 7,
      })),
      getDeliveryDueAlerts: vi.fn().mockReturnValue(of({
        overdueCount: 2,
        dueThisWeekCount: 1,
        overdueOrders: [{ purchaseOrderId: 'po-1', orderNumber: 'PO-001', daysOverdue: 5 }],
      })),
    };
  });

  it('loads operationalMetrics and deliveryDueAlerts when authenticated with active company', () => {
    const comp = createComponent(true);
    comp.ngOnInit();

    expect(dashboardMock.getOperationalMetrics).toHaveBeenCalledWith('comp-1');
    expect(dashboardMock.getDeliveryDueAlerts).toHaveBeenCalledWith('comp-1');
    expect(comp.operationalMetrics()?.pendingApprovals).toBe(2);
    expect(comp.deliveryDueAlerts()?.overdueCount).toBe(2);
    expect(comp.deliveryDueAlerts()?.overdueOrders).toHaveLength(1);
  });
});
