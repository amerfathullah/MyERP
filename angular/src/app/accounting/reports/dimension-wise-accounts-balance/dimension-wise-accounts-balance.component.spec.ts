import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import {
  DimensionWiseAccountsBalanceComponent,
  buildDimensionWiseBalanceCsvRows,
} from './dimension-wise-accounts-balance.component';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('DimensionWiseAccountsBalanceComponent', () => {
  it('formats dimension-wise account balances for CSV export', () => {
    const csv = buildDimensionWiseBalanceCsvRows(
      [{ accountName: '5000 - Travel', dimensionBalances: [120, 80], total: 200 }],
      [
        { dimensionId: 'd1', dimensionName: 'Main CC' },
        { dimensionId: 'd2', dimensionName: 'Sales CC' },
      ],
    );
    expect(csv).toHaveLength(1);
    expect(csv[0]['Account']).toBe('5000 - Travel');
    expect(csv[0]['Main CC']).toBe('120.00');
    expect(csv[0]['Total']).toBe('200.00');
  });

  it('calls getDimensionWiseAccountsBalance with selected dimension and filters', () => {
    const svcMock = {
      getDimensionWiseAccountsBalance: vi.fn().mockReturnValue(of({
        dimension: 'Project',
        currencyCode: 'MYR',
        dimensions: [{ dimensionId: 'p1', dimensionName: 'ERP Rollout' }],
        rows: [],
        dimensionTotals: [500],
        grandTotal: 500,
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: ReportingService, useValue: svcMock },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'c1', name: 'Main' }] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'c1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new DimensionWiseAccountsBalanceComponent());
    comp.ngOnInit();
    comp.filters.patchValue({ dimension: 'Project', showZeroValues: true });
    comp.generate();

    expect(svcMock.getDimensionWiseAccountsBalance).toHaveBeenLastCalledWith(expect.objectContaining({
      companyId: 'c1',
      dimension: 'Project',
      showZeroValues: true,
    }));
    expect(comp.report()?.grandTotal).toBe(500);
  });
});
