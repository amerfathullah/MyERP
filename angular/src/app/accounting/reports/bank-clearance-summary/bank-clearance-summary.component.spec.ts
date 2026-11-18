import { describe, it, expect, vi, beforeEach } from 'vitest';
import { of } from 'rxjs';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Injector, runInInjectionContext } from '@angular/core';
import { ToasterService } from '@abp/ng.theme.shared';
import {
  BankClearanceSummaryComponent,
  resolveBankClearanceSignedAmount,
} from './bank-clearance-summary.component';
import { BankClearanceService } from '../../../proxy/accounting/bank-clearance.service';
import { AccountService } from '../../../proxy/accounting/account.service';
import { AccountSubType } from '../../../proxy/accounting/account-sub-type.enum';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('BankClearanceSummaryComponent', () => {
  let clearanceMock: { getEntries: ReturnType<typeof vi.fn> };
  let accountMock: { getList: ReturnType<typeof vi.fn> };
  let companyMock: { getList: ReturnType<typeof vi.fn> };
  let routerMock: { navigate: ReturnType<typeof vi.fn> };
  let comp: BankClearanceSummaryComponent;

  beforeEach(() => {
    clearanceMock = {
      getEntries: vi.fn().mockReturnValue(of([
        {
          documentType: 'PaymentEntry',
          documentId: 'pe-1',
          sourceDocumentId: 'pe-1',
          documentNumber: 'PE-0001',
          postingDate: '2026-11-10T00:00:00Z',
          debit: 1000,
          credit: 0,
          amount: 1000,
          clearanceDate: '2026-11-12T00:00:00Z',
          againstAccount: 'Acme Customer',
          accountCurrency: 'MYR',
        },
        {
          documentType: 'PurchaseInvoice',
          documentId: 'je-2',
          sourceDocumentId: 'pi-2',
          documentNumber: 'PINV-0002',
          postingDate: '2026-11-15T00:00:00Z',
          debit: 0,
          credit: 250,
          clearanceDate: null,
          againstAccount: 'Vendor Sdn Bhd',
          accountCurrency: 'MYR',
        },
      ])),
    };
    accountMock = {
      getList: vi.fn().mockReturnValue(of({
        items: [
          { id: 'acc-bank', accountCode: '1110', accountName: 'Maybank', accountSubType: AccountSubType.BankAccount, isGroup: false, isActive: true, currency: 'MYR' },
        ],
      })),
    };
    companyMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'comp-1', name: 'Test Co' }] })),
    };
    routerMock = { navigate: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: Router, useValue: routerMock },
        { provide: BankClearanceService, useValue: clearanceMock },
        { provide: AccountService, useValue: accountMock },
        { provide: CompanyService, useValue: companyMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    comp = runInInjectionContext(injector, () => new BankClearanceSummaryComponent());
  });

  it('loads bank accounts with includeCleared=true and summarizes cleared vs uncleared counts', () => {
    comp.ngOnInit();

    expect(clearanceMock.getEntries).toHaveBeenCalledWith(expect.objectContaining({
      companyId: 'comp-1',
      bankAccountId: 'acc-bank',
      includeCleared: true,
    }));
    expect(comp.clearedCount()).toBe(1);
    expect(comp.unclearedCount()).toBe(1);
    expect(comp.netAmount()).toBe(750);
    expect(resolveBankClearanceSignedAmount({ debit: 0, credit: 250 })).toBe(-250);
  });

  it('drills down using sourceDocumentId by documentType', () => {
    comp.drillDown({ documentType: 'PaymentEntry', documentId: 'pe-1', sourceDocumentId: 'pe-1' });
    expect(routerMock.navigate).toHaveBeenCalledWith(['/accounting/payments', 'pe-1']);

    comp.drillDown({ documentType: 'PurchaseInvoice', documentId: 'je-2', sourceDocumentId: 'pi-2' });
    expect(routerMock.navigate).toHaveBeenCalledWith(['/purchasing/invoices', 'pi-2']);

    comp.drillDown({ documentType: 'SalesInvoice', documentId: 'je-3', sourceDocumentId: 'si-3' });
    expect(routerMock.navigate).toHaveBeenCalledWith(['/sales/invoices', 'si-3']);

    comp.drillDown({ documentType: 'JournalEntry', documentId: 'je-4' });
    expect(routerMock.navigate).toHaveBeenCalledWith(['/accounting/journal-entries', 'je-4']);
  });
});
