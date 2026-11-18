import { describe, it, expect, vi, beforeEach } from 'vitest';
import { of } from 'rxjs';
import { Router } from '@angular/router';
import { Injector, runInInjectionContext } from '@angular/core';
import { ToasterService } from '@abp/ng.theme.shared';
import { SalesPartnerCommissionSummaryComponent } from './sales-partner-commission-summary.component';
import { SalesPartnerCommissionSummaryService } from '../../../proxy/sales/sales-partner-commission-summary.service';
import { SalesPartnerService } from '../../../proxy/sales/sales-partner.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('SalesPartnerCommissionSummaryComponent', () => {
  let reportMock: { getReport: ReturnType<typeof vi.fn> };
  let partnerMock: { getList: ReturnType<typeof vi.fn> };
  let routerMock: { navigate: ReturnType<typeof vi.fn> };
  let comp: SalesPartnerCommissionSummaryComponent;

  beforeEach(() => {
    reportMock = {
      getReport: vi.fn().mockReturnValue(of({
        currency: 'MYR',
        docType: 'Sales Invoice',
        transactionCount: 1,
        totalAmount: 2000,
        totalCommission: 100,
        rows: [
          {
            transactionId: 'si-1',
            transactionNumber: 'SINV-001',
            docType: 'Sales Invoice',
            postingDate: '2026-11-18T00:00:00Z',
            customerName: 'Acme Trading',
            salesPartnerName: 'Partner Alpha',
            commissionRate: 5,
            amount: 2000,
            totalCommission: 100,
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

    comp = runInInjectionContext(injector, () => new SalesPartnerCommissionSummaryComponent());
  });

  it('loads sales partners and commission summary report on init', () => {
    comp.ngOnInit();

    expect(partnerMock.getList).toHaveBeenCalled();
    expect(reportMock.getReport).toHaveBeenCalledWith(expect.objectContaining({
      companyId: 'comp-1',
      docType: 'Sales Invoice',
    }));
    expect(comp.report()?.totalCommission).toBe(100);
  });

  it('drills down to Sales Order or Sales Invoice detail', () => {
    comp.drillDown({ transactionId: 'so-1', docType: 'Sales Order' });
    expect(routerMock.navigate).toHaveBeenCalledWith(['/sales/orders', 'so-1']);

    comp.drillDown({ transactionId: 'si-1', docType: 'Sales Invoice' });
    expect(routerMock.navigate).toHaveBeenCalledWith(['/sales/invoices', 'si-1']);
  });
});
