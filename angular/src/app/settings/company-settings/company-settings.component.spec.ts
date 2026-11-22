import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { CompanySettingsComponent } from './company-settings.component';
import { CompanyService } from '../../proxy/core/company.service';
import { AccountService } from '../../proxy/accounting/account.service';
import { CostCenterService } from '../../proxy/accounting/cost-center.service';

describe('CompanySettingsComponent', () => {
  it('patches and saves Expenses Added To Stock accounts on company select and submit', () => {
    const companyServiceMock = {
      getList: vi.fn().mockReturnValue(of({
        items: [
          {
            id: 'comp-1',
            name: 'Test Co',
            currencyCode: 'MYR',
            enablePerpetualInventory: true,
            expensesAddedToStockAccountId: 'acc-exp-stock',
            expensesAddedToStockContraAccountId: 'acc-exp-contra',
          },
        ],
      })),
      updateSettings: vi.fn().mockReturnValue(of(void 0)),
    };
    const accountServiceMock = { getList: vi.fn().mockReturnValue(of({ items: [] })) };
    const costCenterServiceMock = { getList: vi.fn().mockReturnValue(of({ items: [] })) };
    const toasterMock = { success: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: CompanyService, useValue: companyServiceMock },
        { provide: AccountService, useValue: accountServiceMock },
        { provide: CostCenterService, useValue: costCenterServiceMock },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new CompanySettingsComponent());
    comp.ngOnInit();
    comp.onCompanySelect('comp-1');

    expect(comp.form.get('expensesAddedToStockAccountId')?.value).toBe('acc-exp-stock');
    expect(comp.form.get('expensesAddedToStockContraAccountId')?.value).toBe('acc-exp-contra');

    comp.form.patchValue({
      expensesAddedToStockAccountId: 'acc-new-stock',
      expensesAddedToStockContraAccountId: 'acc-new-contra',
    });
    comp.save();

    expect(companyServiceMock.updateSettings).toHaveBeenCalledWith(
      'comp-1',
      expect.objectContaining({
        expensesAddedToStockAccountId: 'acc-new-stock',
        expensesAddedToStockContraAccountId: 'acc-new-contra',
      }),
    );
    expect(toasterMock.success).toHaveBeenCalled();
  });
});
