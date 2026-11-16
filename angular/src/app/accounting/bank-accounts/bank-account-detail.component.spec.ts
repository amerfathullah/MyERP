import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { BankAccountDetailComponent } from './bank-account-detail.component';
import { BankAccountService } from '../../proxy/accounting/bank-account.service';
import { BankAccountBalanceService } from '../../proxy/accounting/bank-account-balance.service';

describe('BankAccountDetailComponent', () => {
  let accountMock: any;
  let balanceMock: any;

  function createComponent(): BankAccountDetailComponent {
    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'ba-1' } } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: BankAccountService, useValue: accountMock },
        { provide: BankAccountBalanceService, useValue: balanceMock },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
        { provide: ConfirmationService, useValue: {} },
      ],
    });
    return runInInjectionContext(injector, () => new BankAccountDetailComponent());
  }

  beforeEach(() => {
    accountMock = { get: vi.fn().mockReturnValue(of({ id: 'ba-1', accountName: 'Maybank Main' })) };
    balanceMock = {
      getAllList: vi.fn().mockReturnValue(of([
        { id: 'bal-1', bankAccountId: 'ba-1', date: '2026-10-31', balance: 50000 },
        { id: 'bal-2', bankAccountId: 'ba-1', date: '2026-11-15', balance: 62500 },
      ])),
    };
  });

  it('loads bank account and balance history via BankAccountBalanceService.getAllList', () => {
    const comp = createComponent();
    comp.ngOnInit();

    expect(accountMock.get).toHaveBeenCalledWith('ba-1');
    expect(balanceMock.getAllList).toHaveBeenCalledWith('ba-1');
    expect(comp.balances()).toHaveLength(2);
    expect(comp.balances()[1].balance).toBe(62500);
  });

  it('falls back to empty balance list when balance fetch errors', () => {
    balanceMock.getAllList.mockReturnValue(throwError(() => new Error('err')));
    const comp = createComponent();
    comp.ngOnInit();

    expect(comp.balances()).toEqual([]);
  });
});
