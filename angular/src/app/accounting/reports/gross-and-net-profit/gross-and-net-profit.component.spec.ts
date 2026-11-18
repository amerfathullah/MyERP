import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CompanyService } from '../../../proxy/core/company.service';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import {
  GrossAndNetProfitComponent,
  buildGrossAndNetProfitCsvRows,
} from './gross-and-net-profit.component';

describe('GrossAndNetProfitComponent', () => {
  it('formats multi-period gross and net profit rows for CSV export', () => {
    const csv = buildGrossAndNetProfitCsvRows({
      periodLabels: ['Jan 2026', 'Feb 2026'],
      rows: [
        {
          accountCode: '4100',
          accountName: 'Direct Sales',
          indent: 1,
          rowKind: 'Account',
          periodAmounts: [1000, 1500],
          total: 2500,
        },
        {
          accountName: 'Gross Profit',
          indent: 0,
          rowKind: 'GrossProfit',
          periodAmounts: [600, 900],
          total: 1500,
        },
      ],
    });

    expect(csv).toHaveLength(2);
    expect(csv[0]['Account']).toBe('  4100 - Direct Sales');
    expect(csv[0]['Jan 2026']).toBe('1000.00');
    expect(csv[1]['Total']).toBe('1500.00');
  });

  it('calls getGrossAndNetProfit with selected periodicity and accumulated flag', () => {
    const reportingMock = {
      getGrossAndNetProfit: vi.fn().mockReturnValue(
        of({
          companyId: 'co-1',
          currencyCode: 'MYR',
          grossProfit: 1200,
          netProfit: 800,
          periodLabels: ['2026-01-01 - 2026-12-31'],
          rows: [],
        }),
      ),
    };

    const injector = Injector.create({
      providers: [
        FormBuilder,
        { provide: ReportingService, useValue: reportingMock },
        { provide: CompanyService, useValue: { getList: () => of({ items: [] }) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'co-1' } },
        { provide: ToasterService, useValue: { warn: vi.fn(), error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new GrossAndNetProfitComponent());
    comp.filters.patchValue({
      companyId: 'co-1',
      fromDate: '2026-01-01',
      toDate: '2026-12-31',
      periodicity: 'Quarterly',
      accumulatedValues: true,
    });
    comp.generate();

    expect(reportingMock.getGrossAndNetProfit).toHaveBeenCalledWith({
      companyId: 'co-1',
      fromDate: '2026-01-01',
      toDate: '2026-12-31',
      periodicity: 'Quarterly',
      accumulatedValues: true,
    });
    expect(comp.report()?.grossProfit).toBe(1200);
    expect(comp.report()?.netProfit).toBe(800);
  });
});
