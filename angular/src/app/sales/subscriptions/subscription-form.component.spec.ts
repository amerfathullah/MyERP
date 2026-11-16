import { Injector, runInInjectionContext } from '@angular/core';
import { Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { SubscriptionFormComponent } from './subscription-form.component';
import { SubscriptionService } from '../../proxy/sales/subscription.service';
import { CompanyService } from '../../proxy/core/company.service';
import { MasterDataService } from '../../proxy/core/master-data.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

describe('SubscriptionFormComponent', () => {
  function createComponent() {
    const subMock = {
      getPlanDimensions: vi.fn().mockReturnValue(of({ costCenterId: 'cc-sales' })),
      create: vi.fn().mockReturnValue(of({ id: 'sub-1' })),
    };
    const masterDataMock = {
      getCostCenters: vi.fn().mockReturnValue(of([{ id: 'cc-sales', name: 'Sales Department' }])),
    };
    const companyMock = { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'comp-1', name: 'Main Co' }] })) };
    const customerMock = { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'cust-1', name: 'Acme Corp' }] })) };
    const supplierMock = { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'sup-1', name: 'Vendor X' }] })) };
    const itemMock = {
      getList: vi.fn().mockReturnValue(of({
        items: [{ id: 'item-1', itemCode: 'SUB-PRO', itemName: 'Pro Plan', standardSellingPrice: 99, standardBuyingPrice: 40 }],
      })),
    };
    const routerMock = { navigate: vi.fn() };
    const toasterMock = { success: vi.fn(), error: vi.fn(), warn: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: SubscriptionService, useValue: subMock },
        { provide: CompanyService, useValue: companyMock },
        { provide: MasterDataService, useValue: masterDataMock },
        { provide: CustomerService, useValue: customerMock },
        { provide: SupplierService, useValue: supplierMock },
        { provide: ItemService, useValue: itemMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: ToasterService, useValue: toasterMock },
        { provide: Router, useValue: routerMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new SubscriptionFormComponent());
    return { comp, subMock, masterDataMock, routerMock, toasterMock };
  }

  it('loads cost centers via MasterDataService and resolves plan dimensions on item selection', () => {
    const { comp, subMock, masterDataMock } = createComponent();
    comp.ngOnInit();

    expect(masterDataMock.getCostCenters).toHaveBeenCalledWith('comp-1');
    comp.form.plans[0].itemId = 'item-1';
    comp.onPlanItemChange(0);

    expect(subMock.getPlanDimensions).toHaveBeenCalledWith('item-1', 'comp-1', 'Customer');
    expect(comp.form.plans[0].itemName).toBe('Pro Plan');
    expect(comp.form.plans[0].rate).toBe(99);
    expect(comp.form.plans[0].costCenterId).toBe('cc-sales');
    expect(comp.form.plans[0].costCenterName).toBe('Sales Department');
  });

  it('submits CreateSubscriptionDto with companyId, partyId, and plan itemId', () => {
    const { comp, subMock, routerMock, toasterMock } = createComponent();
    comp.ngOnInit();

    comp.form.partyId = 'cust-1';
    comp.onPartyChange();
    comp.form.plans[0].itemId = 'item-1';
    comp.onPlanItemChange(0);

    expect(comp.isValid()).toBe(true);
    comp.save();

    expect(subMock.create).toHaveBeenCalledWith(expect.objectContaining({
      companyId: 'comp-1',
      partyId: 'cust-1',
      partyType: 'Customer',
      partyName: 'Acme Corp',
      plans: [{ itemId: 'item-1', itemName: 'Pro Plan', qty: 1, rate: 99 }],
    }));
    expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullyCreated');
    expect(routerMock.navigate).toHaveBeenCalledWith(['/sales/subscriptions']);
  });
});
