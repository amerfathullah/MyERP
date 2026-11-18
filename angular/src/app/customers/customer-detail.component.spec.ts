import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
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
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new CustomerDetailComponent());
    comp.ngOnInit();

    expect(customerMock.getCustomerCompanies).toHaveBeenCalledWith('cust-1');
    expect(comp.allowedCompanies()).toEqual([{ id: 'comp-2', name: 'Subsidiary Co' }]);
    expect(comp.companies()).toEqual([{ id: 'comp-2', name: 'Subsidiary Co' }]);
    expect(comp.selectedCompanyId()).toBe('comp-2');
  });

  it('surfaces toaster error when customer load fails', () => {
    const toasterMock = { error: vi.fn() };
    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { params: { id: 'cust-1' } } } },
        { provide: CustomerService, useValue: { get: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Customer load failed' } } }))) } },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: PaymentReconciliationService, useValue: { getOutstandingInvoices: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: PartyPerformanceService, useValue: { getCustomerPerformance: vi.fn().mockReturnValue(of(null)) } },
        { provide: PartyDashboardService, useValue: { getCustomerDashboard: vi.fn().mockReturnValue(of(null)) } },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new CustomerDetailComponent());
    comp.ngOnInit();

    expect(comp.loading()).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('Customer load failed');
  });

  it('defers overview and transactions until requestOverview is clicked when loadCustomerOverview is false', () => {
    const customerMock = {
      get: vi.fn().mockReturnValue(of({ id: 'cust-1', name: 'Acme', companyId: 'comp-1', loadCustomerOverview: false })),
      getCustomerCompanies: vi.fn().mockReturnValue(of(['comp-1'])),
      getCustomerOverview: vi.fn().mockReturnValue(of(null)),
      getCustomerTransactions: vi.fn().mockReturnValue(of([])),
    };
    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { params: { id: 'cust-1' } } } },
        { provide: CustomerService, useValue: customerMock },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'comp-1', name: 'Co 1' }] })) } },
        { provide: PaymentReconciliationService, useValue: { getOutstandingInvoices: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: PartyPerformanceService, useValue: { getCustomerPerformance: vi.fn().mockReturnValue(of(null)) } },
        { provide: PartyDashboardService, useValue: { getCustomerDashboard: vi.fn().mockReturnValue(of(null)) } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new CustomerDetailComponent());
    comp.ngOnInit();

    expect(comp.overviewDeferred()).toBe(true);
    expect(customerMock.getCustomerOverview).not.toHaveBeenCalled();
    expect(customerMock.getCustomerTransactions).not.toHaveBeenCalled();

    comp.requestOverview();

    expect(comp.overviewDeferred()).toBe(false);
    expect(customerMock.getCustomerOverview).toHaveBeenCalledTimes(1);
    expect(customerMock.getCustomerTransactions).toHaveBeenCalledTimes(1);
  });
});
