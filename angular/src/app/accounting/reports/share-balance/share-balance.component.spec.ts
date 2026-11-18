import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import {
  ShareBalanceComponent,
  buildShareBalanceCsvRows,
} from './share-balance.component';
import { ShareReportingService } from '../../../proxy/accounting/share-reporting.service';
import { ShareholderService } from '../../../proxy/accounting/shareholder.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('ShareBalanceComponent', () => {
  it('formats share balance rows for CSV export', () => {
    const csv = buildShareBalanceCsvRows([
      {
        shareholderTitle: 'Iron Man',
        shareTypeTitle: 'Equity',
        noOfShares: 200,
        averageRate: 15,
        amount: 3000,
      },
    ]);
    expect(csv).toHaveLength(1);
    expect(csv[0]['Shareholder']).toBe('Iron Man');
    expect(csv[0]['No of Shares']).toBe(200);
    expect(csv[0]['Average Rate']).toBe('15.0000');
    expect(csv[0]['Amount']).toBe('3000.00');
  });

  it('calls getShareBalance with date, companyId, and shareholderId filters', () => {
    const svcMock = {
      getShareBalance: vi.fn().mockReturnValue(of({
        date: '2026-06-15',
        shareholderId: 's1',
        shareholderTitle: 'Iron Man',
        companyId: 'c1',
        rows: [
          {
            shareholderId: 's1',
            shareholderTitle: 'Iron Man',
            shareTypeId: 'st1',
            shareTypeTitle: 'Equity',
            noOfShares: 60,
            averageRate: 10,
            amount: 600,
          },
        ],
        totalNoOfShares: 60,
        totalAmount: 600,
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: ShareReportingService, useValue: svcMock },
        {
          provide: ShareholderService,
          useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 's1', title: 'Iron Man' }] })) },
        },
        {
          provide: CompanyService,
          useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'c1', name: 'Main Co' }] })) },
        },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'c1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new ShareBalanceComponent());
    comp.ngOnInit();
    comp.filters.patchValue({ date: '2026-06-15', shareholderId: 's1' });
    comp.generate();

    expect(svcMock.getShareBalance).toHaveBeenLastCalledWith({
      date: '2026-06-15',
      companyId: 'c1',
      shareholderId: 's1',
    });
    expect(comp.report()?.totalAmount).toBe(600);
  });
});
