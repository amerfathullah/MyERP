import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { LocalizationService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { AccountingDimensionsComponent } from './accounting-dimensions.component';
import { AccountingDimensionService } from '../../proxy/accounting/accounting-dimension.service';
import { AccountService } from '../../proxy/accounting/account.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

describe('AccountingDimensionsComponent Filters', () => {
  function createComponent() {
    const dimensionServiceMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'dim-1', documentType: 'Branch', label: 'Branch', fieldName: 'branch', isMandatory: false, isEnabled: true }] })),
      getEnabledDimensions: vi.fn().mockReturnValue(of([{ id: 'dim-1', documentType: 'Branch', label: 'Branch', fieldName: 'branch', isMandatory: true, isEnabled: true }])),
      getFilters: vi.fn().mockReturnValue(of([{ id: 'flt-1', accountingDimensionId: 'dim-1', companyId: 'comp-1', accountId: 'acc-1', isAllowList: true, dimensionValueIds: 'b-1,b-2' }])),
      createFilter: vi.fn().mockReturnValue(of({ id: 'flt-2' })),
      deleteFilter: vi.fn().mockReturnValue(of(void 0)),
    };
    const accountServiceMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'acc-1', accountCode: '4000', accountName: 'Sales Revenue' }] })),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };
    const confirmationMock = { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) };

    const injector = Injector.create({
      providers: [
        { provide: AccountingDimensionService, useValue: dimensionServiceMock },
        { provide: AccountService, useValue: accountServiceMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: ToasterService, useValue: toasterMock },
        { provide: ConfirmationService, useValue: confirmationMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new AccountingDimensionsComponent());
    comp.ngOnInit();
    return { comp, dimensionServiceMock, accountServiceMock, toasterMock };
  }

  it('loads dimension filters for active company when selecting a dimension', () => {
    const { comp, dimensionServiceMock } = createComponent();
    const dim = comp.dimensions()[0];

    comp.selectDimensionForFilters(dim);

    expect(dimensionServiceMock.getFilters).toHaveBeenCalledWith('dim-1', 'comp-1');
    expect(comp.filters()).toHaveLength(1);
    expect(comp.getAccountLabel('acc-1')).toBe('4000 Sales Revenue');
  });

  it('creates and deletes dimension filters and refreshes list', () => {
    const { comp, dimensionServiceMock, toasterMock } = createComponent();
    comp.selectDimensionForFilters(comp.dimensions()[0]);
    comp.filterForm.patchValue({ accountId: 'acc-1', isAllowList: false, dimensionValueIds: 'b-9' });

    comp.createFilter();

    expect(dimensionServiceMock.createFilter).toHaveBeenCalledWith({
      accountingDimensionId: 'dim-1',
      companyId: 'comp-1',
      accountId: 'acc-1',
      isAllowList: false,
      dimensionValueIds: 'b-9',
    });
    expect(toasterMock.success).toHaveBeenCalled();

    comp.deleteFilter('flt-1');
    expect(dimensionServiceMock.deleteFilter).toHaveBeenCalledWith('flt-1');
  });

  it('loads enabled dimensions for active company on init', () => {
    const { comp, dimensionServiceMock } = createComponent();
    expect(dimensionServiceMock.getEnabledDimensions).toHaveBeenCalledWith('comp-1');
    expect(comp.enabledDimensions()).toHaveLength(1);
    expect(comp.enabledDimensions()[0].label).toBe('Branch');
  });
});
