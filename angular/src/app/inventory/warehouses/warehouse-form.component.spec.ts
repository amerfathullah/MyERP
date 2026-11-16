import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { describe, expect, it, vi } from 'vitest';
import { of } from 'rxjs';
import { WarehouseFormComponent } from './warehouse-form.component';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { WarehouseAccountService } from '../../proxy/inventory/warehouse-account.service';
import { CompanyService } from '../../proxy/core/company.service';
import { AccountService } from '../../proxy/accounting/account.service';

describe('WarehouseFormComponent', () => {
  function createComponent(editId: string | null = 'wh-1') {
    const warehouseMock = {
      get: vi.fn().mockReturnValue(of({ id: 'wh-1', companyId: 'co-1', name: 'Main Store' })),
      create: vi.fn().mockReturnValue(of({ id: 'wh-new', companyId: 'co-1', name: 'New Store' })),
      update: vi.fn().mockReturnValue(of({ id: 'wh-1', companyId: 'co-1', name: 'Main Store' })),
    };
    const whAccountMock = {
      getList: vi.fn().mockReturnValue(of({
        items: [{
          id: 'wa-1',
          warehouseId: 'wh-1',
          companyId: 'co-1',
          accountId: 'acc-stock',
          stockReceivedButNotBilledAccountId: 'acc-srbnb',
          stockDeliveredButNotBilledAccountId: 'acc-sdbnb',
          stockAdjustmentAccountId: 'acc-adj',
        }],
      })),
      save: vi.fn().mockReturnValue(of({ id: 'wa-1' })),
      delete: vi.fn().mockReturnValue(of(void 0)),
    };
    const routerMock = { navigate: vi.fn() };
    const toasterMock = { success: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        FormBuilder,
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => editId } } } },
        { provide: Router, useValue: routerMock },
        { provide: WarehouseService, useValue: warehouseMock },
        { provide: WarehouseAccountService, useValue: whAccountMock },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: AccountService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new WarehouseFormComponent());
    return { comp, warehouseMock, whAccountMock, routerMock, toasterMock };
  }

  it('loads and saves perpetual inventory WarehouseAccount mapping on edit', () => {
    const { comp, warehouseMock, whAccountMock, routerMock, toasterMock } = createComponent('wh-1');
    comp.ngOnInit();

    expect(comp.warehouseAccountId()).toBe('wa-1');
    expect(comp.form.value.perpetualAccountId).toBe('acc-stock');
    expect(comp.form.value.stockReceivedButNotBilledAccountId).toBe('acc-srbnb');

    comp.save();

    expect(warehouseMock.update).toHaveBeenCalled();
    expect(whAccountMock.save).toHaveBeenCalledWith({
      warehouseId: 'wh-1',
      companyId: 'co-1',
      accountId: 'acc-stock',
      stockReceivedButNotBilledAccountId: 'acc-srbnb',
      stockDeliveredButNotBilledAccountId: 'acc-sdbnb',
      stockAdjustmentAccountId: 'acc-adj',
    });
    expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullySaved');
    expect(routerMock.navigate).toHaveBeenCalledWith(['/inventory/warehouses']);
  });

  it('deletes saved WarehouseAccount mapping via clearPerpetualAccounts', () => {
    const { comp, whAccountMock, toasterMock } = createComponent('wh-1');
    comp.ngOnInit();

    comp.clearPerpetualAccounts();

    expect(whAccountMock.delete).toHaveBeenCalledWith('wa-1');
    expect(comp.warehouseAccountId()).toBeNull();
    expect(comp.form.value.perpetualAccountId).toBeNull();
    expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullyDeleted');
  });
});
