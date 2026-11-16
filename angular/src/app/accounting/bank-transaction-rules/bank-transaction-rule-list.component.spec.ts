import { Injector, runInInjectionContext } from '@angular/core';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { BankTransactionRuleListComponent } from './bank-transaction-rule-list.component';
import { BankTransactionRuleService } from '../../proxy/accounting/bank-transaction-rule.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

describe('BankTransactionRuleListComponent', () => {
  function createComponent(companyId: string | null = 'comp-1') {
    const ruleServiceMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'r-1', name: 'Grab', priority: 2 }], totalCount: 1 })),
      getNextPriority: vi.fn().mockReturnValue(of(10)),
      reorderPriorities: vi.fn().mockReturnValue(of(void 0)),
    };
    const toasterMock = { success: vi.fn(), warn: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: BankTransactionRuleService, useValue: ruleServiceMock },
        { provide: ToasterService, useValue: toasterMock },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => companyId } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new BankTransactionRuleListComponent());
    comp.ngOnInit();
    return { comp, ruleServiceMock, toasterMock };
  }

  it('loads next priority when opening create rule form', () => {
    const { comp, ruleServiceMock } = createComponent();
    expect(comp.showCreateForm).toBe(false);

    comp.toggleCreateForm();

    expect(comp.showCreateForm).toBe(true);
    expect(ruleServiceMock.getNextPriority).toHaveBeenCalledWith('comp-1');
    expect(comp.nextPriority).toBe(10);
  });

  it('reorders priorities for current company and reloads list', () => {
    const { comp, ruleServiceMock, toasterMock } = createComponent();
    ruleServiceMock.getList.mockClear();

    comp.reorderRules();

    expect(ruleServiceMock.reorderPriorities).toHaveBeenCalledWith('comp-1');
    expect(toasterMock.success).toHaveBeenCalledWith('::PrioritiesReordered');
    expect(ruleServiceMock.getList).toHaveBeenCalled();
  });

  it('warns when reordering without active company', () => {
    const { comp, ruleServiceMock, toasterMock } = createComponent(null);
    comp.reorderRules();
    expect(ruleServiceMock.reorderPriorities).not.toHaveBeenCalled();
    expect(toasterMock.warn).toHaveBeenCalledWith('::PleaseSelectCompanyFirst');
  });
});
