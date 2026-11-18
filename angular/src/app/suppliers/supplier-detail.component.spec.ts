import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { SupplierDetailComponent } from './supplier-detail.component';
import { SupplierService } from '../proxy/purchasing/supplier.service';
import { SupplierScorecardService } from '../proxy/purchasing/supplier-scorecard.service';
import { CompanyService } from '../proxy/core/company.service';
import { PaymentReconciliationService } from '../proxy/accounting/payment-reconciliation.service';
import { PartyPerformanceService } from '../proxy/core/party-performance.service';
import { PartyDashboardService } from '../proxy/accounting/party-dashboard.service';

describe('SupplierDetailComponent Allowed Companies', () => {
  it('filters overview companies by getSupplierCompanyIds when non-empty', () => {
    const supplierMock = {
      get: vi.fn().mockReturnValue(of({ id: 'sup-1', name: 'MegaParts', companyId: 'comp-2' })),
      getSupplierCompanyIds: vi.fn().mockReturnValue(of(['comp-2'])),
      getSupplierOverview: vi.fn().mockReturnValue(of(null)),
      getSupplierTransactions: vi.fn().mockReturnValue(of([])),
    };
    const scorecardMock = {
      getBySupplierIdBySupplierId: vi.fn().mockReturnValue(of({ id: 'sc-1', score: 92.5, currentStanding: 'Very Good', periodType: 'Monthly' })),
    };
    const companyMock = {
      getList: vi.fn().mockReturnValue(of({
        items: [
          { id: 'comp-1', name: 'Parent Co' },
          { id: 'comp-2', name: 'Trading Co' },
        ],
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { params: { id: 'sup-1' } } } },
        { provide: SupplierService, useValue: supplierMock },
        { provide: SupplierScorecardService, useValue: scorecardMock },
        { provide: CompanyService, useValue: companyMock },
        { provide: PaymentReconciliationService, useValue: { getOutstandingInvoices: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: PartyPerformanceService, useValue: { getSupplierPerformance: vi.fn().mockReturnValue(of(null)) } },
        { provide: PartyDashboardService, useValue: { getSupplierDashboard: vi.fn().mockReturnValue(of(null)) } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new SupplierDetailComponent());
    comp.ngOnInit();

    expect(supplierMock.getSupplierCompanyIds).toHaveBeenCalledWith('sup-1');
    expect(scorecardMock.getBySupplierIdBySupplierId).toHaveBeenCalledWith('sup-1');
    expect(comp.scorecard()?.score).toBe(92.5);
    expect(comp.allowedCompanies()).toEqual([{ id: 'comp-2', name: 'Trading Co' }]);
    expect(comp.companies()).toEqual([{ id: 'comp-2', name: 'Trading Co' }]);
    expect(comp.selectedCompanyId()).toBe('comp-2');
  });

  it('surfaces toaster error when supplier load fails', () => {
    const toasterMock = { error: vi.fn() };
    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { params: { id: 'sup-1' } } } },
        { provide: SupplierService, useValue: { get: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Supplier load failed' } } }))) } },
        { provide: SupplierScorecardService, useValue: { getBySupplierIdBySupplierId: vi.fn().mockReturnValue(of(null)) } },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: PaymentReconciliationService, useValue: { getOutstandingInvoices: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: PartyPerformanceService, useValue: { getSupplierPerformance: vi.fn().mockReturnValue(of(null)) } },
        { provide: PartyDashboardService, useValue: { getSupplierDashboard: vi.fn().mockReturnValue(of(null)) } },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new SupplierDetailComponent());
    comp.ngOnInit();

    expect(comp.loading()).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('Supplier load failed');
  });

  it('defers overview and transactions until requestOverview is clicked when loadSupplierOverview is false', () => {
    const supplierMock = {
      get: vi.fn().mockReturnValue(of({ id: 'sup-1', name: 'MegaParts', companyId: 'comp-1', loadSupplierOverview: false })),
      getSupplierCompanyIds: vi.fn().mockReturnValue(of(['comp-1'])),
      getSupplierOverview: vi.fn().mockReturnValue(of(null)),
      getSupplierTransactions: vi.fn().mockReturnValue(of([])),
    };
    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { params: { id: 'sup-1' } } } },
        { provide: SupplierService, useValue: supplierMock },
        { provide: SupplierScorecardService, useValue: { getBySupplierIdBySupplierId: vi.fn().mockReturnValue(of(null)) } },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'comp-1', name: 'Co 1' }] })) } },
        { provide: PaymentReconciliationService, useValue: { getOutstandingInvoices: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: PartyPerformanceService, useValue: { getSupplierPerformance: vi.fn().mockReturnValue(of(null)) } },
        { provide: PartyDashboardService, useValue: { getSupplierDashboard: vi.fn().mockReturnValue(of(null)) } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new SupplierDetailComponent());
    comp.ngOnInit();

    expect(comp.overviewDeferred()).toBe(true);
    expect(supplierMock.getSupplierOverview).not.toHaveBeenCalled();
    expect(supplierMock.getSupplierTransactions).not.toHaveBeenCalled();

    comp.requestOverview();

    expect(comp.overviewDeferred()).toBe(false);
    expect(supplierMock.getSupplierOverview).toHaveBeenCalledTimes(1);
    expect(supplierMock.getSupplierTransactions).toHaveBeenCalledTimes(1);
  });
});
