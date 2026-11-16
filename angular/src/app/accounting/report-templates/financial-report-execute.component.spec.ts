import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { FinancialReportExecuteComponent } from './financial-report-execute.component';
import { FinancialReportTemplateService } from '../../proxy/accounting/financial-report-template.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

describe('FinancialReportExecuteComponent', () => {
  it('loads filtered accounts when toggling accounts preview and applies root type / category filters', () => {
    const accounts = [
      { id: 'acc-1', accountCode: '1100', accountName: 'Cash', rootType: 'Asset', currency: 'MYR' },
    ];
    const svcMock = {
      getList: vi.fn().mockReturnValue(of({
        items: [{ id: 'tpl-1', name: 'Balance Sheet', isEnabled: true, rows: [{ accountCategoryFilter: 'Current Assets' }] }],
      })),
      getFilteredAccounts: vi.fn().mockReturnValue(of(accounts)),
    };
    const injector = Injector.create({
      providers: [
        { provide: FinancialReportTemplateService, useValue: svcMock },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        { provide: ToasterService, useValue: { warn: vi.fn(), error: vi.fn() } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
      ],
    });
    const comp = runInInjectionContext(injector, () => new FinancialReportExecuteComponent());
    comp.ngOnInit();
    comp.selectedTemplateId = 'tpl-1';

    expect(comp.templateCategories()).toEqual(['Current Assets']);

    comp.accountRootTypeFilter = 'Asset';
    comp.accountCategoryFilter = 'Current Assets';
    comp.toggleAccountsPreview();

    expect(comp.showAccountsPreview()).toBe(true);
    expect(svcMock.getFilteredAccounts).toHaveBeenCalledWith({
      companyId: 'comp-1',
      accountRows: [
        { field: 'root_type', operator: '=', value: 'Asset' },
        { field: 'account_category', operator: '=', value: 'Current Assets' },
      ],
    });
    expect(comp.filteredAccounts()).toEqual(accounts);
  });
});
