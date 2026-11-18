import { describe, it, expect, vi, beforeEach } from 'vitest';
import { of } from 'rxjs';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Injector, runInInjectionContext } from '@angular/core';
import { ToasterService } from '@abp/ng.theme.shared';
import {
  VoucherWiseBalanceComponent,
  resolveVoucherRowDifference,
} from './voucher-wise-balance.component';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('VoucherWiseBalanceComponent', () => {
  let reportingMock: { getVoucherWiseBalance: ReturnType<typeof vi.fn> };
  let companyMock: { getList: ReturnType<typeof vi.fn> };
  let routerMock: { navigate: ReturnType<typeof vi.fn> };
  let comp: VoucherWiseBalanceComponent;

  beforeEach(() => {
    reportingMock = {
      getVoucherWiseBalance: vi.fn().mockReturnValue(of({
        companyId: 'comp-1',
        currencyCode: 'MYR',
        unbalancedCount: 1,
        totalDebit: 500,
        totalCredit: 450,
        rows: [
          {
            voucherType: 'JournalEntry',
            voucherId: 'je-1',
            voucherNo: 'JE-0009',
            postingDate: '2026-11-18T00:00:00Z',
            debit: 500,
            credit: 450,
            difference: 50,
          },
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
        { provide: ReportingService, useValue: reportingMock },
        { provide: CompanyService, useValue: companyMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    comp = runInInjectionContext(injector, () => new VoucherWiseBalanceComponent());
  });

  it('loads default company and renders unbalanced voucher totals', () => {
    comp.ngOnInit();

    expect(reportingMock.getVoucherWiseBalance).toHaveBeenCalledWith(expect.objectContaining({
      companyId: 'comp-1',
    }));
    expect(comp.rows().length).toBe(1);
    expect(comp.totalDifference()).toBe(50);
    expect(resolveVoucherRowDifference({ debit: 300, credit: 200 })).toBe(100);
  });

  it('drills down to general ledger for selected voucher', () => {
    comp.filters.patchValue({ companyId: 'comp-1', fromDate: '2026-11-01', toDate: '2026-11-18' });
    comp.drillDown({ voucherType: 'JournalEntry', voucherId: 'je-1', voucherNo: 'JE-0009', debit: 500, credit: 450 });

    expect(routerMock.navigate).toHaveBeenCalledWith(
      ['/accounting/reports/general-ledger'],
      {
        queryParams: {
          companyId: 'comp-1',
          fromDate: '2026-11-01',
          toDate: '2026-11-18',
          voucherNumber: 'JE-0009',
        },
      },
    );
  });
});
