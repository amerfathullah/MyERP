import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { Router } from '@angular/router';
import { ListService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { CompanyListComponent } from './company-list.component';
import { CompanyService } from '../proxy/core/company.service';

describe('CompanyListComponent', () => {
  let companyServiceMock: any;
  let confirmationMock: any;
  let toasterMock: any;
  let listMock: any;
  let routerMock: any;

  function createComponent(): CompanyListComponent {
    const injector = Injector.create({
      providers: [
        { provide: CompanyService, useValue: companyServiceMock },
        { provide: ConfirmationService, useValue: confirmationMock },
        { provide: ToasterService, useValue: toasterMock },
        { provide: ListService, useValue: listMock },
        { provide: Router, useValue: routerMock },
      ],
    });
    return runInInjectionContext(injector, () => new CompanyListComponent());
  }

  beforeEach(() => {
    companyServiceMock = {
      getList: vi.fn().mockReturnValue(of({ items: [], totalCount: 0 })),
      repairSrbnbAndTaxPayableAccounts: vi.fn().mockReturnValue(of(void 0)),
    };
    confirmationMock = { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) };
    toasterMock = { success: vi.fn(), error: vi.fn() };
    listMock = { hookToQuery: vi.fn().mockReturnValue(of({ items: [{ id: 'c1', name: 'Alpha Sdn Bhd', taxId: 'C12345' }], totalCount: 1 })), get: vi.fn() };
    routerMock = { navigate: vi.fn() };
  });

  it('filters companies by searchTerm across name and taxId', () => {
    const comp = createComponent();
    comp.companies = [
      { id: 'c1', name: 'Alpha Sdn Bhd', taxId: 'C100', sstRegistrationNumber: 'SST-1' },
      { id: 'c2', name: 'Beta Trading', taxId: 'C200', sstRegistrationNumber: 'SST-2' },
    ];

    comp.searchTerm = 'c200';
    expect(comp.filteredCompanies.map(c => c.id)).toEqual(['c2']);

    comp.searchTerm = 'Alpha';
    expect(comp.filteredCompanies.map(c => c.id)).toEqual(['c1']);
  });

  it('calls repairSrbnbAndTaxPayableAccounts on confirmation', () => {
    const comp = createComponent();

    comp.repairAccounts({ id: 'c1', name: 'Alpha Sdn Bhd' });

    expect(confirmationMock.warn).toHaveBeenCalledWith('::RepairAccountsConfirmation', '::AreYouSure');
    expect(companyServiceMock.repairSrbnbAndTaxPayableAccounts).toHaveBeenCalledWith('c1');
    expect(comp.repairingCompanyId()).toBeNull();
    expect(toasterMock.success).toHaveBeenCalledWith('::AccountsRepaired');
  });

  it('does not call repair API when confirmation is cancelled', () => {
    confirmationMock.warn.mockReturnValue(of(Confirmation.Status.reject));
    const comp = createComponent();

    comp.repairAccounts({ id: 'c1', name: 'Alpha Sdn Bhd' });

    expect(companyServiceMock.repairSrbnbAndTaxPayableAccounts).not.toHaveBeenCalled();
  });

  it('resets repairingCompanyId and shows error toast on repair failure', () => {
    companyServiceMock.repairSrbnbAndTaxPayableAccounts.mockReturnValue(
      throwError(() => ({ error: { error: { message: 'Missing COA' } } }))
    );
    const comp = createComponent();

    comp.repairAccounts({ id: 'c1', name: 'Alpha Sdn Bhd' });

    expect(comp.repairingCompanyId()).toBeNull();
    expect(toasterMock.error).toHaveBeenCalledWith('Missing COA');
  });
});
