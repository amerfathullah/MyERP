import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import {
  InvalidLedgerEntriesComponent,
  buildInvalidLedgerEntriesCsvRows,
} from './invalid-ledger-entries.component';
import { InvalidLedgerEntriesService } from '../../../proxy/accounting/invalid-ledger-entries.service';
import { AccountService } from '../../../proxy/accounting/account.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('InvalidLedgerEntriesComponent', () => {
  it('formats invalid ledger entries for CSV export', () => {
    const csv = buildInvalidLedgerEntriesCsvRows([
      {
        voucherType: 'SalesInvoice',
        voucherNo: 'SI-0099',
        ledgerSource: 'GL Entry, Payment Ledger Entry',
        reason: 'Cancelled',
      },
    ]);
    expect(csv).toHaveLength(1);
    expect(csv[0]['Voucher Type']).toBe('SalesInvoice');
    expect(csv[0]['Voucher No']).toBe('SI-0099');
    expect(csv[0]['Reason']).toBe('Cancelled');
  });

  it('calls getReport with selected filters', () => {
    const svcMock = {
      getReport: vi.fn().mockReturnValue(of({
        companyId: 'c1',
        fromDate: '2026-01-01',
        toDate: '2026-12-31',
        rows: [
          {
            voucherType: 'SalesInvoice',
            voucherId: 'v1',
            voucherNo: 'SI-0099',
            ledgerSource: 'GL Entry',
            reason: 'Cancelled',
          },
        ],
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: InvalidLedgerEntriesService, useValue: svcMock },
        { provide: AccountService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'a1', accountCode: '1200', accountName: 'Debtors' }] })) } },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'c1', name: 'Main Co' }] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'c1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new InvalidLedgerEntriesComponent());
    comp.ngOnInit();
    comp.filters.patchValue({ accountId: 'a1', voucherNo: 'SI-0099' });
    comp.generate();

    expect(svcMock.getReport).toHaveBeenLastCalledWith(expect.objectContaining({
      companyId: 'c1',
      accountIds: ['a1'],
      voucherNo: 'SI-0099',
    }));
    expect(comp.report()?.rows).toHaveLength(1);
  });
});
