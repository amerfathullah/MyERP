import { Injector, runInInjectionContext } from '@angular/core';
import { Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { BankReconciliationComponent } from './bank-reconciliation.component';
import { BankReconciliationService } from '../../proxy/accounting/bank-reconciliation.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { AccountService } from '../../proxy/accounting/account.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';

describe('BankReconciliationComponent Opening Balance', () => {
  function createComponent() {
    const recServiceMock = {
      getTransactions: vi.fn().mockReturnValue(of({ items: [], totalCount: 0 })),
      getSummary: vi.fn().mockReturnValue(of({ totalTransactions: 3, reconciledCount: 2, unreconciledCount: 1, unreconciledBalance: 150 })),
      getAccountOpeningBalance: vi.fn().mockReturnValue(of(12500.75)),
      searchForMirrorTransaction: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Mirror lookup failed' } } }))),
    };
    const accountServiceMock = {
      getList: vi.fn().mockReturnValue(of({ items: [] })),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn(), warn: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: BankReconciliationService, useValue: recServiceMock },
        { provide: ToasterService, useValue: toasterMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1', load: vi.fn() } },
        { provide: AccountService, useValue: accountServiceMock },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: CustomerService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: SupplierService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new BankReconciliationComponent());
    return { comp, recServiceMock, toasterMock };
  }

  it('fetches account opening balance for selected bank account and tillDate', () => {
    const { comp, recServiceMock } = createComponent();
    comp.bankAccountId = 'bank-1';
    comp.fromDate = '2026-11-01';

    comp.onBankAccountChanged();

    expect(recServiceMock.getAccountOpeningBalance).toHaveBeenCalledWith('bank-1', '2026-11-01');
    expect(comp.openingBalance()).toBe(12500.75);
  });

  it('clears opening balance when bank account is deselected', () => {
    const { comp } = createComponent();
    comp.openingBalance.set(500);
    comp.bankAccountId = '';

    comp.onBankAccountChanged();

    expect(comp.openingBalance()).toBeNull();
  });

  it('shows toaster error when searching mirror transaction fails', () => {
    const { comp, toasterMock } = createComponent();

    comp.createTransfer('tx-1');

    expect(toasterMock.error).toHaveBeenCalledWith('Mirror lookup failed');
  });
});
