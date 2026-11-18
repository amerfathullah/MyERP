import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { TrialBalanceComponent } from './trial-balance.component';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('TrialBalanceComponent', () => {
  function createComponent() {
    const reportingMock = {
      getTrialBalance: vi.fn().mockReturnValue(of({
        companyId: 'hold-1',
        currencyCode: 'MYR',
        isConsolidated: true,
        consolidatedCompanyCount: 2,
        totalDebit: 102.68,
        totalCredit: 102.68,
        rows: [
          { accountId: 'acc-cash', accountCode: '1100', accountName: 'Cash', accountType: 'Asset', debit: 101.34, credit: 0 },
          { accountId: '00000000-0000-0000-0000-000000000000', accountCode: 'FCTR', accountName: 'Foreign Currency Translation Reserve', accountType: 'Equity', debit: 0, credit: 0.01 },
        ],
      })),
    };
    const companyMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'hold-1', name: 'Holdings' }] })),
    };
    const routerMock = { navigate: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: ReportingService, useValue: reportingMock },
        { provide: CompanyService, useValue: companyMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'hold-1' } },
        { provide: ToasterService, useValue: { error: vi.fn(), warn: vi.fn(), success: vi.fn() } },
        { provide: Router, useValue: routerMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new TrialBalanceComponent());
    return { comp, reportingMock, routerMock };
  }

  it('tracks consolidated metadata and skips drill-down on synthetic FCTR row', () => {
    const { comp, reportingMock, routerMock } = createComponent();
    comp.filters.patchValue({
      companyId: 'hold-1',
      fromDate: '2026-01-01',
      toDate: '2026-06-30',
      includeSubsidiaries: true,
    });

    comp.generate();

    expect(reportingMock.getTrialBalance).toHaveBeenCalledWith({
      companyId: 'hold-1',
      asOfDate: '2026-06-30',
      includeSubsidiaries: true,
    });
    expect(comp.isConsolidated()).toBe(true);
    expect(comp.consolidatedCompanyCount()).toBe(2);
    expect(comp.reportCurrency()).toBe('MYR');
    expect(comp.isBalanced()).toBe(true);

    // FCTR synthetic row should not trigger navigation
    comp.drillDown(comp.data()[1]);
    expect(routerMock.navigate).not.toHaveBeenCalled();

    // Real account row drills down to GL
    comp.drillDown(comp.data()[0]);
    expect(routerMock.navigate).toHaveBeenCalledWith(
      ['/accounting/reports/general-ledger'],
      expect.objectContaining({ queryParams: expect.objectContaining({ accountId: 'acc-cash' }) }),
    );
  });
});
