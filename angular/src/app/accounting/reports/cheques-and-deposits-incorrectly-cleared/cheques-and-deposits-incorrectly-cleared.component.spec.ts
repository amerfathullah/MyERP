import { describe, it, expect, vi, beforeEach } from 'vitest';
import { of } from 'rxjs';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Injector, runInInjectionContext } from '@angular/core';
import { ToasterService } from '@abp/ng.theme.shared';
import {
  ChequesAndDepositsIncorrectlyClearedComponent,
  formatIsoDate,
  isBankOrCashLeafAccount,
} from './cheques-and-deposits-incorrectly-cleared.component';
import { BankReconciliationService } from '../../../proxy/accounting/bank-reconciliation.service';
import { AccountService } from '../../../proxy/accounting/account.service';
import { AccountSubType } from '../../../proxy/accounting/account-sub-type.enum';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('ChequesAndDepositsIncorrectlyClearedComponent', () => {
  let bankReconMock: { getIncorrectlyClearedEntries: ReturnType<typeof vi.fn> };
  let accountMock: { getList: ReturnType<typeof vi.fn> };
  let companyMock: { getList: ReturnType<typeof vi.fn> };
  let routerMock: { navigate: ReturnType<typeof vi.fn> };
  let comp: ChequesAndDepositsIncorrectlyClearedComponent;

  beforeEach(() => {
    bankReconMock = {
      getIncorrectlyClearedEntries: vi.fn().mockReturnValue(of([
        {
          postingDate: '2026-11-25T00:00:00Z',
          clearanceDate: '2026-11-15T00:00:00Z',
          documentType: 'Payment Entry',
          documentNumber: 'PE-0099',
          documentId: 'pe-99',
          debit: 500,
          credit: 0,
          referenceNumber: 'CHQ-101',
          accountCurrency: 'MYR',
        },
        {
          postingDate: '2026-11-22T00:00:00Z',
          clearanceDate: '2026-11-10T00:00:00Z',
          documentType: 'Journal Entry',
          documentNumber: 'JE-0042',
          documentId: 'je-42',
          debit: 0,
          credit: 150,
          referenceNumber: 'BANK-ADJ',
          accountCurrency: 'MYR',
        },
      ])),
    };
    accountMock = {
      getList: vi.fn().mockReturnValue(of({
        items: [
          { id: 'acc-ar', accountCode: '1200', accountName: 'Debtors', accountSubType: AccountSubType.AccountsReceivable, isGroup: false, isActive: true },
          { id: 'acc-bank', accountCode: '1110', accountName: 'Maybank', accountSubType: AccountSubType.BankAccount, isGroup: false, isActive: true, currency: 'MYR' },
          { id: 'acc-cash', accountCode: '1100', accountName: 'Petty Cash', accountSubType: AccountSubType.CashAccount, isGroup: false, isActive: true, currency: 'MYR' },
          { id: 'acc-grp', accountCode: '1100G', accountName: 'Bank Group', accountSubType: AccountSubType.BankAccount, isGroup: true, isActive: true },
        ],
      })),
    };
    companyMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'comp-1', name: 'Test Co' }] })),
    };
    routerMock = {
      navigate: vi.fn(),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: Router, useValue: routerMock },
        { provide: BankReconciliationService, useValue: bankReconMock },
        { provide: AccountService, useValue: accountMock },
        { provide: CompanyService, useValue: companyMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    comp = runInInjectionContext(injector, () => new ChequesAndDepositsIncorrectlyClearedComponent());
  });

  it('filters leaf bank/cash accounts and computes totals on init', () => {
    comp.ngOnInit();

    expect(comp.bankAccounts().map(a => a.id)).toEqual(['acc-bank', 'acc-cash']);
    expect(isBankOrCashLeafAccount({ accountSubType: AccountSubType.CashAccount, isGroup: false, isActive: true })).toBe(true);
    expect(isBankOrCashLeafAccount({ accountSubType: AccountSubType.BankAccount, isGroup: false, isActive: false })).toBe(false);

    expect(bankReconMock.getIncorrectlyClearedEntries).toHaveBeenCalledWith(expect.objectContaining({
      companyId: 'comp-1',
      bankAccountId: 'acc-bank',
    }));
    expect(comp.entryCount()).toBe(2);
    expect(comp.totalDebit()).toBe(500);
    expect(comp.totalCredit()).toBe(150);
    expect(comp.netDifference()).toBe(350);
    expect(formatIsoDate('2026-11-25T10:30:00Z')).toBe('2026-11-25');
  });

  it('drills down to Payment Entry or Journal Entry detail', () => {
    comp.drillDown({ documentType: 'Payment Entry', documentId: 'pe-99' });
    expect(routerMock.navigate).toHaveBeenCalledWith(['/accounting/payments', 'pe-99']);

    comp.drillDown({ documentType: 'Journal Entry', documentId: 'je-42' });
    expect(routerMock.navigate).toHaveBeenCalledWith(['/accounting/journal-entries', 'je-42']);
  });
});
