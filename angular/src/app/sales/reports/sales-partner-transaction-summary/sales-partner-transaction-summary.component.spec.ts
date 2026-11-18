import { describe, it, expect, vi, beforeEach } from 'vitest';
import { of } from 'rxjs';
import { Router } from '@angular/router';
import { Injector, runInInjectionContext } from '@angular/core';
import { ToasterService } from '@abp/ng.theme.shared';
import { SalesPartnerTransactionSummaryComponent } from './sales-partner-transaction-summary.component';
import { SalesPartnerCommissionSummaryService } from '../../../proxy/sales/sales-partner-commission-summary.service';
import { SalesPartnerService } from '../../../proxy/sales/sales-partner.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('SalesPartnerTransactionSummaryComponent', () => {
  let reportMock: { getTransactionSummary: ReturnType<typeof vi.fn> };
  let partnerMock: { getList: ReturnType<typeof vi.fn> };
  let routerMock: { navigate: ReturnType<typeof vi.fn> };
  let comp: SalesPartnerTransactionSummaryComponent;

  beforeEach(() => {
    reportMock = {
      getTransactionSummary: vi.fn().mockReturnValue(of({
        currency: 'MYR',
        docType: 'Sales Invoice',
        totalQty: 5,
        totalAmount: 1000,
        totalCommission: 50,
        rows: [
          {
            transactionId: 'si-1',
            transactionNumber: 'SINV-001',
            docType: 'Sales Invoice',
            itemCode: 'ITM-01',
            quantity: 5,
            rate: 200,
            amount: 1000,
            salesPartnerName: 'Partner Alpha',
            commissionRate: 5,
            commission: 50,
          },
        ],
      })),
    };
    partnerMock = { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'sp-1', name: 'Partner Alpha' }] })) };
    routerMock = { navigate: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: Router, useValue: routerMock },
        { provide: SalesPartnerCommissionSummaryService, useValue: reportMock },
        { provide: SalesPartnerService, useValue: partnerMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    comp = runInInjectionContext(injector, () => new SalesPartnerTransactionSummaryComponent());
  });

  it('loads item-level partner transaction summary with GrantCommission totals', () => {
    comp.ngOnInit();

    expect(reportMock.getTransactionSummary).toHaveBeenCalledWith(expect.objectContaining({
      companyId: 'comp-1',
      docType: 'Sales Invoice',
      showReturnEntries: false,
    }));
    expect(comp.report()?.totalQty).toBe(5);
    expect(comp.report()?.totalCommission).toBe(50);
  });

  it('navigates to invoice or order detail on drillDown', () => {
    comp.drillDown({ transactionId: 'so-9', docType: 'Sales Order' });
    expect(routerMock.navigate).toHaveBeenCalledWith(['/sales/orders', 'so-9']);

    comp.drillDown({ transactionId: 'si-1', docType: 'Sales Invoice' });
    expect(routerMock.navigate).toHaveBeenCalledWith(['/sales/invoices', 'si-1']);
  });
});
