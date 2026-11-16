import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { SupplierDetailComponent } from './supplier-detail.component';
import { SupplierService } from '../proxy/purchasing/supplier.service';
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
        { provide: CompanyService, useValue: companyMock },
        { provide: PaymentReconciliationService, useValue: { getOutstandingInvoices: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: PartyPerformanceService, useValue: { getSupplierPerformance: vi.fn().mockReturnValue(of(null)) } },
        { provide: PartyDashboardService, useValue: { getSupplierDashboard: vi.fn().mockReturnValue(of(null)) } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new SupplierDetailComponent());
    comp.ngOnInit();

    expect(supplierMock.getSupplierCompanyIds).toHaveBeenCalledWith('sup-1');
    expect(comp.allowedCompanies()).toEqual([{ id: 'comp-2', name: 'Trading Co' }]);
    expect(comp.companies()).toEqual([{ id: 'comp-2', name: 'Trading Co' }]);
    expect(comp.selectedCompanyId()).toBe('comp-2');
  });
});
