import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import {
  DeferredRevenueAndExpenseComponent,
  buildDeferredRevenueCsvRows,
} from './deferred-revenue-and-expense.component';
import { DeferredReportingService } from '../../../proxy/accounting/deferred-reporting.service';
import { FiscalYearService } from '../../../proxy/accounting/fiscal-year.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('DeferredRevenueAndExpenseComponent', () => {
  it('formats deferred schedule rows and period columns for CSV export', () => {
    const csv = buildDeferredRevenueCsvRows(
      [
        {
          name: 'SI-0001',
          serviceStartDate: '2025-05-01T00:00:00Z',
          serviceEndDate: '2025-07-31T00:00:00Z',
          amount: 920,
          periodValues: [310, 300, 310],
        },
      ],
      [
        { key: 'may_2025', label: 'May 2025' },
        { key: 'jun_2025', label: 'Jun 2025' },
        { key: 'jul_2025', label: 'Jul 2025' },
      ],
    );
    expect(csv).toHaveLength(1);
    expect(csv[0]['Name']).toBe('SI-0001');
    expect(csv[0]['Service Start Date']).toBe('2025-05-01');
    expect(csv[0]['May 2025']).toBe('310.00');
    expect(csv[0]['Jul 2025']).toBe('310.00');
  });

  it('calls getDeferredRevenueAndExpense with selected periodicity, type, and forecast toggle', () => {
    const svcMock = {
      getDeferredRevenueAndExpense: vi.fn().mockReturnValue(of({
        companyId: 'c1',
        currencyCode: 'MYR',
        type: 'Expense',
        periodicity: 'Quarterly',
        withUpcomingPostings: false,
        periods: [{ key: 'q2_2025', label: 'Q2 2025', actualTotal: -300, expectedTotal: -300 }],
        rows: [],
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: DeferredReportingService, useValue: svcMock },
        {
          provide: FiscalYearService,
          useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'fy1', companyId: 'c1', name: 'FY2025' }] })) },
        },
        {
          provide: CompanyService,
          useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'c1', name: 'Main Co' }] })) },
        },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'c1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new DeferredRevenueAndExpenseComponent());
    comp.ngOnInit();
    comp.filters.patchValue({
      filterBasedOn: 'Date Range',
      periodicity: 'Quarterly',
      type: 'Expense',
      withUpcomingPostings: false,
    });
    comp.generate();

    expect(svcMock.getDeferredRevenueAndExpense).toHaveBeenLastCalledWith(expect.objectContaining({
      companyId: 'c1',
      filterBasedOn: 'Date Range',
      periodicity: 'Quarterly',
      type: 'Expense',
      withUpcomingPostings: false,
    }));
    expect(comp.report()?.periods?.[0]?.actualTotal).toBe(-300);
  });
});
