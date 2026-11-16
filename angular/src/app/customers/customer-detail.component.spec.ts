import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { CustomerDetailComponent } from './customer-detail.component';
import { CustomerService } from '../proxy/sales/customer.service';
import { CompanyService } from '../proxy/core/company.service';
import { PaymentReconciliationService } from '../proxy/accounting/payment-reconciliation.service';
import { PartyPerformanceService } from '../proxy/core/party-performance.service';
import { PartyDashboardService } from '../proxy/accounting/party-dashboard.service';

describe('CustomerDetailComponent Allowed Companies', () => {
  it('filters overview companies by getCustomerCompanies when non-empty', () => {
    const customerMock = {
      get: vi.fn().mockReturnValue(of({ id: 'cust-1', name: 'Acme', companyId: 'comp-2' })),
      getCustomerCompanies: vi.fn().mockReturnValue(of(['comp-2'])),
      getCustomerOverview: vi.fn().mockReturnValue(of(null)),
      getCustomerTransactions: vi.fn().mockReturnValue(of([])),
    };
    const companyMock = {
      getList: vi.fn().mockReturnValue(of({
        items: [
          { id: 'comp-1', name: 'Parent Co' },
          { id: 'comp-2', name: 'Subsidiary Co' },
        ],
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { params: { id: 'cust-1' } } } },
        { provide: CustomerService, useValue: customerMock },
        { provide: CompanyService, useValue: companyMock },
        { provide: PaymentReconciliationService, useValue: { getOutstandingInvoices: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: PartyPerformanceService, useValue: { getCustomerPerformance: vi.fn().mockReturnValue(of(null)) } },
        { provide: PartyDashboardService, useValue: { getCustomerDashboard: vi.fn().mockReturnValue(of(null)) } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new CustomerDetailComponent());
    comp.ngOnInit();

    expect(customerMock.getCustomerCompanies).toHaveBeenCalledWith('cust-1');
    expect(comp.allowedCompanies()).toEqual([{ id: 'comp-2', name: 'Subsidiary Co' }]);
    expect(comp.companies()).toEqual([{ id: 'comp-2', name: 'Subsidiary Co' }]);
    expect(comp.selectedCompanyId()).toBe('comp-2');
  });
});
