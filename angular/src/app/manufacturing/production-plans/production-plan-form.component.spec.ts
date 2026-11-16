import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { ProductionPlanFormComponent } from './production-plan-form.component';
import { ProductionPlanService } from '../../proxy/manufacturing/production-plan.service';
import { CompanyService } from '../../proxy/core/company.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';

describe('ProductionPlanFormComponent', () => {
  let serviceMock: any;
  let toasterMock: any;
  let routeQuery: Record<string, string | null>;

  function createComponent(): ProductionPlanFormComponent {
    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: (k: string) => routeQuery[k] ?? null } } } },
        { provide: ProductionPlanService, useValue: serviceMock },
        { provide: ToasterService, useValue: toasterMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: signal('comp-1') } },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: ItemService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: WarehouseService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: ManufacturingService, useValue: { getBomList: vi.fn().mockReturnValue(of({ items: [{ id: 'bom-1', itemId: 'item-1', isActive: true }] })) } },
      ],
    });
    return runInInjectionContext(injector, () => new ProductionPlanFormComponent());
  }

  beforeEach(() => {
    routeQuery = {};
    serviceMock = {
      getOpenSalesOrderItems: vi.fn().mockReturnValue(of([
        { itemId: 'item-1', itemCode: 'FG-01', itemName: 'Widget', bomId: null, unplannedQty: 15, warehouseId: 'wh-1' },
        { itemId: 'item-2', itemCode: 'FG-02', itemName: 'Zero', bomId: 'bom-2', unplannedQty: 0 },
      ])),
      create: vi.fn().mockReturnValue(of({ id: 'pp-1' })),
    };
    toasterMock = { success: vi.fn(), info: vi.fn(), warn: vi.fn(), error: vi.fn() };
  });

  it('loads unplanned open sales order items and deduplicates rows', () => {
    const comp = createComponent();
    comp.ngOnInit();

    comp.loadOpenSalesOrderItems();

    expect(serviceMock.getOpenSalesOrderItems).toHaveBeenCalledWith('comp-1', undefined);
    expect(comp.items.length).toBe(1);
    expect(comp.items.at(0).getRawValue()).toMatchObject({
      itemId: 'item-1',
      itemName: 'Widget',
      bomId: 'bom-1',
      plannedQty: 15,
      warehouseId: 'wh-1',
    });

    comp.loadOpenSalesOrderItems();
    expect(comp.items.length).toBe(1);
  });

  it('auto-loads open sales order items when salesOrderId query param is present', () => {
    routeQuery = { salesOrderId: 'so-9' };
    const comp = createComponent();
    comp.ngOnInit();

    expect(serviceMock.getOpenSalesOrderItems).toHaveBeenCalledWith('comp-1', ['so-9']);
    expect(comp.items.length).toBe(1);
  });

  it('shows info message when no unplanned items exist and handles errors', () => {
    serviceMock.getOpenSalesOrderItems.mockReturnValueOnce(of([]));
    const comp = createComponent();
    comp.form.patchValue({ companyId: 'comp-1' });

    comp.loadOpenSalesOrderItems();
    expect(toasterMock.info).toHaveBeenCalledWith('::NoOpenSalesOrderItems');

    serviceMock.getOpenSalesOrderItems.mockReturnValueOnce(throwError(() => new Error('boom')));
    comp.loadOpenSalesOrderItems();
    expect(comp.loadingSoItems()).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('::FailedToLoad');
  });
});
