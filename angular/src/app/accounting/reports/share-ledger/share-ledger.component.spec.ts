import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import {
  ShareLedgerComponent,
  buildShareLedgerCsvRows,
} from './share-ledger.component';
import { ShareReportingService } from '../../../proxy/accounting/share-reporting.service';
import { ShareholderService } from '../../../proxy/accounting/shareholder.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('ShareLedgerComponent', () => {
  it('formats share ledger rows for CSV export', () => {
    const csv = buildShareLedgerCsvRows([
      {
        shareholderTitle: 'Alice',
        date: '2026-06-15T00:00:00Z',
        transferType: 'Transfer to Bob',
        shareTypeTitle: 'Equity',
        fromNo: 1,
        toNo: 40,
        noOfShares: 40,
        rate: 10,
        amount: 400,
        companyName: 'Main Co',
        shareTransferId: 'st-1',
      },
    ]);
    expect(csv).toHaveLength(1);
    expect(csv[0]['Date']).toBe('2026-06-15');
    expect(csv[0]['Transfer Type']).toBe('Transfer to Bob');
    expect(csv[0]['No of Shares']).toBe(40);
    expect(csv[0]['Amount']).toBe('400.00');
  });

  it('calls getShareLedger with date, companyId, and shareholderId filters', () => {
    const svcMock = {
      getShareLedger: vi.fn().mockReturnValue(of({
        date: '2026-06-30',
        shareholderId: 's1',
        shareholderTitle: 'Alice',
        companyId: 'c1',
        rows: [
          {
            shareholderId: 's1',
            shareholderTitle: 'Alice',
            date: '2026-06-01T00:00:00Z',
            transferType: 'Issue',
            shareTypeTitle: 'Equity',
            fromNo: 1,
            toNo: 100,
            noOfShares: 100,
            rate: 10,
            amount: 1000,
            companyName: 'Main Co',
            shareTransferId: 't1',
          },
        ],
        totalNoOfShares: 100,
        totalAmount: 1000,
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: ShareReportingService, useValue: svcMock },
        {
          provide: ShareholderService,
          useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 's1', title: 'Alice' }] })) },
        },
        {
          provide: CompanyService,
          useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'c1', name: 'Main Co' }] })) },
        },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'c1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new ShareLedgerComponent());
    comp.ngOnInit();
    comp.filters.patchValue({ date: '2026-06-30', shareholderId: 's1' });
    comp.generate();

    expect(svcMock.getShareLedger).toHaveBeenLastCalledWith({
      date: '2026-06-30',
      companyId: 'c1',
      shareholderId: 's1',
    });
    expect(comp.report()?.totalNoOfShares).toBe(100);
  });
});
