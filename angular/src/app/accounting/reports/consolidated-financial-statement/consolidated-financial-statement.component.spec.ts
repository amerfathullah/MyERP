import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import {
  ConsolidatedFinancialStatementComponent,
  buildConsolidatedStatementCsvRows,
} from './consolidated-financial-statement.component';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('ConsolidatedFinancialStatementComponent', () => {
  it('calls getConsolidatedFinancialStatement and builds multi-company CSV rows', () => {
    const sampleReport = {
      rootCompanyId: 'hold-1',
      reportType: 'BalanceSheet',
      presentationCurrency: 'MYR',
      accumulatedInGroupCompany: true,
      companyColumns: [
        { companyId: 'hold-1', companyName: 'Holdings', currencyCode: 'MYR', isGroup: true },
        { companyId: 'sub-1', companyName: 'US Sub', currencyCode: 'USD', isGroup: false },
      ],
      rows: [
        {
          accountKey: '1100',
          accountCode: '1100',
          accountName: 'Cash',
          section: 'Asset',
          level: 0,
          isGroup: false,
          isTotalRow: false,
          companyAmounts: { 'hold-1': 550, 'sub-1': 100 },
          total: 550,
        },
      ],
      summary: [{ label: 'Total Assets', value: 550, currencyCode: 'MYR' }],
    };

    const reportingMock = {
      getConsolidatedFinancialStatement: vi.fn().mockReturnValue(of(sampleReport)),
    };
    const companyMock = {
      getList: vi.fn().mockReturnValue(of({
        items: [{ id: 'hold-1', name: 'Holdings', currencyCode: 'MYR', isGroup: true }],
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: ReportingService, useValue: reportingMock },
        { provide: CompanyService, useValue: companyMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'hold-1' } },
        { provide: ToasterService, useValue: { error: vi.fn(), warn: vi.fn(), success: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new ConsolidatedFinancialStatementComponent());
    comp.ngOnInit();

    expect(reportingMock.getConsolidatedFinancialStatement).toHaveBeenCalledWith(
      expect.objectContaining({ companyId: 'hold-1', reportType: 'BalanceSheet' }),
    );
    expect(comp.report()?.companyColumns?.length).toBe(2);

    const csv = buildConsolidatedStatementCsvRows(sampleReport);
    expect(csv[0]['Holdings (MYR)']).toBe('550.00');
    expect(csv[0]['US Sub (USD)']).toBe('100.00');
    expect(csv[0]['Total (MYR)']).toBe('550.00');
  });
});
