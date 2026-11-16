import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { SubcontractingInwardFormComponent } from './subcontracting-inward-form.component';
import { SubcontractingInwardOrderService } from '../../proxy/purchasing/subcontracting-inward-order.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

describe('SubcontractingInwardFormComponent', () => {
  let inwardMock: any;
  let toasterMock: any;

  function createComponent(): SubcontractingInwardFormComponent {
    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: SubcontractingInwardOrderService, useValue: inwardMock },
        { provide: SupplierService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: ItemService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: WarehouseService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: SalesOrderService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: CompanyContextService, useValue: { selectedCompanyId: signal('comp-1') } },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });
    return runInInjectionContext(injector, () => new SubcontractingInwardFormComponent());
  }

  beforeEach(() => {
    inwardMock = {
      mapFromSalesOrder: vi.fn().mockReturnValue(of({
        currencyCode: 'USD',
        items: [
          { itemId: 'item-1', warehouseId: 'wh-1', quantity: 5, rate: 40, serviceCostPerQty: 12 },
          { itemId: 'item-2', warehouseId: null, quantity: 2, rate: 25, serviceCostPerQty: 5 },
        ],
      })),
      create: vi.fn().mockReturnValue(of({ id: 'scio-1' })),
    };
    toasterMock = { success: vi.fn(), error: vi.fn() };
  });

  it('populates currency and items from Sales Order via mapFromSalesOrder', () => {
    const comp = createComponent();
    comp.ngOnInit();
    comp.form.patchValue({ supplierId: 'sup-1', salesOrderId: 'so-1' });

    comp.mapFromSalesOrder();

    expect(inwardMock.mapFromSalesOrder).toHaveBeenCalledWith({ salesOrderId: 'so-1', supplierId: 'sup-1' });
    expect(comp.form.get('currencyCode')?.value).toBe('USD');
    expect(comp.itemsArray.length).toBe(2);
    expect(comp.grandTotal()).toBe(250);
    expect(comp.mappingFromSo()).toBe(false);
    expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullyUpdated');
  });

  it('resets loading and shows error when mapFromSalesOrder fails', () => {
    inwardMock.mapFromSalesOrder.mockReturnValue(throwError(() => ({ error: { error: { message: 'SO not found' } } })));
    const comp = createComponent();
    comp.ngOnInit();
    comp.form.patchValue({ supplierId: 'sup-1', salesOrderId: 'so-bad' });

    comp.mapFromSalesOrder();

    expect(comp.mappingFromSo()).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('SO not found');
  });
});
